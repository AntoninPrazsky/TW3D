using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TW3D.Graphics;
using TW3D.Shared;

namespace TW3D.GameObjects
{
    /// <summary>
    /// The marks the tyres leave on the track where the braked wheels slide: dark strips under each sliding wheel, on both
    /// sides of the car (each wheel of the physics is a pair of wheels, one on each side), fading away after a while.
    /// </summary>
    sealed class SkidMarks
    {
        //How long a mark stays (seconds), fading out over the last part of it; how dark it is at first
        private const float Life = 20f;
        private const float FadeTime = 5f;
        private const float Darkness = 0.55f;
        //How wide a mark is (scene units: a little narrower than the tyres) and how far above the track it lies, so that
        //it does not flicker with it
        private const float Width = 0.16f;
        private const float Lift = 0.006f;
        //A mark grows by a piece when its wheel has moved this far (scene units); a wheel further away than this (it
        //bounced) starts a new mark
        private const float Step = 0.05f;
        private const float Gap = 0.5f;
        //The oldest pieces go when there are more than this many
        private const int MaxPieces = 800;

        private struct Piece
        {
            public Vector3 From, To;
            public float Side;
            public float Age;
        }

        private readonly List<Piece> pieces = new List<Piece>();
        //Where the mark of each wheel of the physics ends; null while it does not slide
        private readonly Vector3?[] ends = new Vector3?[Car.WheelCount];
        private readonly bool[] slid = new bool[Car.WheelCount];
        private VertexPositionColor[] vertices = new VertexPositionColor[0];

        /// <summary>Ages the marks and lengthens those of the wheels sliding now.</summary>
        /// <param name="elapsed">Real seconds since the last update.</param>
        /// <param name="sliding">The wheels sliding and where they touch the track (see <see cref="Car.SlidingWheels"/>).</param>
        public void Update(float elapsed, Car car, List<(int Wheel, Vector2 Point)> sliding)
        {
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                Piece piece = pieces[i];
                piece.Age += elapsed;
                if (piece.Age >= Life)
                    pieces.RemoveAt(i);
                else
                    pieces[i] = piece;
            }

            System.Array.Clear(slid);
            foreach ((int wheel, Vector2 point) in sliding)
            {
                slid[wheel] = true;
                Vector3 at = new Vector3(-point * Units.PhysicsToScene, 0f);
                if (ends[wheel] is Vector3 end)
                {
                    float distance = Vector3.Distance(at, end);
                    if (distance < Step)
                        continue;
                    if (distance <= Gap)
                        pieces.Add(new Piece { From = end, To = at, Side = car.WheelSide(wheel) });
                }
                ends[wheel] = at;
            }
            for (int wheel = 0; wheel < ends.Length; wheel++)
                if (!slid[wheel])
                    ends[wheel] = null;

            if (pieces.Count > MaxPieces)
                pieces.RemoveRange(0, pieces.Count - MaxPieces);
        }

        /// <summary>Gives the renderer the marks to draw; called between its Begin and End.</summary>
        public void Draw(SceneRenderer renderer)
        {
            int needed = pieces.Count * 12;
            if (vertices.Length < needed)
                vertices = new VertexPositionColor[needed * 2];

            int count = 0;
            foreach (Piece piece in pieces)
            {
                float fade = MathHelper.Clamp((Life - piece.Age) / FadeTime, 0f, 1f);
                Color colour = Color.Black * (Darkness * fade);
                Vector3 from = piece.From + new Vector3(0f, Lift, 0f), to = piece.To + new Vector3(0f, Lift, 0f);
                foreach (float side in new[] { piece.Side, -piece.Side })
                {
                    Vector3 near = new Vector3(0f, 0f, side - Width / 2f), far = new Vector3(0f, 0f, side + Width / 2f);
                    vertices[count++] = new VertexPositionColor(from + near, colour);
                    vertices[count++] = new VertexPositionColor(to + near, colour);
                    vertices[count++] = new VertexPositionColor(to + far, colour);
                    vertices[count++] = new VertexPositionColor(from + near, colour);
                    vertices[count++] = new VertexPositionColor(to + far, colour);
                    vertices[count++] = new VertexPositionColor(from + far, colour);
                }
            }
            renderer.SetMarks(vertices, count / 3);
        }
    }
}
