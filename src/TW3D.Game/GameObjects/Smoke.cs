using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TW3D.Graphics;

namespace TW3D.GameObjects
{
    /// <summary>
    /// A cloud of soft puffs of smoke with its own look: how long a puff lives, how it grows, rises and slows down, how
    /// thick it is and its colour. The tyres (<see cref="TyreSmoke"/>) and the exhaust (<see cref="ExhaustSmoke"/>) puff into
    /// their own clouds.
    /// </summary>
    sealed class Smoke
    {
        //The oldest puffs go when there are more than this many
        private const int MaxPuffs = 400;
        //A puff thickens up over this time (seconds) before it thins out
        private const float FadeIn = 0.1f;

        /// <summary>How long a puff lives (seconds, give or take a quarter).</summary>
        public float Life { get; init; } = 1.6f;
        /// <summary>How big a puff is at first and at the end (radius, scene units).</summary>
        public float StartSize { get; init; } = 0.1f;
        public float EndSize { get; init; } = 0.6f;
        /// <summary>How fast a puff rises (scene units a second).</summary>
        public float Rise { get; init; } = 0.3f;
        /// <summary>How fast what a puff was thrown with dies away (1 / seconds).</summary>
        public float Drag { get; init; } = 3f;
        /// <summary>How much a puff hides what is behind it in its middle at first, at its thickest.</summary>
        public float Opacity { get; init; } = 0.4f;
        /// <summary>Its colour, times the light of the scene greyed (see <see cref="SceneRenderer.AddSmoke"/>).</summary>
        public Vector3 Tint { get; init; } = Vector3.One;

        private struct Puff
        {
            public Vector3 Position, Velocity;
            public float Age, Life, Thickness;
        }

        private readonly List<Puff> puffs = new List<Puff>();
        private readonly Random random = new Random(7);

        /// <summary>A random number between -1 and 1.</summary>
        public float Jitter() => (float)random.NextDouble() * 2f - 1f;

        /// <summary>Adds a puff.</summary>
        /// <param name="velocity">What it is thrown with (scene units a second).</param>
        /// <param name="thickness">How thick it is, 0..1 of the cloud's opacity.</param>
        public void Emit(Vector3 position, Vector3 velocity, float thickness)
        {
            puffs.Add(new Puff
            {
                Position = position,
                Velocity = velocity,
                Life = Life * (0.75f + 0.5f * (float)random.NextDouble()),
                Thickness = thickness
            });
            if (puffs.Count > MaxPuffs)
                puffs.RemoveRange(0, puffs.Count - MaxPuffs);
        }

        /// <summary>Moves and ages the puffs.</summary>
        /// <param name="elapsed">Real seconds since the last update.</param>
        public void Update(float elapsed)
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
        }

        /// <summary>Gives the renderer the puffs to draw; called between its Begin and End.</summary>
        public void Draw(SceneRenderer renderer)
        {
            foreach (Puff puff in puffs)
            {
                float t = puff.Age / puff.Life;
                float size = MathHelper.Lerp(StartSize, EndSize, MathF.Sqrt(t));
                float opacity = Opacity * puff.Thickness * MathHelper.Clamp(puff.Age / FadeIn, 0f, 1f) * (1f - t) * (1f - t);
                renderer.AddSmoke(puff.Position, size, opacity, Tint);
            }
        }
    }
}
