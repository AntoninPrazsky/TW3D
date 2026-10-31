using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using nkast.Aether.Physics2D.Diagnostics;
using TW3D.Graphics;
using TW3D.Input;
using TW3D.Shared;

namespace TW3D.GamePlay
{
    /// <summary>
    /// The keys for development while driving (see docs/playing.md): the physics debug view (F1–F8, or the gamepad Start
    /// button), the lighting of the 2016 version, shadows and ambient occlusion (F9–F11) and the mood of the level (M)
    /// to compare them, and the camera moved away from the car (the numeric keypad, Page Up and Page Down, Home).
    /// They change only the level being played; the Options menu sets the look for good.
    /// </summary>
    sealed class DevelopmentKeys
    {
        //Debug view flags toggled by the F1–F8 keys
        private static readonly (Keys Key, DebugViewFlags Flags)[] DebugKeys =
        {
            (Keys.F1, DebugViewFlags.Shape),
            (Keys.F2, DebugViewFlags.DebugPanel | DebugViewFlags.PerformanceGraph),
            (Keys.F3, DebugViewFlags.Joint),
            (Keys.F4, DebugViewFlags.ContactPoints | DebugViewFlags.ContactNormals),
            (Keys.F5, DebugViewFlags.PolygonPoints),
            (Keys.F6, DebugViewFlags.Controllers),
            (Keys.F7, DebugViewFlags.CenterOfMass),
            (Keys.F8, DebugViewFlags.AABB)
        };

        private const DebugViewFlags GamePadDebugFlags = DebugViewFlags.Shape | DebugViewFlags.DebugPanel | DebugViewFlags.PerformanceGraph
            | DebugViewFlags.Joint | DebugViewFlags.ContactPoints | DebugViewFlags.ContactNormals | DebugViewFlags.Controllers;

        //How fast the camera moves (physics units per second) and how fast the debug view zooms (per second)
        private const float CameraPanSpeed = 10f;
        private const float ZoomSpeed = 0.25f;

        private readonly DebugView debugView;
        private readonly SceneRenderer renderer;
        private readonly ChaseCamera camera;
        private LevelMood mood;

        /// <param name="mood">The mood of the level, which M changes from.</param>
        public DevelopmentKeys(DebugView debugView, SceneRenderer renderer, ChaseCamera camera, LevelMood mood)
        {
            this.debugView = debugView;
            this.renderer = renderer;
            this.camera = camera;
            this.mood = mood;
        }

        /// <param name="elapsed">Elapsed real seconds.</param>
        public void Handle(InputState input, float elapsed)
        {
            foreach ((Keys key, DebugViewFlags flags) in DebugKeys)
                if (input.KeyPressed(key))
                    debugView.Flags ^= flags;
            if (input.ButtonPressed(Buttons.Start))
                debugView.Flags ^= GamePadDebugFlags;

            if (input.KeyPressed(Keys.F9))
                renderer.Classic = !renderer.Classic;
            if (input.KeyPressed(Keys.F10))
                renderer.Shadows = !renderer.Shadows;
            if (input.KeyPressed(Keys.F11))
                renderer.AmbientOcclusion = !renderer.AmbientOcclusion;

            //The moods of the levels, to try them on any level
            if (input.KeyPressed(Keys.M))
            {
                mood = (LevelMood)(((int)mood + 1) % 3);
                renderer.Lighting = Lighting.For(mood);
            }

            Vector2 pan = Vector2.Zero;
            if (input.IsKeyDown(Keys.NumPad8)) pan.Y -= 1f;
            if (input.IsKeyDown(Keys.NumPad2)) pan.Y += 1f;
            if (input.IsKeyDown(Keys.NumPad4)) pan.X -= 1f;
            if (input.IsKeyDown(Keys.NumPad6)) pan.X += 1f;
            if (pan != Vector2.Zero)
                camera.Pan(pan * CameraPanSpeed * elapsed);

            if (input.IsKeyDown(Keys.PageUp))
                camera.Zoom *= 1f + ZoomSpeed * elapsed;
            if (input.IsKeyDown(Keys.PageDown))
                camera.Zoom /= 1f + ZoomSpeed * elapsed;
            if (input.KeyPressed(Keys.Home))
                camera.ResetView();
        }
    }
}
