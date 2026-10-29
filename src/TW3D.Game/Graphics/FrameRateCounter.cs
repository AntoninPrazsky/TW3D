using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TW3D.Screens;

namespace TW3D.Graphics
{
    /// <summary>
    /// Shows frames per second in the top left corner. F12 toggles it;
    /// it is visible by default in Debug builds only.
    /// </summary>
    public sealed class FrameRateCounter : DrawableGameComponent
    {
        private readonly ScreenManager screens;
        private double secondTimer;
        private int framesThisSecond;
        private int framesPerSecond;
        private bool f12WasDown;

        public FrameRateCounter(Game game, ScreenManager screens)
            : base(game)
        {
            this.screens = screens;
#if DEBUG
            Visible = true;
#else
            Visible = false;
#endif
        }

        public override void Update(GameTime gameTime)
        {
            bool f12Down = Keyboard.GetState().IsKeyDown(Keys.F12);
            if (f12Down && !f12WasDown)
                Visible = !Visible;
            f12WasDown = f12Down;

            secondTimer += gameTime.ElapsedGameTime.TotalSeconds;
            if (secondTimer >= 1.0)
            {
                secondTimer -= 1.0;
                framesPerSecond = framesThisSecond;
                framesThisSecond = 0;
            }
        }

        public override void Draw(GameTime gameTime)
        {
            framesThisSecond++;

            //The font contains only digits, space and the letters of "fps"
            string text = framesPerSecond + " fps";
            float scale = screens.UiScale;
            Vector2 position = new Vector2(30f, 25f) * scale;

            SpriteBatch batch = screens.SpriteBatch;
            batch.Begin();
            batch.DrawString(screens.Fonts.Small, text, position + new Vector2(scale), Color.Black, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            batch.DrawString(screens.Fonts.Small, text, position, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            batch.End();
        }
    }
}
