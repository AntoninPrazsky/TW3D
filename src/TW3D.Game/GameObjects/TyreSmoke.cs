using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TW3D.Graphics;
using TW3D.Shared;

namespace TW3D.GameObjects
{
    /// <summary>
    /// The smoke of the tyres slipping hard (sliding braked or spinning): big soft puffs rising from under the slipping
    /// wheels on both sides of the car, growing and thinning out, more and thicker the faster the tyres slip.
    /// </summary>
    sealed class TyreSmoke
    {
        //The tyres smoke slipping faster than this (physics units a second), the most from this speed: puffs a second
        //from each side of a wheel
        private const float MinSlip = 10f;
        private const float FullSlip = 30f;
        private const float Rate = 14f;
        //How fast the puffs fly apart (scene units a second)
        private const float Spread = 0.4f;

        private readonly Smoke smoke = new Smoke { Life = 1.6f, StartSize = 0.1f, EndSize = 0.6f, Rise = 0.3f, Drag = 3f, Opacity = 0.4f };
        //The share of a puff each wheel owes, carried over to the next update
        private readonly float[] owed = new float[Car.WheelCount];

        /// <summary>Moves and ages the puffs and adds those of the wheels slipping now.</summary>
        /// <param name="elapsed">Real seconds since the last update.</param>
        /// <param name="slipping">The wheels slipping (see <see cref="Car.SlippingWheels"/>).</param>
        public void Update(float elapsed, Car car, List<WheelSlip> slipping)
        {
            smoke.Update(elapsed);
            foreach (WheelSlip slip in slipping)
            {
                float share = MathHelper.Clamp((slip.Speed - MinSlip) / (FullSlip - MinSlip), 0f, 1f);
                if (share <= 0f)
                    continue;
                owed[slip.Wheel] += Rate * share * elapsed;
                Vector3 at = new Vector3(-slip.Point * Units.PhysicsToScene, 0f);
                //Thrown along with the ground under the tyre
                Vector2 ground = slip.Ground.GetLinearVelocityFromWorldPoint(slip.Point);
                Vector3 thrown = new Vector3(-ground * Units.PhysicsToScene, 0f);
                while (owed[slip.Wheel] >= 1f)
                {
                    owed[slip.Wheel] -= 1f;
                    float side = car.WheelSide(slip.Wheel);
                    foreach (float z in new[] { side, -side })
                    {
                        Vector3 spread = new Vector3(smoke.Jitter(), System.Math.Abs(smoke.Jitter()) * 0.5f, smoke.Jitter() * 0.5f) * Spread;
                        smoke.Emit(at + new Vector3(0f, smoke.StartSize, z), thrown + spread, MathHelper.Lerp(0.5f, 1f, share));
                    }
                }
            }
        }

        /// <summary>Gives the renderer the puffs to draw; called between its Begin and End.</summary>
        public void Draw(SceneRenderer renderer) => smoke.Draw(renderer);
    }
}
