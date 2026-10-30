using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TW3D.GameObjects;
using TW3D.Input;
using TW3D.Shared;

namespace TW3D.Screens
{
    /// <summary>
    /// The garage: the camera of the 3D garage behind the menus (<see cref="GarageScene"/>) comes to the car on the
    /// lift. The arrows next to the name of the car (or left and right, or dragging the car sideways) move the row of
    /// cars, so that another car comes onto the lift; the car on the lift is the one chosen. On the right the wheels of
    /// its front and rear axle are chosen (any wheels fit any car), on the left bars show how fast, heavy and gripping
    /// it is with them. Everything chosen is saved at once.
    /// </summary>
    public sealed class GarageScreen : Screen
    {
        //The row of cars follows a pointer dragging it sideways. Dragged by this part of the distance of the cars, or
        //flicked faster than this (reference pixels per second), it brings the next car; let go, it settles at this
        //rate (1 / seconds)
        private const float SwitchDistance = 0.25f;
        private const float FlickSpeed = 800f;
        private const float SettleRate = 10f;
        //Font scale of the name and of Back, the size of the arrows and their distance from the name, in reference pixels
        private const float TextScale = 0.8f;
        private const float ArrowScale = 0.6f;
        private const float ArrowSpacing = 40f;
        //The wheels and the bars at the sides: font scales of the values and of the labels, the size of the arrows,
        //the gaps between the rows, the size of the bars (reference pixels), the rate the bars follow changes (1 / seconds)
        private const float ValueScale = 0.55f;
        private const float LabelScale = 0.4f;
        private const float SmallArrowScale = 0.42f;
        private const float RowGap = 36f;
        private const float BarWidth = 260f;
        private const float BarHeight = 14f;
        private const float BarRate = 10f;
        //Extra area around Back and the arrows that reacts to the pointer, in reference pixels
        private const float Padding = 20f;
        //How far the title and the texts slide in and out, in reference pixels
        private const float TitleSlide = 140f;
        private const float TextSlide = 320f;

        //What the arrows and keys change: the wheels of an axle or the car
        private enum Row
        {
            FrontWheels,
            RearWheels,
            Car
        }

        /// <summary>The choice of the wheels of an axle: its label and its value between two arrows.</summary>
        private sealed class WheelChoice
        {
            public Row Row;
            public string Label;
            public Vector2 LabelPosition;
            public Rectangle Left, Right, Value;
        }

        private static readonly string[] StatNames = { "Speed", "Weight", "Grip" };

        private readonly GameSettings settings;
        private readonly GarageScene scene;
        private Catalog catalog;
        private List<CarType> cars;
        private int selected;
        private Row focus = Row.Car;
        private readonly WheelChoice[] wheelChoices =
        {
            new WheelChoice { Row = Row.FrontWheels, Label = "Front wheels" },
            new WheelChoice { Row = Row.RearWheels, Label = "Rear wheels" }
        };
        //The bars as drawn (following the values of the car shown)
        private readonly float[] bars = new float[StatNames.Length];

        //How far the row of cars is moved along its axis (world units, see GarageScene.Scroll), while dragged or settling
        private float scroll;
        //The pointer dragging the row (null = none), where it was in the last frame and how fast it moves (pixels per second)
        private int? dragPointer;
        private float dragX;
        private float dragVelocity;

        private Texture2D arrowTexture;

        private Vector2 namePosition;
        private Rectangle leftArrow, rightArrow, back, backText;
        private Vector2 statsPosition;

        /// <param name="scene">The 3D garage behind the menus.</param>
        public GarageScreen(GameSettings settings, GarageScene scene)
        {
            this.settings = settings;
            this.scene = scene;
            FadeInTime = TimeSpan.FromSeconds(0.7);
            FadeOutTime = TimeSpan.FromSeconds(0.5);
        }

        public override void LoadContent()
        {
            using (Stream stream = TitleContainer.OpenStream("Content/" + Catalog.FileName))
                catalog = Catalog.Load(stream);
            cars = catalog.Cars;
            selected = Math.Max(0, cars.FindIndex(car => car.Model == settings.Car));
            Array.Copy(Stats(cars[selected]), bars, bars.Length);
            arrowTexture = Manager.Content.Load<Texture2D>("Common/arrow");
        }

