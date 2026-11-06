using System.Collections.Generic;

namespace TW3D.Shared
{
    /// <summary>A placed block as <see cref="RoadLooks"/> needs it: where it is, whether it is turned and its own style.</summary>
    public readonly struct RoadBlock
    {
        public RoadBlock(string name, int x, int y, int shift, bool turned, string style)
        {
            Name = name;
            X = x;
            Y = y;
            Shift = shift;
            Turned = turned;
            Style = style;
        }

        public string Name { get; }
        public int X { get; }
        public int Y { get; }
        public int Shift { get; }
        public bool Turned { get; }

        /// <summary>The style of the block itself (see <see cref="BlockPlacement.Style"/>); null for the level's.</summary>
        public string Style { get; }

        public static RoadBlock Of(BlockPlacement placement) =>
            new RoadBlock(placement.Block, placement.X, placement.Y, placement.Shift, placement.Turned, placement.Style);
    }

    /// <summary>
    /// The looks the blocks of the road are drawn in (#58): the style of a block (its own or the level's, the classic one
    /// where it has no look in it), and where the road passes from one style into another, the look of the transition
    /// between them on the block of the styled side, so that the road needs no blocks of its own for it: a dirt road
    /// sinks into the classic slab where it meets a start block, a tunnel or a classic road. Which blocks the road joins
    /// is found as the road mode of the level editor finds it: a road block (one with a profile; of two in one cell the
    /// one placed last) and the road block of the next column whose edge is at the height of its edge. A block with a
    /// transition of its own (a style "first-second") keeps it, and so does a block whose road meets another style on
    /// both sides (it has no look for that). Where two styles other than the classic one meet, the block on the right
    /// takes the transition. Pure logic, shared by the game, the level editor and the tests.
    /// </summary>
    public static class RoadLooks
    {
        private const int StepsPerCell = 9;

        /// <summary>The style each block of the middle layer is drawn in (in the order given): a style or a transition of the catalog, or <see cref="Catalog.ClassicStyle"/>.</summary>
        /// <param name="levelStyle">The style of the level; null for the classic one.</param>
        public static string[] Of(IReadOnlyList<RoadBlock> blocks, string levelStyle, Catalog catalog)
        {
            int count = blocks.Count;
            string[] looks = new string[count];
            BlockType[] types = new BlockType[count];
            //The style each block has before the transitions, and the styles at its left and right edge as placed
            string[] left = new string[count], right = new string[count];
            for (int i = 0; i < count; i++)
            {
                types[i] = catalog.FindBlock(blocks[i].Name);
                string style = blocks[i].Style ?? levelStyle;
                if (style == null || types[i] == null || !types[i].HasLook(style))
                    style = Catalog.ClassicStyle;
                looks[i] = style;
                string[] edges = EdgeStyles(style, blocks[i].Turned);
                left[i] = edges[0];
                right[i] = edges[1];
            }

            //The road blocks by their column and the height of their left edge (of two in one cell, the one placed last)
            Dictionary<(int X, int Y), int> lastInCell = new Dictionary<(int, int), int>();
            for (int i = 0; i < count; i++)
                if (types[i]?.PlacedProfile(false) != null)
                    lastInCell[(blocks[i].X, blocks[i].Y)] = i;
            Dictionary<(int X, int Height), int> byLeftEdge = new Dictionary<(int, int), int>();
            foreach (int i in lastInCell.Values)
                byLeftEdge[(blocks[i].X, Edges(blocks[i], types[i])[0])] = i;

            //The neighbours each road block joins, left and right (-1 for none)
            int[] leftOf = new int[count], rightOf = new int[count];
            for (int i = 0; i < count; i++)
                leftOf[i] = rightOf[i] = -1;
            foreach (int i in lastInCell.Values)
            {
                if (byLeftEdge.TryGetValue((blocks[i].X + 1, Edges(blocks[i], types[i])[1]), out int next))
                {
                    rightOf[i] = next;
                    leftOf[next] = i;
                }
            }

            foreach (int i in lastInCell.Values)
            {
                string style = looks[i];
                if (style == Catalog.ClassicStyle || style.Contains('-'))
                    continue;
                string before = leftOf[i] >= 0 ? right[leftOf[i]] : style;
                string after = rightOf[i] >= 0 ? left[rightOf[i]] : style;
                //Where two styles that are not the classic one meet, the block on the right of them takes the transition
                if (after != style && after != Catalog.ClassicStyle)
                    after = style;
                bool fromLeft = before != style, toRight = after != style;
                if (fromLeft == toRight)
                    continue;
                //The transition as the road goes, left to right, and as the model names it (turned, the other way round)
                string first = fromLeft ? before : style, second = fromLeft ? style : after;
                string transition = blocks[i].Turned ? second + "-" + first : first + "-" + second;
                if (types[i].HasLook(transition))
                    looks[i] = transition;
            }
            return looks;
        }

        /// <summary>The heights of the road at the left and the right edge of a road block as placed (shift steps, growing downward).</summary>
        private static int[] Edges(RoadBlock block, BlockType type)
        {
            int[] profile = type.PlacedProfile(block.Turned);
            int bottom = StepsPerCell * block.Y + block.Shift;
            return new[] { bottom + profile[0], bottom + profile[1] };
        }

        /// <summary>The styles at the left and the right edge of a block drawn in a style or a transition, as placed.</summary>
        private static string[] EdgeStyles(string style, bool turned)
        {
            int dash = style.IndexOf('-');
            if (dash < 0)
                return new[] { style, style };
            string first = style.Substring(0, dash), second = style.Substring(dash + 1);
            return turned ? new[] { second, first } : new[] { first, second };
        }
    }
}
