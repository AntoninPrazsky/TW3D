using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TW3D.Audio;
#if !ANDROID
using TW3D.Editor;
#endif
using TW3D.GamePlay;
using TW3D.Graphics;
using TW3D.Screens;
using TW3D.Shared;

namespace TW3D
{
    /// <summary>The game: its window (or the screen of a phone), the screens from the menu to the levels and the settings.</summary>
    public class TripleWheelGame : Game
    {
#if ANDROID
        //Samples per pixel of the multisample antialiasing (Options > Antialiasing); fewer on phones, where it costs
        //more. The edges of the scene show steps without it even on the screens of phones (the owner, 2026-11-04)
        private const int MultiSampleCount = 4;
        //A phone screen is small, so the user interface is bigger than on a monitor (see ScreenManager.UiSize),
        //the on-screen controls less so (ScreenManager.ControlsSize)
        private const float PhoneUiSize = 2f;
        private const float PhoneControlsSize = 1.5f;
#else
        //Samples per pixel of the multisample antialiasing (Options > Antialiasing): 8 on Windows, 4 elsewhere, which
        //every OpenGL 3 graphics card takes. MonoGame does not lower the count: SDL makes no window at all with more
        //samples than the card has (the GPU of a Raspberry Pi 5 has 4). Off by default on a Raspberry Pi, which draws the
        //scene smaller instead (see PiGraphics and GameSettings.Antialiasing)
        private static readonly int MultiSampleCount = OperatingSystem.IsWindows() ? 8 : 4;
#endif

        private readonly GraphicsDeviceManager graphics;
        private readonly ScreenManager screens;
        private readonly string startLevel;
        //The level editor instead of the game (--editor), the level it opens (null for the last one) and its levels
        //folder (--levels; null for the default one)
        private readonly bool editor;
        private readonly string editorLevel;
        private readonly string editorLevels;
        private readonly GameSettings settings = GameSettings.Load();
        //The screen has multisample antialiasing in this run: as the settings said at the start
        private readonly bool antialiasing;
        //The 3D garage behind the menus
        private BackgroundScreen background;
        private readonly GameRecords records = GameRecords.Load();
        //The levels of the last level select, in its order; the par times of the levels shown there
        private List<string> levels = new List<string>();
        private readonly Dictionary<string, float> pars = new Dictionary<string, float>();
#if !ANDROID
        //The window size given on the command line (a window always, whatever the settings say)
        private readonly Point? windowSize;
#if !ANDROID
        //The game is in full screen now (F11 and the Options menu switch it)
        private bool fullScreen;
#endif
#endif

        /// <param name="windowSize">Size of the window; null = by the settings (full screen, or a maximized window).</param>
        /// <param name="startLevel">Level to start directly, skipping the menu; null = start with the menu.</param>
        /// <param name="editor">Open the level editor instead of the game, in a window (not on Android).</param>
        /// <param name="editorLevel">The level the editor opens: a level name or a level file; null = the last level edited.</param>
        /// <param name="editorLevels">The levels folder of the editor; null = content/Levels of the repository (see Editor/LevelFolder).</param>
        public TripleWheelGame(Point? windowSize = null, string startLevel = null, bool editor = false, string editorLevel = null,
            string editorLevels = null)
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            this.startLevel = startLevel;
            this.editor = editor;
            this.editorLevel = editorLevel;
            this.editorLevels = editorLevels;

            //One update and one frame per refresh of the monitor (e.g. 75 per second on a 75 Hz monitor);
            //the physics simulation keeps its own fixed time step (see PhysicsClock)
            IsFixedTimeStep = false;
            graphics.SynchronizeWithVerticalRetrace = true;

            //Multisample antialiasing (see MultiSampleCount), unless Options switched it off. The screen gets its samples
            //when the window (the surface of a phone) is made: a change in Options comes with the next start, as MonoGame
            //changed nothing seen when its device was reset with other samples (on Windows and on the phone, 2026-11-04)
            antialiasing = settings.Antialiasing;
            graphics.PreferMultiSampling = antialiasing;
            graphics.PreparingDeviceSettings += (sender, e) =>
                e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = antialiasing ? MultiSampleCount : 0;

#if ANDROID
            //Full screen, landscape either way round
            graphics.IsFullScreen = true;
            graphics.SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight;
#else
            Window.Title = "Triple Wheel 3D";
            IsMouseVisible = true;

