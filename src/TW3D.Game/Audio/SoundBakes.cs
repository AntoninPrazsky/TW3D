using System;
using System.Collections.Generic;
using System.Linq;

namespace TW3D.Audio
{
    /// <summary>The sound effects of the game.</summary>
    public enum Sound
    {
        /// <summary>The focus moves to another item of a menu.</summary>
        MenuMove,
        /// <summary>An item of a menu is chosen.</summary>
        MenuSelect,
        /// <summary>Back to the screen before.</summary>
        MenuBack,
        /// <summary>3, 2, 1 of the countdown before the start.</summary>
        Countdown,
        Start,
        Finish,
        /// <summary>The car fell off the level, or lay still on its roof: the run is lost.</summary>
        GameOver,
        /// <summary>A star of the results; played a third and a fifth higher for the second and the third star.</summary>
        Star,
        NewRecord,
        /// <summary>Another car, or other wheels, in the garage.</summary>
        GarageSwitch,
        /// <summary>The engine idling (a loop): played higher as the wheels turn faster.</summary>
        EngineIdle,
        /// <summary>The engine under the throttle (a loop, in step with <see cref="EngineIdle"/>), faded in with the throttle.</summary>
        EngineLoad,
        /// <summary>The motor turning the axles (a loop), while they turn.</summary>
        AxleMotor,
        /// <summary>The tyres screeching (a loop) while the braked wheels slide.</summary>
        Skid,
        /// <summary>The shock absorbers of an axle creaking as its spring moves fast, once for each stroke.</summary>
        Suspension,
        /// <summary>The axles locked (and, higher and softer, unlocked).</summary>
        AxleLock,
        /// <summary>The brake grabbing the wheels of a moving car.</summary>
        Brake,
        /// <summary>A metal object hitting something: barrels, containers, ramps; heavier ones play lower.</summary>
        ImpactMetal,
        /// <summary>A wooden object hitting something: crates.</summary>
        ImpactWood,
        /// <summary>A plastic object hitting something: the barrels.</summary>
        ImpactPlastic,
        /// <summary>The tyres of the car landing or hitting something: a soft thud of rubber.</summary>
        ImpactTyres,
        /// <summary>The body of the car hitting the track or an object: a harder knock of its frame.</summary>
        ImpactBody
    }

    /// <summary>
    /// The recipes of the sound effects: each is synthesized (see <see cref="Synth"/>) when the game starts, no recorded
    /// files. Pleasant and deep rather than bright: the notes are struck bars (a marimba) and soft bongs in C major, low
    /// (octaves 3 to 5) with an octave below for body, never a beep; nothing shrill above 5 kHz, but enough between 300 Hz
    /// and 4 kHz for small speakers, which play little below. Mono. The notes are never nudged off their pitch, so the
    /// sounds stay in tune with each other; only the noise of the garage plays a little higher or lower each time.
    /// (The owner found the first, brighter version too high, beeping and out of tune in the menus.)
    /// </summary>
    public static class SoundBakes
    {
        //The notes of the sounds (Hz)
        private const float C4 = 261.63f, E4 = 329.63f, G4 = 392f, C5 = 523.25f, E5 = 659.26f, G5 = 783.99f;
        //The loops last a second and every part of them is a whole number of hertz, so their end meets their start; the
        //engine fires this many times a second at the pitch it is baked at (an even number: the half order is whole too)
        private const float LoopLength = 1f;
        private const int Firing = 40;

        /// <summary>How loud a sound plays (0..1), before the volume of the Options menu.</summary>
        public static float Volume(Sound sound) => sound switch
        {
            Sound.MenuMove => 0.3f,
            Sound.MenuSelect => 0.5f,
            Sound.MenuBack => 0.45f,
            Sound.Countdown => 0.6f,
            Sound.Start => 0.65f,
            Sound.Finish => 0.65f,
            Sound.GameOver => 0.6f,
            Sound.Star => 0.55f,
            Sound.NewRecord => 0.6f,
            Sound.GarageSwitch => 0.4f,
            Sound.EngineIdle => 0.3f,
            Sound.EngineLoad => 0.5f,
            Sound.AxleMotor => 0.35f,
            Sound.Skid => 0.45f,
            Sound.Suspension => 0.34f,
            Sound.AxleLock => 0.55f,
            Sound.Brake => 0.5f,
            Sound.ImpactMetal => 0.8f,
            Sound.ImpactWood => 0.7f,
            Sound.ImpactPlastic => 0.7f,
            Sound.ImpactTyres => 0.8f,
            Sound.ImpactBody => 0.7f,
            _ => 0.5f
        };

