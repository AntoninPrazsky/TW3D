using System;

namespace TW3D.GamePlay
{
    /// <summary>
    /// When the shock absorbers of one axle creak (the sound <see cref="TW3D.Audio.Sound.Suspension"/>, once for each
    /// stroke) and how loud, from how fast the spring of the axle moves. A stroke is the spring moving one way faster than
    /// <see cref="MinSpeed"/>; it creaks once the stroke has passed its fastest (the spring slows down or turns back), the
    /// louder the faster it was, and the axle does not creak again for <see cref="QuietTime"/>. A spring at rest or over a
    /// flat road never creaks. Even a soft creak plays fairly loud: softer, the engine left only its squeak to be heard,
    /// which sounded robotic. No MonoGame in it, so the tests run it.
    /// </summary>
    public sealed class SpringCreak
    {
        //How fast a spring moves for a stroke to creak, and to creak at its loudest (physics units a second). Measured with
        //the Buggy: a flat road, a car at rest and one pushing against a step move the springs at most about 1 (a car
        //banging against a step now and then 3), the humps of the docks 1 to 3.5, the stairs, the steps and the landings
        //4 to 22
        public const float MinSpeed = 2f;
        public const float FullSpeed = 14f;
        //An axle does not creak again until this long after it creaked (seconds): about the length of the creak
        public const float QuietTime = 0.3f;
        //The softest creak, as a share of the sound's volume
        private const float Softest = 0.6f;

        //How fast the stroke under way has moved at most (0 with none under way) and which way; how long to keep quiet
        private float fastest;
        private int way;
        private float quiet;

        /// <summary>Follows the spring; true when the axle creaks now.</summary>
        /// <param name="speed">How fast the spring moves (physics units a second): more than 0 compressing, less rebounding.</param>
        /// <param name="elapsed">Real seconds since the last update.</param>
        /// <param name="volume">How loud it creaks, as a share of the sound's volume.</param>
        public bool Update(float speed, float elapsed, out float volume)
        {
            volume = 0f;
            quiet = Math.Max(0f, quiet - elapsed);
            float moving = Math.Abs(speed);
            if (fastest > 0f)
            {
                //The stroke goes on as fast or faster: wait for its fastest
                if (Math.Sign(speed) == way && moving >= fastest)
                {
                    fastest = moving;
                    return false;
                }
                volume = Softest + (1f - Softest) * Math.Clamp((fastest - MinSpeed) / (FullSpeed - MinSpeed), 0f, 1f);
                fastest = 0f;
                quiet = QuietTime;
                return true;
            }
            if (quiet <= 0f && moving > MinSpeed)
            {
                fastest = moving;
                way = Math.Sign(speed);
            }
            return false;
        }
    }
}
