using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using nkast.Aether.Physics2D.Dynamics;
using TW3D.GamePlay;
using TW3D.Graphics;
using TW3D.Shared;

namespace TW3D.GameObjects
{
    /// <summary>
    /// The marks the tyres leave where they slip (sliding braked or spinning): dark strips under each slipping wheel, on
    /// both sides of the car (each wheel of the physics is a pair of wheels, one on each side), fading away after a while.
    /// They lie on what the wheel stands on, the track or an object, and move with it. On an object they lie on the
    /// surface of its model, found across the tyre where the mark grows: they bend over its rims and end where it ends
    /// (the physics knows only its outline, a barrel is shorter than the car is wide).
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
        //How much fainter a mark on the earth is
        private const float EarthFaint = 0.45f;
        //How wide a mark is (scene units: a little narrower than the tyres) and how far above what it lies on, so that it
        //does not flicker with it
        private const float Width = 0.16f;
        private const float Lift = 0.006f;
        //On the surface of an object the mark is drawn through this many points across it on each side of the car; the
        //surface is looked for this far above and below where the wheel touches the object (scene units: the model
        //lies a little off the outline of the physics, its rims stand out of it)
        private const int SurfacePoints = 9;
        private const float Above = 0.0625f;
        private const float Below = 0.0625f;
        //A mark grows by a piece when its wheel has moved this far over the ground (physics units); a wheel further away
        //than this (it bounced) starts a new mark, as does a wheel that stopped slipping for longer than this (seconds;
        //shorter, it only lost the ground for a moment)
        private const float Step = 0.5f;
        private const float Gap = 5f;
        private const float Pause = 0.15f;
        //The oldest pieces go when there are more than this many
        private const int MaxPieces = 800;

        //A piece of a mark: the cross sections of the mark at its ends, on its ground
        private struct Piece
        {
            public Body Ground;
            public Vector3?[] From, To;
            public float Darkness;
            public float Age;
        }

        //Where the mark of a wheel ends, on its ground (in the ground's own coordinates, physics units), and the cross
        //section of the mark there
        private struct End
        {
            public Body Ground;
            public Vector2 Point;
            public Vector3?[] Section;
        }

        private readonly PhysicsClock physics;
        private readonly Func<Body, ModelSurface> surfaceOf;
        private readonly List<Piece> pieces = new List<Piece>();
        //Where the mark of each wheel of the physics ends, null while it does not slip; how long it has not slipped
        private readonly End?[] ends = new End?[Car.WheelCount];
        private readonly float[] stopped = new float[Car.WheelCount];
        private readonly bool[] slipped = new bool[Car.WheelCount];
        private readonly ModelSurface.Hit?[] hits = new ModelSurface.Hit?[SurfacePoints];
        private VertexPositionColor[] vertices = new VertexPositionColor[0];

        /// <param name="physics">Where the bodies are drawn, as the marks move with them.</param>
        /// <param name="surfaceOf">The surface of the model of a body the car may stand on, in the coordinates of the model; null where it lies flat on the outline of the body (the track).</param>
        public SkidMarks(PhysicsClock physics, Func<Body, ModelSurface> surfaceOf)
        {
            this.physics = physics;
            this.surfaceOf = surfaceOf;
        }

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

