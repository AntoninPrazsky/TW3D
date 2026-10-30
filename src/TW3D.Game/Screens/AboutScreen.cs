using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TW3D.Input;

namespace TW3D.Screens
{
    /// <summary>
    /// About the game: what it is, who made it and the link to its source code on GitHub, which opens in the web browser.
    /// The link and Back are chosen by the mouse or touch, the arrow keys and Enter, or a gamepad; Esc (Back) closes the
    /// screen. The text is in the middle of the screen below the title, made smaller where it does not fit (phones).
    /// </summary>
    public sealed class AboutScreen : Screen
    {
        public const string RepositoryUrl = "https://github.com/AntoninPrazsky/TW3D";

        //Font scales of the name of the game, of the text, of the small print, of the link and of Back
        private const float NameScale = 0.8f;
        private const float TextScale = 0.5f;
        private const float SmallScale = 0.4f;
        private const float LinkScale = 0.55f;
        private const float BackScale = 0.8f;
        //The widest the text may be, the gap between the groups of paragraphs and the least gap between the text
        //and the title or Back, in reference pixels
        private const float MaxTextWidth = 1500f;
        private const float GroupGap = 40f;
        private const float EdgeGap = 24f;
        //Extra area around the link and Back that reacts to the pointer, in reference pixels
        private const float Padding = 20f;
        //How much the picture of the garage is darkened, so the text is easy to read
        private const float Dimming = 0.45f;
        //How far the title and the texts slide in and out, in reference pixels
        private const float TitleSlide = 140f;
        private const float TextSlide = 320f;

        private enum Item
        {
            None,
            Link,
            Back
        }

        private sealed class Paragraph
        {
            public string Text;
            public float Scale;
            public Color Color;
            /// <summary>The paragraph starts a new group, after a gap.</summary>
            public bool StartsGroup;
            public bool IsLink;
        }

        /// <summary>A line of a paragraph as laid out on the screen.</summary>
        private sealed class Line
        {
            public string Text;
            public Paragraph Paragraph;
            //From the top of the text in reference pixels (before it is made smaller to fit), and on the screen
            public float Top;
            public Vector2 Position;
            public float Scale;
        }

        //The font has only the characters of ASCII, so the name is written without its accents
        private readonly Paragraph[] paragraphs =
        {
            new Paragraph { Text = "Triple Wheel 3D", Scale = NameScale, Color = Color.White },
            new Paragraph { Text = "Version " + typeof(AboutScreen).Assembly.GetName().Version.ToString(3), Scale = SmallScale, Color = Color.LightGray },
            new Paragraph { Text = "An obstacle course driving game.", Scale = TextScale, Color = Color.White, StartsGroup = true },
            new Paragraph { Text = "Rotate and lock the axles of the car to get it through the track.", Scale = TextScale, Color = Color.White },
            new Paragraph { Text = "Created in 2016 for a bachelor's thesis, brought up to date in 2026.", Scale = SmallScale, Color = Color.LightGray, StartsGroup = true },
            new Paragraph { Text = "Made with MonoGame and Aether.Physics2D", Scale = SmallScale, Color = Color.LightGray },
            new Paragraph { Text = "Copyright (c) 2014-2026 Antonin Prazsky, MIT License", Scale = SmallScale, Color = Color.LightGray },
            new Paragraph { Text = "Source code on GitHub", Scale = SmallScale, Color = Color.LightGray, StartsGroup = true },
            new Paragraph { Text = RepositoryUrl.Substring("https://".Length), Scale = LinkScale, Color = Color.White, IsLink = true }
        };

        private readonly List<Line> lines = new List<Line>();
        private Item focused = Item.None;
        private Rectangle link, linkText, back, backText;
        //The screen size and the scale of the user interface the text was laid out for
        private Point layoutSize;
        private float layoutScale;

        public AboutScreen()
        {
            FadeInTime = TimeSpan.FromSeconds(0.7);
            FadeOutTime = TimeSpan.FromSeconds(0.5);
        }

        public override void HandleInput(InputState input, GameTime gameTime)
        {
            UpdateLayout();

            //The mouse focuses the item under it when it moves (a mouse at rest does not take the focus from the keys)
            Item hovered = ItemAt(input.PointerPosition);
            if (input.Mouse.Position != input.PreviousMouse.Position)
                focused = hovered;

            if (input.UpPressed || input.LeftPressed)
                focused = focused == Item.Back ? Item.Link : Item.Back;
            else if (input.DownPressed || input.RightPressed)
                focused = focused == Item.Link ? Item.Back : Item.Link;

            if (input.PointerPressed && hovered != Item.None)
                Activate(hovered);
            else if (input.ConfirmPressed && focused != Item.None)
                Activate(focused);
            else if (input.BackPressed)
                Close();
        }

        private Item ItemAt(Vector2 position)
        {
            return link.Contains(position) ? Item.Link : back.Contains(position) ? Item.Back : Item.None;
        }

        private void Activate(Item item)
        {
            if (item == Item.Link)
                OpenLink(RepositoryUrl);
            else
                Close();
        }

        /// <summary>Opens the address in the web browser of the system.</summary>
        private static void OpenLink(string url)
        {
#if ANDROID
            Android.Content.Intent intent = new Android.Content.Intent(Android.Content.Intent.ActionView, Android.Net.Uri.Parse(url));
            //Started from outside an activity, the browser needs a task of its own
            intent.AddFlags(Android.Content.ActivityFlags.NewTask);
            try
            {
                Android.App.Application.Context.StartActivity(intent);
            }
            catch (Android.Content.ActivityNotFoundException)
            {
                //No web browser on the phone
            }
#else
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
            }
            catch (Win32Exception)
            {
                //No web browser to open it
            }
#endif
        }

