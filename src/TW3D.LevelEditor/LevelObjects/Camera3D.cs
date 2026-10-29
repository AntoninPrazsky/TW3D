using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace LevelEditor.LevelObjects
{
    class Camera3D
    {
        public Vector3 cameraPosition = new Vector3(0, 8, -20);
        
        public Vector3 Target { get; set; }
        public Matrix viewMatrix { get; set; }
        public bool IsOrtographic { get; set; }
        private float leftrightRot = -MathHelper.Pi;
        private float updownRot = -MathHelper.Pi / 10f;
        private const float rotationSpeed = 1f;
        private const float moveSpeed = 3f;
        
        private void UpdateViewMatrix()
        {
            Matrix cameraRotation = Matrix.CreateRotationX(updownRot) * Matrix.CreateRotationY(leftrightRot);

            Vector3 cameraOriginalTarget = new Vector3(0, 0, -1);
            Vector3 cameraRotatedTarget = Vector3.Transform(cameraOriginalTarget, cameraRotation);
            Vector3 cameraFinalTarget = cameraPosition + cameraRotatedTarget;

            Vector3 cameraOriginalUpVector = new Vector3(0, 1, 0);
            Vector3 cameraRotatedUpVector = Vector3.Transform(cameraOriginalUpVector, cameraRotation);

            viewMatrix = Matrix.CreateLookAt(cameraPosition, cameraFinalTarget, cameraRotatedUpVector);
        }

        public void ProcessInput(float amount)
        {
            KeyboardState keyState = Keyboard.GetState();

            if (keyState.IsKeyDown(Keys.NumPad6))
                leftrightRot -= rotationSpeed * amount;
            if (keyState.IsKeyDown(Keys.NumPad4))
                leftrightRot += rotationSpeed * amount;
            if (keyState.IsKeyDown(Keys.NumPad2))
                updownRot -= rotationSpeed * amount;
            if (keyState.IsKeyDown(Keys.NumPad8))
                updownRot += rotationSpeed * amount;
            UpdateViewMatrix();

            Vector3 moveVector = new Vector3(0, 0, 0);

            if (keyState.IsKeyDown(Keys.W))
                moveVector += new Vector3(0, 0, -1);
            if (keyState.IsKeyDown(Keys.S))
                moveVector += new Vector3(0, 0, 1);
            if (keyState.IsKeyDown(Keys.D))
                moveVector += new Vector3(1, 0, 0);
            if (keyState.IsKeyDown(Keys.A))
                moveVector += new Vector3(-1, 0, 0);
            if (keyState.IsKeyDown(Keys.Q))
                moveVector += new Vector3(0, 1, 0);
            if (keyState.IsKeyDown(Keys.E))
                moveVector += new Vector3(0, -1, 0);
            AddToCameraPosition(moveVector * amount);
        }

        private void AddToCameraPosition(Vector3 vectorToAdd)
        {
            Matrix cameraRotation = Matrix.CreateRotationX(updownRot) * Matrix.CreateRotationY(leftrightRot);
            Vector3 rotatedVector = Vector3.Transform(vectorToAdd, cameraRotation);
            cameraPosition += moveSpeed * rotatedVector;
            UpdateViewMatrix();
        }

        public Ray getPickRay(Matrix projection, GraphicsDeviceManager man)
        {
            MouseState mouseState = Mouse.GetState();
            int mouseX = mouseState.X;
            int mouseY = mouseState.Y;
            Vector3 nearsource = new Vector3((float)mouseX, (float)mouseY, 0f);
            Vector3 farsource = new Vector3((float)mouseX, (float)mouseY, 1f);

            Vector3 nearPoint = man.GraphicsDevice.Viewport.Unproject(nearsource, projection, viewMatrix, Matrix.Identity);
            Vector3 farPoint = man.GraphicsDevice.Viewport.Unproject(farsource, projection, viewMatrix, Matrix.Identity);
            Vector3 direction = farPoint - nearPoint;
            direction.Normalize();
            Ray pickRay = new Ray(nearPoint, direction);
            return pickRay;
        }

    }
}
