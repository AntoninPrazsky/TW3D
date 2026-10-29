using System;

namespace TW3D
{
    public static class Program
    {
        /// <summary>
        /// Parametry příkazové řádky (pro vývoj):
        ///   --windowed      spustí hru v okně 1280×720 místo celé obrazovky
        ///   --level název   přeskočí menu a rovnou načte trať (např. --level lock)
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
