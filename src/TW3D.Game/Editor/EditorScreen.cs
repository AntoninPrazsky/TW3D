using System;
using System.IO;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TW3D.GameObjects;
using TW3D.Graphics;
using TW3D.Input;
using TW3D.Screens;
using TW3D.Shared;

namespace TW3D.Editor
{
    /// <summary>
    /// The level editor: the level being edited (an <see cref="EditedLevel"/>, the document with undo, which the
    /// <see cref="LevelSession"/> opens, saves and guards with "Save changes?") drawn as in the game, seen by a free camera
    /// (<see cref="EditorCamera"/>), edited with the <see cref="BlockTool"/> whose cursor the <see cref="EditorMarkers"/>
    /// show, with the user interface of Dear ImGui over it (the menu bar, the <see cref="StatusBar"/> and the windows: the
    /// <see cref="PaletteWindow"/> with the <see cref="BlockPreviews"/>, the <see cref="HelpWindow"/>, the
    /// <see cref="FileDialogs"/> of the session). The screen builds the parts, routes the input to them and draws them in
    /// their order. Opened by <c>--editor [level]</c>, alone on the screen stack (leaving it quits), or from the main menu
    /// of a Debug build (leaving it goes back to the menu). The scene gets the mouse and the keys only when the interface
    /// does not want them (the cursor over a window, a text field being typed in) and no dialog is open.
    /// </summary>
    public sealed class EditorScreen : Screen
    {
        //The font of the interface (DejaVu Sans, approved by the FSF; its licence is content/Fonts/DejaVuSans-LICENSE.txt) and its size in pixels
        //on a 1920 × 1080 window, scaled with the window
        private const string FontFile = "Fonts/DejaVuSans.ttf";
        private const float FontSize = 17f;
        //Where ImGui keeps the positions and sizes of its windows
        private static readonly string LayoutFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TW3D", "editor-layout.ini");
        //Where the sky is drawn relative to the camera: around it, as the game draws it around the car
        private static readonly Vector3 SkyOffset = new Vector3(0f, -2f, 6.5f);
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
        private Model sky;
        private EditorCamera camera;
        private LevelView view;
        private BlockTool tool;
        private EditorMarkers markers;
        private ImGuiRenderer imgui;
        private BlockPreviews previews;
        private PaletteWindow palette;
        private LevelSession session;
        private ImGuiStyle defaultStyle;
        private int fontSize;
        //The size of a preview of the palette in pixels, scaled with the window
        private int previewSize;
        private bool showPalette = true, showHelp, showDemo;
        private Layer layer = Layer.Middle;
        //What the status bar says about the last thing done
        private string message = "";
        //The window had no focus: the click that brings it back is not an edit
        private bool focusReturning;
        //A dialog was open in the last frame: the keys held when it closed answered it (D of Don't save would fly the
        //camera), so the scene gets no keys until they are released
        private bool dialogWasOpen;
        private Keys[] dialogKeys = Array.Empty<Keys>();

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

        public override unsafe void LoadContent()
        {
            ShowImmediately();
            Manager.PointersVisible = false;
            previousTitle = Manager.Game.Window.Title;

            assets = LevelAssets.Load(Manager.Content);
            renderer = new SceneRenderer(Manager.GraphicsDevice, Manager.Content);
            settings.Apply(renderer);
            sky = Manager.Content.Load<Model>("Env/sky");
            camera = new EditorCamera();
            view = new LevelView(assets);
            Action<string> report = note => message = note;
            tool = new BlockTool(camera, report);
            markers = new EditorMarkers(Manager.GraphicsDevice, Manager.Content);

            imgui = new ImGuiRenderer(Manager.Game, LayoutFile);
            ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            defaultStyle = *ImGui.GetStyle().NativePtr;
            previews = new BlockPreviews(Manager.GraphicsDevice, assets, imgui);
            palette = new PaletteWindow(previews, assets.Catalog);

            session = new LevelSession(Manager.Game, Manager.GraphicsDevice, imgui, assets.Catalog, levelsArgument,
                CaptureCamera, tool.EndStroke, Show, report);
            //The close button of the window asks too when there are unsaved changes
            Manager.Game.Exiting += OnExiting;

            session.OpenFirst(levelArgument);
        }

