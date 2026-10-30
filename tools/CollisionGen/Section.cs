using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;

namespace TW3D.CollisionGen
{
    /// <summary>
    /// The part of a model within a band across the physics plane (inner ≤ |z| ≤ outer), seen along the z axis:
    /// everything in the band that a body moving in the plane (x, y) within the same band can touch.
    /// </summary>
    static class Section
    {
        //Decimal places of the coordinates in the polygon clipping (model units)
        private const int Precision = 6;
        //Vertices closer than this to the line through their neighbours are left out (model units)
        private const double SimplifyDistance = 0.002;
        //Smaller polygons are left out (square model units)
        private const double MinArea = 0.001;

        /// <param name="inner">Inner edge of the band (0: the band is one slab -outer ≤ z ≤ outer).</param>
        /// <param name="outer">Outer edge of the band (infinity: the whole model).</param>
        /// <param name="openChains">Number of open cuts through the mesh at the edges of the band (a mesh with holes).</param>
        public static PathsD Band(ObjMesh mesh, double inner, double outer, out int openChains)
        {
            (double From, double To)[] slabs = inner <= 0
                ? new[] { (-outer, outer) }
                : new[] { (inner, outer), (-outer, -inner) };

            //The faces within the band, flattened onto the plane (x, y), and the cuts through the model at the edges
            //of the band: a straight extrusion along z has no face within the band that would not be flattened to a line
            PathsD faces = new PathsD();
            PathsD cuts = new PathsD();
            openChains = 0;
            foreach ((double from, double to) in slabs)
            {
                foreach (int[] face in mesh.Faces)
                {
                    List<Vector3D> polygon = face.Select(i => mesh.Vertices[i]).ToList();
                    polygon = Clip(polygon, from, true);
                    polygon = Clip(polygon, to, false);
                    if (polygon.Count < 3)
                        continue;
                    PathD flat = new PathD(polygon.Select(v => new PointD(v.X, v.Y)));
                    double area = Clipper.Area(flat);
                    if (area == 0)
                        continue;
                    if (area < 0)
                        flat.Reverse();
                    faces.Add(flat);
                }
                foreach (double z in new[] { from, to })
                {
                    //Holes in a cut are found by the even-odd rule within each cut, the cuts are then joined
                    cuts.AddRange(Union(Cut(mesh, z, out int open), FillRule.EvenOdd));
                    openChains += open;
                }
            }

            PathsD region = Clipper.Union(faces, cuts, FillRule.NonZero, Precision);
            return Clean(region);
        }

        /// <summary>
        /// Fills the holes (nothing moving in the plane can get into them), leaves out tiny details
        /// and splits the outlines touching themselves.
        /// </summary>
        private static PathsD Clean(PathsD region)
        {
            PathsD outlines = new PathsD(region.Where(Clipper.IsPositive));
            outlines = Clipper.SimplifyPaths(outlines, SimplifyDistance, true);
            outlines = Union(outlines, FillRule.NonZero);
            outlines = new PathsD(outlines.Where(path => Clipper.IsPositive(path)));

            PathsD result = new PathsD();
            foreach (PathD path in outlines)
                foreach (PathD part in SplitAtTouches(path))
                    if (Clipper.Area(part) >= MinArea)
                        result.Add(part);
            return result;
        }

        private static PathsD Union(PathsD paths, FillRule fillRule)
            => Clipper.Union(paths, new PathsD(), fillRule, Precision);

        /// <summary>An outline passing twice through the same point, split into simple outlines.</summary>
        private static IEnumerable<PathD> SplitAtTouches(PathD path)
        {
            List<PointD> stack = new List<PointD>();
            Dictionary<(double, double), int> seen = new Dictionary<(double, double), int>();
            foreach (PointD point in path)
            {
                (double, double) key = (Math.Round(point.x, Precision), Math.Round(point.y, Precision));
                if (seen.TryGetValue(key, out int start))
                {
                    //The loop since the first visit of the point is a separate outline
                    PathD loop = new PathD(stack.Skip(start));
                    foreach (PointD p in loop)
                        seen.Remove((Math.Round(p.x, Precision), Math.Round(p.y, Precision)));
                    stack.RemoveRange(start, stack.Count - start);
                    if (loop.Count >= 3)
                        yield return loop;
                }
                seen[key] = stack.Count;
                stack.Add(point);
            }
            if (stack.Count >= 3)
                yield return new PathD(stack);
        }

