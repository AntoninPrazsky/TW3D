using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TW3D.Input;

namespace TW3D.Editor
{
    /// <summary>
    /// The camera of the editor: it looks at a target, usually a point of the track plane (z = 0), from a distance,
    /// turned around it (the yaw around the vertical axis, the pitch up and down). A yaw of 0 looks along +z as the
    /// camera of the game does, so the track runs from left to right on the screen. The mouse pans it (the middle
    /// button: the point of the plane under the cursor stays under it), orbits it (Alt + the left button, around the
    /// point under the cursor) and zooms it (the wheel, towards the cursor); the keys of the old editor fly it: W/S/A/D
    /// along the plane, Q/E up and down, and the numeric keypad turns it.
    /// </summary>
    sealed class EditorCamera
    {
        private const float FieldOfView = MathHelper.PiOver4;
        private const float NearPlane = 0.2f;
        private const float FarPlane = 1000f;
        //The pitch stops short of straight up and down, where the view would roll
        private const float MinPitch = -MathHelper.PiOver2 + 0.05f;
        private const float MaxPitch = MathHelper.PiOver2 - 0.05f;
        private const float MinDistance = 1f;
        private const float MaxDistance = 500f;
        //The view of the old editor: a little from above
        private const float DefaultPitch = -MathHelper.Pi / 10f;
        private const float DefaultDistance = 22f;
        //Radians of turning per pixel of an orbiting drag and per second of a key held
        private const float OrbitSpeed = 0.005f;
        private const float TurnSpeed = 1f;
        //How much of the distance to the target the camera flies per second with a key held
        private const float FlySpeed = 0.5f;
        //How much closer one notch of the wheel brings the camera
        private const float ZoomStep = 0.85f;
        //Room around the level when it is framed, as a part of its size
        private const float FrameMargin = 1.15f;
        //Mouse wheel units of MonoGame per notch
        private const float WheelNotch = 120f;

        private enum Drag
        {
            None,
            Pan,
            Orbit
        }

        private Drag drag;

        /// <summary>The mouse is dragging the camera (panning or orbiting): the buttons are the camera's until it ends.</summary>
        public bool IsDragging => drag != Drag.None;

        /// <summary>The point the camera looks at (scene units).</summary>
        public Vector3 Target { get; set; }

        /// <summary>Turn around the vertical axis (radians); 0 looks along +z, as the game does.</summary>
        public float Yaw { get; set; }

        /// <summary>Turn up (positive) or down (negative), radians.</summary>
        public float Pitch { get; set; } = DefaultPitch;

        /// <summary>How far the camera is from the target.</summary>
        public float Distance { get; set; } = DefaultDistance;

        /// <summary>The matrices of the last <see cref="Update"/>.</summary>
        public Matrix View { get; private set; }
        public Matrix Projection { get; private set; }

        public Vector3 Position => Target - Forward * Distance;

        /// <summary>The direction the camera looks in.</summary>
        public Vector3 Forward => new Vector3(MathF.Sin(Yaw) * MathF.Cos(Pitch), MathF.Sin(Pitch), MathF.Cos(Yaw) * MathF.Cos(Pitch));

        /// <summary>The direction the camera looks in along the plane, whatever its pitch.</summary>
        private Vector3 Ahead => new Vector3(MathF.Sin(Yaw), 0f, MathF.Cos(Yaw));

        /// <summary>The direction to the right of the screen along the plane.</summary>
        private Vector3 Right => Vector3.Normalize(Vector3.Cross(Ahead, Vector3.Up));

        /// <summary>Computes the matrices for the frame; call before drawing and picking.</summary>
        public void Update(Viewport viewport)
        {
            View = Matrix.CreateLookAt(Position, Target, Vector3.Up);
            Projection = Matrix.CreatePerspectiveFieldOfView(FieldOfView, viewport.AspectRatio, NearPlane, FarPlane);
        }

        /// <summary>The ray from the camera through a point of the screen.</summary>
        public Ray PickRay(Vector2 screenPoint, Viewport viewport)
        {
            Vector3 near = viewport.Unproject(new Vector3(screenPoint, 0f), Projection, View, Matrix.Identity);
            Vector3 far = viewport.Unproject(new Vector3(screenPoint, 1f), Projection, View, Matrix.Identity);
            return new Ray(near, Vector3.Normalize(far - near));
        }

        /// <summary>The point where a ray meets the plane z = depth; null when the ray runs away from it.</summary>
        public static Vector3? HitPlane(Ray ray, float depth)
        {
            float? distance = ray.Intersects(new Plane(Vector3.Backward, -depth));
            return distance.HasValue ? ray.Position + ray.Direction * distance.Value : null;
        }

        /// <summary>Frames a box of the scene: the camera looks at its middle from the default angle, far enough to see it all.</summary>
        public void Frame(BoundingBox bounds, float aspectRatio)
        {
            Vector3 centre = (bounds.Min + bounds.Max) / 2f;
            Vector3 half = (bounds.Max - bounds.Min) / 2f;
            float tangent = MathF.Tan(FieldOfView / 2f);
            float fit = Math.Max(half.Y / tangent, half.X / (tangent * aspectRatio));
            Target = new Vector3(centre.X, centre.Y, 0f);
            Yaw = 0f;
            Pitch = DefaultPitch;
            Distance = MathHelper.Clamp(fit * FrameMargin + half.Z, MinDistance, MaxDistance);
        }

