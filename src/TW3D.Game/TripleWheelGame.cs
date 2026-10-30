using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
        //Samples per pixel of the multisample antialiasing; fewer on phones, where it costs more
        private const int MultiSampleCount = 4;
        //A phone screen is small, so the user interface is bigger than on a monitor (see ScreenManager.UiSize)
        private const float PhoneUiSize = 2f;
#else
        //Samples per pixel of the multisample antialiasing
        private const int MultiSampleCount = 8;
#endif

        private readonly GraphicsDeviceManager graphics;
        private readonly ScreenManager screens;
        private readonly string startLevel;
        private readonly GraphicsSettings settings = GraphicsSettings.Load();
#if !ANDROID
        //The window size given on the command line (a window always, whatever the settings say)
        private readonly Point? windowSize;
#endif

        /// <param name="windowSize">Size of the window; null = by the settings (full screen, or a maximized window).</param>
        /// <param name="startLevel">Level to start directly, skipping the menu; null = start with the menu.</param>
        public TripleWheelGame(Point? windowSize = null, string startLevel = null)
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            this.startLevel = startLevel;

            //One update and one frame per refresh of the monitor (e.g. 75 per second on a 75 Hz monitor);
            //the physics simulation keeps its own fixed time step (see PhysicsClock)
            IsFixedTimeStep = false;
            graphics.SynchronizeWithVerticalRetrace = true;

            //Multisample antialiasing; if the graphics card does not support this many samples, MonoGame uses fewer
            graphics.PreferMultiSampling = true;
            graphics.PreparingDeviceSettings += (sender, e) =>
                e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = MultiSampleCount;

#if ANDROID
            //Full screen, landscape either way round
            graphics.IsFullScreen = true;
            graphics.SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight;
#else
            Window.Title = "Triple Wheel 3D";
            IsMouseVisible = true;

            this.windowSize = windowSize;
            SetDisplayMode();
            //The user interface adapts to any window size
            Window.ClientSizeChanged += OnClientSizeChanged;
#endif

            screens = new ScreenManager(this);
#if ANDROID
            screens.UiSize = PhoneUiSize;
#endif
            Components.Add(screens);
            Components.Add(new FrameRateCounter(this, screens, settings) { DrawOrder = 100 });
        }

#if !ANDROID
        /// <summary>
        /// Full screen or a window, by the settings (a window if its size was given on the command line).
        /// The window can be resized; it opens maximized.
        /// </summary>
        /// <remarks>
        /// Full screen is a borderless window of the size of the screen (same as the 2016 desktop version).
        /// With some graphics drivers it stutters with vsync (measured on an AMD Radeon with OpenGL: now and
        /// then a frame takes 50–80 ms, and the desktop stutters too), while a window does not.
        /// </remarks>
        private void SetDisplayMode()
        {
            DisplayMode display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            bool fullScreen = settings.FullScreen && !windowSize.HasValue;
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
            SetDisplayMode();
            graphics.ApplyChanges();
            MaximizeWindow();
        }

        //MonoGame cannot maximize its window, but the window is a window of SDL
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
#if !ANDROID
            MaximizeWindow();
#endif

            MainMenuScreen menu = new MainMenuScreen("Triple Wheel 3D");
            menu.AddItem("Play", () => screens.Add(new LevelSelectScreen("Level Select", FindLevels(), StartLevel)));
            //Not implemented
            menu.AddItem("Garage", null);
            menu.AddItem("About", null);
#if ANDROID
            menu.AddItem("Options", () => screens.Add(new OptionsScreen(settings, null)));
#else
            menu.AddItem("Options", () => screens.Add(new OptionsScreen(settings, ApplyDisplayMode)));
#endif
            menu.AddItem("Exit", Exit);

            screens.Add(new BackgroundScreen());
            screens.Add(menu);

            if (startLevel != null)
                //Level started directly from the command line (without the logo screen), Esc returns to the menu
                StartLevel(startLevel);
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

        private void StartLevel(string level)
        {
            screens.Add(new GameplayScreen(level, settings));
        }
    }
}
