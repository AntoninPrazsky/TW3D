using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using nkast.Aether.Physics2D.Dynamics;
using TW3D.Graphics;
using TW3D.Shared;

namespace TW3D.GameObjects
{
    /// <summary>
    /// The earth the tyres throw up on the earth of the off-road track (#59): clods and crumbs leaving the tread as it turns
    /// up from the ground, flung on the way the wheel turns (a rooster tail behind a spinning wheel) and carried on with the
    /// wheel (pushed ahead of a wheel sliding braked), more the faster it slides, a few crumbs from a wheel rolling fast,
    /// a spray where a wheel lands hard, and a brown dust where the tyre
    /// slips (in place of the white smoke of the tyres, see <see cref="TyreSmoke"/>); and what sticks to the car
    /// (<see cref="CarMud"/>), by the same measures of the sliding, the rolling and the landings. The pieces fly under the gravity of
    /// the physics (in the time of the game, which runs faster than real time), turn darker as they fall and fade out; a
    /// transition block throws half as much. None on the classic slab, the asphalt or an object.
    /// </summary>
    sealed class DirtSpray
    {
        //The pieces on the screen at most; the oldest go first
        private const int MaxClods = 700;
        //Clods a second from each side of a wheel: sliding fast (from MinSlide to FullSlide physics units a second) and
        //rolling fast (up to RollSpeed); the mud on the car grows by the same (CarMud)
        private const float SlideRate = 100f;
        private const float RollRate = 10f;
        private const float MinSlide = 3f;
        private const float FullSlide = 28f;
        private const float RollSpeed = 60f;
        //How much of the speed of the tyre over the ground the dust is blown with, at most (physics units a second)
        private const float Throw = 0.55f;
        private const float MaxThrow = 22f;
        //A clod keeps this part of the velocity of the wheel, and is flung with this part of the speed of the tread round the
        //wheel, at most MaxFling (physics units a second); it leaves the tread from ReleaseLeast to ReleaseMost (radians)
        //past the ground the way the wheel turns, most of them early; and it goes up from the ground this fast (scene units
        //a real second), from a slow slide to a fast one
        private const float Carry = 0.8f;
        private const float Fling = 0.35f;
        private const float MaxFling = 14f;
        private const float ReleaseLeast = 0.1f;
        private const float ReleaseMost = 1.7f;
        private const float MinLift = 0.2f;
        private const float MaxLift = 0.9f;
        //A wheel landing faster than this (physics units a second, down onto the ground) sprays clods, the most from LandFull
        private const float LandSpeed = 10f;
        private const float LandFull = 40f;
        private const float LandClods = 26f;
        //How big a piece is (radius, scene units), how long it flies (real seconds) and how far across the rut it starts
        private const float MinSize = 0.012f;
        private const float MaxSize = 0.042f;
        private const float MinLife = 0.45f;
        private const float MaxLife = 1.0f;
        private const float RutHalfWidth = 0.08f;
        //The colours of the earth (sRGB, as the materials of the off-road blocks have them) the pieces are, and of the dust
        private static readonly Vector3 Earth = new Vector3(0.38f, 0.3f, 0.22f);
        private static readonly Vector3 DarkEarth = new Vector3(0.27f, 0.2f, 0.14f);
        private static readonly Vector3 DustTint = new Vector3(1.3f, 0.95f, 0.6f);

        private struct Clod
        {
            public Vector3 Position, Velocity, Colour;
            public float Age, Life, Size;
        }

        private readonly List<Clod> clods = new List<Clod>();
        private readonly Smoke dust = new Smoke { Life = 1.2f, StartSize = 0.08f, EndSize = 0.5f, Rise = 0.12f, Drag = 3.5f, Opacity = 0.35f, Tint = DustTint };
        private readonly Random random = new Random(59);
        private readonly List<WheelSlip> grounded = new List<WheelSlip>();
        //The share of a clod and of a puff of dust each wheel owes, carried over to the next update; whether each wheel stood
        //on the ground and how fast it moved in the last update
        private readonly float[] owedClods = new float[Car.WheelCount], owedDust = new float[Car.WheelCount];
        private readonly bool[] stood = new bool[Car.WheelCount];
        private readonly Vector2[] lastVelocity = new Vector2[Car.WheelCount];
        //The game runs this many times faster than real time; the gravity of the physics in scene units a real second squared
        private readonly float gameSpeed;
        private readonly float gravity;

