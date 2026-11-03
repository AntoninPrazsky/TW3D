using Microsoft.Xna.Framework;

namespace TW3D.Graphics
{
    /// <summary>
    /// What the sky behind a scene looks like (the technique Sky of Scene.fx, drawn by <see cref="SceneRenderer.SetSky"/>):
    /// computed from the direction of every pixel, with no picture. The colours are on the screen (gamma).
    /// </summary>
    public sealed class SkyLook
    {
        /// <summary>The colour overhead.</summary>
        public Vector3 Zenith { get; init; }

        /// <summary>The colour at the horizon: the colour the distance of the scene fades into (<see cref="Lighting.HazeColor"/>).</summary>
        public Vector3 Horizon { get; init; }

        /// <summary>The ground far below the horizon.</summary>
        public Vector3 Ground { get; init; }

        /// <summary>The distant hills along the horizon (hazed towards the horizon's colour by the shader).</summary>
        public Vector3 Hills { get; init; }

        /// <summary>Unit vector towards the sun (or the moon) of the sky.</summary>
        public Vector3 SunDirection { get; init; }

        /// <summary>The colour of the disc of the sun or the moon; black for none.</summary>
        public Vector3 SunDisc { get; init; }

        /// <summary>The radius of the disc, in radians (larger than the real one, which would be a few pixels).</summary>
        public float SunRadius { get; init; } = 0.03f;

        /// <summary>The glow of the sky around the sun.</summary>
        public Vector3 SunGlow { get; init; }

        /// <summary>How bright the stars are; 0 for none.</summary>
        public float Stars { get; init; }

        /// <summary>How much of the sky the clouds cover, 0 to 1.</summary>
        public float Clouds { get; init; }

        /// <summary>The side of the clouds the sun lights, and their shaded side.</summary>
        public Vector3 CloudLight { get; init; }
        public Vector3 CloudShade { get; init; }
    }
}
