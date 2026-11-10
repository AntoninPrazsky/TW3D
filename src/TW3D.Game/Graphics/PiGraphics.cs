using System;
using System.Runtime.InteropServices;

namespace TW3D.Graphics
{
    /// <summary>
    /// The graphics of the game on a Raspberry Pi 5, and on any computer with Linux on ARM64, which is most likely one:
    /// its GPU (VideoCore VII) has far less for the lighting of Scene.fx than the phones the game was made for. With the
    /// graphics of a desktop the Pi drew a level at 1920×1080 in 49 ms a frame (20 frames per second) and the garage of the
    /// menus in 72 ms, the time spent in the pixel shaders. The owner chose (2026-11-04) to draw the 3D scene at 1280×720,
    /// scaled up to the screen, with the user interface at the full resolution over it, without multisampling, without
    /// ambient occlusion (Options can switch it on) and with a shadow map of 1024 pixels: a level then takes about 14.5 ms
    /// at 1280×720 and the garage about 22 ms (measured off screen, which is a little faster than the screen).
    /// </summary>
    public static class PiGraphics
    {
        /// <summary>The game runs on Linux on ARM64 (not on Android, which has graphics of its own).</summary>
        public static bool Active { get; } =
#if ANDROID
            false;
#else
            OperatingSystem.IsLinux() && RuntimeInformation.OSArchitecture == Architecture.Arm64;
#endif

        /// <summary>
        /// The height the 3D scene is drawn at, at most, until a Resolution is chosen in the Options menu (the largest
        /// height of RenderResolution not above it; the width keeps the shape of the screen).
        /// </summary>
        public const int SceneHeight = 720;

        //The shadow map is 1024 texels until another Shadow detail is chosen (ShadowDetail.Low; medium at most, see
        //ShadowDetails.Highest): at 2048 it costs the Pi about 7 ms more at 1280×720 (2026-11-10). The surfaces are low
        //until chosen too (medium and high cost about 1.5 and 2 ms where they fill the screen), and the edges of the
        //shadows have the coarse filter of a phone (SceneRenderer.SmoothShadows)
    }
}
