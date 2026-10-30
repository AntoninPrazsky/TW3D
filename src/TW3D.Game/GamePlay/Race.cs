namespace TW3D.GamePlay
{
    public enum RaceState
    {
        /// <summary>The countdown before the start; the car is held still.</summary>
        Countdown,
        Racing,
        /// <summary>The car has reached the finish; the results follow after a while.</summary>
        Finished
    }

    /// <summary>
    /// A run of a level: the countdown before the start, the time from the start until the car reaches the finish, and
    /// the wait at the finish before the results. The time is measured by the simulation, not by the frames, so the run
    /// is timed fairly even when frames are slow; it is in real seconds (the game runs faster than real time, see
    /// GameplayScreen.GameSpeed).
    /// </summary>
    public sealed class Race
    {
        /// <summary>The countdown before the start, in seconds.</summary>
        public const float CountdownTime = 3f;
        //How long the results wait after the finish (seconds)
        private const float ResultsDelay = 1.5f;

        //The simulated time at the start
        private double startTime;

        /// <param name="par">The par time of the level (see <see cref="RaceScore"/>).</param>
        /// <param name="hasFinish">The level has a finish, so the run can end.</param>
        public Race(float par, bool hasFinish)
        {
            Par = par;
            HasFinish = hasFinish;
        }

        public RaceState State { get; private set; } = RaceState.Countdown;

        /// <summary>Seconds left of the countdown.</summary>
        public float Countdown { get; private set; } = CountdownTime;

        /// <summary>The time of the run in seconds: from the start until now, or until the finish.</summary>
        public float Time { get; private set; }

        /// <summary>Seconds since the finish.</summary>
        public float SinceFinish { get; private set; }

        public float Par { get; }

        public bool HasFinish { get; }

        /// <summary>The car was moved by hand during the run, which then does not count.</summary>
        public bool Assisted { get; set; }

        /// <summary>The finish was long enough ago for the results to follow.</summary>
        public bool ResultsDue => State == RaceState.Finished && SinceFinish >= ResultsDelay;

        /// <summary>Moves the run on; true the moment it finishes.</summary>
        /// <param name="elapsed">Elapsed real seconds.</param>
        /// <param name="simulatedTime">How much time the simulation has run so far, in real seconds.</param>
        /// <param name="atFinish">The car is at the finish.</param>
        public bool Update(float elapsed, double simulatedTime, bool atFinish)
        {
            switch (State)
            {
                case RaceState.Countdown:
                    Countdown -= elapsed;
                    if (Countdown <= 0f)
                    {
                        State = RaceState.Racing;
                        startTime = simulatedTime;
                    }
                    return false;

                case RaceState.Racing:
                    Time = (float)(simulatedTime - startTime);
                    if (!atFinish)
                        return false;
                    State = RaceState.Finished;
                    return true;

                default:
                    SinceFinish += elapsed;
                    return false;
            }
        }
    }
}
