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
    /// the lift in the order of the catalog, and a landscape behind the windows. The camera slowly drifts around the view
    /// of the menu, or, as <see cref="Focus"/> grows, sweeps around the car on the lift (the garage screen), or comes to
    /// the wheels of one of its axles (<see cref="View"/>).
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

        /// <summary>Distance of the cars of the row (world units): the car in front of the lift stands behind the camera of the menu.</summary>
        public const float Spacing = 4f;

        private readonly ScreenManager manager;
        private readonly GameSettings settings;
        private readonly SceneRenderer renderer;
        private readonly Model garage, window;
        private readonly List<CarType> cars;
        private readonly Catalog catalog;
        private readonly World world = new World(Vector2.Zero);
        private readonly Dictionary<(string Car, string Front, string Rear), Car> previews = new Dictionary<(string, string, string), Car>();
        private readonly List<(CarType Car, float Offset)> row = new List<(CarType, float)>();
        //How far the camera has come to the wheels of each axle (0–1)
        private float frontWheels, rearWheels;
        private float time;

        public GarageScene(ScreenManager manager, GameSettings settings)
        {
            this.manager = manager;
            this.settings = settings;
            using (Stream stream = TitleContainer.OpenStream("Content/" + Catalog.FileName))
                catalog = Catalog.Load(stream);
            cars = catalog.Cars;
            ModelCollision.Load();
            garage = manager.Content.Load<Model>("Garage/garage");
            window = manager.Content.Load<Model>("Garage/window");
            renderer = new SceneRenderer(manager.GraphicsDevice, manager.Content) { Lighting = Lighting.Garage, ShadowDistance = ShadowDistance };
        }

        /// <summary>0 = the view of the menu, 1 = the view of the garage screen (between them the camera moves).</summary>
        public float Focus { get; set; }

        /// <summary>How far the row of cars is moved along its axis (world units), while it is dragged or settles.</summary>
        public float Scroll { get; set; }

        /// <summary>What the camera of the garage screen looks at: the whole car on the lift or the wheels of an axle.</summary>
        public CarView View { get; set; }

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
        }

        /// <summary>
        /// Where the camera looks at the wheels of an axle of the car on the lift from: from its side turned to the
        /// camera of the garage screen (towards -z), turned towards the end of the car the axle is at.
        /// </summary>
        private (Vector3 Eye, Vector3 Target) WheelsView(Car car, bool front)
        {
            Vector3 target = Vector3.Transform(car.WheelsCentre(front, true), Turned);
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
            Matrix garageWorld = Matrix.CreateTranslation(-LiftCentre) * Matrix.CreateRotationY(MathHelper.PiOver2)
                * Matrix.CreateScale(ModelScale) * Matrix.CreateTranslation(0f, selected.WheelsBottom, 0f);

            //The camera: between the view of the menu and the view of the car on the lift
            Vector3 menuEye = Vector3.Transform(MenuEye, garageWorld);
            Vector3 menuTarget = Vector3.Transform(MenuEye + Vector3.Normalize(MenuDirection) * MenuLook, garageWorld);
            menuEye = Vector3.Transform(menuEye - menuTarget, Matrix.CreateRotationY(MenuDrift * MathF.Sin(time * MenuDriftRate))) + menuTarget;

            Vector3 carTarget = new Vector3(0f, (selected.WheelsBottom + 0.3f) / 2f, 0f);
            float sweep = MathHelper.ToRadians(GarageCorner + GarageSweep * MathF.Sin(time * GarageSweepRate));
            float pitch = MathHelper.ToRadians(GaragePitch);
            Vector3 carEye = carTarget + GarageDistance * new Vector3(MathF.Sin(sweep) * MathF.Cos(pitch), MathF.Sin(pitch), -MathF.Cos(sweep) * MathF.Cos(pitch));
            Vector3 garageEye = carEye, garageTarget = carTarget;
            if (frontWheels > 0f)
            {
                (Vector3 eye, Vector3 target) wheels = WheelsView(selected, true);
                float blend = MathHelper.SmoothStep(0f, 1f, frontWheels);
                garageEye = Vector3.Lerp(garageEye, wheels.eye, blend);
                garageTarget = Vector3.Lerp(garageTarget, wheels.target, blend);
            }
            if (rearWheels > 0f)
            {
                (Vector3 eye, Vector3 target) wheels = WheelsView(selected, false);
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
            renderer.SetBackground(window, garageWorld);
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
