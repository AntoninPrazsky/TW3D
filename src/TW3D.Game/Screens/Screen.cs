using System;
using Microsoft.Xna.Framework;
using TW3D.Input;

namespace TW3D.Screens
{
    /// <summary>
    /// One layer of the user interface (logo, menu, level select, the game itself).
    /// Screens are stacked in the <see cref="ScreenManager"/>; the topmost one receives input.
    /// </summary>
    public abstract class Screen
    {
        public ScreenManager Manager { get; internal set; }

        /// <summary>How long the screen takes to appear and to disappear.</summary>
        protected TimeSpan FadeInTime { get; set; } = TimeSpan.FromSeconds(0.5);
        protected TimeSpan FadeOutTime { get; set; } = TimeSpan.FromSeconds(0.5);

        /// <summary>A popup leaves the screens below it visible; any other screen makes them disappear.</summary>
        protected bool IsPopup { get; set; }

        /// <summary>The screen stays visible even when another screen is opened on top of it (e.g. the background).</summary>
        protected bool StaysVisibleWhenCovered { get; set; }

        /// <summary>Once fully visible, the screen covers the whole display, so the screens below it are not drawn.</summary>
        public bool IsOpaque { get; protected set; }

        /// <summary>0 = invisible, 1 = fully visible. Screens multiply their colours by it when drawing.</summary>
        public float Visibility { get; private set; }

        public bool IsClosing { get; private set; }

        /// <summary>Another (non-popup) screen is open on top of this one.</summary>
        public bool IsCovered { get; private set; }

        /// <summary>The screen is the topmost one and the game window is active, so it receives input.</summary>
        public bool HasFocus { get; internal set; }

        /// <summary>The screen is disappearing (it was covered or is closing) rather than appearing.</summary>
        protected bool IsDisappearing => IsClosing || (IsCovered && !StaysVisibleWhenCovered);

        /// <summary>
        /// Offset for sliding animations: 0 when fully visible, rising to 1 when invisible
        /// (eased, so the movement slows down near the final position).
        /// </summary>
        protected float SlideAmount => (1f - Visibility) * (1f - Visibility);

        public virtual void LoadContent() { }

        public virtual void UnloadContent() { }

        /// <summary>Called only for the screen that has the focus.</summary>
        public virtual void HandleInput(InputState input, GameTime gameTime) { }

        /// <summary>Called for every screen in every frame.</summary>
        public virtual void Update(GameTime gameTime) { }

        public virtual void Draw(GameTime gameTime) { }

        /// <summary>Starts the fade out; the screen is removed when it becomes invisible.</summary>
        public void Close()
        {
            IsClosing = true;
        }

        internal void SetCovered(bool covered)
        {
            IsCovered = covered;
        }

        internal bool IsPopupScreen => IsPopup;

        internal void UpdateVisibility(GameTime gameTime)
        {
            bool appearing = !IsDisappearing;
            TimeSpan fadeTime = appearing ? FadeInTime : FadeOutTime;
            float step = fadeTime <= TimeSpan.Zero ? 1f : (float)(gameTime.ElapsedGameTime.TotalSeconds / fadeTime.TotalSeconds);
            Visibility = MathHelper.Clamp(Visibility + (appearing ? step : -step), 0f, 1f);
        }

        /// <summary>Makes the screen fully visible at once, without fading in.</summary>
        protected void ShowImmediately()
        {
            Visibility = 1f;
        }
    }
}
