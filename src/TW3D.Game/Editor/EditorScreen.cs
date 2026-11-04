using System;
using System.IO;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TW3D.GameObjects;
using TW3D.GamePlay;
using TW3D.Graphics;
using TW3D.Input;
using TW3D.Screens;
using TW3D.Shared;

namespace TW3D.Editor
{
    /// <summary>
    /// The level editor: the level being edited (an <see cref="EditedLevel"/>, the document with undo, which the
    /// <see cref="LevelSession"/> opens, saves and guards with "Save changes?") drawn as in the game, seen by a free camera
    /// (<see cref="EditorCamera"/>), edited with the active tool (the <see cref="BlockTool"/> places blocks, the
    /// <see cref="SelectTool"/> works on many of them at once, the <see cref="ObjectTool"/> places the dynamic objects, the
    /// <see cref="SceneryTool"/> the props of the scenery; B, V, O and P choose) whose cursor and highlights the
    /// <see cref="EditorMarkers"/> show, with the user interface of
    /// Dear ImGui over it (the menu bar with its <see cref="EditorMenus"/>, the <see cref="StatusBar"/> and the windows:
    /// the <see cref="PaletteWindow"/> with the <see cref="BlockPreviews"/>, the <see cref="InspectorWindow"/>, the
    /// <see cref="LevelWindow"/> with the <see cref="LevelReport"/> of its problems, the <see cref="HelpWindow"/>, the
    /// <see cref="FileDialogs"/> of the session). The screen builds the parts, routes the input to the camera and the active tool and draws them in their order. Opened
    /// by <c>--editor [level]</c>, alone on the screen stack (leaving it quits), or from the main menu of a Debug build
    /// (leaving it goes back to the menu). The scene gets the mouse and the keys only when the interface does not want
    /// them (the cursor over a window, a text field being typed in) and no dialog is open. F5 drives the level in the
    /// game: a test drive of the <see cref="GameplayScreen"/> covers the editor, which waits under it as it is.
    /// </summary>
    public sealed class EditorScreen : Screen
    {
        //Where ImGui keeps the positions and sizes of its windows
        private static readonly string LayoutFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TW3D", "editor-layout.ini");
        //How far the camera of the game is from the car (scene units): the haze of the renderer is set for that distance,
        //so it is moved by what the camera of the editor is further
        private const float GameCameraDistance = 4.5f;
        //How far from the camera the shadows reach, at least (see SceneRenderer.ShadowDistance)
        private const float MinShadowDistance = 40f;
        private readonly GameSettings settings;
        private readonly string levelArgument;
        private readonly string levelsArgument;
        //Opened from the command line, alone: leaving quits the game; otherwise leaving goes back to the menu
        private readonly bool standalone;
        //The title of the window before the editor, put back when it leaves
        private string previousTitle;

        private LevelAssets assets;
        private SceneRenderer renderer;
        private EditorCamera camera;
        private LevelView view;
        private BlockTool placeTool;
        private ObjectTool objectTool;
        private SelectTool selectTool;
        private SceneryTool sceneryTool;
        private EditorTool activeTool = EditorTool.Place;
        private EditorMarkers markers;
        private ImGuiRenderer imgui;
        private BlockPreviews previews;
        private PaletteWindow palette;
        private readonly InspectorWindow inspector = new InspectorWindow();
        private readonly LevelWindow levelWindow = new LevelWindow();
        private readonly LevelReport levelReport = new LevelReport();
        private HiddenLayerGuard hiddenGuard;
        private LevelSession session;
        private InterfaceLayout layout;
        //The size of a preview of the palette in pixels, scaled with the window
        private int previewSize;
        private bool showPalette = true, showInspector = true, showLevel = true, showHelp, showDemo;
        private Layer layer = Layer.Middle;
        //What the status bar says about the last thing done
        private string message = "";
        //Tells the status bar; one delegate for the tools and the menus, which would make one every frame
        private Action<string> report;
        //The window or the screen had no focus (another window, a test drive): the click that brings it back is not an edit
        private bool focusReturning;
        //A dialog was open in the last frame: the keys held when it closed answered it (D of Don't save would fly the
        //camera), so the scene gets no keys until they are released; so do the keys held when the focus comes back (the
        //arrows that drove the car of a test drive would move the cursor)
        private bool dialogWasOpen;
        private Keys[] heldKeys = Array.Empty<Keys>();
        //Right Ctrl went down and nothing else has happened since (see RightControlTapped)
        private bool rightControlAlone;
        //A test drive covers the editor; the cell the menu asked to start one from, started with the next input, not in
        //the middle of drawing the interface
        private bool driving;
        private Cell? driveRequested;

