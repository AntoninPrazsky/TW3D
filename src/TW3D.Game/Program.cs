using System;
using Microsoft.Xna.Framework;
using TW3D.GamePlay;

namespace TW3D
{
    public static class Program
    {
        /// <summary>
        /// Command line arguments (for development):
        ///   --windowed      runs the game in a 1280×720 window instead of full screen
        ///   --size WxH      runs the game in a window of the given size (e.g. --size 2560x1080)
        ///   --level name    skips the menu and loads the given level directly (e.g. --level lock)
        ///   --tutorial      shows the tutorial's lessons of each level again, taught or not, saving nothing
        ///   --tutorial demo shows every lesson in turn, each done by itself after a while (for screenshots)
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Point? windowSize = null;
            if (Array.IndexOf(args, "--windowed") >= 0)
                windowSize = new Point(1280, 720);

            string size = ArgumentValue(args, "--size");
            if (size != null)
            {
                string[] parts = size.Split('x');
                if (parts.Length == 2 && int.TryParse(parts[0], out int width) && int.TryParse(parts[1], out int height))
                    windowSize = new Point(width, height);
            }

            if (Array.IndexOf(args, "--tutorial") >= 0)
                Tutorial.Lever = ArgumentValue(args, "--tutorial") == "demo" ? TutorialLever.Demo : TutorialLever.Replay;

            using (var game = new TripleWheelGame(windowSize, ArgumentValue(args, "--level")))
                game.Run();
        }

        private static string ArgumentValue(string[] args, string name)
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
