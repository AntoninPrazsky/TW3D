using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using nkast.Aether.Physics2D.Dynamics;
using TW3D.Graphics;
using TW3D.Screens;
using TW3D.Shared;

namespace TW3D.GameObjects
{
    /// <summary>
    /// The 3D garage behind the menus (content/Garage, built from the Wings 3D files of 2015–2016): a hall with a lift in
    /// its middle, the car chosen in the garage on the lift with its wheels, the other cars in a row along the axis of
    /// the lift in the order of the catalog, and the sky of a day behind the windows. The camera slowly drifts around the view
    /// of the menu, or, as <see cref="Focus"/> grows, sweeps around the car on the lift (the garage screen), or comes to
    /// the wheels of one of its axles (<see cref="View"/>). The main menu shows the 3D logo of the game in it
    /// (<see cref="Title"/>): placed against the frame, not against the hall, so that it keeps its place in the picture
    /// however the camera drifts and whatever the shape of the window, lit by the light of the garage, swaying a little,
    /// and drawn over everything (see <see cref="SceneRenderer.SetOverlay"/>).
    /// </summary>
    public sealed class GarageScene
    {
        /// <summary>What the camera of the garage screen looks at.</summary>
        public enum CarView
        {
            Whole,
            FrontWheels,
            RearWheels
        }

        //The garage model is in the units of the car models, y up, with the axis of the lift along z and its middle
        //on the floor here; it is turned so that the axis runs along x (as the cars stand) and scaled as the cars
        private const float ModelScale = 0.25f;
        private static readonly Vector3 LiftCentre = new Vector3(0.14f, -1.1522f, 0f);
        //The cars face +x, towards the camera of the menu, as the car of the render of 2015 did (their models face -x).
        //The row runs from the car on the lift backwards: the next cars of the catalog behind it, the previous ones in
        //front of it, out of the view of the menu.
        private static readonly Matrix Turned = Matrix.CreateRotationY(MathHelper.Pi);
        private static readonly Vector3 RowAxis = -Vector3.UnitX;

        //The view of the menu (in the garage model): where the camera of the render of 2015 stood and where it looked,
        //its vertical field of view (degrees); it drifts this far around the car (radians) at this rate (radians per second)
        private static readonly Vector3 MenuEye = new Vector3(4.95f, 3.54f, 9.93f);
        private static readonly Vector3 MenuDirection = new Vector3(-0.606f, -0.256f, -0.753f);
        private const float MenuLook = 11f;
        private const float MenuFieldOfView = 45f;
        private const float MenuDrift = 0.15f;
        private const float MenuDriftRate = 0.12f;

        //The view of the garage screen: the camera looks at the car on the lift from this distance (world units),
        //this much from above (degrees), with this field of view, sweeping this far to both sides (degrees) of the
        //view from the front corner of the car (this far from its side view) at this rate (radians per second); it
        //stays beside the cars rolling along the row
        private const float GarageDistance = 3f;
        private const float GaragePitch = 18f;
        private const float GarageFieldOfView = 40f;
        private const float GarageCorner = 45f;
        private const float GarageSweep = 30f;
        private const float GarageSweepRate = 0.25f;
        //The view of the wheels of an axle: from this distance (world units), this much from above (degrees), from
        //the side of the car turned this far towards the end of the car the axle is at, sweeping this far (degrees);
        //the camera comes to the wheels or goes back in this time (seconds)
        private const float WheelsDistance = 1.6f;
        private const float WheelsPitch = 12f;
        private const float WheelsAngle = 30f;
        private const float WheelsSweep = 10f;
        private const float WheelsTime = 0.8f;
        //The shadows reach across the hall (world units)
        private const float ShadowDistance = 10f;
        //The logo hangs this far in front of the camera (world units): its size is solved from the frame, so this sets
        //only its perspective, and it is drawn over the scene, so nothing of the hall cuts it however near it is
        private const float LogoDistance = 1f;
        //How deep the logo is, to its height (tools/garagegen/logo.py prints it): its front stands at that distance, so
        //that the wheels of the emblem, nearer the camera than the middle of the logo, do not reach out of its place
        private const float LogoDepth = 0.417f;
        //It sways about its vertical and its horizontal axis this far (radians) at these unrelated rates (radians per
        //second), so that it reads as an object in the room, never so far that it is hard to read; standing in the left
        //corner, it is always turned a little towards the middle of the frame (its face to the right), as a sign faces
        //the room rather than the wall beside it
        private const float LogoYaw = 0.05f, LogoPitch = 0.04f, LogoTurn = 0.1f;
        private const float LogoYawRate = 0.37f, LogoPitchRate = 0.23f;
        //The floor under the car on the lift and the middles of its axles follow the car at this rate (1 / seconds):
        //other wheels (larger, or wider) raise the car on the lift and move the view of the wheels smoothly, not at once
        private const float FollowRate = 8f;

