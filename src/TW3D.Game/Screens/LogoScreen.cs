using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TW3D.Input;

namespace TW3D.Screens
{
    /// <summary>
    /// The logo shown at start-up. It disappears after a while or after any key, button or click,
    /// slowly fading into the screens below it.
    /// </summary>
    public sealed class LogoScreen : Screen
    {
        private readonly TimeSpan duration;
        private TimeSpan shownFor;
        private Texture2D logo;

        public LogoScreen(TimeSpan duration)
        {
            this.duration = duration;
            FadeInTime = TimeSpan.Zero;
            FadeOutTime = TimeSpan.FromSeconds(2.0);
            IsOpaque = true;
        }

        public override void LoadContent()
        {
            logo = Manager.Content.Load<Texture2D>("Common/logo");
            ShowImmediately();
        }

        public override void HandleInput(InputState input, GameTime gameTime)
        {
            if (input.AnyPressed)
                Close();
        }

        public override void Update(GameTime gameTime)
        {
            shownFor += gameTime.ElapsedGameTime;
            if (shownFor >= duration)
                Close();
        }

        public override void Draw(GameTime gameTime)
        {
            Viewport viewport = Manager.Viewport;
            float scale = Manager.UiScale;
            Vector2 size = new Vector2(logo.Width, logo.Height) * scale;
            Vector2 position = (new Vector2(viewport.Width, viewport.Height) - size) / 2f;

            SpriteBatch batch = Manager.SpriteBatch;
            batch.Begin();
            batch.Draw(Manager.Pixel, viewport.Bounds, Color.Black * Visibility);
            batch.Draw(logo, position, null, Color.White * Visibility, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            batch.End();
        }
    }
}
