using System;

namespace TW3D.Editor
{
    /// <summary>
    /// A break in the road of the middle layer: a road block whose right edge meets the left edge of the road block in
    /// the next column at another height. Found by <see cref="EditedLevel.RoadGaps"/>.
    /// </summary>
    sealed class RoadGap
    {
        /// <param name="a">The road block on the left of the gap.</param>
        /// <param name="b">The road block on the right of it, in the next column.</param>
        /// <param name="leftHeight">The height of the surface at the right edge of <paramref name="a"/>, in shift steps.</param>
        /// <param name="rightHeight">The height of the surface at the left edge of <paramref name="b"/>, in shift steps.</param>
        public RoadGap(PlacedBlock a, PlacedBlock b, int leftHeight, int rightHeight)
        {
            A = a;
            B = b;
            LeftHeight = leftHeight;
            RightHeight = rightHeight;
        }

        /// <summary>The road block on the left of the gap.</summary>
        public PlacedBlock A { get; }

        /// <summary>The road block on the right of the gap.</summary>
        public PlacedBlock B { get; }

        /// <summary>The height of the surface at the right edge of A, in shift steps (they grow downward).</summary>
        public int LeftHeight { get; }

        /// <summary>The height of the surface at the left edge of B, in shift steps.</summary>
        public int RightHeight { get; }

        /// <summary>By how many steps the surfaces differ.</summary>
        public int Steps => Math.Abs(LeftHeight - RightHeight);
    }
}