        public override void UnloadContent()
        {
            scene.Focus = 0f;
            scene.Scroll = 0f;
        }

        /// <summary>How fast, heavy and gripping a car is with its wheels, each as a part of the most there is.</summary>
        private float[] Stats(CarType car)
        {
            CarWheels chosen = settings.WheelsOf(car);
            WheelType front = catalog.FindWheel(chosen.Front), rear = catalog.FindWheel(chosen.Rear);
            float heaviest = cars.Max(c => c.Mass) + 6f * catalog.Wheels.Max(wheel => wheel.Mass);
            return new[]
            {
                car.TopSpeed / cars.Max(c => c.TopSpeed),
                (car.Mass + 3f * (front.Mass + rear.Mass)) / heaviest,
                (front.Friction + rear.Friction) / 2f / catalog.Wheels.Max(wheel => wheel.Friction)
            };
        }

        private static float Spacing => GarageScene.Spacing;

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

        /// <summary>Puts the next (or previous) wheels of the catalog on an axle of the car shown.</summary>
        private void ChangeWheels(Row axle, int direction)
        {
            List<WheelType> wheels = catalog.Wheels;
            if (wheels.Count < 2)
                return;
            CarType car = cars[selected];
            CarWheels chosen = settings.WheelsOf(car);
            string current = axle == Row.FrontWheels ? chosen.Front : chosen.Rear;
            int index = Math.Max(0, wheels.FindIndex(wheel => wheel.Model == current));
            string next = wheels[(index + direction + wheels.Count) % wheels.Count].Model;
            settings.Wheels[car.Model] = new CarWheels
            {
                Front = axle == Row.FrontWheels ? next : chosen.Front,
                Rear = axle == Row.RearWheels ? next : chosen.Rear
            };
            settings.Save();
        }

        public override void Update(GameTime gameTime)
        {
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (!HasFocus)
                dragPointer = null;
            if (dragPointer == null)
            {
                scroll *= MathF.Exp(-SettleRate * elapsed);
                if (Math.Abs(scroll) < 0.5f)
                    scroll = 0f;
            }

            float[] stats = Stats(cars[selected]);
            float follow = 1f - MathF.Exp(-BarRate * elapsed);
            for (int i = 0; i < bars.Length; i++)
                bars[i] += (stats[i] - bars[i]) * follow;

            //The camera of the garage comes to the car as the screen appears, and goes back as it disappears
            scene.Focus = Visibility;
            scene.Scroll = scroll;
        }

        public override void HandleInput(InputState input, GameTime gameTime)
        {
            UpdateLayout();
            if (Drag(input, (float)gameTime.ElapsedGameTime.TotalSeconds))
                return;

            Vector2 pointer = input.PointerPosition;
            if (input.UpPressed)
                focus = (Row)(((int)focus + 2) % 3);
            else if (input.DownPressed)
                focus = (Row)(((int)focus + 1) % 3);

            int keys = input.LeftPressed ? -1 : input.RightPressed ? 1 : 0;
            if (keys != 0)
            {
                if (focus == Row.Car)
                    Show(keys);
                else
                    ChangeWheels(focus, keys);
                return;
            }

            if (input.PointerPressed)
            {
                if (leftArrow.Contains(pointer))
                    Show(-1);
                else if (rightArrow.Contains(pointer))
                    Show(1);
                foreach (WheelChoice choice in wheelChoices)
                {
                    int direction = choice.Left.Contains(pointer) ? -1 : choice.Right.Contains(pointer) || choice.Value.Contains(pointer) ? 1 : 0;
                    if (direction != 0)
                    {
                        focus = choice.Row;
                        ChangeWheels(choice.Row, direction);
                    }
                }
            }
            if (input.BackPressed || input.ConfirmPressed || (input.PointerPressed && back.Contains(pointer)))
                Close();
        }

        /// <summary>Back, the arrows or a wheel choice is at the position.</summary>
        private bool IsButton(Vector2 position)
        {
            return leftArrow.Contains(position) || rightArrow.Contains(position) || back.Contains(position)
                || wheelChoices.Any(choice => choice.Left.Contains(position) || choice.Right.Contains(position) || choice.Value.Contains(position));
        }