        /// <summary>Distance of the cars of the row (world units): the car in front of the lift stands behind the camera of the menu.</summary>
        public const float Spacing = 4f;

        private readonly ScreenManager manager;
        private readonly GameSettings settings;
        private readonly SceneRenderer renderer;
        private readonly Model garage, logo;
        private readonly List<CarType> cars;
        private readonly Catalog catalog;
        private readonly World world = new World(Vector2.Zero);
        private readonly Dictionary<(string Car, string Front, string Rear), Car> previews = new Dictionary<(string, string, string), Car>();
        private readonly List<(CarType Car, float Offset)> row = new List<(CarType, float)>();
        //How far the camera has come to the wheels of each axle (0–1)
        private float frontWheels, rearWheels;
        private float time;
        //The bottom of the wheels of the car on the lift and the middles of its axles, following the car (see FollowRate);
        //null until the first update
        private float? floor;
        private Vector3? frontCentre, rearCentre;

        public GarageScene(ScreenManager manager, GameSettings settings)
        {
            this.manager = manager;
            this.settings = settings;
            using (Stream stream = TitleContainer.OpenStream("Content/" + Catalog.FileName))
                catalog = Catalog.Load(stream);
            cars = catalog.Cars;
            ModelCollision.Load();
            garage = manager.Content.Load<Model>("Garage/garage");
            logo = manager.Content.Load<Model>("Garage/logo");
            renderer = new SceneRenderer(manager.GraphicsDevice, manager.Content) { Lighting = Lighting.Garage, ShadowDistance = ShadowDistance };
        }

        /// <summary>0 = the view of the menu, 1 = the view of the garage screen (between them the camera moves).</summary>
        public float Focus { get; set; }

        /// <summary>How far the row of cars is moved along its axis (world units), while it is dragged or settles.</summary>
        public float Scroll { get; set; }

        /// <summary>What the camera of the garage screen looks at: the whole car on the lift or the wheels of an axle.</summary>
        public CarView View { get; set; }

        /// <summary>
        /// Where the 3D logo stands in the frame: the middle of its box and its height, in pixels of the screen (the main
        /// menu sets it as it lays itself out, and moves it up out of the frame as it goes); null for no logo.
        /// </summary>
        public (Vector2 Centre, float Height)? Title { get; set; }

        /// <summary>
        /// Pixels the cars on the lift move to the right on the screen when the row moves by a world unit, as last
        /// drawn (negative when the row runs from right to left).
        /// </summary>
        public float PixelsPerUnit { get; private set; } = 100f;

        private int Selected => Math.Max(0, cars.FindIndex(car => car.Model == settings.Car));

        /// <summary>The car with the wheels chosen for it.</summary>
        private Car Preview(CarType car)
        {
            CarWheels wheels = settings.WheelsOf(car);
            var key = (car.Model, wheels.Front, wheels.Rear);
            if (!previews.TryGetValue(key, out Car preview))
            {
                preview = new Car(world, manager, null, null, Vector2.Zero, car, catalog.FindWheel(wheels.Front), catalog.FindWheel(wheels.Rear));
                previews.Add(key, preview);
            }
            return preview;
        }