            this.windowSize = windowSize;
            //The editor and a window of a given size start in a window, whatever the settings say
            fullScreen = settings.FullScreen && !windowSize.HasValue && !editor;
            SetDisplayMode();
            //The user interface adapts to any window size
            Window.ClientSizeChanged += OnClientSizeChanged;
#endif

            screens = new ScreenManager(this);
#if ANDROID
            screens.UiSize = PhoneUiSize;
            screens.ControlsSize = PhoneControlsSize;
#endif
            Components.Add(screens);
            Components.Add(new FrameRateCounter(this, screens, settings) { DrawOrder = 100 });
        }

#if !ANDROID
        /// <summary>
        /// Full screen or a window, as <see cref="fullScreen"/> says (at the start by the settings, a window if its size
        /// was given on the command line). The window can be resized; it opens maximized.
        /// </summary>
        /// <remarks>
        /// Full screen is a borderless window of the size of the screen (same as the 2016 desktop version).
        /// With some graphics drivers it stutters with vsync (measured on an AMD Radeon with OpenGL: now and
        /// then a frame takes 50–80 ms, and the desktop stutters too), while a window does not.
        /// </remarks>
        private void SetDisplayMode()
        {
            DisplayMode display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            Point size = fullScreen ? new Point(display.Width, display.Height)
                : windowSize ?? new Point(display.Width * 3 / 4, display.Height * 3 / 4);

            graphics.PreferredBackBufferWidth = size.X;
            graphics.PreferredBackBufferHeight = size.Y;
            graphics.HardwareModeSwitch = false;
            graphics.IsFullScreen = fullScreen;
            Window.AllowUserResizing = !fullScreen;
        }

        /// <summary>Switches between full screen and the window, after a change in the Options menu.</summary>
        private void ApplyDisplayMode()
        {
            fullScreen = settings.FullScreen;
            SetDisplayMode();
            graphics.ApplyChanges();
            MaximizeWindow();
        }

        /// <summary>
        /// F11 switches between full screen and the window anywhere, as the Options menu does; the settings remember it,
        /// but for the editor alone (--editor), which starts in a window anyway.
        /// </summary>
        protected override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            //The input of this frame was read by the screens, which update in base.Update
            if (!screens.Input.KeyPressed(Keys.F11))
                return;
            if (!editor)
            {
                settings.FullScreen = !fullScreen;
                settings.Save();
            }
            fullScreen = !fullScreen;
            SetDisplayMode();
            graphics.ApplyChanges();
            MaximizeWindow();
        }

        //MonoGame cannot maximize its window, but the window is a window of SDL. "SDL2" is its name on Windows
        //(SDL2.dll); on Linux the library MonoGame ships is named by its version (see ResolveSdl)
        static TripleWheelGame()
        {
            NativeLibrary.SetDllImportResolver(typeof(TripleWheelGame).Assembly, ResolveSdl);
        }

        private static IntPtr ResolveSdl(string name, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (name != "SDL2")
                return IntPtr.Zero;
            string file = OperatingSystem.IsLinux() ? "libSDL2-2.0.so.0" : null;
            return file != null && NativeLibrary.TryLoad(file, assembly, searchPath, out IntPtr handle) ? handle : IntPtr.Zero;
        }

        [DllImport("SDL2")]
        private static extern void SDL_SetWindowResizable(IntPtr window, int resizable);

        [DllImport("SDL2")]
        private static extern void SDL_RestoreWindow(IntPtr window);

        [DllImport("SDL2")]
        private static extern void SDL_MaximizeWindow(IntPtr window);

        private void MaximizeWindow()
        {
            if (graphics.IsFullScreen || windowSize.HasValue)
                return;
            try
            {
                //After full screen, the window of SDL is no longer resizable (MonoGame thinks it still is)
                //and SDL still has it maximized from before, so maximizing it again would do nothing
                SDL_SetWindowResizable(Window.Handle, 1);
                SDL_RestoreWindow(Window.Handle);
                SDL_MaximizeWindow(Window.Handle);
            }
            catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException)
            {
                //The window stays of its size
            }
        }
