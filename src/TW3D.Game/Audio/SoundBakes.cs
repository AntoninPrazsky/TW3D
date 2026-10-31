using System;

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
        /// <summary>The car fell off the level: the run is lost.</summary>
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
        /// <summary>The car hitting something or landing.</summary>
        ImpactCar
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
            Sound.AxleLock => 0.55f,
            Sound.Brake => 0.5f,
            Sound.ImpactMetal => 0.8f,
            Sound.ImpactWood => 0.7f,
            Sound.ImpactPlastic => 0.7f,
            Sound.ImpactCar => 0.7f,
            _ => 0.5f
        };

        /// <summary>The sound loops (played on and on, its pitch and volume changed as it plays).</summary>
        public static bool IsLoop(Sound sound) => sound is Sound.EngineIdle or Sound.EngineLoad or Sound.AxleMotor or Sound.Skid;

        /// <summary>
        /// How far (octaves) a sound's pitch may be nudged at random: the noises and the knocks, so no two sound the same;
        /// the notes stay in tune, and the loops are pitched by the car.
        /// </summary>
        public static float Nudge(Sound sound) => sound switch
        {
            Sound.GarageSwitch or Sound.AxleLock => 0.03f,
            Sound.Brake => 0.05f,
            Sound.ImpactMetal or Sound.ImpactWood or Sound.ImpactPlastic or Sound.ImpactCar => 0.08f,
            _ => 0f
        };

        /// <summary>The samples of a sound (see <see cref="Synth"/>), peaking at 0.9.</summary>
        public static float[] Bake(Sound sound)
        {
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
                Sound.AxleLock => AxleLock(),
                Sound.Brake => Brake(),
                Sound.ImpactMetal => ImpactMetal(),
                Sound.ImpactWood => ImpactWood(),
                Sound.ImpactPlastic => ImpactPlastic(),
                Sound.ImpactCar => ImpactCar(),
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

        /// <summary>Metal hit: a hollow bonk (the uneven partials of a drum or a box of steel), its weight and the hit.</summary>
        private static float[] ImpactMetal()
        {
            float[] signal = Synth.Buffer(1f);
            const float body = 300f;
            Synth.AddTone(signal, 0f, 0.9f, body, body * 0.98f, 0.6f, 0.001f, 7f, 1f, 0.3f);
            Synth.AddTone(signal, 0f, 0.7f, body * 2.32f, body * 2.3f, 0.35f, 0.001f, 10f, 1f);
            Synth.AddTone(signal, 0f, 0.5f, body * 3.9f, body * 3.88f, 0.18f, 0.001f, 16f, 1f);
            Synth.AddTone(signal, 0f, 0.3f, body * 5.4f, body * 5.38f, 0.08f, 0.0005f, 24f, 1f);
            Synth.AddTone(signal, 0f, 0.25f, body / 2f, body / 2.3f, 0.45f, 0.002f, 18f, 1f);
            Synth.AddNoise(signal, 0f, 0.015f, 1f, 0.0005f, 300f, 900f, 900f, 1.2f, seed: 8);
            Synth.LowPass(signal, 3000f);
            Synth.Reverb(signal, 0.4f, 0.15f, 0.4f);
            Synth.FadeOut(signal, 0.1f);
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

        /// <summary>
        /// Plastic hit: the dull bomp of a hollow plastic barrel, its air booming briefly and the slap of its wall; no
        /// ringing, plastic does not ring.
        /// </summary>
        private static float[] ImpactPlastic()
        {
            float[] signal = Synth.Buffer(0.35f);
            Synth.AddTone(signal, 0f, 0.3f, 250f, 215f, 0.6f, 0.002f, 26f, 1f, 0.6f, 0.25f);
            Synth.AddTone(signal, 0f, 0.2f, 540f, 500f, 0.35f, 0.001f, 40f, 1f);
            Synth.AddTone(signal, 0f, 0.25f, 125f, 110f, 0.2f, 0.003f, 20f, 1f);
            Synth.AddNoise(signal, 0f, 0.02f, 0.9f, 0.001f, 150f, 800f, 650f, 1f, seed: 13);
            Synth.LowPass(signal, 2500f);
            Synth.FadeOut(signal, 0.05f);
            return signal;
        }

        /// <summary>The car hitting or landing: the thump of the tyres, the body, and its frame rattling.</summary>
        private static float[] ImpactCar()
        {
            float[] signal = Synth.Buffer(0.5f);
            Synth.AddTone(signal, 0f, 0.4f, 130f, 100f, 0.4f, 0.002f, 16f, 1f, 0.6f, 0.25f);
            Synth.AddTone(signal, 0f, 0.3f, 340f, 320f, 0.55f, 0.001f, 22f, 1f, 0.25f);
            Synth.AddTone(signal, 0f, 0.2f, 610f, 610f, 0.25f, 0.001f, 32f, 1f);
            Synth.AddTone(signal, 0f, 0.15f, 1130f, 1130f, 0.07f, 0.0005f, 50f, 1f);
            Synth.AddNoise(signal, 0f, 0.015f, 0.8f, 0.0005f, 300f, 700f, 700f, 1f, seed: 10);
            Synth.LowPass(signal, 3000f);
            Synth.FadeOut(signal, 0.06f);
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
