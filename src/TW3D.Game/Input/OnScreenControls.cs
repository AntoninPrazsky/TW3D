using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TW3D.Input
{
    /// <summary>
    /// Virtual joystick (bottom left) and the brake and axle lock buttons (bottom right)
    /// operated by touch or by the mouse.
    /// </summary>
    public sealed class OnScreenControls
    {
        //Size of the controls at the 1280×720 reference resolution
        private const float ReferenceScale = 1.6f;
        //How far the stick can be pulled from the centre, in texture pixels
        private const float StickTravel = 30f;

        //Sprite sheet regions: released and pressed state
        private static readonly Rectangle BrakeReleased = new Rectangle(1, 1, 145, 160);
        private static readonly Rectangle BrakePressedRegion = new Rectangle(1, 162, 145, 160);
        private static readonly Rectangle LockReleased = new Rectangle(1, 1, 145, 127);
        private static readonly Rectangle LockPressedRegion = new Rectangle(1, 129, 145, 127);

        private Texture2D socketTexture;
        private Texture2D stickTexture;
        private Texture2D brakeTexture;
        private Texture2D lockTexture;

        private float scale;
        private Vector2 joystickCenter;
        private Vector2 brakeCenter;
        private Vector2 lockCenter;

        private int? joystickPointerId;
        private Vector2 stickOffset;

        /// <summary>Joystick deflection, both axes in the range -1..1, Y pointing up.</summary>
        public Vector2 Stick { get; private set; }

        public bool BrakePressed { get; private set; }

        public bool LockPressed { get; private set; }

        public void LoadContent(ContentManager content)
        {
            socketTexture = content.Load<Texture2D>("Common/socket");
            stickTexture = content.Load<Texture2D>("Common/stick");
            brakeTexture = content.Load<Texture2D>("Common/brakepedal");
            lockTexture = content.Load<Texture2D>("Common/lock");
        }

        public void Update(InputState input, Viewport viewport, float uiScale)
        {
            Layout(viewport, uiScale);

            UpdateJoystick(input);

            BrakePressed = false;
            LockPressed = false;
            foreach (Pointer pointer in input.Pointers)
            {
                if (!pointer.IsDown || pointer.Id == joystickPointerId)
                    continue;
                if (ButtonBounds(brakeCenter, BrakeReleased).Contains(pointer.Position))
                    BrakePressed = true;
                if (ButtonBounds(lockCenter, LockReleased).Contains(pointer.Position))
                    LockPressed = true;
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
                || ButtonBounds(lockCenter, LockReleased).Contains(pointer.Position);
        }

        private float JoystickRadius => socketTexture.Width / 2f * scale;

        /// <summary>Places the controls on the screen; called by Update, or before drawing when there was no input.</summary>
        public void Layout(Viewport viewport, float uiScale)
        {
            scale = ReferenceScale * uiScale;

            //The controls keep a distance of a fifth of the screen height from the bottom and side edges
            float margin = viewport.Height / 5f;
            float y = viewport.Height - margin;
            joystickCenter = new Vector2(margin, y);
            brakeCenter = new Vector2(viewport.Width - margin, y);
            lockCenter = new Vector2(viewport.Width - 3f * margin, y);
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
        }

        private void DrawButton(SpriteBatch batch, Texture2D texture, Vector2 center, Rectangle region, float alpha)
        {
            Vector2 origin = new Vector2(region.Width, region.Height) / 2f;
            batch.Draw(texture, center, region, Color.White * (0.5f * alpha), 0f, origin, scale, SpriteEffects.None, 0f);
        }
    }
}
