using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TW3D.Screens
{
    public sealed class GameFonts
    {
        public GameFonts(ContentManager content)
        {
            Menu = content.Load<SpriteFont>("Fonts/menuFont");
            Small = content.Load<SpriteFont>("Fonts/frameRateCounterFont");
        }

        /// <summary>Large font of menu items and titles.</summary>
        public SpriteFont Menu { get; }

        /// <summary>Small font with digits and the letters of "fps" only.</summary>
        public SpriteFont Small { get; }
    }
}
