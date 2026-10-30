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
    /// the lift, and a landscape behind the windows. The camera slowly drifts around the view of the menu, or, as
    /// <see cref="Focus"/> grows, sweeps around the car on the lift (the garage screen).
    /// </summary>
    public sealed class GarageScene
    {
        //The garage model is in the units of the car models, y up, with the axis of the lift along z and its middle
        //on the floor here; it is turned so that the axis runs along x (as the cars stand) and scaled as the cars
        private const float ModelScale = 0.25f;
        private static readonly Vector3 LiftCentre = new Vector3(0.14f, -1.1522f, 0f);

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
        //view from the front corner of the car (this far from its side view) at this rate (radians per second)
        private const float GarageDistance = 3f;
        private const float GaragePitch = 18f;
        private const float GarageFieldOfView = 40f;
        private const float GarageCorner = 45f;
        private const float GarageSweep = 35f;
        private const float GarageSweepRate = 0.25f;
        //The shadows reach across the hall (world units)
        private const float ShadowDistance = 10f;

        /// <summary>Distance of the cars of the row (world units).</summary>
        public const float Spacing = 2.7f;

        private readonly ScreenManager manager;
        private readonly GameSettings settings;
        private readonly SceneRenderer renderer;
        private readonly Model garage, window;
        private readonly List<CarType> cars;
        private readonly Catalog catalog;
        private readonly World world = new World(Vector2.Zero);
        private readonly Dictionary<(string Car, string Front, string Rear), Car> previews = new Dictionary<(string, string, string), Car>();
        private readonly List<(CarType Car, float Offset)> row = new List<(CarType, float)>();
        //With two cars, the side of the lift the other one stands on (it stays there when the row stops): at first
        //the side away from the camera of the menu
        private int otherSide = -1;
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
        /// The cars of the row near the lift and how far along the row from it they are (world units): the chosen
        /// car and its neighbours (with two cars, the other one on the side the row moves from).
        /// </summary>
        public List<(CarType Car, float Offset)> Row()
        {
            row.Clear();
            int selected = Selected;
            if (Scroll > 0.001f)
                otherSide = -1;
            else if (Scroll < -0.001f)
                otherSide = 1;
            for (int offset = -1; offset <= 1; offset++)
            {
                if (cars.Count == 2 && offset == -otherSide)
                    continue;
                if (cars.Count < 2 && offset != 0)
                    continue;
                row.Add((cars[(selected + offset + cars.Count) % cars.Count], offset * Spacing + Scroll));
            }
            return row;
        }

        public void Update(float elapsed)
        {
            time += elapsed;
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

            float focus = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(Focus, 0f, 1f));
            Vector3 eye = Vector3.Lerp(menuEye, carEye, focus);
            Vector3 target = Vector3.Lerp(menuTarget, carTarget, focus);
            float fieldOfView = MathHelper.Lerp(MenuFieldOfView, GarageFieldOfView, focus);
            Matrix view = Matrix.CreateLookAt(eye, target, Vector3.Up);
            Matrix projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(fieldOfView), viewport.AspectRatio, 0.05f, 100f);

            Vector3 left = viewport.Project(carTarget, projection, view, Matrix.Identity);
            Vector3 right = viewport.Project(carTarget + Vector3.UnitX, projection, view, Matrix.Identity);
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
                preview.Placement = Matrix.CreateTranslation(offset, 0f, 0f);
                preview.Draw(renderer);
            }
            renderer.End();
        }
    }
}
