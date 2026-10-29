using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;

namespace TW3D.ScreenSystem
{
    public sealed class VirtualButton
    {
        private Texture2D _sprite;
        private Vector2 _origin;
        private Rectangle _normal;
        private Rectangle _pressed;
        private Vector2 _position;

        private Rectangle _butPos;

        private float scale;

        public bool Pressed;

        public VirtualButton(Texture2D sprite, Vector2 position, Rectangle normal, Rectangle pressed, float screenWidth)
        {
            _sprite = sprite;
            _origin = new Vector2(normal.Width / 2f, normal.Height / 2f);
            _normal = normal;
            _pressed = pressed;
            Pressed = false;
            _position = position;

            scale = screenWidth / 800f;

            _butPos = new Rectangle(
                (int)(_position.X - _normal.Width * scale / 2), 
                (int)(_position.Y - _normal.Height * scale / 2), 
                (int)(_normal.Width * scale),
                (int)(_normal.Height * scale)
                );
        }

        public void Update(TouchLocation touchLocation)
        {
            if (touchLocation.State == TouchLocationState.Pressed ||
                touchLocation.State == TouchLocationState.Moved)
            {
                // FOR CIRCLE BUTTONS
                /*Vector2 delta = touchLocation.Position - _position;
                if (delta.LengthSquared() <= 400f)
                {
                    Pressed = true;
                }
                */
                if (_butPos.Contains(touchLocation.Position)) Pressed = true;
            }
        }

        public void Draw(SpriteBatch batch)
        {
            batch.Draw(_sprite, _position, Pressed ? _pressed : _normal, Color.White * 0.5f, 0f, _origin, scale, SpriteEffects.None, 0f);
        }
    }
}