        /// <summary>The sound loops (played on and on, its pitch and volume changed as it plays).</summary>
        public static bool IsLoop(Sound sound) => sound is Sound.EngineIdle or Sound.EngineLoad or Sound.AxleMotor or Sound.Skid;

        /// <summary>
        /// How many takes of a sound are baked, each struck at another spot (the modes of its body as loud as there) and a
        /// little higher or lower; one of them plays at random, so a run up the stairs does not repeat one sample step after
        /// step.
        /// </summary>
        public static int Variants(Sound sound) =>
            sound is Sound.ImpactTyres or Sound.ImpactBody or Sound.ImpactMetal or Sound.ImpactPlastic or Sound.Suspension ? 4 : 1;

        /// <summary>
        /// How far (octaves) a sound's pitch may be nudged at random: the noises and the knocks, so no two sound the same;
        /// the notes stay in tune, and the loops are pitched by the car.
        /// </summary>
        public static float Nudge(Sound sound) => sound switch
        {
            Sound.GarageSwitch or Sound.AxleLock => 0.03f,
            Sound.Brake => 0.05f,
            Sound.ImpactMetal or Sound.ImpactWood or Sound.ImpactPlastic or Sound.ImpactTyres or Sound.ImpactBody => 0.08f,
            Sound.Suspension => 0.05f,
            _ => 0f
        };

        /// <summary>The samples of a sound (see <see cref="Synth"/>), peaking at 0.9.</summary>
        /// <param name="variant">Which take of the sound, 0 to <see cref="Variants"/> - 1.</param>
        public static float[] Bake(Sound sound, int variant = 0)
        {
            if (variant < 0 || variant >= Variants(sound))
                throw new ArgumentOutOfRangeException(nameof(variant));
            float[] signal = sound switch
            {
                Sound.MenuMove => MenuMove(),
                Sound.MenuSelect => Choose(),
                Sound.MenuBack => TwoNotes(G4, C4),
                Sound.Countdown => Bong(G4, 0.5f, 6f),
                Sound.Start => Bong(G5, 0.9f, 3f),
                Sound.Finish => Finish(),
                Sound.GameOver => GameOver(),
                Sound.Star => Star(),
                Sound.NewRecord => NewRecord(),
                Sound.GarageSwitch => Whoosh(),
                Sound.EngineIdle => Engine(load: false),
                Sound.EngineLoad => Engine(load: true),
                Sound.AxleMotor => AxleMotor(),
                Sound.Skid => Skid(),
                Sound.Suspension => Suspension(variant),
                Sound.AxleLock => AxleLock(),
                Sound.Brake => Brake(),
                Sound.ImpactMetal => ImpactMetal(variant),
                Sound.ImpactWood => ImpactWood(),
                Sound.ImpactPlastic => ImpactPlastic(variant),
                Sound.ImpactTyres => ImpactTyres(variant),
                Sound.ImpactBody => ImpactBody(variant),
                _ => throw new ArgumentOutOfRangeException(nameof(sound))
            };
            Synth.Normalize(signal, 0.9f);
            return signal;
        }

        /// <summary>
        /// A struck bar (a marimba): the fundamental with its first harmonics (which small speakers play when they drop the
        /// fundamental), the octave below for body, and the partials of a bar (about four and nine times the fundamental),
        /// which die away much faster and give the knock of the mallet.
        /// </summary>
        private static void Mallet(float[] signal, float start, float frequency, float amplitude, float decay, float length)
        {
            Synth.AddTone(signal, start, length, frequency, frequency, amplitude, 0.003f, decay, 1f, 0.35f, 0.1f);
            Synth.AddTone(signal, start, length, frequency / 2f, frequency / 2f, amplitude * 0.3f, 0.006f, decay * 0.8f, 1f);
            Synth.AddTone(signal, start, length * 0.5f, frequency * 3.93f, frequency * 3.93f, amplitude * 0.22f, 0.001f, decay * 4f, 1f);
            Synth.AddTone(signal, start, length * 0.25f, frequency * 9.2f, frequency * 9.2f, amplitude * 0.04f, 0.0005f, decay * 9f, 1f);
        }

