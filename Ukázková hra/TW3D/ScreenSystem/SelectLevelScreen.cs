using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FarseerPhysics.Graphics.ScreenSystem
{
    /// <summary>
    /// Base class for screens that contain a menu of options. The user can
    /// move up and down to select an entry, or cancel to back out of the screen.
    /// </summary>
    public class SelectLevelScreen : GameScreen
    {

        private const float NumEntries = 4;

        private List<MenuEntry> _menuEntries = new List<MenuEntry>();
        private string _menuTitle;
        private Vector2 _titlePosition;
        private Vector2 _titleOrigin;
        private int _selectedEntry;
        private float _menuBorderTop;
        private float _menuBorderBottom;
        private float _menuBorderMargin;
        private float _menuOffset;
        private float _maxOffset;

        private float _titleScale;
        private float _screenWidth;

        private float _slFix;

        private Texture2D _texScrollButton;
        private Texture2D _texSlider;

        private MenuButton _scrollUp;
        private MenuButton _scrollDown;
        private MenuButton _scrollSlider;
        private bool _scrollLock;

        /// <summary>
        /// Constructor.
        /// </summary>
        public SelectLevelScreen(string menuTitle, float screenWidth)
        {
            _menuTitle = menuTitle;
            _screenWidth = screenWidth;
            TransitionOnTime = TimeSpan.FromSeconds(0.7);
            TransitionOffTime = TimeSpan.FromSeconds(0.5);
            HasCursor = true;
        }

        public void AddLevelItem(string levelName, GameScreen screen)
        {
            MenuEntry entry = new MenuEntry(this, levelName, screen, _screenWidth);
            _menuEntries.Add(entry);
        }

        public override void LoadContent()
        {
            base.LoadContent();
            Viewport viewport = ScreenManager.GraphicsDevice.Viewport;
            SpriteFont font = ScreenManager.Fonts.MenuSpriteFont;

            _titleScale = _screenWidth / 800f;
            _slFix = 20f * _screenWidth / 1280f;
            _texScrollButton = ScreenManager.Content.Load<Texture2D>("Common/arrow");
            _texSlider = ScreenManager.Content.Load<Texture2D>("Common/slider");

            float scrollBarPos = 84f * _screenWidth / 1280f;
            for (int i = 0; i < _menuEntries.Count; ++i)
            {
                _menuEntries[i].Initialize();
            }
            
            _titleOrigin = font.MeasureString(_menuTitle) / 2f;
            _titlePosition = new Vector2(viewport.Width / 2f, font.MeasureString("M").Y * _titleScale / 2f);

            //_menuBorderMargin = font.MeasureString("M").Y * _titleScale * 0.8f;
            //_menuBorderTop = (viewport.Height - _menuBorderMargin * (NumEntries - 1)) / 2f;
            //_menuBorderBottom = (viewport.Height + _menuBorderMargin * (NumEntries - 1)) / 2f;

            _menuBorderMargin = _menuEntries[0].GetHeight();
            _menuBorderTop = (viewport.Height - _menuBorderMargin * (NumEntries - 1)) / 2f + _slFix * 2.4f;
            _menuBorderBottom = (viewport.Height + _menuBorderMargin * (NumEntries - 1)) / 2f;

            _menuOffset = _slFix; //Výchozí pozice menu (v budoucnu se nabídka posune na poslední levely)
            _maxOffset = Math.Max(0f, (_menuEntries.Count - NumEntries * 3) * _menuBorderMargin);
            
            _scrollUp = new MenuButton(_texScrollButton, false, new Vector2(scrollBarPos, _menuBorderTop - _texScrollButton.Height / 3), this);
            _scrollDown = new MenuButton(_texScrollButton, true, new Vector2(scrollBarPos, _menuBorderBottom + _texScrollButton.Height / 3), this);
            _scrollSlider = new MenuButton(_texSlider, false, new Vector2(scrollBarPos, _menuBorderTop), this);

            _scrollLock = false;
        }

        /// <summary>
        /// Returns the index of the menu entry at the position of the given mouse state.
        /// </summary>
        /// <returns>Index of menu entry if valid, -1 otherwise</returns>
        private int GetMenuEntryAt(Vector2 position)
        {
            int index = 0;
            foreach (MenuEntry entry in _menuEntries)
            {
                float width = entry.GetWidth();
                float height = entry.GetHeight();
                Rectangle rect = new Rectangle((int)(entry.Position.X), (int)(entry.Position.Y), (int)width, (int)height);

                if (rect.Contains((int)position.X, (int)position.Y) && entry.Alpha > 0.1f)
                    return index;

                ++index;
            }
            return -1;
        }

        /// <summary>
        /// Responds to user input, changing the selected entry and accepting
        /// or cancelling the menu.
        /// </summary>
        public override void HandleInput(InputHelper input, GameTime gameTime)
        {
            // Mouse or touch on a menu item
            int hoverIndex = GetMenuEntryAt(input.Cursor);
            if (hoverIndex > -1 && _menuEntries[hoverIndex].IsSelectable() && !_scrollLock)
                _selectedEntry = hoverIndex;
            else
                _selectedEntry = -1;

            _scrollSlider.Hover = false;
            if (input.IsCursorValid)
            {
                _scrollUp.Collide(input.Cursor);
                _scrollDown.Collide(input.Cursor);
                _scrollSlider.Collide(input.Cursor);
            }
            else
            {
                _scrollUp.Hover = false;
                _scrollDown.Hover = false;
                _scrollLock = false;
            }
            
            // Accept or cancel the menu? 
            if (input.IsMenuSelect() && _selectedEntry != -1)
            {
                if (_menuEntries[_selectedEntry].IsExitItem())
                    ScreenManager.Game.Exit();
                else if (_menuEntries[_selectedEntry].Screen != null)
                {
                    (_menuEntries[_selectedEntry].Screen as IGameScreen).LevelName = _menuEntries[_selectedEntry].Level;
                    ScreenManager.AddScreen(_menuEntries[_selectedEntry].Screen);
                    
                    if (_menuEntries[_selectedEntry].Screen is IGameScreen)
                        ScreenManager.AddScreen(new MessageBoxScreen((_menuEntries[_selectedEntry].Screen as IGameScreen).GetDetails()));
                }
            }
            else if (input.IsMenuCancel())
                ExitScreen();

            if (input.IsMenuPressed())
            {
                if (_scrollUp.Hover)
                {
                    _menuOffset = Math.Max(_menuOffset - 200f * (float)gameTime.ElapsedGameTime.TotalSeconds, _slFix);
                    _scrollLock = false;
                }
                if (_scrollDown.Hover)
                {
                    _menuOffset = Math.Min(_menuOffset + 200f * (float)gameTime.ElapsedGameTime.TotalSeconds, _maxOffset - _slFix);
                    _scrollLock = false;
                }
                if (_scrollSlider.Hover)
                {
                    _scrollLock = true;
                }
            }
            
            
            if (input.IsMenuReleased())
                _scrollLock = false;
            
            
            if (_scrollLock)
            {
                _scrollSlider.Hover = true;
                _menuOffset = Math.Max(Math.Min(((input.Cursor.Y - _menuBorderTop) / (_menuBorderBottom - _menuBorderTop)) * _maxOffset, _maxOffset - _slFix), _slFix);
            }
            
        }
            
        /// <summary>
        /// Allows the screen the chance to position the menu entries. By default
        /// all menu entries are lined up in a vertical list, centered on the screen.
        /// </summary>
        
        protected virtual void UpdateMenuEntryLocations()
        {
            // Make the menu slide into place during transitions, using a
            // power curve to make things look more interesting (this makes
            // the movement slow down as it nears the end).
            float transitionOffset = (float)Math.Pow(TransitionPosition, 2);

            Vector2 position = Vector2.Zero;
            position.Y = _menuBorderTop - _menuOffset;

            //Pozice obrázkù úrovní odpovídá šíøce displeje 1280px, protože pro tuto velikost jsou nativní
            float sc = ScreenManager.GraphicsDevice.Viewport.Width / 1280f;
            
            // update each menu entry's location in turn
            for (int i = 0; i < _menuEntries.Count; ++i)
            {
                if (i % 3 == 0) position.X = 168f * sc;
                else position.X = _menuEntries[i].GetWidth() + _menuEntries[i-1].Position.X + 16f * sc;
                
                if (ScreenState == ScreenState.TransitionOn)
                    position.X -= transitionOffset * 256;
                else
                    position.X += transitionOffset * 256;

                // set the entry's position
                _menuEntries[i].Position = position;

                if (position.Y < _menuBorderTop)
                    _menuEntries[i].Alpha = 1f - Math.Min(_menuBorderTop - position.Y, _menuBorderMargin) / _menuBorderMargin;
                else if (position.Y > _menuBorderBottom)
                    _menuEntries[i].Alpha = 1f - Math.Min(position.Y - _menuBorderBottom, _menuBorderMargin) / _menuBorderMargin;
                else
                    _menuEntries[i].Alpha = 1f;

                if (i % 3 == 2) position.Y += _menuEntries[i].GetHeight() + 16f * sc;
            }
            
            Vector2 scrollPos = _scrollSlider.Position;
            scrollPos.Y = MathHelper.Lerp(_menuBorderTop, _menuBorderBottom, _menuOffset / _maxOffset);
            _scrollSlider.Position = scrollPos;
        }
            
        /// <summary>
        /// Updates the menu.
        /// </summary>
        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);
            
            // Update each nested MenuEntry object.
            for (int i = 0; i < _menuEntries.Count; ++i)
            {
                bool isSelected = IsActive && (i == _selectedEntry);
                _menuEntries[i].Update(isSelected, gameTime);
            }
            
            _scrollUp.Update(gameTime);
            _scrollDown.Update(gameTime);
            _scrollSlider.Update(gameTime);
            
        }

        /// <summary>
        /// Draws the menu.
        /// </summary>
        public override void Draw(GameTime gameTime)
        {
            // make sure our entries are in the right place before we draw them
            UpdateMenuEntryLocations();

            SpriteBatch spriteBatch = ScreenManager.SpriteBatch;
            SpriteFont font = ScreenManager.Fonts.MenuSpriteFont;

            spriteBatch.Begin();
            // Draw each menu entry in turn.
            for (int i = 0; i < _menuEntries.Count; ++i)
            {
                _menuEntries[i].Draw();
            }

            // Make the menu slide into place during transitions, using a
            // power curve to make things look more interesting (this makes
            // the movement slow down as it nears the end).
            Vector2 transitionOffset = new Vector2(0f, (float)Math.Pow(TransitionPosition, 2) * 100f);

            spriteBatch.DrawString(font, _menuTitle, _titlePosition - transitionOffset + Vector2.One, Color.Black, 0, _titleOrigin, _titleScale, SpriteEffects.None, 0);
            spriteBatch.DrawString(font, _menuTitle, _titlePosition - transitionOffset, new Color(255, 210, 0), 0, _titleOrigin, _titleScale, SpriteEffects.None, 0);
            
            _scrollUp.Draw();
            _scrollSlider.Draw();
            _scrollDown.Draw();
             
            spriteBatch.End();
        }
    }
}