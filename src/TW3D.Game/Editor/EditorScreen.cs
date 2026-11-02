using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TW3D.GameObjects;
using TW3D.Graphics;
using TW3D.Input;
using TW3D.Screens;
using TW3D.Shared;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace TW3D.Editor
{
    /// <summary>
    /// The level editor: the level being edited (an <see cref="EditedLevel"/>, the document with undo) drawn as in the
    /// game, seen by a free camera (<see cref="EditorCamera"/>), edited with the <see cref="BlockTool"/> whose cursor
    /// the <see cref="EditorMarkers"/> show, with the user interface of Dear ImGui over it (the menu bar, the status bar
    /// and the windows). Opened by <c>--editor [level]</c>, alone on the screen stack (leaving it quits), or from the
    /// main menu of a Debug build (leaving it goes back to the menu). The scene gets the mouse and the keys only when
    /// the interface does not want them (the cursor over a window, a text field being typed in).
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
        private const string TitlePrefix = "Level Editor - ";

        private readonly GameSettings settings;
        private readonly string levelArgument;
        //Opened from the command line, alone: leaving quits the game; otherwise leaving goes back to the menu
        private readonly bool standalone;
        //The title of the window before the editor, put back when it leaves
        private string previousTitle;

        private LevelAssets assets;
        private SceneRenderer renderer;
        private Model sky;
        private EditorCamera camera;
        private LevelView view;
        private EditedLevel level;
        private BlockTool tool;
        private EditorMarkers markers;
        private ImGuiRenderer imgui;
        private ImGuiStyle defaultStyle;
        private int fontSize;
        private bool showPalette = true, showHelp, showDemo;
        private Layer layer = Layer.Middle;
        //The title of the window says the level has unsaved changes; the title is set again when that changes
        private bool titleDirty;
        //What the status bar says about the last thing done
        private string message = "";
        //The window had no focus: the click that brings it back is not an edit
        private bool focusReturning;

        /// <param name="level">A level name (a file in Content/Levels without the extension) or a level file; null = a new level.</param>
        /// <param name="standalone">The editor is alone (opened by --editor): leaving it quits the game instead of going back to the menu.</param>
        public EditorScreen(GameSettings settings, string level, bool standalone)
        {
            this.settings = settings;
            levelArgument = level;
            this.standalone = standalone;
            FadeInTime = TimeSpan.Zero;
            FadeOutTime = TimeSpan.Zero;
            IsOpaque = true;
        }

        private string LevelName => level.File != null ? Path.GetFileNameWithoutExtension(level.File) : "new level";

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
            tool = new BlockTool(camera, note => message = note);
            markers = new EditorMarkers(Manager.GraphicsDevice, Manager.Content);

            imgui = new ImGuiRenderer(Manager.Game, LayoutFile);
            ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            defaultStyle = *ImGui.GetStyle().NativePtr;

            Open(levelArgument);
        }

        public override void UnloadContent()
        {
            tool.EndStroke();
            imgui.Dispose();
            Manager.PointersVisible = true;
            Manager.Game.Window.Title = previousTitle;
        }

        /// <summary>Opens a level by its name or file; null, or a level that is not found, gives a new level.</summary>
        private void Open(string argument)
        {
            string file = argument;
            if (file != null && !File.Exists(file))
                file = Path.Combine(AppContext.BaseDirectory, Manager.Content.RootDirectory, "Levels", argument + LevelFile.Extension);

            if (file != null && File.Exists(file))
            {
                try
                {
                    level = new EditedLevel(assets.Catalog, LevelFile.Load(file), Path.GetFullPath(file));
                    List<LevelProblem> problems = level.Problems();
                    message = problems.Count == 0 ? "Opened " + Path.GetFileName(file) + "."
                        : problems.Count == 1 ? "1 problem in the level: " + problems[0]
                        : problems.Count + " problems in the level: " + problems[0];
                }
                catch (Exception e) when (e is IOException || e is InvalidDataException || e is System.Text.Json.JsonException)
                {
                    message = "Could not read " + file + ": " + e.Message;
                    level = EditedLevel.New(assets.Catalog);
                }
            }
            else
            {
                if (argument != null)
                    message = "Level " + argument + " was not found; a new level.";
                level = EditedLevel.New(assets.Catalog);
            }

            view.Level = level;
            tool.Level = level;
            SetTitle();
            FrameLevel();
        }

        /// <summary>The title of the window: the name of the level, with a star while it has unsaved changes.</summary>
        private void SetTitle()
        {
            titleDirty = level.IsDirty;
            Manager.Game.Window.Title = TitlePrefix + LevelName + (titleDirty ? "*" : "");
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
            if (standalone)
                Manager.Game.Exit();
            else
                Close();
        }

        private void CycleMood()
        {
            level.SetMood((LevelMood)(((int)level.Mood + 1) % 3));
        }

        public override void Update(GameTime gameTime)
        {
            //Without the focus the screen gets no input, so it would not see the button go up: a stroke ends here
            if (!HasFocus)
            {
                tool.EndStroke();
                focusReturning = true;
            }
        }

        public override void HandleInput(InputState input, GameTime gameTime)
        {
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            ImGuiIOPtr io = ImGui.GetIO();
            bool keysFree = !io.WantCaptureKeyboard;
            bool mouseFree = !io.WantCaptureMouse;

            if (keysFree)
            {
                if (input.KeyPressed(Keys.Escape))
                    Leave();
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

            //The camera has the mouse first: its drags take precedence over the tool, and Ctrl + the wheel is the tool's
            Viewport viewport = Manager.Viewport;
            bool control = input.IsKeyDown(Keys.LeftControl) || input.IsKeyDown(Keys.RightControl);
            camera.HandleMouse(input, viewport, mouseFree, !control);
            tool.HandleMouse(input, viewport, layer, mouseFree && !focusReturning);
            focusReturning = false;

            if (level.IsDirty != titleDirty)
                SetTitle();
        }

        public override void Draw(GameTime gameTime)
        {
            Viewport viewport = Manager.Viewport;
            camera.Update(viewport);

            renderer.Lighting = Lighting.For(level.Mood);
            renderer.HazeOffset = Math.Max(0f, camera.Distance - GameCameraDistance);
            renderer.ShadowDistance = Math.Max(MinShadowDistance, camera.Distance * 1.5f);
            renderer.Begin(camera.View, camera.Projection);
            renderer.SetBackground(sky, Matrix.CreateScale(Units.ModelToScene) * Matrix.CreateTranslation(camera.Position + SkyOffset));
            view.Draw(renderer);
            renderer.End();
            markers.Draw(camera.View, camera.Projection, layer, tool.Cursor, level.Finish);

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

            DrawMenuBar();
            DrawDockSpace(viewport, statusHeight);
            DrawStatusBar(viewport, statusHeight);
            if (showPalette)
                DrawPalette(viewport);
            if (showHelp)
                DrawHelp();
            if (showDemo)
                ImGui.ShowDemoWindow(ref showDemo);
        }

        private void DrawMenuBar()
        {
            if (!ImGui.BeginMainMenuBar())
                return;

            if (ImGui.BeginMenu("File"))
            {
                ImGui.MenuItem("New", "Ctrl+N", false, false);
                ImGui.MenuItem("Open...", "Ctrl+O", false, false);
                ImGui.MenuItem("Save", "Ctrl+S", false, false);
                ImGui.MenuItem("Save as...", "Ctrl+Shift+S", false, false);
                ImGui.Separator();
                if (ImGui.MenuItem(standalone ? "Exit" : "Back to the menu", "Esc"))
                    Leave();
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Edit"))
            {
                if (ImGui.MenuItem(level.CanUndo ? "Undo " + level.UndoName : "Undo", "Ctrl+Z", false, level.CanUndo))
                    tool.Undo();
                if (ImGui.MenuItem(level.CanRedo ? "Redo " + level.RedoName : "Redo", "Ctrl+Y", false, level.CanRedo))
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
                        if (ImGui.MenuItem(mood.ToString(), mood == LevelMood.Day ? "M" : null, level.Mood == mood))
                            level.SetMood(mood);
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

        private void DrawStatusBar(ImGuiViewportPtr viewport, float height)
        {
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(viewport.WorkPos.X, viewport.WorkPos.Y + viewport.WorkSize.Y - height));
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(viewport.WorkSize.X, height));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
            const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings
                | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoNavFocus;
            ImGui.Begin("Status bar", flags);
            ImGui.PopStyleVar(2);

            ImGui.Text($"Cell: {tool.Cursor.X}, {tool.Cursor.Y}");
            Separate();
            ImGui.Text("Layer: " + layer);
            Separate();
            ImGui.Text("Tool: Place");
            Separate();
            ImGui.Text($"Block: {tool.Block}, shift {tool.Shift}");
            Separate();
            ImGui.Text((level.File != null ? Path.GetFileName(level.File) : LevelName) + (level.IsDirty ? "*" : ""));
            if (message.Length > 0)
            {
                Separate();
                ImGui.TextDisabled(message);
            }
            ImGui.End();
        }

        /// <summary>A gap between the items of the status bar.</summary>
        private static void Separate()
        {
            ImGui.SameLine(0f, ImGui.GetFontSize());
            ImGui.TextDisabled("|");
            ImGui.SameLine(0f, ImGui.GetFontSize());
        }

        private void DrawPalette(ImGuiViewportPtr viewport)
        {
            float scale = ImGui.GetFontSize() / FontSize;
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(320f, 480f) * scale, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(viewport.WorkPos.X + viewport.WorkSize.X - 340f * scale, viewport.WorkPos.Y + 20f * scale), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Palette", ref showPalette))
                ImGui.TextWrapped("The blocks of the catalog, as turning 3D previews, come here (issue #24).");
            ImGui.End();
        }

        private void DrawHelp()
        {
            float scale = ImGui.GetFontSize() / FontSize;
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(540f, 560f) * scale, ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Controls", ref showHelp))
            {
                ImGui.SeparatorText("Mouse");
                HelpRows(new[]
                {
                    ("Left button", "Place the chosen block in the cell under the cursor; dragging paints"),
                    ("Left Shift + left button", "Erase the block under the cursor; dragging erases"),
                    ("Right button", "Pick: the block under the cursor becomes the chosen block, with its shift"),
                    ("Ctrl + wheel", "Shift the block under the cursor up or down (the chosen shift follows)"),
                    ("Middle button drag", "Pan along the track"),
                    ("Wheel", "Zoom towards the cursor"),
                    ("Alt + left drag", "Orbit around the point under the cursor")
                });
                ImGui.SeparatorText("Keys");
                HelpRows(new[]
                {
                    ("Arrows", "Move the cursor cell (the camera follows)"),
                    ("Space / Delete", "Place the chosen block / erase the block at the cursor cell"),
                    ("PageUp / PageDown", "Shift the block at the cursor cell up / down (the chosen shift follows)"),
                    ("F", "The block at the cursor cell is the finish of the level, or stops being it"),
                    (", / .", "The previous / next block of the catalog becomes the chosen block"),
                    ("Ctrl+Z / Ctrl+Y", "Undo / redo"),
                    ("Tab", "The next layer; Right Shift / Right Ctrl toggle the back / front layer"),
                    ("M", "The mood of the level: day, sunset, night"),
                    ("W / S / A / D, Q / E", "Fly the camera along the track, up and down"),
                    ("Numeric keypad 4 / 6, 8 / 2", "Turn the camera"),
                    ("Home", "Frame the whole level"),
                    ("F9 / F10 / F11", "Lighting of 2016 / shadows / ambient occlusion"),
                    ("F1", "This window"),
                    ("Esc", "Leave the editor")
                });
                ImGui.Spacing();
                ImGui.TextWrapped("The palette, selection, dynamic objects, files and the test drive come with the next issues of the editor (GitHub issue #20).");
            }
            ImGui.End();
        }

        private static void HelpRows((string Keys, string Action)[] rows)
        {
            if (!ImGui.BeginTable("help", 2, ImGuiTableFlags.SizingStretchProp))
                return;
            foreach ((string keys, string action) in rows)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(keys);
                ImGui.TableNextColumn();
                ImGui.TextWrapped(action);
            }
            ImGui.EndTable();
        }
    }
}