        /// <summary>Moves the row of cars with the pointer dragging it (pressed outside the buttons); true while it does.</summary>
        private bool Drag(InputState input, float elapsed)
        {
            foreach (Pointer pointer in input.Pointers)
            {
                if (dragPointer == null && pointer.Phase == PointerPhase.Pressed && cars.Count > 1 && !IsButton(pointer.Position))
                {
                    dragPointer = pointer.Id;
                    dragX = pointer.Position.X;
                    dragVelocity = 0f;
                    return true;
                }
                if (pointer.Id != dragPointer)
                    continue;

                //The cars follow the pointer on the screen
                float move = pointer.Position.X - dragX;
                dragX = pointer.Position.X;
                if (Math.Abs(scene.PixelsPerUnit) > 1f)
                    scroll = MathHelper.Clamp(scroll + move / scene.PixelsPerUnit, -Spacing, Spacing);
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
                direction = dragVelocity / scene.PixelsPerUnit;
            else if (Math.Abs(scroll) > SwitchDistance * Spacing)
                direction = scroll;

            //Moved backwards along the row, the next car comes
            if (direction < 0f)
                Show(1);
            else if (direction > 0f)
                Show(-1);
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

            //The wheels on the right, in the middle of the height: a label over the value between two arrows, the value
            //as wide as the widest name of wheels
            float labelHeight = font.MeasureString("M").Y * LabelScale * uiScale;
            float valueHeight = font.MeasureString("M").Y * ValueScale * uiScale;
            float valueWidth = catalog.Wheels.Max(wheel => font.MeasureString(wheel.Name).X) * ValueScale * uiScale;
            Point smallArrow = (new Vector2(arrowTexture.Height, arrowTexture.Width) * SmallArrowScale * uiScale).ToPoint();
            float smallSpacing = ArrowSpacing * 0.6f * uiScale;
            float rowHeight = labelHeight + valueHeight;
            float gap = RowGap * uiScale;
            float top = (safe.Top + safe.Bottom) / 2f - (2f * rowHeight + gap) / 2f;
            for (int i = 0; i < wheelChoices.Length; i++)
            {
                WheelChoice choice = wheelChoices[i];
                float y = top + i * (rowHeight + gap);
                float valueCenterY = y + labelHeight + valueHeight / 2f;
                choice.Right = new Rectangle(safe.Right - smallArrow.X, (int)(valueCenterY - smallArrow.Y / 2f), smallArrow.X, smallArrow.Y);
                float valueRight = choice.Right.X - smallSpacing;
                choice.Value = new Rectangle((int)(valueRight - valueWidth), (int)(y + labelHeight), (int)valueWidth, (int)valueHeight);
                choice.Left = new Rectangle((int)(choice.Value.X - smallSpacing) - smallArrow.X, choice.Right.Y, smallArrow.X, smallArrow.Y);
                float labelWidth = font.MeasureString(choice.Label).X * LabelScale * uiScale;
                choice.LabelPosition = new Vector2(choice.Value.Center.X - labelWidth / 2f, y);
                choice.Left.Inflate(padding, padding / 2);
                choice.Right.Inflate(padding, padding / 2);
            }

            //The bars on the left, in the middle of the height
            float statHeight = labelHeight + BarHeight * uiScale;
            statsPosition = new Vector2(safe.Left, (safe.Top + safe.Bottom) / 2f - (StatNames.Length * statHeight + (StatNames.Length - 1) * gap / 2f) / 2f);
        }

        public override void Draw(GameTime gameTime)
        {
            UpdateLayout();
            Viewport viewport = Manager.Viewport;
            float uiScale = Manager.UiScale;
            float alpha = Visibility;

            SpriteFont font = Manager.Fonts.Menu;
            float slide = SlideAmount * TextSlide * uiScale * (IsDisappearing ? 1f : -1f);
            Vector2 pointer = Manager.Input.PointerPosition;
            Color shadow = Color.DarkSlateGray * (alpha * alpha);

            Manager.SpriteBatch.Begin();

            //The names move with the cars, fading out before they reach the arrows
            foreach ((CarType car, float offset) in scene.Row())
            {
                float x = offset * scene.PixelsPerUnit;
                float nameWidth = font.MeasureString(car.Name).X * TextScale * uiScale;
                float nameAlpha = alpha * MathHelper.Clamp(1f - Math.Abs(x) / (viewport.Width / 4f), 0f, 1f);
                Manager.DrawShadowedText(font, car.Name, new Vector2((viewport.Width - nameWidth) / 2f + x + slide, namePosition.Y),
                    Color.White * nameAlpha, Color.DarkSlateGray * (nameAlpha * nameAlpha), TextScale * uiScale);
            }
            DrawArrow(leftArrow, -MathHelper.PiOver2, ArrowScale, pointer, slide, alpha);
            DrawArrow(rightArrow, MathHelper.PiOver2, ArrowScale, pointer, slide, alpha);

            //The wheels of the car shown, the chosen row in yellow
            CarWheels chosen = settings.WheelsOf(cars[selected]);
            foreach (WheelChoice choice in wheelChoices)
            {
                Manager.DrawShadowedText(font, choice.Label, choice.LabelPosition - new Vector2(slide, 0f), Color.LightGray * alpha, shadow, LabelScale * uiScale);
                string name = catalog.FindWheel(choice.Row == Row.FrontWheels ? chosen.Front : chosen.Rear).Name;
                float nameWidth = font.MeasureString(name).X * ValueScale * uiScale;
                bool highlighted = focus == choice.Row || (choice.Value.Contains(pointer) && HasFocus);
                Manager.DrawShadowedText(font, name, new Vector2(choice.Value.Center.X - nameWidth / 2f - slide, choice.Value.Y),
                    (highlighted ? MainMenuScreen.Yellow : Color.White) * alpha, shadow, ValueScale * uiScale);
                DrawArrow(choice.Left, -MathHelper.PiOver2, SmallArrowScale, pointer, -slide, alpha);
                DrawArrow(choice.Right, MathHelper.PiOver2, SmallArrowScale, pointer, -slide, alpha);
            }

            //The bars: speed, weight and grip
            float labelHeight = font.MeasureString("M").Y * LabelScale * uiScale;
            Vector2 position = statsPosition + new Vector2(slide, 0f);
            for (int i = 0; i < StatNames.Length; i++)
            {
                Manager.DrawShadowedText(font, StatNames[i], position, Color.LightGray * alpha, shadow, LabelScale * uiScale);
                Rectangle bar = new Rectangle((int)position.X, (int)(position.Y + labelHeight), (int)(BarWidth * uiScale), (int)(BarHeight * uiScale));
                Manager.SpriteBatch.Draw(Manager.Pixel, bar, Color.White * (0.2f * alpha));
                bar.Width = (int)(bar.Width * MathHelper.Clamp(bars[i], 0f, 1f));
                Manager.SpriteBatch.Draw(Manager.Pixel, bar, MainMenuScreen.Yellow * alpha);
                position.Y += labelHeight + (BarHeight + RowGap / 2f) * uiScale;
            }

            Color backColor = back.Contains(pointer) && HasFocus ? MainMenuScreen.Yellow : Color.White;
            Manager.DrawShadowedText(font, "Back", new Vector2(backText.X + slide, backText.Y), backColor * alpha, shadow, TextScale * uiScale);

            MainMenuScreen.DrawTitle(Manager, "Garage", Manager.SafeArea.Top - SlideAmount * TitleSlide * uiScale, alpha);

            Manager.SpriteBatch.End();
        }

        /// <summary>An arrow turned to the side; yellow and slightly bigger under the pointer.</summary>
        private void DrawArrow(Rectangle bounds, float rotation, float arrowScale, Vector2 pointer, float slide, float alpha)
        {
            bool highlighted = bounds.Contains(pointer) && HasFocus;
            float scale = arrowScale * Manager.UiScale * (highlighted ? 1.1f : 1f);
            Color color = (highlighted ? MainMenuScreen.Yellow : Color.White) * alpha;
            Vector2 origin = new Vector2(arrowTexture.Width, arrowTexture.Height) / 2f;
            Manager.SpriteBatch.Draw(arrowTexture, bounds.Center.ToVector2() + new Vector2(slide, 0f), null, color, rotation, origin, scale, SpriteEffects.None, 0f);
        }
    }
}
