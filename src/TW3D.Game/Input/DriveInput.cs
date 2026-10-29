using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace TW3D.Input
{
    /// <summary>
    /// The player's commands for the car, combined from the keyboard, the on-screen controls and a gamepad.
    /// All sources work at the same time.
    /// </summary>
    public readonly struct DriveInput
    {
        public DriveInput(Vector2 stick, bool brake, bool lockAxles)
        {
            Stick = stick;
            Brake = brake;
            LockAxles = lockAxles;
        }

        /// <summary>X = throttle (right) / reverse (left), Y = axle rotation (up = clockwise). Both -1..1.</summary>
        public Vector2 Stick { get; }

        public bool Brake { get; }

        public bool LockAxles { get; }

        public static DriveInput Read(InputState input, OnScreenControls controls)
        {
            Vector2 keys = Vector2.Zero;
            if (input.IsKeyDown(Keys.Left)) keys.X -= 1f;
            if (input.IsKeyDown(Keys.Right)) keys.X += 1f;
            if (input.IsKeyDown(Keys.Down)) keys.Y -= 1f;
            if (input.IsKeyDown(Keys.Up)) keys.Y += 1f;
            if (keys != Vector2.Zero)
                keys.Normalize();

            //The keyboard has priority, then the on-screen joystick, then the gamepad
            Vector2 stick = keys;
            if (stick == Vector2.Zero)
                stick = controls.Stick;
            if (stick == Vector2.Zero)
                stick = input.GamePad.ThumbSticks.Left;

            bool brake = input.IsKeyDown(Keys.Space) || controls.BrakePressed || input.GamePad.IsButtonDown(Buttons.A);
            bool lockAxles = input.IsKeyDown(Keys.LeftControl) || controls.LockPressed || input.GamePad.IsButtonDown(Buttons.B);

            return new DriveInput(stick, brake, lockAxles);
        }
    }
}
