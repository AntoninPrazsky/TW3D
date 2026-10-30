using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using nkast.Aether.Physics2D;
using nkast.Aether.Physics2D.Dynamics;

namespace TW3D.GamePlay
{
    /// <summary>
    /// Runs the physics simulation with a fixed time step, independent of the frame rate,
    /// and interpolates the positions of moving bodies between the last two steps,
    /// so the movement is smooth at any frame rate. The time is game time (see GameplayScreen.GameSpeed):
    /// 60 steps per game second, e.g. 90 steps per real second at the game speed 1.5.
    /// </summary>
    public sealed class PhysicsClock
    {
        /// <summary>Length of one simulation step in game seconds (the same as the 60 updates per second of the 2016 version).</summary>
        public const float StepTime = 1f / 60f;

        //After a long frame the simulation does not try to catch up more than this; it slows down instead
        private const int MaxStepsPerFrame = 8;

        private readonly struct BodyState
        {
            public BodyState(Vector2 position, float rotation)
            {
                Position = position;
                Rotation = rotation;
            }

            public Vector2 Position { get; }
            public float Rotation { get; }
        }

        private readonly World world;
        private readonly Dictionary<Body, BodyState> previousStates = new Dictionary<Body, BodyState>();
        private SolverIterations solverIterations;
        private float accumulatedTime;

        public PhysicsClock(World world, SolverIterations solverIterations)
        {
            this.world = world;
            this.solverIterations = solverIterations;
        }

        /// <summary>Position between the previous (0) and the last (1) simulation step to draw.</summary>
        public float Alpha { get; private set; } = 1f;

        /// <summary>Game seconds simulated so far (the steps run times their length).</summary>
        public double Time { get; private set; }

        /// <summary>Runs as many simulation steps as fit into the elapsed game time.</summary>
        public void Advance(float elapsedSeconds)
        {
            accumulatedTime += elapsedSeconds;

            int steps = 0;
            while (accumulatedTime >= StepTime && steps < MaxStepsPerFrame)
            {
                SaveStates();
                world.Step(StepTime, ref solverIterations);
                Time += StepTime;
                accumulatedTime -= StepTime;
                steps++;
            }

            if (accumulatedTime >= StepTime)
                accumulatedTime = 0f;

            Alpha = accumulatedTime / StepTime;
        }

        private void SaveStates()
        {
            foreach (Body body in world.BodyList)
                if (body.BodyType != BodyType.Static)
                    previousStates[body] = new BodyState(body.Position, body.Rotation);
        }

        /// <summary>Position of the body to draw, interpolated between the last two simulation steps.</summary>
        public Vector2 Position(Body body)
        {
            if (!previousStates.TryGetValue(body, out BodyState previous))
                return body.Position;
            return Vector2.Lerp(previous.Position, body.Position, Alpha);
        }

        /// <summary>Rotation of the body to draw, interpolated between the last two simulation steps.</summary>
        public float Rotation(Body body)
        {
            if (!previousStates.TryGetValue(body, out BodyState previous))
                return body.Rotation;

            //The rotation may have been reset (the car resets it after a full turn); don't interpolate over the jump
            if (Math.Abs(body.Rotation - previous.Rotation) > MathHelper.Pi)
                return body.Rotation;
            return MathHelper.Lerp(previous.Rotation, body.Rotation, Alpha);
        }
    }
}
