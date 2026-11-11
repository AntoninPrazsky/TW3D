using System;
using System.Collections.Generic;
using System.IO;

namespace TW3D.Audio
{
    /// <summary>A mode of resonance of a body (see <see cref="Synth.AddResonance"/>).</summary>
    /// <param name="Hz">The frequency it rings at.</param>
    /// <param name="Decay">How fast it dies away (1 / seconds).</param>
    /// <param name="Amplitude">How loud a push of 1 as short as a sample rings it.</param>
    public readonly record struct Mode(float Hz, float Decay, float Amplitude);

    /// <summary>
    /// The arithmetic the sound effects are made of (see <see cref="SoundBakes"/>): tones and noise added into a buffer of
    /// samples, filters, a reverb and the conversion to 16-bit PCM. Mono at <see cref="SampleRate"/>, values -1..1.
    /// After the procedural sound of BS3D.
    /// </summary>
    public static class Synth
    {
        public const int SampleRate = 44100;
        //Every tone and noise fades out over the last tenth of its length, at most this long, so it never stops with a click
        private const float MaxRelease = 0.03f;

        /// <summary>An empty buffer for a sound of the given length.</summary>
        public static float[] Buffer(float seconds)
        {
            return new float[(int)(seconds * SampleRate)];
        }

        /// <summary>
        /// Adds a tone: a fundamental gliding from one pitch to another (exponentially, as the ear hears pitch) and its
        /// harmonics, with an attack and an exponential decay.
        /// </summary>
        /// <param name="start">When the tone starts (seconds).</param>
        /// <param name="length">How long it lasts (seconds); it fades out over its last tenth.</param>
        /// <param name="decay">How fast it dies away (1 / seconds): 0 holds it, 20 is a short pluck.</param>
        /// <param name="harmonics">The amplitudes of the fundamental and of the harmonics above it (2×, 3×, …).</param>
        public static void AddTone(float[] signal, float start, float length, float fromHz, float toHz, float amplitude,
            float attack, float decay, params float[] harmonics)
        {
            int first = (int)(start * SampleRate);
            int count = Math.Min((int)(length * SampleRate), signal.Length - first);
            double phase = 0.0;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float frequency = fromHz * MathF.Pow(toHz / fromHz, t / length);
                phase += 2.0 * Math.PI * frequency / SampleRate;

                float value = 0f;
                for (int h = 0; h < harmonics.Length; h++)
                    value += harmonics[h] * MathF.Sin((float)(phase * (h + 1)));

                signal[first + i] += value * amplitude * Envelope(t, length, attack, decay);
            }
        }