        /// <summary>The default view of the start of the level.</summary>
        public void Reset()
        {
            Target = Vector3.Zero;
            Yaw = 0f;
            Pitch = DefaultPitch;
            Distance = DefaultDistance;
        }

        /// <summary>
        /// The keys of the old editor: W/S/A/D and Q/E fly the camera, the numeric keypad turns it. With Ctrl held the
        /// letters are shortcuts (Ctrl+S saves), not flying.
        /// </summary>
        /// <param name="elapsed">Elapsed seconds.</param>
        public void HandleKeys(InputState input, float elapsed)
        {
            Vector3 move = Vector3.Zero;
            if (!input.IsKeyDown(Keys.LeftControl) && !input.IsKeyDown(Keys.RightControl))
            {
                if (input.IsKeyDown(Keys.W)) move += Ahead;
                if (input.IsKeyDown(Keys.S)) move -= Ahead;
                if (input.IsKeyDown(Keys.D)) move += Right;
                if (input.IsKeyDown(Keys.A)) move -= Right;
                if (input.IsKeyDown(Keys.Q)) move += Vector3.Up;
                if (input.IsKeyDown(Keys.E)) move -= Vector3.Up;
            }
            if (move != Vector3.Zero)
                Target += move * FlySpeed * Distance * elapsed;

            if (input.IsKeyDown(Keys.NumPad6)) Yaw += TurnSpeed * elapsed;
            if (input.IsKeyDown(Keys.NumPad4)) Yaw -= TurnSpeed * elapsed;
            if (input.IsKeyDown(Keys.NumPad8)) Pitch += TurnSpeed * elapsed;
            if (input.IsKeyDown(Keys.NumPad2)) Pitch -= TurnSpeed * elapsed;
            Pitch = MathHelper.Clamp(Pitch, MinPitch, MaxPitch);
        }

        /// <summary>
        /// The mouse: the middle button drags the plane under the cursor, Alt + the left button orbits around the point
        /// under the cursor, the wheel zooms towards it. A drag begun goes on until the button is released, wherever the
        /// cursor gets to.
        /// </summary>
        /// <param name="free">The cursor is over the scene, not over the user interface.</param>
        /// <param name="zoom">The wheel is the camera's; false while a tool uses it (Ctrl + the wheel shifts a block).</param>
        public void HandleMouse(InputState input, Viewport viewport, bool free, bool zoom)
        {
            MouseState mouse = input.Mouse, previous = input.PreviousMouse;
            Vector2 position = new Vector2(mouse.X, mouse.Y);
            Vector2 previousPosition = new Vector2(previous.X, previous.Y);
            Update(viewport);

            switch (drag)
            {
                case Drag.None:
                    if (!free)
                        break;
                    if (mouse.MiddleButton == ButtonState.Pressed && previous.MiddleButton == ButtonState.Released)
                        drag = Drag.Pan;
                    else if (mouse.LeftButton == ButtonState.Pressed && previous.LeftButton == ButtonState.Released
                        && (input.IsKeyDown(Keys.LeftAlt) || input.IsKeyDown(Keys.RightAlt)))
                    {
                        drag = Drag.Orbit;
                        LookAt(HitPlane(PickRay(position, viewport), 0f) ?? Target);
                    }
                    break;
                case Drag.Pan:
                    if (mouse.MiddleButton == ButtonState.Released)
                        drag = Drag.None;
                    else
                        Grab(previousPosition, position, viewport);
                    break;
                case Drag.Orbit:
                    if (mouse.LeftButton == ButtonState.Released)
                        drag = Drag.None;
                    else
                        Orbit(position - previousPosition);
                    break;
            }

            if (free && zoom && input.WheelDelta != 0)
                Zoom(input.WheelDelta / WheelNotch, HitPlane(PickRay(position, viewport), 0f));
        }

        /// <summary>Makes a point the target without moving the camera.</summary>
        private void LookAt(Vector3 point)
        {
            Vector3 position = Position;
            Vector3 toPoint = point - position;
            float distance = toPoint.Length();
            if (distance < MinDistance)
                return;
            toPoint /= distance;
            Target = point;
            Distance = Math.Min(distance, MaxDistance);
            Pitch = MathHelper.Clamp(MathF.Asin(toPoint.Y), MinPitch, MaxPitch);
            Yaw = MathF.Atan2(toPoint.X, toPoint.Z);
        }

        /// <summary>Turns the camera around the target by a movement of the mouse (pixels).</summary>
        private void Orbit(Vector2 movement)
        {
            Yaw += movement.X * OrbitSpeed;
            Pitch = MathHelper.Clamp(Pitch + movement.Y * OrbitSpeed, MinPitch, MaxPitch);
        }

        /// <summary>Moves the camera so that the point of the plane under the cursor stays under it.</summary>
        private void Grab(Vector2 fromScreen, Vector2 toScreen, Viewport viewport)
        {
            Vector3? from = HitPlane(PickRay(fromScreen, viewport), 0f);
            Vector3? to = HitPlane(PickRay(toScreen, viewport), 0f);
            if (from.HasValue && to.HasValue)
                Target -= to.Value - from.Value;
        }

        /// <summary>Brings the camera closer by notches of the wheel (negative: further), keeping a point where it is on the screen.</summary>
        private void Zoom(float notches, Vector3? pivot)
        {
            float distance = MathHelper.Clamp(Distance * MathF.Pow(ZoomStep, notches), MinDistance, MaxDistance);
            float factor = distance / Distance;
            if (pivot.HasValue)
                Target = pivot.Value + (Target - pivot.Value) * factor;
            Distance = distance;
        }
    }
}
