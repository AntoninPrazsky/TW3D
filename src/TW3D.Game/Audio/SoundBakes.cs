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
        /// <summary>A star of the results; played a third and a fifth higher for the second and the third star.</summary>
        Star,
        NewRecord,
        /// <summary>Another car, or other wheels, in the garage.</summary>
        GarageSwitch
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

        /// <summary>How loud a sound plays (0..1), before the volume of the Options menu.</summary>
        public static float Volume(Sound sound) => sound switch
        {
            Sound.MenuMove => 0.3f,
            Sound.MenuSelect => 0.5f,
            Sound.MenuBack => 0.45f,
            Sound.Countdown => 0.6f,
            Sound.Start => 0.65f,
            Sound.Finish => 0.65f,
            Sound.Star => 0.55f,
            Sound.NewRecord => 0.6f,
            Sound.GarageSwitch => 0.4f,
            _ => 0.5f
        };

        /// <summary>How far (octaves) a sound's pitch may be nudged at random: only the noise, the notes stay in tune.</summary>
        public static float Nudge(Sound sound) => sound == Sound.GarageSwitch ? 0.03f : 0f;

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
                Sound.Star => Star(),
                Sound.NewRecord => NewRecord(),
                Sound.GarageSwitch => Whoosh(),
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
