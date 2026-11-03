using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TW3D.Screens
{
    public sealed class GameFonts
    {
        public GameFonts(ContentManager content)
        {
            Menu = new GameFont(content, "Fonts/menuFont", 40, 20, 40, 80, 160);
            Small = content.Load<SpriteFont>("Fonts/frameRateCounterFont");
            Prompts = content.Load<SpriteFont>("Fonts/promptFont");
        }

        /// <summary>
        /// The font of the menus, the titles and the display of a run (Triple Wheel Display, see tools/fontgen), in four sizes,
        /// measured in the size 40, whose capitals are as high as those of the font of 2016.
        /// </summary>
        public GameFont Menu { get; }

        /// <summary>Small font with digits and the letters of "fps" only.</summary>
        public SpriteFont Small { get; }

        /// <summary>The glyphs of keys and gamepad buttons (PromptFont), only those listed in its .spritefont file.</summary>
        public SpriteFont Prompts { get; }
    }
}
