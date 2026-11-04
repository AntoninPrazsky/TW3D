using System;
using Microsoft.Xna.Framework.Graphics;
using TW3D.Audio;
using TW3D.Graphics;

namespace TW3D.Screens
{
    /// <summary>
    /// The Options menu: the graphics and display settings (the detail of the shadows too, but on a Raspberry Pi), the tutorial and the volume of the sound. Selecting an item (or pressing left or right on it)
    /// switches its value; the settings are saved at once. The Resolution row steps through the heights the 3D scene may
    /// be drawn at on the screen as it is now (see <see cref="RenderResolution"/>), so it follows the window while the menu
    /// is open.
    /// </summary>
    public sealed class OptionsScreen : ListMenuScreen
    {
        //The volume changes by tenths
        private const float VolumeStep = 0.1f;

        private readonly GameSettings settings;
        //The heights of the Resolution row, and the size of the screen they are of
        private int[] heights = Array.Empty<int>();
        private int heightsWidth, heightsHeight;

        /// <param name="displayChanged">Applies a switch between full screen and a window; null = no such option (phones).</param>
        /// <param name="antialiasing">The screen has antialiasing in this run; a switch of it comes with the next start.</param>
        public OptionsScreen(GameSettings settings, Action displayChanged, bool antialiasing)
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

            //Enter (or a click) steps the height of the 3D down, round to native; left and right step it down and up. The
            //menus and the display of a run stay at the size of the screen. A height pinned by --render cannot be changed
            AddItem(new MenuItem
            {
                Text = () => "Resolution: " + ResolutionText(),
                Activate = () => SetResolution(RenderResolution.Next(Heights(), SceneHeight())),
                Adjust = step => SetResolution(RenderResolution.Step(Heights(), SceneHeight(), step)),
                IsEnabled = () => !SceneRenderer.PinnedHeight.HasValue,
                IsSwitch = true
            });
            //The multisample antialiasing of the screen, which the game gets when it starts
            AddItem(new MenuItem
            {
                Text = () => "Antialiasing: " + (settings.Antialiasing ? "On" : "Off")
                    + (settings.Antialiasing != antialiasing ? " (after a restart)" : ""),
                Activate = () => Change(() => settings.Antialiasing = !settings.Antialiasing),
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
            //The size of the shadow map: Enter steps it up, round to low, left and right step it down and up. A Raspberry Pi
            //keeps the low one, its GPU has too little for more
            if (ShadowDetails.CanChoose)
                AddItem(new MenuItem
                {
                    Text = () => "Shadow detail: " + ShadowDetails.Name(settings.ShadowDetail),
                    Activate = () => Change(() => settings.ShadowDetail = ShadowDetails.Next(settings.ShadowDetail)),
                    Adjust = step => Change(() => settings.ShadowDetail = ShadowDetails.Step(settings.ShadowDetail, step)),
                    IsEnabled = () => !settings.Classic && settings.Shadows,
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

        /// <summary>The size of the screen (the window) now.</summary>
        private (int Width, int Height) ScreenSize
        {
            get
            {
                PresentationParameters screen = Manager.GraphicsDevice.PresentationParameters;
                return (screen.BackBufferWidth, screen.BackBufferHeight);
            }
        }

        /// <summary>The heights of the Resolution row on the screen as it is now.</summary>
        private int[] Heights()
        {
            (int width, int height) = ScreenSize;
            if (width != heightsWidth || height != heightsHeight)
            {
                heights = RenderResolution.Heights(width, height);
                heightsWidth = width;
                heightsHeight = height;
            }
            return heights;
        }

        /// <summary>The height the 3D scene is drawn at now.</summary>
        private int SceneHeight()
        {
            (int width, int height) = ScreenSize;
            return SceneRenderer.SceneHeight(width, height, settings.Resolution);
        }

        /// <summary>The size the 3D scene is drawn at, as the row says it: "1280 × 720", "1920 × 1080 (native)".</summary>
        private string ResolutionText()
        {
            (int width, int height) = ScreenSize;
            int sceneHeight = SceneHeight();
            if (sceneHeight <= 0 || sceneHeight >= height)
                return $"{width} \u00D7 {height} (native)";
            return $"{RenderResolution.WidthAt(width, height, sceneHeight)} \u00D7 {sceneHeight}";
        }

        /// <summary>Keeps a height of the row as the part of the height of the screen it is.</summary>
        private void SetResolution(int sceneHeight)
        {
            Change(() => settings.Resolution = RenderResolution.PartOf(ScreenSize.Height, sceneHeight));
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
