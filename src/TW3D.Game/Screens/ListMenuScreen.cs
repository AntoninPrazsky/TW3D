using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TW3D.Input;

namespace TW3D.Screens
{
    /// <summary>
    /// A menu with a title and its items one per line in the middle of the screen (Options, the pause menu), possibly
    /// with some content above them (the results of a level).
    /// The items are chosen by the mouse or touch, the arrow keys and Enter, or a gamepad; Esc (Back) closes the menu.
    /// </summary>
    public abstract class ListMenuScreen : Screen
    {
        protected sealed class MenuItem
        {
            public Func<string> Text;
            public Action Activate;
            public Func<bool> IsEnabled = () => true;
            /// <summary>The item switches a setting, so left and right switch it too.</summary>
            public bool IsSwitch;
            //Where the text is drawn, and the slightly bigger area that reacts to the mouse
            public Rectangle TextBounds;
            public Rectangle Bounds;
        }

        //Font scale of the items at the reference resolution, and the line spacing relative to the line height
        private const float ItemScale = 0.8f;
        private const float LineSpacing = 1.4f;
        //Extra area around the item text that reacts to the mouse, in reference pixels
        private const float ItemPadding = 12f;
        //How far the items slide in and out, and the title, in reference pixels
        private const float ItemSlide = 320f;
        private const float TitleSlide = 140f;

        private readonly string title;
        private readonly List<MenuItem> items = new List<MenuItem>();
        private int focusedItem = -1;
        private float contentTop;

        protected ListMenuScreen(string title)
        {
            this.title = title;
        }

        protected void AddItem(MenuItem item)
        {
            items.Add(item);
        }

        public override void HandleInput(InputState input, GameTime gameTime)
        {
            UpdateLayout();

            //The mouse focuses the item under it when it moves (a mouse at rest does not take the focus from the keys)
            int hovered = ItemAt(input.PointerPosition);
            if (input.Mouse.Position != input.PreviousMouse.Position)
                focusedItem = hovered;

            if (input.UpPressed)
                MoveFocus(-1);
            if (input.DownPressed)
                MoveFocus(1);

            if (input.PointerPressed && hovered >= 0)
                items[hovered].Activate();
            else if (focusedItem >= 0 && items[focusedItem].IsEnabled() && (input.ConfirmPressed || ((input.LeftPressed || input.RightPressed) && items[focusedItem].IsSwitch)))
                items[focusedItem].Activate();
            else if (input.BackPressed)
                Close();
        }

        private int ItemAt(Vector2 position)
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i].IsEnabled() && items[i].Bounds.Contains(position))
                    return i;
            return -1;
        }

        private void MoveFocus(int direction)
        {
            int index = focusedItem < 0 ? (direction > 0 ? -1 : items.Count) : focusedItem;
            for (int step = 0; step < items.Count; step++)
            {
                index = (index + direction + items.Count) % items.Count;
                if (items[index].IsEnabled())
                {
                    focusedItem = index;
                    return;
                }
            }
        }

        private void UpdateLayout()
        {
            Viewport viewport = Manager.Viewport;
            SpriteFont font = Manager.Fonts.Menu;
            float scale = ItemScale * Manager.UiScale;
            float lineHeight = font.MeasureString("M").Y * scale;
            //In the middle of the screen, but below the title (the user interface of phones is bigger)
            float content = ContentHeight;
            float top = Math.Max(viewport.Height / 2f - (content + lineHeight * LineSpacing * items.Count) / 2f,
                Manager.SafeArea.Top + MainMenuScreen.TitleHeight(Manager) + lineHeight * (LineSpacing - 1f) * 2f);
            contentTop = top;
            top += content;
            int padding = (int)(ItemPadding * Manager.UiScale);

            for (int i = 0; i < items.Count; i++)
            {
                Vector2 size = font.MeasureString(items[i].Text()) * scale;
                items[i].TextBounds = new Rectangle((int)((viewport.Width - size.X) / 2f), (int)(top + i * lineHeight * LineSpacing), (int)size.X, (int)lineHeight);
                items[i].Bounds = items[i].TextBounds;
                items[i].Bounds.Inflate(padding, padding);
            }
        }

        /// <summary>Draws what is behind the menu; called between SpriteBatch.Begin and End.</summary>
        protected virtual void DrawBackdrop() { }

        /// <summary>Height of the content above the items, in screen pixels.</summary>
        protected virtual float ContentHeight => 0f;

        /// <summary>Draws the content above the items from its top; called between SpriteBatch.Begin and End.</summary>
        /// <param name="slide">How far the items are moved sideways as they slide in or out.</param>
        protected virtual void DrawContent(float top, float slide, float alpha) { }

        public override void Draw(GameTime gameTime)
        {
            UpdateLayout();

            SpriteFont font = Manager.Fonts.Menu;
            float uiScale = Manager.UiScale;
            float alpha = Visibility;

            //Items slide in from the left and out to the right, the title slides from the top
            float slide = SlideAmount * ItemSlide * uiScale * (IsDisappearing ? 1f : -1f);

            Manager.SpriteBatch.Begin();

            DrawBackdrop();
            DrawContent(contentTop, slide, alpha);

            for (int i = 0; i < items.Count; i++)
            {
                MenuItem item = items[i];
                Color color = !item.IsEnabled() ? Color.White * 0.45f : (i == focusedItem && HasFocus ? MainMenuScreen.Yellow : Color.White);
                Vector2 position = new Vector2(item.TextBounds.X + slide, item.TextBounds.Y);
                Manager.DrawShadowedText(font, item.Text(), position, color * alpha, Color.DarkSlateGray * (alpha * alpha), ItemScale * uiScale);
            }

            MainMenuScreen.DrawTitle(Manager, title, Manager.SafeArea.Top - SlideAmount * TitleSlide * uiScale, alpha);

            Manager.SpriteBatch.End();
        }
    }
}
