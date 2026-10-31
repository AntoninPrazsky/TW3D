using TW3D.GamePlay;
using TW3D.Shared;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>The score and the stars of a run, and the par time of a level (GamePlay/RaceScore).</summary>
    public class RaceScoreTests
    {
        [Theory]
        [InlineData(20f, 1000)]
        [InlineData(10f, 2000)]
        [InlineData(40f, 500)]
        public void TheScoreIsInProportionToThePar(float time, int score)
        {
            Assert.Equal(score, RaceScore.Score(time, 20f));
        }

        [Theory]
        [InlineData(15f, 3)]
        [InlineData(20f, 3)]
        [InlineData(20.01f, 2)]
        [InlineData(30f, 2)]
        [InlineData(30.01f, 1)]
        public void ThreeStarsWithinTheParTwoWithinOneAndAHalfOfIt(float time, int stars)
        {
            Assert.Equal(stars, RaceScore.Stars(time, 20f));
        }

        [Fact]
        public void TheParOfTheLevelFileComesFirst()
        {
            LevelData level = new LevelData { Par = 14f };
            level.Middle.Add(new BlockPlacement { Block = "a1", X = 60, Y = 0, Finish = true });
            Assert.Equal(14f, RaceScore.Par(level));
        }

        [Theory]
        [InlineData(10, -2, 10f)]
        [InlineData(40, -4, 22f)]
        public void WithoutOneTheParIsHalfASecondACellToTheFinishAtLeastTen(int x, int y, float par)
        {
            LevelData level = new LevelData();
            level.Middle.Add(new BlockPlacement { Block = "a1", X = x, Y = y, Finish = true });
            Assert.Equal(par, RaceScore.Par(level));
        }

        [Theory]
        [InlineData(65.325f, "1:05.32")]
        [InlineData(8.5f, "0:08.50")]
        [InlineData(0f, "0:00.00")]
        public void TimesShowMinutesSecondsAndHundredths(float seconds, string text)
        {
            Assert.Equal(text, RaceScore.FormatTime(seconds));
        }
    }
}
