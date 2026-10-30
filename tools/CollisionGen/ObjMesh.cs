using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace TW3D.CollisionGen
{
    /// <summary>The polygons of an OBJ model (positions only); vertices at the same position are merged.</summary>
    sealed class ObjMesh
    {
        public readonly List<Vector3D> Vertices = new List<Vector3D>();

        /// <summary>The faces as indices into <see cref="Vertices"/>.</summary>
        public readonly List<int[]> Faces = new List<int[]>();

        public double MinZ => Vertices.Min(v => v.Z);
        public double MaxZ => Vertices.Max(v => v.Z);

        /// <summary>The largest distance of a vertex from the plane z = 0.</summary>
        public double MaxAbsZ => Vertices.Max(v => Math.Abs(v.Z));

        public static ObjMesh Load(string path)
        {
            ObjMesh mesh = new ObjMesh();
            //Index of each OBJ vertex in the merged vertices
            List<int> merged = new List<int>();
            Dictionary<Vector3D, int> byPosition = new Dictionary<Vector3D, int>();

            foreach (string line in File.ReadLines(path))
            {
                string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    continue;

                if (parts[0] == "v")
                {
                    Vector3D v = new Vector3D(Parse(parts[1]), Parse(parts[2]), Parse(parts[3]));
                    //Merged by the position rounded to a millionth, a mesh does not need to share its vertices
                    Vector3D key = new Vector3D(Math.Round(v.X, 6), Math.Round(v.Y, 6), Math.Round(v.Z, 6));
                    if (!byPosition.TryGetValue(key, out int index))
                    {
                        index = mesh.Vertices.Count;
                        mesh.Vertices.Add(v);
                        byPosition.Add(key, index);
                    }
                    merged.Add(index);
                }
                else if (parts[0] == "f")
                {
                    List<int> face = new List<int>();
                    foreach (string corner in parts.Skip(1))
                    {
                        int index = int.Parse(corner.Split('/')[0], CultureInfo.InvariantCulture);
                        //Negative indices count back from the last vertex
                        int vertex = merged[index > 0 ? index - 1 : merged.Count + index];
                        if (face.Count == 0 || face[face.Count - 1] != vertex)
                            face.Add(vertex);
                    }
                    if (face.Count > 1 && face[0] == face[face.Count - 1])
                        face.RemoveAt(face.Count - 1);
                    if (face.Count >= 3)
                        mesh.Faces.Add(face.ToArray());
                }
            }

            if (mesh.Faces.Count == 0)
                throw new InvalidDataException(path + " has no faces.");
            return mesh;
        }

        private static double Parse(string text) => double.Parse(text, CultureInfo.InvariantCulture);
    }

    readonly record struct Vector3D(double X, double Y, double Z);
}
