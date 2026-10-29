using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FarseerPhysics.Graphics.GamePlay;
using FarseerPhysics.Graphics.ScreenSystem;

namespace TW3D
{

    public class Game1 : Game
    {
        GraphicsDeviceManager graphics;
        
        public ScreenManager ScreenManager { get; set; }

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            //graphics.PreferredBackBufferWidth = 1920;
            //graphics.PreferredBackBufferHeight = 1080;

            graphics.IsFullScreen = true;
            
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

            FarseerPhysics.ConvertUnits.SetDisplayUnitToSimUnitRatio(this.GraphicsDevice.Viewport.Height * (1f / 30f));

            MainGame mainGame = new MainGame();
            mainGame.LevelName = "lock";

            BackgroundScreen bgScreen;
            bgScreen = new BackgroundScreen();

            MenuScreen menuScreen = new MenuScreen("Triple Wheel 3D", this.GraphicsDevice.Viewport.Width);
            SelectLevelScreen selLevScreen = new SelectLevelScreen("Level Select", this.GraphicsDevice.Viewport.Width);
            
            selLevScreen.AddLevelItem("startblock", mainGame);
            selLevScreen.AddLevelItem("lock", mainGame);
            selLevScreen.AddLevelItem("tunel", mainGame);

            //Duplicitní úrovně pro otestování funkčnosti posuvníku
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

            //Není implementováno
            menuScreen.AddMenuItem("Garage", EntryType.Screen, null);
            menuScreen.AddMenuItem("About", EntryType.Screen, null);
            menuScreen.AddMenuItem("Options", EntryType.Screen, null);

            menuScreen.AddMenuItem("Exit", EntryType.ExitItem, null);

            ScreenManager.AddScreen(bgScreen);
            ScreenManager.AddScreen(menuScreen);
            ScreenManager.AddScreen(new LogoScreen(TimeSpan.FromSeconds(1.5)));
        }

        
    }
}
