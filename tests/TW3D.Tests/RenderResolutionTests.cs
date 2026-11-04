using TW3D.Graphics;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>
    /// What the Resolution row of the Options menu promises (Graphics/RenderResolution, after BS3D's): every height it
    /// offers has the screen's own aspect ratio and even sides, native comes first, nothing goes under the floor, a click
    /// steps down and wraps round, left and right step and stop, and the height drawn follows what was chosen or pinned.
    /// </summary>
    public class RenderResolutionTests
    {
        [Theory]
        [InlineData(1920, 1080, new[] { 1080, 900, 720, 540, 360 })]
        [InlineData(3840, 2160, new[] { 2160, 1800, 1440, 1080, 720 })]
        [InlineData(2560, 1440, new[] { 1440, 1206, 954, 720, 486 })]
        [InlineData(2560, 1080, new[] { 1080, 918, 702, 540, 360 })]
        [InlineData(3840, 1600, new[] { 1600, 1330, 1070, 800, 530 })]
        [InlineData(1920, 1200, new[] { 1200, 1000, 800, 600, 400 })]
        //A window higher than wide
        [InlineData(900, 1600, new[] { 1600, 1344, 1056, 800, 544 })]
        public void TheListIsTheFractionsAtTheExactAspectOfTheScreen(int width, int height, int[] expected)
        {
            Assert.Equal(expected, RenderResolution.Heights(width, height));
        }

        [Theory]
        [InlineData(1920, 1080)]
        [InlineData(3840, 2160)]
        [InlineData(2560, 1440)]
        [InlineData(3840, 1600)]
        [InlineData(3440, 1440)]
        [InlineData(1920, 1200)]
        [InlineData(900, 1600)]
        public void EveryHeightKeepsTheAspectExactlyWithEvenSides(int width, int height)
        {
            foreach (int rung in RenderResolution.Heights(width, height))
            {
                int rungWidth = RenderResolution.WidthAt(width, height, rung);
                Assert.Equal(0, rung % 2);
                Assert.Equal(0, rungWidth % 2);
                //Multiplied out, so that exactly means exactly
                Assert.Equal((long)width * rung, (long)rungWidth * height);
            }
        }

        [Theory]
        [InlineData(1366, 768, new[] { 768, 640, 512, 384 })]
        //21:9 whose lowest fraction has no height at the exact aspect near it
        [InlineData(2560, 1080, new[] { 1080, 918, 702, 540, 360 })]
        public void AScreenWithoutAUsableUnitStaysWithinAFifthOfAPercent(int width, int height, int[] expected)
        {
            int[] heights = RenderResolution.Heights(width, height);
            Assert.Equal(expected, heights);
            foreach (int rung in heights)
            {
                float ratio = RenderResolution.WidthAt(width, height, rung) / (float)rung;
                Assert.InRange(ratio / ((float)width / height), 0.998f, 1.002f);
            }
        }

        [Fact]
        public void NothingGoesUnderTheFloorAndNativeIsAlwaysFirst()
        {
            //Exact 16:9 multiples of 18 near 600 and 480 (1056 × 594 and 864 × 486), then the floor
            Assert.Equal(new[] { 720, 594, 486, 360 }, RenderResolution.Heights(1280, 720));
            Assert.Equal(new[] { 480, 402 }, RenderResolution.Heights(640, 480));
            //A window lower than the floor has native alone
            Assert.Equal(new[] { 300 }, RenderResolution.Heights(500, 300));
            Assert.Empty(RenderResolution.Heights(0, 0));
            foreach (int rung in RenderResolution.Heights(1280, 720)[1..])
                Assert.True(rung >= RenderResolution.MinHeight);
        }

        [Fact]
        public void AClickStepsDownAndWrapsToNative()
        {
            int[] heights = RenderResolution.Heights(1920, 1080);
            Assert.Equal(900, RenderResolution.Next(heights, 1080));
            Assert.Equal(720, RenderResolution.Next(heights, 900));
            Assert.Equal(1080, RenderResolution.Next(heights, 360));
            //Off the list, the first height below it
            Assert.Equal(720, RenderResolution.Next(heights, 800));
        }

        [Fact]
        public void LeftAndRightStepAndStopAtTheEnds()
        {
            int[] heights = RenderResolution.Heights(1920, 1080);
            Assert.Equal(900, RenderResolution.Step(heights, 720, 1));
            Assert.Equal(540, RenderResolution.Step(heights, 720, -1));
            Assert.Equal(1080, RenderResolution.Step(heights, 1080, 1));
            Assert.Equal(360, RenderResolution.Step(heights, 360, -1));
        }

        [Fact]
        public void AHeightAskedForGoesToTheNearestTheLowerOfTwo()
        {
            int[] heights = RenderResolution.Heights(1920, 1080);
            Assert.Equal(720, RenderResolution.Nearest(heights, 720));
            Assert.Equal(720, RenderResolution.Nearest(heights, 700));
            //As far from 720 as from 900: the lower, the cheaper frame
            Assert.Equal(720, RenderResolution.Nearest(heights, 810));
            Assert.Equal(1080, RenderResolution.Nearest(heights, 4000));
        }

        [Fact]
        public void TheSceneIsNativeUntilAHeightIsChosen()
        {
            Assert.Equal(1080, RenderResolution.SceneHeight(1920, 1080, null, null, null));
            Assert.Equal(0, RenderResolution.SceneHeight(0, 0, null, 0.5f, null));
        }

        [Fact]
        public void AChosenPartOfTheHeightFollowsTheScreen()
        {
            //Two thirds chosen on 1920 × 1080, kept as a part of the height: the nearest height of another screen
            float part = RenderResolution.PartOf(1080, 720);
            Assert.Equal(0.6667f, part);
            Assert.Equal(720, RenderResolution.SceneHeight(1920, 1080, null, part, null));
            Assert.Equal(1440, RenderResolution.SceneHeight(3840, 2160, null, part, null));
            Assert.Equal(486, RenderResolution.SceneHeight(1280, 720, null, part, null));
            //A window lower than the floor has native alone, whatever was chosen
            Assert.Equal(300, RenderResolution.SceneHeight(500, 300, null, part, null));
            //Native chosen is 1
            Assert.Equal(1f, RenderResolution.PartOf(1080, 1080));
            Assert.Equal(1080, RenderResolution.SceneHeight(1920, 1080, null, 1f, 720));
        }

        [Fact]
        public void APinnedHeightWinsAndADefaultCapsTheSceneUntilOneIsChosen()
        {
            //--render 700: the nearest height of the list, whatever was chosen
            Assert.Equal(720, RenderResolution.SceneHeight(1920, 1080, 700, 1f, null));
            //The Raspberry Pi's 720 rows: the largest height not above it, native on a screen no higher
            Assert.Equal(720, RenderResolution.SceneHeight(1920, 1080, null, null, 720));
            Assert.Equal(720, RenderResolution.SceneHeight(3840, 2160, null, null, 720));
            Assert.Equal(720, RenderResolution.SceneHeight(1280, 720, null, null, 720));
        }
    }
}