        /// <param name="level">A level name (a file of the levels folder without the extension) or a level file; null = the last level, or a new one.</param>
        /// <param name="standalone">The editor is alone (opened by --editor): leaving it quits the game instead of going back to the menu.</param>
        /// <param name="levels">The levels folder given by --levels; null = the repository's content/Levels or the application data (see <see cref="LevelFolder"/>).</param>
        public EditorScreen(GameSettings settings, string level, bool standalone, string levels = null)
        {
            this.settings = settings;
            levelArgument = level;
            levelsArgument = levels;
            this.standalone = standalone;
            FadeInTime = TimeSpan.Zero;
            FadeOutTime = TimeSpan.Zero;
            IsOpaque = true;
        }

        private EditedLevel Level => session.Level;

        public override void LoadContent()
        {
            ShowImmediately();
            Manager.PointersVisible = false;
            previousTitle = Manager.Game.Window.Title;

            assets = LevelAssets.Load(Manager.Content);
            //The markers are drawn over the scene with its depth buffer, so the scene is drawn on the screen itself
            renderer = new SceneRenderer(Manager.GraphicsDevice, Manager.Content) { HorizonDip = SceneRenderer.HighAbove, MayDrawSmaller = false };
            settings.Apply(renderer);
            camera = new EditorCamera();
            view = new LevelView(assets);
            report = note => message = note;
            placeTool = new BlockTool(camera, report);
            objectTool = new ObjectTool(camera, placeTool, assets, report);
            sceneryTool = new SceneryTool(camera, placeTool, assets, report);
            selectTool = new SelectTool(camera, placeTool, sceneryTool.Bounds, report);
            hiddenGuard = new HiddenLayerGuard(placeTool, report);
            markers = new EditorMarkers(Manager.GraphicsDevice, Manager.Content);

            imgui = new ImGuiRenderer(Manager.Game, LayoutFile);
            ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            layout = new InterfaceLayout(imgui, Manager.Content.RootDirectory);
            previews = new BlockPreviews(Manager.GraphicsDevice, assets, imgui);
            palette = new PaletteWindow(previews, assets.Catalog, SetTool);

            session = new LevelSession(Manager.Game, Manager.GraphicsDevice, imgui, assets.Catalog, levelsArgument,
                CaptureCamera, EndStrokes, Show, report);
            //The close button of the window asks too when there are unsaved changes
            Manager.Game.Exiting += OnExiting;

            session.OpenFirst(levelArgument);
        }

        public override void UnloadContent()
        {
            Manager.Game.Exiting -= OnExiting;
            EndStrokes();
            session.Dispose();
            previews.Dispose();
            renderer.Dispose();
            imgui.Dispose();
            Manager.PointersVisible = true;
            Manager.Game.Window.Title = previousTitle;
        }

        /// <summary>The session made a level the one edited: the view, the tool and the camera (where it was, or framing the level).</summary>
        private void Show(EditedLevel opened, CameraView saved)
        {
            view.Level = opened;
            placeTool.Level = opened;
            objectTool.Level = opened;
            selectTool.Level = opened;
            sceneryTool.Level = opened;
            if (saved != null)
            {
                camera.Target = new Vector3(saved.TargetX, saved.TargetY, saved.TargetZ);
                camera.Yaw = saved.Yaw;
                camera.Pitch = saved.Pitch;
                camera.Distance = saved.Distance;
            }
            else
                FrameLevel();
        }

