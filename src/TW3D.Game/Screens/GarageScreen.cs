using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using nkast.Aether.Physics2D.Dynamics;
using TW3D.GameObjects;
using TW3D.Graphics;
using TW3D.Input;
using TW3D.Shared;

namespace TW3D.Screens
{
    /// <summary>
    /// The garage: the cars of the catalog, one at a time, turning in the middle of the screen. The arrows next to
    /// the name of the car (or left and right) show the other cars; the car shown is the one chosen, saved at once.
    /// </summary>
    public sealed class GarageScreen : Screen
    {
        //How fast the car turns (radians per second); the camera looks at it from this distance (world units),
        //a little from above, with this vertical field of view (degrees)
        private const float TurnSpeed = 0.5f;
        private const float CameraDistance = 5.2f;
        private const float CameraRise = 0.3f;
        private const float FieldOfView = 30f;
        //Font scale of the name and of Back, the size of the arrows and their distance from the name, in reference pixels
        private const float TextScale = 0.8f;
        private const float ArrowScale = 0.6f;
        private const float ArrowSpacing = 40f;
        //Extra area around Back and the arrows that reacts to the pointer, in reference pixels
        private const float Padding = 20f;
        //How much the picture of the garage is darkened, so the car stands out
        private const float Dimming = 0.45f;
        //How far the title and the texts slide in and out, in reference pixels
        private const float TitleSlide = 140f;
        private const float TextSlide = 320f;

        private readonly GameSettings settings;
        private readonly Dictionary<CarType, Car> previews = new Dictionary<CarType, Car>();
        private List<CarType> cars;
        private int selected;
        private float angle = 0.8f;

        private World world;
        private SceneRenderer renderer;
        private Texture2D arrowTexture;

        private Vector2 namePosition;
        private Rectangle leftArrow, rightArrow, back, backText;

        public GarageScreen(GameSettings settings)
        {
            this.settings = settings;
            FadeInTime = TimeSpan.FromSeconds(0.7);
            FadeOutTime = TimeSpan.FromSeconds(0.5);
        }

        public override void LoadContent()
        {
            using (Stream stream = TitleContainer.OpenStream("Content/" + Catalog.FileName))
                cars = Catalog.Load(stream).Cars;
            selected = Math.Max(0, cars.FindIndex(car => car.Model == settings.Car));

            //The cars are built as for a level, in a world of their own that never moves
            ModelCollision.Load();
            world = new World(Vector2.Zero);
            //Without shadows and ambient occlusion: they are drawn into textures first, which clears the screen
            //with the picture of the garage behind the car
            renderer = new SceneRenderer(Manager.GraphicsDevice, Manager.Content)
            {
                Classic = settings.Classic,
                Shadows = false,
                AmbientOcclusion = false,
                Lighting = Lighting.Day
            };
            arrowTexture = Manager.Content.Load<Texture2D>("Common/arrow");
        }

        private Car Preview(CarType car)
        {
            if (!previews.TryGetValue(car, out Car preview))
            {
                preview = new Car(world, Manager, null, null, Vector2.Zero, car.Model, car.Wheel);
                previews.Add(car, preview);
            }
            return preview;
        }

        private void Show(int direction)
        {
            selected = (selected + direction + cars.Count) % cars.Count;
            settings.Car = cars[selected].Model;
            settings.Save();
        }