        /// <summary>
        /// Breaks the paragraphs into lines and places them, Back and the link on the screen;
        /// again only when the size of the screen or of the user interface changes.
        /// </summary>
        private void UpdateLayout()
        {
            Viewport viewport = Manager.Viewport;
            float uiScale = Manager.UiScale;
            if (layoutSize == viewport.Bounds.Size && layoutScale == uiScale)
                return;
            layoutSize = viewport.Bounds.Size;
            layoutScale = uiScale;

            Rectangle safe = Manager.SafeArea;
            SpriteFont font = Manager.Fonts.Menu;
            float lineHeight = font.MeasureString("M").Y;
            int padding = (int)(Padding * uiScale);

            //Back at the left end of the bottom row, as in the garage
            Vector2 backSize = font.MeasureString("Back") * BackScale * uiScale;
            backText = new Rectangle(safe.Left, (int)(safe.Bottom - backSize.Y), (int)backSize.X, (int)backSize.Y);
            back = backText;
            back.Inflate(padding, padding);

            //The paragraphs broken into lines as wide as the screen allows
            float maxWidth = Math.Min(MaxTextWidth, safe.Width / uiScale);
            float height = 0f;
            lines.Clear();
            foreach (Paragraph paragraph in paragraphs)
            {
                if (paragraph.StartsGroup)
                    height += GroupGap;
                foreach (string text in Wrap(font, paragraph.Text, maxWidth / paragraph.Scale))
                {
                    lines.Add(new Line { Text = text, Paragraph = paragraph, Top = height });
                    height += lineHeight * paragraph.Scale;
                }
            }

            //Between the title and Back, in the middle of the screen if there is room for it, made smaller if there is not
            float top = safe.Top + MainMenuScreen.TitleHeight(Manager) + EdgeGap * uiScale;
            float bottom = backText.Top - EdgeGap * uiScale;
            float scale = uiScale * Math.Min(1f, (bottom - top) / (height * uiScale));
            float y = MathHelper.Clamp((viewport.Height - height * scale) / 2f, top, bottom - height * scale);
            foreach (Line line in lines)
            {
                line.Scale = line.Paragraph.Scale * scale;
                Vector2 size = font.MeasureString(line.Text) * line.Scale;
                line.Position = new Vector2((viewport.Width - size.X) / 2f, y + line.Top * scale);
                if (line.Paragraph.IsLink)
                {
                    linkText = new Rectangle((int)line.Position.X, (int)line.Position.Y, (int)size.X, (int)size.Y);
                    link = linkText;
                    link.Inflate(padding, padding / 2);
                }
            }
        }

        /// <summary>The words of the text in lines at most as wide as given (in the units of the font).</summary>
        private static IEnumerable<string> Wrap(SpriteFont font, string text, float maxWidth)
        {
            string line = "";
            foreach (string word in text.Split(' '))
            {
                string longer = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && font.MeasureString(longer).X > maxWidth)
                {
                    yield return line;
                    line = word;
                }
                else
                    line = longer;
            }
            yield return line;
        }

        public override void Draw(GameTime gameTime)
        {
            UpdateLayout();

            SpriteFont font = Manager.Fonts.Menu;
            SpriteBatch batch = Manager.SpriteBatch;
            float uiScale = Manager.UiScale;
            float alpha = Visibility;
            Color shadow = Color.DarkSlateGray * (alpha * alpha);

            //The texts slide in from the left and out to the right, the title slides from the top
            float slide = SlideAmount * TextSlide * uiScale * (IsDisappearing ? 1f : -1f);

            batch.Begin();
            batch.Draw(Manager.Pixel, Manager.Viewport.Bounds, Color.Black * (Dimming * alpha));

            foreach (Line line in lines)
            {
                Color color = line.Paragraph.IsLink && focused == Item.Link && HasFocus ? MainMenuScreen.Yellow : line.Paragraph.Color;
                Manager.DrawShadowedText(font, line.Text, line.Position + new Vector2(slide, 0f), color * alpha, shadow, line.Scale);
            }

            //The link is underlined, with a shadow like its text
            Color linkColor = focused == Item.Link && HasFocus ? MainMenuScreen.Yellow : Color.White;
            int thickness = (int)Math.Max(1f, 3f * uiScale);
            int shadowOffset = (int)Math.Max(1f, 2f * uiScale);
            Rectangle underline = new Rectangle(linkText.X + (int)slide, linkText.Bottom - thickness, linkText.Width, thickness);
            batch.Draw(Manager.Pixel, new Rectangle(underline.X + shadowOffset, underline.Y + shadowOffset, underline.Width, underline.Height), shadow);
            batch.Draw(Manager.Pixel, underline, linkColor * alpha);

            Color backColor = focused == Item.Back && HasFocus ? MainMenuScreen.Yellow : Color.White;
            Manager.DrawShadowedText(font, "Back", new Vector2(backText.X + slide, backText.Y), backColor * alpha, shadow, BackScale * uiScale);

            MainMenuScreen.DrawTitle(Manager, "About", Manager.SafeArea.Top - SlideAmount * TitleSlide * uiScale, alpha);

            batch.End();
        }
    }
}
