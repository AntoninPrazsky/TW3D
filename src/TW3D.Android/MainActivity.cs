using System;
using System.Linq;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Xna.Framework;

namespace TW3D
{
    /// <summary>The Android app: one activity showing the game, in landscape over the whole screen.</summary>
    [Activity(
        Label = "@string/app_name",
        MainLauncher = true,
        Icon = "@drawable/icon",
        Theme = "@android:style/Theme.NoTitleBar.Fullscreen",
        AlwaysRetainTaskState = true,
        LaunchMode = LaunchMode.SingleInstance,
        ScreenOrientation = ScreenOrientation.SensorLandscape,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden
            | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.UiMode)]
    public class MainActivity : AndroidGameActivity
    {
        private Game1 game;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            WindowManagerLayoutParams attributes = Window.Attributes;
            //Draw also next to the camera cutout of the screen
            if (OperatingSystem.IsAndroidVersionAtLeast(28))
                attributes.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
            //The highest refresh rate of the display (like on desktop, where the game follows the monitor);
            //otherwise Android runs the game at 60 Hz
            if (OperatingSystem.IsAndroidVersionAtLeast(30) && Display != null)
            {
                Display.Mode current = Display.GetMode();
                Display.Mode fastest = Display.GetSupportedModes()
                    .Where(mode => mode.PhysicalWidth == current.PhysicalWidth && mode.PhysicalHeight == current.PhysicalHeight)
                    .OrderByDescending(mode => mode.RefreshRate)
                    .First();
                attributes.PreferredDisplayModeId = fastest.ModeId;
            }
            Window.Attributes = attributes;

            game = new Game1();
            SetContentView((View)game.Services.GetService(typeof(View)));
            HideSystemBars();
            game.Run();
        }

        public override void OnWindowFocusChanged(bool hasFocus)
        {
            base.OnWindowFocusChanged(hasFocus);
            //Android shows the bars again e.g. after returning to the app
            if (hasFocus)
                HideSystemBars();
        }

        /// <summary>Hides the status and navigation bars; a swipe from the edge shows them for a while.</summary>
        private void HideSystemBars()
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(30))
                return;

            //Since Android 15 apps are drawn under the bars anyway
            if (!OperatingSystem.IsAndroidVersionAtLeast(35))
                Window.SetDecorFitsSystemWindows(false);
            IWindowInsetsController controller = Window.InsetsController;
            if (controller == null)
                return;
            controller.Hide(WindowInsets.Type.SystemBars());
            controller.SystemBarsBehavior = (int)WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
        }
    }
}
