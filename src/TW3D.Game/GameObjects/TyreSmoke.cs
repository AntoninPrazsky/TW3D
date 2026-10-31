using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TW3D.Graphics;
using TW3D.Shared;

namespace TW3D.GameObjects
{
    /// <summary>
    /// The smoke of the tyres slipping hard (sliding braked or spinning): soft puffs rising from under the slipping wheels
    /// on both sides of the car, growing and thinning out, more and thicker the faster the tyres slip.
    /// </summary>
    sealed class TyreSmoke
    {
        //The tyres smoke slipping faster than this (physics units a second), the most from this speed: puffs a second
        //from each side of a wheel, how thick they are at first
        private const float MinSlip = 10f;
        private const float FullSlip = 30f;
        private const float Rate = 14f;
        private const float Opacity = 0.4f;
        //How long a puff lives (seconds, give or take a quarter), how big it is at first and at the end (radius, scene
        //units), how fast it rises and how fast what it was thrown with dies away (scene units a second, 1 / seconds)
        private const float Life = 1.6f;
        private const float StartSize = 0.1f;
        private const float EndSize = 0.6f;
        private const float Rise = 0.3f;
        private const float Drag = 3f;
        private const float Spread = 0.4f;
        //The oldest puffs go when there are more than this many
        private const int MaxPuffs = 400;

        private struct Puff
        {
            public Vector3 Position, Velocity;
            public float Age, Life, Thickness;
        }

        private readonly List<Puff> puffs = new List<Puff>();
        private readonly Random random = new Random(7);
        //The share of a puff each wheel owes, carried over to the next update
        private readonly float[] owed = new float[Car.WheelCount];

        /// <summary>Moves and ages the puffs and adds those of the wheels slipping now.</summary>
        /// <param name="elapsed">Real seconds since the last update.</param>
        /// <param name="slipping">The wheels slipping (see <see cref="Car.SlippingWheels"/>).</param>
        public void Update(float elapsed, Car car, List<WheelSlip> slipping)
        {
            float drag = MathF.Exp(-Drag * elapsed);
            for (int i = puffs.Count - 1; i >= 0; i--)
            {
                Puff puff = puffs[i];
                puff.Age += elapsed;
                if (puff.Age >= puff.Life)
                {
                    puffs.RemoveAt(i);
                    continue;
                }
                puff.Position += (puff.Velocity + new Vector3(0f, Rise, 0f)) * elapsed;
                puff.Velocity *= drag;
                puffs[i] = puff;
            }

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
                        puffs.Add(new Puff
                        {
                            Position = at + new Vector3(0f, StartSize, z),
                            Velocity = thrown + new Vector3(Jitter(), Math.Abs(Jitter()) * 0.5f, Jitter() * 0.5f),
                            Life = Life * (0.75f + 0.5f * (float)random.NextDouble()),
                            Thickness = MathHelper.Lerp(0.5f, 1f, share)
                        });
                    }
                }
            }

            if (puffs.Count > MaxPuffs)
                puffs.RemoveRange(0, puffs.Count - MaxPuffs);
        }

        private float Jitter() => ((float)random.NextDouble() * 2f - 1f) * Spread;

        /// <summary>Gives the renderer the puffs to draw; called between its Begin and End.</summary>
        public void Draw(SceneRenderer renderer)
        {
            foreach (Puff puff in puffs)
            {
                float t = puff.Age / puff.Life;
                float size = MathHelper.Lerp(StartSize, EndSize, MathF.Sqrt(t));
                float opacity = Opacity * puff.Thickness * MathHelper.Clamp(puff.Age / 0.1f, 0f, 1f) * (1f - t) * (1f - t);
                renderer.AddSmoke(puff.Position, size, opacity);
            }
        }
    }
}
