using Microsoft.Xna.Framework;
using TW3D.GameObjects;

namespace TW3D.Screens
{
    /// <summary>
    /// The 3D garage behind the menus, with the car chosen in the garage on its lift (see <see cref="GarageScene"/>).
    /// </summary>
    public sealed class BackgroundScreen : Screen
    {
        private readonly GameSettings settings;

        public BackgroundScreen(GameSettings settings)
        {
            this.settings = settings;
            StaysVisibleWhenCovered = true;
            IsOpaque = true;
        }

        /// <summary>The garage; the garage screen moves its camera and its row of cars.</summary>
        public GarageScene Scene { get; private set; }

        public override void LoadContent()
        {
            Scene = new GarageScene(Manager, settings);
        }

        public override void Update(GameTime gameTime)
        {
            Scene.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        }

        public override void Draw(GameTime gameTime)
        {
            Scene.Draw();

            //Fading in from black
            if (Visibility < 1f)
            {
                Manager.SpriteBatch.Begin();
                Manager.SpriteBatch.Draw(Manager.Pixel, Manager.Viewport.Bounds, Color.Black * (1f - Visibility));
                Manager.SpriteBatch.End();
            }
        }
    }
}