        /// <summary>
        /// The cars of the row near the lift and how far along the row from it they are (world units, the next cars of
        /// the catalog further): the chosen car and its neighbours.
        /// </summary>
        public List<(CarType Car, float Offset)> Row()
        {
            row.Clear();
            int selected = Selected;
            for (int index = Math.Max(0, selected - 1); index <= Math.Min(cars.Count - 1, selected + 1); index++)
                row.Add((cars[index], (index - selected) * Spacing + Scroll));
            return row;
        }

        public void Update(float elapsed)
        {
            time += elapsed;
            float step = elapsed / WheelsTime;
            frontWheels = MathHelper.Clamp(frontWheels + (View == CarView.FrontWheels ? step : -step), 0f, 1f);
            rearWheels = MathHelper.Clamp(rearWheels + (View == CarView.RearWheels ? step : -step), 0f, 1f);

            Car selected = Preview(cars[Selected]);
            float follow = 1f - MathF.Exp(-FollowRate * elapsed);
            floor = MathHelper.Lerp(floor ?? selected.WheelsBottom, selected.WheelsBottom, follow);
            frontCentre = Vector3.Lerp(frontCentre ?? selected.WheelsCentre(true, true), selected.WheelsCentre(true, true), follow);
            rearCentre = Vector3.Lerp(rearCentre ?? selected.WheelsCentre(false, true), selected.WheelsCentre(false, true), follow);
        }

        /// <summary>
        /// The world matrix of the logo (one unit high, its middle at its origin, facing +z; see tools/garagegen/logo.py)
        /// standing in a place of the frame: the place turned into the camera's own coordinates through its projection, at
        /// <see cref="LogoDistance"/> in front of it, so that it keeps its place at any field of view and any window shape.
        /// </summary>
        /// <param name="centre">Where the middle of the logo is on the screen, in pixels.</param>
        /// <param name="height">How high it is on the screen, in pixels.</param>
        private Matrix LogoWorld(Vector2 centre, float height, Matrix view, Matrix projection, Viewport viewport)
        {
            //A point of the screen at a distance in front of the camera: x right, y up, the camera looking along -z
            float x = (centre.X / viewport.Width * 2f - 1f) * LogoDistance / projection.M11;
            float y = (1f - centre.Y / viewport.Height * 2f) * LogoDistance / projection.M22;
            float size = height / viewport.Height * 2f * LogoDistance / projection.M22;
            Matrix sway = Matrix.CreateRotationY(LogoTurn + LogoYaw * MathF.Sin(time * LogoYawRate))
                * Matrix.CreateRotationX(LogoPitch * MathF.Sin(time * LogoPitchRate + 1f));
            return Matrix.CreateScale(size) * sway * Matrix.CreateTranslation(x, y, -LogoDistance - LogoDepth / 2f * size) * Matrix.Invert(view);
        }

        /// <summary>
        /// Where the camera looks at the wheels of an axle of the car on the lift from: from its side turned to the
        /// camera of the garage screen (towards -z), turned towards the end of the car the axle is at.
        /// </summary>
        /// <param name="centre">The middle of the axle's wheels (see Car.WheelsCentre), before the car is turned.</param>
        private (Vector3 Eye, Vector3 Target) WheelsView(Vector3 centre, bool front)
        {
            Vector3 target = Vector3.Transform(centre, Turned);
            float angle = MathHelper.ToRadians((front ? WheelsAngle : -WheelsAngle) + WheelsSweep * MathF.Sin(time * GarageSweepRate));
            float pitch = MathHelper.ToRadians(WheelsPitch);
            return (target + WheelsDistance * new Vector3(MathF.Sin(angle) * MathF.Cos(pitch), MathF.Sin(pitch), -MathF.Cos(angle) * MathF.Cos(pitch)), target);
        }

