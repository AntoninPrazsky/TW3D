using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Xna.Framework.Audio;

namespace TW3D.Audio
{
    /// <summary>
    /// The sound effects, synthesized when the game starts (see <see cref="SoundBakes"/>) and played by the screens. Without
    /// an audio device the game stays silent.
    /// </summary>
    public sealed class GameSounds : IDisposable
    {
        private readonly SoundEffect[] effects;
        private readonly Random random = new Random();

        public GameSounds()
        {
            Sound[] sounds = Enum.GetValues<Sound>();
            effects = new SoundEffect[sounds.Length];
            try
            {
                foreach (Sound sound in sounds)
                    effects[(int)sound] = new SoundEffect(Synth.ToPcm16(SoundBakes.Bake(sound)), Synth.SampleRate, AudioChannels.Mono);
            }
            catch (NoAudioHardwareException e)
            {
                Debug.WriteLine("No sound: " + e.Message);
            }
        }

        /// <summary>The volume of all the sounds (0..1), from the Options menu.</summary>
        public static float Volume
        {
            get => SoundEffect.MasterVolume;
            set => SoundEffect.MasterVolume = Math.Clamp(value, 0f, 1f);
        }

        /// <param name="pitch">Octaves up (positive) or down, -1..1; the sound's random nudge is added.</param>
        public void Play(Sound sound, float pitch = 0f)
        {
            SoundEffect effect = effects[(int)sound];
            if (effect == null)
                return;
            float nudge = SoundBakes.Nudge(sound) * (random.NextSingle() * 2f - 1f);
            try
            {
                effect.Play(SoundBakes.Volume(sound), Math.Clamp(pitch + nudge, -1f, 1f), 0f);
            }
            catch (InstancePlayLimitException)
            {
                //Too many sounds at once: this one is left out
            }
        }

        /// <summary>Writes every sound as a WAV file into a folder, to listen to them outside the game (see Program).</summary>
        public static void Export(string folder)
        {
            Directory.CreateDirectory(folder);
            foreach (Sound sound in Enum.GetValues<Sound>())
                using (FileStream file = File.Create(Path.Combine(folder, sound + ".wav")))
                    Synth.WriteWav(SoundBakes.Bake(sound), file);
        }

        public void Dispose()
        {
            foreach (SoundEffect effect in effects)
                effect?.Dispose();
        }
    }
}
