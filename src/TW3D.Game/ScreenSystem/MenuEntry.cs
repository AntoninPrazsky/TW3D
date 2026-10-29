using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TW3D.ScreenSystem
{
    public enum EntryType
    {
        Screen,
        Separator,
        ExitItem,
        Level
    }

    /// <summary>
    /// Helper class represents a single entry in a MenuScreen. By default this
    /// just draws the entry text string, but it can be customized to display menu
    /// entries in different ways. This also provides an event that will be raised
    /// when the menu entry is selected.
    /// </summary>
    public sealed class MenuEntry
    {
        private Vector2 _baseOrigin;

        private float _height;
        private MenuScreen _menu;
        private SelectLevelScreen _levMenu;
        private float _scale;

        private Texture2D image;

        /// <summary>
        /// Tracks a fading selection effect on the entry.
        /// </summary>
        /// <remarks>
        /// The entries transition out of the selection effect when they are deselected.
        /// </remarks>
        
        private float _selectionFade;

        private EntryType _type;
        private float _width;

        /// <summary>
        /// Constructs a new menu entry with the specified text.
        /// </summary>
        public MenuEntry(MenuScreen menu, string text, EntryType type, GameScreen screen, float screenWidth)
        {
            Text = text;
            Screen = screen;
            _type = type;
            _menu = menu;
            _scale = 0.9f * screenWidth / 800f;
            Alpha = 1.0f;
        }

        /// <summary>
        /// Constructs a new level selection entry.
        /// </summary>
        public MenuEntry(SelectLevelScreen menu, string level, GameScreen screen, float screenWidth)
        {
            Screen = screen;
            _type = EntryType.Level;
            _levMenu = menu;
            Level = level;
            Text = level;
            _scale = screenWidth / 1280f;
            _scale = (float)(Math.Round((double)_scale, 2));
            Alpha = 1.0f;
        }

        /// <summary>
        /// Gets or sets the text of this menu entry.
        /// </summary>
        public string Text { get; set; }

        public string Level { get; set; }

        /// <summary>
        /// Gets or sets the position at which to draw this menu entry.
        /// </summary>
        public Vector2 Position { get; set; }

        public float Alpha { get; set; }

        public GameScreen Screen { get; private set; }

        public void Initialize()
        {
            if (_type == EntryType.Level)
            {
                image = _levMenu.ScreenManager.Content.Load<Texture2D>("Levels/levimgs/" + Level);
                _baseOrigin = new Vector2(image.Width, image.Height) / 2;
                _width = image.Width;
                _height = image.Height;
            }
            else
            {
                SpriteFont font;
                font = _menu.ScreenManager.Fonts.MenuSpriteFont;
                _baseOrigin = new Vector2(font.MeasureString(Text).X, font.MeasureString("M").Y) * 0.5f;
                _width = font.MeasureString(Text).X;
                _height = font.MeasureString("M").Y;
            }

            
        }

        public bool IsExitItem()
        {
            return _type == EntryType.ExitItem;
        }

        public bool IsSelectable()
        {
            return _type != EntryType.Separator;
        }

        /// <summary>
        /// Updates the menu entry.
        /// </summary>
        public void Update(bool isSelected, GameTime gameTime)
        {
            // there is no such thing as a selected item on Windows Phone, so we always
            // force isSelected to be false
#if WINDOWS_PHONE
            isSelected = false;
#endif
            // When the menu selection changes, entries gradually fade between
            // their selected and deselected appearance, rather than instantly
            // popping to the new state.
            
            /*
            if (_type != EntryType.Separator)
            {
                float fadeSpeed = (float)gameTime.ElapsedGameTime.TotalSeconds * 4;
                if (isSelected)
                    _selectionFade = Math.Min(_selectionFade + fadeSpeed, 1f);
                else
                    _selectionFade = Math.Max(_selectionFade - fadeSpeed, 0f);

                _scale = 0.7f + 0.1f * _selectionFade;
            }
            */
        }

        /// <summary>
        /// Draws the menu entry. This can be overridden to customize the appearance.
        /// </summary>
        public void Draw()
        {
            
            SpriteBatch batch;

            if (_menu == null)
            {
                batch = _levMenu.ScreenManager.SpriteBatch;
            }
            else
            {
                batch = _menu.ScreenManager.SpriteBatch;
            }

            if (_type == EntryType.Level)
            {
                Rectangle rect = new Rectangle((int)(Position.X), (int)(Position.Y), (int)(image.Width * _scale), (int)(image.Height * _scale));
                batch.Draw(image, rect, Color.White * Alpha);
            }
            else
            {
                SpriteFont font;
                font = _menu.ScreenManager.Fonts.MenuSpriteFont;
                // Draw the selected entry in yellow, otherwise white
                Color color = _type == EntryType.Separator ? Color.DarkOrange : Color.Lerp(Color.White, new Color(255, 210, 0), _selectionFade);
                color *= Alpha;

                batch.DrawString(font, Text, Position - _baseOrigin * _scale + Vector2.One, Color.DarkSlateGray * Alpha * Alpha, 0, Vector2.Zero, _scale, SpriteEffects.None, 0);
                batch.DrawString(font, Text, Position - _baseOrigin * _scale, color, 0, Vector2.Zero, _scale, SpriteEffects.None, 0);
            }
        }

        /// <summary>
        /// Queries how much space this menu entry requires.
        /// </summary>
        public int GetHeight()
        {
            return (int)(_height * _scale);
        }

        /// <summary>
        /// Queries how wide the entry is, used for centering on the screen.
        /// </summary>
        public int GetWidth()
        {
            return (int)(_width * _scale);
        }
    }
}