        /// <summary>A soft held chord under a melody, for warmth: sine tones swelling in and dying slowly.</summary>
        private static void Pad(float[] signal, float start, float length, float amplitude, params float[] notes)
        {
            foreach (float note in notes)
            {
                Synth.AddTone(signal, start, length, note, note, amplitude, 0.12f, 1.5f, 1f, 0.15f);
                Synth.AddTone(signal, start, length, note * 1.003f, note * 1.003f, amplitude * 0.5f, 0.12f, 1.5f, 1f);
            }
        }

        /// <summary>The focus moving: a short, quiet knock of a bar.</summary>
        private static float[] MenuMove()
        {
            float[] signal = Synth.Buffer(0.2f);
            Mallet(signal, 0f, G4, 0.8f, 30f, 0.18f);
            Synth.LowPass(signal, 3500f);
            return signal;
        }

        /// <summary>
        /// Choosing: a quick run up the chord of C major on bars (E4, G4, C5), the last note sung: two soft tones a hair
        /// apart swell in under it and ring on, beating gently. (A bare fifth up sounded empty and gloomy to the owner, like
        /// a sound of an old operating system rather than of a game.)
        /// </summary>
        private static float[] Choose()
        {
            float[] signal = Synth.Buffer(0.9f);
            Mallet(signal, 0f, E4, 0.5f, 12f, 0.35f);
            Mallet(signal, 0.055f, G4, 0.5f, 10f, 0.4f);
            Mallet(signal, 0.11f, C5, 0.6f, 5f, 0.7f);
            Synth.AddTone(signal, 0.11f, 0.7f, C5, C5, 0.22f, 0.025f, 4f, 1f, 0.25f);
            Synth.AddTone(signal, 0.11f, 0.7f, C5 * 1.004f, C5 * 1.004f, 0.16f, 0.025f, 4f, 1f);
            Synth.LowPass(signal, 3500f);
            Synth.Reverb(signal, 0.55f, 0.22f, 0.45f);
            Synth.FadeOut(signal, 0.12f);
            return signal;
        }

        /// <summary>Going back: two bars a fifth down.</summary>
        private static float[] TwoNotes(float first, float second)
        {
            float[] signal = Synth.Buffer(0.8f);
            Mallet(signal, 0f, first, 0.7f, 9f, 0.5f);
            Mallet(signal, 0.08f, second, 0.7f, 7f, 0.6f);
            Synth.LowPass(signal, 3500f);
            Synth.Reverb(signal, 0.5f, 0.2f, 0.4f);
            Synth.FadeOut(signal, 0.1f);
            return signal;
        }

        /// <summary>A soft bong of the start lights: a round tone with a few harmonics and the octave below, dying away.</summary>
        private static float[] Bong(float frequency, float length, float decay)
        {
            float[] signal = Synth.Buffer(length + 0.2f);
            Synth.AddTone(signal, 0f, length, frequency, frequency, 0.7f, 0.006f, decay, 1f, 0.4f, 0.15f, 0.06f);
            Synth.AddTone(signal, 0f, length, frequency / 2f, frequency / 2f, 0.3f, 0.01f, decay * 0.8f, 1f);
            Synth.LowPass(signal, 3000f);
            Synth.Reverb(signal, 0.4f, 0.15f, 0.3f);
            Synth.FadeOut(signal, 0.12f);
            return signal;
        }

        /// <summary>A short fanfare: the chord of C major broken upwards on bars, over the chord held softly.</summary>
        private static float[] Finish()
        {
            float[] signal = Synth.Buffer(2.2f);
            float[] notes = { C4, E4, G4, C5 };
            for (int i = 0; i < notes.Length; i++)
                Mallet(signal, i * 0.1f, notes[i], 0.6f, i == notes.Length - 1 ? 2.5f : 4f, 1.5f);
            Pad(signal, 0.3f, 1.6f, 0.12f, C4, E4, G4);
            Synth.LowPass(signal, 3500f);
            Synth.Reverb(signal, 0.8f, 0.3f, 0.7f);
            Synth.FadeOut(signal, 0.4f);
            return signal;
        }

