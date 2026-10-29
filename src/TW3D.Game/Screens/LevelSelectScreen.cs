using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TW3D.Input;

namespace TW3D.Screens
{
    /// <summary>
    /// A scrollable grid of level preview images. The layout is designed in 1280×720 reference
    /// pixels and scaled uniformly to the screen, centred if the aspect ratio differs.
    /// </summary>
    public sealed class LevelSelectScreen : Screen
    {
        //Layout in reference pixels
        private const int Columns = 3;
        private const float ImageWidth = 350f;
        private const float ImageHeight = 197f;
        private const float Gap = 16f;
        private const float GridLeft = 168f;
        private const float ListTop = 92f;
        private const float ListBottom = 715f;
        private const float ScrollBarX = 84f;
        private const float ScrollUpY = 110f;
        private const float ScrollDownY = 680f;
        private const float SliderTop = 175f;
        private const float SliderBottom = 615f;

        private const float ArrowScrollSpeed = 400f;
        private const float WheelScrollStep = 80f;
        private const float DragThreshold = 10f;
        private const float ItemSlide = 256f;
        private const float TitleSlide = 100f;

        private readonly string title;
        private readonly List<string> levels;
        private readonly Action<string> startLevel;
        private readonly List<Texture2D> images = new List<Texture2D>();

        private Texture2D arrowTexture;
        private Texture2D sliderTexture;

        //Reference layout → screen: screen = origin + reference * scale
        private Vector2 origin;
        private float scale;

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
                images.Add(Manager.Content.Load<Texture2D>("Levels/levimgs/" + level));
        }

        private void UpdateLayout()
        {
            Viewport viewport = Manager.Viewport;
            scale = Manager.UiScale;
            origin = new Vector2(viewport.Width - ScreenManager.ReferenceWidth * scale, viewport.Height - ScreenManager.ReferenceHeight * scale) / 2f;
        }

        private Vector2 ToReference(Vector2 screenPoint)
        {
            return (screenPoint - origin) / scale;
        }

        private Vector2 ToScreen(Vector2 referencePoint)
        {
            return origin + referencePoint * scale;
        }

        private float MaxScroll
        {
            get
            {
                int rows = (levels.Count + Columns - 1) / Columns;
                float contentHeight = rows * ImageHeight + Math.Max(0, rows - 1) * Gap;
                return Math.Max(0f, contentHeight - (ListBottom - ListTop));
            }
        }

        private void ScrollTo(float value)
        {
            scroll = MathHelper.Clamp(value, 0f, MaxScroll);
        }

        /// <summary>Bounds of a level image in reference pixels (without the slide animation).</summary>
        private RectangleF LevelBounds(int index)
        {
            int column = index % Columns;
            int row = index / Columns;
            return new RectangleF(
                GridLeft + column * (ImageWidth + Gap),
                ListTop + row * (ImageHeight + Gap) - scroll,
                ImageWidth,
                ImageHeight);
        }

        private int LevelAt(Vector2 reference)
        {
            if (reference.Y < ListTop || reference.Y > ListBottom)
                return -1;
            for (int i = 0; i < levels.Count; i++)
                if (LevelBounds(i).Contains(reference))
                    return i;
            return -1;
        }

        private static RectangleF CenteredBounds(Texture2D texture, float x, float y)
        {
            return new RectangleF(x - texture.Width / 2f, y - texture.Height / 2f, texture.Width, texture.Height);
        }

        private float SliderY => MaxScroll > 0f ? MathHelper.Lerp(SliderTop, SliderBottom, scroll / MaxScroll) : SliderTop;

        public override void HandleInput(InputState input, GameTime gameTime)
        {
            UpdateLayout();
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            Vector2 pointer = ToReference(input.PointerPosition);
            bool canScroll = MaxScroll > 0f;

            int hovered = LevelAt(pointer);
            if (hovered >= 0 || input.Mouse.Position != input.PreviousMouse.Position)
                focusedLevel = hovered;

            if (input.PointerPressed)
            {
                pressPosition = pointer;
                pressScroll = scroll;
                if (canScroll && CenteredBounds(arrowTexture, ScrollBarX, ScrollUpY).Contains(pointer))
                    drag = Drag.ArrowUp;
                else if (canScroll && CenteredBounds(arrowTexture, ScrollBarX, ScrollDownY).Contains(pointer))
                    drag = Drag.ArrowDown;
                else if (canScroll && CenteredBounds(sliderTexture, ScrollBarX, SliderY).Contains(pointer))
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
                case Drag.Slider: ScrollTo((pointer.Y - SliderTop) / (SliderBottom - SliderTop) * MaxScroll); break;
                case Drag.PressedOnLevel:
                    //Moving the pressed pointer turns the press into scrolling of the list
                    if (canScroll && Math.Abs(pointer.Y - pressPosition.Y) > DragThreshold)
                        drag = Drag.List;
                    break;
                case Drag.List: ScrollTo(pressScroll - (pointer.Y - pressPosition.Y)); break;
            }

            if (input.PointerReleased)
            {
                if (drag == Drag.PressedOnLevel && LevelAt(pointer) == pressedLevel)
                    startLevel(levels[pressedLevel]);
                drag = Drag.None;
            }
            else if (input.Pointers.Count == 0)
                drag = Drag.None;

            if (input.WheelDelta != 0)
                ScrollTo(scroll - input.WheelDelta / 120f * WheelScrollStep);
            if (input.KeyPressed(Keys.PageUp))
                ScrollTo(scroll - (ListBottom - ListTop));
            if (input.KeyPressed(Keys.PageDown))
                ScrollTo(scroll + (ListBottom - ListTop));

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
            if (input.UpPressed) move = -Columns;
            if (input.DownPressed) move = Columns;
            if (move == 0)
                return;

            focusedLevel = focusedLevel < 0 ? 0 : MathHelper.Clamp(focusedLevel + move, 0, levels.Count - 1);

            //Scroll so that the focused level is fully visible
            RectangleF bounds = LevelBounds(focusedLevel);
            if (bounds.Y < ListTop)
                ScrollTo(scroll - (ListTop - bounds.Y));
            else if (bounds.Bottom > ListBottom)
                ScrollTo(scroll + (bounds.Bottom - ListBottom));
        }

