using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TW3D.Input
{
    /// <summary>
    /// Virtual joystick (bottom left), the brake and axle lock buttons (bottom right) and the pause button
    /// (top right) operated by touch or by the mouse.
    /// </summary>
    public sealed class OnScreenControls
    {
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
        public void Update(InputState input, Viewport viewport, float controlsScale)
        {
            Layout(viewport, controlsScale);

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

        /// <summary>Draws the controls; must be called between SpriteBatch.Begin and End.</summary>
        public void Draw(SpriteBatch batch, float alpha)
        {
            Vector2 socketOrigin = new Vector2(socketTexture.Width, socketTexture.Height) / 2f;
            Vector2 stickOrigin = new Vector2(stickTexture.Width, stickTexture.Height) / 2f;
            batch.Draw(socketTexture, joystickCenter, null, Color.White * alpha, 0f, socketOrigin, scale, SpriteEffects.None, 0f);
            batch.Draw(stickTexture, joystickCenter + stickOffset, null, Color.White * alpha, 0f, stickOrigin, scale, SpriteEffects.None, 0f);

            DrawButton(batch, brakeTexture, brakeCenter, BrakePressed ? BrakePressedRegion : BrakeReleased, alpha);
            DrawButton(batch, lockTexture, lockCenter, LockPressed ? LockPressedRegion : LockReleased, alpha);
            DrawPauseButton(batch, alpha);
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
