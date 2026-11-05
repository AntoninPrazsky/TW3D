using Microsoft.Xna.Framework;

namespace TW3D.Graphics
{
    /// <summary>
    /// The parts of a model that can be painted, marked by the names of their materials (tools/ContentPipeline): the two
    /// paints of a car, "primary…" and "secondary…", and the rims of a wheel, "rim…".
    /// </summary>
    public enum PaintPart
    {
        None,
        Primary,
        Secondary,
        Rim
    }

    /// <summary>
    /// The colours the painted parts of a model are drawn in, in place of the colours of their materials (see
    /// <see cref="SceneRenderer.Add"/>); a part without a colour keeps that of its material, and so does every part of the
    /// default paint. The colours are as the materials have them, picked on the screen (sRGB); the materials keep their
    /// metal and roughness.
    /// </summary>
    public readonly struct Paint
    {
        public Paint(Vector3? primary, Vector3? secondary, Vector3? rim)
        {
            Primary = primary;
            Secondary = secondary;
            Rim = rim;
        }

        public Vector3? Primary { get; }

        public Vector3? Secondary { get; }

        public Vector3? Rim { get; }

        /// <summary>The colour of a part; null where the material keeps its own.</summary>
        public Vector3? Of(PaintPart part)
        {
            switch (part)
            {
                case PaintPart.Primary: return Primary;
                case PaintPart.Secondary: return Secondary;
                case PaintPart.Rim: return Rim;
                default: return null;
            }
        }

        /// <summary>The colour of a key of the palette of the garage; null for the original colour.</summary>
        public static Vector3? ColourOf(string key)
        {
            PaintColour colour = PaintColours.Find(key);
            return colour.IsOriginal ? null : new Vector3(colour.R, colour.G, colour.B);
        }

        /// <summary>The paint of the body of a car in its colours: the primary and secondary paint.</summary>
        public static Paint Body(CarColours colours) => new Paint(ColourOf(colours?.Primary), ColourOf(colours?.Secondary), null);

        /// <summary>The paint of the wheels of an axle of a car in its colours: their rims.</summary>
        public static Paint Wheels(CarColours colours, bool front) => new Paint(null, null, ColourOf(front ? colours?.FrontRims : colours?.RearRims));
    }
}
