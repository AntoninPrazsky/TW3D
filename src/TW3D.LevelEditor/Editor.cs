using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using LevelEditor.LevelObjects;
using System;
using System.IO;
using TW3D.Graphics;
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
        SceneRenderer renderer;
        Model sky;
        KeyboardState previousKeys;
        readonly string startLevel;

        //The editor's background; the 2016 version had no sky
        static readonly Color BackgroundColor = Color.Gray;
        //Where the sky is drawn relative to the camera: as in the game, which draws it around the car
        static readonly Vector3 SkyOffset = new Vector3(0f, -2f, 6.5f);

        /// <param name="startLevel">Level file to open; null = ask for it.</param>
        public Editor(string startLevel = null)
            : base()
        {
            this.startLevel = startLevel;
            graphics = new GraphicsDeviceManager(this);
            graphics.PreferMultiSampling = true;
            graphics.PreparingDeviceSettings += (sender, e) => e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = 4;
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
            level = new Level(graphics, camera, catalog, Vector2.Zero, 0.25f, startLevel);
            selector = new Selector(graphics, camera, Vector2.Zero, 0.25f, Content, level);

            renderer = new SceneRenderer(GraphicsDevice, Content) { ClearColor = BackgroundColor };
            sky = Content.Load<Model>("Env/sky");

            spriteBatch = new SpriteBatch(GraphicsDevice);
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

            //The lighting as in the game (F9: of the 2016 version, F10: shadows)
            KeyboardState keys = Keyboard.GetState();
            if (keys.IsKeyDown(Keys.F9) && previousKeys.IsKeyUp(Keys.F9))
            {
                renderer.Classic = !renderer.Classic;
                Console.WriteLine(renderer.Classic ? "Lighting of the 2016 version." : "Lighting of the game.");
            }
            if (keys.IsKeyDown(Keys.F10) && previousKeys.IsKeyUp(Keys.F10))
            {
                renderer.Shadows = !renderer.Shadows;
                Console.WriteLine(renderer.Shadows ? "Shadows on." : "Shadows off.");
            }
            previousKeys = keys;

            float timeDifference = (float)gameTime.ElapsedGameTime.TotalMilliseconds / 1000f;
            camera.ProcessInput(timeDifference);
            selector.Update();
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(BackgroundColor);

            renderer.Begin(camera.viewMatrix, GameObject.CreateProjection(GraphicsDevice.Viewport, camera.IsOrtographic));
            if (!renderer.Classic)
                renderer.SetBackground(sky, Matrix.CreateScale(0.25f) * Matrix.CreateTranslation(camera.cameraPosition + SkyOffset));
            level.Draw(renderer);
            renderer.End();

            //The selector is a helper of the editor, not a part of the scene
            selector.Draw();
            base.Draw(gameTime);
        }
    }
}