        /// <summary>The part of a polygon on one side of the plane z = <paramref name="z"/>.</summary>
        private static List<Vector3D> Clip(List<Vector3D> polygon, double z, bool keepAbove)
        {
            List<Vector3D> result = new List<Vector3D>();
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector3D a = polygon[i];
                Vector3D b = polygon[(i + 1) % polygon.Count];
                bool aIn = keepAbove ? a.Z >= z : a.Z <= z;
                bool bIn = keepAbove ? b.Z >= z : b.Z <= z;
                if (aIn)
                    result.Add(a);
                if (aIn != bIn)
                    result.Add(Lerp(a, b, (z - a.Z) / (b.Z - a.Z)));
            }
            return result;
        }

        private static Vector3D Lerp(Vector3D a, Vector3D b, double t)
            => new Vector3D(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);

        /// <summary>The closed outlines where the plane z = <paramref name="z"/> cuts the model.</summary>
        /// <param name="openChains">Number of cuts that do not close (the mesh has a hole there).</param>
        private static PathsD Cut(ObjMesh mesh, double z, out int openChains)
        {
            openChains = 0;
            PathsD loops = new PathsD();
            if (double.IsInfinity(z) || z <= mesh.MinZ || z >= mesh.MaxZ)
                return loops;
            //A plane through a vertex would cut the mesh ambiguously
            while (mesh.Vertices.Any(v => Math.Abs(v.Z - z) < 1e-9))
                z += 1e-7;

            //The cut is made of segments across the faces, between the points where the plane crosses their edges;
            //an edge (the pair of its vertices) is shared by the neighbouring faces, which links the segments
            List<((int, int) From, (int, int) To)> segments = new List<((int, int), (int, int))>();
            foreach (int[] face in mesh.Faces)
            {
                List<(int, int)> crossed = new List<(int, int)>();
                for (int k = 0; k < face.Length; k++)
                {
                    int i = face[k], j = face[(k + 1) % face.Length];
                    if ((mesh.Vertices[i].Z > z) != (mesh.Vertices[j].Z > z))
                        crossed.Add((Math.Min(i, j), Math.Max(i, j)));
                }
                if (crossed.Count < 2)
                    continue;
                //A concave face can be crossed more than twice: the crossings in order along the cut, in pairs
                PointD first = Crossing(mesh, crossed[0], z), second = Crossing(mesh, crossed[1], z);
                PointD direction = new PointD(second.x - first.x, second.y - first.y);
                crossed = crossed.OrderBy(edge =>
                {
                    PointD p = Crossing(mesh, edge, z);
                    return (p.x - first.x) * direction.x + (p.y - first.y) * direction.y;
                }).ToList();
                for (int k = 0; k + 1 < crossed.Count; k += 2)
                    segments.Add((crossed[k], crossed[k + 1]));
            }

            Dictionary<(int, int), List<int>> segmentsAt = new Dictionary<(int, int), List<int>>();
            for (int s = 0; s < segments.Count; s++)
                foreach ((int, int) end in new[] { segments[s].From, segments[s].To })
                {
                    if (!segmentsAt.TryGetValue(end, out List<int> list))
                        segmentsAt.Add(end, list = new List<int>());
                    list.Add(s);
                }

            //The segments joined into loops, each segment used once; where more than two faces share an edge
            //(two parts of the model touching along it), the loops may cross there, which the even-odd rule allows
            bool[] used = new bool[segments.Count];
            for (int s = 0; s < segments.Count; s++)
            {
                if (used[s])
                    continue;
                used[s] = true;
                (int, int) start = segments[s].From, current = segments[s].To;
                List<(int, int)> loop = new List<(int, int)> { start };
                while (current != start)
                {
                    loop.Add(current);
                    int next = segmentsAt[current].FirstOrDefault(t => !used[t], -1);
                    if (next < 0)
                        break;
                    used[next] = true;
                    current = segments[next].From == current ? segments[next].To : segments[next].From;
                }
                if (current != start || loop.Count < 3)
                    openChains++;
                else
                    loops.Add(new PathD(loop.Select(edge => Crossing(mesh, edge, z))));
            }
            return loops;
        }

        private static PointD Crossing(ObjMesh mesh, (int, int) edge, double z)
        {
            Vector3D a = mesh.Vertices[edge.Item1], b = mesh.Vertices[edge.Item2];
            Vector3D p = Lerp(a, b, (z - a.Z) / (b.Z - a.Z));
            return new PointD(p.X, p.Y);
        }
    }
}
