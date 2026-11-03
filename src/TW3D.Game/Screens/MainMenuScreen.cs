using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TW3D.GameObjects;
using TW3D.Input;

namespace TW3D.Screens
{
    /// <summary>
    /// The main menu: the 3D logo of the game (its lettering) in the top left corner (drawn in the garage behind the
    /// menus, see <see cref="GarageScene.Title"/>), two items in the middle of the left and right half, a row of items at
    /// the bottom and a place in the top right corner, level with the logo (the level editor of a Debug build).
    /// Items and the logo are positioned relative to the screen edges and scaled with the user interface, so the layout
    /// works on any aspect ratio. The logo slides up out of the frame as the menu goes, as the titles of the other menus do.
    /// </summary>
    public sealed class MainMenuScreen : Screen
    {
        private enum Placement
        {
            MiddleLeft,
            MiddleRight,
            BottomLeft,
            BottomCenter,
            BottomRight,
            TopRight
        }

        //The items take these places in the order they are added
        private static readonly Placement[] Places =
        {
            Placement.MiddleLeft, Placement.MiddleRight, Placement.BottomLeft, Placement.BottomCenter, Placement.BottomRight, Placement.TopRight
        };

        //Font scale of the titles of the other menus at the reference resolution
        private const float TitleScale = 1.3f;
        //The 3D logo (its lettering): its height at the reference resolution and its width to its height
        //(tools/garagegen/logo.py prints it); it never reaches lower than this many line heights above the middle of the
        //screen, where the items of the middle are, nor further than half way across the screen
        private const float LogoHeight = 90f;
        private const float LogoWidth = 5.573f;
        private const float LogoClearance = 1.5f;
        //Extra area around the item text that reacts to the mouse, in reference pixels
        private const float ItemPadding = 20f;
        //How far the items slide in and out, in reference pixels
        private const float ItemSlide = 320f;

        /// <summary>The colour of the focused item and of the titles, used by all screens.</summary>
        public static readonly Color Yellow = new Color(255, 210, 0);

        private readonly BackgroundScreen background;
        private readonly MenuItems items = new MenuItems();

        /// <param name="background">The garage behind the menus, which shows the logo.</param>
        public MainMenuScreen(BackgroundScreen background)
        {
            this.background = background;
            FadeInTime = TimeSpan.FromSeconds(0.7);
            FadeOutTime = TimeSpan.FromSeconds(0.5);
        }

        /// <summary>
        /// Adds an item; items without an action are shown dimmed as not yet available. The first item that can be
        /// chosen (Play) has the focus from the start, so Enter or A starts the game.
        /// </summary>
        public void AddItem(string text, Action activate)
        {
            if (items.Count >= Places.Length)
                throw new InvalidOperationException("The main menu has room for " + Places.Length + " items.");

            items.Add(new MenuItem { Text = () => text, Activate = activate, IsEnabled = () => activate != null });
            if (items.Focused < 0 && activate != null)
                items.MoveFocus(1);
        }

        public override void LoadContent()
        {
            items.Sounds = Manager.Sounds;
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
            //The top right place is level with the middle of the logo
            float topRowY = safe.Top + LogoSize(lineHeight).Y / 2f;
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
                    case Placement.TopRight: center = new Vector2(safe.Right - size.X / 2f, topRowY); break;
                    default: center = new Vector2(safe.Right - size.X / 2f, bottomRowY); break;
                }
                items[i].Place(new Rectangle((int)(center.X - size.X / 2f), (int)(center.Y - lineHeight / 2f), (int)size.X, (int)lineHeight), padding);
            }
        }

        /// <summary>
        /// The size of the logo on the screen, in pixels: its height at the reference resolution scaled with the user
        /// interface, but no larger than leaves the middle of the screen to the items there, nor wider than half the screen.
        /// </summary>
        private Vector2 LogoSize(float lineHeight)
        {
            Viewport viewport = Manager.Viewport;
            Rectangle safe = Manager.SafeArea;
            float height = Math.Min(LogoHeight * Manager.UiScale, viewport.Height / 2f - lineHeight * LogoClearance - safe.Top);
            height = Math.Max(0f, Math.Min(height, (viewport.Width / 2f - safe.Left) / LogoWidth));
            return new Vector2(height * LogoWidth, height);
        }

        /// <summary>
        /// Puts the logo in the garage in the top left corner of the screen, or as far up out of the frame as the menu
        /// has gone; takes it away when the menu is not seen at all. Done whether the menu has the focus or not, so that
        /// the logo leaves with it when another screen covers it.
        /// </summary>
        public override void Update(GameTime gameTime)
        {
            GarageScene scene = background.Scene;
            if (scene == null)
                return;
            if (Visibility <= 0f)
            {
                scene.Title = null;
                return;
            }
            float lineHeight = Manager.Fonts.Menu.MeasureString("M").Y * MenuItems.TextScale * Manager.UiScale;
            Vector2 size = LogoSize(lineHeight);
            Rectangle safe = Manager.SafeArea;
            float top = safe.Top;
            //Fully gone it is just above the top of the screen
            top -= SlideAmount * (top + size.Y);
            scene.Title = (new Vector2(safe.Left + size.X / 2f, top + size.Y / 2f), size.Y);
        }

        public override void Draw(GameTime gameTime)
        {
            UpdateLayout();
            float uiScale = Manager.UiScale;
            float alpha = Visibility;

            //Items slide in from the left and out to the right; the logo, in the garage behind, slides from the top
            float slide = SlideAmount * ItemSlide * uiScale * (IsDisappearing ? 1f : -1f);

            Manager.SpriteBatch.Begin();
            items.Draw(Manager, MenuItems.TextScale * uiScale, slide, alpha, HasFocus);
            Manager.SpriteBatch.End();
        }

        /// <summary>Draws a screen title centred horizontally, with its top at the given height (shared by the other menus).</summary>
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