        /// <summary>Where the camera is now, for the session to remember with the last level (<see cref="Show"/> puts it back).</summary>
        private CameraView CaptureCamera() => new CameraView
        {
            TargetX = camera.Target.X, TargetY = camera.Target.Y, TargetZ = camera.Target.Z,
            Yaw = camera.Yaw, Pitch = camera.Pitch, Distance = camera.Distance
        };

        /// <summary>The game is closing (the close button of the window, or the editor's own exit): the session decides whether it may.</summary>
        private void OnExiting(object sender, ExitingEventArgs e)
        {
            if (session.LetExit())
                return;
            e.Cancel = true;
            //The editor asks "Save changes?" in its own interface, which a test drive covers
            if (driving)
                CloseTestDrive();
        }

        /// <summary>Drives the level being edited in the game from a cell (see <see cref="TestDriveStart"/>); a level the game cannot build is refused.</summary>
        private void TestDrive(Cell start)
        {
            EndStrokes();
            GameplayScreen drive = TestDriveStart.Create(Level, assets.Catalog, start, settings, TestDriveEnded, out message);
            if (drive == null)
                return;
            driving = true;
            //The game shows where the mouse is pressed; the editor does not, it has its cursor
            Manager.PointersVisible = true;
            Manager.Add(drive);
        }

        /// <summary>The test drive closed (or was closed): the editor is back as it was.</summary>
        private void TestDriveEnded()
        {
            driving = false;
            Manager.PointersVisible = false;
            //The button that chose Back to editor may still be down, over a button of the palette or the menu bar, and
            //the interface did not see the frames of the drive
            imgui.ResyncInput();
        }

        /// <summary>Closes the test drive with its pause menu or results: the editor is seen again.</summary>
        private void CloseTestDrive()
        {
            bool above = false;
            foreach (Screen screen in Manager.Screens)
            {
                if (above)
                    screen.Close();
                else if (screen == this)
                    above = true;
            }
            TestDriveEnded();
        }

        /// <summary>The camera sees the whole level.</summary>
        private void FrameLevel()
        {
            BoundingBox? bounds = view.SceneBounds();
            if (bounds.HasValue)
                camera.Frame(bounds.Value, Manager.Viewport.AspectRatio);
            else
                camera.Reset();
        }

        /// <summary>Leaves the editor: quits the game when the editor is alone, otherwise goes back to the menu.</summary>
        private void Leave()
        {
            session.MarkLeaving();
            if (standalone)
                Manager.Game.Exit();
            else
                Close();
        }

        /// <summary>The tool that gets the keys and the mouse of the scene.</summary>
        private IEditorTool ActiveTool => activeTool switch
        {
            EditorTool.Object => objectTool,
            EditorTool.Select => selectTool,
            EditorTool.Scenery => sceneryTool,
            _ => placeTool
        };

        /// <summary>
        /// The depth of the plane the cursor cell is on (physics units): of the active layer, of the middle one for the
        /// Object tool (the objects are there), the chosen depth for the Scenery tool.
        /// </summary>
        private float CursorDepth => activeTool switch
        {
            EditorTool.Object => 0f,
            EditorTool.Scenery => sceneryTool.Depth,
            _ => LevelView.Depth(layer)
        };

        /// <summary>Makes a tool the active one (its key, the View > Tool menu, a choice in the palette); a drag of the other ends.</summary>
        private void SetTool(EditorTool value)
        {
            //The key of the active tool pressed in the middle of a drag leaves it going
            if (value == activeTool)
                return;
            EndStrokes();
            activeTool = value;
        }

        /// <summary>Ends a drag of any tool, closing its step of undo: before the level is saved, dropped or driven, and when the focus goes.</summary>
        private void EndStrokes()
        {
            placeTool.EndStroke();
            objectTool.EndStroke();
            selectTool.EndStroke();
            sceneryTool.EndStroke();
        }

        private void CycleMood()
        {
            Level.SetMood((LevelMood)(((int)Level.Mood + 1) % 3));
        }

        public override void Update(GameTime gameTime)
        {
            //Without the focus the screen gets no input, so it would not see the button go up: a stroke ends here
            if (!HasFocus)
            {
                EndStrokes();
                focusReturning = true;
                driveRequested = null;
            }
            session.UpdateTitle();
            //The selection is of the layer it was made in, whichever way the layer was switched, and of what is seen
            selectTool.Sync(layer);
            selectTool.Scenery = view.ShowScenery;
        }

