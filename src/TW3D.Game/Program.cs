using System;

namespace TW3D
{
    public static class Program
    {
        /// <summary>
        /// Command line arguments (for development):
        ///   --windowed      runs the game in a 1280×720 window instead of full screen
        ///   --level name    skips the menu and loads the given level directly (e.g. --level lock)
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            bool windowed = Array.IndexOf(args, "--windowed") >= 0;

            int levelArg = Array.IndexOf(args, "--level");
            string startLevel = (levelArg >= 0 && levelArg + 1 < args.Length) ? args[levelArg + 1] : null;

            using (var game = new Game1(windowed, startLevel))
                game.Run();
        }
    }
}
