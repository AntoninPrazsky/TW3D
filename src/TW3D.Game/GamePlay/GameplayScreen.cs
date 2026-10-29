using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using nkast.Aether.Physics2D;
using nkast.Aether.Physics2D.Diagnostics;
using nkast.Aether.Physics2D.Dynamics;
using nkast.Aether.Physics2D.Dynamics.Joints;
using TW3D.GameObjects;
using TW3D.Graphics;
using TW3D.Input;
using TW3D.Screens;
using TW3D.Shared;

namespace TW3D.GamePlay
{
    /// <summary>
    /// The game itself: one level with the car, its physics simulation, the 3D view,
    /// the on-screen controls and the physics debug view (F1–F8).
    /// </summary>
    public sealed class GameplayScreen : Screen
    {
        //Blocks and dynamic objects the levels are built from (content/catalog.json), loaded once
        private static Catalog catalog;

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

        //Physics units per second
        private const float CameraPanSpeed = 10f;

        private readonly string levelName;

        private World world;
        private PhysicsClock physics;
        private ChaseCamera camera;
        private DebugView debugView;
        private OnScreenControls controls;

        private Level level;
        private Sky sky;
        private Car car;

        private FixedMouseJoint dragJoint;
        private int dragPointerId;

        public GameplayScreen(string levelName)
        {
            this.levelName = levelName;
            FadeInTime = TimeSpan.FromSeconds(0.75);
            FadeOutTime = TimeSpan.FromSeconds(0.75);
            IsOpaque = true;
        }

        public override void LoadContent()
        {
            LoadCatalog(Manager.Content);

            Settings.ContinuousPhysics = false;
            world = new World(new Vector2(0f, 12f));
            physics = new PhysicsClock(world, new SolverIterations
            {
                VelocityIterations = 6,
                PositionIterations = 2,
                TOIVelocityIterations = Settings.TOIVelocityIterations,
                TOIPositionIterations = Settings.TOIPositionIterations,
                AutoClearForces = true
            });
            camera = new ChaseCamera { MinRotation = -0.5f, MaxRotation = 0.5f };

            level = new Level(world, Manager, camera, physics, catalog, Vector2.Zero, levelName);
            sky = new Sky(Manager, camera);
            car = new Car(world, Manager, camera, physics, new Vector2(-1.6f, -7f), "car", "wheel");

            debugView = new DebugView(world)
            {
                DefaultShapeColor = Color.White,
                SleepingShapeColor = Color.LightGray,
                Flags = DebugViewFlags.None
            };
            debugView.LoadContent(Manager.GraphicsDevice, Manager.Content);

            controls = new OnScreenControls();
            controls.LoadContent(Manager.Content);

            //Loading takes a while; the game should not try to catch up with the lost time afterwards
            Manager.Game.ResetElapsedTime();
        }

        public override void UnloadContent()
        {
            debugView?.Dispose();
        }

        /// <summary>Loads the catalog and the models of all its blocks and objects (shared by all levels, loaded only once).</summary>
        private static void LoadCatalog(ContentManager content)
        {
            if (catalog != null)
                return;

            using (Stream stream = TitleContainer.OpenStream("Content/" + Catalog.FileName))
                catalog = Catalog.Load(stream);

            foreach (BlockType block in catalog.Blocks)
                TrackBlockModels.Models.Add(block.Name, content.Load<Model>(block.ModelAsset));
            foreach (string model in catalog.ObjectModels)
                TrackPropModels.Models.Add(model, content.Load<Model>(ObjectType.ModelAsset(model)));
        }

        public override void HandleInput(InputState input, GameTime gameTime)
        {
            if (input.BackPressed)
            {
                Close();
                return;
            }

            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

            HandleDebugKeys(input);
            HandleCameraKeys(input, elapsed);

            controls.Update(input, Manager.Viewport, Manager.UiScale);
            Drive(DriveInput.Read(input, controls), elapsed);
            HandleCarDragging(input);
        }

