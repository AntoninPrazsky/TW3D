using TW3D.GamePlay;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>A run of a level (GamePlay/Race): the start, the finish and the car falling off the level.</summary>
    public class RaceTests
    {
        private const float Step = 0.1f;

        /// <summary>A race past its countdown, at simulated time 0.</summary>
        private static Race Started()
        {
            Race race = new Race(10f, true);
            race.Update(Race.CountdownTime, 0.0, false, false);
            Assert.Equal(RaceState.Racing, race.State);
            return race;
        }

        /// <summary>Runs a race for some seconds from a simulated time; true if it ended in them.</summary>
        private static bool Run(Race race, ref double time, float seconds, bool atFinish, bool belowLevel)
        {
            bool ended = false;
            for (float t = 0f; t < seconds - Step / 2f; t += Step)
            {
                time += Step;
                ended |= race.Update(Step, time, atFinish, belowLevel);
            }
            return ended;
        }

        [Fact]
        public void FallingBelowTheLevelForAWhileLosesTheRun()
        {
            Race race = Started();
            double time = 0.0;
            Assert.False(Run(race, ref time, Race.FallTime - 0.2f, false, true));
            Assert.Equal(RaceState.Racing, race.State);
            Assert.True(Run(race, ref time, 0.3f, false, true));
            Assert.Equal(RaceState.Lost, race.State);
        }

        [Fact]
        public void ACarBackOnTheLevelFallsAfreshNextTime()
        {
            Race race = Started();
            double time = 0.0;
            Run(race, ref time, Race.FallTime - 0.2f, false, true);
            Run(race, ref time, Step, false, false);
            Assert.Equal(0f, race.Falling);
            Assert.False(Run(race, ref time, Race.FallTime - 0.2f, false, true));
            Assert.Equal(RaceState.Racing, race.State);
        }

        [Fact]
        public void FallingDuringTheCountdownDoesNotLose()
        {
            Race race = new Race(10f, true);
            for (int i = 0; i < 20; i++)
                race.Update(Step, 0.0, false, true);
            Assert.Equal(RaceState.Countdown, race.State);
            Assert.Equal(0f, race.Falling);
        }

        [Fact]
        public void TheResultsFollowTheFinishAndTheLossAfterAWhile()
        {
            foreach (bool lost in new[] { false, true })
            {
                Race race = Started();
                double time = 0.0;
                Run(race, ref time, 5f, false, false);
                Assert.True(Run(race, ref time, lost ? Race.FallTime + Step : Step, !lost, lost));
                Assert.Equal(lost ? RaceState.Lost : RaceState.Finished, race.State);
                Assert.False(race.ResultsDue);
                Run(race, ref time, 2f, false, false);
                Assert.True(race.ResultsDue);
            }
        }

        [Fact]
        public void TheTimeStopsAtTheEnd()
        {
            Race race = Started();
            double time = 0.0;
            Run(race, ref time, 4f, false, false);
            Run(race, ref time, Step, true, false);
            float finished = race.Time;
            Run(race, ref time, 3f, false, false);
            Assert.Equal(finished, race.Time);
        }
    }
}
