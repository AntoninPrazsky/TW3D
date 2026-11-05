using System;

namespace TW3D.GamePlay
{
    /// <summary>
    /// The creak of the shock absorbers of one axle (the loop <see cref="TW3D.Audio.Sound.Suspension"/>): how loud and how
    /// high it plays, from how fast the spring of the axle moves. A spring at rest or over a flat road is silent, a hump
    /// creaks it softly, a step, a landing or a bounce loudly. The creak comes at once and dies away over a moment, so a
    /// quick stroke is heard; the faster the spring moves, the faster it creaks, a compression a little lower than a
    /// rebound. No MonoGame in it, so the tests run it.
    /// </summary>
    public sealed class SpringCreak
    {
        //How fast a spring moves for its creak to be heard, and to be at its loudest (physics units a second). Measured
        //with the Buggy: a flat road, a car at rest and one pushing against a step move the springs at most about 1, the
        //humps of the docks 1 to 3.5, the stairs, the steps and the landings 4 to 22
        public const float MinSpeed = 1.5f;
        public const float FullSpeed = 14f;
        //The softest creak heard, as a share of the sound's volume
        private const float Softest = 0.2f;
        //How fast the creak comes and dies away (1 / seconds)
        private const float Attack = 60f;
        private const float Release = 10f;
        //The pitch (octaves) of the slowest and of the fastest creak; a compression lower and a rebound higher by this much
        private const float SlowPitch = -0.2f;
        private const float FastPitch = 0.25f;
        private const float StrokePitch = 0.08f;

        private readonly float axlePitch;

        /// <param name="axlePitch">The axle's own pitch (octaves): the rear and the front one creak a little differently.</param>
        public SpringCreak(float axlePitch)
        {
            this.axlePitch = axlePitch;
            Pitch = axlePitch;
        }

        /// <summary>How loud the creak plays, as a share of the sound's volume (0..1).</summary>
        public float Level { get; private set; }

        /// <summary>How high it plays (octaves).</summary>
        public float Pitch { get; private set; }

        /// <param name="speed">How fast the spring moves (physics units a second): more than 0 compressing, less rebounding.</param>
        /// <param name="elapsed">Real seconds since the last update.</param>
        public void Update(float speed, float elapsed)
        {
            float moving = Math.Abs(speed);
            float share = Math.Clamp((moving - MinSpeed) / (FullSpeed - MinSpeed), 0f, 1f);
            float target = moving > MinSpeed ? Softest + (1f - Softest) * share : 0f;
            Level += (target - Level) * (1f - MathF.Exp(-(target > Level ? Attack : Release) * elapsed));
            //The pitch follows the spring while it creaks and stays as the creak dies away
            if (target > 0f)
                Pitch = Math.Clamp(axlePitch + SlowPitch + (FastPitch - SlowPitch) * share + (speed > 0f ? -StrokePitch : StrokePitch), -1f, 1f);
        }
    }
}
