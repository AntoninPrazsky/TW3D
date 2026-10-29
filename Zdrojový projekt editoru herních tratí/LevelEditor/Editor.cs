using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using LevelEditor.LevelObjects;
using System;

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
            TrackBlockModels.Models.Add("a1", Content.Load<Model>("Levels/mod/a1"));
            TrackBlockModels.Models.Add("b1", Content.Load<Model>("Levels/mod/b1"));
            TrackBlockModels.Models.Add("b2", Content.Load<Model>("Levels/mod/b2"));
            TrackBlockModels.Models.Add("b3", Content.Load<Model>("Levels/mod/b3"));
            TrackBlockModels.Models.Add("b4", Content.Load<Model>("Levels/mod/b4"));
            TrackBlockModels.Models.Add("c1", Content.Load<Model>("Levels/mod/c1"));
            TrackBlockModels.Models.Add("c2", Content.Load<Model>("Levels/mod/c2"));
            TrackBlockModels.Models.Add("c3", Content.Load<Model>("Levels/mod/c3"));

            TrackBlockModels.Models.Add("b1f", Content.Load<Model>("Levels/mod/b1f"));
            TrackBlockModels.Models.Add("b2f", Content.Load<Model>("Levels/mod/b2f"));
            TrackBlockModels.Models.Add("b3f", Content.Load<Model>("Levels/mod/b3f"));
            TrackBlockModels.Models.Add("b4f", Content.Load<Model>("Levels/mod/b4f"));
            TrackBlockModels.Models.Add("c1f", Content.Load<Model>("Levels/mod/c1f"));
            TrackBlockModels.Models.Add("c2f", Content.Load<Model>("Levels/mod/c2f"));
            TrackBlockModels.Models.Add("c3f", Content.Load<Model>("Levels/mod/c3f"));

            TrackBlockModels.Models.Add("s1", Content.Load<Model>("Levels/mod/s1"));
            TrackBlockModels.Models.Add("s1f", Content.Load<Model>("Levels/mod/s1f"));

            TrackBlockModels.Models.Add("xa1", Content.Load<Model>("Levels/mod/xa1"));

            TrackBlockModels.Models.Add("d1", Content.Load<Model>("Levels/mod/d1"));
            TrackBlockModels.Models.Add("d1f", Content.Load<Model>("Levels/mod/d1f"));
            TrackBlockModels.Models.Add("d2", Content.Load<Model>("Levels/mod/d2"));
            TrackBlockModels.Models.Add("d2f", Content.Load<Model>("Levels/mod/d2f"));
            TrackBlockModels.Models.Add("d3", Content.Load<Model>("Levels/mod/d3"));
            TrackBlockModels.Models.Add("d3f", Content.Load<Model>("Levels/mod/d3f"));
            
            TrackBlockModels.Models.Add("kea1a", Content.Load<Model>("Levels/mod/kea1a"));
            TrackBlockModels.Models.Add("kea1b", Content.Load<Model>("Levels/mod/kea1b"));
            TrackBlockModels.Models.Add("kea1c", Content.Load<Model>("Levels/mod/kea1c"));
            TrackBlockModels.Models.Add("kea1d", Content.Load<Model>("Levels/mod/kea1d"));
            TrackBlockModels.Models.Add("kea1e", Content.Load<Model>("Levels/mod/kea1e"));
            TrackBlockModels.Models.Add("kea1f", Content.Load<Model>("Levels/mod/kea1f"));
            
            TrackBlockModels.Models.Add("xa2", Content.Load<Model>("Levels/mod/xa2"));
            TrackBlockModels.Models.Add("xa3", Content.Load<Model>("Levels/mod/xa3"));
            TrackBlockModels.Models.Add("xa4", Content.Load<Model>("Levels/mod/xa4"));
            TrackBlockModels.Models.Add("xa5", Content.Load<Model>("Levels/mod/xa5"));
            TrackBlockModels.Models.Add("xa6", Content.Load<Model>("Levels/mod/xa6"));
            TrackBlockModels.Models.Add("xa7", Content.Load<Model>("Levels/mod/xa7"));

            TrackBlockModels.Models.Add("ba1", Content.Load<Model>("Levels/mod/ba1"));
            TrackBlockModels.Models.Add("ba2", Content.Load<Model>("Levels/mod/ba2"));

            TrackBlockModels.Models.Add("tu1s", Content.Load<Model>("Levels/mod/tu1s"));
            TrackBlockModels.Models.Add("tu2s", Content.Load<Model>("Levels/mod/tu2s"));
            TrackBlockModels.Models.Add("tu3s", Content.Load<Model>("Levels/mod/tu3s"));
            TrackBlockModels.Models.Add("tu1v", Content.Load<Model>("Levels/mod/tu1v"));
            TrackBlockModels.Models.Add("tu2v", Content.Load<Model>("Levels/mod/tu2v"));
            TrackBlockModels.Models.Add("tu3v", Content.Load<Model>("Levels/mod/tu3v"));

            TrackBlockModels.Models.Add("sb", Content.Load<Model>("Levels/mod/sb"));
            TrackBlockModels.Models.Add("sg", Content.Load<Model>("Levels/mod/sg"));
            TrackBlockModels.Models.Add("st", Content.Load<Model>("Levels/mod/st"));
            TrackBlockModels.Models.Add("st2", Content.Load<Model>("Levels/mod/st2"));
            TrackBlockModels.Models.Add("g", Content.Load<Model>("Levels/mod/g"));

            TrackBlockModels.Models.Add("u1", Content.Load<Model>("Levels/mod/u1"));
            TrackBlockModels.Models.Add("u2", Content.Load<Model>("Levels/mod/u2"));
            TrackBlockModels.Models.Add("t1", Content.Load<Model>("Levels/mod/t1"));
            TrackBlockModels.Models.Add("t2", Content.Load<Model>("Levels/mod/t2"));

            TrackBlockModels.Models.Add("bl1", Content.Load<Model>("Levels/mod/bl1"));
            TrackBlockModels.Models.Add("bl1s", Content.Load<Model>("Levels/mod/bl1s"));
            TrackBlockModels.Models.Add("bl2", Content.Load<Model>("Levels/mod/bl2"));
            TrackBlockModels.Models.Add("bl2s", Content.Load<Model>("Levels/mod/bl2s"));
            TrackBlockModels.Models.Add("bl2f", Content.Load<Model>("Levels/mod/bl2f"));
            TrackBlockModels.Models.Add("bl2sf", Content.Load<Model>("Levels/mod/bl2sf"));
            TrackBlockModels.Models.Add("za", Content.Load<Model>("Levels/mod/za"));

            TrackBlockModels.Models.Add("lo", Content.Load<Model>("Levels/mod/lo"));

            //ENV:

            TrackPropModels.Models.Add("box", Content.Load<Model>("Env/box"));
            TrackPropModels.Models.Add("bar1", Content.Load<Model>("Env/bar1"));
            TrackPropModels.Models.Add("bar2", Content.Load<Model>("Env/bar2"));

            TrackPropModels.Models.Add("tb1", Content.Load<Model>("Env/tb1"));

            TrackPropModels.Models.Add("rm", Content.Load<Model>("Env/rm"));

            TrackPropModels.Models.Add("co", Content.Load<Model>("Env/co"));
            TrackPropModels.Models.Add("co2", Content.Load<Model>("Env/co2"));

            TrackPropModels.Models.Add("mov1", Content.Load<Model>("Env/mov1"));
                        
            camera = new Camera3D();
            camera.IsOrtographic = false;
            level = new Level(graphics, camera, Vector2.Zero, "fajl", 0.25f);
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
