using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace LevelEditor.LevelObjects
{
    /// <summary>
    /// The free camera of the editor: moved by W, S, A, D (forwards, backwards, sideways) and Q, E (up, down),
    /// turned by the numeric keypad (4, 6 around, 8, 2 up and down).
    /// </summary>
    class Camera3D
    {
        private const float RotationSpeed = 1f;
        private const float MoveSpeed = 3f;

        private float leftRightRotation = -MathHelper.Pi;
        private float upDownRotation = -MathHelper.Pi / 10f;

        public Vector3 Position { get; set; } = new Vector3(0, 8, -20);

        public Matrix View { get; private set; }

        public bool IsOrthographic { get; set; }

        private Matrix Rotation => Matrix.CreateRotationX(upDownRotation) * Matrix.CreateRotationY(leftRightRotation);

        private void UpdateView()
        {
            Vector3 target = Position + Vector3.Transform(Vector3.Forward, Rotation);
            Vector3 up = Vector3.Transform(Vector3.Up, Rotation);
            View = Matrix.CreateLookAt(Position, target, up);
        }

        /// <param name="elapsed">Elapsed seconds.</param>
        public void ProcessInput(float elapsed)
        {
            KeyboardState keys = Keyboard.GetState();

            if (keys.IsKeyDown(Keys.NumPad6))
                leftRightRotation -= RotationSpeed * elapsed;
            if (keys.IsKeyDown(Keys.NumPad4))
                leftRightRotation += RotationSpeed * elapsed;
            if (keys.IsKeyDown(Keys.NumPad2))
                upDownRotation -= RotationSpeed * elapsed;
            if (keys.IsKeyDown(Keys.NumPad8))
                upDownRotation += RotationSpeed * elapsed;

            Vector3 move = Vector3.Zero;
            if (keys.IsKeyDown(Keys.W))
                move += Vector3.Forward;
            if (keys.IsKeyDown(Keys.S))
                move += Vector3.Backward;
            if (keys.IsKeyDown(Keys.D))
                move += Vector3.Right;
            if (keys.IsKeyDown(Keys.A))
                move += Vector3.Left;
            if (keys.IsKeyDown(Keys.Q))
                move += Vector3.Up;
            if (keys.IsKeyDown(Keys.E))
                move += Vector3.Down;

            //The camera moves in the directions it looks
            Position += Vector3.Transform(move * elapsed, Rotation) * MoveSpeed;
            UpdateView();
        }

        /// <summary>Ray going from the camera through the mouse cursor.</summary>
        public Ray PickRay(Matrix projection, Viewport viewport)
        {
            MouseState mouse = Mouse.GetState();
            Vector3 near = viewport.Unproject(new Vector3(mouse.X, mouse.Y, 0f), projection, View, Matrix.Identity);
            Vector3 far = viewport.Unproject(new Vector3(mouse.X, mouse.Y, 1f), projection, View, Matrix.Identity);
            return new Ray(near, Vector3.Normalize(far - near));
        }
    }
}
