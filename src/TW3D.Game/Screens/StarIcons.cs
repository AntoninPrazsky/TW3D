using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TW3D.Screens
{
    /// <summary>The stars of a run of a level (see GamePlay.RaceScore): a row of three, the ones won in yellow.</summary>
    public static class StarIcons
    {
        private const int TextureSize = 64;
        private const int MaxStars = 3;
        //The gap between the stars, as a part of their size
        private const float Gap = 0.2f;

        private static Texture2D texture;

        /// <summary>A white five-pointed star with soft edges (its alpha is its coverage).</summary>
        private static Texture2D Texture(GraphicsDevice device)
        {
            if (texture != null && !texture.IsDisposed)
                return texture;

            Vector2[] points = new Vector2[10];
            for (int i = 0; i < points.Length; i++)
            {
                double angle = -Math.PI / 2 + i * Math.PI / 5;
                float radius = i % 2 == 0 ? 0.5f : 0.2f;
                points[i] = new Vector2(0.5f + radius * (float)Math.Cos(angle), 0.53f + radius * (float)Math.Sin(angle));
            }

            //4 × 4 samples per pixel
            Color[] pixels = new Color[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                            if (Contains(points, new Vector2((x + (sx + 0.5f) / 4f) / TextureSize, (y + (sy + 0.5f) / 4f) / TextureSize)))
                                inside++;
                    pixels[y * TextureSize + x] = Color.White * (inside / 16f);
                }
            }
            texture = new Texture2D(device, TextureSize, TextureSize);
            texture.SetData(pixels);
            return texture;
        }

        private static bool Contains(Vector2[] polygon, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                if ((polygon[i].Y > point.Y) != (polygon[j].Y > point.Y)
                    && point.X < (polygon[j].X - polygon[i].X) * (point.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X)
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>Width of the row of stars as large as size.</summary>
        public static float Width(float size)
        {
            return size * (MaxStars + Gap * (MaxStars - 1));
        }

        /// <summary>Draws the row of stars from its top left corner; called between SpriteBatch.Begin and End.</summary>
        public static void Draw(SpriteBatch batch, Vector2 position, float size, int stars, float alpha)
        {
            Texture2D star = Texture(batch.GraphicsDevice);
            for (int i = 0; i < MaxStars; i++)
            {
                Rectangle bounds = new Rectangle((int)(position.X + i * size * (1f + Gap)), (int)position.Y, (int)size, (int)size);
                batch.Draw(star, bounds, (i < stars ? MainMenuScreen.Yellow : Color.White * 0.25f) * alpha);
            }
        }
    }
}
