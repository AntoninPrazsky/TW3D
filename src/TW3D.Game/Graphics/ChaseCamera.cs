using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TW3D.Shared;

namespace TW3D.Graphics
{
    /// <summary>
    /// Camera following an object (the car) with a delay. Provides the 3D view of the scene
    /// and the 2D view for the physics debug view. It can frame another point with the object, from the side
    /// (<see cref="Framed"/>: the signal lamps of the start gate during the countdown).
    /// </summary>
    /// <remarks>
    /// Physics coordinates (x, y) are drawn at (-x, -y) × <see cref="Units.PhysicsToScene"/>; the physics plane is z = 0.
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

        private const float WorldTo3D = Units.PhysicsToScene;
        //Far clipping plane distance. The 2016 version used 22 or 50 to save the performance of phones,
        //which cut off the scene too close; now it is far beyond the sky and any level.
        private const float FarPlane = 1000f;
        //Physics units visible vertically in the debug view at zoom 1
        private const float DebugViewHeight = 30f;

        private const float MinZoom = 0.02f;
        private const float MaxZoom = 20f;

        //The eye: this far above the point it follows and in front of the road (scene units), turned about the vertical
        //axis through that point (radians), and farther by a factor
        private static readonly Vector3 EyeOffset = new Vector3(0f, 2f, -4f);
        //Framing a point with the target (see Framed): the camera looks at the point this part of the way from the target to
        //it, from behind the target and low: the eye turns this much (radians; negative turns it back along the road, so
        //that it looks ahead past the target at what it frames), is this high above the road instead of EyeOffset.Y, and
        //moves away until both are in view with this margin (physics units), at least and at most this many times farther
        private const float FramingShare = 0.45f;
        private const float FramingYaw = -0.85f;
        private const float FramingEyeHeight = 0.7f;
        private const float FramingMargin = 12f;
        private const float MinFramingDistance = 1.6f;
        private const float MaxFramingDistance = 2.2f;
        private const float FieldOfView = 45f;

        private Vector2 panOffset;
        private float zoom = 1f;
        //The turn, the height and the distance of the eye of this frame (see EyeOffset)
        private float eyeYaw;
        private float eyeHeight = EyeOffset.Y;
        private float eyeDistance = 1f;

        /// <summary>Position of the followed object in physics units; the camera looks at it.</summary>
        public Vector2 Focus { get; private set; }

        /// <summary>
        /// A point kept in view with the target (the signal lamps of the start gate during the countdown), in physics units:
        /// the camera looks between the two, from the side, and as far as needed to see both. Null for none.
        /// </summary>
        public Vector2? Framed { get; set; }

        /// <summary>How much the camera frames <see cref="Framed"/>: 1 fully, 0 not at all (it follows the target alone, as always).</summary>
        public float Framing { get; set; }

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

        /// <summary>
        /// Puts the camera at its target at once, as if it had caught up with it: a level started far from where the
        /// camera is (a test drive of the level editor from any cell) does not fly the camera in.
        /// </summary>
        /// <param name="targetPosition">Position of the followed object in physics units.</param>
        public void JumpTo(Vector2 targetPosition)
        {
            Focus = FramedFocus(targetPosition);
            Position = Focus + panOffset;
        }

        /// <summary>What the camera looks at for a target: the target, or a point on the way from it to the framed point.</summary>
        private Vector2 FramedFocus(Vector2 targetPosition)
        {
            if (!(Framed is Vector2 framed) || Framing <= 0f)
                return targetPosition;
            return Vector2.Lerp(targetPosition, Vector2.Lerp(targetPosition, framed, FramingShare), Framing);
        }

        /// <summary>How many times farther the eye has to be to see the target and the framed point, with a margin.</summary>
        private float FramingDistance(Viewport viewport, Vector2 targetPosition)
        {
            if (!(Framed is Vector2 framed))
                return 1f;
            float halfHeight = MathF.Tan(MathHelper.ToRadians(FieldOfView) / 2f) * -EyeOffset.Z / WorldTo3D;
            float halfWidth = halfHeight * viewport.AspectRatio;
            Vector2 half = new Vector2(Math.Abs(framed.X - targetPosition.X), Math.Abs(framed.Y - targetPosition.Y)) / 2f + new Vector2(FramingMargin);
            return MathHelper.Clamp(Math.Max(half.X / halfWidth, half.Y / halfHeight), MinFramingDistance, MaxFramingDistance);
        }

        /// <summary>Moves the camera after the followed object; call once per frame.</summary>
        /// <param name="targetPosition">Position of the followed object in physics units.</param>
        /// <param name="targetRotation">Rotation of the followed object in radians.</param>
        public void Update(float elapsedSeconds, Viewport viewport, Vector2 targetPosition, float targetRotation)
        {
            Focus = FramedFocus(targetPosition);
            float framing = Framed.HasValue ? MathHelper.Clamp(Framing, 0f, 1f) : 0f;
            eyeYaw = FramingYaw * framing;
            eyeHeight = MathHelper.Lerp(EyeOffset.Y, FramingEyeHeight, framing);
            eyeDistance = MathHelper.Lerp(1f, FramingDistance(viewport, targetPosition), framing);
            float rotation = MathHelper.Clamp(-targetRotation % MathHelper.TwoPi, MinRotation, MaxRotation);

            Position = Follow(Position, Focus + panOffset, elapsedSeconds);
            Rotation = FollowAngle(Rotation, rotation, elapsedSeconds);

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
            //The camera looks at the target (with no delay) from its own delayed position, slightly above and in front of
            //the road, turned to the side and farther while it frames another point
            Vector3 lookAt = new Vector3(-Focus.X * WorldTo3D - 0.5f, -Focus.Y * WorldTo3D, 0f);
            Vector3 eye = new Vector3(-Position.X * WorldTo3D, -Position.Y * WorldTo3D, 0f)
                + Vector3.Transform(new Vector3(EyeOffset.X, eyeHeight, EyeOffset.Z) * eyeDistance, Matrix.CreateRotationY(eyeYaw));
            Vector3 up = new Vector3((float)Math.Cos(MathHelper.PiOver2 - Rotation), (float)Math.Sin(MathHelper.PiOver2 - Rotation), 0f);

            View = Matrix.CreateLookAt(eye, lookAt, up);
            Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(FieldOfView), viewport.AspectRatio, 0.1f, FarPlane);
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