        public override void Draw(GameTime gameTime)
        {
            UpdateLayout();
            SpriteBatch batch = Manager.SpriteBatch;
            float alpha = Visibility;
            float slide = SlideAmount * ItemSlide * (IsDisappearing ? 1f : -1f);

            //Level images are clipped to the list area (vertically only, so they can slide in from the side)
            Viewport viewport = Manager.Viewport;
            int clipTop = (int)ToScreen(new Vector2(0f, ListTop)).Y;
            int clipBottom = (int)Math.Ceiling(ToScreen(new Vector2(0f, ListBottom)).Y);
            Rectangle previousScissor = Manager.GraphicsDevice.ScissorRectangle;
            Manager.GraphicsDevice.ScissorRectangle = new Rectangle(0, clipTop, viewport.Width, clipBottom - clipTop);

            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, ScissorRasterizer);
            for (int i = 0; i < levels.Count; i++)
            {
                RectangleF bounds = LevelBounds(i);
                if (bounds.Bottom < ListTop || bounds.Y > ListBottom)
                    continue;

                Rectangle screenBounds = ToScreenRectangle(new RectangleF(bounds.X + slide, bounds.Y, bounds.Width, bounds.Height));
                batch.Draw(images[i], screenBounds, Color.White * alpha);

                if (i == focusedLevel && HasFocus)
                    DrawFrame(batch, screenBounds, Math.Max(2, (int)(4 * scale)), MainMenuScreen.Yellow * alpha);
            }
            batch.End();
            Manager.GraphicsDevice.ScissorRectangle = previousScissor;

            batch.Begin();
            if (MaxScroll > 0f)
            {
                Vector2 pointer = ToReference(Manager.Input.PointerPosition);
                DrawScrollButton(batch, arrowTexture, ScrollUpY, false, drag == Drag.ArrowUp || CenteredBounds(arrowTexture, ScrollBarX, ScrollUpY).Contains(pointer), slide, alpha);
                DrawScrollButton(batch, arrowTexture, ScrollDownY, true, drag == Drag.ArrowDown || CenteredBounds(arrowTexture, ScrollBarX, ScrollDownY).Contains(pointer), slide, alpha);
                DrawScrollButton(batch, sliderTexture, SliderY, false, drag == Drag.Slider || CenteredBounds(sliderTexture, ScrollBarX, SliderY).Contains(pointer), slide, alpha);
            }
            MainMenuScreen.DrawTitle(Manager, title, origin.Y - SlideAmount * TitleSlide * scale, alpha);
            batch.End();
        }

        private static readonly RasterizerState ScissorRasterizer = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };

        private void DrawScrollButton(SpriteBatch batch, Texture2D texture, float y, bool flip, bool highlighted, float slide, float alpha)
        {
            //Highlighted buttons are yellow and slightly bigger
            float buttonScale = scale * (highlighted ? 1.1f : 1f);
            Color color = (highlighted && HasFocus ? MainMenuScreen.Yellow : Color.White) * alpha;
            Vector2 center = ToScreen(new Vector2(ScrollBarX + slide, y));
            Vector2 textureCenter = new Vector2(texture.Width, texture.Height) / 2f;
            batch.Draw(texture, center, null, color, 0f, textureCenter, buttonScale, flip ? SpriteEffects.FlipVertically : SpriteEffects.None, 0f);
        }

        private void DrawFrame(SpriteBatch batch, Rectangle bounds, int thickness, Color color)
        {
            Texture2D pixel = Manager.Pixel;
            batch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
            batch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
            batch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
            batch.Draw(pixel, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
        }

        private Rectangle ToScreenRectangle(RectangleF reference)
        {
            Vector2 topLeft = ToScreen(new Vector2(reference.X, reference.Y));
            Vector2 bottomRight = ToScreen(new Vector2(reference.Right, reference.Bottom));
            return new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)(bottomRight.X - topLeft.X), (int)(bottomRight.Y - topLeft.Y));
        }

        /// <summary>A rectangle with float coordinates (reference pixels).</summary>
        private readonly struct RectangleF
        {
            public RectangleF(float x, float y, float width, float height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }

            public float X { get; }
            public float Y { get; }
            public float Width { get; }
            public float Height { get; }
            public float Right => X + Width;
            public float Bottom => Y + Height;

            public bool Contains(Vector2 point)
            {
                return point.X >= X && point.X < Right && point.Y >= Y && point.Y < Bottom;
            }
        }
    }
}
