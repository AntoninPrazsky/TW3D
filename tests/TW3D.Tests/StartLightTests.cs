using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TW3D.GamePlay;
using TW3D.Graphics;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>
    /// The red start lights of the start gate (sg) come on column by column as the display of the run shows them (#67):
    /// the model has a column of two lamps for each of <see cref="Race.SignalColumns"/>, and the lamps are sorted into
    /// those columns although the content pipeline gives their centres only roughly (Graphics/StartLightColumns).
    /// </summary>
    public class StartLightTests
    {
        /// <summary>The material of the start lights in sg.mtl.</summary>
        private const string SignalMaterial = "signalred";

        [Fact]
        public void TheStartGateHasAColumnOfTwoLampsForEveryStartLight()
        {
            List<(string Name, float Z, float Radius)> lamps = SignalLamps(Path.Combine(Repository.Levels, "mod", "sg.obj"));
            int[] columns = StartLightColumns.Assign(lamps.Select(lamp => lamp.Z).ToList(), lamps.Select(lamp => lamp.Radius).ToList());

            Assert.Equal(Race.SignalColumns, columns.Distinct().Count());
            //SignalRed1a and 1b are the leftmost column seen by the driver, the first to come on
            for (int column = 0; column < Race.SignalColumns; column++)
                Assert.Equal(new[] { $"SignalRed{column + 1}a", $"SignalRed{column + 1}b" },
                    lamps.Where((lamp, index) => columns[index] == column).Select(lamp => lamp.Name).OrderBy(name => name, StringComparer.Ordinal));
        }

        [Fact]
        public void LampsRoughlyOneAboveTheOtherShareAColumn()
        {
            //The centres and radii of the bounding spheres of sg's lamps (SignalRed1a, 1b, 2a … 5b) as the game got them from
            //the content pipeline (2026-11-11): rounded to 0.01 they made seven columns, and the fifth start light never came on
            float[] z = { 3.5438762f, 3.5438762f, 1.735073f, 1.7336291f, -0.066370726f, -0.06637065f, -1.8644505f, -1.857555f, -3.6663704f, -3.6572895f };
            float[] radius = { 0.4185741f, 0.418574f, 0.42493555f, 0.4265484f, 0.42654815f, 0.4265481f, 0.42447016f, 0.4176222f, 0.4265478f, 0.41732022f };
            Assert.Equal(new[] { 0, 0, 1, 1, 2, 2, 3, 3, 4, 4 }, StartLightColumns.Assign(z, radius));
        }

        [Fact]
        public void ColumnsAreNumberedFromTheFurthestAlongZWhateverTheOrderOfTheLamps()
        {
            float[] z = { -1.8f, 1.8f, -1.79f, 0f, 1.81f };
            float[] radius = { 0.4f, 0.4f, 0.4f, 0.4f, 0.4f };
            Assert.Equal(new[] { 2, 0, 2, 1, 0 }, StartLightColumns.Assign(z, radius));
        }

        /// <summary>
        /// The lamps of an OBJ model (its objects with faces of the start lights' material): the name of each, and the
        /// centre along z and the radius of the box round its vertices.
        /// </summary>
        private static List<(string Name, float Z, float Radius)> SignalLamps(string file)
        {
            List<float[]> vertices = new List<float[]>();
            Dictionary<string, List<float[]>> lampVertices = new Dictionary<string, List<float[]>>();
            string name = null;
            string material = null;
            foreach (string line in File.ReadLines(file))
            {
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    continue;
                switch (parts[0])
                {
                    case "o":
                        name = parts[1];
                        break;
                    case "usemtl":
                        material = parts[1];
                        break;
                    case "v":
                        vertices.Add(parts.Skip(1).Take(3).Select(value => float.Parse(value, CultureInfo.InvariantCulture)).ToArray());
                        break;
                    case "f" when material == SignalMaterial:
                        if (!lampVertices.TryGetValue(name, out List<float[]> points))
                            lampVertices[name] = points = new List<float[]>();
                        foreach (string corner in parts.Skip(1))
                        {
                            int index = int.Parse(corner.Split('/')[0], CultureInfo.InvariantCulture);
                            points.Add(vertices[index > 0 ? index - 1 : vertices.Count + index]);
                        }
                        break;
                }
            }
            return lampVertices.Select(lamp =>
            {
                float[] min = Enumerable.Range(0, 3).Select(axis => lamp.Value.Min(point => point[axis])).ToArray();
                float[] max = Enumerable.Range(0, 3).Select(axis => lamp.Value.Max(point => point[axis])).ToArray();
                float radius = MathF.Sqrt(Enumerable.Range(0, 3).Sum(axis => (max[axis] - min[axis]) * (max[axis] - min[axis]))) / 2f;
                return (lamp.Key, (min[2] + max[2]) / 2f, radius);
            }).ToList();
        }
    }
}
