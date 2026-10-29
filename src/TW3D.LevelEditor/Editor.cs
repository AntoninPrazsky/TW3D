using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using LevelEditor.LevelObjects;
using System;
using System.IO;
using TW3D.Shared;

namespace LevelEditor
{
    public class Editor : Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        Level level;
        Camera3D camera;
        Selector selector;
        //Sky sky;

        public Editor()
            : base()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            graphics.PreferredBackBufferWidth = 1900;
            graphics.PreferredBackBufferHeight = 1000;
            graphics.SynchronizeWithVerticalRetrace = true;
            Window.Title = "Level Editor";
            Window.AllowUserResizing = true;
            //Window.IsBorderless = true;
            //Window.AllowAltF4 = false;
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            base.Initialize();
        }

        protected override void LoadContent()
        {
            //The blocks and dynamic objects come from the catalog shared with the game
            Catalog catalog;
            using (Stream stream = TitleContainer.OpenStream("Content/" + Catalog.FileName))
                catalog = Catalog.Load(stream);

            foreach (BlockType block in catalog.Blocks)
                TrackBlockModels.Models.Add(block.Name, Content.Load<Model>(block.ModelAsset));
            foreach (string model in catalog.ObjectModels)
                TrackPropModels.Models.Add(model, Content.Load<Model>(ObjectType.ModelAsset(model)));

            camera = new Camera3D();
            camera.IsOrtographic = false;
            level = new Level(graphics, camera, catalog, Vector2.Zero, 0.25f);
            selector = new Selector(graphics, camera, Vector2.Zero, 0.25f, Content, level);


            spriteBatch = new SpriteBatch(GraphicsDevice);
            //sky = new Sky(graphics, camera, Content, 1f);
        }

        protected override void UnloadContent() { }

        protected override void Update(GameTime gameTime)
        {
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
            {
                Console.WriteLine("Are you sure you want to quit? Y/N");
                ConsoleKeyInfo ans = Console.ReadKey();
                if (ans.KeyChar == 'Y' || ans.KeyChar == 'y') Exit();
                else Console.WriteLine("\nNot quitting.");
            }

            float timeDifference = (float)gameTime.ElapsedGameTime.TotalMilliseconds / 1000f;
            camera.ProcessInput(timeDifference);
            selector.Update();
            //sky.Update();
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Gray);
            level.Draw();
            selector.Draw();
            //sky.Draw();
            base.Draw(gameTime);
        }
    }
}
