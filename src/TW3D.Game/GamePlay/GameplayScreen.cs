using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using nkast.Aether.Physics2D;
using nkast.Aether.Physics2D.Diagnostics;
using nkast.Aether.Physics2D.Dynamics;
using TW3D.GameObjects;
using TW3D.Graphics;
using TW3D.Input;
using TW3D.Screens;
using TW3D.Shared;

namespace TW3D.GamePlay
{
    /// <summary>
    /// The game itself: one level with the car, its physics simulation, the 3D view, the on-screen controls, the
    /// tutorial, the pause menu and the keys for development. A run of the level (see <see cref="Race"/>) starts with
    /// a countdown, the car held still; the time runs from the start until the car reaches the finish block of the
    /// level, then the results follow.
    /// </summary>
    public sealed class GameplayScreen : Screen
    {
        //Game seconds per real second. For the physics engine the world is huge (the car is about
        //20 metres long, gravity 12 m/s²), so at real time everything moves as in slow motion.
        //Shrinking the whole world while keeping its look would be the same as running the game faster:
        //everything moves along the same paths, only in a shorter time.
        private const float GameSpeed = 1.5f;

        //How far the stick has to be pushed sideways to drive, and up or down to turn the axles
        private const float ThrottleThreshold = 0.2f;
        private const float AxlesThreshold = 0.7f;

        //Where the car starts (physics units): on the road at the cell [0, 0] of the level
        private static readonly Vector2 CarStart = new Vector2(-1.6f, -7f);

        private readonly string levelName;
        private readonly GameSettings settings;
        private readonly GameRecords records;
        private readonly IReadOnlyList<string> levels;

        private World world;
        private PhysicsClock physics;
        private ChaseCamera camera;
        private SceneRenderer renderer;
        private DebugView debugView;
        private OnScreenControls controls;
        private DevelopmentKeys developmentKeys;
        private CarDrag drag;

        private Level level;
        private Sky sky;
        private Car car;
        private Race race;
        //The cards of the tutorial; null when the tutorial is off
        private Tutorial tutorial;
        //The best time before the run, and whether the run became the best one
        private float? previousBest;
        private bool record;
        //The results over the level at the finish; null until they come
        private ResultsScreen results;

        /// <param name="levels">All levels in the order of the level select (for the next one after the finish).</param>
        public GameplayScreen(string levelName, GameSettings settings, GameRecords records, IReadOnlyList<string> levels)
        {
            this.levelName = levelName;
            this.settings = settings;
            this.records = records;
            this.levels = levels;
            FadeInTime = TimeSpan.FromSeconds(0.75);
            FadeOutTime = TimeSpan.FromSeconds(0.75);
            IsOpaque = true;
        }

        public override void LoadContent()
        {
            LevelAssets assets = LevelAssets.Load(Manager.Content);

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
            renderer = new SceneRenderer(Manager.GraphicsDevice, Manager.Content);
            settings.Apply(renderer);

            level = new Level(world, Manager, camera, physics, assets, Vector2.Zero, levelName);
            renderer.Lighting = Lighting.For(level.Mood);
            sky = new Sky(Manager, camera);
            Catalog catalog = assets.Catalog;
            CarType carType = catalog.FindCar(settings.Car);
            CarWheels wheels = settings.WheelsOf(carType);
            car = new Car(world, Manager, camera, physics, CarStart, carType, catalog.FindWheel(wheels.Front), catalog.FindWheel(wheels.Rear));
            race = new Race(level.Par, level.HasFinish);

            debugView = new DebugView(world)
            {
                DefaultShapeColor = Color.White,
                SleepingShapeColor = Color.LightGray,
                Flags = DebugViewFlags.None
            };
            debugView.LoadContent(Manager.GraphicsDevice, Manager.Content);

            controls = new OnScreenControls();
            controls.LoadContent(Manager.Content, Manager.Pixel);
            developmentKeys = new DevelopmentKeys(debugView, renderer, camera, level.Mood);
            if (settings.Tutorial || Tutorial.Lever != TutorialLever.None)
                tutorial = new Tutorial(IndexOf(levelName), records.Lessons, records.AddLesson);
            drag = new CarDrag(world, car, camera);

            //Loading takes a while; the game should not try to catch up with the lost time afterwards
            Manager.Game.ResetElapsedTime();
        }

        public override void UnloadContent()
        {
            debugView?.Dispose();
        }

        public override void HandleInput(InputState input, GameTime gameTime)
        {
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

            controls.Update(input, Manager.Viewport, Manager.ControlsScale, elapsed);
            if (input.BackPressed || input.KeyPressed(Keys.P) || controls.PausePressed)
            {
                Pause();
                return;
            }

            developmentKeys.Handle(input, elapsed);

            //The car is held still before the start and rolls out after the finish
            DriveInput drive = race.State == RaceState.Racing
                ? DriveInput.Read(input, controls)
                : new DriveInput(Vector2.Zero, race.State == RaceState.Countdown, false);
            Drive(drive, elapsed * GameSpeed);
            tutorial?.Update(elapsed, race.State == RaceState.Racing,
                new DriveActions(drive.Stick.X > ThrottleThreshold, car.holderRotation, car.brake, car.holderRotLockChnage),
                car.car.LinearVelocity.X);

            //A run in which the car was dragged by hand does not count
            if (drag.Update(input, controls, Manager.Viewport) && race.State != RaceState.Finished)
                race.Assisted = true;
        }

