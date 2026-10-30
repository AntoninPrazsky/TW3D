using System;

namespace LevelEditor
{
    /// <summary>
    /// The main class.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// <param name="args">--level &lt;file&gt; opens the level file instead of asking for it.</param>
        [STAThread]
        static void Main(string[] args)
        {
            string level = null;
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "--level")
                    level = args[i + 1];

            using (var game = new Editor(level))
                game.Run();
        }
    }
}
