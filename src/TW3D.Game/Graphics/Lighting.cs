using Microsoft.Xna.Framework;
using TW3D.Shared;

namespace TW3D.Graphics
{
    /// <summary>
    /// The light of a scene: the sun (or the moon), the light of the sky and the ground, the haze,
    /// the colours of the sky and the headlights of the car. Colours of the light are linear;
    /// the colours of the sky and the haze are on the screen.
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

        /// <summary>The colour of the sky at the horizon, where the distance fades to.</summary>
        public Vector3 HazeColor { get; init; }
        public float HazeStart { get; init; }
        public float HazeEnd { get; init; }
        public float HazeMax { get; init; }

        /// <summary>
        /// The colours of the sky at the horizon and above: the brightness of the sky picture in these
        /// colours, mixed with the picture as it is by <see cref="SkyRecolor"/> (0 = the picture as it is).
        /// </summary>
        public Vector3 SkyHorizon { get; init; } = Vector3.One;
        public Vector3 SkyZenith { get; init; } = Vector3.One;
        public float SkyRecolor { get; init; }
        /// <summary>Glow of the sky around the sun.</summary>
        public Vector3 SunGlow { get; init; }

        /// <summary>The headlights of the car are on.</summary>
        public bool Headlights { get; init; }

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

        /// <summary>A low orange sun on the left, long shadows to the right, a warm sky.</summary>
        public static readonly Lighting Sunset = new Lighting
        {
            SunDirection = Vector3.Normalize(new Vector3(0.9f, 0.32f, -0.35f)),
            SunColor = new Vector3(1f, 0.55f, 0.28f) * 2.6f,
            SkyLight = new Vector3(0.42f, 0.4f, 0.62f) * 0.5f,
            GroundLight = new Vector3(0.45f, 0.28f, 0.16f) * 0.3f,
            RimLight = new Vector3(1f, 0.6f, 0.4f) * 0.45f,
            HazeColor = new Color(250, 178, 128).ToVector3(),
            HazeStart = 6f,
            HazeEnd = 50f,
            HazeMax = 0.6f,
            SkyHorizon = new Vector3(1.25f, 0.72f, 0.5f),
            SkyZenith = new Vector3(0.6f, 0.55f, 0.95f),
            SkyRecolor = 0.85f,
            SunGlow = new Vector3(1f, 0.55f, 0.25f) * 0.7f
        };

        /// <summary>The cold light of the moon from the right, a dark sky, the headlights of the car on.</summary>
        public static readonly Lighting Night = new Lighting
        {
            SunDirection = Vector3.Normalize(new Vector3(-0.45f, 1f, -0.45f)),
            SunColor = new Vector3(0.55f, 0.68f, 1f) * 0.28f,
            SkyLight = new Vector3(0.18f, 0.24f, 0.45f) * 0.22f,
            GroundLight = new Vector3(0.06f, 0.06f, 0.08f) * 0.3f,
            RimLight = new Vector3(0.3f, 0.42f, 0.8f) * 0.25f,
            HazeColor = new Color(22, 30, 58).ToVector3(),
            HazeStart = 5f,
            HazeEnd = 40f,
            HazeMax = 0.7f,
            SkyHorizon = new Vector3(0.16f, 0.2f, 0.36f),
            SkyZenith = new Vector3(0.05f, 0.07f, 0.16f),
            SkyRecolor = 1f,
            SunGlow = new Vector3(0.35f, 0.42f, 0.6f) * 0.3f,
            Headlights = true
        };

        /// <summary>
        /// The garage of the menu: white light from lamps high above, a little from the side of the windows, a light
        /// hall around; the headlights of the cars on.
        /// </summary>
        public static readonly Lighting Garage = new Lighting
        {
            SunDirection = Vector3.Normalize(new Vector3(0.2f, 1f, 0.35f)),
            SunColor = new Vector3(1f, 0.96f, 0.9f) * 1.9f,
            SkyLight = new Vector3(0.62f, 0.64f, 0.68f) * 0.6f,
            GroundLight = new Vector3(0.5f, 0.5f, 0.5f) * 0.3f,
            RimLight = new Vector3(0.6f, 0.65f, 0.75f) * 0.2f,
            //No haze; its colour is the horizon mirrored by metal: the light grey of the hall
            HazeColor = new Color(205, 208, 212).ToVector3(),
            HazeStart = 100f,
            HazeEnd = 200f,
            HazeMax = 0f,
            Headlights = true
        };

        public static Lighting For(LevelMood mood)
        {
            switch (mood)
            {
                case LevelMood.Sunset: return Sunset;
                case LevelMood.Night: return Night;
                default: return Day;
            }
        }
    }
}
