using Microsoft.Xna.Framework;

namespace TW3D.Graphics
{
    /// <summary>
    /// The light of a scene: the sun, the light of the sky and the ground, and the haze.
    /// Colours of the light are linear; the haze colour is on the screen (the colour of the sky at the horizon).
    /// </summary>
    public sealed class Lighting
    {
        /// <summary>Unit vector towards the sun.</summary>
        public Vector3 SunDirection { get; init; }
        public Vector3 SunColor { get; init; }
        /// <summary>Light coming from above.</summary>
        public Vector3 SkyLight { get; init; }
        /// <summary>Light coming from below, reflected by the ground.</summary>
        public Vector3 GroundLight { get; init; }
        /// <summary>Light at grazing angles.</summary>
        public Vector3 RimLight { get; init; }
        public float Exposure { get; init; } = 1f;

        public Vector3 HazeColor { get; init; }
        public float HazeStart { get; init; }
        public float HazeEnd { get; init; }
        public float HazeMax { get; init; }

        /// <summary>
        /// A sunny day. The sun shines from above and from the left, a little from the side of the camera:
        /// the faces turned to the camera are lit as well, and the shadows fall to the right onto the track.
        /// </summary>
        public static readonly Lighting Day = new Lighting
        {
            SunDirection = Vector3.Normalize(new Vector3(0.65f, 1f, -0.4f)),
            SunColor = new Vector3(1f, 0.93f, 0.82f) * 2.3f,
            SkyLight = new Vector3(0.5f, 0.68f, 0.9f) * 0.55f,
            GroundLight = new Vector3(0.32f, 0.28f, 0.24f) * 0.35f,
            RimLight = new Vector3(0.55f, 0.7f, 0.9f) * 0.35f,
            HazeColor = new Color(176, 235, 255).ToVector3(),
            HazeStart = 8f,
            HazeEnd = 60f,
            HazeMax = 0.55f
        };
    }
}