        /// <summary>
        /// Game over: the chord of C major broken downwards on bars, slower than the fanfare of the finish, over the chord
        /// held softly; gentle, not a buzzer.
        /// </summary>
        private static float[] GameOver()
        {
            float[] signal = Synth.Buffer(2.4f);
            float[] notes = { G4, E4, C4 };
            for (int i = 0; i < notes.Length; i++)
                Mallet(signal, i * 0.22f, notes[i], 0.6f, i == notes.Length - 1 ? 2f : 4f, 1.6f);
            Pad(signal, 0.45f, 1.6f, 0.1f, C4, E4, G4);
            Synth.LowPass(signal, 3500f);
            Synth.Reverb(signal, 0.8f, 0.3f, 0.7f);
            Synth.FadeOut(signal, 0.4f);
            return signal;
        }

        /// <summary>A star: a bar of C5 ringing long, with a faint octave above.</summary>
        private static float[] Star()
        {
            float[] signal = Synth.Buffer(1.2f);
            Mallet(signal, 0f, C5, 0.8f, 5f, 1f);
            Synth.AddTone(signal, 0f, 0.6f, C5 * 2f, C5 * 2f, 0.1f, 0.002f, 8f, 1f);
            Synth.LowPass(signal, 3500f);
            Synth.Reverb(signal, 0.5f, 0.25f, 0.5f);
            Synth.FadeOut(signal, 0.2f);
            return signal;
        }

        /// <summary>A new record: a quick run up two octaves of C major, then the chord held, shimmering a little.</summary>
        private static float[] NewRecord()
        {
            float[] signal = Synth.Buffer(2.2f);
            float[] notes = { C4, E4, G4, C5, E5, G5 };
            for (int i = 0; i < notes.Length; i++)
                Mallet(signal, i * 0.06f, notes[i], 0.5f, 6f, 0.8f);
            Pad(signal, 0.35f, 1.6f, 0.12f, C5, E5, G5);
            Synth.LowPass(signal, 3500f);
            Synth.Reverb(signal, 0.8f, 0.35f, 0.7f);
            Synth.FadeOut(signal, 0.4f);
            return signal;
        }

        /// <summary>
        /// The engine of a car, deep and unhurried: the harmonics of its slow firing shaped by two resonances of its body
        /// (the rumble and the throat), a strong burble at half the rate (the uneven beat of a big engine), the firings
        /// alternating stronger and weaker, and grit: noise in bursts with the firing. Under the throttle the harmonics
        /// reach higher and the grit is louder. A loop (see <see cref="LoopLength"/>). (The first, faster engine sounded
        /// like a motorbike to the owner.)
        /// </summary>
        private static float[] Engine(bool load)
        {
            float[] signal = Synth.Buffer(LoopLength);
            for (int h = 1; h * Firing <= 2400; h++)
                Synth.AddSine(signal, h * Firing, EngineShape(h * Firing, load) / MathF.Pow(h, 0.6f), h * 2.4f);
            for (int h = 0; (h + 0.5f) * Firing <= 1200; h++)
                Synth.AddSine(signal, (h + 0.5f) * Firing, 0.5f * EngineShape((h + 0.5f) * Firing, load) / MathF.Pow(h + 1, 0.6f), h * 1.7f + 1f);
            Synth.Modulate(signal, t => 1f + 0.15f * MathF.Sin(2f * MathF.PI * Firing / 2f * t) + 0.08f * MathF.Sin(2f * MathF.PI * 2f * t)
                + 0.05f * MathF.Sin(2f * MathF.PI * 5f * t + 1f));

            float[] grit = Synth.LoopNoise(signal.Length, 600f, 0.8f, seed: 4);
            float gritLevel = load ? 0.22f : 0.08f;
            for (int i = 0; i < signal.Length; i++)
            {
                float pulse = 0.5f + 0.5f * MathF.Cos(2f * MathF.PI * Firing * i / Synth.SampleRate);
                signal[i] += grit[i] * gritLevel * pulse * pulse * pulse * pulse;
            }
            return signal;
        }

        /// <summary>
        /// How loud a harmonic of the engine is: two resonances of the body, falling off above, and the lowest harmonics
        /// held back (the engine idles lower still, and small speakers would hear only them as nothing).
        /// </summary>
        private static float EngineShape(float frequency, bool load)
        {
            float rumble = 1f / (1f + MathF.Pow((frequency - 180f) / 100f, 2f));
            float throat = 0.8f / (1f + MathF.Pow((frequency - 450f) / 180f, 2f));
            float lows = frequency / (frequency + 120f);
            return (rumble + throat + (load ? 0.2f : 0.12f)) * lows * lows * MathF.Exp(-frequency / (load ? 2000f : 1300f));
        }

