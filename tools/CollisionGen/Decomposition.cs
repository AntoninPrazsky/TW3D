using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using nkast.Aether.Physics2D;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Common.Decomposition;

namespace TW3D.CollisionGen
{
    /// <summary>Outlines split into convex polygons of the physics engine (at most 8 vertices each).</summary>
    static class Decomposition
    {
        //Decimal places of the coordinates in collision.json (model units)
        private const int Decimals = 4;

        /// <param name="lostArea">Part of the area of the outlines not covered by the convex polygons (0–1).</param>
        public static List<float[]> ConvexPolygons(PathsD outlines, out double lostArea)
        {
            List<float[]> result = new List<float[]>();
            double area = 0, covered = 0;
            foreach (PathD outline in outlines)
            {
                area += Clipper.Area(outline);
                Vertices vertices = new Vertices(outline.Select(p => new Vector2((float)p.x, (float)p.y)));
                //The same decomposition as the game used for the outlines of the collision bitmaps
                foreach (Vertices polygon in Triangulate.ConvexPartition(vertices, TriangulationAlgorithm.Bayazit))
                {
                    float[] coordinates = polygon
                        .SelectMany(v => new[] { (float)Math.Round(v.X, Decimals), (float)Math.Round(v.Y, Decimals) })
                        .ToArray();
                    if (polygon.Count > Settings.MaxPolygonVertices)
                        throw new InvalidOperationException("A convex polygon has more than " + Settings.MaxPolygonVertices + " vertices.");
                    covered += polygon.GetArea();
                    result.Add(coordinates);
                }
            }
            lostArea = area > 0 ? Math.Max(0, 1 - covered / area) : 0;
            return result;
        }
    }
}
