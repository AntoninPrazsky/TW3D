using System;
using Microsoft.Xna.Framework;

namespace TW3D.Screens
{
    /// <summary>An item of a menu: its text (which may change, e.g. with the value of an option), what it does and where it is on the screen.</summary>
    public sealed class MenuItem
    {
        public Func<string> Text;

        /// <summary>The text as the menus show it: in capitals, as the menus and the titles of the game are written.</summary>
        public string Label => Text().ToUpperInvariant();
        public Action Activate;
        public Func<bool> IsEnabled = () => true;
        /// <summary>The item switches a setting, so left and right activate it too.</summary>
        public bool IsSwitch;
        /// <summary>For a switch with steps (a volume): left and right step it down (-1) and up (+1) instead of activating it.</summary>
        public Action<int> Adjust;
        /// <summary>Where the text is drawn, and the slightly bigger area that reacts to the pointer.</summary>
        public Rectangle TextBounds;
        public Rectangle Bounds;

        /// <summary>Puts the text at its place; the area reacting to the pointer reaches this far around it.</summary>
        public void Place(Rectangle textBounds, int padding)
        {
            TextBounds = textBounds;
            Bounds = textBounds;
            Bounds.Inflate(padding, padding);
        }
    }
}
