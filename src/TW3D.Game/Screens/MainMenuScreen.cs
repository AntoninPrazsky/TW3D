using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TW3D.Input;

namespace TW3D.Screens
{
    /// <summary>
    /// The main menu: the game title at the top, two items in the middle of the left and right half
    /// and a row of items at the bottom. Items are positioned relative to the screen edges and scaled
    /// with the user interface, so the layout works on any aspect ratio.
    /// </summary>
    public sealed class MainMenuScreen : Screen
    {
        private enum Placement
        {
            MiddleLeft,
            MiddleRight,
            BottomLeft,
            BottomCenter,
            BottomRight
        }

        private sealed class MenuItem
        {
            public string Text;
            public Placement Placement;
            public Action Activate;
            public Rectangle Bounds;

            public bool IsEnabled => Activate != null;
        }

        //Font scale of the items and the title at the reference resolution
        private const float ItemScale = 1.44f;
        private const float TitleScale = 1.6f;
        //Distance of the bottom corner items from the side edges, as a fraction of the screen width
        private const float SideMargin = 1f / 50f;
        //How far the items slide in and out, in reference pixels
        private const float ItemSlide = 256f;
        private const float TitleSlide = 100f;

        public static readonly Color Yellow = new Color(255, 210, 0);

        private readonly string title;
        private readonly List<MenuItem> items = new List<MenuItem>();
        private int focusedItem = -1;

        public MainMenuScreen(string title)
        {
            this.title = title;
            FadeInTime = TimeSpan.FromSeconds(0.7);
            FadeOutTime = TimeSpan.FromSeconds(0.5);
        }

        /// <summary>Adds an item; items without an action are shown dimmed as not yet available.</summary>
        public void AddItem(string text, Action activate)
        {
            Placement[] order = { Placement.MiddleLeft, Placement.MiddleRight, Placement.BottomLeft, Placement.BottomCenter, Placement.BottomRight };
            if (items.Count >= order.Length)
                throw new InvalidOperationException("The main menu has room for " + order.Length + " items.");

            items.Add(new MenuItem { Text = text, Placement = order[items.Count], Activate = activate });
        }

        public override void HandleInput(InputState input, GameTime gameTime)
        {
            UpdateLayout();

            //The mouse (or a finger) focuses the item under it
            int hovered = ItemAt(input.PointerPosition);
            if (hovered >= 0 || input.Mouse.Position != input.PreviousMouse.Position)
                focusedItem = hovered;

            if (input.LeftPressed || input.UpPressed)
                MoveFocus(-1);
            if (input.RightPressed || input.DownPressed)
                MoveFocus(1);

            if (input.PointerPressed && hovered >= 0)
                items[hovered].Activate();
            else if (input.ConfirmPressed && focusedItem >= 0)
                items[focusedItem].Activate();
            else if (input.BackPressed)
                Manager.Game.Exit();
        }

        private int ItemAt(Vector2 position)
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i].IsEnabled && items[i].Bounds.Contains(position))
                    return i;
            return -1;
        }

        private void MoveFocus(int direction)
        {
            int index = focusedItem < 0 ? (direction > 0 ? -1 : items.Count) : focusedItem;
            for (int step = 0; step < items.Count; step++)
            {
                index = (index + direction + items.Count) % items.Count;
                if (items[index].IsEnabled)
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

            foreach (MenuItem item in items)
            {
                Vector2 size = font.MeasureString(item.Text) * scale;
                Vector2 center;
                switch (item.Placement)
                {
                    case Placement.MiddleLeft: center = new Vector2(viewport.Width / 5f, viewport.Height / 2f); break;
                    case Placement.MiddleRight: center = new Vector2(viewport.Width * 0.8f, viewport.Height / 2f); break;
                    case Placement.BottomLeft: center = new Vector2(viewport.Width * SideMargin + size.X / 2f, viewport.Height - lineHeight / 2f); break;
                    case Placement.BottomCenter: center = new Vector2(viewport.Width / 2f, viewport.Height - lineHeight / 2f); break;
                    default: center = new Vector2(viewport.Width * (1f - SideMargin) - size.X / 2f, viewport.Height - lineHeight / 2f); break;
                }

                item.Bounds = new Rectangle((int)(center.X - size.X / 2f), (int)(center.Y - lineHeight / 2f), (int)size.X, (int)lineHeight);
            }
        }

        public override void Draw(GameTime gameTime)
        {
            UpdateLayout();

            SpriteFont font = Manager.Fonts.Menu;
            float uiScale = Manager.UiScale;
            float alpha = Visibility;

            //Items slide in from the left and out to the right, the title slides from the top
            float slide = SlideAmount * ItemSlide * uiScale * (IsDisappearing ? 1f : -1f);
            float titleSlide = SlideAmount * TitleSlide * uiScale;

            Manager.SpriteBatch.Begin();

            for (int i = 0; i < items.Count; i++)
            {
                MenuItem item = items[i];
                Color color = !item.IsEnabled ? Color.White * 0.45f : (i == focusedItem && HasFocus ? Yellow : Color.White);
                Vector2 position = new Vector2(item.Bounds.X + slide, item.Bounds.Y);
                Manager.DrawShadowedText(font, item.Text, position, color * alpha, Color.DarkSlateGray * (alpha * alpha), ItemScale * uiScale);
            }

            DrawTitle(Manager, title, -titleSlide, alpha);

            Manager.SpriteBatch.End();
        }

        /// <summary>Draws a screen title centred horizontally, with its top at the given height (shared with the level select).</summary>
        public static void DrawTitle(ScreenManager manager, string title, float top, float alpha)
        {
            SpriteFont font = manager.Fonts.Menu;
            float scale = TitleScale * manager.UiScale;
            Vector2 size = font.MeasureString(title) * scale;
            Vector2 position = new Vector2((manager.Viewport.Width - size.X) / 2f, top);
            manager.DrawShadowedText(font, title, position, Yellow * alpha, Color.Black * alpha, scale);
        }
    }
}