            Array.Clear(slipped);
            foreach (WheelSlip slip in slipping)
            {
                slipped[slip.Wheel] = true;
                Vector2 at = slip.Ground.GetLocalPoint(slip.Point);
                End? previous = null;
                if (ends[slip.Wheel] is End end && end.Ground == slip.Ground)
                {
                    float distance = Vector2.Distance(at, end.Point);
                    if (distance < Step)
                        continue;
                    if (distance <= Gap)
                        previous = end;
                }
                Vector3?[] section = Section(slip.Ground, at, slip.Ground.GetLocalVector(slip.Up), car.WheelSide(slip.Wheel));
                if (previous is End from)
                {
                    //On the earth of the off-road track a tyre digs a darker rut rather than leaving rubber: a fainter mark
                    float darkness = Darkness * MathHelper.Clamp(slip.Speed / FullSlip, LightestShare, 1f) * (1f - EarthFaint * DirtSpray.EarthOf(slip.Ground));
                    pieces.Add(new Piece { Ground = slip.Ground, From = from.Section, To = section, Darkness = darkness });
                }
                ends[slip.Wheel] = new End { Ground = slip.Ground, Point = at, Section = section };
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

        /// <summary>
        /// The cross section of a mark where a wheel touches its ground: points across the tyre, on one side of the car and
        /// then on the other, in the ground's own coordinates as the scene has them (see <see cref="Local"/>);
        /// null where there is nothing under the tyre. On the track the two edges of the tyre, lifted off it; on an object
        /// points evenly spaced from edge to edge, where they find the surface of its model, lifted off it.
        /// </summary>
        /// <param name="at">Where the wheel touches the ground, in its own coordinates (physics units).</param>
        /// <param name="up">The way from the ground to the wheel, in its own coordinates.</param>
        /// <param name="side">How far the tyres are from the middle of the car (scene units).</param>
        private Vector3?[] Section(Body ground, Vector2 at, Vector2 up, float side)
        {
            Vector3 point = Local(at), normal = Vector3.Normalize(Local(up));
            ModelSurface surface = surfaceOf(ground);
            int count = surface != null ? SurfacePoints : 2;
            Vector3?[] section = new Vector3?[2 * count];
            for (int i = 0; i < 2; i++)
            {
                float middle = i == 0 ? side : -side;
                Vector3 near = point + new Vector3(0f, 0f, middle - Width / 2f), far = point + new Vector3(0f, 0f, middle + Width / 2f);
                if (surface == null)
                {
                    section[2 * i] = near + normal * Lift;
                    section[2 * i + 1] = far + normal * Lift;
                    continue;
                }
                //The model is drawn at a quarter of its size, the ground's own coordinates of the scene are those of the model so scaled
                surface.Cast((near + normal * Above) / Units.ModelToScene, (far + normal * Above) / Units.ModelToScene, -normal,
                    (Above + Below) / Units.ModelToScene, hits);
                for (int j = 0; j < count; j++)
                    section[i * count + j] = hits[j] is ModelSurface.Hit hit ? hit.Point * Units.ModelToScene + hit.Normal * Lift : null;
            }
            return section;
        }

        /// <summary>
        /// A point (or a way) of a body's own coordinates of the physics as the scene has it, the body standing at the
        /// origin unturned: the physics world is turned around (see <see cref="Units.PhysicsToScene"/>). These are the
        /// coordinates of the model of the body at its scale.
        /// </summary>
        private static Vector3 Local(Vector2 point) => new Vector3(-point * Units.PhysicsToScene, 0f);

        /// <summary>Gives the renderer the marks to draw; called between its Begin and End.</summary>
        public void Draw(SceneRenderer renderer)
        {
            //Two triangles for each strip between two points of a cross section, on each side of the car
            int needed = 0;
            foreach (Piece piece in pieces)
                needed += (piece.From.Length - 2) * 6;
            if (vertices.Length < needed)
                vertices = new VertexPositionColor[needed * 2];

            int count = 0;
            foreach (Piece piece in pieces)
            {
                float fade = MathHelper.Clamp((Life - piece.Age) / FadeTime, 0f, 1f);
                Color colour = Color.Black * (piece.Darkness * fade);
                //Where its ground is drawn (as its model is: see GameObject.ModelWorld)
                Matrix ground = Matrix.CreateRotationZ(physics.Rotation(piece.Ground)) * Matrix.CreateTranslation(Local(physics.Position(piece.Ground)));
                int points = piece.From.Length / 2;
                for (int i = 0; i < piece.From.Length - 1; i++)
                {
                    //Not between the two sides of the car, nor where either end of the strip lies over nothing
                    if (i % points == points - 1 || !(piece.From[i] is Vector3 fromNear && piece.From[i + 1] is Vector3 fromFar
                        && piece.To[i] is Vector3 toNear && piece.To[i + 1] is Vector3 toFar))
                        continue;
                    Vector3 a = Vector3.Transform(fromNear, ground), b = Vector3.Transform(toNear, ground);
                    Vector3 c = Vector3.Transform(toFar, ground), d = Vector3.Transform(fromFar, ground);
                    vertices[count++] = new VertexPositionColor(a, colour);
                    vertices[count++] = new VertexPositionColor(b, colour);
                    vertices[count++] = new VertexPositionColor(c, colour);
                    vertices[count++] = new VertexPositionColor(a, colour);
                    vertices[count++] = new VertexPositionColor(c, colour);
                    vertices[count++] = new VertexPositionColor(d, colour);
                }
            }
            renderer.SetMarks(vertices, count / 3);
        }
    }
}
