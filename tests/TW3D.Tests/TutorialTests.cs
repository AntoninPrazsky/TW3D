using System;
using System.Collections.Generic;
using System.Linq;
using TW3D.GamePlay;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>
    /// The rules of the tutorial (GamePlay/Tutorial): which lessons a level shows, when an action is done, what is
    /// saved, and when the stuck hint comes. The tutorial is run frame by frame as the game runs it.
    /// </summary>
    public class TutorialTests : IDisposable
    {
        private const float Frame = 1f / 60f;
        //A speed at which the car makes progress, and none
        private const float Moving = 10f;
        private const float Standing = 0f;

        private static readonly DriveActions Nothing = new DriveActions(false, false, false, false);
        private static readonly DriveActions Throttle = new DriveActions(true, false, false, false);
        private static readonly DriveActions ThrottleAndAxles = new DriveActions(true, true, false, false);

        //The lessons the tutorial saved as taught
        private readonly List<string> taught = new List<string>();

        public TutorialTests()
        {
            //A lever set by an earlier test or the command line would change which lessons come
            Tutorial.Lever = TutorialLever.None;
        }

        public void Dispose()
        {
            Tutorial.Lever = TutorialLever.None;
        }

        private Tutorial Start(int level, params string[] taughtBefore)
        {
            return new Tutorial(level, taughtBefore.ToList(), taught.Add);
        }

        private static void Run(Tutorial tutorial, float seconds, DriveActions actions, float carSpeed = Moving, bool racing = true)
        {
            for (float time = 0f; time < seconds; time += Frame)
                tutorial.Update(Frame, racing, actions, carSpeed);
        }

        /// <summary>The cards shown in turn over a while, the player doing nothing (the actions give up).</summary>
        private static List<string> Cards(Tutorial tutorial, float seconds)
        {
            List<string> cards = new List<string>();
            string last = null;
            for (float time = 0f; time < seconds; time += Frame)
            {
                tutorial.Update(Frame, true, Nothing, Moving);
                string key = tutorial.Lesson?.Key;
                if (key != null && key != last)
                    cards.Add(key);
                last = key;
            }
            return cards;
        }

        [Fact]
        public void TheFirstLevelTeachesDrivingAndThePar()
        {
            Assert.Equal(new[] { "drive", "par" }, Cards(Start(0), 40f));
        }

        [Fact]
        public void ALevelShowsItsOwnLessonsEvenWhenTaught()
        {
            Assert.Equal(new[] { "drive", "par" }, Cards(Start(0, "drive", "par"), 40f));
        }

        [Fact]
        public void ALessonNotTaughtFollowsIntoTheNextLevels()
        {
            Assert.Equal(new[] { "drive", "par", "axles" }, Cards(Start(1), 40f));
        }

        [Fact]
        public void ALessonTaughtIsNotShownInTheNextLevels()
        {
            Assert.Equal(new[] { "axles" }, Cards(Start(1, "drive", "par"), 40f));
        }

        /// <summary>A lesson is taught on a level by its position in the level select, so the order of the levels decides it.</summary>
        [Theory]
        [InlineData("drive", "training1")]
        [InlineData("par", "training1")]
        [InlineData("axles", "training2")]
        [InlineData("lock", "startblock")]
        public void TheLessonsAreOnTheLevelsMeantForThem(string lesson, string level)
        {
            Assert.Equal(level, Repository.LevelNames()[Tutorial.Lessons.Single(l => l.Key == lesson).FromLevel]);
        }

        [Fact]
        public void NoCardComesBeforeTheStart()
        {
            Tutorial tutorial = Start(0);
            Run(tutorial, 5f, Nothing, racing: false);
            Assert.Null(tutorial.Lesson);
            Run(tutorial, 1.1f, Nothing);
            Assert.Equal("drive", tutorial.Lesson?.Key);
        }

        [Fact]
        public void AnActionIsDoneByHoldingItsControl()
        {
            Tutorial tutorial = Start(0);
            Run(tutorial, 1.1f, Nothing);
            Run(tutorial, 0.5f, Throttle);
            Assert.Null(tutorial.PraiseAge);
            Assert.Empty(taught);

            Run(tutorial, 0.6f, Throttle);
            Assert.NotNull(tutorial.PraiseAge);
            Assert.Equal(new[] { "drive" }, taught);
        }

        [Fact]
        public void AnActionNotDoneGivesUpUntaught()
        {
            Tutorial tutorial = Start(0);
            Run(tutorial, 30f, Nothing);
            Assert.Empty(taught);
        }

        [Fact]
        public void ANoticeIsTaughtWhenRead()
        {
            Tutorial tutorial = Start(0);
            Run(tutorial, 1.1f, Nothing);
            Run(tutorial, 1.1f, Throttle);
            Run(tutorial, 15f, Nothing);
            Assert.Equal(new[] { "drive", "par" }, taught);
        }

        [Fact]
        public void TheFinishTakesTheCardAwayUntaught()
        {
            Tutorial tutorial = Start(0);
            Run(tutorial, 1.1f, Nothing);
            Run(tutorial, Frame, Throttle, racing: false);
            Assert.Null(tutorial.Lesson);
            Assert.Empty(taught);
        }

        //The third level: no lessons of its own, and those before it taught, so only the stuck hint can come

        [Fact]
        public void TheStuckHintComesAfterThreeSecondsOfTheThrottleWithNoProgress()
        {
            Tutorial tutorial = Start(2, "drive", "par", "axles");
            Run(tutorial, 2.9f, Throttle, Standing);
            Assert.Null(tutorial.Lesson);
            Run(tutorial, 0.2f, Throttle, Standing);
            Assert.Same(Tutorial.StuckHint, tutorial.Lesson);
        }

        [Fact]
        public void StandingStillWithoutTheThrottleIsNotBeingStuck()
        {
            Tutorial tutorial = Start(2, "drive", "par", "axles");
            Run(tutorial, 10f, Nothing, Standing);
            Assert.Null(tutorial.Lesson);
        }

        [Fact]
        public void ACarMakingProgressIsNotStuck()
        {
            Tutorial tutorial = Start(2, "drive", "par", "axles");
            Run(tutorial, 10f, Throttle, 5f);
            Assert.Null(tutorial.Lesson);
        }

        [Fact]
        public void TurningTheAxlesStartsTheCountAgain()
        {
            Tutorial tutorial = Start(2, "drive", "par", "axles");
            Run(tutorial, 2f, Throttle, Standing);
            Run(tutorial, Frame, ThrottleAndAxles, Standing);
            Run(tutorial, 2f, Throttle, Standing);
            Assert.Null(tutorial.Lesson);
            Run(tutorial, 1.1f, Throttle, Standing);
            Assert.Same(Tutorial.StuckHint, tutorial.Lesson);
        }

        [Fact]
        public void TheHintIsDoneByTurningTheAxlesAndNeverSaved()
        {
            Tutorial tutorial = Start(2, "drive", "par", "axles");
            Run(tutorial, 3.1f, Throttle, Standing);
            Run(tutorial, 0.7f, ThrottleAndAxles, Standing);
            Assert.NotNull(tutorial.PraiseAge);
            Assert.Empty(taught);
        }

        [Fact]
        public void AnUnheededHintComesAgainOnlyAfterThreeMoreSeconds()
        {
            Tutorial tutorial = Start(2, "drive", "par", "axles");
            Run(tutorial, 3.1f, Throttle, Standing);
            Assert.Same(Tutorial.StuckHint, tutorial.Lesson);

            //It stays 7 s and takes 0.35 s to leave; the count starts again when it is gone
            Run(tutorial, 7.5f, Throttle, Standing);
            Assert.Null(tutorial.Lesson);
            Run(tutorial, 2.5f, Throttle, Standing);
            Assert.Null(tutorial.Lesson);
            Run(tutorial, 0.5f, Throttle, Standing);
            Assert.Same(Tutorial.StuckHint, tutorial.Lesson);
        }
    }
}
