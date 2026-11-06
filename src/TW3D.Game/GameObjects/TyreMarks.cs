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
    /// The marks the tyres leave on what they roll over, strips under the wheels on both sides of the car (each wheel of the
    /// physics is a pair of wheels, one on each side), fading away after a while: the dark skid marks where the tyres slip
    /// (sliding braked or spinning, <see cref="Skid"/>), and the tracks of the mud the tyres carry off the earth of the
    /// off-road track onto the clean road (<see cref="Mud"/>), fainter as the treads wipe clean (see <see cref="CarMud"/>).
    /// They lie on what the wheel stands on, the track or an object, and move with it. On an object they lie on the
    /// surface of its model, found across the tyre where the mark grows: they bend over its rims and end where it ends
    /// (the physics knows only its outline, a barrel is shorter than the car is wide). A mark follows the ground round a
    /// bend (a piece sooner where the ground turns) and breaks where the wheel goes up or down a step or round a sharp
    /// corner, so that no piece spans the air between two faces.
    /// </summary>
    sealed class TyreMarks
    {
        //How long a mark stays (seconds), fading out over the last part of it
        private const float Life = 20f;
        private const float FadeTime = 5f;
        //The skid marks: how dark they are at first, slipping at least this fast (physics units a second; slower, lighter,
        //down to a share of it), and how much fainter on the earth (a rut dug rather than rubber left)
        private const float SkidDarkness = 0.55f;
        private const float FullSlip = 25f;
        private const float LightestShare = 0.4f;
        private const float EarthFaint = 0.45f;
        //The tracks of the mud: how much of the road they cover with all the mud on the treads (fainter as the treads wipe
        //clean, as much less as there is less mud on them), and less than this they leave none; how much less some pieces of them
        //cover, at random, so that they are smears rather than stripes; their colour (sRGB: the earth of the off-road
        //blocks drying on the road, lighter and browner than the grey of the asphalt, or it would hardly show there)
        private const float MudOpacity = 0.85f;
        private const float LeastMud = 0.03f;
        private const float MudPatchiness = 0.45f;
        private static readonly Vector3 MudColour = new Vector3(0.52f, 0.39f, 0.25f);
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
        //A wheel further away than this from the end of its mark (physics units: it bounced) starts a new mark, as does a
        //wheel that stopped laying it for longer than this (seconds; shorter, it only lost the ground for a moment)
        private const float Gap = 5f;
        private const float Pause = 0.15f;
        //Where the ground turns more than this since the end of the mark (the cosine of the angle between their normals),
        //the mark grows by a piece however short, so that it follows a bend; turned more than this, or the wheel moved
        //along the normal of the ground more than this part of the way (a step up or down), it starts anew instead of
        //spanning the air
        private const float Bend = 0.985f;
        private const float Corner = 0.82f;
        private const float Climb = 0.3f;

        //A piece of a mark: the cross sections of the mark at its ends, on its ground
        private struct Piece
        {
            public Body Ground;
            public Vector3?[] From, To;
            public float Opacity;
            public float Age;
        }

        //Where the mark of a wheel ends, on its ground (in the ground's own coordinates, physics units), the normal of the
        //ground there (the same coordinates) and the cross section of the mark there
        private struct End
        {
            public Body Ground;
            public Vector2 Point;
            public Vector2 Up;
            public Vector3?[] Section;
        }

        private readonly PhysicsClock physics;
        private readonly Func<Body, ModelSurface> surfaceOf;
        //The colour of the marks (sRGB), the length of a piece (physics units: a mark grows by a piece when its wheel has
        //moved this far over the ground) and the most pieces there are (the oldest go first)
        private readonly Vector3 colour;
        private readonly float step;
        private readonly int maxPieces;
        //How much less some pieces cover, at random (0 for every piece alike)
        private readonly float patchiness;
        private readonly Random random = new Random(60);
        private readonly List<Piece> pieces = new List<Piece>();
        //Where the mark of each wheel of the physics ends, null while it lays none; how long it has laid none
        private readonly End?[] ends = new End?[Car.WheelCount];
        private readonly float[] stopped = new float[Car.WheelCount];
        private readonly bool[] laid = new bool[Car.WheelCount];
        private readonly ModelSurface.Hit?[] hits = new ModelSurface.Hit?[SurfacePoints];
        private VertexPositionColor[] vertices = new VertexPositionColor[0];

        /// <param name="physics">Where the bodies are drawn, as the marks move with them.</param>
        /// <param name="surfaceOf">The surface of the model of a body the car may stand on, in the coordinates of the model; null where it lies flat on the outline of the body (the track).</param>
        private TyreMarks(PhysicsClock physics, Func<Body, ModelSurface> surfaceOf, Vector3 colour, float step, int maxPieces, float patchiness)
        {
            this.physics = physics;
            this.surfaceOf = surfaceOf;
            this.colour = colour;
            this.step = step;
            this.maxPieces = maxPieces;
            this.patchiness = patchiness;
        }

        /// <summary>The dark skid marks of the tyres slipping (fed by <see cref="SkidStrength"/>).</summary>
        public static TyreMarks Skid(PhysicsClock physics, Func<Body, ModelSurface> surfaceOf) => new TyreMarks(physics, surfaceOf, Vector3.Zero, 0.5f, 800, 0f);

        /// <summary>The tracks of the mud of the tyres on the clean road (fed by <see cref="MudStrength"/>): longer pieces, as they follow every rolling wheel, and patchy.</summary>
        public static TyreMarks Mud(PhysicsClock physics, Func<Body, ModelSurface> surfaceOf) => new TyreMarks(physics, surfaceOf, MudColour, 1.5f, 2000, MudPatchiness);

        /// <summary>How dark the skid mark of a slipping wheel is: darker the faster it slips, fainter on the earth.</summary>
        public static float SkidStrength(WheelSlip slip) =>
            SkidDarkness * MathHelper.Clamp(slip.Speed / FullSlip, LightestShare, 1f) * (1f - EarthFaint * DirtSpray.EarthOf(slip.Ground));

        /// <summary>How much a wheel on the ground marks it with mud: none on the earth, elsewhere by the mud on its treads.</summary>
        public static float MudStrength(WheelSlip wheel, CarMud mud)
        {
            if (DirtSpray.EarthOf(wheel.Ground) > 0f)
                return 0f;
            float tread = mud.Treads(Car.IsFrontWheel(wheel.Wheel));
            return tread < LeastMud ? 0f : MudOpacity * tread;
        }

        /// <summary>Ages the marks and lengthens those of the wheels laying one now.</summary>
        /// <param name="elapsed">Real seconds since the last update.</param>
        /// <param name="wheels">The wheels on the ground that may lay a mark (see <see cref="Car.SlippingWheels"/>).</param>
        /// <param name="strength">How strong the mark of a wheel is now, 0 to 1; 0 lays none.</param>
        public void Update(float elapsed, Car car, List<WheelSlip> wheels, Func<WheelSlip, float> strength)
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

            Array.Clear(laid);
            foreach (WheelSlip wheel in wheels)
            {
                float opacity = strength(wheel);
                if (opacity <= 0f)
                    continue;
                laid[wheel.Wheel] = true;
                Vector2 at = wheel.Ground.GetLocalPoint(wheel.Point), up = wheel.Ground.GetLocalVector(wheel.Up);
                End? previous = null;
                if (ends[wheel.Wheel] is End end && end.Ground == wheel.Ground)
                {
                    float distance = Vector2.Distance(at, end.Point);
                    float turned = Vector2.Dot(end.Up, up);
                    if (distance < step && turned > Bend)
                        continue;
                    //On over the ground, not up or down a step nor round a sharp corner through the air
                    float climb = distance > 0f ? Math.Abs(Vector2.Dot(at - end.Point, up)) / distance : 0f;
                    if (distance <= Gap && turned > Corner && climb < Climb)
                        previous = end;
                }
                Vector3?[] section = Section(wheel.Ground, at, up, car.WheelSide(wheel.Wheel));
                if (previous is End from)
                    pieces.Add(new Piece { Ground = wheel.Ground, From = from.Section, To = section,
                        Opacity = opacity * (1f - patchiness * (float)random.NextDouble()) });
                ends[wheel.Wheel] = new End { Ground = wheel.Ground, Point = at, Up = up, Section = section };
            }
            for (int wheel = 0; wheel < ends.Length; wheel++)
            {
                stopped[wheel] = laid[wheel] ? 0f : stopped[wheel] + elapsed;
                if (stopped[wheel] > Pause)
                    ends[wheel] = null;
            }

            if (pieces.Count > maxPieces)
                pieces.RemoveRange(0, pieces.Count - maxPieces);
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
                //Premultiplied by its alpha
                Color tint = new Color(colour) * (piece.Opacity * fade);
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
                    vertices[count++] = new VertexPositionColor(a, tint);
                    vertices[count++] = new VertexPositionColor(b, tint);
                    vertices[count++] = new VertexPositionColor(c, tint);
                    vertices[count++] = new VertexPositionColor(a, tint);
                    vertices[count++] = new VertexPositionColor(c, tint);
                    vertices[count++] = new VertexPositionColor(d, tint);
                }
            }
            renderer.AddMarks(vertices, count / 3);
        }
    }
}
