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
    /// files. The rules, after BS3D: pleasant before realistic and nothing harsh; what makes a sound recognizable lies
    /// between 300 Hz and 4 kHz (small speakers play little below); mono; a sound that is not a note plays a little
    /// higher or lower each time, so nothing sounds the same twice.
    /// </summary>
    public static class SoundBakes
    {
        //The notes of the sounds (Hz)
        private const float E5 = 659.26f, G5 = 783.99f, B5 = 987.77f, C5 = 523.25f;
        private const float C6 = 1046.5f, D6 = 1174.66f, E6 = 1318.51f, G6 = 1567.98f, C7 = 2093f;

        /// <summary>How loud a sound plays (0..1), before the volume of the Options menu.</summary>
        public static float Volume(Sound sound) => sound switch
        {
            Sound.MenuMove => 0.25f,
            Sound.MenuSelect => 0.45f,
            Sound.MenuBack => 0.4f,
            Sound.Countdown => 0.55f,
            Sound.Start => 0.6f,
            Sound.Finish => 0.6f,
            Sound.Star => 0.5f,
            Sound.NewRecord => 0.55f,
            Sound.GarageSwitch => 0.35f,
            _ => 0.5f
        };

        /// <summary>How far (octaves) a sound's pitch may be nudged at random; the notes of the race are not.</summary>
        public static float Nudge(Sound sound) => sound switch
        {
            Sound.MenuMove or Sound.MenuSelect or Sound.MenuBack or Sound.GarageSwitch => 0.03f,
            _ => 0f
        };

        /// <summary>The samples of a sound (see <see cref="Synth"/>), peaking at 0.9.</summary>
        public static float[] Bake(Sound sound)
        {
            float[] signal = sound switch
            {
                Sound.MenuMove => MenuMove(),
                Sound.MenuSelect => TwoNotes(G5, D6),
                Sound.MenuBack => TwoNotes(B5, E5),
                Sound.Countdown => Beep(E5, 0.25f),
                Sound.Start => Beep(E6, 0.6f),
                Sound.Finish => Finish(),
                Sound.Star => Star(),
                Sound.NewRecord => NewRecord(),
                Sound.GarageSwitch => Whoosh(),
                _ => throw new ArgumentOutOfRangeException(nameof(sound))
            };
            Synth.Normalize(signal, 0.9f);
            return signal;
        }

        /// <summary>A short tick: a tone falling a little, and a knock.</summary>
        private static float[] MenuMove()
        {
            float[] signal = Synth.Buffer(0.06f);
            Synth.AddTone(signal, 0f, 0.06f, 1500f, 1150f, 0.7f, 0.001f, 70f, 1f, 0.3f);
            Synth.AddNoise(signal, 0f, 0.012f, 1.5f, 0.0005f, 300f, 3000f, 3000f, 0.8f, seed: 1);
            return signal;
        }

        /// <summary>Two soft notes, one after the other: up for choosing, down for going back.</summary>
        private static float[] TwoNotes(float first, float second)
        {
            float[] signal = Synth.Buffer(0.4f);
            Synth.AddTone(signal, 0f, 0.13f, first, first, 0.6f, 0.003f, 22f, 1f, 0.35f, 0.12f);
            Synth.AddTone(signal, 0.07f, 0.26f, second, second, 0.6f, 0.003f, 14f, 1f, 0.35f, 0.12f);
            Synth.Reverb(signal, 0.35f, 0.18f, 0.3f);
            Synth.FadeOut(signal, 0.05f);
            return signal;
        }

        /// <summary>A beep of the start lights: a held note with odd harmonics (a soft square), the edge rolled off.</summary>
        private static float[] Beep(float frequency, float length)
        {
            float[] signal = Synth.Buffer(length + 0.05f);
            Synth.AddTone(signal, 0f, length, frequency, frequency, 0.7f, 0.004f, 2.5f, 1f, 0.05f, 0.3f, 0.03f, 0.15f);
            Synth.LowPass(signal, 4000f);
            return signal;
        }

        /// <summary>A short fanfare: the chord of C major broken upwards, ringing like bells.</summary>
        private static float[] Finish()
        {
            float[] signal = Synth.Buffer(1.8f);
            float[] notes = { C5, E5, G5, C6 };
            for (int i = 0; i < notes.Length; i++)
            {
                float start = i * 0.09f;
                Synth.AddTone(signal, start, 1.3f - start, notes[i], notes[i], 0.5f, 0.003f, i == notes.Length - 1 ? 2.5f : 4.5f, 1f, 0.45f, 0.2f, 0.1f);
            }
            Synth.Reverb(signal, 0.7f, 0.3f, 0.6f);
            Synth.FadeOut(signal, 0.3f);
            return signal;
        }

        /// <summary>A bell: C6 with its harmonics, and a shimmer an octave up.</summary>
        private static float[] Star()
        {
            float[] signal = Synth.Buffer(1f);
            Synth.AddTone(signal, 0f, 0.8f, C6, C6, 0.8f, 0.002f, 7f, 1f, 0.5f, 0.15f, 0.08f);
            Synth.AddTone(signal, 0f, 0.5f, C7 * 1.004f, C7 * 1.004f, 0.15f, 0.002f, 12f, 1f);
            Synth.Reverb(signal, 0.4f, 0.2f, 0.4f);
            Synth.FadeOut(signal, 0.15f);
            return signal;
        }

        /// <summary>A quick run up two octaves, then a shimmer of two notes a hair apart.</summary>
        private static float[] NewRecord()
        {
            float[] signal = Synth.Buffer(1.8f);
            float[] notes = { G5, C6, E6, G6, C7 };
            for (int i = 0; i < notes.Length; i++)
                Synth.AddTone(signal, i * 0.06f, 0.5f, notes[i], notes[i], 0.45f, 0.002f, 6f, 1f, 0.4f, 0.12f);
            Synth.AddTone(signal, 0.3f, 1.2f, C7, C7, 0.25f, 0.05f, 3f, 1f, 0.2f);
            Synth.AddTone(signal, 0.3f, 1.2f, C7 * 1.003f, C7 * 1.003f, 0.25f, 0.05f, 3f, 1f, 0.2f);
            Synth.Reverb(signal, 0.6f, 0.3f, 0.6f);
            Synth.FadeOut(signal, 0.3f);
            return signal;
        }

        /// <summary>A whoosh: noise in a band rising from low to middle, swelling and fading, with a soft second layer.</summary>
        private static float[] Whoosh()
        {
            float[] signal = Synth.Buffer(0.36f);
            Synth.AddNoise(signal, 0f, 0.36f, 1f, 0.12f, 9f, 450f, 1600f, 1.6f, seed: 2);
            Synth.AddNoise(signal, 0.02f, 0.3f, 0.5f, 0.1f, 10f, 700f, 2400f, 2.5f, seed: 3);
            Synth.FadeOut(signal, 0.08f);
            return signal;
        }
    }
}