        public override void HandleInput(InputState input, GameTime gameTime)
        {
            //A drive the Level menu asked for starts before the keys of this frame, which would change the level after
            //the drive took it
            if (driveRequested.HasValue)
            {
                Cell start = driveRequested.Value;
                driveRequested = null;
                if (!session.IsDialogOpen)
                {
                    TestDrive(start);
                    return;
                }
            }
            //A dialog has the keys and the mouse; the key that closes it is not seen again here, as it was pressed before
            if (session.IsDialogOpen)
            {
                EndStrokes();
                dialogWasOpen = true;
                return;
            }
            if (dialogWasOpen || focusReturning)
            {
                dialogWasOpen = false;
                heldKeys = input.Keyboard.GetPressedKeys();
            }
            bool keysHeld = Array.Exists(heldKeys, key => input.IsKeyDown(key));
            if (!keysHeld)
                heldKeys = Array.Empty<Keys>();

            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            ImGuiIOPtr io = ImGui.GetIO();
            bool keysFree = !io.WantCaptureKeyboard && !keysHeld;
            bool mouseFree = !io.WantCaptureMouse;
            bool control = input.IsKeyDown(Keys.LeftControl) || input.IsKeyDown(Keys.RightControl);
            bool shift = input.IsKeyDown(Keys.LeftShift) || input.IsKeyDown(Keys.RightShift);

            //The shortcuts of the menus work while a text field of a window has the keyboard too
            if (control && !keysHeld)
                HandleShortcuts(input, shift);

            if (keysFree && !session.IsDialogOpen)
            {
                if (input.KeyPressed(Keys.F5))
                {
                    TestDrive(shift ? placeTool.Cursor : new Cell(0, 0));
                    return;
                }
                //Esc drops the selection of the tool first, and leaves only when there is none
                if (input.KeyPressed(Keys.Escape) && !ActiveTool.Cancel())
                    session.Guard(Leave);
                if (input.KeyPressed(Keys.F1))
                    showHelp = !showHelp;
                if (input.KeyPressed(Keys.F9))
                    renderer.Classic = !renderer.Classic;
                //F10 the shadows, Shift+F10 the ambient occlusion; F11 is the game's (full screen or a window)
                if (input.KeyPressed(Keys.F10))
                {
                    if (shift)
                        renderer.AmbientOcclusion = !renderer.AmbientOcclusion;
                    else
                        renderer.Shadows = !renderer.Shadows;
                }
                if (input.KeyPressed(Keys.M))
                    CycleMood();
                if (input.KeyPressed(Keys.R) && !control)
                    placeTool.ToggleRoad();
                if (input.KeyPressed(Keys.Home))
                    FrameLevel();
                //The layer being edited: Tab cycles, Right Shift and Right Ctrl toggle as in the editor of 2016
                if (input.KeyPressed(Keys.Tab))
                    layer = (Layer)(((int)layer + 1) % 3);
                if (input.KeyPressed(Keys.RightShift))
                    layer = layer == Layer.Back ? Layer.Middle : Layer.Back;
                if (RightControlTapped(input))
                    layer = layer == Layer.Front ? Layer.Middle : Layer.Front;
                for (int i = 0; i < EditorToolKeys.ByTool.Length; i++)
                    if (!control && input.KeyPressed(EditorToolKeys.ByTool[i]))
                        SetTool((EditorTool)i);
                //Comma and period choose a block, which is for the Place tool: it becomes active and gets the key
                if (input.KeyPressed(Keys.OemComma) || input.KeyPressed(Keys.OemPeriod))
                    SetTool(EditorTool.Place);
                camera.HandleKeys(input, elapsed);
                //The Select tool has its Ctrl shortcuts (A, C, X, V) itself: they must not reach a text field of a window
                if (!hiddenGuard.HandleKeys(input, activeTool, view.HiddenNote(activeTool, layer)))
                    ActiveTool.HandleKeys(input, layer);
            }
            if (session.IsDialogOpen)
                return;

            //The camera has the mouse first: its drags take precedence over the tool, and Ctrl + the wheel is the tool's
            Viewport viewport = Manager.Viewport;
            camera.HandleMouse(input, viewport, mouseFree, !control);
            if (!hiddenGuard.HandleMouse(input, ActiveTool, activeTool, viewport, layer, mouseFree && !focusReturning, view.HiddenNote(activeTool, layer), CursorDepth))
                ActiveTool.HandleMouse(input, viewport, layer, mouseFree && !focusReturning);
            focusReturning = false;
        }

