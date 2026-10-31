using System;
using TW3D.Audio;

namespace TW3D.Screens
{
    /// <summary>
    /// The Options menu: the graphics and display settings, the tutorial and the volume of the sound. Selecting an item (or pressing left or right on it)
    /// switches its value; the settings are saved at once.
    /// </summary>
    public sealed class OptionsScreen : ListMenuScreen
    {
        //The volume changes by tenths
        private const float VolumeStep = 0.1f;

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
            AddItem(new MenuItem
            {
                Text = () => "Tutorial: " + (settings.Tutorial ? "On" : "Off"),
                Activate = () => Change(() => settings.Tutorial = !settings.Tutorial),
                IsSwitch = true
            });
            //Enter (or a click) turns the volume up, round to off; left and right turn it down and up
            AddItem(new MenuItem
            {
                Text = () => "Sound: " + (settings.SoundVolume > 0f ? MathF.Round(settings.SoundVolume * 100f) + "%" : "Off"),
                Activate = () => SetVolume(settings.SoundVolume >= 1f ? 0f : settings.SoundVolume + VolumeStep),
                Adjust = step => SetVolume(settings.SoundVolume + step * VolumeStep),
                IsSwitch = true
            });
            AddItem(new MenuItem { Text = () => "Back", Activate = Close });
        }

        private void SetVolume(float volume)
        {
            Change(() => settings.SoundVolume = Math.Clamp(MathF.Round(volume / VolumeStep) * VolumeStep, 0f, 1f));
            GameSounds.Volume = settings.SoundVolume;
        }

        private void Change(Action change)
        {
            change();
            settings.Save();
        }
    }
}
