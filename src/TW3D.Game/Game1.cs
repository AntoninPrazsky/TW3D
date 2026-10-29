using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TW3D.GamePlay;
using TW3D.Graphics;
using TW3D.Screens;

namespace TW3D
{
    public class Game1 : Game
    {
        private static readonly string[] Levels = { "startblock", "lock", "tunel" };

        private readonly GraphicsDeviceManager graphics;
        private readonly ScreenManager screens;
        private readonly string startLevel;

        /// <param name="windowSize">Size of the window; null = borderless full screen.</param>
        /// <param name="startLevel">Level to start directly, skipping the menu; null = start with the menu.</param>
        public Game1(Point? windowSize = null, string startLevel = null)
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            this.startLevel = startLevel;

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

            screens = new ScreenManager(this);
            Components.Add(screens);
            Components.Add(new FrameRateCounter(this, screens) { DrawOrder = 100 });
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
            menu.AddItem("Play", () => screens.Add(new LevelSelectScreen("Level Select", Levels, StartLevel)));
            //Not implemented
            menu.AddItem("Garage", null);
            menu.AddItem("About", null);
            menu.AddItem("Options", null);
            menu.AddItem("Exit", Exit);

            screens.Add(new BackgroundScreen());
            screens.Add(menu);

            if (startLevel != null)
                //Level started directly from the command line (without the logo screen), Esc returns to the menu
                StartLevel(startLevel);
            else
                screens.Add(new LogoScreen(TimeSpan.FromSeconds(1.5)));
        }

        private void StartLevel(string level)
        {
            screens.Add(new GameplayScreen(level));
        }
    }
}