        /// <summary>
        /// Right Ctrl was let go without anything done while it was held. It is a Ctrl for the shortcuts and the clicks
        /// too (Ctrl+V, Ctrl + click), which must not switch the layer under them.
        /// </summary>
        private bool RightControlTapped(InputState input)
        {
            if (input.IsKeyDown(Keys.RightControl))
            {
                if (input.KeyPressed(Keys.RightControl))
                    rightControlAlone = true;
                if (input.Keyboard.GetPressedKeys().Length > 1 || input.WheelDelta != 0 || input.Mouse.LeftButton == ButtonState.Pressed
                    || input.Mouse.RightButton == ButtonState.Pressed || input.Mouse.MiddleButton == ButtonState.Pressed)
                    rightControlAlone = false;
                return false;
            }
            bool tapped = rightControlAlone && input.PreviousKeyboard.IsKeyDown(Keys.RightControl);
            rightControlAlone = false;
            return tapped;
        }

        /// <summary>Ctrl+N, Ctrl+O, Ctrl+S, Ctrl+Shift+S of the File menu and Ctrl+Z, Ctrl+Y (Ctrl+Shift+Z) of the Edit menu.</summary>
        private void HandleShortcuts(InputState input, bool shift)
        {
            if (input.KeyPressed(Keys.N))
                session.New();
            else if (input.KeyPressed(Keys.O))
                session.ShowOpen();
            else if (input.KeyPressed(Keys.S))
            {
                if (shift)
                    session.SaveAs();
                else
                    session.Save();
            }
            //Ctrl+Shift+Z redoes as in the editors of today, Ctrl+Y as in Windows
            //A drag of a tool ends first, so that the step it makes is the one taken back, not one made after it
            else if (input.KeyPressed(Keys.Z) && !shift)
            {
                EndStrokes();
                placeTool.Undo();
            }
            else if (input.KeyPressed(Keys.Y) || input.KeyPressed(Keys.Z) && shift)
            {
                EndStrokes();
                placeTool.Redo();
            }
        }

        public override void Draw(GameTime gameTime)
        {
            //The previews of the palette are drawn first: switching render targets may discard the screen, and the
            //scene, drawn after them, covers it anyway
            previewSize = Math.Max(16, (int)Math.Round(BlockPreviews.ReferenceCellSize * Manager.UiScale));
            if (showPalette && palette.NeedsPreviews)
                previews.Draw(previewSize, (float)gameTime.ElapsedGameTime.TotalSeconds);

            Viewport viewport = Manager.Viewport;
            camera.Update(viewport);

            renderer.Lighting = Lighting.For(Level.Mood);
            renderer.HazeOffset = Math.Max(0f, camera.Distance - GameCameraDistance);
            renderer.ShadowDistance = Math.Max(MinShadowDistance, camera.Distance * 1.5f);
            renderer.Begin(camera.View, camera.Projection);
            renderer.SetSky();
            view.Draw(renderer);
            renderer.End();
            //The cursor cell of the Place tool is the other tools' too (Space adds or toggles there), on their plane
            markers.Draw(camera.View, camera.Projection, CursorDepth, placeTool.Cursor, Level.Finish);
            markers.DrawGaps(camera.View, camera.Projection, Level);
            if (activeTool == EditorTool.Select && view.HiddenNote(activeTool, layer) == null)
                selectTool.Draw(markers, camera.View, camera.Projection);
            if (activeTool == EditorTool.Object && view.ShowObjects)
                markers.DrawObjects(camera.View, camera.Projection, objectTool.Bounds(objectTool.Hovered), objectTool.Bounds(objectTool.Selected));
            if (activeTool == EditorTool.Scenery && view.ShowScenery)
                markers.DrawObjects(camera.View, camera.Projection, sceneryTool.Bounds(sceneryTool.Hovered), sceneryTool.Bounds(sceneryTool.Selected));

            layout.Update(Manager.UiScale);
            imgui.BeforeLayout(gameTime);
            DrawInterface();
            imgui.AfterLayout();
        }

