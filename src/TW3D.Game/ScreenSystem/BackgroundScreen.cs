using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TW3D.ScreenSystem
{
    /// <summary>
    /// The background screen sits behind all the other menu screens.
    /// It draws a background image that remains fixed in place regardless
    /// of whatever transitions the screens on top of it may be doing.
    /// </summary>
    public class BackgroundScreen : GameScreen
    {
        private Texture2D _backgroundTexture;
        private Rectangle _viewport, _crop;
        
        /// <summary>
        /// Constructor.
        /// </summary>
        public BackgroundScreen()
        {
            TransitionOnTime = TimeSpan.FromSeconds(0.5);
            TransitionOffTime = TimeSpan.FromSeconds(0.5);
        }

        public override void LoadContent()
        {
            _backgroundTexture = ScreenManager.Content.Load<Texture2D>("Common/garage");

            Viewport viewport = ScreenManager.GraphicsDevice.Viewport;
            _viewport = viewport.Bounds;
            
            int _cropSize;
            //Obrázek na pozadí je širokoúhlejší než poměr stran zobrazení (bude oříznut zleva a zprava)
            if (((float)_backgroundTexture.Width / (float)_backgroundTexture.Height) > ((float)_viewport.Width / (float)_viewport.Height))
            { 
                _cropSize = (int)(((float)_viewport.Width / (float)_viewport.Height) * (float)_backgroundTexture.Height);
                _crop = new Rectangle((_backgroundTexture.Width - _cropSize) / 2, 0, _cropSize, _backgroundTexture.Height);
            }
            //Obrázek na pozadí je méně širokoúhlý než poměr stran zobrazení (bude oříznut shora a zespodu)
            else
            {
                _cropSize = (int)(((float)_viewport.Height / (float)_viewport.Width) * (float)_backgroundTexture.Width);
                _crop = new Rectangle(0, (_backgroundTexture.Height - _cropSize) / 2, _backgroundTexture.Width, _cropSize);
            }
        }

        /// <summary>
        /// Updates the background screen. Unlike most screens, this should not
        /// transition off even if it has been covered by another screen: it is
        /// supposed to be covered, after all! This overload forces the
        /// coveredByOtherScreen parameter to false in order to stop the base
        /// Update method wanting to transition off.
        /// </summary>
        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
                base.Update(gameTime, otherScreenHasFocus, false);
        }

        /// <summary>
        /// Draws the background screen.
        /// </summary>
        public override void Draw(GameTime gameTime)
        {
            
            ScreenManager.SpriteBatch.Begin();
            ScreenManager.SpriteBatch.Draw(_backgroundTexture, _viewport, _crop, Color.White);
            ScreenManager.SpriteBatch.End();
        
        }
    }
}