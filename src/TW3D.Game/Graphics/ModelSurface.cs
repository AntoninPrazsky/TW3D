using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TW3D.Graphics
{
    /// <summary>
    /// The surface of a model: where rays meet it. Made of the triangles the content pipeline keeps in the Tag of the
    /// models built with its parameter Surface (tools/ContentPipeline/MtlModelProcessor.cs), in the coordinates of the
    /// model. The skid marks of the tyres lie on it (see <see cref="GameObjects.TyreMarks"/>).
    /// </summary>
    sealed class ModelSurface
    {
        /// <summary>Where a ray meets the surface, and the normal of the surface there, turned against the ray.</summary>
        public readonly record struct Hit(Vector3 Point, Vector3 Normal);

        //The corners of the triangles, three a triangle, and the box around each triangle
        private readonly Vector3[] corners;
        private readonly Vector3[] lows, highs;
        //The triangles within reach of the rays cast now
        private readonly List<int> near = new List<int>();

        private ModelSurface(Vector3[] corners)
        {
            this.corners = corners;
            lows = new Vector3[corners.Length / 3];
            highs = new Vector3[corners.Length / 3];
            for (int i = 0; i < lows.Length; i++)
            {
                Vector3 a = corners[3 * i], b = corners[3 * i + 1], c = corners[3 * i + 2];
                lows[i] = Vector3.Min(a, Vector3.Min(b, c));
                highs[i] = Vector3.Max(a, Vector3.Max(b, c));
            }
        }

        /// <summary>The surface of a model, or null when the model was not built with its triangles.</summary>
        public static ModelSurface Of(Model model)
        {
            return model.Tag is Vector3[] corners && corners.Length >= 3 ? new ModelSurface(corners) : null;
        }

        /// <summary>
        /// Where parallel rays first meet the surface: rays from points evenly spaced along a line, from one end of it to
        /// the other, all in one direction and as long. A ray meets a triangle from either side.
        /// </summary>
        /// <param name="direction">The direction of the rays, a unit vector.</param>
        /// <param name="hits">For each ray (as many as there are hits, at least two), where it meets the surface; null where it meets nothing.</param>
        public void Cast(Vector3 from, Vector3 to, Vector3 direction, float length, Hit?[] hits)
        {
            //Only the triangles in the box around all the rays can be met
            Vector3 reach = direction * length;
            Vector3 low = Vector3.Min(Vector3.Min(from, to), Vector3.Min(from + reach, to + reach));
            Vector3 high = Vector3.Max(Vector3.Max(from, to), Vector3.Max(from + reach, to + reach));
            near.Clear();
            for (int i = 0; i < lows.Length; i++)
            {
                if (lows[i].X <= high.X && highs[i].X >= low.X && lows[i].Y <= high.Y && highs[i].Y >= low.Y && lows[i].Z <= high.Z && highs[i].Z >= low.Z)
                    near.Add(i);
            }

            for (int ray = 0; ray < hits.Length; ray++)
            {
                Vector3 origin = Vector3.Lerp(from, to, ray / (float)(hits.Length - 1));
                float nearest = length;
                int met = -1;
                foreach (int i in near)
                {
                    if (Meets(origin, direction, i, out float distance) && distance <= nearest)
                    {
                        nearest = distance;
                        met = i;
                    }
                }
                if (met < 0)
                {
                    hits[ray] = null;
                    continue;
                }
                Vector3 normal = Vector3.Normalize(Vector3.Cross(corners[3 * met + 1] - corners[3 * met], corners[3 * met + 2] - corners[3 * met]));
                if (Vector3.Dot(normal, direction) > 0f)
                    normal = -normal;
                hits[ray] = new Hit(origin + direction * nearest, normal);
            }
        }

        /// <summary>Whether a ray meets a triangle, and how far from its origin (Möller–Trumbore).</summary>
        private bool Meets(Vector3 origin, Vector3 direction, int triangle, out float distance)
        {
            distance = 0f;
            Vector3 a = corners[3 * triangle];
            Vector3 ab = corners[3 * triangle + 1] - a, ac = corners[3 * triangle + 2] - a;
            Vector3 p = Vector3.Cross(direction, ac);
            float determinant = Vector3.Dot(ab, p);
            //The ray runs along the triangle (or the triangle has no area)
            if (Math.Abs(determinant) < 1e-9f)
                return false;
            float inverse = 1f / determinant;
            Vector3 s = origin - a;
            float u = Vector3.Dot(s, p) * inverse;
            if (u < 0f || u > 1f)
                return false;
            Vector3 q = Vector3.Cross(s, ab);
            float v = Vector3.Dot(direction, q) * inverse;
            if (v < 0f || u + v > 1f)
                return false;
            distance = Vector3.Dot(ac, q) * inverse;
            return distance >= 0f;
        }
    }
}