        public void Draw()
        {
            Viewport viewport = manager.Viewport;
            settings.Apply(renderer);

            //The garage stands on the floor where the wheels of the cars stand, its lift under the car on it
            Car selected = Preview(cars[Selected]);
            float wheelsBottom = floor ?? selected.WheelsBottom;
            Matrix garageWorld = Matrix.CreateTranslation(-LiftCentre) * Matrix.CreateRotationY(MathHelper.PiOver2)
                * Matrix.CreateScale(ModelScale) * Matrix.CreateTranslation(0f, wheelsBottom, 0f);

            //The camera: between the view of the menu and the view of the car on the lift
            Vector3 menuEye = Vector3.Transform(MenuEye, garageWorld);
            Vector3 menuTarget = Vector3.Transform(MenuEye + Vector3.Normalize(MenuDirection) * MenuLook, garageWorld);
            menuEye = Vector3.Transform(menuEye - menuTarget, Matrix.CreateRotationY(MenuDrift * MathF.Sin(time * MenuDriftRate))) + menuTarget;

            Vector3 carTarget = new Vector3(0f, (wheelsBottom + 0.3f) / 2f, 0f);
            float sweep = MathHelper.ToRadians(GarageCorner + GarageSweep * MathF.Sin(time * GarageSweepRate));
            float pitch = MathHelper.ToRadians(GaragePitch);
            Vector3 carEye = carTarget + GarageDistance * new Vector3(MathF.Sin(sweep) * MathF.Cos(pitch), MathF.Sin(pitch), -MathF.Cos(sweep) * MathF.Cos(pitch));
            Vector3 garageEye = carEye, garageTarget = carTarget;
            if (frontWheels > 0f)
            {
                (Vector3 eye, Vector3 target) wheels = WheelsView(frontCentre ?? selected.WheelsCentre(true, true), true);
                float blend = MathHelper.SmoothStep(0f, 1f, frontWheels);
                garageEye = Vector3.Lerp(garageEye, wheels.eye, blend);
                garageTarget = Vector3.Lerp(garageTarget, wheels.target, blend);
            }
            if (rearWheels > 0f)
            {
                (Vector3 eye, Vector3 target) wheels = WheelsView(rearCentre ?? selected.WheelsCentre(false, true), false);
                float blend = MathHelper.SmoothStep(0f, 1f, rearWheels);
                garageEye = Vector3.Lerp(garageEye, wheels.eye, blend);
                garageTarget = Vector3.Lerp(garageTarget, wheels.target, blend);
            }

            float focus = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(Focus, 0f, 1f));
            Vector3 eye = Vector3.Lerp(menuEye, garageEye, focus);
            Vector3 target = Vector3.Lerp(menuTarget, garageTarget, focus);
            float fieldOfView = MathHelper.Lerp(MenuFieldOfView, GarageFieldOfView, focus);
            Matrix view = Matrix.CreateLookAt(eye, target, Vector3.Up);
            Matrix projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(fieldOfView), viewport.AspectRatio, 0.05f, 100f);

            Vector3 left = viewport.Project(carTarget, projection, view, Matrix.Identity);
            Vector3 right = viewport.Project(carTarget + RowAxis, projection, view, Matrix.Identity);
            PixelsPerUnit = right.X - left.X;

            renderer.Begin(view, projection);
            if (Title is (Vector2 titleCentre, float titleHeight))
                renderer.SetOverlay(logo, LogoWorld(titleCentre, titleHeight, view, projection, viewport));
            //The windows look out at the sky of a day (Lighting.Garage)
            renderer.SetSky();
            renderer.Add(garage, garageWorld);
            //The car on the lift last: its headlights light the hall
            List<(CarType Car, float Offset)> drawn = Row();
            drawn.Sort((a, b) => Math.Abs(b.Offset).CompareTo(Math.Abs(a.Offset)));
            foreach ((CarType car, float offset) in drawn)
            {
                Car preview = Preview(car);
                preview.Placement = Turned * Matrix.CreateTranslation(RowAxis * offset);
                preview.Draw(renderer);
            }
            renderer.End();
        }
    }
}
