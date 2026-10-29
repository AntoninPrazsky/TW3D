using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TW3D.Input;

namespace TW3D.Screens
{
    /// <summary>
    /// A grid of level preview images below the title. As many columns as fit the screen are used;
    /// when the levels do not fit vertically, the list scrolls and a scroll bar appears on its left.
    /// </summary>
    public sealed class LevelSelectScreen : Screen
    {
        //Sizes in reference (1920×1080) pixels
        private const float ImageWidth = 350f;
        private const float ImageHeight = 197f;
        private const float Gap = 24f;
        private const float TitleSpacing = 32f;
        private const float ScrollBarScale = 0.6f;
        private const float ScrollBarSpacing = 32f;
        private const int MaxColumns = 6;

        private const float ArrowScrollSpeed = 900f;
        private const float WheelScrollStep = 120f;
        private const float ItemSlide = 320f;
        private const float TitleSlide = 140f;
        //Screen pixels the pressed pointer has to move to start scrolling instead of clicking
        private const float DragThreshold = 10f;

        private static readonly RasterizerState ScissorRasterizer = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };

        private readonly string title;
        private readonly List<string> levels;
        private readonly Action<string> startLevel;
        private readonly List<Texture2D> images = new List<Texture2D>();

        private Texture2D arrowTexture;
        private Texture2D sliderTexture;

        //Layout in screen pixels, recalculated every frame
        private float uiScale;
        private int columns;
        private Vector2 imageSize;
        private float gap;
        private float gridLeft;
        private float listTop;
        private float listBottom;
        private bool showScrollBar;
        private float scrollBarX;
        private float arrowUpY;
        private float arrowDownY;
        private float sliderTop;
        private float sliderBottom;

        //Scroll position in reference pixels, so it does not depend on the screen size
        private float scroll;
        private int focusedLevel = -1;

        private enum Drag { None, PressedOnLevel, List, Slider, ArrowUp, ArrowDown }
        private Drag drag;
        private int pressedLevel;
        private Vector2 pressPosition;
        private float pressScroll;

        public LevelSelectScreen(string title, IEnumerable<string> levels, Action<string> startLevel)
        {
            this.title = title;
            this.levels = new List<string>(levels);
            this.startLevel = startLevel;
            FadeInTime = TimeSpan.FromSeconds(0.7);
            FadeOutTime = TimeSpan.FromSeconds(0.5);
        }

        public override void LoadContent()
        {
            arrowTexture = Manager.Content.Load<Texture2D>("Common/arrow");
            sliderTexture = Manager.Content.Load<Texture2D>("Common/slider");
            images.Clear();
            foreach (string level in levels)
            {
                //A new level may not have a preview image yet; a placeholder with its name is drawn instead
                Texture2D image = null;
                try
                {
                    image = Manager.Content.Load<Texture2D>("Levels/levimgs/" + level);
                }
                catch (ContentLoadException)
                {
                }
                images.Add(image);
            }
        }

        private void UpdateLayout()
        {
            Rectangle safe = Manager.SafeArea;
            uiScale = Manager.UiScale;

            imageSize = new Vector2(ImageWidth, ImageHeight) * uiScale;
            gap = Gap * uiScale;
            listTop = safe.Top + MainMenuScreen.TitleHeight(Manager) + TitleSpacing * uiScale;
            listBottom = safe.Bottom;

            float arrowWidth = arrowTexture.Width * ScrollBarScale * uiScale;
            float scrollBarColumn = arrowWidth + ScrollBarSpacing * uiScale;

            //Columns that fit next to a scroll bar; the scroll bar is left out if all rows fit anyway
            columns = (int)((safe.Width - scrollBarColumn + gap) / (imageSize.X + gap));
            columns = MathHelper.Clamp(Math.Min(columns, levels.Count), 1, MaxColumns);
            showScrollBar = ContentHeight > listBottom - listTop;

            float gridWidth = columns * imageSize.X + (columns - 1) * gap;
            float blockWidth = gridWidth + (showScrollBar ? scrollBarColumn : 0f);
            float blockLeft = safe.Left + (safe.Width - blockWidth) / 2f;
            gridLeft = blockLeft + (showScrollBar ? scrollBarColumn : 0f);
            scrollBarX = blockLeft + arrowWidth / 2f;

            float arrowHeight = arrowTexture.Height * ScrollBarScale * uiScale;
            float sliderHeight = sliderTexture.Height * ScrollBarScale * uiScale;
            arrowUpY = listTop + arrowHeight / 2f;
            arrowDownY = listBottom - arrowHeight / 2f;
            sliderTop = arrowUpY + arrowHeight / 2f + gap + sliderHeight / 2f;
            sliderBottom = arrowDownY - arrowHeight / 2f - gap - sliderHeight / 2f;

            //The screen may have been resized
            ScrollTo(scroll);
        }

