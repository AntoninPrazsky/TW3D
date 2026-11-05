using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using TW3D.Audio;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>
    /// The sound effects (Audio/SoundBakes and Audio/Synth): what the rules of the sounds ask that arithmetic can tell.
    /// Whether a sound is right is for the ear; these catch a recipe gone wrong (silence, clipping, a click at the end,
    /// a hiss, a sound all in the bass small speakers drop, notes out of tune, the car ringing like a note when it lands).
    /// </summary>
    public class SoundTests
    {
        //Every take of every sound (see SoundBakes.Variants)
        public static IEnumerable<object[]> Sounds => Takes(sound => true);

        public static IEnumerable<object[]> OneShots => Takes(sound => !SoundBakes.IsLoop(sound));

        public static IEnumerable<object[]> Loops => Takes(SoundBakes.IsLoop);

        private static IEnumerable<object[]> Takes(Func<Sound, bool> which) => Enum.GetValues<Sound>().Where(which)
            .SelectMany(sound => Enumerable.Range(0, SoundBakes.Variants(sound)).Select(variant => new object[] { sound, variant }));

        //The sounds that are notes: they stay in tune
        private static readonly Sound[] Notes =
        {
            Sound.MenuMove, Sound.MenuSelect, Sound.MenuBack, Sound.Countdown, Sound.Start, Sound.Finish, Sound.Star, Sound.NewRecord
        };

        [Theory]
        [MemberData(nameof(Sounds))]
        public void PeaksAtNineTenthsWithoutGaps(Sound sound, int variant)
        {
            float[] signal = SoundBakes.Bake(sound, variant);
            Assert.All(signal, sample => Assert.True(float.IsFinite(sample)));
            Assert.Equal(0.9f, signal.Max(Math.Abs), 3);
        }

        /// <summary>A sound cut off loud clicks: its last 5 ms are quiet.</summary>
        [Theory]
        [MemberData(nameof(OneShots))]
        public void EndsQuietly(Sound sound, int variant)
        {
            float[] signal = SoundBakes.Bake(sound, variant);
            int tail = Synth.SampleRate / 200;
            float rms = MathF.Sqrt(signal.Skip(signal.Length - tail).Sum(s => s * s) / tail);
            Assert.True(rms < 0.01f, $"{sound} ends at RMS {rms}");
        }

        /// <summary>
        /// Deep, but heard on small speakers, which play little below 300 Hz: a good part of its energy lies between 300 Hz
        /// and 4 kHz; and nothing shrill: next to none above 5 kHz.
        /// </summary>
        [Theory]
        [MemberData(nameof(Sounds))]
        public void IsHeardOnSmallSpeakersAndNotShrill(Sound sound, int variant)
        {
            float[] signal = SoundBakes.Bake(sound, variant);
            Complex[] spectrum = Spectrum(signal);
            double hzPerBin = (double)Synth.SampleRate / spectrum.Length;
            double total = 0.0, band = 0.0, shrill = 0.0;
            for (int i = 1; i < spectrum.Length / 2; i++)
            {
                double energy = spectrum[i].Magnitude * spectrum[i].Magnitude;
                double frequency = i * hzPerBin;
                total += energy;
                if (frequency >= 300.0 && frequency <= 4000.0)
                    band += energy;
                if (frequency > 5000.0)
                    shrill += energy;
            }
            Assert.True(band / total > 0.35, $"{sound}: {band / total:P1} of its energy between 300 Hz and 4 kHz");
            Assert.True(shrill / total < 0.02, $"{sound}: {shrill / total:P1} of its energy above 5 kHz");
        }

        /// <summary>The sounds of a menu do not pile up as the player goes through it: they are over (but a soft tail) in 0.35 s.</summary>
        [Fact]
        public void TheMenuSoundsAreShort()
        {
            foreach (Sound sound in new[] { Sound.MenuMove, Sound.MenuSelect, Sound.MenuBack })
            {
                float[] signal = SoundBakes.Bake(sound);
                float total = signal.Sum(s => s * s);
                float early = signal.Take((int)(0.35f * Synth.SampleRate)).Sum(s => s * s);
                Assert.True(early / total > 0.9f, $"{sound}: {early / total:P0} of its energy in the first 0.35 s");
            }
        }

        /// <summary>A loop clicks where its end meets its start unless the step there is like any other step of it.</summary>
        [Theory]
        [MemberData(nameof(Loops))]
        public void LoopsWithoutASeam(Sound sound, int variant)
        {
            float[] signal = SoundBakes.Bake(sound, variant);
            float largestStep = 0f;
            for (int i = 1; i < signal.Length; i++)
                largestStep = Math.Max(largestStep, Math.Abs(signal[i] - signal[i - 1]));
            Assert.True(Math.Abs(signal[0] - signal[^1]) <= largestStep, $"{sound}: a step of {Math.Abs(signal[0] - signal[^1])} where it loops");
        }

        /// <summary>The notes stay in tune with each other: they are never nudged off their pitch, nor are the loops.</summary>
        [Fact]
        public void TheNotesAndTheLoopsAreNotNudged()
        {
            foreach (Sound sound in Notes.Concat(Enum.GetValues<Sound>().Where(SoundBakes.IsLoop)))
                Assert.Equal(0f, SoundBakes.Nudge(sound));
        }

        /// <summary>The takes of a sound differ, or a run up the stairs would repeat one sample step after step.</summary>
        [Fact]
        public void TheTakesOfASoundDiffer()
        {
            foreach (Sound sound in Enum.GetValues<Sound>().Where(sound => SoundBakes.Variants(sound) > 1))
            {
                float[][] takes = Enumerable.Range(0, SoundBakes.Variants(sound)).Select(variant => SoundBakes.Bake(sound, variant)).ToArray();
                for (int a = 0; a < takes.Length; a++)
                    for (int b = a + 1; b < takes.Length; b++)
                        Assert.False(takes[a].SequenceEqual(takes[b]), $"{sound}: takes {a + 1} and {b + 1} are the same");
            }
        }

        /// <summary>
        /// The car hitting something thuds or knocks, it does not ring like an instrument: little of what it has above
        /// 150 Hz lies in its three strongest bands 20 Hz wide (the first landing, four fading tones, had 76 % there, a
        /// metal barrel has 91 %; noise has next to none).
        /// </summary>
        [Theory]
        [InlineData(Sound.ImpactTyres, 0.35)]
        [InlineData(Sound.ImpactBody, 0.45)]
        public void TheCarDoesNotRing(Sound sound, double most)
        {
            for (int variant = 0; variant < SoundBakes.Variants(sound); variant++)
            {
                Complex[] spectrum = Spectrum(SoundBakes.Bake(sound, variant));
                double hzPerBin = (double)Synth.SampleRate / spectrum.Length;
                int width = (int)Math.Ceiling(20.0 / hzPerBin);
                List<double> bands = new List<double>();
                for (int first = (int)Math.Ceiling(150.0 / hzPerBin); first + width <= spectrum.Length / 2; first += width)
                    bands.Add(Enumerable.Range(first, width).Sum(i => spectrum[i].Magnitude * spectrum[i].Magnitude));
                double strongest = bands.OrderByDescending(energy => energy).Take(3).Sum() / bands.Sum();
                Assert.True(strongest < most, $"{sound} {variant + 1}: {strongest:P0} in its three strongest bands");
            }
        }

        /// <summary>The shock absorbers creak quieter than the engine idles, even at their loudest.</summary>
        [Fact]
        public void TheCreakIsQuieterThanTheEngine()
        {
            Assert.True(Loudness(Sound.Suspension) < Loudness(Sound.EngineIdle), $"{Loudness(Sound.Suspension)} against {Loudness(Sound.EngineIdle)}");
        }

        [Fact]
        public void AWavFileHoldsTheSamples()
        {
            float[] signal = SoundBakes.Bake(Sound.MenuMove);
            using (MemoryStream stream = new MemoryStream())
            {
                Synth.WriteWav(signal, stream);
                byte[] wav = stream.ToArray();
                Assert.Equal(44 + signal.Length * 2, wav.Length);
                Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
                Assert.Equal(Synth.SampleRate, BitConverter.ToInt32(wav, 24));
            }
        }

        /// <summary>The RMS of a sound as it plays at its full volume.</summary>
        private static float Loudness(Sound sound)
        {
            float[] signal = SoundBakes.Bake(sound);
            return SoundBakes.Volume(sound) * MathF.Sqrt(signal.Sum(s => s * s) / signal.Length);
        }

        /// <summary>The spectrum of the samples, padded to a power of two (an iterative radix-2 FFT).</summary>
        private static Complex[] Spectrum(float[] signal)
        {
            int n = 1;
            while (n < signal.Length)
                n <<= 1;
            Complex[] data = new Complex[n];
            for (int i = 0; i < signal.Length; i++)
                data[i] = signal[i];

            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1)
                    j ^= bit;
                j ^= bit;
                if (i < j)
                    (data[i], data[j]) = (data[j], data[i]);
            }
            for (int length = 2; length <= n; length <<= 1)
            {
                Complex step = Complex.FromPolarCoordinates(1.0, -2.0 * Math.PI / length);
                for (int i = 0; i < n; i += length)
                {
                    Complex w = Complex.One;
                    for (int k = 0; k < length / 2; k++)
                    {
                        Complex even = data[i + k], odd = data[i + k + length / 2] * w;
                        data[i + k] = even + odd;
                        data[i + k + length / 2] = even - odd;
                        w *= step;
                    }
                }
            }
            return data;
        }
    }
}
