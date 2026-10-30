using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using TW3D.Input;

namespace TW3D.Screens
{
    /// <summary>
    /// Keeps the stack of screens, updates and draws them and passes input to the topmost one.
    /// Also provides resources shared by all screens.
    /// </summary>
    public sealed class ScreenManager : DrawableGameComponent
    {
        /// <summary>The user interface is designed for this resolution and scaled to the actual one.</summary>
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;

        /// <summary>Distance of the user interface from the screen edges, in reference pixels.</summary>
        public const float EdgeMargin = 48f;

        private readonly List<Screen> screens = new List<Screen>();
        private readonly List<Screen> screensToUpdate = new List<Screen>();
        private bool contentLoaded;
        private Texture2D pointerTexture;

        public ScreenManager(Game game)
            : base(game)
        {
        }

        public ContentManager Content => Game.Content;

        public SpriteBatch SpriteBatch { get; private set; }

        public GameFonts Fonts { get; private set; }

        public InputState Input { get; } = new InputState();

        /// <summary>1×1 white texture for drawing rectangles.</summary>
        public Texture2D Pixel { get; private set; }

        public Viewport Viewport => GraphicsDevice.Viewport;

        /// <summary>
        /// Scale of the user interface: sizes designed for 1920×1080 scaled so that they fit the screen.
        /// On screens wider than 16:9 (e.g. 21:9) the height decides, so nothing gets out of the screen.
        /// </summary>
        public float UiScale => Math.Min(Viewport.Width / ReferenceWidth, Viewport.Height / ReferenceHeight) * UiSize;

        /// <summary>
        /// Size of the user interface relative to the screen; 1 is designed for monitors.
        /// Small screens (phones) need it bigger, both to be readable and to be hit by a finger.
        /// </summary>
        public float UiSize { get; set; } = 1f;

        /// <summary>
        /// Size of the on-screen controls relative to the screen, like <see cref="UiSize"/>. On phones they need not
        /// be as big as the text of the menus: they are not read, only hit by a thumb.
        /// </summary>
        public float ControlsSize { get; set; } = 1f;

        /// <summary>Scale of the on-screen controls (see <see cref="UiScale"/> and <see cref="ControlsSize"/>).</summary>
        public float ControlsScale => UiScale / UiSize * ControlsSize;

        /// <summary>The screen without the margin along its edges; the user interface stays inside it.</summary>
        public Rectangle SafeArea
        {
            get
            {
                Rectangle area = Viewport.Bounds;
                int margin = (int)(EdgeMargin * UiScale);
                area.Inflate(-margin, -margin);
                return area;
            }
        }

        public IReadOnlyList<Screen> Screens => screens;

        public void Add(Screen screen)
        {
            screen.Manager = this;
            screens.Add(screen);
            if (contentLoaded)
                screen.LoadContent();
        }

        protected override void LoadContent()
        {
            SpriteBatch = new SpriteBatch(GraphicsDevice);
            Fonts = new GameFonts(Content);
            Pixel = new Texture2D(GraphicsDevice, 1, 1);
            Pixel.SetData(new[] { Color.White });
            pointerTexture = Content.Load<Texture2D>("Common/cursor");

            contentLoaded = true;
            foreach (Screen screen in screens)
                screen.LoadContent();
        }

        protected override void UnloadContent()
        {
            foreach (Screen screen in screens)
                screen.UnloadContent();
            Pixel.Dispose();
            SpriteBatch.Dispose();
        }

        public override void Update(GameTime gameTime)
        {
            Input.Update();

            //Every screen below a screen that is not a popup is covered;
            //the topmost screen that is not closing gets the focus
            bool covered = false;
            Screen focused = null;
            for (int i = screens.Count - 1; i >= 0; i--)
            {
                Screen screen = screens[i];
                screen.SetCovered(covered);
                if (!screen.IsClosing)
                {
                    if (focused == null)
                        focused = screen;
                    if (!screen.IsPopupScreen)
                        covered = true;
                }
            }

            foreach (Screen screen in screens)
                screen.HasFocus = screen == focused && Game.IsActive;

            //Screens may add or close other screens while being updated
            screensToUpdate.Clear();
            screensToUpdate.AddRange(screens);
            foreach (Screen screen in screensToUpdate)
            {
                screen.UpdateVisibility(gameTime);
                screen.Update(gameTime);
            }

            if (focused != null && focused.HasFocus)
                focused.HandleInput(Input, gameTime);

            for (int i = screens.Count - 1; i >= 0; i--)
            {
                if (screens[i].IsClosing && screens[i].Visibility <= 0f)
                {
                    screens[i].UnloadContent();
                    screens.RemoveAt(i);
                }
            }
        }

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            //Nothing below a fully visible opaque screen can be seen
            int first = 0;
            for (int i = screens.Count - 1; i >= 0; i--)
            {
                if (screens[i].IsOpaque && screens[i].Visibility >= 1f)
                {
                    first = i;
                    break;
                }
            }

            for (int i = first; i < screens.Count; i++)
                if (screens[i].Visibility > 0f)
                    screens[i].Draw(gameTime);

            DrawPointers();
        }

        /// <summary>A small circle under every finger or the pressed mouse.</summary>
        private void DrawPointers()
        {
            Vector2 origin = new Vector2(pointerTexture.Width, pointerTexture.Height) / 2f;
            SpriteBatch.Begin();
            foreach (Pointer pointer in Input.Pointers)
                if (pointer.IsDown)
                    SpriteBatch.Draw(pointerTexture, pointer.Position, null, Color.White, 0f, origin, UiScale, SpriteEffects.None, 0f);
            SpriteBatch.End();
        }

        /// <summary>Draws text with a dark shadow; must be called between SpriteBatch.Begin and End.</summary>
        public void DrawShadowedText(SpriteFont font, string text, Vector2 position, Color color, Color shadow, float scale)
        {
            Vector2 shadowOffset = new Vector2(Math.Max(1f, 2f * UiScale));
            SpriteBatch.DrawString(font, text, position + shadowOffset, shadow, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            SpriteBatch.DrawString(font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