        private float ContentHeight
        {
            get
            {
                int rows = (levels.Count + columns - 1) / columns;
                return rows * imageSize.Y + Math.Max(0, rows - 1) * gap;
            }
        }

        /// <summary>Maximum scroll position in reference pixels.</summary>
        private float MaxScroll => Math.Max(0f, ContentHeight - (listBottom - listTop)) / uiScale;

        private void ScrollTo(float value)
        {
            scroll = MathHelper.Clamp(value, 0f, MaxScroll);
        }

        private Rectangle LevelBounds(int index)
        {
            int column = index % columns;
            int row = index / columns;
            float x = gridLeft + column * (imageSize.X + gap);
            float y = listTop + row * (imageSize.Y + gap) - scroll * uiScale;
            return new Rectangle((int)x, (int)y, (int)imageSize.X, (int)imageSize.Y);
        }

        private int LevelAt(Vector2 position)
        {
            if (position.Y < listTop || position.Y > listBottom)
                return -1;
            for (int i = 0; i < levels.Count; i++)
                if (LevelBounds(i).Contains(position))
                    return i;
            return -1;
        }

        private Rectangle ScrollBarButtonBounds(Texture2D texture, float centerY)
        {
            Vector2 size = new Vector2(texture.Width, texture.Height) * ScrollBarScale * uiScale;
            return new Rectangle((int)(scrollBarX - size.X / 2f), (int)(centerY - size.Y / 2f), (int)size.X, (int)size.Y);
        }

        private float SliderY => MaxScroll > 0f ? MathHelper.Lerp(sliderTop, sliderBottom, scroll / MaxScroll) : sliderTop;

        public override void HandleInput(InputState input, GameTime gameTime)
        {
            UpdateLayout();
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            Vector2 pointer = input.PointerPosition;

            int hovered = LevelAt(pointer);
            if (hovered >= 0 || input.Mouse.Position != input.PreviousMouse.Position)
                focusedLevel = hovered;

            if (input.PointerPressed)
            {
                pressPosition = pointer;
                pressScroll = scroll;
                if (showScrollBar && ScrollBarButtonBounds(arrowTexture, arrowUpY).Contains(pointer))
                    drag = Drag.ArrowUp;
                else if (showScrollBar && ScrollBarButtonBounds(arrowTexture, arrowDownY).Contains(pointer))
                    drag = Drag.ArrowDown;
                else if (showScrollBar && ScrollBarButtonBounds(sliderTexture, SliderY).Contains(pointer))
                    drag = Drag.Slider;
                else if (hovered >= 0)
                {
                    drag = Drag.PressedOnLevel;
                    pressedLevel = hovered;
                }
            }

            switch (drag)
            {
                case Drag.ArrowUp: ScrollTo(scroll - ArrowScrollSpeed * elapsed); break;
                case Drag.ArrowDown: ScrollTo(scroll + ArrowScrollSpeed * elapsed); break;
                case Drag.Slider: ScrollTo((pointer.Y - sliderTop) / (sliderBottom - sliderTop) * MaxScroll); break;
                case Drag.PressedOnLevel:
                    //Moving the pressed pointer turns the press into scrolling of the list
                    if (showScrollBar && Math.Abs(pointer.Y - pressPosition.Y) > DragThreshold)
                        drag = Drag.List;
                    break;
                case Drag.List: ScrollTo(pressScroll - (pointer.Y - pressPosition.Y) / uiScale); break;
            }

            if (input.PointerReleased)
            {
                if (drag == Drag.PressedOnLevel && LevelAt(pointer) == pressedLevel)
                    startLevel(levels[pressedLevel]);
                drag = Drag.None;
            }
            else if (input.Pointers.Count == 0)
                drag = Drag.None;

            float page = (listBottom - listTop) / uiScale;
            if (input.WheelDelta != 0)
                ScrollTo(scroll - input.WheelDelta / 120f * WheelScrollStep);
            if (input.KeyPressed(Keys.PageUp))
                ScrollTo(scroll - page);
            if (input.KeyPressed(Keys.PageDown))
                ScrollTo(scroll + page);

            HandleKeyboardFocus(input);

            if (input.ConfirmPressed && focusedLevel >= 0)
                startLevel(levels[focusedLevel]);
            else if (input.BackPressed)
                Close();
        }

        private void HandleKeyboardFocus(InputState input)
        {
            int move = 0;
            if (input.LeftPressed) move = -1;
            if (input.RightPressed) move = 1;
            if (input.UpPressed) move = -columns;
            if (input.DownPressed) move = columns;
            if (move == 0 || levels.Count == 0)
                return;

            focusedLevel = focusedLevel < 0 ? 0 : MathHelper.Clamp(focusedLevel + move, 0, levels.Count - 1);

            //Scroll so that the focused level is fully visible
            Rectangle bounds = LevelBounds(focusedLevel);
            if (bounds.Top < listTop)
                ScrollTo(scroll - (listTop - bounds.Top) / uiScale);
            else if (bounds.Bottom > listBottom)
                ScrollTo(scroll + (bounds.Bottom - listBottom) / uiScale);
        }