        public override void Update(GameTime gameTime)
        {
            angle += TurnSpeed * (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        public override void HandleInput(InputState input, GameTime gameTime)
        {
            UpdateLayout();
            Vector2 pointer = input.PointerPosition;

            if (input.LeftPressed || (input.PointerPressed && leftArrow.Contains(pointer)))
                Show(-1);
            else if (input.RightPressed || (input.PointerPressed && rightArrow.Contains(pointer)))
                Show(1);
            else if (input.BackPressed || input.ConfirmPressed || (input.PointerPressed && back.Contains(pointer)))
                Close();
        }

        private void UpdateLayout()
        {
            Rectangle safe = Manager.SafeArea;
            SpriteFont font = Manager.Fonts.Menu;
            float uiScale = Manager.UiScale;
            float lineHeight = font.MeasureString("M").Y * TextScale * uiScale;
            int padding = (int)(Padding * uiScale);

            //The name of the car in the middle of the bottom row, between the arrows; Back at its left end
            Vector2 nameSize = font.MeasureString(cars[selected].Name) * TextScale * uiScale;
            float rowCenter = safe.Bottom - lineHeight / 2f;
            namePosition = new Vector2((Manager.Viewport.Width - nameSize.X) / 2f, rowCenter - lineHeight / 2f);

            //The arrow of the texture points up; turned, its width is its height
            Point arrowSize = (new Vector2(arrowTexture.Height, arrowTexture.Width) * ArrowScale * uiScale).ToPoint();
            float spacing = ArrowSpacing * uiScale;
            leftArrow = new Rectangle((int)(namePosition.X - spacing) - arrowSize.X, (int)(rowCenter - arrowSize.Y / 2f), arrowSize.X, arrowSize.Y);
            rightArrow = new Rectangle((int)(namePosition.X + nameSize.X + spacing), leftArrow.Y, arrowSize.X, arrowSize.Y);
            leftArrow.Inflate(padding, padding);
            rightArrow.Inflate(padding, padding);

            Vector2 backSize = font.MeasureString("Back") * TextScale * uiScale;
            backText = new Rectangle(safe.Left, (int)(rowCenter - lineHeight / 2f), (int)backSize.X, (int)lineHeight);
            back = backText;
            back.Inflate(padding, padding);
        }

        public override void Draw(GameTime gameTime)
        {
            UpdateLayout();
            Viewport viewport = Manager.Viewport;
            float uiScale = Manager.UiScale;
            float alpha = Visibility;

            Manager.SpriteBatch.Begin();
            Manager.SpriteBatch.Draw(Manager.Pixel, viewport.Bounds, Color.Black * (Dimming * alpha));
            Manager.SpriteBatch.End();

            //The car turning in the middle of the screen; it comes from the distance as the screen appears
            Vector3 target = new Vector3(0f, -0.25f, 0f);
            float distance = CameraDistance * (1f + 2f * SlideAmount);
            Vector3 eye = target + Vector3.Normalize(new Vector3(MathF.Sin(angle), CameraRise, MathF.Cos(angle))) * distance;
            renderer.Begin(Matrix.CreateLookAt(eye, target, Vector3.Up),
                Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(FieldOfView), viewport.AspectRatio, 0.1f, 100f));
            Preview(cars[selected]).Draw(renderer);
            renderer.End();

            SpriteFont font = Manager.Fonts.Menu;
            float slide = SlideAmount * TextSlide * uiScale * (IsDisappearing ? 1f : -1f);
            Vector2 pointer = Manager.Input.PointerPosition;

            Manager.SpriteBatch.Begin();

            Manager.DrawShadowedText(font, cars[selected].Name, namePosition + new Vector2(slide, 0f), Color.White * alpha,
                Color.DarkSlateGray * (alpha * alpha), TextScale * uiScale);
            DrawArrow(leftArrow, -MathHelper.PiOver2, pointer, slide, alpha);
            DrawArrow(rightArrow, MathHelper.PiOver2, pointer, slide, alpha);

            Color backColor = back.Contains(pointer) && HasFocus ? MainMenuScreen.Yellow : Color.White;
            Manager.DrawShadowedText(font, "Back", new Vector2(backText.X + slide, backText.Y), backColor * alpha,
                Color.DarkSlateGray * (alpha * alpha), TextScale * uiScale);

            MainMenuScreen.DrawTitle(Manager, "Garage", Manager.SafeArea.Top - SlideAmount * TitleSlide * uiScale, alpha);

            Manager.SpriteBatch.End();
        }

        /// <summary>An arrow turned to the side; yellow and slightly bigger under the pointer.</summary>
        private void DrawArrow(Rectangle bounds, float rotation, Vector2 pointer, float slide, float alpha)
        {
            bool highlighted = bounds.Contains(pointer) && HasFocus;
            float scale = ArrowScale * Manager.UiScale * (highlighted ? 1.1f : 1f);
            Color color = (highlighted ? MainMenuScreen.Yellow : Color.White) * alpha;
            Vector2 origin = new Vector2(arrowTexture.Width, arrowTexture.Height) / 2f;
            Manager.SpriteBatch.Draw(arrowTexture, bounds.Center.ToVector2() + new Vector2(slide, 0f), null, color, rotation, origin, scale, SpriteEffects.None, 0f);
        }
    }
}