        /// <summary>
        /// Tyres screeching: a cluster of tones close together around 1 kHz (they beat against each other, the wobble of a
        /// screech), the rubber scrubbing (noise in a band) and a rumble of the road. A loop. Kept in the middle rather
        /// than shrill.
        /// </summary>
        private static float[] Skid()
        {
            float[] signal = Synth.Buffer(LoopLength);
            int[] tones = { 940, 953, 967, 988, 1004 };
            for (int i = 0; i < tones.Length; i++)
            {
                Synth.AddSine(signal, tones[i], 0.25f, i * 1.3f);
                Synth.AddSine(signal, tones[i] * 2, 0.06f, i * 2.1f);
            }
            float[] scrub = Synth.LoopNoise(signal.Length, 1300f, 1.5f, seed: 11);
            float[] road = Synth.LoopNoise(signal.Length, 320f, 0.8f, seed: 12);
            for (int i = 0; i < signal.Length; i++)
                signal[i] += scrub[i] * 0.35f + road[i] * 0.3f;
            Synth.Modulate(signal, t => 1f + 0.2f * MathF.Sin(2f * MathF.PI * 6f * t) + 0.1f * MathF.Sin(2f * MathF.PI * 11f * t + 0.5f));
            return signal;
        }

        /// <summary>
        /// The shock absorbers creaking at a stroke of the spring, high like the springs of an old car: a bush sticking and
        /// slipping. Every slip knocks the strut (a click ringing a few of its resonances, high in the middle), and the slips
        /// come at a rate that glides as the stroke goes, a few hundred a second, so they run into a squeaky tone with a
        /// rasp: each slip comes a little early or late and knocks a little harder or softer. Louder after a longer stick
        /// (a slower rate). Each take glides its own way: up and back a little, high and clean, up and down (eek-ook),
        /// and rough. (The first version, random clicks under noise, crackled like a radio off its station; of the second,
        /// the owner wanted something between a buzzing creak and a clean squeak, higher.)
        /// </summary>
        private static float[] Suspension(int variant)
        {
            //The rate of the slips at the start, in the middle and at the end (a second), how far each slip is early or
            //late and how much harder or softer it knocks (shares), the resonances and the length
            (float from, float middle, float to, float jitter, float rasp, Mode[] modes, float length) = variant switch
            {
                1 => (250f, 420f, 340f, 0.015f, 0.1f, new[] { new Mode(1300f, 40f, 1f), new Mode(2000f, 55f, 0.6f), new Mode(2900f, 90f, 0.3f) }, 0.38f),
                2 => (220f, 400f, 200f, 0.025f, 0.15f, new[] { new Mode(900f, 45f, 0.8f), new Mode(1500f, 50f, 1f), new Mode(2300f, 80f, 0.4f) }, 0.42f),
                3 => (300f, 380f, 340f, 0.03f, 0.25f, new[] { new Mode(1200f, 50f, 1f), new Mode(1800f, 60f, 0.8f), new Mode(2600f, 90f, 0.35f) }, 0.36f),
                _ => (180f, 320f, 260f, 0.02f, 0.1f, new[] { new Mode(1100f, 45f, 1f), new Mode(1700f, 60f, 0.7f), new Mode(2500f, 90f, 0.35f) }, 0.4f)
            };
            float[] force = Synth.Buffer(length);
            double phase = 0.0;
            int slip = 0;
            for (int i = 0; i < force.Length; i++)
            {
                float t = (float)i / Synth.SampleRate, along = t / length;
                float rate = along < 0.5f ? from + (middle - from) * along * 2f : middle + (to - middle) * (along - 0.5f) * 2f;
                phase += rate * (1f + jitter * Synth.Noise(slip, 71)) / Synth.SampleRate;
                if (phase >= 1.0)
                {
                    phase -= 1.0;
                    //The creak swells in and dies away with the stroke
                    float swell = Math.Min(1f, t / 0.04f) * Math.Min(1f, (length - t) / 0.12f);
                    force[i] += swell * MathF.Sqrt(100f / rate) * (1f + rasp * Synth.Noise(slip, 72));
                    slip++;
                }
            }
            float[] signal = Synth.Buffer(length);
            Synth.AddResonance(signal, force, modes);
            Synth.LowPass(signal, 4000f);
            Synth.FadeOut(signal, 0.03f);
            return signal;
        }