        /// <param name="gameSpeed">How many times faster than real time the physics runs.</param>
        /// <param name="gravity">The gravity of the physics (physics units a second squared, downwards).</param>
        public DirtSpray(float gameSpeed, float gravity)
        {
            this.gameSpeed = gameSpeed;
            this.gravity = gravity * Units.PhysicsToScene * gameSpeed * gameSpeed;
        }

        /// <summary>How much of the ground a body is earth: 1 a block of the off-road style, half a transition into it, else 0.</summary>
        public static float EarthOf(Body ground)
        {
            if (ground?.Tag is not TrackLook look || look.Style == null)
                return 0f;
            if (look.Style == "offroad")
                return 1f;
            return look.Style.Contains("offroad") ? 0.5f : 0f;
        }

        /// <summary>Moves the pieces and the dust and throws up new ones from the wheels on the earth.</summary>
        /// <param name="elapsed">Real seconds since the last update.</param>
        /// <param name="density">How many pieces, 0..1 (fewer at a lower detail of the surfaces).</param>
        /// <param name="mud">The mud on the car, which the wheels on the earth add to (whatever the density).</param>
        public void Update(float elapsed, Car car, float density, CarMud mud)
        {
            dust.Update(elapsed);
            for (int i = clods.Count - 1; i >= 0; i--)
            {
                Clod clod = clods[i];
                clod.Age += elapsed;
                if (clod.Age >= clod.Life)
                {
                    clods.RemoveAt(i);
                    continue;
                }
                clod.Velocity.Y -= gravity * elapsed;
                clod.Position += clod.Velocity * elapsed;
                clods[i] = clod;
            }

            car.SlippingWheels(grounded, 0f);
            bool[] standing = new bool[Car.WheelCount];
            foreach (WheelSlip wheel in grounded)
            {
                standing[wheel.Wheel] = true;
                float onEarth = EarthOf(wheel.Ground);
                float slide = MathHelper.Clamp((wheel.Speed - MinSlide) / (FullSlide - MinSlide), 0f, 1f);
                float roll = MathHelper.Clamp(car.Speed / RollSpeed, 0f, 1f);
                //Landing: the speed it came down with, onto the ground
                float landing = stood[wheel.Wheel] ? 0f
                    : MathHelper.Clamp((-Vector2.Dot(lastVelocity[wheel.Wheel], wheel.Up) - LandSpeed) / (LandFull - LandSpeed), 0f, 1f);
                //The mud on the car: more on the earth, washed off the wheels elsewhere
                mud?.Wheel(Car.IsFrontWheel(wheel.Wheel), onEarth, roll, slide, landing, elapsed);
                if (onEarth <= 0f)
                    continue;
                float earth = onEarth * density;
                Vector3 at = new Vector3(-wheel.Point * Units.PhysicsToScene, 0f);
                Vector3 up = new Vector3(-wheel.Up, 0f);
                float side = car.WheelSide(wheel.Wheel);

                if (landing > 0f)
                {
                    for (int n = (int)(LandClods * landing * earth); n > 0; n--)
                        foreach (float z in new[] { side, -side })
                        {
                            Vector3 velocity = up * Lerp(0.6f, 1.6f) + new Vector3(Jitter() * 0.9f, 0f, Jitter() * 0.3f);
                            Emit(at + new Vector3(0f, 0f, z + Jitter() * RutHalfWidth), velocity);
                        }
                }

                //Sliding and rolling: the earth leaves the tread as it turns up from the ground, flung on the way the tread
                //moves round the wheel and carried on with the wheel (physics coordinates, the angles as the physics turns)
                owedClods[wheel.Wheel] += (SlideRate * slide + RollRate * roll * roll) * earth * elapsed;
                Vector2 thrown = wheel.Slide.LengthSquared() > 0f ? wheel.Slide * Math.Min(1f, MaxThrow / wheel.Slide.Length()) : Vector2.Zero;
                Vector3 along = new Vector3(-thrown * Units.PhysicsToScene * gameSpeed * Throw, 0f);
                Vector2 centre = car.WheelCentre(wheel.Wheel), hub = car.WheelVelocity(wheel.Wheel);
                float spin = car.WheelTurn(wheel.Wheel);
                Vector2 toGround = wheel.Point - centre;
                float radius = toGround.Length(), groundAngle = MathF.Atan2(toGround.Y, toGround.X);
                float tread = Math.Min(Math.Abs(spin) * radius * Fling, MaxFling);
                while (owedClods[wheel.Wheel] >= 1f)
                {
                    owedClods[wheel.Wheel] -= 1f;
                    foreach (float z in new[] { side, -side })
                    {
                        float release = Math.Sign(spin) * MathHelper.Lerp(ReleaseLeast, ReleaseMost, (float)Math.Pow(random.NextDouble(), 2.0));
                        Vector2 outward = new Vector2(MathF.Cos(groundAngle + release), MathF.Sin(groundAngle + release));
                        Vector2 flung = new Vector2(-outward.Y, outward.X) * Math.Sign(spin) * tread * Lerp(0.6f, 1.1f);
                        float lift = MathHelper.Lerp(MinLift, MaxLift, slide) * Lerp(0.5f, 1.2f);
                        Vector3 velocity = new Vector3(-(hub * Carry + flung) * Units.PhysicsToScene * gameSpeed, 0f) + up * lift
                            + new Vector3(Jitter() * 0.15f, 0f, Jitter() * 0.2f);
                        Vector3 from = new Vector3(-(centre + outward * radius) * Units.PhysicsToScene, 0f);
                        Emit(from + new Vector3(0f, 0f, z + Jitter() * RutHalfWidth), velocity);
                    }
                }

                //Dust where the tyre slips, as the smoke of the tyres does elsewhere
                owedDust[wheel.Wheel] += 10f * slide * earth * elapsed;
                while (owedDust[wheel.Wheel] >= 1f)
                {
                    owedDust[wheel.Wheel] -= 1f;
                    foreach (float z in new[] { side, -side })
                        dust.Emit(at + new Vector3(0f, dust.StartSize, z), along * 0.3f + new Vector3(dust.Jitter(), Math.Abs(dust.Jitter()) * 0.5f, dust.Jitter() * 0.5f) * 0.3f,
                            MathHelper.Lerp(0.5f, 1f, slide));
                }
            }
            for (int i = 0; i < Car.WheelCount; i++)
            {
                stood[i] = standing[i];
                lastVelocity[i] = car.WheelVelocity(i);
            }
        }

