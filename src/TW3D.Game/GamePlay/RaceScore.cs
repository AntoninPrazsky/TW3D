using System;
using System.Globalization;
using TW3D.Shared;

namespace TW3D.GamePlay
{
    /// <summary>
    /// The score of a run of a level, by its time and the par time of the level (the time a good run takes): finishing
    /// in the par time scores 1000 points, faster more and slower less, in proportion (half the time, twice the points).
    /// Within the par time the run gets three stars, within one and a half of it two, otherwise one.
    /// </summary>
    public static class RaceScore
    {
        private const float ParPoints = 1000f;
        private const float TwoStarTime = 1.5f;
        //A level without a par time in its file is meant to take this long per grid cell its finish is from the start
        //(across and up or down), at least this long
        private const float SecondsPerCell = 0.5f;
        private const float MinimumPar = 10f;

        /// <summary>The par time of a level: from its file, or estimated from how far its finish is from the start.</summary>
        public static float Par(LevelData level)
        {
            if (level.Par.HasValue)
                return level.Par.Value;
            BlockPlacement finish = level.FinishBlock;
            //The car starts in the cell [0, 0]
            int cells = finish != null ? Math.Abs(finish.X) + Math.Abs(finish.Y) : 0;
            return Math.Max(MinimumPar, cells * SecondsPerCell);
        }

        public static int Score(float time, float par)
        {
            return (int)MathF.Round(ParPoints * par / Math.Max(time, 0.01f));
        }

        public static int Stars(float time, float par)
        {
            return time <= par ? 3 : time <= par * TwoStarTime ? 2 : 1;
        }

        /// <summary>A time as minutes, seconds and hundredths: 1:05.32.</summary>
        public static string FormatTime(float seconds)
        {
            int hundredths = (int)MathF.Floor(Math.Max(seconds, 0f) * 100f);
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}.{2:00}", hundredths / 6000, hundredths / 100 % 60, hundredths % 100);
        }
    }
}
