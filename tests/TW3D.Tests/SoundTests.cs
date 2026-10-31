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
    /// a hiss, a bass the speakers drop).
    /// </summary>
    public class SoundTests
    {
        public static IEnumerable<object[]> Sounds => Enum.GetValues<Sound>().Select(sound => new object[] { sound });

        [Theory]
        [MemberData(nameof(Sounds))]
        public void PeaksAtNineTenthsWithoutGaps(Sound sound)
        {
            float[] signal = SoundBakes.Bake(sound);
            Assert.All(signal, sample => Assert.True(float.IsFinite(sample)));
            Assert.Equal(0.9f, signal.Max(Math.Abs), 3);
        }

        /// <summary>A sound cut off loud clicks: its last 5 ms are quiet.</summary>
        [Theory]
        [MemberData(nameof(Sounds))]
        public void EndsQuietly(Sound sound)
        {
            float[] signal = SoundBakes.Bake(sound);
            int tail = Synth.SampleRate / 200;
            float rms = MathF.Sqrt(signal.Skip(signal.Length - tail).Sum(s => s * s) / tail);
            Assert.True(rms < 0.01f, $"{sound} ends at RMS {rms}");
        }

        /// <summary>What makes a sound recognizable lies between 300 Hz and 4 kHz: most of its energy is there.</summary>
        [Theory]
        [MemberData(nameof(Sounds))]
        public void LivesBetween300HzAnd4kHz(Sound sound)
        {
            float[] signal = SoundBakes.Bake(sound);
            Complex[] spectrum = Spectrum(signal);
            double hzPerBin = (double)Synth.SampleRate / spectrum.Length;
            double total = 0.0, band = 0.0;
            for (int i = 1; i < spectrum.Length / 2; i++)
            {
                double energy = spectrum[i].Magnitude * spectrum[i].Magnitude;
                total += energy;
                if (i * hzPerBin >= 300.0 && i * hzPerBin <= 4000.0)
                    band += energy;
            }
            Assert.True(band / total > 0.9, $"{sound}: {band / total:P1} of its energy between 300 Hz and 4 kHz");
        }

        [Fact]
        public void TheMenuSoundsAreShort()
        {
            foreach (Sound sound in new[] { Sound.MenuMove, Sound.MenuSelect, Sound.MenuBack })
                Assert.True(SoundBakes.Bake(sound).Length <= Synth.SampleRate / 2, $"{sound} is longer than half a second");
        }

        [Fact]
        public void TheNotesOfTheRaceAreNotNudged()
        {
            foreach (Sound sound in new[] { Sound.Countdown, Sound.Start, Sound.Finish, Sound.Star, Sound.NewRecord })
                Assert.Equal(0f, SoundBakes.Nudge(sound));
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
