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
    /// the name of the car (or left and right, or dragging the car sideways) show the other cars; the car shown is
    /// the one chosen, saved at once.
    /// </summary>
    public sealed class GarageScreen : Screen
    {
        //How fast the car turns (radians per second); the camera looks at it from this distance (world units),
        //a little from above, with this vertical field of view (degrees)
        private const float TurnSpeed = 0.5f;
        private const float CameraDistance = 5.2f;
        private const float CameraRise = 0.3f;
        private const float FieldOfView = 30f;
        //The cars stand in a row, the next one just out of the screen: this far from the edge (world units, a car
        //reaches about 1.5 from its middle). The row follows a pointer dragging it sideways. Dragged by this part
        //of the screen width, or flicked faster than this (reference pixels per second), it brings the next car;
        //let go, it settles at this rate (1 / seconds)
        private const float CarReach = 1.8f;
        private const float SwitchDistance = 0.2f;
        private const float FlickSpeed = 800f;
        private const float SettleRate = 10f;
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

        //How far the row of cars is moved right of its place (pixels), while dragged or settling
        private float scroll;
        //The pointer dragging the row (null = none), where it was in the last frame and how fast it moves (pixels per second)
        private int? dragPointer;
        private float dragX;
        private float dragVelocity;
        private readonly List<(CarType Car, float X)> visibleCars = new List<(CarType, float)>();

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

        //The camera comes from the distance as the screen appears
        private float Distance => CameraDistance * (1f + 2f * SlideAmount);

        //World units across the view per pixel of the screen, where the cars stand
        private float UnitsPerPixel => 2f * Distance * MathF.Tan(MathHelper.ToRadians(FieldOfView) / 2f) / Manager.Viewport.Height;

        //How far apart the cars of the row are (pixels)
        private float Spacing => Manager.Viewport.Width / 2f + CarReach / UnitsPerPixel;

        private void Show(int direction)
        {
            if (cars.Count < 2)
                return;
            selected = (selected + direction + cars.Count) % cars.Count;
            //The row moves by one car: the car chosen stays where it is on the screen, then settles in the middle
            scroll = MathHelper.Clamp(scroll + direction * Spacing, -Spacing, Spacing);
            settings.Car = cars[selected].Model;
            settings.Save();
        }

        public override void Update(GameTime gameTime)
        {
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            angle += TurnSpeed * elapsed;

            if (!HasFocus)
                dragPointer = null;
            if (dragPointer == null)
            {
                scroll *= MathF.Exp(-SettleRate * elapsed);
                if (Math.Abs(scroll) < 0.5f)
                    scroll = 0f;
            }
        }

        public override void HandleInput(InputState input, GameTime gameTime)
        {
            UpdateLayout();
            if (Drag(input, (float)gameTime.ElapsedGameTime.TotalSeconds))
                return;

            Vector2 pointer = input.PointerPosition;

            if (input.LeftPressed || (input.PointerPressed && leftArrow.Contains(pointer)))
                Show(-1);
            else if (input.RightPressed || (input.PointerPressed && rightArrow.Contains(pointer)))
                Show(1);
            else if (input.BackPressed || input.ConfirmPressed || (input.PointerPressed && back.Contains(pointer)))
                Close();
        }

        /// <summary>Moves the row of cars with the pointer dragging it (pressed outside the buttons); true while it does.</summary>
        private bool Drag(InputState input, float elapsed)
        {
            foreach (Pointer pointer in input.Pointers)
            {
                if (dragPointer == null && pointer.Phase == PointerPhase.Pressed && cars.Count > 1
                    && !leftArrow.Contains(pointer.Position) && !rightArrow.Contains(pointer.Position) && !back.Contains(pointer.Position))
                {
                    dragPointer = pointer.Id;
                    dragX = pointer.Position.X;
                    dragVelocity = 0f;
                    return true;
                }
                if (pointer.Id != dragPointer)
                    continue;

                float move = pointer.Position.X - dragX;
                dragX = pointer.Position.X;
                scroll = MathHelper.Clamp(scroll + move, -Spacing, Spacing);
                if (elapsed > 0f)
                    dragVelocity = MathHelper.Lerp(dragVelocity, move / elapsed, 0.5f);
                if (pointer.Phase == PointerPhase.Released)
                    Release();
                return true;
            }

            //The touch was lost without being released
            if (dragPointer != null)
                Release();
            return false;
        }

        /// <summary>Lets go of the row of cars: dragged or flicked far enough, it brings the next car in that direction.</summary>
        private void Release()
        {
            dragPointer = null;
            float direction = 0f;
            if (Math.Abs(dragVelocity) > FlickSpeed * Manager.UiScale)
                direction = dragVelocity;
            else if (Math.Abs(scroll) > SwitchDistance * Manager.Viewport.Width)
                direction = scroll;

            //Dragged to the left, the car on the right comes
            if (direction < 0f)
                Show(1);
            else if (direction > 0f)
                Show(-1);
        }

        /// <summary>The cars on the screen and how far right of its middle they are (pixels).</summary>
        private List<(CarType Car, float X)> VisibleCars()
        {
            visibleCars.Clear();
            for (int offset = -1; offset <= 1; offset++)
            {
                float x = offset * Spacing + scroll;
                if (Math.Abs(x) < Spacing)
                    visibleCars.Add((cars[(selected + offset + cars.Count) % cars.Count], x));
            }
            return visibleCars;
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

            //The car turning in the middle of the screen. Each car of the row is drawn by itself, moved sideways
            //in front of the camera
            Vector3 target = new Vector3(0f, -0.25f, 0f);
            Vector3 eye = target + Vector3.Normalize(new Vector3(MathF.Sin(angle), CameraRise, MathF.Cos(angle))) * Distance;
            Matrix view = Matrix.CreateLookAt(eye, target, Vector3.Up);
            Matrix projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(FieldOfView), viewport.AspectRatio, 0.1f, 100f);
            foreach ((CarType car, float x) in VisibleCars())
            {
                renderer.Begin(view * Matrix.CreateTranslation(x * UnitsPerPixel, 0f, 0f), projection);
                Preview(car).Draw(renderer);
                renderer.End();
            }

            SpriteFont font = Manager.Fonts.Menu;
            float slide = SlideAmount * TextSlide * uiScale * (IsDisappearing ? 1f : -1f);
            Vector2 pointer = Manager.Input.PointerPosition;

            Manager.SpriteBatch.Begin();

            //The names move with the cars, fading out before they reach the arrows
            foreach ((CarType car, float x) in visibleCars)
            {
                float nameWidth = font.MeasureString(car.Name).X * TextScale * uiScale;
                float nameAlpha = alpha * MathHelper.Clamp(1f - Math.Abs(x) / (viewport.Width / 4f), 0f, 1f);
                Manager.DrawShadowedText(font, car.Name, new Vector2((viewport.Width - nameWidth) / 2f + x + slide, namePosition.Y),
                    Color.White * nameAlpha, Color.DarkSlateGray * (nameAlpha * nameAlpha), TextScale * uiScale);
            }
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
