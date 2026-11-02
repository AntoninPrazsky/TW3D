using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using TW3D.Input;

namespace TW3D.Screens
{
    /// <summary>
    /// The pause menu over a level, which stands still while the menu is open.
    /// Esc, P or the back button close it again, like Resume. A test drive of the level editor leaves it for the editor,
    /// with no main menu.
    /// </summary>
    public sealed class PauseScreen : ListMenuScreen
    {
        //How much the level behind the menu is darkened
        private const float Dimming = 0.55f;

        /// <param name="restart">Starts the level again.</param>
        /// <param name="leave">Leaves the level for the level select (or the level editor: see <paramref name="leaveText"/>).</param>
        /// <param name="mainMenu">Leaves the level for the main menu; null leaves the item out.</param>
        /// <param name="leaveText">The item of <paramref name="leave"/>.</param>
        public PauseScreen(Action restart, Action leave, Action mainMenu, string leaveText = "Level select")
            : base("Paused")
        {
            IsPopup = true;
            FadeInTime = TimeSpan.FromSeconds(0.25);
            FadeOutTime = TimeSpan.FromSeconds(0.25);

            AddItem(new MenuItem { Text = () => "Resume", Activate = Close });
            AddItem(new MenuItem { Text = () => "Restart", Activate = () => Leave(restart) });
            AddItem(new MenuItem { Text = () => leaveText, Activate = () => Leave(leave) });
            if (mainMenu != null)
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
