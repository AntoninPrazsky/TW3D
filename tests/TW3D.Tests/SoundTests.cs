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
        /// Deep, but heard on small speakers, which play little below 300 Hz: most of what the ear hears of it (its energy
        /// weighted as the ear hears it at a moderate level, the A-weighting) lies above 300 Hz; and nothing shrill: next to
        /// none of its energy above 5 kHz. (The share of the bare energy between 300 Hz and 4 kHz, the first rule, failed
        /// deep thuds whose bass the ear hardly hears while small speakers play the harmonics of their boom, their knock
        /// and their rattle: the tyres the owner chose have 4 % of their energy there, but 77 % of what is heard above
        /// 300 Hz. Every sound the owner approved before has at least 68 %.)
        /// </summary>
        [Theory]
        [MemberData(nameof(Sounds))]
        public void IsHeardOnSmallSpeakersAndNotShrill(Sound sound, int variant)
        {
            float[] signal = SoundBakes.Bake(sound, variant);
            Complex[] spectrum = Spectrum(signal);
            double hzPerBin = (double)Synth.SampleRate / spectrum.Length;
            double total = 0.0, shrill = 0.0, heard = 0.0, heardAbove = 0.0;
            for (int i = 1; i < spectrum.Length / 2; i++)
            {
                double energy = spectrum[i].Magnitude * spectrum[i].Magnitude;
                double frequency = i * hzPerBin;
                total += energy;
                if (frequency > 5000.0)
                    shrill += energy;
                double weight = AWeighting(frequency);
                heard += energy * weight;
                if (frequency >= 300.0)
                    heardAbove += energy * weight;
            }
            Assert.True(heardAbove / heard > 0.6, $"{sound} {variant + 1}: {heardAbove / heard:P1} of what is heard of it above 300 Hz");
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

        /// <summary>
        /// The creak of the springs plays as it is, never pitched: pitched by the axle, the stroke and at random, its metal
        /// sounded robotic in the game. Its takes make it vary instead.
        /// </summary>
        [Fact]
        public void TheCreakIsNotPitched()
        {
            Assert.Equal(0f, SoundBakes.Nudge(Sound.Suspension));
            Assert.True(SoundBakes.Variants(Sound.Suspension) >= 8);
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
        /// The car hitting something thuds or crunches, it does not ring like an instrument: little of what it has above
        /// 150 Hz lies in its three strongest bands 20 Hz wide (the first landing, four fading tones, had 76 % there, the
        /// first metal barrel 91 %; noise has next to none; the tyres the owner chose have about 36 %, the body about 22 %).
        /// Nor do the clods of earth the tyres throw up and a wheel landing in the earth.
        /// </summary>
        [Theory]
        [InlineData(Sound.ImpactTyres, 0.45)]
        [InlineData(Sound.ImpactBody, 0.35)]
        [InlineData(Sound.MudClod, 0.35)]
        [InlineData(Sound.MudSplat, 0.35)]
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

        /// <summary>The shock absorbers creak no louder than the engine under the throttle, even at their loudest.</summary>
        [Fact]
        public void TheCreakIsNoLouderThanTheEngine()
        {
            for (int variant = 0; variant < SoundBakes.Variants(Sound.Suspension); variant++)
                Assert.True(Loudness(Sound.Suspension, variant) <= Loudness(Sound.EngineLoad),
                    $"take {variant + 1}: {Loudness(Sound.Suspension, variant)} against {Loudness(Sound.EngineLoad)}");
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

        /// <summary>The RMS of a take of a sound as it plays at its full volume.</summary>
        private static float Loudness(Sound sound, int variant = 0)
        {
            float[] signal = SoundBakes.Bake(sound, variant);
            return SoundBakes.Volume(sound) * MathF.Sqrt(signal.Sum(s => s * s) / signal.Length);
        }

        /// <summary>How strongly the ear hears a frequency at a moderate level (the A-weighting, as a share of power; 1 at 1 kHz).</summary>
        private static double AWeighting(double frequency)
        {
            double f2 = frequency * frequency;
            double ra = 12194.0 * 12194.0 * f2 * f2
                / ((f2 + 20.6 * 20.6) * Math.Sqrt((f2 + 107.7 * 107.7) * (f2 + 737.9 * 737.9)) * (f2 + 12194.0 * 12194.0));
            return ra * ra * 1.5849;
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
