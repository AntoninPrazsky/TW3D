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
        //Every take of every sound (see SoundBakes.Variants); null without an audio device
        private readonly SoundEffect[][] effects;
        private readonly Random random = new Random();

        public GameSounds()
        {
            Sound[] sounds = Enum.GetValues<Sound>();
            effects = new SoundEffect[sounds.Length][];
            try
            {
                foreach (Sound sound in sounds)
                {
                    SoundEffect[] takes = new SoundEffect[SoundBakes.Variants(sound)];
                    for (int variant = 0; variant < takes.Length; variant++)
                        takes[variant] = new SoundEffect(Synth.ToPcm16(SoundBakes.Bake(sound, variant)), Synth.SampleRate, AudioChannels.Mono);
                    effects[(int)sound] = takes;
                }
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

        /// <summary>Plays a sound, one of its takes at random.</summary>
        /// <param name="pitch">Octaves up (positive) or down, -1..1; the sound's random nudge is added.</param>
        /// <param name="volume">A share of the sound's own volume (a hit as hard as it was), 0..1.</param>
        public void Play(Sound sound, float pitch = 0f, float volume = 1f)
        {
            SoundEffect[] takes = effects[(int)sound];
            if (takes == null)
                return;
            SoundEffect effect = takes[random.Next(takes.Length)];
            float nudge = SoundBakes.Nudge(sound) * (random.NextSingle() * 2f - 1f);
            try
            {
                effect.Play(SoundBakes.Volume(sound) * Math.Clamp(volume, 0f, 1f), Math.Clamp(pitch + nudge, -1f, 1f), 0f);
            }
            catch (InstancePlayLimitException)
            {
                //Too many sounds at once: this one is left out
            }
        }

        /// <summary>
        /// A loop of a sound (see <see cref="SoundBakes.IsLoop"/>), silent and stopped: its owner sets its volume and pitch
        /// and plays it. Null without an audio device.
        /// </summary>
        public SoundEffectInstance Loop(Sound sound)
        {
            SoundEffect effect = effects[(int)sound]?[0];
            if (effect == null)
                return null;
            SoundEffectInstance loop = effect.CreateInstance();
            loop.IsLooped = true;
            loop.Volume = 0f;
            return loop;
        }

        /// <summary>
        /// Writes every sound as a WAV file into a folder, to listen to them outside the game (see Program); a sound of a
        /// few takes as one file for each, numbered from 1.
        /// </summary>
        public static void Export(string folder)
        {
            Directory.CreateDirectory(folder);
            foreach (Sound sound in Enum.GetValues<Sound>())
            {
                int variants = SoundBakes.Variants(sound);
                for (int variant = 0; variant < variants; variant++)
                {
                    string name = variants > 1 ? $"{sound}{variant + 1}.wav" : sound + ".wav";
                    using (FileStream file = File.Create(Path.Combine(folder, name)))
                        Synth.WriteWav(SoundBakes.Bake(sound, variant), file);
                }
            }
        }

        public void Dispose()
        {
            foreach (SoundEffect[] takes in effects)
                if (takes != null)
                    foreach (SoundEffect effect in takes)
                        effect?.Dispose();
        }
    }
}
