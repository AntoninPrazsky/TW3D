using System;
using System.Collections.Generic;
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
    /// the on-screen controls, the pause menu and the physics debug view (F1–F8).
    /// A run of the level starts with a countdown (the car held still); the time runs from the start
    /// until the car reaches the finish block of the level, then the results follow.
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

        //Game seconds per real second. For the physics engine the world is huge (the car is about
        //20 metres long, gravity 12 m/s²), so at real time everything moves as in slow motion.
        //Shrinking the whole world while keeping its look would be the same as running the game faster:
        //everything moves along the same paths, only in a shorter time.
        private const float GameSpeed = 1.5f;

        //The run: the countdown before the start and how long the results wait after the finish (real seconds), how
        //long "Start" shows; the font scales of the countdown, of the time and of the par time below it
        private const float CountdownTime = 3f;
        private const float ResultsDelay = 1.5f;
        private const float StartShown = 0.8f;
        private const float CountdownScale = 2.6f;
        private const float TimeScale = 0.6f;
        private const float ParScale = 0.35f;

        private enum RaceState
        {
            Countdown,
            Racing,
            Finished
        }

        private readonly string levelName;
        private readonly GameSettings settings;
        private readonly GameRecords records;
        private readonly IReadOnlyList<string> levels;

        private RaceState race = RaceState.Countdown;
        private float countdown = CountdownTime;
        //Simulated game time at the start; the time of the run in real seconds; real seconds since the finish
        private double startTime;
        private float raceTime;
        private float sinceFinish;
        private bool resultsShown;
        private ResultsScreen results;
        //The best time before the run, and whether the run became the best one
        private float? previousBest;
        private bool record;
        //The car was moved by hand during the run, which then does not count
        private bool assisted;

        private World world;
        private PhysicsClock physics;
        private ChaseCamera camera;
        private SceneRenderer renderer;
        private LevelMood mood;
        private DebugView debugView;
        private OnScreenControls controls;

        private Level level;
        private Sky sky;
        private Car car;

        private FixedMouseJoint dragJoint;
        private int dragPointerId;

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
            renderer = new SceneRenderer(Manager.GraphicsDevice, Manager.Content);
            settings.Apply(renderer);

            level = new Level(world, Manager, camera, physics, catalog, Vector2.Zero, levelName);
            mood = level.Mood;
            renderer.Lighting = Lighting.For(mood);
            sky = new Sky(Manager, camera);
            CarType carType = catalog.FindCar(settings.Car);
            CarWheels wheels = settings.WheelsOf(carType);
            car = new Car(world, Manager, camera, physics, new Vector2(-1.6f, -7f), carType, catalog.FindWheel(wheels.Front), catalog.FindWheel(wheels.Rear));

            debugView = new DebugView(world)
            {
                DefaultShapeColor = Color.White,
                SleepingShapeColor = Color.LightGray,
                Flags = DebugViewFlags.None
            };
            debugView.LoadContent(Manager.GraphicsDevice, Manager.Content);

            controls = new OnScreenControls();
            controls.LoadContent(Manager.Content, Manager.Pixel);

            //Loading takes a while; the game should not try to catch up with the lost time afterwards
            Manager.Game.ResetElapsedTime();
        }

        public override void UnloadContent()
        {
            debugView?.Dispose();
        }

        /// <summary>
        /// Loads the catalog, the models of all its blocks and objects and the collision shapes
        /// (shared by all levels, loaded only once).
        /// </summary>
        private static void LoadCatalog(ContentManager content)
        {
            if (catalog != null)
                return;

            using (Stream stream = TitleContainer.OpenStream("Content/" + Catalog.FileName))
                catalog = Catalog.Load(stream);
            ModelCollision.Load();

            foreach (BlockType block in catalog.Blocks)
                TrackBlockModels.Models.Add(block.Name, content.Load<Model>(block.ModelAsset));
            foreach (string model in catalog.ObjectModels)
                TrackPropModels.Models.Add(model, content.Load<Model>(ObjectType.ModelAsset(model)));
        }

        public override void HandleInput(InputState input, GameTime gameTime)
        {
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

            controls.Update(input, Manager.Viewport, Manager.ControlsScale);
            if (input.BackPressed || input.KeyPressed(Keys.P) || controls.PausePressed)
            {
                Pause();
                return;
            }

            HandleDebugKeys(input);
            HandleCameraKeys(input, elapsed);

            //The car is held still before the start and rolls out after the finish
            if (race == RaceState.Racing)
                Drive(DriveInput.Read(input, controls), elapsed * GameSpeed);
            else
                Drive(new DriveInput(Vector2.Zero, race == RaceState.Countdown, false), elapsed * GameSpeed);
            HandleCarDragging(input);
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
            Manager.Add(new GameplayScreen(levelName, settings, records, levels));
            Close();
        }

        /// <summary>The level after this one in the level select; null if this is the last one.</summary>
        private string NextLevel()
        {
            int index = -1;
            for (int i = 0; i < levels.Count; i++)
                if (levels[i] == levelName)
                    index = i;
            return index >= 0 && index + 1 < levels.Count ? levels[index + 1] : null;
        }

        private void ExitToMainMenu()
        {
            foreach (Screen screen in Manager.Screens)
                if (screen is LevelSelectScreen)
                    screen.Close();
            Close();
        }

        /// <param name="elapsed">Elapsed game time (see <see cref="GameSpeed"/>).</param>
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
                        if (race != RaceState.Finished)
                            assisted = true;
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

            //The lighting of the 2016 version, and the scene without shadows or ambient occlusion, for comparison
            //(only for this level; the Options menu sets them for good)
            if (input.KeyPressed(Keys.F9))
                renderer.Classic = !renderer.Classic;
            if (input.KeyPressed(Keys.F10))
                renderer.Shadows = !renderer.Shadows;
            if (input.KeyPressed(Keys.F11))
                renderer.AmbientOcclusion = !renderer.AmbientOcclusion;

            //The moods of the levels, to try them on any level
            if (input.KeyPressed(Keys.M))
            {
                mood = (LevelMood)(((int)mood + 1) % 3);
                renderer.Lighting = Lighting.For(mood);
            }
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
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds * GameSpeed;

            car.Update();
            level.Update();

            //Leaving the game (for another window, or another app on a phone) pauses it
            if (!Manager.Game.IsActive && !IsClosing && Manager.Screens[Manager.Screens.Count - 1] == this)
                Pause();

            //The simulation runs with a fixed time step whatever the frame rate is, and pauses while the screen does
            //not have the focus (window inactive, the pause menu, screen closing); under the results it goes on, the car
            //out of the player's hands (the results get the input)
            bool running = HasFocus || (resultsShown && Manager.Game.IsActive && !IsClosing && Manager.Screens[Manager.Screens.Count - 1] is ResultsScreen);
            if (running)
                physics.Advance(elapsed);

            //The camera follows the car in game time too, so it keeps up with it, and stands still when the game is paused
            camera.Update(running ? elapsed : 0f, Manager.Viewport, physics.Position(car.car), physics.Rotation(car.car));

            if (HasFocus)
                UpdateRace((float)gameTime.ElapsedGameTime.TotalSeconds);
        }

        /// <summary>The countdown, the time of the run and the finish.</summary>
        /// <param name="elapsed">Elapsed real time.</param>
        private void UpdateRace(float elapsed)
        {
            switch (race)
            {
                case RaceState.Countdown:
                    countdown -= elapsed;
                    if (countdown <= 0f)
                    {
                        race = RaceState.Racing;
                        startTime = physics.Time;
                    }
                    break;

                case RaceState.Racing:
                    //The time of the simulation, not of the frames: the run is timed fairly even when frames are slow
                    raceTime = (float)((physics.Time - startTime) / GameSpeed);
                    if (level.FinishArea is (Vector2 min, Vector2 max))
                    {
                        Vector2 position = car.car.Position;
                        if (position.X >= min.X && position.X <= max.X && position.Y >= min.Y && position.Y <= max.Y)
                            Finish();
                    }
                    break;

                case RaceState.Finished:
                    sinceFinish += elapsed;
                    if (sinceFinish >= ResultsDelay && !resultsShown)
                    {
                        resultsShown = true;
                        string next = NextLevel();
                        results = new ResultsScreen(raceTime, level.Par, previousBest, record, !assisted,
                            next != null ? () => { Manager.Add(new GameplayScreen(next, settings, records, levels)); Close(); } : null,
                            Restart, Close);
                        Manager.Add(results);
                    }
                    break;
            }
        }

        private void Finish()
        {
            race = RaceState.Finished;
            car.acceleration = 0f;
            previousBest = records.BestTime(levelName);
            record = !assisted && records.Add(levelName, raceTime);
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
            DrawRace(batch);
            //Fading in and out through black
            batch.Draw(Manager.Pixel, Manager.Viewport.Bounds, Color.Black * (1f - Visibility));
            batch.End();
        }

        /// <summary>The time of the run at the top, the countdown and the start and the finish in the middle of the screen.</summary>
        private void DrawRace(SpriteBatch batch)
        {
            SpriteFont font = Manager.Fonts.Menu;
            float uiScale = Manager.UiScale;
            Rectangle safe = Manager.SafeArea;
            float centreX = Manager.Viewport.Width / 2f;

            //The time gives way to the title of the results, which show it too
            float alpha = results != null ? 1f - results.Visibility : 1f;
            string time = RaceScore.FormatTime(race == RaceState.Countdown ? 0f : raceTime);
            Color timeColour = race == RaceState.Finished ? MainMenuScreen.Yellow : Color.White;
            Vector2 timeSize = DrawTime(font, time, new Vector2(centreX, safe.Top), timeColour * alpha, TimeScale * uiScale, alpha);
            if (level.FinishArea.HasValue)
            {
                string par = "Par " + RaceScore.FormatTime(level.Par);
                Vector2 parSize = font.MeasureString(par) * ParScale * uiScale;
                Manager.DrawShadowedText(font, par, new Vector2(centreX - parSize.X / 2f, safe.Top + timeSize.Y), Color.LightGray * alpha, Color.Black * alpha, ParScale * uiScale);
            }

            //3, 2, 1: each number appears large and shrinks a little; then Start, fading out; at the finish Finish
            if (race == RaceState.Countdown)
            {
                int number = (int)MathF.Ceiling(countdown);
                float age = number - countdown;
                DrawBig(batch, number.ToString(), Color.White, 1f + 0.3f * MathF.Max(0f, 1f - age * 4f), MathHelper.Clamp((1f - age) * 4f, 0f, 1f));
            }
            else if (race == RaceState.Racing && raceTime < StartShown)
                DrawBig(batch, "Start", MainMenuScreen.Yellow, 1f, 1f - raceTime / StartShown);
            else if (race == RaceState.Finished && !resultsShown)
                DrawBig(batch, "Finish", MainMenuScreen.Yellow, 1f, 1f);
        }

        /// <summary>
        /// Draws a time centred under a point, its digits in cells of the same width, so that it stands still as the
        /// digits change (the digits of the font are not equally wide). Returns its size.
        /// </summary>
        private Vector2 DrawTime(SpriteFont font, string text, Vector2 topCentre, Color colour, float scale, float alpha)
        {
            float digitWidth = 0f;
            for (char digit = '0'; digit <= '9'; digit++)
                digitWidth = Math.Max(digitWidth, font.MeasureString(digit.ToString()).X);

            float width = 0f;
            foreach (char c in text)
                width += char.IsDigit(c) ? digitWidth : font.MeasureString(c.ToString()).X;

            float x = topCentre.X - width * scale / 2f;
            foreach (char c in text)
            {
                string glyph = c.ToString();
                float glyphWidth = font.MeasureString(glyph).X;
                float cell = char.IsDigit(c) ? digitWidth : glyphWidth;
                Manager.DrawShadowedText(font, glyph, new Vector2(x + (cell - glyphWidth) * scale / 2f, topCentre.Y), colour, Color.Black * alpha, scale);
                x += cell * scale;
            }
            return new Vector2(width, font.LineSpacing) * scale;
        }

        private void DrawBig(SpriteBatch batch, string text, Color colour, float size, float alpha)
        {
            SpriteFont font = Manager.Fonts.Menu;
            float scale = CountdownScale * size * Manager.UiScale;
            Vector2 textSize = font.MeasureString(text) * scale;
            Vector2 position = new Vector2(Manager.Viewport.Width, Manager.Viewport.Height) / 2f - textSize / 2f;
            Manager.DrawShadowedText(font, text, position, colour * alpha, Color.Black * alpha, scale);
        }
    }
}