        private void DrawInterface()
        {
            ImGuiViewportPtr viewport = ImGui.GetMainViewport();
            float statusHeight = ImGui.GetFrameHeightWithSpacing();
            float scale = InterfaceLayout.Scale;

            levelReport.Update(Level);
            DrawMenuBar();
            InterfaceLayout.DrawDockSpace(viewport, statusHeight);
            StatusBar.Draw(viewport, statusHeight, placeTool, activeTool, layer, objectTool.Chosen, sceneryTool, session, levelReport, message);
            if (showPalette)
                palette.Draw(ref showPalette, viewport, placeTool, objectTool, sceneryTool, layer, previewSize, scale);
            if (showInspector)
                inspector.Draw(ref showInspector, viewport, scale, palette.DefaultWidth, activeTool, placeTool, objectTool, selectTool, sceneryTool, layer);
            if (showLevel)
                levelWindow.Draw(ref showLevel, viewport, levelReport, camera, scale, palette.DefaultWidth, statusHeight);
            if (showHelp)
                HelpWindow.Draw(ref showHelp, scale);
            if (showDemo)
                ImGui.ShowDemoWindow(ref showDemo);
            //At the root of the ID stack, where the popups of the dialogs are opened, whatever asked for them (a menu, a key)
            session.DrawDialogs(scale);
        }

        private void DrawMenuBar()
        {
            if (!ImGui.BeginMainMenuBar())
                return;

            if (ImGui.BeginMenu("File"))
            {
                if (ImGui.MenuItem("New", "Ctrl+N"))
                    session.New();
                if (ImGui.MenuItem("Open...", "Ctrl+O"))
                    session.ShowOpen();
                if (ImGui.BeginMenu("Open recent"))
                {
                    session.DrawRecent();
                    ImGui.EndMenu();
                }
                ImGui.Separator();
                if (ImGui.MenuItem("Save", "Ctrl+S"))
                    session.Save();
                if (ImGui.MenuItem("Save as...", "Ctrl+Shift+S"))
                    session.SaveAs();
                ImGui.Separator();
                if (ImGui.MenuItem(standalone ? "Exit" : "Back to the menu", "Esc"))
                    session.Guard(Leave);
                ImGui.EndMenu();
            }
            EditorMenus.DrawEdit(Level, placeTool, selectTool, activeTool, view.HiddenNote(activeTool, layer), report);
            EditorMenus.DrawView(renderer, view, markers, activeTool, SetTool, ref layer, FrameLevel, ref showPalette, ref showInspector, ref showLevel,
                ref showDemo);
            if (ImGui.BeginMenu("Level"))
            {
                if (ImGui.BeginMenu("Mood"))
                {
                    foreach (LevelMood mood in Enum.GetValues<LevelMood>())
                        if (ImGui.MenuItem(mood.ToString(), mood == LevelMood.Day ? "M" : null, Level.Mood == mood))
                            Level.SetMood(mood);
                    ImGui.EndMenu();
                }
                if (ImGui.MenuItem("Road mode", "R", placeTool.RoadMode))
                    placeTool.ToggleRoad();
                ImGui.Separator();
                if (ImGui.MenuItem("Test drive", "F5"))
                    driveRequested = new Cell(0, 0);
                if (ImGui.MenuItem("Test drive from the cursor", "Shift+F5"))
                    driveRequested = placeTool.Cursor;
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Help"))
            {
                ImGui.MenuItem("Controls", "F1", ref showHelp);
                ImGui.EndMenu();
            }
            ImGui.EndMainMenuBar();
        }
    }
}
