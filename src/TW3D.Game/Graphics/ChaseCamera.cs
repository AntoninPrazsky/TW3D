using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using nkast.Aether.Physics2D.Dynamics;

namespace TW3D.Graphics
{
    /// <summary>
    /// Camera following a physics body with a delay. Provides the 3D view of the scene
    /// and the 2D view for the physics debug view.
    /// </summary>
    /// <remarks>
    /// Physics coordinates (x, y) map to 3D coordinates (-x / 10, -y / 10); the physics plane is z = 0.
    /// </remarks>
    public sealed class ChaseCamera
    {
        //The camera catches up with its target at a speed that grows with the square of the distance,
        //reaching the full speed at this distance (physics units)
        private const float FullSpeedDistance = 50f;
        private const float MaxSpeed = 100f;
        //The same for the rotation (radians)
        private const float FullTurnSpeedAngle = 5f;
        private const float MaxTurnSpeed = 80f;

        private const float WorldTo3D = 0.1f;
        //Far clipping plane distance. The 2016 version used 22 or 50 to save the performance of phones,
        //which cut off the scene too close; now it is far beyond the sky and any level.
        private const float FarPlane = 1000f;
        //Physics units visible vertically in the debug view at zoom 1
        private const float DebugViewHeight = 30f;

        private const float MinZoom = 0.02f;
        private const float MaxZoom = 20f;

        private Vector2 panOffset;
        private float zoom = 1f;

        /// <summary>The followed body.</summary>
        public Body Target { get; set; }

        /// <summary>Limits of the camera rotation following the target's rotation (radians).</summary>
        public float MinRotation { get; set; } = -MathHelper.Pi;
        public float MaxRotation { get; set; } = MathHelper.Pi;

        /// <summary>Current (delayed) camera position in physics units.</summary>
        public Vector2 Position { get; private set; }

        /// <summary>Current (delayed) camera rotation in radians.</summary>
        public float Rotation { get; private set; }

        /// <summary>Zoom of the physics debug view (the 3D view is not affected).</summary>
        public float Zoom
        {
            get { return zoom; }
            set { zoom = MathHelper.Clamp(value, MinZoom, MaxZoom); }
        }

        public Matrix View { get; private set; }
        public Matrix Projection { get; private set; }
        public BoundingFrustum Frustum { get; private set; } = new BoundingFrustum(Matrix.Identity);

        /// <summary>View and projection for drawing the physics debug view in physics units.</summary>
        public Matrix DebugView { get; private set; }
        public Matrix DebugProjection { get; private set; }

        /// <summary>Moves the camera away from the target (in physics units).</summary>
        public void Pan(Vector2 amount)
        {
            panOffset += amount;
        }

        /// <summary>Cancels panning and zooming.</summary>
        public void ResetView()
        {
            panOffset = Vector2.Zero;
            Zoom = 1f;
        }

        public void Update(float elapsedSeconds, Viewport viewport)
        {
            if (Target != null)
            {
                Vector2 targetPosition = Target.Position + panOffset;
                float targetRotation = MathHelper.Clamp(-Target.Rotation % MathHelper.TwoPi, MinRotation, MaxRotation);

                Position = Follow(Position, targetPosition, elapsedSeconds);
                Rotation = FollowAngle(Rotation, targetRotation, elapsedSeconds);
            }

            Update3D(viewport);
            UpdateDebugView(viewport);
        }

        private static Vector2 Follow(Vector2 current, Vector2 target, float elapsedSeconds)
        {
            Vector2 toTarget = target - current;
            float distance = toTarget.Length();
            if (distance <= 0f)
                return current;

            float speed = MaxSpeed * Math.Min(1f, (distance / FullSpeedDistance) * (distance / FullSpeedDistance));
            float step = Math.Min(speed * elapsedSeconds, distance);
            return current + toTarget / distance * step;
        }

        private static float FollowAngle(float current, float target, float elapsedSeconds)
        {
            float difference = target - current;
            float distance = Math.Abs(difference);
            if (distance <= 0f)
                return current;

            float speed = MaxTurnSpeed * Math.Min(1f, (distance / FullTurnSpeedAngle) * (distance / FullTurnSpeedAngle));
            float step = Math.Min(speed * elapsedSeconds, distance);
            return current + Math.Sign(difference) * step;
        }

        private void Update3D(Viewport viewport)
        {
            //The camera looks at the target (with no delay) from its own delayed position, slightly above and behind
            Vector2 focus = Target != null ? Target.Position : Position;
            Vector3 lookAt = new Vector3(-focus.X * WorldTo3D - 0.5f, -focus.Y * WorldTo3D, 0f);
            Vector3 eye = new Vector3(-Position.X * WorldTo3D, -Position.Y * WorldTo3D + 2f, -4f);
            Vector3 up = new Vector3((float)Math.Cos(MathHelper.PiOver2 - Rotation), (float)Math.Sin(MathHelper.PiOver2 - Rotation), 0f);

            View = Matrix.CreateLookAt(eye, lookAt, up);
            Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(45f), viewport.AspectRatio, 0.1f, FarPlane);
            Frustum = new BoundingFrustum(View * Projection);
        }

        private void UpdateDebugView(Viewport viewport)
        {
            float pixelsPerUnit = viewport.Height / DebugViewHeight;
            float width = viewport.Width / pixelsPerUnit;
            float height = viewport.Height / pixelsPerUnit;

            DebugProjection = Matrix.CreateOrthographicOffCenter(0f, width, height, 0f, 0f, 1f);
            DebugView = Matrix.CreateTranslation(new Vector3(-Position, 0f))
                * Matrix.CreateRotationZ(Rotation)
                * Matrix.CreateScale(zoom)
                * Matrix.CreateTranslation(width / 2f, height / 2f, 0f);
        }

        /// <summary>Ray going from the camera through the given point on the screen (3D coordinates).</summary>
        public Ray GetPickRay(Vector2 screenPoint, Viewport viewport)
        {
            Vector3 near = viewport.Unproject(new Vector3(screenPoint, 0f), Projection, View, Matrix.Identity);
            Vector3 far = viewport.Unproject(new Vector3(screenPoint, 1f), Projection, View, Matrix.Identity);
            return new Ray(near, Vector3.Normalize(far - near));
        }

        /// <summary>Point of the physics plane under the given point on the screen, in physics units.</summary>
        public Vector2? ScreenToWorld(Vector2 screenPoint, Viewport viewport)
        {
            Ray ray = GetPickRay(screenPoint, viewport);
            float? distance = ray.Intersects(new Plane(Vector3.Backward, 0f));
            if (!distance.HasValue)
                return null;

            Vector3 point = ray.Position + ray.Direction * distance.Value;
            return new Vector2(-point.X, -point.Y) / WorldTo3D;
        }
    }
}
