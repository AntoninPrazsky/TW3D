using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TW3D.GamePlay;
using TW3D.ScreenSystem;

namespace TW3D
{

    public class Game1 : Game
    {
        GraphicsDeviceManager graphics;
        
        public ScreenManager ScreenManager { get; set; }

        private string startLevel;

        public Game1(bool windowed = false, string startLevel = null)
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            this.startLevel = startLevel;

            Window.Title = "Triple Wheel 3D";

            if (windowed)
            {
                graphics.PreferredBackBufferWidth = 1280;
                graphics.PreferredBackBufferHeight = 720;
            }
            else
            {
                //Borderless full-screen window (same as the 2016 desktop version)
                graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
                graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
                graphics.HardwareModeSwitch = false;
                graphics.IsFullScreen = true;
            }

            this.IsMouseVisible = true;
            
            ScreenManager = new ScreenManager(this);
            Components.Add(ScreenManager);
            
            FrameRateCounter frameRateCounter = new FrameRateCounter(ScreenManager);
            frameRateCounter.DrawOrder = 101;
            Components.Add(frameRateCounter);
        }


        protected override void Initialize()
        {
            base.Initialize();

            ConvertUnits.SetDisplayUnitToSimUnitRatio(this.GraphicsDevice.Viewport.Height * (1f / 30f));

            MainGame mainGame = new MainGame();
            mainGame.LevelName = "lock";

            BackgroundScreen bgScreen;
            bgScreen = new BackgroundScreen();

            MenuScreen menuScreen = new MenuScreen("Triple Wheel 3D", this.GraphicsDevice.Viewport.Width);
            SelectLevelScreen selLevScreen = new SelectLevelScreen("Level Select", this.GraphicsDevice.Viewport.Width);
            
            selLevScreen.AddLevelItem("startblock", mainGame);
            selLevScreen.AddLevelItem("lock", mainGame);
            selLevScreen.AddLevelItem("tunel", mainGame);

            //Duplicate levels to test the scroll bar
            selLevScreen.AddLevelItem("lock", mainGame);
            selLevScreen.AddLevelItem("tunel", mainGame);
            selLevScreen.AddLevelItem("startblock", mainGame);
            selLevScreen.AddLevelItem("tunel", mainGame);
            selLevScreen.AddLevelItem("startblock", mainGame);
            selLevScreen.AddLevelItem("lock", mainGame);
            selLevScreen.AddLevelItem("tunel", mainGame);
            selLevScreen.AddLevelItem("startblock", mainGame);
            selLevScreen.AddLevelItem("lock", mainGame);
            selLevScreen.AddLevelItem("lock", mainGame);
            selLevScreen.AddLevelItem("startblock", mainGame);
            selLevScreen.AddLevelItem("tunel", mainGame);

            menuScreen.AddMenuItem("Play", EntryType.Screen, selLevScreen);

            //Not implemented
            menuScreen.AddMenuItem("Garage", EntryType.Screen, null);
            menuScreen.AddMenuItem("About", EntryType.Screen, null);
            menuScreen.AddMenuItem("Options", EntryType.Screen, null);

            menuScreen.AddMenuItem("Exit", EntryType.ExitItem, null);

            ScreenManager.AddScreen(bgScreen);
            ScreenManager.AddScreen(menuScreen);

            if (startLevel != null)
            {
                //Level started directly from the command line (without the logo screen), Esc returns to the menu
                mainGame.LevelName = startLevel;
                ScreenManager.AddScreen(mainGame);
            }
            else
            {
                ScreenManager.AddScreen(new LogoScreen(TimeSpan.FromSeconds(1.5)));
            }
        }

        
    }
}