        /// <param name="elapsed">Elapsed game time (see <see cref="GameSpeed"/>).</param>
        private void Drive(DriveInput drive, float elapsed)
        {
            //Throttle, slowly fading out when released
            if (Math.Abs(drive.Stick.X) > ThrottleThreshold)
                car.acceleration = drive.Stick.X * 5f;
            else
                car.acceleration -= Math.Sign(car.acceleration) * 2f * elapsed;

            //Axle rotation
            car.holderRotation = Math.Abs(drive.Stick.Y) > AxlesThreshold;
            if (car.holderRotation)
                car.holderRotationDir = drive.Stick.Y > 0f;

            car.brake = drive.Brake;
            car.holderRotLockChnage = drive.LockAxles;
        }

        public override void Update(GameTime gameTime)
        {
            float realElapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float elapsed = realElapsed * GameSpeed;

            car.Update();
            level.Update();

            //Leaving the game (for another window, or another app on a phone) pauses it
            if (!Manager.Game.IsActive && !IsClosing && Manager.Screens[Manager.Screens.Count - 1] == this)
                Pause();

            //The simulation runs with a fixed time step whatever the frame rate is, and pauses while the screen does
            //not have the focus (window inactive, the pause menu, screen closing); under the results it goes on, the car
            //out of the player's hands (the results get the input)
            bool running = HasFocus || (results != null && Manager.Game.IsActive && !IsClosing && Manager.Screens[Manager.Screens.Count - 1] is ResultsScreen);
            if (running)
                physics.Advance(elapsed);

            //The camera follows the car in game time too, so it keeps up with it, and stands still when the game is paused
            camera.Update(running ? elapsed : 0f, Manager.Viewport, physics.Position(car.car), physics.Rotation(car.car));

            if (HasFocus)
            {
                if (race.Update(realElapsed, physics.Time / GameSpeed, level.IsAtFinish(car.car.Position)))
                    Finish();
                if (race.ResultsDue && results == null)
                    ShowResults();
            }
        }

        private void Finish()
        {
            car.acceleration = 0f;
            previousBest = records.BestTime(levelName);
            record = !race.Assisted && records.Add(levelName, race.Time);
        }

        private void ShowResults()
        {
            string next = NextLevel();
            results = new ResultsScreen(race.Time, race.Par, previousBest, record, !race.Assisted,
                next != null ? () => Start(next) : null, Restart, Close);
            Manager.Add(results);
        }

        /// <summary>
        /// Opens the pause menu. The level stands still while the menu is on top of it
        /// (the simulation runs only while this screen has the focus).
        /// </summary>
        private void Pause()
        {
            Manager.Add(new PauseScreen(Restart, Close, ExitToMainMenu));
        }

        /// <summary>The level from the start: a new game screen in place of this one.</summary>
        private void Restart()
        {
            Start(levelName);
        }

        /// <summary>Another level in place of this one.</summary>
        private void Start(string level)
        {
            Manager.Add(new GameplayScreen(level, settings, records, levels));
            Close();
        }

        /// <summary>The level after this one in the level select; null if this is the last one.</summary>
        private string NextLevel()
        {
            int index = IndexOf(levelName);
            return index >= 0 && index + 1 < levels.Count ? levels[index + 1] : null;
        }

        /// <summary>The position of a level in the level select; -1 if it is not there.</summary>
        private int IndexOf(string level)
        {
            for (int i = 0; i < levels.Count; i++)
                if (levels[i] == level)
                    return i;
            return -1;
        }

        private void ExitToMainMenu()
        {
            foreach (Screen screen in Manager.Screens)
                if (screen is LevelSelectScreen)
                    screen.Close();
            Close();
        }

        public override void Draw(GameTime gameTime)
        {
            renderer.Begin(camera.View, camera.Projection);
            sky.Draw(renderer);
            car.Draw(renderer);
            level.Draw(renderer);
            renderer.End();

            Matrix debugProjection = camera.DebugProjection;
            Matrix debugViewMatrix = camera.DebugView;
            debugView.RenderDebugData(ref debugProjection, ref debugViewMatrix);

            SpriteBatch batch = Manager.SpriteBatch;
            batch.Begin();
            controls.Layout(Manager.Viewport, Manager.ControlsScale);
            controls.Draw(batch, 1f);
            //The time gives way to the title of the results, which show it too
            float hudBottom = RaceHud.Draw(Manager, race, results != null ? 1f - results.Visibility : 1f);
            if (tutorial != null)
                TutorialCard.Draw(Manager, tutorial, controls, hudBottom);
            //Fading in and out through black
            batch.Draw(Manager.Pixel, Manager.Viewport.Bounds, Color.Black * (1f - Visibility));
            batch.End();
        }
    }
}
