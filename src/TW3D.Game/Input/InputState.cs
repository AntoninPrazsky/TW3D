using System;
using System.Collections.Generic;
using System.Threading;
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

    /// <summary>A kind of input device, see <see cref="InputState.LastDevice"/>.</summary>
    public enum InputDevice
    {
        Keyboard,
        Mouse,
        Touch,
        GamePad
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
        //How far the mouse has to move (in pixels) to count as being used
        private const int MouseMoveThreshold = 8;

        //Every gamepad button, including the sticks and the triggers pushed in a direction
        private static readonly Buttons[] AllButtons = Enum.GetValues<Buttons>();

        private readonly List<Pointer> pointers = new List<Pointer>();
        private int mousePointerId = FirstMousePointerId;
        //Where the mouse was when it was last used (or first seen), to tell whether it has moved since
        private Point? mouseRest;

        //The back gesture or button of the phone, reported by the Android activity (from another thread)
        private static int systemBackRequested;

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

        /// <summary>The back gesture or button of the phone was used in this frame (see <see cref="RequestSystemBack"/>).</summary>
        public bool SystemBackPressed { get; private set; }

        /// <summary>
        /// The device the player used last: a key pressed, the mouse moved or clicked, the screen touched, or a
        /// gamepad button, stick or trigger pushed. A phone starts with touch, a computer with the keyboard.
        /// </summary>
        public InputDevice LastDevice { get; private set; } =
#if ANDROID
            InputDevice.Touch;
#else
            InputDevice.Keyboard;
#endif

        /// <summary>
        /// Reports the back gesture or button of the system. Since Android 16 the system no longer sends it as a key,
        /// the activity receives it by a callback and passes it on here.
        /// </summary>
        public static void RequestSystemBack()
        {
            Interlocked.Exchange(ref systemBackRequested, 1);
        }

        /// <param name="windowOrigin">Where the window's client area is on the screen.</param>
        public void Update(Point windowOrigin)
        {
            PreviousKeyboard = Keyboard;
            PreviousMouse = Mouse;
            PreviousGamePad = GamePad;

            Keyboard = Microsoft.Xna.Framework.Input.Keyboard.GetState();
            Mouse = Microsoft.Xna.Framework.Input.Mouse.GetState();
            GamePad = Microsoft.Xna.Framework.Input.GamePad.GetState(PlayerIndex.One);

            WheelDelta = Mouse.ScrollWheelValue - PreviousMouse.ScrollWheelValue;
            SystemBackPressed = Interlocked.Exchange(ref systemBackRequested, 0) != 0;

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

            NoteDevice(pointers.Count > 0, windowOrigin);

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

        /// <param name="touched">Something touches the screen.</param>
        /// <param name="windowOrigin">Where the window's client area is on the screen.</param>
        private void NoteDevice(bool touched, Point windowOrigin)
        {
            //The mouse on the screen, not in the window: moving the window under it is no use of the mouse
            Point mouse = Mouse.Position + windowOrigin;
            mouseRest ??= mouse;
            Point moved = mouse - mouseRest.Value;

            if (Keyboard.GetPressedKeyCount() > 0 && Keyboard != PreviousKeyboard)
                LastDevice = InputDevice.Keyboard;
            else if (GamePadUsed())
                LastDevice = InputDevice.GamePad;
            else if (touched)
                LastDevice = InputDevice.Touch;
            else if (Math.Abs(moved.X) + Math.Abs(moved.Y) > MouseMoveThreshold || WheelDelta != 0
                || Mouse.LeftButton == ButtonState.Pressed && PreviousMouse.LeftButton == ButtonState.Released
                || Mouse.RightButton == ButtonState.Pressed && PreviousMouse.RightButton == ButtonState.Released)
                LastDevice = InputDevice.Mouse;

            //The mouse counts from where it was last used, so that slow movements add up; a platform may move it with a
            //finger too, which is no use of the mouse
            if (LastDevice == InputDevice.Mouse || touched)
                mouseRest = mouse;
        }

        private bool GamePadUsed()
        {
            if (!GamePad.IsConnected)
                return false;
            foreach (Buttons button in AllButtons)
                if (ButtonPressed(button))
                    return true;
            return false;
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

        /// <summary>Esc, the gamepad Back button, or the back gesture or button of the phone.</summary>
        public bool BackPressed => KeyPressed(Keys.Escape) || ButtonPressed(Buttons.Back) || SystemBackPressed;

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
                return PointerPressed || ButtonPressed(Buttons.A) || ButtonPressed(Buttons.B) || ButtonPressed(Buttons.Start) || ButtonPressed(Buttons.Back)
                    || SystemBackPressed;
            }
        }
    }
}
