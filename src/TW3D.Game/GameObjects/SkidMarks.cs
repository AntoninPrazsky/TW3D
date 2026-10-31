using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using nkast.Aether.Physics2D.Dynamics;
using TW3D.Graphics;
using TW3D.Shared;

namespace TW3D.GameObjects
{
    /// <summary>
    /// The marks the tyres leave where they slip (sliding braked or spinning): dark strips under each slipping wheel, on
    /// both sides of the car (each wheel of the physics is a pair of wheels, one on each side), fading away after a while.
    /// They lie on what the wheel stands on, the track or an object, and move with it.
    /// </summary>
    sealed class SkidMarks
    {
        //How long a mark stays (seconds), fading out over the last part of it; how dark it is at first, slipping at least
        //this fast (physics units a second; slower, it is lighter, down to a share of it)
        private const float Life = 20f;
        private const float FadeTime = 5f;
        private const float Darkness = 0.55f;
        private const float FullSlip = 25f;
        private const float LightestShare = 0.4f;
        //How wide a mark is (scene units: a little narrower than the tyres) and how far above the ground it lies, so that
        //it does not flicker with it
        private const float Width = 0.16f;
        private const float Lift = 0.006f;
        //A mark grows by a piece when its wheel has moved this far over the ground (physics units); a wheel further away
        //than this (it bounced) starts a new mark, as does a wheel that stopped slipping for longer than this (seconds;
        //shorter, it only lost the ground for a moment)
        private const float Step = 0.5f;
        private const float Gap = 5f;
        private const float Pause = 0.15f;
        //The oldest pieces go when there are more than this many
        private const int MaxPieces = 800;

        //A piece of a mark: its ends on its ground (in the ground's own coordinates, physics units)
        private struct Piece
        {
            public Body Ground;
            public Vector2 From, To;
            public float Side;
            public float Darkness;
            public float Age;
        }

        //Where the mark of a wheel ends, on its ground
        private struct End
        {
            public Body Ground;
            public Vector2 Point;
        }

        private readonly List<Piece> pieces = new List<Piece>();
        //Where the mark of each wheel of the physics ends, null while it does not slip; how long it has not slipped
        private readonly End?[] ends = new End?[Car.WheelCount];
        private readonly float[] stopped = new float[Car.WheelCount];
        private readonly bool[] slipped = new bool[Car.WheelCount];
        private VertexPositionColor[] vertices = new VertexPositionColor[0];

        /// <summary>Ages the marks and lengthens those of the wheels slipping now.</summary>
        /// <param name="elapsed">Real seconds since the last update.</param>
        /// <param name="slipping">The wheels slipping (see <see cref="Car.SlippingWheels"/>).</param>
        public void Update(float elapsed, Car car, List<WheelSlip> slipping)
        {
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                Piece piece = pieces[i];
                piece.Age += elapsed;
                if (piece.Age >= Life || piece.Ground.World == null)
                    pieces.RemoveAt(i);
                else
                    pieces[i] = piece;
            }

            System.Array.Clear(slipped);
            foreach (WheelSlip slip in slipping)
            {
                slipped[slip.Wheel] = true;
                Vector2 at = slip.Ground.GetLocalPoint(slip.Point);
                if (ends[slip.Wheel] is End end && end.Ground == slip.Ground)
                {
                    float distance = Vector2.Distance(at, end.Point);
                    if (distance < Step)
                        continue;
                    if (distance <= Gap)
                    {
                        float darkness = Darkness * MathHelper.Clamp(slip.Speed / FullSlip, LightestShare, 1f);
                        pieces.Add(new Piece { Ground = slip.Ground, From = end.Point, To = at, Side = car.WheelSide(slip.Wheel), Darkness = darkness });
                    }
                }
                ends[slip.Wheel] = new End { Ground = slip.Ground, Point = at };
            }
            for (int wheel = 0; wheel < ends.Length; wheel++)
            {
                stopped[wheel] = slipped[wheel] ? 0f : stopped[wheel] + elapsed;
                if (stopped[wheel] > Pause)
                    ends[wheel] = null;
            }

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
                Color colour = Color.Black * (piece.Darkness * fade);
                Vector3 from = Scene(piece.Ground.GetWorldPoint(piece.From)), to = Scene(piece.Ground.GetWorldPoint(piece.To));
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

        /// <summary>A point of the physics in the scene, lifted above the ground.</summary>
        private static Vector3 Scene(Vector2 point) => new Vector3(-point * Units.PhysicsToScene, 0f) + new Vector3(0f, Lift, 0f);
    }
}
