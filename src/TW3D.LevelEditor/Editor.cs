using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using LevelEditor.LevelObjects;
using TW3D.Graphics;
using TW3D.Shared;

namespace LevelEditor
{
    /// <summary>
    /// The level editor: the level drawn as in the game, a free camera and the selector, which edits the level by the
    /// keys (see <see cref="Selector"/>). The console window is its command line.
    /// </summary>
    public class Editor : Game
    {
        //The editor's background; the 2016 version had no sky
        private static readonly Color BackgroundColor = Color.Gray;
        //Where the sky is drawn relative to the camera: as in the game, which draws it around the car
        private static readonly Vector3 SkyOffset = new Vector3(0f, -2f, 6.5f);
        //The editor's camera is about 15 units further from the level than the game's, the haze starts so much later
        private const float HazeOffset = 15f;

        private readonly GraphicsDeviceManager graphics;
        private readonly string startLevel;
        private Level level;
        private Camera3D camera;
        private Selector selector;
        private SceneRenderer renderer;
        private Model sky;
        private KeyboardState previousKeys;

        /// <param name="startLevel">Level file to open; null = ask for it.</param>
        public Editor(string startLevel = null)
        {
            this.startLevel = startLevel;
            graphics = new GraphicsDeviceManager(this);
            graphics.PreferMultiSampling = true;
            graphics.PreparingDeviceSettings += (sender, e) => e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = 4;
            graphics.PreferredBackBufferWidth = 1900;
            graphics.PreferredBackBufferHeight = 1000;
            graphics.SynchronizeWithVerticalRetrace = true;
            Content.RootDirectory = "Content";
            Window.Title = "Level Editor";
            Window.AllowUserResizing = true;
            IsMouseVisible = true;
        }

        protected override void LoadContent()
        {
            //The blocks and dynamic objects come from the catalog shared with the game
            Catalog catalog;
            using (Stream stream = TitleContainer.OpenStream("Content/" + Catalog.FileName))
                catalog = Catalog.Load(stream);

            camera = new Camera3D();
            level = new Level(catalog, Content, startLevel);
            selector = new Selector(graphics, camera, Content, level);

            renderer = new SceneRenderer(GraphicsDevice, Content) { ClearColor = BackgroundColor, HazeOffset = HazeOffset };
            sky = Content.Load<Model>("Env/sky");
        }

        protected override void Update(GameTime gameTime)
        {
            KeyboardState keys = Keyboard.GetState();
            if (keys.IsKeyDown(Keys.Escape))
            {
                Console.WriteLine("Are you sure you want to quit? Y/N");
                ConsoleKeyInfo answer = Console.ReadKey();
                if (answer.KeyChar == 'Y' || answer.KeyChar == 'y')
                    Exit();
                else
                    Console.WriteLine("\nNot quitting.");
            }

            //The lighting as in the game (F9: of the 2016 version, F10: shadows)
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
            //The mood of the level (day, sunset, night), saved with it
            if (keys.IsKeyDown(Keys.M) && previousKeys.IsKeyUp(Keys.M))
            {
                level.Mood = (LevelMood)(((int)level.Mood + 1) % 3);
                Console.WriteLine("Mood of the level: " + level.Mood + ".");
            }
            previousKeys = keys;

            camera.ProcessInput((float)gameTime.ElapsedGameTime.TotalSeconds);
            selector.Update();
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(BackgroundColor);

            renderer.Lighting = Lighting.For(level.Mood);
            renderer.Begin(camera.View, GameObject.CreateProjection(GraphicsDevice.Viewport, camera.IsOrthographic));
            if (!renderer.Classic)
                renderer.SetBackground(sky, Matrix.CreateScale(Units.ModelToScene) * Matrix.CreateTranslation(camera.Position + SkyOffset));
            level.Draw(renderer);
            renderer.End();

            //The selector is a helper of the editor, not a part of the scene
            selector.Draw();
            base.Draw(gameTime);
        }
    }
}
