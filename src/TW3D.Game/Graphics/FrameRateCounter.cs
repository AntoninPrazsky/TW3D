using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TW3D.Screens;

namespace TW3D.Graphics
{
    /// <summary>
    /// Shows frames per second in the top left corner, if the settings say so (the Options menu);
    /// F12 switches it too.
    /// </summary>
    public sealed class FrameRateCounter : DrawableGameComponent
    {
        private readonly ScreenManager screens;
        private readonly GameSettings settings;
        private double secondTimer;
        private int framesThisSecond;
        private int framesPerSecond;

        public FrameRateCounter(Game game, ScreenManager screens, GameSettings settings)
            : base(game)
        {
            this.screens = screens;
            this.settings = settings;
        }

        public override void Update(GameTime gameTime)
        {
            //The input of this frame was read by the screens, which update first
            if (screens.Input.KeyPressed(Keys.F12))
            {
                settings.ShowFrameRate = !settings.ShowFrameRate;
                settings.Save();
            }
            Visible = settings.ShowFrameRate;

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

            //The font contains only digits, space and the letters of "fps". Always in the native size of the bitmap font,
            //whatever the screen (small on phones)
            string text = framesPerSecond + " fps";
            const float scale = 1f;
            //In the corner of the safe area, away from the rounded corners of phone screens
            Vector2 position = new Vector2(screens.SafeArea.Left, screens.SafeArea.Top);

            SpriteBatch batch = screens.SpriteBatch;
            batch.Begin();
            batch.DrawString(screens.Fonts.Small, text, position + new Vector2(scale), Color.Black, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            batch.DrawString(screens.Fonts.Small, text, position, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            batch.End();
        }
    }
}
