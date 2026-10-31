using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TW3D.Input
{
    /// <summary>
    /// Virtual joystick (bottom left), the brake and axle lock buttons (bottom right) and the pause button
    /// (top right) operated by touch or by the mouse. They are shown while the player uses touch or the mouse,
    /// and fade out for the keyboard or a gamepad (they still work then, and a click or a touch brings them back).
    /// </summary>
    public sealed class OnScreenControls
    {
        //How long the controls take to fade in or out (seconds)
        private const float FadeTime = 0.3f;

        //Size of the controls at the 1920×1080 reference resolution (the joystick is then 180 px wide)
        private const float ReferenceScale = 1.125f;
        //Distance from the screen edges and between the buttons, in reference pixels
        private const float EdgeMargin = 48f;
        private const float ButtonSpacing = 32f;
        //How far the stick can be pulled from the centre, in texture pixels
        private const float StickTravel = 30f;
        //Size of the pause button and its distance from the screen edges, in reference pixels;
        //it reacts to a touch a little around it too
        private const float PauseButtonSize = 64f;
        private const float PauseMargin = 24f;
        private const float PauseTouchMargin = 24f;

        //Sprite sheet regions: released and pressed state
        private static readonly Rectangle BrakeReleased = new Rectangle(1, 1, 145, 160);
        private static readonly Rectangle BrakePressedRegion = new Rectangle(1, 162, 145, 160);
        private static readonly Rectangle LockReleased = new Rectangle(1, 1, 145, 127);
        private static readonly Rectangle LockPressedRegion = new Rectangle(1, 129, 145, 127);

        private Texture2D socketTexture;
        private Texture2D stickTexture;
        private Texture2D brakeTexture;
        private Texture2D lockTexture;
        private Texture2D pixel;

        private float scale;
        private Vector2 joystickCenter;
        private Vector2 brakeCenter;
        private Vector2 lockCenter;
        private Rectangle pauseBounds;
        private Rectangle pauseTouchBounds;

        private int? joystickPointerId;
        private Vector2 stickOffset;

        //0 hidden .. 1 shown; null until the first update, which shows or hides them at once
        private float? visibility;

        /// <summary>Joystick deflection, both axes in the range -1..1, Y pointing up.</summary>
        public Vector2 Stick { get; private set; }

        public bool BrakePressed { get; private set; }

        public bool LockPressed { get; private set; }

        /// <summary>The pause button was pressed in this frame.</summary>
        public bool PausePressed { get; private set; }

        /// <param name="pixel">1×1 white texture (the pause button is drawn by rectangles).</param>
        public void LoadContent(ContentManager content, Texture2D pixel)
        {
            socketTexture = content.Load<Texture2D>("Common/socket");
            stickTexture = content.Load<Texture2D>("Common/stick");
            brakeTexture = content.Load<Texture2D>("Common/brakepedal");
            lockTexture = content.Load<Texture2D>("Common/lock");
            this.pixel = pixel;
        }

        /// <param name="controlsScale">Scale of the controls (see <see cref="Screens.ScreenManager.ControlsScale"/>).</param>
        /// <param name="elapsed">Real seconds since the last update.</param>
        public void Update(InputState input, Viewport viewport, float controlsScale, float elapsed)
        {
            Layout(viewport, controlsScale);

            float shown = input.LastDevice == InputDevice.Touch || input.LastDevice == InputDevice.Mouse ? 1f : 0f;
            visibility = visibility.HasValue
                ? MathHelper.Clamp(visibility.Value + MathF.Sign(shown - visibility.Value) * elapsed / FadeTime, 0f, 1f)
                : shown;

            UpdateJoystick(input);

            BrakePressed = false;
            LockPressed = false;
            PausePressed = false;
            foreach (Pointer pointer in input.Pointers)
            {
                if (!pointer.IsDown || pointer.Id == joystickPointerId)
                    continue;
                if (ButtonBounds(brakeCenter, BrakeReleased).Contains(pointer.Position))
                    BrakePressed = true;
                if (ButtonBounds(lockCenter, LockReleased).Contains(pointer.Position))
                    LockPressed = true;
                if (pointer.Phase == PointerPhase.Pressed && pauseTouchBounds.Contains(pointer.Position))
                    PausePressed = true;
            }
        }

        /// <summary>
        /// True if the pointer operates one of the controls, so it should not be used for anything else.
        /// </summary>
        public bool Uses(Pointer pointer)
        {
            return pointer.Id == joystickPointerId
                || Vector2.Distance(pointer.Position, joystickCenter) <= JoystickRadius
                || ButtonBounds(brakeCenter, BrakeReleased).Contains(pointer.Position)
                || ButtonBounds(lockCenter, LockReleased).Contains(pointer.Position)
                || pauseTouchBounds.Contains(pointer.Position);
        }

        private float JoystickRadius => socketTexture.Width / 2f * scale;

        /// <summary>Places the controls on the screen; called by Update, or before drawing when there was no input.</summary>
        public void Layout(Viewport viewport, float controlsScale)
        {
            scale = ReferenceScale * controlsScale;
            float margin = EdgeMargin * controlsScale;
            float bottom = viewport.Height - margin;

            //Joystick in the bottom left corner, brake in the bottom right corner and the lock button next to it
            float joystickRadius = socketTexture.Width / 2f * scale;
            joystickCenter = new Vector2(margin + joystickRadius, bottom - joystickRadius);

            Vector2 brakeSize = new Vector2(BrakeReleased.Width, BrakeReleased.Height) * scale;
            brakeCenter = new Vector2(viewport.Width - margin - brakeSize.X / 2f, bottom - brakeSize.Y / 2f);

            Vector2 lockSize = new Vector2(LockReleased.Width, LockReleased.Height) * scale;
            lockCenter = new Vector2(brakeCenter.X - brakeSize.X / 2f - ButtonSpacing * controlsScale - lockSize.X / 2f, bottom - lockSize.Y / 2f);

            //Pause in the top right corner, closer to it than the other controls are to theirs
            int pauseSize = (int)(PauseButtonSize * controlsScale);
            int pauseMargin = (int)(PauseMargin * controlsScale);
            pauseBounds = new Rectangle(viewport.Width - pauseMargin - pauseSize, pauseMargin, pauseSize, pauseSize);
            pauseTouchBounds = pauseBounds;
            pauseTouchBounds.Inflate((int)(PauseTouchMargin * controlsScale), (int)(PauseTouchMargin * controlsScale));
        }

        private void UpdateJoystick(InputState input)
        {
            Pointer? owner = null;

            if (joystickPointerId.HasValue)
            {
                foreach (Pointer pointer in input.Pointers)
                    if (pointer.Id == joystickPointerId.Value && pointer.IsDown)
                        owner = pointer;
            }
            else
            {
                //A pointer that is pressed on the joystick, or slides onto it, grabs it
                foreach (Pointer pointer in input.Pointers)
                {
                    if (pointer.IsDown && Vector2.Distance(pointer.Position, joystickCenter) <= JoystickRadius)
                    {
                        owner = pointer;
                        joystickPointerId = pointer.Id;
                        break;
                    }
                }
            }

            if (owner.HasValue)
            {
                float maxTravel = StickTravel * scale;
                Vector2 offset = owner.Value.Position - joystickCenter;
                if (offset.Length() > maxTravel)
                    offset = Vector2.Normalize(offset) * maxTravel;

                stickOffset = offset;
                Stick = new Vector2(offset.X, -offset.Y) / maxTravel;
            }
            else
            {
                joystickPointerId = null;
                stickOffset = Vector2.Zero;
                Stick = Vector2.Zero;
            }
        }

        private Rectangle ButtonBounds(Vector2 center, Rectangle region)
        {
            int width = (int)(region.Width * scale);
            int height = (int)(region.Height * scale);
            return new Rectangle((int)(center.X - width / 2f), (int)(center.Y - height / 2f), width, height);
        }

        /// <summary>Draws the controls (as far as they are shown); must be called between SpriteBatch.Begin and End.</summary>
        public void Draw(SpriteBatch batch, float alpha)
        {
            alpha *= visibility ?? 0f;
            if (alpha <= 0f)
                return;

            Vector2 socketOrigin = new Vector2(socketTexture.Width, socketTexture.Height) / 2f;
            Vector2 stickOrigin = new Vector2(stickTexture.Width, stickTexture.Height) / 2f;
            batch.Draw(socketTexture, joystickCenter, null, Color.White * alpha, 0f, socketOrigin, scale, SpriteEffects.None, 0f);
            batch.Draw(stickTexture, joystickCenter + stickOffset, null, Color.White * alpha, 0f, stickOrigin, scale, SpriteEffects.None, 0f);

            DrawButton(batch, brakeTexture, brakeCenter, BrakePressed ? BrakePressedRegion : BrakeReleased, alpha);
            DrawButton(batch, lockTexture, lockCenter, LockPressed ? LockPressedRegion : LockReleased, alpha);
            DrawPauseButton(batch, alpha);
        }

        /// <summary>
        /// Draws the on-screen control of a car control as an icon (on the tutorial cards): the joystick moving the way it
        /// is used, or the brake or lock button being pressed. Must be called between SpriteBatch.Begin and End.
        /// </summary>
        /// <param name="centre">The middle of the icon.</param>
        /// <param name="height">The height of the icon in pixels.</param>
        /// <param name="time">Seconds since the icon appeared, for its movement.</param>
        public void DrawIcon(SpriteBatch batch, CarControl control, Vector2 centre, float height, Color colour, float time)
        {
            bool pressed = time % 1.2f >= 0.6f;
            switch (control)
            {
                case CarControl.Throttle:
                case CarControl.Axles:
                    float iconScale = height / socketTexture.Height;
                    float swing = MathF.Sin(time * 3f);
                    Vector2 push = control == CarControl.Throttle
                        ? new Vector2(0.55f + 0.45f * swing, 0f)
                        : new Vector2(0f, swing);
                    batch.Draw(socketTexture, centre, null, colour, 0f, new Vector2(socketTexture.Width, socketTexture.Height) / 2f, iconScale, SpriteEffects.None, 0f);
                    batch.Draw(stickTexture, centre + push * StickTravel * iconScale, null, colour, 0f, new Vector2(stickTexture.Width, stickTexture.Height) / 2f, iconScale, SpriteEffects.None, 0f);
                    break;
                case CarControl.Brake:
                    DrawIconButton(batch, brakeTexture, pressed ? BrakePressedRegion : BrakeReleased, centre, height, colour);
                    break;
                case CarControl.Lock:
                    DrawIconButton(batch, lockTexture, pressed ? LockPressedRegion : LockReleased, centre, height, colour);
                    break;
            }
        }

        /// <summary>The width of an icon of <see cref="DrawIcon"/> of the given height.</summary>
        public float IconWidth(CarControl control, float height)
        {
            return control switch
            {
                CarControl.Brake => height * BrakeReleased.Width / BrakeReleased.Height,
                CarControl.Lock => height * LockReleased.Width / LockReleased.Height,
                _ => height * socketTexture.Width / socketTexture.Height
            };
        }

        private static void DrawIconButton(SpriteBatch batch, Texture2D texture, Rectangle region, Vector2 centre, float height, Color colour)
        {
            batch.Draw(texture, centre, region, colour, 0f, new Vector2(region.Width, region.Height) / 2f, height / region.Height, SpriteEffects.None, 0f);
        }

        /// <summary>Two upright bars, like the buttons half transparent.</summary>
        private void DrawPauseButton(SpriteBatch batch, float alpha)
        {
            int barWidth = (int)(pauseBounds.Width * 0.22f);
            int barHeight = (int)(pauseBounds.Height * 0.7f);
            int gap = (int)(pauseBounds.Width * 0.2f);
            int left = pauseBounds.Center.X - gap / 2 - barWidth;
            int top = pauseBounds.Center.Y - barHeight / 2;
            batch.Draw(pixel, new Rectangle(left, top, barWidth, barHeight), Color.White * (0.5f * alpha));
            batch.Draw(pixel, new Rectangle(left + barWidth + gap, top, barWidth, barHeight), Color.White * (0.5f * alpha));
        }

        private void DrawButton(SpriteBatch batch, Texture2D texture, Vector2 center, Rectangle region, float alpha)
        {
            Vector2 origin = new Vector2(region.Width, region.Height) / 2f;
            batch.Draw(texture, center, region, Color.White * (0.5f * alpha), 0f, origin, scale, SpriteEffects.None, 0f);
        }
    }
}
