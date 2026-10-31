using Microsoft.Xna.Framework;
using TW3D.Graphics;

namespace TW3D.GameObjects
{
    /// <summary>
    /// The smoke of the exhaust: small bluish grey puffs blown out to the back of the car from the ends of its pipes (see
    /// <see cref="Car.Exhausts"/>), thin and few idling, more and thicker under the throttle and a burst when it is pressed
    /// (the engine revving up). Wispier than the smoke of the tyres, it thins out quicker and hardly rises.
    /// </summary>
    sealed class ExhaustSmoke
    {
        //Puffs a second from each pipe idling and at full throttle, and in a burst when the throttle is pressed; how thick
        //they are idling (at full throttle and in a burst, as thick as they get)
        private const float IdleRate = 6f;
        private const float LoadRate = 24f;
        private const float BurstRate = 40f;
        private const float IdleThickness = 0.4f;
        //How fast the throttle is followed and a burst dies away (1 / seconds); the throttle pressed further than this
        //at once starts a burst
        private const float FollowRate = 6f;
        private const float BurstFade = 2.5f;
        private const float BurstPress = 0.3f;
        //How fast the puffs blow out of the pipes and fly apart (scene units a second)
        private const float Blow = 1.5f;
        private const float Spread = 0.15f;

        private readonly Smoke smoke = new Smoke
        {
            Life = 1.1f,
            StartSize = 0.03f,
            EndSize = 0.35f,
            Rise = 0.12f,
            Drag = 2.5f,
            Opacity = 0.65f,
            Tint = new Vector3(0.8f, 0.9f, 1.15f)
        };
        private readonly Vector3[] ends = new Vector3[2];
        private float load, burst, pressed, owed;

        /// <summary>Moves and ages the puffs and blows out new ones.</summary>
        /// <param name="elapsed">Real seconds since the last update.</param>
        /// <param name="throttle">How far the throttle is held, 0..1.</param>
        public void Update(float elapsed, Car car, float throttle)
        {
            smoke.Update(elapsed);
            if (!car.Exhausts(ends, out Vector3 blowing))
                return;

            throttle = MathHelper.Clamp(throttle, 0f, 1f);
            if (throttle - pressed > BurstPress)
                burst = 1f;
            pressed = throttle;
            burst = MathHelper.Max(0f, burst - BurstFade * elapsed);
            load = MathHelper.Lerp(load, throttle, 1f - System.MathF.Exp(-FollowRate * elapsed));

            owed += (MathHelper.Lerp(IdleRate, LoadRate, load) + BurstRate * burst) * elapsed;
            float thickness = MathHelper.Lerp(IdleThickness, 1f, MathHelper.Max(load, burst));
            while (owed >= 1f)
            {
                owed -= 1f;
                foreach (Vector3 end in ends)
                {
                    Vector3 spread = new Vector3(smoke.Jitter(), smoke.Jitter(), smoke.Jitter()) * Spread;
                    smoke.Emit(end, car.Velocity + blowing * Blow + spread, thickness);
                }
            }
        }

        /// <summary>Gives the renderer the puffs to draw; called between its Begin and End.</summary>
        public void Draw(SceneRenderer renderer) => smoke.Draw(renderer);
    }
}