        /// <summary>The motor of the axles: an electric hum and its harmonics, and the whine of its gears. A loop.</summary>
        private static float[] AxleMotor()
        {
            float[] signal = Synth.Buffer(LoopLength);
            for (int h = 1; h <= 6; h++)
                Synth.AddSine(signal, 120f * h, h == 1 ? 0.25f : 0.4f / h, h * 2.4f);
            Synth.AddSine(signal, 840f, 0.3f, 0.3f);
            Synth.AddSine(signal, 1200f, 0.16f, 1.1f);
            Synth.AddSine(signal, 1680f, 0.08f, 2.2f);
            Synth.Modulate(signal, t => 1f + 0.15f * MathF.Sin(2f * MathF.PI * 10f * t));
            return signal;
        }

        /// <summary>The lock of the axles: a thump, the latch ringing briefly, and its click.</summary>
        private static float[] AxleLock()
        {
            float[] signal = Synth.Buffer(0.35f);
            Synth.AddTone(signal, 0f, 0.3f, 190f, 160f, 0.45f, 0.002f, 20f, 1f, 0.6f, 0.25f);
            Synth.AddTone(signal, 0f, 0.15f, 520f, 520f, 0.45f, 0.001f, 30f, 1f);
            Synth.AddTone(signal, 0f, 0.1f, 1370f, 1370f, 0.14f, 0.0005f, 55f, 1f);
            Synth.AddNoise(signal, 0f, 0.012f, 1.2f, 0.0005f, 300f, 1600f, 1600f, 1.5f, seed: 5);
            Synth.LowPass(signal, 3500f);
            Synth.FadeOut(signal, 0.05f);
            return signal;
        }

        /// <summary>The brake: the clamp grabbing, a soft thud, and the tyres scrubbing for a moment.</summary>
        private static float[] Brake()
        {
            float[] signal = Synth.Buffer(0.45f);
            Synth.AddNoise(signal, 0f, 0.01f, 1f, 0.0005f, 300f, 1300f, 1300f, 1.5f, seed: 6);
            Synth.AddNoise(signal, 0.005f, 0.4f, 0.7f, 0.02f, 8f, 600f, 350f, 0.9f, seed: 7);
            Synth.AddTone(signal, 0f, 0.12f, 320f, 280f, 0.35f, 0.002f, 30f, 1f, 0.3f);
            Synth.LowPass(signal, 3000f);
            Synth.FadeOut(signal, 0.08f);
            return signal;
        }

        /// <summary>Wood hit: the knock of a crate, two resonances of its boards and its weight.</summary>
        private static float[] ImpactWood()
        {
            float[] signal = Synth.Buffer(0.35f);
            Synth.AddTone(signal, 0f, 0.3f, 330f, 315f, 0.6f, 0.001f, 28f, 1f, 0.25f);
            Synth.AddTone(signal, 0f, 0.2f, 780f, 770f, 0.3f, 0.001f, 45f, 1f);
            Synth.AddTone(signal, 0f, 0.2f, 160f, 140f, 0.4f, 0.002f, 30f, 1f);
            Synth.AddNoise(signal, 0f, 0.012f, 1f, 0.0005f, 300f, 1100f, 1100f, 1.2f, seed: 9);
            Synth.LowPass(signal, 3200f);
            Synth.FadeOut(signal, 0.05f);
            return signal;
        }

        /// <summary>How much higher or lower each take of a sound is tuned (see <see cref="Variants"/>).</summary>
        private static float Tune(int variant) => variant switch
        {
            1 => 0.94f,
            2 => 1.05f,
            3 => 0.98f,
            _ => 1f
        };