        private void Drive(DriveInput drive, float elapsed)
        {
            //Throttle, slowly fading out when released
            if (Math.Abs(drive.Stick.X) > 0.2f)
                car.acceleration = drive.Stick.X * 5f;
            else
                car.acceleration -= Math.Sign(car.acceleration) * 2f * elapsed;

            //Axle rotation
            car.holderRotation = Math.Abs(drive.Stick.Y) > 0.7f;
            if (car.holderRotation)
                car.holderRotationDir = drive.Stick.Y > 0f;

            car.brake = drive.Brake;
            car.holderRotLockChnage = drive.LockAxles;
        }

        /// <summary>The car can be grabbed with the mouse or a finger and dragged around.</summary>
        private void HandleCarDragging(InputState input)
        {
            bool dragPointerFound = false;

            foreach (Pointer pointer in input.Pointers)
            {
                if (dragJoint == null)
                {
                    if (pointer.Phase == PointerPhase.Pressed && !controls.Uses(pointer) && car.IsIntersected(pointer.Position.X, pointer.Position.Y))
                    {
                        dragJoint = new FixedMouseJoint(car.car, car.car.Position) { MaxForce = 400f * car.car.Mass };
                        world.Add(dragJoint);
                        car.car.Awake = true;
                        dragPointerId = pointer.Id;
                        dragPointerFound = true;
                    }
                }
                else if (pointer.Id == dragPointerId && pointer.IsDown)
                {
                    dragPointerFound = true;
                    Vector2? target = camera.ScreenToWorld(pointer.Position, Manager.Viewport);
                    if (target.HasValue)
                        dragJoint.WorldAnchorB = target.Value;
                }
            }

            if (dragJoint != null && !dragPointerFound)
            {
                world.Remove(dragJoint);
                dragJoint = null;
            }
        }

        private void HandleDebugKeys(InputState input)
        {
            foreach ((Keys key, DebugViewFlags flags) in DebugKeys)
                if (input.KeyPressed(key))
                    debugView.Flags ^= flags;

            if (input.ButtonPressed(Buttons.Start))
                debugView.Flags ^= GamePadDebugFlags;
        }

        private void HandleCameraKeys(InputState input, float elapsed)
        {
            Vector2 pan = Vector2.Zero;
            if (input.IsKeyDown(Keys.NumPad8)) pan.Y -= 1f;
            if (input.IsKeyDown(Keys.NumPad2)) pan.Y += 1f;
            if (input.IsKeyDown(Keys.NumPad4)) pan.X -= 1f;
            if (input.IsKeyDown(Keys.NumPad6)) pan.X += 1f;
            if (pan != Vector2.Zero)
                camera.Pan(pan * CameraPanSpeed * elapsed);

            if (input.IsKeyDown(Keys.PageUp))
                camera.Zoom *= 1f + 0.25f * elapsed;
            if (input.IsKeyDown(Keys.PageDown))
                camera.Zoom /= 1f + 0.25f * elapsed;
            if (input.KeyPressed(Keys.Home))
                camera.ResetView();
        }

        public override void Update(GameTime gameTime)
        {
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

            car.Update();
            level.Update();

            //The simulation runs with a fixed time step whatever the frame rate is,
            //and pauses while the screen does not have the focus (window inactive, screen closing)
            if (HasFocus)
                physics.Advance(elapsed);

            camera.Update(elapsed, Manager.Viewport, physics.Position(car.car), physics.Rotation(car.car));
        }

        public override void Draw(GameTime gameTime)
        {
            //The sky is a background and has to be drawn first
            sky.Draw();
            car.Draw();
            level.Draw();

            Matrix debugProjection = camera.DebugProjection;
            Matrix debugViewMatrix = camera.DebugView;
            debugView.RenderDebugData(ref debugProjection, ref debugViewMatrix);

            SpriteBatch batch = Manager.SpriteBatch;
            batch.Begin();
            controls.Layout(Manager.Viewport, Manager.UiScale);
            controls.Draw(batch, 1f);
            //Fading in and out through black
            batch.Draw(Manager.Pixel, Manager.Viewport.Bounds, Color.Black * (1f - Visibility));
            batch.End();
        }
    }
}
