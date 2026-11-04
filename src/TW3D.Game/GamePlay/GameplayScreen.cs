using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using nkast.Aether.Physics2D;
using nkast.Aether.Physics2D.Diagnostics;
using nkast.Aether.Physics2D.Dynamics;
using TW3D.Audio;
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
    /// level, then the car brakes and the results follow. A car fallen off the level loses the run: game over.
    /// A test drive of the level editor (see <see cref="TestDrive"/>) plays the level being edited, from any cell, with no
    /// tutorial, no records and no next level; it leaves for the editor, which waits under it as it was.
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
        //What the message about the problems of a level from the editor calls it
        private const string TestDriveName = "test drive";
        //The item of the pause menu and the button of the results that end a test drive
        private const string BackToEditorText = "Back to editor";

        //The name of the level, the records and the levels of the level select; null in a test drive
        private readonly string levelName;
        private readonly GameSettings settings;
        private readonly GameRecords records;
        private readonly IReadOnlyList<string> levels;
        //A test drive: the level being edited, the cell the car starts at and what brings the editor back; the level is
        //null in the game
        private readonly LevelData testLevel;
        private readonly int startX, startY;
        private readonly Action back;

        private World world;
        private PhysicsClock physics;
        private ChaseCamera camera;
        private SceneRenderer renderer;
        private DebugView debugView;
        private OnScreenControls controls;
        private Speedometer speedometer;
        private DevelopmentKeys developmentKeys;
        private CarDrag drag;

        private Level level;
        private Car car;
        private Race race;
        //The cards of the tutorial; null when the tutorial is off
        private Tutorial tutorial;
        //How many columns of the start lights were on when the countdown last beeped
        private int countdownBeeped;
        //Where the car started (physics x) and how far of the way to the start gate it got (0 to 1): the camera comes back
        //from framing the gate as it does (see StartFraming)
        private float framingFrom;
        private float framingProgress;
        //The sounds of the car and of things hitting each other, and how far the throttle is held (0..1)
        private CarSounds carSounds;
        private ImpactSounds impacts;
        private float throttleHeld;
        //The marks and the smoke of the tyres, and the wheels slipping now (sliding braked or spinning faster than this
        //over what they stand on, physics units a second)
        private const float SlipSpeed = 8f;
        private SkidMarks skidMarks;
        private TyreSmoke smoke;
        private ExhaustSmoke exhaust;
        //Where the camera looks (physics units) and how the car it follows is turned (radians)
        private Vector2 followed;
        private float followedRotation;
        private readonly List<WheelSlip> slipping = new List<WheelSlip>();
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

        private GameplayScreen(LevelData level, int startX, int startY, GameSettings settings, Action back)
        {
            testLevel = level;
            this.startX = startX;
            this.startY = startY;
            this.settings = settings;
            this.back = back;
            //The editor waits under this screen as it was: it is back the moment the test drive closes
            FadeInTime = TimeSpan.FromSeconds(0.25);
            FadeOutTime = TimeSpan.Zero;
            IsOpaque = true;
        }

        /// <summary>
        /// A test drive of the level editor: the level being edited, in memory, with the car and the wheels of the garage,
        /// starting at a cell. Nothing of the game's own is touched (no tutorial, no records); the pause menu and the
        /// results offer Restart and Back to editor, Esc goes back at once.
        /// </summary>
        /// <param name="level">The level being edited; it must be valid (see <see cref="LevelData.Validate"/>).</param>
        /// <param name="startX">The cell the car starts at; [0, 0] is the start of the level.</param>
        /// <param name="back">Brings the editor back; called as the test drive closes for the editor (not when it restarts).</param>
        public static GameplayScreen TestDrive(LevelData level, int startX, int startY, GameSettings settings, Action back)
        {
            return new GameplayScreen(level, startX, startY, settings, back);
        }

        //How long after the start the camera takes to come back to the car when the start gate stands where the car starts
        //(seconds); farther, it comes back as the car drives to the gate
        private const float FramingReturn = 2.2f;

        /// <summary>
        /// How much the camera frames the signal lamps of the start gate: fully during the countdown, then less and less as
        /// the car drives to the gate, smoothly, until it follows the car alone once the car is under the lamps. Driving
        /// back does not bring the framing back.
        /// </summary>
        private float StartFraming()
        {
            if (!(camera.Framed is Vector2 signals) || race.State == RaceState.Finished || race.State == RaceState.Lost)
                return 0f;
            if (race.State == RaceState.Countdown)
                return 1f;
            float way = signals.X - framingFrom;
            float progress = Math.Abs(way) < Units.GridCell ? race.Time / FramingReturn : (followed.X - framingFrom) / way;
            framingProgress = Math.Max(framingProgress, MathHelper.Clamp(progress, 0f, 1f));
            float t = framingProgress;
            return 1f - t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Where the car starts at a cell (physics units): as at the start of the level, moved by the cell and down by the
        /// shift of the block of the middle layer there (the later one of two in a cell, as the editor shows it).
        /// </summary>
        private static Vector2 StartAt(LevelData level, int x, int y)
        {
            int shift = 0;
            foreach (BlockPlacement placement in level.Middle)
                if (placement.X == x && placement.Y == y)
                    shift = placement.Shift;
            return CarStart + new Vector2(x * Units.GridCell, y * Units.GridCell + shift * Units.ShiftStep);
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
            renderer = new SceneRenderer(Manager.GraphicsDevice, Manager.Content) { DeepShadows = true };
            settings.Apply(renderer);

            level = testLevel != null
                ? new Level(world, Manager, camera, physics, assets, Vector2.Zero, testLevel, TestDriveName)
                : new Level(world, Manager, camera, physics, assets, Vector2.Zero, levelName);
            renderer.Lighting = Lighting.For(level.Mood);
            renderer.HorizonDip = SceneRenderer.HighAbove;
            Catalog catalog = assets.Catalog;
            CarType carType = catalog.FindCar(settings.Car);
            CarWheels wheels = settings.WheelsOf(carType);
            Vector2 carStart = testLevel != null ? StartAt(testLevel, startX, startY) : CarStart;
            car = new Car(world, Manager, camera, physics, carStart, carType, catalog.FindWheel(wheels.Front), catalog.FindWheel(wheels.Rear));
            //During the countdown the camera looks at the signal lamps of the start gate and the car from the side; it
            //starts there, as a test drive may start anywhere in the level and the camera would fly there from the origin
            camera.Framed = level.StartSignals(carStart);
            camera.Framing = 1f;
            camera.JumpTo(carStart);
            framingFrom = carStart.X;
            race = new Race(level.Par, level.HasFinish);

            debugView = new DebugView(world)
            {
                DefaultShapeColor = Color.White,
                SleepingShapeColor = Color.LightGray,
                Flags = DebugViewFlags.None
            };
            debugView.LoadContent(Manager.GraphicsDevice, Manager.Content);

            //A computer is driven with the keyboard, a phone by touch, until the player clicks or touches the level
            Manager.Input.UseDefaultDevice();
            controls = new OnScreenControls();
            controls.LoadContent(Manager.Content, Manager.Pixel);
            speedometer = new Speedometer(Manager.GraphicsDevice);
            developmentKeys = new DevelopmentKeys(debugView, renderer, camera, level.Mood);
            if (testLevel == null && (settings.Tutorial || Tutorial.Lever != TutorialLever.None))
                tutorial = new Tutorial(IndexOf(levelName), records.Lessons, records.AddLesson);
            drag = new CarDrag(world, car, camera);
            carSounds = new CarSounds(Manager.Sounds, car);
            skidMarks = new SkidMarks();
            smoke = new TyreSmoke();
            exhaust = new ExhaustSmoke();
            impacts = new ImpactSounds(world, Manager.Sounds);

            //Loading takes a while; the game should not try to catch up with the lost time afterwards
            Manager.Game.ResetElapsedTime();
        }

        public override void UnloadContent()
        {
            debugView?.Dispose();
            carSounds?.Dispose();
            impacts?.Dispose();
            //Every level and every restart makes its own renderer, with a shadow map and render targets of the size of the
            //screen: they are freed with the screen, not left for the finalizer (a test drive of the editor is started often)
            renderer?.Dispose();
            speedometer?.Dispose();
        }

        public override void HandleInput(InputState input, GameTime gameTime)
        {
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

            controls.Update(input, Manager.Viewport, Manager.ControlsScale, elapsed);
            if (testLevel != null && input.BackPressed)
            {
                BackToEditor();
                return;
            }
            if (input.BackPressed || input.KeyPressed(Keys.P) || controls.PausePressed)
            {
                Pause();
                return;
            }

            developmentKeys.Handle(input, elapsed);

            //The car is held still before the start and brakes after the finish
            DriveInput drive = race.State == RaceState.Racing
                ? DriveInput.Read(input, controls)
                : new DriveInput(Vector2.Zero, race.State is RaceState.Countdown or RaceState.Finished, false);
            Drive(drive, elapsed * GameSpeed);
            throttleHeld = Math.Abs(drive.Stick.X) > ThrottleThreshold ? Math.Min(1f, Math.Abs(drive.Stick.X)) : 0f;
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
            if (running)
            {
                car.SlippingWheels(slipping, SlipSpeed);
                skidMarks.Update(realElapsed, car, slipping);
                smoke.Update(realElapsed, car, slipping);
                exhaust.Update(realElapsed, car, throttleHeld);
                impacts.Update(realElapsed);
                speedometer.Update(Speedometer.Kmh(car.Speed, GameSpeed), realElapsed);
            }
            //A car fallen off the level is gone: it is not heard any more
            carSounds.Update(realElapsed, running && race.State != RaceState.Lost, throttleHeld, car.brake, car.holderRotLockChnage, car.holderRotation, running ? slipping : null);

            //The camera follows the car in game time too, so it keeps up with it, and stands still when the game is
            //paused. It does not follow the car below the level: a car falling off it drops out of sight, and is left
            //there when the run is lost
            if (race.State != RaceState.Lost)
            {
                followed = physics.Position(car.car);
                followed.Y = Math.Min(followed.Y, level.Bottom);
                followedRotation = physics.Rotation(car.car);
            }
            camera.Framing = StartFraming();
            camera.Update(running ? elapsed : 0f, Manager.Viewport, followed, followedRotation);

            if (HasFocus)
            {
                RaceState before = race.State;
                bool belowLevel = car.car.Position.Y > level.Bottom && !drag.Holding;
                if (race.Update(realElapsed, physics.Time / GameSpeed, level.IsAtFinish(car.car.Position), belowLevel))
                {
                    if (race.State == RaceState.Finished)
                        Finish();
                    else
                        GameOver();
                }
                BeepCountdown(before);
                if (race.ResultsDue && results == null)
                    ShowResults();
            }
        }

        /// <summary>Beeps each column of the start lights as it comes on, and the start (lights out) an octave higher.</summary>
        private void BeepCountdown(RaceState before)
        {
            if (race.State == RaceState.Countdown)
            {
                int lit = race.RedSignals;
                if (lit != countdownBeeped)
                {
                    countdownBeeped = lit;
                    Manager.Sounds.Play(Sound.Countdown);
                }
            }
            else if (before == RaceState.Countdown)
                Manager.Sounds.Play(Sound.Start);
        }

        private void Finish()
        {
            Manager.Sounds.Play(Sound.Finish);
            car.acceleration = 0f;
            if (records == null)
                return;
            previousBest = records.BestTime(levelName);
            record = !race.Assisted && records.Add(levelName, race.Time);
        }

        /// <summary>The car fell off the level: the run is lost.</summary>
        private void GameOver()
        {
            Manager.Sounds.Play(Sound.GameOver);
            car.acceleration = 0f;
            if (records != null)
                previousBest = records.BestTime(levelName);
        }

        private void ShowResults()
        {
            if (testLevel != null)
                results = race.State == RaceState.Lost
                    ? new ResultsScreen(previousBest, null, Restart, BackToEditor, BackToEditorText)
                    : new ResultsScreen(race.Time, race.Par, previousBest, record, !race.Assisted, null, Restart, BackToEditor, BackToEditorText);
            else
            {
                string next = NextLevel();
                Action nextLevel = next != null ? () => Start(next) : null;
                results = race.State == RaceState.Lost
                    ? new ResultsScreen(previousBest, nextLevel, Restart, Close)
                    : new ResultsScreen(race.Time, race.Par, previousBest, record, !race.Assisted, nextLevel, Restart, Close);
            }
            Manager.Add(results);
        }

        /// <summary>
        /// Opens the pause menu. The level stands still while the menu is on top of it
        /// (the simulation runs only while this screen has the focus).
        /// </summary>
        private void Pause()
        {
            if (testLevel != null)
                Manager.Add(new PauseScreen(Restart, BackToEditor, null, BackToEditorText));
            else
                Manager.Add(new PauseScreen(Restart, Close, ExitToMainMenu));
        }

        /// <summary>The level from the start: a new game screen in place of this one (a test drive of the same level from the same cell).</summary>
        private void Restart()
        {
            if (testLevel != null)
            {
                Manager.Add(TestDrive(testLevel, startX, startY, settings, back));
                Close();
            }
            else
                Start(levelName);
        }

        /// <summary>Ends a test drive: the editor under it is back.</summary>
        private void BackToEditor()
        {
            back();
            Close();
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
            //The previews of the levels are taken of the track alone, at the full resolution whatever the Options say
            renderer.MayDrawSmaller = !developmentKeys.TrackOnly;
            renderer.Begin(camera.View, camera.Projection);
            renderer.SetSky();
            //The start lights of the start gate count the start down, as those of the display of the run do
            renderer.StartLights = race.RedSignals;
            if (!developmentKeys.TrackOnly)
                car.Draw(renderer);
            level.Draw(renderer);
            skidMarks.Draw(renderer);
            smoke.Draw(renderer);
            if (!developmentKeys.TrackOnly)
                exhaust.Draw(renderer);
            renderer.End();

            Matrix debugProjection = camera.DebugProjection;
            Matrix debugViewMatrix = camera.DebugView;
            debugView.RenderDebugData(ref debugProjection, ref debugViewMatrix);

            SpriteBatch batch = Manager.SpriteBatch;
            batch.Begin();
            if (!developmentKeys.TrackOnly)
            {
                controls.Layout(Manager.Viewport, Manager.ControlsScale);
                controls.Draw(batch, 1f);
                //The time gives way to the title of the results, which show it too
                float hudBottom = RaceHud.Draw(Manager, race, results != null ? 1f - results.Visibility : 1f);
                speedometer.Draw(Manager, results != null ? 1f - results.Visibility : 1f, controls.JoystickTop);
                if (tutorial != null)
                    TutorialCard.Draw(Manager, tutorial, controls, hudBottom);
            }
            //Fading in and out through black
            batch.Draw(Manager.Pixel, Manager.Viewport.Bounds, Color.Black * (1f - Visibility));
            batch.End();
        }
    }
}
