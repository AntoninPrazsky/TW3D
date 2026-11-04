using System;
using System.Collections.Generic;

namespace TW3D.Graphics
{
    /// <summary>
    /// The heights the 3D scene may be drawn at on a screen of a given size (the Resolution row of the Options menu), each
    /// at <b>the screen's own aspect ratio</b>, so that the scene scaled up to the screen is never stretched: native first,
    /// then 5/6, 2/3, 1/2 and 1/3 of its height, none under <see cref="MinHeight"/> (on 1920 × 1080: 1600 × 900,
    /// 1280 × 720, 960 × 540 and 640 × 360). Only the scene gets smaller; the menus and the display of a run are drawn
    /// over it at the size of the screen (see <see cref="SceneRenderer"/>). Ported from BS3D (Prazsky.Core.Render).
    /// <para>
    /// <b>Exact where the screen allows it.</b> A height is the nearest whole multiple of the unit of the aspect (16:9's
    /// is 9 pixels, doubled to 18 so that both sides are even), so that its width is a whole number at the exact ratio:
    /// 2560 × 1440 gives 2144 × 1206 rather than a rounded 2133 × 1200. A screen whose unit is too coarse for that (1366 ×
    /// 768, whose ratio does not reduce) takes the even height of the fraction instead, whose width rounded to even is then
    /// off the ratio by less than 0.2 %. Pure arithmetic, tested without a window.
    /// </para>
    /// </summary>
    public static class RenderResolution
    {
        /// <summary>The lowest height offered: under it the scene is a smear behind the sharp menus, whatever the screen.</summary>
        public const int MinHeight = 360;

        //The parts of the screen's height below native, the largest first
        private static readonly float[] Fractions = { 5f / 6f, 2f / 3f, 1f / 2f, 1f / 3f };

        //How far from its fraction a height at the exact aspect may be before the fraction's own height is taken instead
        private const float ExactTolerance = 0.03f;

        /// <summary>
        /// The heights offered on a screen of a size, native first and then downwards, none twice. The width of each is
        /// <see cref="WidthAt"/>. Empty for a screen of no size.
        /// </summary>
        public static int[] Heights(int width, int height)
        {
            if (width <= 0 || height <= 0)
                return Array.Empty<int>();

            int divisor = GreatestCommonDivisor(width, height);
            int unitWidth = width / divisor, unitHeight = height / divisor;
            //Both sides of every height even, so an odd unit is doubled
            if (unitWidth % 2 != 0 || unitHeight % 2 != 0)
                unitHeight *= 2;

            List<int> heights = new List<int> { height };
            foreach (float fraction in Fractions)
            {
                float target = height * fraction;
                int exact = (int)MathF.Round(target / unitHeight) * unitHeight;
                int rung = exact > 0 && MathF.Abs(exact - target) <= target * ExactTolerance
                    ? exact
                    : (int)MathF.Round(target * 0.5f) * 2;
                if (rung >= MinHeight && rung < height && !heights.Contains(rung))
                    heights.Add(rung);
            }
            return heights.ToArray();
        }

        /// <summary>The width a height has at the aspect of the screen, rounded to even.</summary>
        public static int WidthAt(int screenWidth, int screenHeight, int height)
        {
            return (int)MathF.Round(screenWidth * (float)height / screenHeight * 0.5f) * 2;
        }

        /// <summary>
        /// The height of the list nearest to a height, the lower of two equally near (the cheaper frame): what a height
        /// asked for becomes on a screen whose list does not have it. 0 for an empty list.
        /// </summary>
        public static int Nearest(IReadOnlyList<int> heights, int height)
        {
            int best = 0;
            foreach (int rung in heights)
            {
                int distance = Math.Abs(rung - height), bestDistance = Math.Abs(best - height);
                if (best == 0 || distance < bestDistance || distance == bestDistance && rung < best)
                    best = rung;
            }
            return best;
        }

        /// <summary>
        /// The height one click of the row goes to from a height: the first one below it, from the lowest back to native
        /// (the first click from native is the smallest step down). The height itself for an empty list.
        /// </summary>
        public static int Next(IReadOnlyList<int> heights, int height)
        {
            if (heights.Count == 0)
                return height;
            foreach (int rung in heights)
                if (rung < height)
                    return rung;
            return heights[0];
        }

        /// <summary>
        /// The height one step of left or right goes to: the next one up (towards native, <paramref name="steps"/> > 0) or
        /// down, stopping at both ends of the list. A height that is not in the list goes to the nearest one first.
        /// </summary>
        public static int Step(IReadOnlyList<int> heights, int height, int steps)
        {
            if (heights.Count == 0)
                return height;
            int index = IndexOf(heights, Nearest(heights, height));
            //The list goes downwards: up is towards its start
            return heights[Math.Clamp(index - steps, 0, heights.Count - 1)];
        }

        /// <summary>
        /// The height the scene is drawn at on a screen of a size: a height pinned for the run (the --render argument), else
        /// the part of the screen's height chosen in the Options menu, else the default (720 rows on a Raspberry Pi, see
        /// PiGraphics: the largest height of the list not above it), else native; always a height of the list, so the
        /// aspect stays exact. 0 for a screen of no size.
        /// </summary>
        /// <param name="pinned">The height pinned for this run; null for none.</param>
        /// <param name="chosen">The part of the screen's height chosen (1 is native); null when none was chosen.</param>
        /// <param name="defaultHeight">The height drawn at most while none was chosen; null for native.</param>
        public static int SceneHeight(int width, int height, int? pinned, float? chosen, int? defaultHeight)
        {
            int[] heights = Heights(width, height);
            if (heights.Length == 0)
                return 0;
            if (pinned.HasValue)
                return Nearest(heights, pinned.Value);
            if (chosen.HasValue)
                return Nearest(heights, (int)MathF.Round(height * Math.Clamp(chosen.Value, 0f, 1f)));
            if (defaultHeight.HasValue)
            {
                foreach (int rung in heights)
                    if (rung <= defaultHeight.Value)
                        return rung;
                return heights[heights.Length - 1];
            }
            return height;
        }

        /// <summary>The part of the screen's height a height is, as the settings keep it (four decimals).</summary>
        public static float PartOf(int screenHeight, int height)
        {
            return height >= screenHeight ? 1f : MathF.Round(height / (float)screenHeight, 4);
        }

        private static int IndexOf(IReadOnlyList<int> heights, int height)
        {
            for (int i = 0; i < heights.Count; i++)
                if (heights[i] == height)
                    return i;
            return 0;
        }

        private static int GreatestCommonDivisor(int a, int b)
        {
            while (b != 0)
                (a, b) = (b, a % b);
            return a;
        }
    }
}
