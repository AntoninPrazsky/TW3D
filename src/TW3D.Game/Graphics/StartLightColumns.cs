using System;
using System.Collections.Generic;
using System.Linq;

namespace TW3D.Graphics
{
    /// <summary>
    /// The columns of the red start lights of a start gate (sg): the lamps one above the other are a column, lit together
    /// (see <see cref="SceneRenderer.StartLights"/>). Plain logic, so that the tests can check it on the model.
    /// </summary>
    public static class StartLightColumns
    {
        /// <summary>
        /// The column of each lamp: 0 for the column furthest along +z of the model (the leftmost seen by a driver driving
        /// along +x of the physics), then one more for each column along -z. A lamp belongs to the column of the lamp before
        /// it along -z when their centres lie less than its radius apart along z. The centres cannot be compared exactly:
        /// they are the centres of the bounding spheres of the content pipeline, which only approximates them (in sg the two
        /// lamps of a column were up to 0.009 apart and 0.07 off the true centre, so rounding split columns, #67).
        /// </summary>
        /// <param name="z">The z of the centre of each lamp in the model.</param>
        /// <param name="radius">The radius of each lamp (of its bounding sphere).</param>
        public static int[] Assign(IReadOnlyList<float> z, IReadOnlyList<float> radius)
        {
            if (z.Count != radius.Count)
                throw new ArgumentException("A radius is needed for every lamp.", nameof(radius));
            int[] columns = new int[z.Count];
            int column = -1;
            float previous = float.NaN;
            foreach (int lamp in Enumerable.Range(0, z.Count).OrderByDescending(lamp => z[lamp]))
            {
                if (column < 0 || previous - z[lamp] >= radius[lamp])
                    column++;
                columns[lamp] = column;
                previous = z[lamp];
            }
            return columns;
        }
    }
}