        public override void Draw(GameTime gameTime)
        {
            UpdateLayout();
            SpriteBatch batch = Manager.SpriteBatch;
            Viewport viewport = Manager.Viewport;
            float alpha = Visibility;
            int slide = (int)(SlideAmount * ItemSlide * uiScale * (IsDisappearing ? 1f : -1f));

            //Level images are clipped to the list area (vertically only, so they can slide in from the side)
            Rectangle previousScissor = Manager.GraphicsDevice.ScissorRectangle;
            Manager.GraphicsDevice.ScissorRectangle = new Rectangle(0, (int)listTop, viewport.Width, (int)Math.Ceiling(listBottom - listTop));

            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, ScissorRasterizer);
            for (int i = 0; i < levels.Count; i++)
            {
                Rectangle bounds = LevelBounds(i);
                if (bounds.Bottom < listTop || bounds.Top > listBottom)
                    continue;

                bounds.Offset(slide, 0);
                if (images[i] != null)
                    batch.Draw(images[i], bounds, Color.White * alpha);
                else
                    DrawPlaceholder(batch, bounds, levels[i], alpha);

                if (i == focusedLevel && HasFocus)
                    DrawFrame(batch, bounds, Math.Max(2, (int)(4f * uiScale)), MainMenuScreen.Yellow * alpha);
            }
            batch.End();
            Manager.GraphicsDevice.ScissorRectangle = previousScissor;

            batch.Begin();
            if (showScrollBar)
            {
                Vector2 pointer = Manager.Input.PointerPosition;
                DrawScrollBarButton(batch, arrowTexture, arrowUpY, false, drag == Drag.ArrowUp || ScrollBarButtonBounds(arrowTexture, arrowUpY).Contains(pointer), slide, alpha);
                DrawScrollBarButton(batch, arrowTexture, arrowDownY, true, drag == Drag.ArrowDown || ScrollBarButtonBounds(arrowTexture, arrowDownY).Contains(pointer), slide, alpha);
                DrawScrollBarButton(batch, sliderTexture, SliderY, false, drag == Drag.Slider || ScrollBarButtonBounds(sliderTexture, SliderY).Contains(pointer), slide, alpha);
            }
            MainMenuScreen.DrawTitle(Manager, title, Manager.SafeArea.Top - SlideAmount * TitleSlide * uiScale, alpha);
            if (levels.Count == 0)
                DrawCenteredText(batch, "No levels found", new Rectangle(0, (int)listTop, viewport.Width, (int)(listBottom - listTop) / 2), 0.6f * uiScale, alpha);
            batch.End();
        }

        private void DrawScrollBarButton(SpriteBatch batch, Texture2D texture, float centerY, bool flip, bool highlighted, int slide, float alpha)
        {
            //Highlighted buttons are yellow and slightly bigger
            float scale = ScrollBarScale * uiScale * (highlighted ? 1.1f : 1f);
            Color color = (highlighted && HasFocus ? MainMenuScreen.Yellow : Color.White) * alpha;
            Vector2 origin = new Vector2(texture.Width, texture.Height) / 2f;
            batch.Draw(texture, new Vector2(scrollBarX + slide, centerY), null, color, 0f, origin, scale, flip ? SpriteEffects.FlipVertically : SpriteEffects.None, 0f);
        }

        /// <summary>A dark rectangle with the level name, for a level without a preview image.</summary>
        private void DrawPlaceholder(SpriteBatch batch, Rectangle bounds, string level, float alpha)
        {
            batch.Draw(Manager.Pixel, bounds, new Color(30, 30, 30) * (0.85f * alpha));
            DrawFrame(batch, bounds, Math.Max(1, (int)(2f * uiScale)), Color.Gray * alpha);

            //The level name in the menu font, as big as fits into the rectangle
            SpriteFont font = Manager.Fonts.Menu;
            Vector2 size = font.MeasureString(level);
            float scale = Math.Min(bounds.Width * 0.8f / size.X, bounds.Height * 0.35f / size.Y);
            DrawCenteredText(batch, level, bounds, scale, alpha);
        }

        private void DrawCenteredText(SpriteBatch batch, string text, Rectangle bounds, float scale, float alpha)
        {
            Vector2 size = Manager.Fonts.Menu.MeasureString(text) * scale;
            Vector2 position = new Vector2(bounds.Center.X - size.X / 2f, bounds.Center.Y - size.Y / 2f);
            Manager.DrawShadowedText(Manager.Fonts.Menu, text, position, Color.White * alpha, Color.Black * alpha, scale);
        }

        private void DrawFrame(SpriteBatch batch, Rectangle bounds, int thickness, Color color)
        {
            Texture2D pixel = Manager.Pixel;
            batch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
            batch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
            batch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
            batch.Draw(pixel, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
        }
    }
}
