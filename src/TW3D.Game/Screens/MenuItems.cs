using System.Collections;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TW3D.Input;

namespace TW3D.Screens
{
    /// <summary>
    /// The items of a menu and the one of them that has the focus: the item under the mouse when it moves, or the one
    /// the arrow keys or a gamepad moved the focus to. An item is activated by a click or a touch on it, or by Enter
    /// (the gamepad A) on the focused one. The screen places the items (see <see cref="MenuItem.Place"/>).
    /// </summary>
    public sealed class MenuItems : IReadOnlyList<MenuItem>
    {
        /// <summary>Font scale of the items at the reference resolution.</summary>
        public const float TextScale = 0.8f;
        //How visible an item that cannot be chosen is
        private const float DisabledAlpha = 0.45f;

        private readonly List<MenuItem> items = new List<MenuItem>();

        public int Count => items.Count;

        public MenuItem this[int index] => items[index];

        /// <summary>Index of the item with the focus; -1 for none.</summary>
        public int Focused { get; private set; } = -1;

        public void Add(MenuItem item)
        {
            items.Add(item);
        }

        /// <summary>Index of the item that can be chosen at the position; -1 for none.</summary>
        public int ItemAt(Vector2 position)
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i].IsEnabled() && items[i].Bounds.Contains(position))
                    return i;
            return -1;
        }

        /// <summary>The mouse focuses the item under it when it moves (a mouse at rest does not take the focus from the keys).</summary>
        public void FollowMouse(InputState input)
        {
            if (input.Mouse.Position != input.PreviousMouse.Position)
                Focused = ItemAt(input.PointerPosition);
        }

        /// <summary>Moves the focus to the next (1) or the previous (-1) item that can be chosen, around the ends.</summary>
        public void MoveFocus(int direction)
        {
            int index = Focused < 0 ? (direction > 0 ? -1 : items.Count) : Focused;
            for (int step = 0; step < items.Count; step++)
            {
                index = (index + direction + items.Count) % items.Count;
                if (items[index].IsEnabled())
                {
                    Focused = index;
                    return;
                }
            }
        }

        /// <summary>
        /// Activates the item under a pointer pressed in this frame, or the focused item on Enter (A). True if an item
        /// was activated.
        /// </summary>
        /// <param name="switchByLeftRight">Left and right activate the focused item too, if it is a switch (not when they move the focus).</param>
        public bool Activate(InputState input, bool switchByLeftRight)
        {
            int pressed = input.PointerPressed ? ItemAt(input.PointerPosition) : -1;
            if (pressed >= 0)
            {
                items[pressed].Activate();
                return true;
            }

            if (Focused < 0 || !items[Focused].IsEnabled())
                return false;
            if (input.ConfirmPressed || (switchByLeftRight && items[Focused].IsSwitch && (input.LeftPressed || input.RightPressed)))
            {
                items[Focused].Activate();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Draws the items at their places, moved sideways as they slide in or out, the focused one in yellow if the
        /// menu has the focus. Called between SpriteBatch.Begin and End.
        /// </summary>
        /// <param name="scale">Font scale of the items on the screen.</param>
        public void Draw(ScreenManager manager, float scale, float slide, float alpha, bool hasFocus)
        {
            SpriteFont font = manager.Fonts.Menu;
            for (int i = 0; i < items.Count; i++)
            {
                MenuItem item = items[i];
                Color color = !item.IsEnabled() ? Color.White * DisabledAlpha : (i == Focused && hasFocus ? MainMenuScreen.Yellow : Color.White);
                Vector2 position = new Vector2(item.TextBounds.X + slide, item.TextBounds.Y);
                manager.DrawShadowedText(font, item.Text(), position, color * alpha, Color.DarkSlateGray * (alpha * alpha), scale);
            }
        }

        public IEnumerator<MenuItem> GetEnumerator() => items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
