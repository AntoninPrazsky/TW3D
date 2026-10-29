using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TW3D.Screens
{
    /// <summary>
    /// The garage image behind the menus. It fills the whole screen and is cropped
    /// to the screen's aspect ratio, so it is never stretched.
    /// </summary>
    public sealed class BackgroundScreen : Screen
    {
        private Texture2D texture;

        public BackgroundScreen()
        {
            StaysVisibleWhenCovered = true;
            IsOpaque = true;
        }

        public override void LoadContent()
        {
            texture = Manager.Content.Load<Texture2D>("Common/garage");
        }

        public override void Draw(GameTime gameTime)
        {
            Viewport viewport = Manager.Viewport;
            float screenAspect = (float)viewport.Width / viewport.Height;
            float imageAspect = (float)texture.Width / texture.Height;

            //Crop the sides of an image wider than the screen, or the top and bottom of a narrower one
            Rectangle source;
            if (imageAspect > screenAspect)
            {
                int width = (int)(texture.Height * screenAspect);
                source = new Rectangle((texture.Width - width) / 2, 0, width, texture.Height);
            }
            else
            {
                int height = (int)(texture.Width / screenAspect);
                source = new Rectangle(0, (texture.Height - height) / 2, texture.Width, height);
            }

            Manager.SpriteBatch.Begin();
            Manager.SpriteBatch.Draw(texture, viewport.Bounds, source, Color.White * Visibility);
            Manager.SpriteBatch.End();
        }
    }
}
