using System;
using TW3D.GamePlay;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>
    /// The creak of the shock absorbers (GamePlay/SpringCreak) from how fast a spring moves, at the speeds measured in the
    /// levels: silent on a flat road, soft over humps, loud on steps and landings, dying away after a stroke.
    /// </summary>
    public class SpringCreakTests
    {
        //A frame at 60 frames a second
        private const float Frame = 1f / 60f;

        /// <summary>Runs the creak for some seconds with the spring moving at a speed given by the time.</summary>
        private static void Run(SpringCreak creak, float seconds, Func<float, float> speed)
        {
            for (float t = 0f; t < seconds; t += Frame)
                creak.Update(speed(t), Frame);
        }

        [Fact]
        public void AFlatRoadDoesNotCreak()
        {
            SpringCreak creak = new SpringCreak(0f);
            float loudest = 0f;
            //The springs of a car on a flat road or pushing against a step bob at about 1 physics unit a second at most
            for (float t = 0f; t < 5f; t += Frame)
            {
                creak.Update(MathF.Sin(t * 37f), Frame);
                loudest = Math.Max(loudest, creak.Level);
            }
            Assert.Equal(0f, loudest);
        }

        [Fact]
        public void AHumpCreaksSofterThanALanding()
        {
            SpringCreak hump = new SpringCreak(0f), landing = new SpringCreak(0f);
            Run(hump, 0.1f, t => 3f);
            Run(landing, 0.1f, t => 15f);
            Assert.InRange(hump.Level, 0.15f, 0.5f);
            Assert.InRange(landing.Level, 0.95f, 1f);
        }

        [Fact]
        public void AStrokeCreaksAtOnceAndDiesAwayAfterIt()
        {
            SpringCreak creak = new SpringCreak(0f);
            Run(creak, 2f * Frame, t => 12f);
            Assert.True(creak.Level > 0.6f, $"{creak.Level} after two frames");
            Run(creak, 0.1f, t => 0f);
            Assert.True(creak.Level > 0.2f, $"{creak.Level} a tenth of a second after the stroke");
            Run(creak, 0.4f, t => 0f);
            Assert.True(creak.Level < 0.02f, $"{creak.Level} half a second after the stroke");
        }

        [Fact]
        public void AFasterStrokeCreaksHigher()
        {
            SpringCreak slow = new SpringCreak(0f), fast = new SpringCreak(0f);
            Run(slow, 0.1f, t => 3f);
            Run(fast, 0.1f, t => 12f);
            Assert.True(fast.Pitch > slow.Pitch);
        }

        [Fact]
        public void ACompressionCreaksLowerThanARebound()
        {
            SpringCreak compressing = new SpringCreak(0f), rebounding = new SpringCreak(0f);
            Run(compressing, 0.1f, t => 8f);
            Run(rebounding, 0.1f, t => -8f);
            Assert.True(compressing.Pitch < rebounding.Pitch);
        }

        [Fact]
        public void ThePitchStaysAsTheCreakDiesAway()
        {
            SpringCreak creak = new SpringCreak(0f);
            Run(creak, 0.1f, t => 10f);
            float pitch = creak.Pitch;
            Run(creak, 0.3f, t => 0.5f);
            Assert.Equal(pitch, creak.Pitch);
        }

        [Fact]
        public void TheAxlesCreakDifferently()
        {
            SpringCreak rear = new SpringCreak(-0.06f), front = new SpringCreak(0.06f);
            Run(rear, 0.1f, t => 8f);
            Run(front, 0.1f, t => 8f);
            Assert.Equal(rear.Level, front.Level);
            Assert.True(rear.Pitch < front.Pitch);
        }
    }
}
