using System;
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
    /// button), the lighting of the 2016 version, shadows and ambient occlusion (F9, F10, Shift+F10) and the mood of the level (M)
    /// to compare them, the camera moved away from the car (the numeric keypad, Page Up and Page Down, Home), and the
    /// track alone (H: the car and everything over the level hidden, for the previews of the levels in the level
    /// select), the car turned onto its roof (U, to try out the Game over on the roof) and the road of the blocks drawn as
    /// a test surface (T: asphalt, earth, as it is; see SceneRenderer.TestSurface). They change only the level being
    /// played; the Options menu sets the look for good.
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
        private readonly Action turnOver;
        private LevelMood mood;

        /// <summary>The track alone: the car, the time, the tutorial and the on-screen controls are not drawn (H).</summary>
        public bool TrackOnly { get; private set; }

        /// <param name="mood">The mood of the level, which M changes from.</param>
        /// <param name="turnOver">Turns the car onto its roof (U).</param>
        public DevelopmentKeys(DebugView debugView, SceneRenderer renderer, ChaseCamera camera, LevelMood mood, Action turnOver)
        {
            this.debugView = debugView;
            this.renderer = renderer;
            this.camera = camera;
            this.mood = mood;
            this.turnOver = turnOver;
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
            //F10 the shadows, Shift+F10 the ambient occlusion (F11 is the game's: full screen or a window)
            if (input.KeyPressed(Keys.F10))
            {
                if (input.IsKeyDown(Keys.LeftShift) || input.IsKeyDown(Keys.RightShift))
                    renderer.AmbientOcclusion = !renderer.AmbientOcclusion;
                else
                    renderer.Shadows = !renderer.Shadows;
            }

            if (input.KeyPressed(Keys.H))
                TrackOnly = !TrackOnly;

            //The car onto its roof, to try out how a run on the roof is lost
            if (input.KeyPressed(Keys.U))
                turnOver();

            //The road of the blocks as each test surface in turn, then as it is
            if (input.KeyPressed(Keys.T))
                renderer.TestSurface = (Surface)(((int)renderer.TestSurface + 1) % Enum.GetValues<Surface>().Length);

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
