using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TW3D.Graphics;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>
    /// The surfaces of the blocks (Effects/Surfaces.fxh): the noise they are drawn from tiles and keeps its fields apart
    /// (Graphics/SurfaceNoise), the random values of the corners of the cells are fixed and varied, the shader, the content
    /// pipeline and the game number the surfaces and the details alike, and the Surface detail row of the Options menu
    /// steps as the Shadow detail row does.
    /// </summary>
    public class SurfaceTests
    {
        private const int Size = SurfaceNoise.Size;

        [Fact]
        public void TheNoiseHasEveryMipmapDownToOneTexel()
        {
            byte[][] levels = SurfaceNoise.Levels;
            Assert.Equal(1 + (int)Math.Log2(Size), levels.Length);
            for (int level = 0; level < levels.Length; level++)
                Assert.Equal((Size >> level) * (Size >> level) * 4, levels[level].Length);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void TheNoiseTilesWithoutASeam(int channel)
        {
            //The steps across the edges of the texture (where one cell of the level grid meets the next) are as small as the
            //steps between any two neighbouring texels
            byte[] texels = SurfaceNoise.Levels[0];
            double inside = 0, acrossX = 0, acrossY = 0;
            for (int i = 0; i < Size; i++)
            {
                for (int j = 0; j < Size - 1; j++)
                    inside += Math.Abs(At(texels, j + 1, i, channel) - At(texels, j, i, channel));
                acrossX += Math.Abs(At(texels, 0, i, channel) - At(texels, Size - 1, i, channel));
                acrossY += Math.Abs(At(texels, i, 0, channel) - At(texels, i, Size - 1, channel));
            }
            inside /= Size * (Size - 1);
            Assert.InRange(acrossX / Size, 0, inside * 1.5 + 0.5);
            Assert.InRange(acrossY / Size, 0, inside * 1.5 + 0.5);
        }

        [Fact]
        public void TheMottleAndTheSlopesUseTheRangeAroundTheMiddle()
        {
            byte[] texels = SurfaceNoise.Levels[0];
            for (int channel = 0; channel < 3; channel++)
            {
                int[] values = Enumerable.Range(0, Size * Size).Select(i => (int)texels[i * 4 + channel]).OrderBy(v => v).ToArray();
                Assert.InRange(values.Average(), 118, 138);
                //Most of the values within the range, not crowded in the middle nor cut off at its ends
                Assert.InRange(values[values.Length * 95 / 100] - values[values.Length * 5 / 100], 90, 230);
            }
        }

        [Fact]
        public void TheStonesAreSmallAndApart()
        {
            //The cellular noise is 0 at its points and grows away from them: a stone takes a small part of the texture
            byte[] texels = SurfaceNoise.Levels[0];
            double below = Enumerable.Range(0, Size * Size).Count(i => texels[i * 4 + 3] < 0.3 * 255) / (double)(Size * Size);
            Assert.InRange(below, 0.05, 0.4);
        }

        [Fact]
        public void AMipmapIsTheMeanOfFourTexels()
        {
            byte[] full = SurfaceNoise.Levels[0], half = SurfaceNoise.Levels[1];
            for (int y = 0; y < Size / 2; y += 7)
            {
                for (int x = 0; x < Size / 2; x += 5)
                {
                    for (int channel = 0; channel < 4; channel++)
                    {
                        double mean = (At(full, 2 * x, 2 * y, channel) + At(full, 2 * x + 1, 2 * y, channel)
                            + At(full, 2 * x, 2 * y + 1, channel) + At(full, 2 * x + 1, 2 * y + 1, channel)) / 4.0;
                        Assert.InRange(half[(y * Size / 2 + x) * 4 + channel], mean - 1.0, mean + 1.0);
                    }
                }
            }
        }

        [Fact]
        public void TheCornersOfTheCellsHaveFixedAndVariedValues()
        {
            float[] seeds = (from x in Enumerable.Range(-5, 10) from y in Enumerable.Range(-2, 5) from z in Enumerable.Range(-1, 3)
                             select SurfaceNoise.CornerSeed(x, y, z)).ToArray();
            Assert.All(seeds, seed => Assert.InRange(seed, 0f, 0.99999994f));
            Assert.True(seeds.Distinct().Count() > seeds.Length * 0.98);
            Assert.InRange(seeds.Average(), 0.4, 0.6);
            Assert.Equal(SurfaceNoise.CornerSeed(3, -1, 2), SurfaceNoise.CornerSeed(3, -1, 2));
        }

        private static int At(byte[] texels, int x, int y, int channel) => texels[(y * Size + x) * 4 + channel];

        [Fact]
        public void TheShaderThePipelineAndTheGameNumberTheSurfacesAlike()
        {
            string effects = Path.Combine(Repository.Content, "Effects");
            string surfaces = File.ReadAllText(Path.Combine(effects, "Surfaces.fxh"));
            string scene = File.ReadAllText(Path.Combine(effects, "Scene.fx"));
            string processor = File.ReadAllText(Path.Combine(Repository.Root, "tools", "ContentPipeline", "MtlModelProcessor.cs"));
            foreach (Surface surface in Enum.GetValues<Surface>().Where(surface => surface != Surface.None))
            {
                string name = surface.ToString(), define = "SURFACE_" + name.ToUpperInvariant();
                Assert.Matches(new Regex(@"#define " + define + @"\s+" + (int)surface + @"\b"), surfaces);
                //The techniques of the surface (Lit<Surface><Detail>, which the renderer looks up by name)
                Assert.Contains("SURFACE_TECHNIQUES(" + name + ", " + define + ")", scene);
                //Its material ("asphalt…") numbered as the game reads it
                Assert.Matches(new Regex(@"StartsWith\(""" + name.ToLowerInvariant() + @""", StringComparison\.OrdinalIgnoreCase\) \? " + (int)surface + "f"), processor);
            }
            foreach (SurfaceDetail detail in Enum.GetValues<SurfaceDetail>())
            {
                string define = "SURFACE_" + detail.ToString().ToUpperInvariant();
                Assert.Matches(new Regex(@"#define " + define + @"\s+" + (int)detail + @"\b"), surfaces);
                Assert.Contains("SURFACE_TECHNIQUE(Lit##name##" + detail + ", true, surface, " + define + ")", scene);
                Assert.Contains("SURFACE_TECHNIQUE(LitCoarseShadows##name##" + detail + ", false, surface, " + define + ")", scene);
            }
        }

        [Fact]
        public void TheDefaultIsHighOnADesktopAndLowOnAPi()
        {
            //The CI runs the tests on Windows and on Linux on ARM64, which the game takes for a Raspberry Pi
            Assert.Equal(PiGraphics.Active ? SurfaceDetail.Low : SurfaceDetail.High, SurfaceDetails.Default);
        }

        [Fact]
        public void EnterStepsUpAndWrapsRound()
        {
            Assert.Equal(SurfaceDetail.Medium, SurfaceDetails.Next(SurfaceDetail.Low));
            Assert.Equal(SurfaceDetail.High, SurfaceDetails.Next(SurfaceDetail.Medium));
            Assert.Equal(SurfaceDetail.Low, SurfaceDetails.Next(SurfaceDetail.High));
        }

        [Theory]
        [InlineData(SurfaceDetail.Low, -1, SurfaceDetail.Low)]
        [InlineData(SurfaceDetail.Low, 1, SurfaceDetail.Medium)]
        [InlineData(SurfaceDetail.Medium, -1, SurfaceDetail.Low)]
        [InlineData(SurfaceDetail.High, 1, SurfaceDetail.High)]
        [InlineData(SurfaceDetail.High, -1, SurfaceDetail.Medium)]
        public void LeftAndRightStepAndStop(SurfaceDetail detail, int step, SurfaceDetail expected)
        {
            Assert.Equal(expected, SurfaceDetails.Step(detail, step));
        }

        [Fact]
        public void AnUnknownDetailIsTheDefault()
        {
            SurfaceDetail unknown = (SurfaceDetail)7;
            Assert.Equal(SurfaceDetails.Default, SurfaceDetails.Valid(unknown));
            Assert.Equal(SurfaceDetails.Default.ToString(), SurfaceDetails.Name(unknown));
        }

        [Fact]
        public void TheSettingsFileKeepsTheDetailByItsName()
        {
            JsonSerializerOptions options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            string json = JsonSerializer.Serialize(new GameSettings { SurfaceDetail = SurfaceDetail.Low }, options);
            Assert.Contains("\"surfaceDetail\":\"Low\"", json);
            Assert.Equal(SurfaceDetail.Medium, JsonSerializer.Deserialize<GameSettings>("{\"surfaceDetail\":\"Medium\"}", options).SurfaceDetail);
        }
    }
}