        /// <summary>
        /// The modes of a body (see <see cref="Synth.AddResonance"/>): this many, spread between two frequencies as the modes
        /// of a plate or a shell lie (evenly, each a little off its place), the higher ones softer by the tilt (amplitude
        /// falling as frequency^-tilt) and dying faster. The seed picks where each lies and how loud it is, as another spot
        /// of the same body struck would. Many modes close together and out of tune with each other sound like a thing; a
        /// few far apart, like an instrument.
        /// </summary>
        /// <param name="decay">How fast the lowest of them dies away (1 / seconds).</param>
        /// <param name="decayPerKhz">How much faster each kilohertz higher dies away.</param>
        private static Mode[] Modes(int count, float lowHz, float highHz, float decay, float decayPerKhz, float tilt, int seed)
        {
            Mode[] modes = new Mode[count];
            for (int k = 0; k < count; k++)
            {
                float hz = lowHz + (highHz - lowHz) * (k + 0.5f + 0.45f * Synth.Noise(k, seed)) / count;
                float loudness = (0.6f + 0.4f * Synth.Noise(k, seed + 1)) * MathF.Pow(hz / lowHz, -tilt);
                float dying = (decay + decayPerKhz * (hz - lowHz) / 1000f) * (1f + 0.15f * Synth.Noise(k, seed + 2));
                modes[k] = new Mode(hz, dying, loudness);
            }
            return modes;
        }

        /// <summary>A few modes given one by one (frequency, decay, amplitude), tuned by a take's tuning.</summary>
        private static Mode[] Tuned(float tune, params (float Hz, float Decay, float Amplitude)[] modes)
        {
            Mode[] tuned = new Mode[modes.Length];
            for (int i = 0; i < modes.Length; i++)
                tuned[i] = new Mode(modes[i].Hz * tune, modes[i].Decay, modes[i].Amplitude);
            return tuned;
        }

        /// <summary>Adds a body struck once: a contact (see <see cref="Synth.AddContact"/>) ringing its modes.</summary>
        private static void Strike(float[] signal, float start, float contact, float push, IEnumerable<Mode> modes)
        {
            float[] force = Synth.Buffer(signal.Length / (float)Synth.SampleRate);
            Synth.AddContact(force, start, contact, push);
            Synth.AddResonance(signal, force, modes);
        }

        /// <summary>
        /// The tyres landing or hitting something: a soft, deep thud. The tyres squash for a while (a long, soft contact),
        /// which booms the car on them and the air in them (their lowest modes; driven a little, so that the boom gains
        /// the harmonics small speakers play) and hardly rings anything higher; the rubber whumps (low noise swelling and
        /// dying smoothly); a moment later the suspension takes the blow and knocks the frame dully (a contact ringing
        /// many heavily damped modes in the low middle), and a loose part rattles twice. All of it is muffled above
        /// 1.2 kHz. (The first version, four fading tones, rang like a drum; the second, bursts of noise with sharp edges,
        /// cracked like wood being split. Of the candidates of the third, the owner found this one the closest.)
        /// </summary>
        private static float[] ImpactTyres(int variant)
        {
            float[] signal = Synth.Buffer(0.4f);
            float tune = Tune(variant);
            int seed = 41 + variant * 8;
            Strike(signal, 0f, 0.01f, 1f, Tuned(tune, (75f, 24f, 1f), (110f, 30f, 0.6f), (160f, 38f, 0.35f), (225f, 45f, 0.5f)));
            Synth.Saturate(signal, 1.2f);
            Synth.AddNoise(signal, 0f, 0.12f, 3.5f, 0.006f, 40f, 300f * tune, 180f * tune, 0.7f, seed);
            Strike(signal, 0.012f + 0.003f * variant, 0.004f, 0.15f, Modes(30, 180f * tune, 600f * tune, 110f, 60f, 0.3f, seed + 2));
            float[] rattle = Synth.Buffer(0.4f);
            Synth.AddContact(rattle, 0.06f + 0.01f * variant, 0.0006f, 0.06f);
            Synth.AddContact(rattle, 0.115f + 0.007f * variant, 0.0006f, 0.045f);
            Synth.AddResonance(signal, rattle, Modes(10, 1200f * tune, 3000f * tune, 120f, 30f, 0.2f, seed + 6));
            Synth.LowPass(signal, 1200f);
            Synth.FadeOut(signal, 0.05f);
            return signal;
        }

