using System;
using System.Collections.Generic;
using TW3D.GamePlay;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>
    /// When the shock absorbers creak (GamePlay/SpringCreak) from how fast a spring moves, at the speeds measured in the
    /// levels: never on a flat road, once for each stroke of a hump, a step or a landing, as loud as the stroke was fast.
    /// </summary>
    public class SpringCreakTests
    {
        //A frame at 60 frames a second
        private const float Frame = 1f / 60f;

        /// <summary>Runs the creak for some seconds with the spring moving at a speed given by the time; the creaks heard.</summary>
        private static List<(float Time, float Volume, float Pitch)> Run(SpringCreak creak, float seconds, Func<float, float> speed)
        {
            List<(float, float, float)> creaks = new List<(float, float, float)>();
            for (float t = 0f; t < seconds; t += Frame)
                if (creak.Update(speed(t), Frame, out float volume, out float pitch))
                    creaks.Add((t, volume, pitch));
            return creaks;
        }

        /// <summary>A stroke of the spring: its speed rising to the fastest and falling back over a tenth of a second.</summary>
        private static Func<float, float> Stroke(float fastest, float start = 0f) =>
            t => t < start || t > start + 0.1f ? 0f : fastest * MathF.Sin(MathF.PI * (t - start) / 0.1f);

        [Fact]
        public void AFlatRoadNeverCreaks()
        {
            //The springs of a car on a flat road or pushing against a step bob at about 1 physics unit a second at most
            Assert.Empty(Run(new SpringCreak(0f), 5f, t => 1.5f * MathF.Sin(t * 37f)));
        }

        [Fact]
        public void AStrokeCreaksOnceAtItsFastest()
        {
            List<(float Time, float Volume, float Pitch)> creaks = Run(new SpringCreak(0f), 0.5f, Stroke(10f));
            Assert.Single(creaks);
            Assert.InRange(creaks[0].Time, 0.05f, 0.05f + 2f * Frame);
        }

        [Fact]
        public void ALandingCreaksLouderThanAHump()
        {
            float hump = Run(new SpringCreak(0f), 0.5f, Stroke(3f))[0].Volume;
            float landing = Run(new SpringCreak(0f), 0.5f, Stroke(15f))[0].Volume;
            Assert.InRange(hump, 0.25f, 0.4f);
            Assert.Equal(1f, landing, 3);
        }

        [Fact]
        public void AnAxleKeepsQuietForAMomentAfterItCreaks()
        {
            Func<float, float> twoClose = t => Stroke(8f)(t) + Stroke(-8f, 0.15f)(t);
            Func<float, float> twoApart = t => Stroke(8f)(t) + Stroke(-8f, 0.45f)(t);
            Assert.Single(Run(new SpringCreak(0f), 1f, twoClose));
            Assert.Equal(2, Run(new SpringCreak(0f), 1f, twoApart).Count);
        }

        [Fact]
        public void ACompressionCreaksLowerThanARebound()
        {
            float compressing = Run(new SpringCreak(0f), 0.5f, Stroke(8f))[0].Pitch;
            float rebounding = Run(new SpringCreak(0f), 0.5f, Stroke(-8f))[0].Pitch;
            Assert.True(compressing < rebounding);
        }

        [Fact]
        public void AFasterStrokeCreaksHigher()
        {
            float slow = Run(new SpringCreak(0f), 0.5f, Stroke(4f))[0].Pitch;
            float fast = Run(new SpringCreak(0f), 0.5f, Stroke(12f))[0].Pitch;
            Assert.True(fast > slow);
        }

        [Fact]
        public void TheAxlesCreakDifferently()
        {
            var rear = Run(new SpringCreak(-0.06f), 0.5f, Stroke(8f))[0];
            var front = Run(new SpringCreak(0.06f), 0.5f, Stroke(8f))[0];
            Assert.Equal(rear.Volume, front.Volume);
            Assert.True(rear.Pitch < front.Pitch);
        }
    }
}
