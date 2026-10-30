using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using TW3D.Input;

namespace TW3D.Screens
{
    /// <summary>
    /// The pause menu over a level, which stands still while the menu is open.
    /// Esc, P or the back button close it again, like Resume.
    /// </summary>
    public sealed class PauseScreen : ListMenuScreen
    {
        //How much the level behind the menu is darkened
        private const float Dimming = 0.55f;

        /// <param name="restart">Starts the level again.</param>
        /// <param name="levelSelect">Leaves the level for the level select.</param>
        /// <param name="mainMenu">Leaves the level for the main menu.</param>
        public PauseScreen(Action restart, Action levelSelect, Action mainMenu)
            : base("Paused")
        {
            IsPopup = true;
            FadeInTime = TimeSpan.FromSeconds(0.25);
            FadeOutTime = TimeSpan.FromSeconds(0.25);

            AddItem(new MenuItem { Text = () => "Resume", Activate = Close });
            AddItem(new MenuItem { Text = () => "Restart", Activate = () => Leave(restart) });
            AddItem(new MenuItem { Text = () => "Level select", Activate = () => Leave(levelSelect) });
            AddItem(new MenuItem { Text = () => "Main menu", Activate = () => Leave(mainMenu) });
        }

        private void Leave(Action action)
        {
            Close();
            action();
        }

        public override void HandleInput(InputState input, GameTime gameTime)
        {
            if (input.KeyPressed(Keys.P))
                Close();
            else
                base.HandleInput(input, gameTime);
        }

        protected override void DrawBackdrop()
        {
            Manager.SpriteBatch.Draw(Manager.Pixel, Manager.Viewport.Bounds, Color.Black * (Dimming * Visibility));
        }
    }
}
