using System;

namespace TW3D.Screens
{
    /// <summary>
    /// The Options menu: the graphics and display settings. Selecting an item (or pressing left or right on it)
    /// switches its value; the settings are saved at once.
    /// </summary>
    public sealed class OptionsScreen : ListMenuScreen
    {
        private readonly GameSettings settings;

        /// <param name="displayChanged">Applies a switch between full screen and a window; null = no such option (phones).</param>
        public OptionsScreen(GameSettings settings, Action displayChanged)
            : base("Options")
        {
            this.settings = settings;
            FadeInTime = TimeSpan.FromSeconds(0.7);
            FadeOutTime = TimeSpan.FromSeconds(0.5);

            if (displayChanged != null)
                AddItem(new MenuItem
                {
                    Text = () => "Display: " + (settings.FullScreen ? "Full screen" : "Window"),
                    Activate = () =>
                    {
                        Change(() => settings.FullScreen = !settings.FullScreen);
                        displayChanged();
                    },
                    IsSwitch = true
                });

            AddItem(new MenuItem
            {
                Text = () => "Look: " + (settings.Classic ? "Classic (2016)" : "New"),
                Activate = () => Change(() => settings.Classic = !settings.Classic),
                IsSwitch = true
            });
            //Shadows and ambient occlusion belong to the new look
            AddItem(new MenuItem
            {
                Text = () => "Shadows: " + (settings.Shadows ? "On" : "Off"),
                Activate = () => Change(() => settings.Shadows = !settings.Shadows),
                IsEnabled = () => !settings.Classic,
                IsSwitch = true
            });
            AddItem(new MenuItem
            {
                Text = () => "Ambient occlusion: " + (settings.AmbientOcclusion ? "On" : "Off"),
                Activate = () => Change(() => settings.AmbientOcclusion = !settings.AmbientOcclusion),
                IsEnabled = () => !settings.Classic,
                IsSwitch = true
            });
            AddItem(new MenuItem
            {
                Text = () => "Frame rate: " + (settings.ShowFrameRate ? "On" : "Off"),
                Activate = () => Change(() => settings.ShowFrameRate = !settings.ShowFrameRate),
                IsSwitch = true
            });
            AddItem(new MenuItem { Text = () => "Back", Activate = Close });
        }

        private void Change(Action change)
        {
            change();
            settings.Save();
        }
    }
}
