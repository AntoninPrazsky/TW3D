using System.Collections.Generic;
using System.Text;
using FarseerPhysics.Common;
using FarseerPhysics.Common.Decomposition;
using FarseerPhysics.Common.PolygonManipulation;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Joints;
using FarseerPhysics.Factories;
using FarseerPhysics.Graphics.ScreenSystem;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using FarseerPhysics.DebugView;
using FarseerPhysics.Collision.Shapes;
using System;
using FarseerPhysics.Graphics.DrawingSystem;
using TW3D.GameObjects;

namespace FarseerPhysics.Graphics.GamePlay
{
    internal class MainGame : PhysicsGameScreen, IGameScreen
    {
        Car car;
        Level level;
        Sky sky;
        private string levelName = "lock";
        
        private const float _scale = 0.25f;
        
        #region IDemoScreen Members

        public string GetTitle()
        {
            return "TW3D Game Window";
        }

        public string LevelName
        {
            get { return levelName; }
            set { levelName = value; }
        }

        public string GetDetails()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Welcome, betatesters!");

            return sb.ToString();
        }

        #endregion
        public override void LoadContent()
        {
            base.LoadContent();
            
            HasVirtualStick = true;
            EnableCameraControl = true;

            Settings.ContinuousPhysics = false;
            Settings.VelocityIterations = 6;
            Settings.PositionIterations = 2;
            
            if (TrackBlockModels.Models.Count == 0) {
            TrackBlockModels.Models.Add("a1", ScreenManager.Content.Load<Model>("Levels/mod/a1"));
            TrackBlockModels.Models.Add("b1", ScreenManager.Content.Load<Model>("Levels/mod/b1"));
            TrackBlockModels.Models.Add("b2", ScreenManager.Content.Load<Model>("Levels/mod/b2"));
            TrackBlockModels.Models.Add("b3", ScreenManager.Content.Load<Model>("Levels/mod/b3"));
            TrackBlockModels.Models.Add("b4", ScreenManager.Content.Load<Model>("Levels/mod/b4"));
            TrackBlockModels.Models.Add("c1", ScreenManager.Content.Load<Model>("Levels/mod/c1"));
            TrackBlockModels.Models.Add("c2", ScreenManager.Content.Load<Model>("Levels/mod/c2"));
            TrackBlockModels.Models.Add("c3", ScreenManager.Content.Load<Model>("Levels/mod/c3"));

            TrackBlockModels.Models.Add("b1f", ScreenManager.Content.Load<Model>("Levels/mod/b1f"));
            TrackBlockModels.Models.Add("b2f", ScreenManager.Content.Load<Model>("Levels/mod/b2f"));
            TrackBlockModels.Models.Add("b3f", ScreenManager.Content.Load<Model>("Levels/mod/b3f"));
            TrackBlockModels.Models.Add("b4f", ScreenManager.Content.Load<Model>("Levels/mod/b4f"));
            TrackBlockModels.Models.Add("c1f", ScreenManager.Content.Load<Model>("Levels/mod/c1f"));
            TrackBlockModels.Models.Add("c2f", ScreenManager.Content.Load<Model>("Levels/mod/c2f"));
            TrackBlockModels.Models.Add("c3f", ScreenManager.Content.Load<Model>("Levels/mod/c3f"));

            TrackBlockModels.Models.Add("s1", ScreenManager.Content.Load<Model>("Levels/mod/s1"));
            TrackBlockModels.Models.Add("s1f", ScreenManager.Content.Load<Model>("Levels/mod/s1f"));

            TrackBlockModels.Models.Add("xa1", ScreenManager.Content.Load<Model>("Levels/mod/xa1"));

            TrackBlockModels.Models.Add("d1", ScreenManager.Content.Load<Model>("Levels/mod/d1"));
            TrackBlockModels.Models.Add("d1f", ScreenManager.Content.Load<Model>("Levels/mod/d1f"));
            TrackBlockModels.Models.Add("d2", ScreenManager.Content.Load<Model>("Levels/mod/d2"));
            TrackBlockModels.Models.Add("d2f", ScreenManager.Content.Load<Model>("Levels/mod/d2f"));
            TrackBlockModels.Models.Add("d3", ScreenManager.Content.Load<Model>("Levels/mod/d3"));
            TrackBlockModels.Models.Add("d3f", ScreenManager.Content.Load<Model>("Levels/mod/d3f"));

            TrackBlockModels.Models.Add("kea1a", ScreenManager.Content.Load<Model>("Levels/mod/kea1a"));
            TrackBlockModels.Models.Add("kea1b", ScreenManager.Content.Load<Model>("Levels/mod/kea1b"));
            TrackBlockModels.Models.Add("kea1c", ScreenManager.Content.Load<Model>("Levels/mod/kea1c"));
            TrackBlockModels.Models.Add("kea1d", ScreenManager.Content.Load<Model>("Levels/mod/kea1d"));
            TrackBlockModels.Models.Add("kea1e", ScreenManager.Content.Load<Model>("Levels/mod/kea1e"));
            TrackBlockModels.Models.Add("kea1f", ScreenManager.Content.Load<Model>("Levels/mod/kea1f"));

            TrackBlockModels.Models.Add("xa2", ScreenManager.Content.Load<Model>("Levels/mod/xa2"));
            TrackBlockModels.Models.Add("xa3", ScreenManager.Content.Load<Model>("Levels/mod/xa3"));
            TrackBlockModels.Models.Add("xa4", ScreenManager.Content.Load<Model>("Levels/mod/xa4"));
            TrackBlockModels.Models.Add("xa5", ScreenManager.Content.Load<Model>("Levels/mod/xa5"));
            TrackBlockModels.Models.Add("xa6", ScreenManager.Content.Load<Model>("Levels/mod/xa6"));
            TrackBlockModels.Models.Add("xa7", ScreenManager.Content.Load<Model>("Levels/mod/xa7"));

            TrackBlockModels.Models.Add("ba1", ScreenManager.Content.Load<Model>("Levels/mod/ba1"));
            TrackBlockModels.Models.Add("ba2", ScreenManager.Content.Load<Model>("Levels/mod/ba2"));

            TrackBlockModels.Models.Add("tu1s", ScreenManager.Content.Load<Model>("Levels/mod/tu1s"));
            TrackBlockModels.Models.Add("tu2s", ScreenManager.Content.Load<Model>("Levels/mod/tu2s"));
            TrackBlockModels.Models.Add("tu3s", ScreenManager.Content.Load<Model>("Levels/mod/tu3s"));
            TrackBlockModels.Models.Add("tu1v", ScreenManager.Content.Load<Model>("Levels/mod/tu1v"));
            TrackBlockModels.Models.Add("tu2v", ScreenManager.Content.Load<Model>("Levels/mod/tu2v"));
            TrackBlockModels.Models.Add("tu3v", ScreenManager.Content.Load<Model>("Levels/mod/tu3v"));

            TrackBlockModels.Models.Add("sb", ScreenManager.Content.Load<Model>("Levels/mod/sb"));
            TrackBlockModels.Models.Add("sg", ScreenManager.Content.Load<Model>("Levels/mod/sg"));
            TrackBlockModels.Models.Add("st", ScreenManager.Content.Load<Model>("Levels/mod/st"));
            TrackBlockModels.Models.Add("st2", ScreenManager.Content.Load<Model>("Levels/mod/st2"));
            TrackBlockModels.Models.Add("g", ScreenManager.Content.Load<Model>("Levels/mod/g"));

            TrackBlockModels.Models.Add("u1", ScreenManager.Content.Load<Model>("Levels/mod/u1"));
            TrackBlockModels.Models.Add("u2", ScreenManager.Content.Load<Model>("Levels/mod/u2"));
            TrackBlockModels.Models.Add("t1", ScreenManager.Content.Load<Model>("Levels/mod/t1"));
            TrackBlockModels.Models.Add("t2", ScreenManager.Content.Load<Model>("Levels/mod/t2"));

            TrackBlockModels.Models.Add("bl1", ScreenManager.Content.Load<Model>("Levels/mod/bl1"));
            TrackBlockModels.Models.Add("bl1s", ScreenManager.Content.Load<Model>("Levels/mod/bl1s"));
            TrackBlockModels.Models.Add("bl2", ScreenManager.Content.Load<Model>("Levels/mod/bl2"));
            TrackBlockModels.Models.Add("bl2s", ScreenManager.Content.Load<Model>("Levels/mod/bl2s"));
            TrackBlockModels.Models.Add("bl2f", ScreenManager.Content.Load<Model>("Levels/mod/bl2f"));
            TrackBlockModels.Models.Add("bl2sf", ScreenManager.Content.Load<Model>("Levels/mod/bl2sf"));

            TrackBlockModels.Models.Add("lo", ScreenManager.Content.Load<Model>("Levels/mod/lo"));

            TrackBlockModels.Models.Add("za", ScreenManager.Content.Load<Model>("Levels/mod/za"));


            TrackPropModels.Models.Add("box", ScreenManager.Content.Load<Model>("Env/box"));
            TrackPropModels.Models.Add("bar1", ScreenManager.Content.Load<Model>("Env/bar1"));
            TrackPropModels.Models.Add("bar2", ScreenManager.Content.Load<Model>("Env/bar2"));

            TrackPropModels.Models.Add("tb1", ScreenManager.Content.Load<Model>("Env/tb1"));

            TrackPropModels.Models.Add("rm", ScreenManager.Content.Load<Model>("Env/rm"));

            TrackPropModels.Models.Add("co", ScreenManager.Content.Load<Model>("Env/co"));
            TrackPropModels.Models.Add("co2", ScreenManager.Content.Load<Model>("Env/co2"));

            TrackPropModels.Models.Add("mov1", ScreenManager.Content.Load<Model>("Env/mov1"));

            }

            level = new Level(World, ScreenManager, Camera, Vector2.Zero, levelName);

            sky = new Sky(ScreenManager, Camera);

            World.Gravity = new Vector2(0f, 12f);
            
            car = new Car(World, ScreenManager, Camera, new Vector2(-1.6f, -7f), "car", "wheel");
            
            Camera.MinRotation = -0.5f;
            Camera.MaxRotation = 0.5f;
        }

        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            car.Update();
            level.Update();
            base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);
        }

        public override void HandleInput(InputHelper input, GameTime gameTime)
        {
            if (input.VirtualState.ThumbSticks.Left.X > 0.2f)
                car.acceleration = input.VirtualState.ThumbSticks.Left.X * 5f;
            else if (input.VirtualState.ThumbSticks.Left.X < -0.2f)
                car.acceleration = input.VirtualState.ThumbSticks.Left.X * 5f;
            else
                car.acceleration -= Math.Sign(car.acceleration) * (float)(2 * gameTime.ElapsedGameTime.TotalSeconds);
            
            // HOLDERS ROTATION
            if (input.VirtualState.ThumbSticks.Left.Y < -0.7f || input.VirtualState.ThumbSticks.Left.Y > 0.7f)
            {
                car.holderRotationDir = (input.VirtualState.ThumbSticks.Left.Y > 0.7f);
                car.holderRotation = true;
            }
            else
            {
                car.holderRotation = false;
            }
            //--HOLDERS ROTATION

            car.brake = (input.VirtualState.Buttons.A == ButtonState.Pressed);
            car.holderRotLockChnage = (input.VirtualState.Buttons.B == ButtonState.Pressed);
            
            if(car.IsIntersected(input.Cursor.X, input.Cursor.Y))
            {
                base.Handle3DCursor(input, car.car);
            }

            base.HandleInput(input, gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            car.Draw();
            sky.Draw();
            level.Draw();
            base.Draw(gameTime);
        }
    }
}