        private void Emit(Vector3 position, Vector3 velocity)
        {
            //Most pieces in the colour of the earth, some darker (from under the surface), each a little lighter or darker
            Vector3 colour = Vector3.Lerp(Earth, DarkEarth, (float)random.NextDouble() < 0.35f ? Lerp(0.6f, 1f) : Lerp(0f, 0.3f)) * Lerp(0.85f, 1.12f);
            clods.Add(new Clod
            {
                Position = position,
                Velocity = velocity,
                Colour = colour,
                Life = Lerp(MinLife, MaxLife),
                //Many crumbs, few big clods
                Size = MathHelper.Lerp(MinSize, MaxSize, (float)Math.Pow(random.NextDouble(), 2.5))
            });
            if (clods.Count > MaxClods)
                clods.RemoveRange(0, clods.Count - MaxClods);
        }

        /// <summary>Gives the renderer the pieces and the dust to draw; called between its Begin and End.</summary>
        public void Draw(SceneRenderer renderer)
        {
            foreach (Clod clod in clods)
            {
                //Fading out over the last quarter of its flight
                float fade = MathHelper.Clamp((clod.Life - clod.Age) / (clod.Life * 0.25f), 0f, 1f);
                renderer.AddDebris(clod.Position, clod.Size, clod.Colour, fade);
            }
            dust.Draw(renderer);
        }

        private float Jitter() => (float)random.NextDouble() * 2f - 1f;

        private float Lerp(float from, float to) => from + (to - from) * (float)random.NextDouble();
    }
}