        /// <summary>
        /// Adds noise through a band-pass filter (a whoosh or a knock), with an attack and an exponential decay. The middle
        /// of the band may glide over the length of the noise, as a tone does.
        /// </summary>
        /// <param name="q">How narrow the band is: 0.7 is wide, 3 is nearly a whistle.</param>
        /// <param name="seed">Two layers of noise in one sound need two seeds, or they are the same noise.</param>
        public static void AddNoise(float[] signal, float start, float length, float amplitude, float attack, float decay,
            float fromHz, float toHz, float q, int seed)
        {
            int first = (int)(start * SampleRate);
            int count = Math.Min((int)(length * SampleRate), signal.Length - first);
            //A second-order band-pass (constant peak gain), its coefficients following the gliding middle of the band
            double z1 = 0.0, z2 = 0.0;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float frequency = fromHz * MathF.Pow(toHz / fromHz, t / length);
                double w = 2.0 * Math.PI * frequency / SampleRate, alpha = Math.Sin(w) / (2.0 * q), a0 = 1.0 + alpha;
                double b0 = alpha / a0, b2 = -alpha / a0, a1 = -2.0 * Math.Cos(w) / a0, a2 = (1.0 - alpha) / a0;
                double x = Noise(i, seed), y = b0 * x + z1;
                z1 = -a1 * y + z2;
                z2 = b2 * x - a2 * y;
                signal[first + i] += (float)y * amplitude * Envelope(t, length, attack, decay);
            }
        }

        /// <summary>
        /// Adds a contact into a force (see <see cref="AddResonance"/>): a half sine of force, carrying the same push however
        /// long it lasts. A long contact is soft (a tyre squashing), a short one hard (steel on steel).
        /// </summary>
        /// <param name="start">When the contact starts (seconds).</param>
        /// <param name="contact">How long it lasts (seconds); at least a sample.</param>
        /// <param name="push">How much it pushes: a push of 1 as short as a sample rings a mode as loud as its amplitude.</param>
        public static void AddContact(float[] force, float start, float contact, float push)
        {
            int first = (int)(start * SampleRate);
            int count = Math.Max(1, (int)(contact * SampleRate));
            float total = 0f;
            for (int i = 0; i < count; i++)
                total += MathF.Sin(MathF.PI * (i + 0.5f) / count);
            for (int i = 0; i < count && first + i < force.Length; i++)
                force[first + i] += push / total * MathF.Sin(MathF.PI * (i + 0.5f) / count);
        }

        /// <summary>
        /// Adds a body ringing from a force (modal synthesis): each mode of the body is a resonator the force drives,
        /// ringing on and dying away at its own rate. A push as short as a sample rings a mode as loud as its amplitude; a
        /// longer contact rings the high modes less (its force has little in it faster than itself), which makes a soft hit
        /// dull and a hard one bright. Modes above the highest frequency the samples hold are left out.
        /// </summary>
        public static void AddResonance(float[] signal, float[] force, IEnumerable<Mode> modes)
        {
            foreach (Mode mode in modes)
            {
                if (mode.Hz <= 0f || mode.Hz >= SampleRate * 0.45f)
                    continue;
                //A two-pole resonator: its answer to a single sample of 1 is r^n sin((n + 1) w) / sin w
                double w = 2.0 * Math.PI * mode.Hz / SampleRate, r = Math.Exp(-mode.Decay / SampleRate);
                double c1 = 2.0 * r * Math.Cos(w), c2 = -r * r, gain = mode.Amplitude * Math.Sin(w);
                double y1 = 0.0, y2 = 0.0;
                for (int i = 0; i < signal.Length && i < force.Length; i++)
                {
                    double y = force[i] + c1 * y1 + c2 * y2;
                    signal[i] += (float)(gain * y);
                    y2 = y1;
                    y1 = y;
                }
            }
        }

        /// <summary>
        /// Adds a steady sine with no envelope, a part of a loop: over a buffer of a whole second, a whole number of hertz
        /// ends where it starts, so the loop has no seam.
        /// </summary>
        public static void AddSine(float[] signal, float frequency, float amplitude, float phase)
        {
            for (int i = 0; i < signal.Length; i++)
                signal[i] += amplitude * MathF.Sin(2f * MathF.PI * frequency * i / SampleRate + phase);
        }

        /// <summary>Multiplies the sound by a function of time (seconds): a tremolo, or bursts.</summary>
        public static void Modulate(float[] signal, Func<float, float> gain)
        {
            for (int i = 0; i < signal.Length; i++)
                signal[i] *= gain((float)i / SampleRate);
        }

        /// <summary>
        /// Noise through a band-pass filter that loops without a seam: the filter runs over the buffer twice and keeps the
        /// second pass, so it starts from where it ends. Peaks at 1.
        /// </summary>
        public static float[] LoopNoise(int length, float frequency, float q, int seed)
        {
            float[] noise = new float[length];
            for (int i = 0; i < length; i++)
                noise[i] = Noise(i, seed);
            BandPass(noise, frequency, q, loop: true);
            Normalize(noise, 1f);
            return noise;
        }

        /// <summary>White noise as a function of the sample index: the same every time.</summary>
        public static float Noise(int index, int seed)
        {
            uint x = (uint)index * 747796405u + (uint)seed * 2891336453u + 1u;
            x ^= x >> 17;
            x *= 0xed5ad4bbu;
            x ^= x >> 11;
            x *= 0xac4c1b51u;
            x ^= x >> 15;
            return x / (float)uint.MaxValue * 2f - 1f;
        }

        /// <summary>
        /// A Schroeder reverb: four parallel comb filters into two all-pass filters (the Freeverb topology), added to the
        /// sound as a tail of ambience.
        /// </summary>
        /// <param name="room">Scales the delays: a smaller room answers sooner.</param>
        /// <param name="wet">How much of the tail is added.</param>
        /// <param name="decay">0..1: how long the tail rings.</param>
        public static void Reverb(float[] signal, float room, float wet, float decay)
        {
            int[] combDelays = { (int)(1116 * room), (int)(1188 * room), (int)(1277 * room), (int)(1356 * room) };
            int[] allPassDelays = { (int)(556 * room), (int)(441 * room) };
            float feedback = Math.Clamp(0.7f + decay * 0.25f, 0.5f, 0.84f);

            float[] tail = new float[signal.Length];
            foreach (int delay in combDelays)
            {
                if (delay < 1)
                    continue;
                float[] line = new float[delay];
                int index = 0;
                for (int i = 0; i < signal.Length; i++)
                {
                    float output = line[index];
                    line[index] = signal[i] + output * feedback;
                    index = (index + 1) % delay;
                    tail[i] += output * 0.25f;
                }
            }
            foreach (int delay in allPassDelays)
            {
                if (delay < 1)
                    continue;
                float[] line = new float[delay];
                int index = 0;
                for (int i = 0; i < signal.Length; i++)
                {
                    float buffered = line[index];
                    float output = buffered - tail[i];
                    line[index] = tail[i] + buffered * 0.5f;
                    index = (index + 1) % delay;
                    tail[i] = output;
                }
            }

            for (int i = 0; i < signal.Length; i++)
                signal[i] = signal[i] * (1f - wet * 0.5f) + tail[i] * wet;
        }

        /// <summary>A second-order low-pass (Butterworth): what is above the cutoff is rolled off.</summary>
        /// <param name="loop">The sound is a loop: the filter runs on from its end into its start, so it has no seam.</param>
        public static void LowPass(float[] signal, float cutoffHz, bool loop = false)
        {
            double w = 2.0 * Math.PI * cutoffHz / SampleRate, cos = Math.Cos(w), alpha = Math.Sin(w) / (2.0 * 0.70710678);
            double a0 = 1.0 + alpha;
            double b0 = (1.0 - cos) / 2.0 / a0;
            Filter(signal, b0, (1.0 - cos) / a0, b0, -2.0 * cos / a0, (1.0 - alpha) / a0, loop);
        }

        /// <summary>A second-order high-pass (Butterworth): what is below the cutoff is rolled off.</summary>
        public static void HighPass(float[] signal, float cutoffHz)
        {
            double w = 2.0 * Math.PI * cutoffHz / SampleRate, cos = Math.Cos(w), alpha = Math.Sin(w) / (2.0 * 0.70710678);
            double a0 = 1.0 + alpha;
            double b0 = (1.0 + cos) / 2.0 / a0;
            Filter(signal, b0, -(1.0 + cos) / a0, b0, -2.0 * cos / a0, (1.0 - alpha) / a0, false);
        }

        /// <summary>
        /// Drives the sound into a soft clip: scaled to peak at 1, then rounded off by a tanh (peaking at 1 again). Its
        /// loudest part gains harmonics, which let small speakers that drop a low boom still play its pitch.
        /// </summary>
        /// <param name="drive">How hard: about 1 rounds the peaks a little, 3 squashes them.</param>
        public static void Saturate(float[] signal, float drive)
        {
            Normalize(signal, 1f);
            float scale = 1f / MathF.Tanh(drive);
            for (int i = 0; i < signal.Length; i++)
                signal[i] = MathF.Tanh(signal[i] * drive) * scale;
        }

        /// <summary>
        /// A second-order band-pass (constant peak gain): what is near the middle passes, the rest is rolled off. Struck by
        /// a click, it rings at the middle for a moment, the longer the narrower it is.
        /// </summary>
        /// <param name="q">How narrow the band is: 0.7 is wide, 3 is nearly a whistle.</param>
        /// <param name="loop">The sound is a loop: the filter runs on from its end into its start, so it has no seam.</param>
        public static void BandPass(float[] signal, float hz, float q, bool loop = false)
        {
            double w = 2.0 * Math.PI * hz / SampleRate, alpha = Math.Sin(w) / (2.0 * q), a0 = 1.0 + alpha;
            Filter(signal, alpha / a0, 0.0, -alpha / a0, -2.0 * Math.Cos(w) / a0, (1.0 - alpha) / a0, loop);
        }

        /// <summary>
        /// A biquad filter (transposed direct form II) over the sound, in place. For a loop it first runs over the whole
        /// sound once without keeping it, so it starts with what its end leaves ringing.
        /// </summary>
        private static void Filter(float[] signal, double b0, double b1, double b2, double a1, double a2, bool loop)
        {
            double z1 = 0.0, z2 = 0.0;
            for (int pass = loop ? 0 : 1; pass < 2; pass++)
                for (int i = 0; i < signal.Length; i++)
                {
                    double x = signal[i], y = b0 * x + z1;
                    z1 = b1 * x - a1 * y + z2;
                    z2 = b2 * x - a2 * y;
                    if (pass == 1)
                        signal[i] = (float)y;
                }
        }

        /// <summary>Scales the sound so its loudest sample reaches the peak.</summary>
        public static void Normalize(float[] signal, float peak)
        {
            float loudest = 0f;
            foreach (float sample in signal)
                loudest = Math.Max(loudest, Math.Abs(sample));
            if (loudest < 1e-6f)
                return;
            float scale = peak / loudest;
            for (int i = 0; i < signal.Length; i++)
                signal[i] *= scale;
        }

        /// <summary>Fades the end of the sound out over the given time (a reverb tail cut short would click).</summary>
        public static void FadeOut(float[] signal, float seconds)
        {
            int count = Math.Min(signal.Length, (int)(seconds * SampleRate));
            for (int i = 0; i < count; i++)
                signal[signal.Length - 1 - i] *= (float)i / count;
        }

        /// <summary>The sound as 16-bit little-endian PCM, as SoundEffect takes it.</summary>
        public static byte[] ToPcm16(float[] signal)
        {
            byte[] pcm = new byte[signal.Length * 2];
            for (int i = 0; i < signal.Length; i++)
            {
                short value = (short)(Math.Clamp(signal[i], -1f, 1f) * short.MaxValue);
                pcm[i * 2] = (byte)value;
                pcm[i * 2 + 1] = (byte)(value >> 8);
            }
            return pcm;
        }

        /// <summary>The sound as a WAV file (16-bit mono PCM), to listen to outside the game.</summary>
        public static void WriteWav(float[] signal, Stream stream)
        {
            byte[] pcm = ToPcm16(signal);
            using (BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
            {
                writer.Write("RIFF".ToCharArray());
                writer.Write(36 + pcm.Length);
                writer.Write("WAVEfmt ".ToCharArray());
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(SampleRate);
                writer.Write(SampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write("data".ToCharArray());
                writer.Write(pcm.Length);
                writer.Write(pcm);
            }
        }

        /// <summary>The level of a tone at a time: up over the attack, then dying away, and faded out at its end.</summary>
        private static float Envelope(float t, float length, float attack, float decay)
        {
            float level = attack > 0f ? Math.Min(1f, t / attack) : 1f;
            level *= MathF.Exp(-decay * t);
            float release = Math.Min(MaxRelease, length * 0.1f);
            float left = length - t;
            if (left < release)
                level *= Math.Max(0f, left / release);
            return level;
        }
    }
}
