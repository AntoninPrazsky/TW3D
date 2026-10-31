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
            Prompts = content.Load<SpriteFont>("Fonts/promptFont");
        }

        /// <summary>Large font of menu items and titles.</summary>
        public SpriteFont Menu { get; }

        /// <summary>Small font with digits and the letters of "fps" only.</summary>
        public SpriteFont Small { get; }

        /// <summary>The glyphs of keys and gamepad buttons (PromptFont), only those listed in its .spritefont file.</summary>
        public SpriteFont Prompts { get; }
    }
}