        /// <summary>
        /// The body of the car hitting the track or an object: a crunch, harder than the tyres. The steel frame lands (a
        /// short contact booming its heavy low modes), a sharper contact crunches many damped modes of the frame and the
        /// sheet metal at once (close together and out of tune with each other, so it clanks like a car, not a bell), and
        /// the frame scrapes over the road for a moment (noise in a band, grainy). (The second version, three partials,
        /// sounded like a bamboo stick hitting a plastic bucket; the owner chose this one of three candidates.)
        /// </summary>
        private static float[] ImpactBody(int variant)
        {
            float[] signal = Synth.Buffer(0.45f);
            float tune = Tune(variant);
            int seed = 85 + variant * 8;
            Strike(signal, 0f, 0.004f, 1f, Tuned(tune, (90f, 45f, 1f), (140f, 50f, 0.7f), (210f, 55f, 0.5f)));
            Strike(signal, 0f, 0.001f, 0.6f, Modes(60, 250f * tune, 2500f * tune, 60f, 40f, 0.3f, seed));
            float[] scrape = Synth.Buffer(0.45f);
            Synth.AddNoise(scrape, 0.01f, 0.16f, 2f, 0.005f, 18f, 900f * tune, 700f * tune, 0.8f, seed + 1);
            for (int i = 0; i < scrape.Length; i++)
                signal[i] += scrape[i] * (0.35f + 0.65f * MathF.Abs(Synth.Noise(i / 110, seed + 2)));
            Synth.LowPass(signal, 4500f);
            Synth.FadeOut(signal, 0.06f);
            return signal;
        }

        /// <summary>
        /// Metal hit (barrels, containers, ramps): a steel drum hitting the ground and bouncing twice quickly, clang-a-lang.
        /// Each hard, short contact rings many modes close together and out of tune with each other (a few stronger low
        /// ones give the body of the hollow drum), the higher dying faster, and the mass of the thing thumps the ground.
        /// The takes bounce a little differently, one only once. Heavier things play it lower (see ImpactSounds). (The
        /// first version, five partials, sounded like a musical instrument; the owner chose this one of three candidates.)
        /// </summary>
        private static float[] ImpactMetal(int variant)
        {
            float[] signal = Synth.Buffer(0.9f);
            float tune = Tune(variant);
            int seed = 127 + variant * 8;
            float bounce = 0.045f + variant switch { 1 => -0.007f, 2 => 0.007f, 3 => -0.003f, _ => 0f };
            float[] force = Synth.Buffer(0.9f);
            Synth.AddContact(force, 0f, 0.0008f, 1f);
            Synth.AddContact(force, bounce, 0.0008f, 0.45f);
            if (variant != 3)
                Synth.AddContact(force, bounce + 0.04f, 0.0008f, 0.2f);
            Synth.AddResonance(signal, force, Tuned(tune, (170f, 16f, 0.8f), (247f, 18f, 0.6f), (318f, 20f, 0.5f)));
            Synth.AddResonance(signal, force, Modes(48, 360f * tune, 4500f * tune, 18f, 14f, 0.5f, seed));
            Strike(signal, 0f, 0.005f, 0.8f, Tuned(tune, (72f, 30f, 1f), (108f, 36f, 0.6f)));
            Synth.LowPass(signal, 4500f);
            Synth.FadeOut(signal, 0.12f);
            return signal;
        }

        /// <summary>
        /// Plastic hit (the barrels): a knock of a hollow plastic drum, brighter than its boom. A fairly hard contact rings
        /// the wall's many modes, which die away almost at once (plastic does not ring), over one low mode of the air
        /// inside. Nothing glides (gliding tones creaked like a chair in the first version); the owner chose this one of
        /// three candidates.
        /// </summary>
        private static float[] ImpactPlastic(int variant)
        {
            float[] signal = Synth.Buffer(0.35f);
            float tune = Tune(variant);
            Strike(signal, 0f, 0.0015f, 1f, Tuned(tune, (140f, 35f, 0.4f)).Concat(Modes(30, 280f * tune, 3000f * tune, 50f, 40f, 0.4f, 165 + variant * 8)));
            Synth.LowPass(signal, 3500f);
            Synth.FadeOut(signal, 0.05f);
            return signal;
        }

        /// <summary>A whoosh: noise in a band rising from low to middle, swelling and fading.</summary>
        private static float[] Whoosh()
        {
            float[] signal = Synth.Buffer(0.38f);
            Synth.AddNoise(signal, 0f, 0.38f, 1f, 0.13f, 8f, 250f, 900f, 1.2f, seed: 2);
            Synth.AddNoise(signal, 0.02f, 0.32f, 0.4f, 0.1f, 10f, 400f, 1400f, 2f, seed: 3);
            Synth.LowPass(signal, 3000f);
            Synth.FadeOut(signal, 0.08f);
            return signal;
        }
    }
}
