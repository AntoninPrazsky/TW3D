using System;
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

        //The items take these places in the order they are added
        private static readonly Placement[] Places = { Placement.MiddleLeft, Placement.MiddleRight, Placement.BottomLeft, Placement.BottomCenter, Placement.BottomRight };

        //Font scale of the title at the reference resolution
        private const float TitleScale = 1.3f;
        //Extra area around the item text that reacts to the mouse, in reference pixels
        private const float ItemPadding = 20f;
        //How far the items slide in and out, in reference pixels
        private const float ItemSlide = 320f;
        private const float TitleSlide = 140f;

        /// <summary>The colour of the focused item and of the titles, used by all screens.</summary>
        public static readonly Color Yellow = new Color(255, 210, 0);

        private readonly string title;
        private readonly MenuItems items = new MenuItems();

        public MainMenuScreen(string title)
        {
            this.title = title;
            FadeInTime = TimeSpan.FromSeconds(0.7);
            FadeOutTime = TimeSpan.FromSeconds(0.5);
        }

        /// <summary>Adds an item; items without an action are shown dimmed as not yet available.</summary>
        public void AddItem(string text, Action activate)
        {
            if (items.Count >= Places.Length)
                throw new InvalidOperationException("The main menu has room for " + Places.Length + " items.");

            items.Add(new MenuItem { Text = () => text, Activate = activate, IsEnabled = () => activate != null });
        }

        public override void HandleInput(InputState input, GameTime gameTime)
        {
            UpdateLayout();
            items.FollowMouse(input);

            if (input.LeftPressed || input.UpPressed)
                items.MoveFocus(-1);
            if (input.RightPressed || input.DownPressed)
                items.MoveFocus(1);

            //Left and right move the focus here
            if (!items.Activate(input, switchByLeftRight: false) && input.BackPressed)
                Manager.Game.Exit();
        }

        private void UpdateLayout()
        {
            Viewport viewport = Manager.Viewport;
            Rectangle safe = Manager.SafeArea;
            SpriteFont font = Manager.Fonts.Menu;
            float scale = MenuItems.TextScale * Manager.UiScale;
            float lineHeight = font.MeasureString("M").Y * scale;
            float bottomRowY = safe.Bottom - lineHeight / 2f;
            int padding = (int)(ItemPadding * Manager.UiScale);

            for (int i = 0; i < items.Count; i++)
            {
                Vector2 size = font.MeasureString(items[i].Text()) * scale;
                Vector2 center;
                switch (Places[i])
                {
                    case Placement.MiddleLeft: center = new Vector2(viewport.Width / 5f, viewport.Height / 2f); break;
                    case Placement.MiddleRight: center = new Vector2(viewport.Width * 0.8f, viewport.Height / 2f); break;
                    case Placement.BottomLeft: center = new Vector2(safe.Left + size.X / 2f, bottomRowY); break;
                    case Placement.BottomCenter: center = new Vector2(viewport.Width / 2f, bottomRowY); break;
                    default: center = new Vector2(safe.Right - size.X / 2f, bottomRowY); break;
                }
                items[i].Place(new Rectangle((int)(center.X - size.X / 2f), (int)(center.Y - lineHeight / 2f), (int)size.X, (int)lineHeight), padding);
            }
        }

        public override void Draw(GameTime gameTime)
        {
            UpdateLayout();
            float uiScale = Manager.UiScale;
            float alpha = Visibility;

            //Items slide in from the left and out to the right, the title slides from the top
            float slide = SlideAmount * ItemSlide * uiScale * (IsDisappearing ? 1f : -1f);
            float titleSlide = SlideAmount * TitleSlide * uiScale;

            Manager.SpriteBatch.Begin();
            items.Draw(Manager, MenuItems.TextScale * uiScale, slide, alpha, HasFocus);
            DrawTitle(Manager, title, Manager.SafeArea.Top - titleSlide, alpha);
            Manager.SpriteBatch.End();
        }

        /// <summary>Draws a screen title centred horizontally, with its top at the given height (shared by all screens).</summary>
        public static void DrawTitle(ScreenManager manager, string title, float top, float alpha)
        {
            SpriteFont font = manager.Fonts.Menu;
            float scale = TitleScale * manager.UiScale;
            Vector2 size = font.MeasureString(title) * scale;
            Vector2 position = new Vector2((manager.Viewport.Width - size.X) / 2f, top);
            manager.DrawShadowedText(font, title, position, Yellow * alpha, Color.Black * alpha, scale);
        }

        /// <summary>Height of a screen title in screen pixels.</summary>
        public static float TitleHeight(ScreenManager manager)
        {
            return manager.Fonts.Menu.MeasureString("M").Y * TitleScale * manager.UiScale;
        }
    }
}
