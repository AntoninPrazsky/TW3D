using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace TW3D.Input
{
    public enum PointerPhase
    {
        Pressed,
        Held,
        Released
    }

    /// <summary>
    /// A finger on a touch screen, or the mouse while its left button is down.
    /// </summary>
    public readonly struct Pointer
    {
        public Pointer(int id, PointerPhase phase, Vector2 position)
        {
            Id = id;
            Phase = phase;
            Position = position;
        }

        /// <summary>Stays the same while the pointer is down, a new press gets a new id.</summary>
        public int Id { get; }

        public PointerPhase Phase { get; }

        /// <summary>Position in screen pixels.</summary>
        public Vector2 Position { get; }

        public bool IsDown => Phase != PointerPhase.Released;
    }

    /// <summary>
    /// The state of all input devices in the current frame, together with the previous frame
    /// for detecting presses and releases.
    /// Mouse and touch input are unified into pointers. MonoGame DesktopGL generates no touch
    /// events at all, so on the desktop the left mouse button acts as the only pointer.
    /// </summary>
    public sealed class InputState
    {
        //Mouse pointer ids are kept apart from touch ids
        private const int FirstMousePointerId = 1000000;

        private readonly List<Pointer> pointers = new List<Pointer>();
        private int mousePointerId = FirstMousePointerId;

        public KeyboardState Keyboard { get; private set; }
        public KeyboardState PreviousKeyboard { get; private set; }
        public MouseState Mouse { get; private set; }
        public MouseState PreviousMouse { get; private set; }
        public GamePadState GamePad { get; private set; }
        public GamePadState PreviousGamePad { get; private set; }

        /// <summary>All touches and the mouse (while its left button is down or has just been released).</summary>
        public IReadOnlyList<Pointer> Pointers => pointers;

        /// <summary>Position of the first touch, or of the mouse cursor when nothing touches the screen.</summary>
        public Vector2 PointerPosition { get; private set; }

        /// <summary>The first pointer went down in this frame.</summary>
        public bool PointerPressed { get; private set; }

        /// <summary>The first pointer was released in this frame.</summary>
        public bool PointerReleased { get; private set; }

        /// <summary>Mouse wheel movement since the last frame (120 per notch, positive = away from the user).</summary>
        public int WheelDelta { get; private set; }

        public void Update()
        {
            PreviousKeyboard = Keyboard;
            PreviousMouse = Mouse;
            PreviousGamePad = GamePad;

            Keyboard = Microsoft.Xna.Framework.Input.Keyboard.GetState();
            Mouse = Microsoft.Xna.Framework.Input.Mouse.GetState();
            GamePad = Microsoft.Xna.Framework.Input.GamePad.GetState(PlayerIndex.One);

            WheelDelta = Mouse.ScrollWheelValue - PreviousMouse.ScrollWheelValue;

            pointers.Clear();
            foreach (TouchLocation touch in TouchPanel.GetState())
            {
                switch (touch.State)
                {
                    case TouchLocationState.Pressed: pointers.Add(new Pointer(touch.Id, PointerPhase.Pressed, touch.Position)); break;
                    case TouchLocationState.Moved: pointers.Add(new Pointer(touch.Id, PointerPhase.Held, touch.Position)); break;
                    case TouchLocationState.Released: pointers.Add(new Pointer(touch.Id, PointerPhase.Released, touch.Position)); break;
                }
            }

            if (pointers.Count == 0)
                AddMousePointer();

            if (pointers.Count > 0)
            {
                Pointer first = pointers[0];
                PointerPosition = first.Position;
                PointerPressed = first.Phase == PointerPhase.Pressed;
                PointerReleased = first.Phase == PointerPhase.Released;
            }
            else
            {
                PointerPosition = new Vector2(Mouse.X, Mouse.Y);
                PointerPressed = false;
                PointerReleased = false;
            }
        }

        private void AddMousePointer()
        {
            bool down = Mouse.LeftButton == ButtonState.Pressed;
            bool wasDown = PreviousMouse.LeftButton == ButtonState.Pressed;
            Vector2 position = new Vector2(Mouse.X, Mouse.Y);

            if (down && !wasDown)
            {
                mousePointerId++;
                pointers.Add(new Pointer(mousePointerId, PointerPhase.Pressed, position));
            }
            else if (down)
                pointers.Add(new Pointer(mousePointerId, PointerPhase.Held, position));
            else if (wasDown)
                pointers.Add(new Pointer(mousePointerId, PointerPhase.Released, position));
        }

        public bool IsKeyDown(Keys key)
        {
            return Keyboard.IsKeyDown(key);
        }

        public bool KeyPressed(Keys key)
        {
            return Keyboard.IsKeyDown(key) && PreviousKeyboard.IsKeyUp(key);
        }

        public bool ButtonPressed(Buttons button)
        {
            return GamePad.IsButtonDown(button) && PreviousGamePad.IsButtonUp(button);
        }

        /// <summary>Esc or the gamepad Back button.</summary>
        public bool BackPressed => KeyPressed(Keys.Escape) || ButtonPressed(Buttons.Back);

        /// <summary>Enter, Space or the gamepad A / Start button.</summary>
        public bool ConfirmPressed => KeyPressed(Keys.Enter) || KeyPressed(Keys.Space) || ButtonPressed(Buttons.A) || ButtonPressed(Buttons.Start);

        public bool LeftPressed => KeyPressed(Keys.Left) || ButtonPressed(Buttons.DPadLeft);
        public bool RightPressed => KeyPressed(Keys.Right) || ButtonPressed(Buttons.DPadRight);
        public bool UpPressed => KeyPressed(Keys.Up) || ButtonPressed(Buttons.DPadUp);
        public bool DownPressed => KeyPressed(Keys.Down) || ButtonPressed(Buttons.DPadDown);

        /// <summary>Any key, gamepad button or pointer was pressed in this frame.</summary>
        public bool AnyPressed
        {
            get
            {
                foreach (Keys key in Keyboard.GetPressedKeys())
                    if (PreviousKeyboard.IsKeyUp(key))
                        return true;
                return PointerPressed || ButtonPressed(Buttons.A) || ButtonPressed(Buttons.B) || ButtonPressed(Buttons.Start) || ButtonPressed(Buttons.Back);
            }
        }
    }
}
