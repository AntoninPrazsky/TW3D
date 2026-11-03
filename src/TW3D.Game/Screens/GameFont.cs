using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TW3D.Screens
{
    /// <summary>
    /// A font built in several sizes (by tools/fontgen, into content/Fonts/&lt;name&gt;&lt;size&gt;.spritefont), drawn sharp at any
    /// size: a text is drawn from the smallest size at least as large as it is on the screen (reduced at most to a half,
    /// never enlarged while a larger size has its characters), the largest size, which has only some characters, when
    /// it has all of the text's. The layouts measure in the base size: <see cref="MeasureString"/> and
    /// <see cref="LineSpacing"/> are those of the base size, and a scale of 1 draws the text as large as the base size does.
    /// </summary>
    public sealed class GameFont
    {
        //A size may be reduced to its next smaller size a little before it would have to be enlarged: at this part
        private const float Tolerance = 0.97f;

        private readonly SpriteFont baseFont;
        //The sizes from the smallest, how large each is to the base size, and the characters each has
        private readonly SpriteFont[] fonts;
        private readonly float[] ratios;
        private readonly HashSet<char>[] characters;

        /// <param name="name">The content name without the size, e.g. "Fonts/menuFont".</param>
        /// <param name="baseSize">The size the layouts measure in.</param>
        /// <param name="sizes">Every size built, from the smallest.</param>
        public GameFont(ContentManager content, string name, int baseSize, params int[] sizes)
        {
            fonts = new SpriteFont[sizes.Length];
            ratios = new float[sizes.Length];
            characters = new HashSet<char>[sizes.Length];
            for (int i = 0; i < sizes.Length; i++)
            {
                fonts[i] = content.Load<SpriteFont>(name + sizes[i]);
                ratios[i] = (float)sizes[i] / baseSize;
                characters[i] = new HashSet<char>(fonts[i].Characters);
                if (sizes[i] == baseSize)
                    baseFont = fonts[i];
            }
        }

        /// <summary>The base size itself, for the metrics of its glyphs.</summary>
        public SpriteFont BaseFont => baseFont;

        /// <summary>The height of a line in the base size.</summary>
        public int LineSpacing => baseFont.LineSpacing;

        /// <summary>The size of a text in the base size; the text is as large on the screen times the scale it is drawn with.</summary>
        public Vector2 MeasureString(string text) => baseFont.MeasureString(text);

        /// <summary>Draws a text from the size that fits its scale best.</summary>
        /// <param name="scale">How large it is to the base size.</param>
        public void Draw(SpriteBatch batch, string text, Vector2 position, Color colour, float scale)
        {
            int size = SizeFor(text, scale);
            batch.DrawString(fonts[size], text, position, colour, 0f, Vector2.Zero, scale / ratios[size], SpriteEffects.None, 0f);
        }

        /// <summary>The smallest size at least as large as the scale with all the characters of the text, else the largest with them.</summary>
        private int SizeFor(string text, float scale)
        {
            int chosen = -1;
            for (int i = 0; i < fonts.Length; i++)
            {
                if (!HasAll(i, text))
                    continue;
                chosen = i;
                if (ratios[i] >= scale * Tolerance)
                    break;
            }
            //No size has every character: the base size draws those it lacks as its default character
            return chosen >= 0 ? chosen : System.Array.IndexOf(fonts, baseFont);
        }

        private bool HasAll(int size, string text)
        {
            HashSet<char> set = characters[size];
            foreach (char c in text)
                if (c != '\n' && c != '\r' && !set.Contains(c))
                    return false;
            return true;
        }
    }
}
