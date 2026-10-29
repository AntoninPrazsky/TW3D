using System;
using TW3D.DrawingSystem;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;

namespace TW3D.ScreenSystem
{
    public sealed class VirtualStick
    {
        private Sprite _socketSprite;
        private Sprite _stickSprite;
        private int _picked;
        private Vector2 _position;
        private Vector2 _center;

        private float pickLength = 2025f; //Distance (squared) from the joystick centre within which a touch grabs the joystick
        private float stickLength = 30f; //How far the joystick can be pulled
        private float scale;

        private TouchCollection previousTouchCollection;

        public Vector2 StickPosition;

        public VirtualStick(Texture2D socket, Texture2D stick, Vector2 position, float screenWidth)
        {
            _socketSprite = new Sprite(socket);
            _stickSprite = new Sprite(stick);
            _picked = -1;
            _center = position;
            _position = position;
            StickPosition = Vector2.Zero;
            scale = screenWidth / 800f;
            scale = (float)(Math.Round((double)scale, 2));
            pickLength *= scale;
            stickLength *= scale;
        }

        public void Update(TouchCollection touchCollection)
        {
            /*  if ((touchLocation.State == TouchLocationState.Pressed || touchLocation.State == TouchLocationState.Moved) && _picked < 0)
              {
                  Vector2 delta = touchLocation.Position - _position;
                  if (delta.LengthSquared() <= 2025f)
                      _picked = touchLocation.Id;
              }*/


            /*
            if ((touchLocation.State == TouchLocationState.Pressed || touchLocation.State == TouchLocationState.Moved) && touchLocation.Id == _picked)
            {
                Vector2 delta = touchLocation.Position - _center;
                if (delta != Vector2.Zero)
                {
                    float length = delta.Length();

                    if (length > 25f)
                        delta *= (25f / length);

                    StickPosition = delta / 25f;
                    StickPosition.Y *= -1f;
                    _position = _center + delta;
                }
            }*/

            /*
            if ((touchLocation.State == TouchLocationState.Released && (ptl.State == TouchLocationState.Moved || ptl.State == TouchLocationState.Pressed))
                && touchLocation.Id == _picked
                )
            {
                _picked = -1;
                _position = _center;
                StickPosition = Vector2.Zero;
            }*/

            foreach (TouchLocation tl in touchCollection)
            {
                if ((tl.State == TouchLocationState.Pressed || tl.State == TouchLocationState.Moved) && _picked < 0)
                {
                    Vector2 delta = tl.Position - _position;
                    if (delta.LengthSquared() <= pickLength)
                        _picked = tl.Id;
                }

                if ((tl.State == TouchLocationState.Pressed || tl.State == TouchLocationState.Moved) && tl.Id == _picked)
                {
                    Vector2 delta = tl.Position - _center;
                    if (delta != Vector2.Zero)
                    {
                        float length = delta.Length();

                        if (length > stickLength)
                            delta *= (stickLength / length);

                        StickPosition = delta / stickLength;
                        StickPosition.Y *= -1f;
                        _position = _center + delta;
                    }

                }

                foreach (TouchLocation tlp in previousTouchCollection)
                {
                    if ((tlp.State == TouchLocationState.Moved || tlp.State == TouchLocationState.Pressed))
                    {

                        if ((tl.State == TouchLocationState.Released || tl.State == TouchLocationState.Invalid) && tl.Id == _picked)
                        {
                            _picked = -1;
                            _position = _center;
                            StickPosition = Vector2.Zero;
                        }

                    }
                }

            }

            previousTouchCollection = touchCollection;

        }

        public void Draw(SpriteBatch batch)
        {
            batch.Draw(_socketSprite.Texture, _center, null, Color.White, 0f, _socketSprite.Origin, scale, SpriteEffects.None, 0f);
            batch.Draw(_stickSprite.Texture, _position, null, Color.White, 0f, _stickSprite.Origin, scale, SpriteEffects.None, 0f);
        }
    }
}