        public override void UnloadContent()
        {
            Manager.Game.Exiting -= OnExiting;
            tool.EndStroke();
            session.Dispose();
            previews.Dispose();
            imgui.Dispose();
            Manager.PointersVisible = true;
            Manager.Game.Window.Title = previousTitle;
        }

        /// <summary>The session made a level the one edited: the view, the tool and the camera (where it was, or framing the level).</summary>
        private void Show(EditedLevel opened, CameraView saved)
        {
            view.Level = opened;
            tool.Level = opened;
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
            if (!session.LetExit())
                e.Cancel = true;
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

        private void CycleMood()
        {
            Level.SetMood((LevelMood)(((int)Level.Mood + 1) % 3));
        }

        public override void Update(GameTime gameTime)
        {
            //Without the focus the screen gets no input, so it would not see the button go up: a stroke ends here
            if (!HasFocus)
            {
                tool.EndStroke();
                focusReturning = true;
            }
            session.UpdateTitle();
        }

        public override void HandleInput(InputState input, GameTime gameTime)
        {
            //A dialog has the keys and the mouse; the key that closes it is not seen again here, as it was pressed before
            if (session.IsDialogOpen)
            {
                tool.EndStroke();
                dialogWasOpen = true;
                return;
            }
            if (dialogWasOpen)
            {
                dialogWasOpen = false;
                dialogKeys = input.Keyboard.GetPressedKeys();
            }
            bool dialogKeysHeld = Array.Exists(dialogKeys, key => input.IsKeyDown(key));
            if (!dialogKeysHeld)
                dialogKeys = Array.Empty<Keys>();

            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            ImGuiIOPtr io = ImGui.GetIO();
            bool keysFree = !io.WantCaptureKeyboard && !dialogKeysHeld;
            bool mouseFree = !io.WantCaptureMouse;
            bool control = input.IsKeyDown(Keys.LeftControl) || input.IsKeyDown(Keys.RightControl);
            bool shift = input.IsKeyDown(Keys.LeftShift) || input.IsKeyDown(Keys.RightShift);

            //The shortcuts of the menus work while a text field of a window has the keyboard too
            if (control && !dialogKeysHeld)
                HandleShortcuts(input, shift);

            if (keysFree && !session.IsDialogOpen)
            {
                if (input.KeyPressed(Keys.Escape))
                    session.Guard(Leave);
                if (input.KeyPressed(Keys.F1))
                    showHelp = !showHelp;
                if (input.KeyPressed(Keys.F9))
                    renderer.Classic = !renderer.Classic;
                if (input.KeyPressed(Keys.F10))
                    renderer.Shadows = !renderer.Shadows;
                if (input.KeyPressed(Keys.F11))
                    renderer.AmbientOcclusion = !renderer.AmbientOcclusion;
                if (input.KeyPressed(Keys.M))
                    CycleMood();
                if (input.KeyPressed(Keys.Home))
                    FrameLevel();
                //The layer being edited: Tab cycles, Right Shift and Right Ctrl toggle as in the old editor
                if (input.KeyPressed(Keys.Tab))
                    layer = (Layer)(((int)layer + 1) % 3);
                if (input.KeyPressed(Keys.RightShift))
                    layer = layer == Layer.Back ? Layer.Middle : Layer.Back;
                if (input.KeyPressed(Keys.RightControl))
                    layer = layer == Layer.Front ? Layer.Middle : Layer.Front;
                camera.HandleKeys(input, elapsed);
                tool.HandleKeys(input, layer);
            }
            if (session.IsDialogOpen)
                return;

            //The camera has the mouse first: its drags take precedence over the tool, and Ctrl + the wheel is the tool's
            Viewport viewport = Manager.Viewport;
            camera.HandleMouse(input, viewport, mouseFree, !control);
            tool.HandleMouse(input, viewport, layer, mouseFree && !focusReturning);
            focusReturning = false;
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
            else if (input.KeyPressed(Keys.Z) && !shift)
                tool.Undo();
            else if (input.KeyPressed(Keys.Y) || input.KeyPressed(Keys.Z) && shift)
                tool.Redo();
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
            renderer.SetBackground(sky, Matrix.CreateScale(Units.ModelToScene) * Matrix.CreateTranslation(camera.Position + SkyOffset));
            view.Draw(renderer);
            renderer.End();
            markers.Draw(camera.View, camera.Projection, layer, tool.Cursor, Level.Finish);

            UpdateFont();
            imgui.BeforeLayout(gameTime);
            DrawInterface();
            imgui.AfterLayout();
        }

        /// <summary>The font and the sizes of the interface follow the size of the window.</summary>
        private unsafe void UpdateFont()
        {
            int size = Math.Max(8, (int)Math.Round(FontSize * Manager.UiScale));
            if (size == fontSize)
                return;
            fontSize = size;
            imgui.LoadFont(Path.Combine(AppContext.BaseDirectory, Manager.Content.RootDirectory, FontFile), size);

            ImGuiStylePtr style = ImGui.GetStyle();
            *style.NativePtr = defaultStyle;
            ImGui.StyleColorsDark();
            style.WindowRounding = 4f;
            style.FrameRounding = 3f;
            style.ScaleAllSizes(size / FontSize);
        }

        private void DrawInterface()
        {
            ImGuiViewportPtr viewport = ImGui.GetMainViewport();
            float statusHeight = ImGui.GetFrameHeightWithSpacing();
            float scale = ImGui.GetFontSize() / FontSize;

            DrawMenuBar();
            DrawDockSpace(viewport, statusHeight);
            StatusBar.Draw(viewport, statusHeight, tool, layer, palette.Object, session, message);
            if (showPalette)
                palette.Draw(ref showPalette, viewport, tool, layer, previewSize, scale);
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
            if (ImGui.BeginMenu("Edit"))
            {
                if (ImGui.MenuItem(Level.CanUndo ? "Undo " + Level.UndoName : "Undo", "Ctrl+Z", false, Level.CanUndo))
                    tool.Undo();
                if (ImGui.MenuItem(Level.CanRedo ? "Redo " + Level.RedoName : "Redo", "Ctrl+Y", false, Level.CanRedo))
                    tool.Redo();
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("View"))
            {
                if (ImGui.MenuItem("Lighting of 2016", "F9", renderer.Classic))
                    renderer.Classic = !renderer.Classic;
                if (ImGui.MenuItem("Shadows", "F10", renderer.Shadows))
                    renderer.Shadows = !renderer.Shadows;
                if (ImGui.MenuItem("Ambient occlusion", "F11", renderer.AmbientOcclusion))
                    renderer.AmbientOcclusion = !renderer.AmbientOcclusion;
                ImGui.Separator();
                if (ImGui.BeginMenu("Layer"))
                {
                    foreach (Layer candidate in Enum.GetValues<Layer>())
                        if (ImGui.MenuItem(candidate.ToString(), null, layer == candidate))
                            layer = candidate;
                    ImGui.EndMenu();
                }
                if (ImGui.MenuItem("Frame the level", "Home"))
                    FrameLevel();
                ImGui.Separator();
                ImGui.MenuItem("Palette", null, ref showPalette);
                ImGui.MenuItem("ImGui demo window", null, ref showDemo);
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Level"))
            {
                if (ImGui.BeginMenu("Mood"))
                {
                    foreach (LevelMood mood in Enum.GetValues<LevelMood>())
                        if (ImGui.MenuItem(mood.ToString(), mood == LevelMood.Day ? "M" : null, Level.Mood == mood))
                            Level.SetMood(mood);
                    ImGui.EndMenu();
                }
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Help"))
            {
                ImGui.MenuItem("Controls", "F1", ref showHelp);
                ImGui.EndMenu();
            }
            ImGui.EndMainMenuBar();
        }

        /// <summary>
        /// A dock space over the whole work area between the menu bar and the status bar: the windows can be docked to its
        /// edges, and the scene shows through its middle, where the mouse is the scene's.
        /// </summary>
        private static void DrawDockSpace(ImGuiViewportPtr viewport, float statusHeight)
        {
            ImGui.SetNextWindowPos(viewport.WorkPos);
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(viewport.WorkSize.X, viewport.WorkSize.Y - statusHeight));
            ImGui.SetNextWindowViewport(viewport.ID);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, System.Numerics.Vector2.Zero);
            const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse
                | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoNavFocus
                | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings;
            ImGui.Begin("Editor dock space", flags);
            ImGui.PopStyleVar(3);
            ImGui.DockSpace(ImGui.GetID("EditorDockSpace"), System.Numerics.Vector2.Zero, ImGuiDockNodeFlags.PassthruCentralNode);
            ImGui.End();
        }
    }
}