#endif

        private void OnClientSizeChanged(object sender, EventArgs e)
        {
            if (graphics.IsFullScreen)
                return;
            Rectangle client = Window.ClientBounds;
            if (client.Width > 0 && client.Height > 0
                && (client.Width != graphics.PreferredBackBufferWidth || client.Height != graphics.PreferredBackBufferHeight))
            {
                graphics.PreferredBackBufferWidth = client.Width;
                graphics.PreferredBackBufferHeight = client.Height;
                graphics.ApplyChanges();
            }
        }

        protected override void Initialize()
        {
            base.Initialize();
            GameSounds.Volume = settings.SoundVolume;
#if !ANDROID
            MaximizeWindow();
            if (editor)
            {
                //The editor alone, without the menus; leaving it quits
                screens.Add(new EditorScreen(settings, editorLevel, standalone: true, editorLevels));
                return;
            }
#endif

            background = new BackgroundScreen(settings);
            MainMenuScreen menu = new MainMenuScreen(background);
            menu.AddItem("Play", OpenLevelSelect);
            menu.AddItem("Garage", () => screens.Add(new GarageScreen(settings, background.Scene)));
            menu.AddItem("About", () => screens.Add(new AboutScreen()));
#if ANDROID
            menu.AddItem("Options", () => screens.Add(new OptionsScreen(settings, null, antialiasing)));
#else
            menu.AddItem("Options", () => screens.Add(new OptionsScreen(settings, ApplyDisplayMode, antialiasing)));
#endif
            menu.AddItem("Exit", Exit);
#if !ANDROID
            //The level editor, for the players too (the sixth place, in the top right corner), where it runs: not on a
            //Raspberry Pi (see EditorScreen.CanRun); Android leaves it out
            if (EditorScreen.CanRun)
                menu.AddItem("Level editor", () => screens.Add(new EditorScreen(settings, null, standalone: false, editorLevels)));
#endif

            screens.Add(background);
            screens.Add(menu);

            if (startLevel != null)
            {
                //Level started directly from the command line (without the logo screen),
                //over the level select as if it was chosen there
                OpenLevelSelect();
                StartLevel(startLevel);
            }
            else
                screens.Add(new LogoScreen(TimeSpan.FromSeconds(1.5)));
        }

        /// <summary>
        /// Every level file in Content/Levels (next to the game) appears in the level select.
        /// The folder is read each time the level select opens.
        /// </summary>
        private List<string> FindLevels()
        {
#if ANDROID
            //On Android the content is packed in the app, Android lists its files
            string folder = Content.RootDirectory + "/Levels";
            string[] files = Android.App.Application.Context.Assets.List(folder) ?? Array.Empty<string>();
            return LevelFile.SortLevels(files, fileName => TitleContainer.OpenStream(folder + "/" + fileName));
#else
            return LevelFile.FindLevels(Path.Combine(AppContext.BaseDirectory, Content.RootDirectory, "Levels"));
#endif
        }

        private void OpenLevelSelect()
        {
            levels = FindLevels();
            screens.Add(new LevelSelectScreen("Level Select", levels, StartLevel, LevelResult));
        }

        private void StartLevel(string level)
        {
            screens.Add(new GameplayScreen(level, settings, records, levels));
        }

        /// <summary>The stars and the best time of a level; null if it was not finished yet.</summary>
        private (int Stars, float Time)? LevelResult(string level)
        {
            float? best = records.BestTime(level);
            if (!best.HasValue)
                return null;
            if (!pars.TryGetValue(level, out float par))
            {
                using (Stream stream = TitleContainer.OpenStream("Content/Levels/" + level + LevelFile.Extension))
                    par = RaceScore.Par(LevelFile.Load(stream));
                pars[level] = par;
            }
            return (RaceScore.Stars(best.Value, par), best.Value);
        }
    }
}
