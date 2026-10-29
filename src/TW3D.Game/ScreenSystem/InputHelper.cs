using System.Collections.Generic;
using FarseerPhysics.Graphics.DrawingSystem;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace FarseerPhysics.Graphics.ScreenSystem
{
    /// <summary>
    ///   an enum of all available mouse buttons.
    /// </summary>
    public enum MouseButtons
    {
        LeftButton,
        MiddleButton,
        RightButton,
        ExtraButton1,
        ExtraButton2
    }

    public class InputHelper
    {
        private readonly List<GestureSample> _gestures = new List<GestureSample>();

        private bool _handleVirtualStick;

        private bool _cursorIsVisible;
        private Sprite _cursorSprite;


        private VirtualStick _phoneStick;
        private VirtualButton _phoneA;
        private VirtualButton _phoneB;

        private ScreenManager _manager;
        private Viewport _viewport;

        /// <summary>
        ///   Constructs a new input state.
        /// </summary>
        public InputHelper(ScreenManager manager)
        {
            KeyboardState = new KeyboardState();
            GamePadState = new GamePadState();
            MouseState = new MouseState();
            VirtualState = new GamePadState();
            //ADDED
            TouchCollection = new TouchCollection();
            //--ADDED

            PreviousKeyboardState = new KeyboardState();
            PreviousGamePadState = new GamePadState();
            PreviousMouseState = new MouseState();
            PreviousVirtualState = new GamePadState();
            //ADDED
            PreviousTouchCollection = new TouchCollection();
            //--ADDED


            _manager = manager;

            _cursorIsVisible = true;
            IsCursorMoved = false;

            IsCursorValid = true;

            Cursor = Vector2.Zero;

            _handleVirtualStick = false;
        }

        public GamePadState GamePadState { get; private set; }

        public KeyboardState KeyboardState { get; private set; }

        public MouseState MouseState { get; private set; }

        //ADDED
        public TouchCollection TouchCollection { get; private set; }
        //--ADDED

        public GamePadState VirtualState { get; private set; }

        public GamePadState PreviousGamePadState { get; private set; }

        public KeyboardState PreviousKeyboardState { get; private set; }

        public MouseState PreviousMouseState { get; private set; }

        //ADDED
        public TouchCollection PreviousTouchCollection { get; private set; }
        //--ADDED

        public GamePadState PreviousVirtualState { get; private set; }

        public bool ShowCursor
        {
            get { return _cursorIsVisible && IsCursorValid; }
            set { _cursorIsVisible = value; }
        }

        public bool EnableVirtualStick
        {
            get { return _handleVirtualStick; }
            set { _handleVirtualStick = value; }
        }

        public Vector2 Cursor { get; private set; }

        public bool IsCursorMoved { get; private set; }

        public bool IsCursorValid { get; private set; }

        public void LoadContent()
        {
            _cursorSprite = new Sprite(_manager.Content.Load<Texture2D>("Common/cursor"));

            // virtual stick content
            float _stickPos = _manager.GraphicsDevice.Viewport.Height / 5f;
            _phoneStick = new VirtualStick(_manager.Content.Load<Texture2D>("Common/socket"),
                                           _manager.Content.Load<Texture2D>("Common/stick"), new Vector2(_stickPos, _manager.GraphicsDevice.Viewport.Height - _stickPos), _manager.GraphicsDevice.Viewport.Width);
            
            Texture2D brake = _manager.Content.Load<Texture2D>("Common/brakepedal");
            _phoneA = new VirtualButton(brake, new Vector2(_manager.GraphicsDevice.Viewport.Width - _stickPos, _manager.GraphicsDevice.Viewport.Height - _stickPos),
                new Rectangle(1, 1, 145, 160), new Rectangle(1, 162, 145, 160), _manager.GraphicsDevice.Viewport.Width);
            Texture2D lockH = _manager.Content.Load<Texture2D>("Common/lock");
            
            _phoneB = new VirtualButton(lockH, new Vector2(_manager.GraphicsDevice.Viewport.Width - 3 * _stickPos, _manager.GraphicsDevice.Viewport.Height - _stickPos)
                , new Rectangle(1, 1, 145, 127), new Rectangle(1, 129, 145, 127), _manager.GraphicsDevice.Viewport.Width);

            _viewport = _manager.GraphicsDevice.Viewport;
        }

        /// <summary>
        ///   Reads the latest state of the keyboard and gamepad and mouse/touchpad.
        /// </summary>
        public void Update(GameTime gameTime)
        {
            PreviousKeyboardState = KeyboardState;
            PreviousGamePadState = GamePadState;
            PreviousMouseState = MouseState;
            //ADDED
            PreviousTouchCollection = TouchCollection;
            //--ADDED

            if (_handleVirtualStick)
                PreviousVirtualState = VirtualState;

            KeyboardState = Keyboard.GetState();
            GamePadState = GamePad.GetState(PlayerIndex.One);
            MouseState = Mouse.GetState();
            //ADDED
            TouchCollection = TouchPanel.GetState();
            //--ADDED

            if (_handleVirtualStick)
            {
                //Vstup z gamepadu herní konzole
                //VirtualState = GamePad.GetState(PlayerIndex.One);

                //Vstup z gamepadu, pokud je připojen (jinak z klávesnice)
                //VirtualState = GamePad.GetState(PlayerIndex.One).IsConnected ? GamePad.GetState(PlayerIndex.One) : HandleKeyboard();

                //Vstup pouze z klávesnice
                VirtualState = HandleKeyboard();

                //Vstup pouze z virtuálních ovládacích prvků na displeji
                //VirtualState = HandleVirtualStick();
            }

            _gestures.Clear();
            while (TouchPanel.IsGestureAvailable)
            {
                _gestures.Add(TouchPanel.ReadGesture());
            }

            // Update cursor
            Vector2 oldCursor = Cursor;
            if (GamePadState.IsConnected && GamePadState.ThumbSticks.Left != Vector2.Zero)
            {
                Vector2 temp = GamePadState.ThumbSticks.Left;
                Cursor += temp * new Vector2(300f, -300f) * (float)gameTime.ElapsedGameTime.TotalSeconds;
                Mouse.SetPosition((int)Cursor.X, (int)Cursor.Y);
            }
            else
            {
                //ADDED
                foreach (TouchLocation tl in TouchCollection)
                {
                    if (tl.State == TouchLocationState.Pressed || tl.State == TouchLocationState.Moved || tl.State == TouchLocationState.Released)
                    {
                        Cursor = new Vector2(tl.Position.X, tl.Position.Y);
                        break;
                    }
                }
                //--ADDED

            }

            Cursor = new Vector2(MathHelper.Clamp(Cursor.X, 0f, _viewport.Width), MathHelper.Clamp(Cursor.Y, 0f, _viewport.Height));

            if (IsCursorValid && oldCursor != Cursor)
                IsCursorMoved = true;
            else
                IsCursorMoved = false;

            //ADDED
            foreach (TouchLocation tl in TouchCollection)
            {
                if (tl.State == TouchLocationState.Pressed || tl.State == TouchLocationState.Moved)
                {
                    IsCursorValid = true;
                    break;
                }
                else IsCursorValid = false;
            }
            //--ADDED
        }

        public void Draw()
        {
            if (_cursorIsVisible && IsCursorValid)
            {
                _manager.SpriteBatch.Begin();
                _manager.SpriteBatch.Draw(_cursorSprite.Texture, Cursor, null, Color.White, 0f, _cursorSprite.Origin, 1f, SpriteEffects.None, 0f);
                _manager.SpriteBatch.End();
            }

            if (_handleVirtualStick)
            {
                _manager.SpriteBatch.Begin();
                _phoneA.Draw(_manager.SpriteBatch);
                _phoneB.Draw(_manager.SpriteBatch);
                _phoneStick.Draw(_manager.SpriteBatch);
                _manager.SpriteBatch.End();
            }
        }

        private GamePadState HandleKeyboard()
        {
            Vector2 leftStick = Vector2.Zero;
            List<Buttons> buttons = new List<Buttons>();

            //Left
            if (KeyboardState.IsKeyDown(Keys.Left))
                leftStick.X -= 1f;
            //Down
            if (KeyboardState.IsKeyDown(Keys.Down))
                leftStick.Y -= 1f;
            //Right
            if (KeyboardState.IsKeyDown(Keys.Right))
                leftStick.X += 1f;
            //Up
            if (KeyboardState.IsKeyDown(Keys.Up))
                leftStick.Y += 1f;
            if (KeyboardState.IsKeyDown(Keys.Space))
                buttons.Add(Buttons.A);
            if (KeyboardState.IsKeyDown(Keys.LeftControl))
                buttons.Add(Buttons.B);
            if (leftStick != Vector2.Zero)
                leftStick.Normalize();
            
            return new GamePadState(leftStick, Vector2.Zero, 0f, 0f, buttons.ToArray());
        }

        private GamePadState HandleVirtualStick()
        {
            
            List<Buttons> buttons = new List<Buttons>();
            Vector2 stick = Vector2.Zero;

            _phoneA.Pressed = false;
            _phoneB.Pressed = false;


            foreach (TouchLocation tl in TouchCollection)
            {
                _phoneA.Update(tl);
                _phoneB.Update(tl);
            }
 
            _phoneStick.Update(TouchCollection);
 
            if (_phoneA.Pressed)
            {
                buttons.Add(Buttons.A);
            }
            if (_phoneB.Pressed)
            {
                buttons.Add(Buttons.B);
            }
            stick = _phoneStick.StickPosition;

            return new GamePadState(stick, Vector2.Zero, 0f, 0f, buttons.ToArray());
        }

        /// <summary>
        ///   Helper for checking if a key was newly pressed during this update.
        /// </summary>
        public bool IsNewKeyPress(Keys key)
        {
            return (KeyboardState.IsKeyDown(key) && PreviousKeyboardState.IsKeyUp(key));
        }

        public bool IsNewKeyRelease(Keys key)
        {
            return (PreviousKeyboardState.IsKeyDown(key) && KeyboardState.IsKeyUp(key));
        }

        /// <summary>
        ///   Helper for checking if a button was newly pressed during this update.
        /// </summary>
        public bool IsNewButtonPress(Buttons button)
        {
            return (GamePadState.IsButtonDown(button) && PreviousGamePadState.IsButtonUp(button));
        }

        public bool IsNewButtonRelease(Buttons button)
        {
            return (PreviousGamePadState.IsButtonDown(button) && GamePadState.IsButtonUp(button));
        }

        /// <summary>
        ///   Helper for checking if a mouse button was newly pressed during this update.
        /// </summary>
        public bool IsNewMouseButtonPress(MouseButtons button)
        {
            switch (button)
            {
                case MouseButtons.LeftButton:
                    if (MouseState.LeftButton == ButtonState.Pressed && PreviousMouseState.LeftButton == ButtonState.Released) return true;
                        foreach (TouchLocation tl in TouchCollection)
                        {
                            if (tl.State == TouchLocationState.Pressed)
                            {
                                foreach (TouchLocation tl2 in PreviousTouchCollection)
                                {
                                if (tl2.State == TouchLocationState.Released && tl2.Id == tl.Id)
                                    {
                                        return true;
                                    }
                                    
                                }
                                return true;
                            }
                        }
                        return false;
                case MouseButtons.RightButton:
                    return (MouseState.RightButton == ButtonState.Pressed && PreviousMouseState.RightButton == ButtonState.Released);
                case MouseButtons.MiddleButton:
                    return (MouseState.MiddleButton == ButtonState.Pressed && PreviousMouseState.MiddleButton == ButtonState.Released);
                case MouseButtons.ExtraButton1:
                    return (MouseState.XButton1 == ButtonState.Pressed && PreviousMouseState.XButton1 == ButtonState.Released);
                case MouseButtons.ExtraButton2:
                    return (MouseState.XButton2 == ButtonState.Pressed && PreviousMouseState.XButton2 == ButtonState.Released);
                default:
                    return false;
            }
        }


        /// <summary>
        /// Checks if the requested mouse button is released.
        /// </summary>
        /// <param name="button">The button.</param>
        public bool IsNewMouseButtonRelease(MouseButtons button)
        {
            switch (button)
            {
                case MouseButtons.LeftButton:
                    if (PreviousMouseState.LeftButton == ButtonState.Pressed && MouseState.LeftButton == ButtonState.Released) return true;
                    
                    foreach (TouchLocation tl in PreviousTouchCollection)
                        {
                            if (tl.State == TouchLocationState.Pressed || tl.State == TouchLocationState.Moved)
                            {
                                foreach (TouchLocation tl2 in TouchCollection)
                                {
                                if (tl2.State == TouchLocationState.Released && tl2.Id == tl.Id)
                                    {
                                        return true;
                                    }
                               }
                            }
                        }
                        return false;

                case MouseButtons.RightButton:
                    return (PreviousMouseState.RightButton == ButtonState.Pressed && MouseState.RightButton == ButtonState.Released);
                case MouseButtons.MiddleButton:
                    return (PreviousMouseState.MiddleButton == ButtonState.Pressed && MouseState.MiddleButton == ButtonState.Released);
                case MouseButtons.ExtraButton1:
                    return (PreviousMouseState.XButton1 == ButtonState.Pressed && MouseState.XButton1 == ButtonState.Released);
                case MouseButtons.ExtraButton2:
                    return (PreviousMouseState.XButton2 == ButtonState.Pressed && MouseState.XButton2 == ButtonState.Released);
                default:
                    return false;
            }

        }

        /// <summary>
        ///   Checks for a "menu select" input action.
        /// </summary>
        public bool IsMenuSelect()
        {
            return IsNewKeyPress(Keys.Space) || IsNewKeyPress(Keys.Enter) || IsNewButtonPress(Buttons.A) || IsNewButtonPress(Buttons.Start) || IsNewMouseButtonPress(MouseButtons.LeftButton);
        }

        public bool IsMenuPressed()
        {
            //ADDED
            if (KeyboardState.IsKeyDown(Keys.Space) || KeyboardState.IsKeyDown(Keys.Enter) || GamePadState.IsButtonDown(Buttons.A) || GamePadState.IsButtonDown(Buttons.Start) || MouseState.LeftButton == ButtonState.Pressed) return true;
            foreach (TouchLocation tl in TouchCollection)
            {
                if (tl.State == TouchLocationState.Pressed || tl.State == TouchLocationState.Moved)
                {
                    return true;
                }
            }
            return false;
            //--ADDED
        }

        public bool IsMenuReleased()
        {
            return IsNewKeyRelease(Keys.Space) || IsNewKeyRelease(Keys.Enter) || IsNewButtonRelease(Buttons.A) || IsNewButtonRelease(Buttons.Start) || IsNewMouseButtonRelease(MouseButtons.LeftButton);
        }

        /// <summary>
        ///   Checks for a "menu cancel" input action.
        /// </summary>
        public bool IsMenuCancel()
        {
            return IsNewKeyPress(Keys.Escape) || IsNewButtonPress(Buttons.Back);
        }
    }
}