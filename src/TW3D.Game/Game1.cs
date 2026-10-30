using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TW3D.GamePlay;
using TW3D.Graphics;
using TW3D.Screens;
using TW3D.Shared;

namespace TW3D
{
    public class Game1 : Game
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

        /// <param name="windowSize">Size of the window; null = borderless full screen.</param>
        /// <param name="startLevel">Level to start directly, skipping the menu; null = start with the menu.</param>
        public Game1(Point? windowSize = null, string startLevel = null)
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

            if (windowSize.HasValue)
            {
                graphics.PreferredBackBufferWidth = windowSize.Value.X;
                graphics.PreferredBackBufferHeight = windowSize.Value.Y;
                //The user interface adapts to any window size
                Window.AllowUserResizing = true;
                Window.ClientSizeChanged += OnClientSizeChanged;
            }
            else
            {
                //Borderless full-screen window (same as the 2016 desktop version)
                graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
                graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
                graphics.HardwareModeSwitch = false;
                graphics.IsFullScreen = true;
            }
#endif

            screens = new ScreenManager(this);
#if ANDROID
            screens.UiSize = PhoneUiSize;
#endif
            Components.Add(screens);
            Components.Add(new FrameRateCounter(this, screens, settings) { DrawOrder = 100 });
        }

        private void OnClientSizeChanged(object sender, EventArgs e)
        {
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

            MainMenuScreen menu = new MainMenuScreen("Triple Wheel 3D");
            menu.AddItem("Play", () => screens.Add(new LevelSelectScreen("Level Select", FindLevels(), StartLevel)));
            //Not implemented
            menu.AddItem("Garage", null);
            menu.AddItem("About", null);
            menu.AddItem("Options", () => screens.Add(new OptionsScreen(settings)));
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
