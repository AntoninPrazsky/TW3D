using System;

namespace TW3D.Graphics
{
    /// <summary>
    /// The noise the surfaces of the blocks are drawn from (Effects/Surfaces.fxh): computed by the game, not an image, in a
    /// texture of <see cref="Size"/> texels that tiles. The shader lays it on a face of a block once per cell of the level
    /// grid (the coarse lookup) and <c>SURFACE_FINE</c> times (the fine lookup), so its fields have two scales, and the
    /// surface of one block joins the surface of the next one without a seam. Its mipmaps are averaged here, so the detail
    /// fades out in the distance instead of shimmering.
    /// <list type="bullet">
    /// <item><description>Red: mottle, gradient noise of four octaves (4 to 32 waves across the texture), for patches of
    /// colour.</description></item>
    /// <item><description>Green and blue: the slope of a grain (gradient noise of three octaves, 16 to 64 waves) along the
    /// two axes of the texture, 0.5 for flat, for the relief: the shader tilts the normal by it. A slope rather than a
    /// height, so that the shader needs no derivatives for it and its mipmaps fade the relief with the
    /// distance.</description></item>
    /// <item><description>Alpha: the distance to the nearest of 32 × 32 random points (cellular noise), 0 at a point, each
    /// point's distances divided by a random size of its own, for stones and pebbles of many sizes (the shader cuts them
    /// out below a threshold: some points are too small to make any).</description></item>
    /// </list>
    /// Pure arithmetic, the same every time (fixed hashes), tested without a window.
    /// </summary>
    public static class SurfaceNoise
    {
        /// <summary>The texels on a side of the texture.</summary>
        public const int Size = 256;

        //The waves across the texture of the first octave of the mottle, its octaves, the same of the grain, and the points
        //of the cellular noise on a side
        private const int MottleFrequency = 4;
        private const int MottleOctaves = 4;
        private const int GrainFrequency = 16;
        private const int GrainOctaves = 3;
        private const int CellPoints = 32;
        //How far a point of the cellular noise may move from the middle of its square (a part of the square): less than a
        //half, so that the nearest point, and so every stone, is in the 3 × 3 squares around a texel
        private const float CellJitter = 0.4f;
        //The smallest size of a point of the cellular noise (its distances are divided by its size, 1 the largest)
        private const float PointSizeLeast = 0.15f;
        //The mottle and the grain are spread so that this many standard deviations reach from the middle to an end of the
        //range of a byte; the slopes of the grain likewise
        private const float Spread = 2.5f;

        //Each field has its own hashes
        private const uint MottleSalt = 1, GrainSalt = 2, CellSalt = 3, SeedSalt = 4;

        private static readonly Lazy<byte[][]> levels = new Lazy<byte[][]>(Compute);

        /// <summary>
        /// The texels of the texture and of each of its mipmaps (RGBA, 4 bytes a texel, row by row), from the full size down
        /// to one texel. Computed once.
        /// </summary>
        public static byte[][] Levels => levels.Value;

        /// <summary>
        /// The random value of a corner of the cells of the level grid, 0 to 1. The shader blends the values of the eight
        /// corners of a block's cell across it, which varies the surface from block to block; neighbouring blocks share
        /// their corners, so the variation has no seam either.
        /// </summary>
        public static float CornerSeed(int x, int y, int z)
        {
            return Unit(unchecked(Mix(Mix(Mix(Mix(SeedSalt) + (uint)z) + (uint)y) + (uint)x)));
        }

        private static byte[][] Compute()
        {
            float[] mottle = Fbm(MottleFrequency, MottleOctaves, MottleSalt);
            float[] grain = Fbm(GrainFrequency, GrainOctaves, GrainSalt);
            float[] cells = Cellular();

            //The slope of the grain along both axes (central differences, wrapping round), in its own units: its spread
            //is made the same as the mottle's below
            float[] slopeX = new float[Size * Size], slopeY = new float[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    slopeX[y * Size + x] = grain[y * Size + Wrap(x + 1)] - grain[y * Size + Wrap(x - 1)];
                    slopeY[y * Size + x] = grain[Wrap(y + 1) * Size + x] - grain[Wrap(y - 1) * Size + x];
                }
            }

            Normalize(mottle);
            //Both slopes by one scale, so that the relief does not lean along an axis
            float slopeScale = 0.5f / (Spread * MathF.Sqrt((Variance(slopeX) + Variance(slopeY)) / 2f));
            for (int i = 0; i < slopeX.Length; i++)
            {
                slopeX[i] = 0.5f + slopeX[i] * slopeScale;
                slopeY[i] = 0.5f + slopeY[i] * slopeScale;
            }

            //The mipmaps average the full texture as it is (its values within 0 to 1), 2 × 2 texels into one, before the
            //values are rounded to bytes
            float[][] fields = { mottle, slopeX, slopeY, cells };
            foreach (float[] field in fields)
                for (int i = 0; i < field.Length; i++)
                    field[i] = Math.Clamp(field[i], 0f, 1f);
            int count = 1 + (int)Math.Log2(Size);
            byte[][] result = new byte[count][];
            int size = Size;
            for (int level = 0; level < count; level++)
            {
                byte[] texels = new byte[size * size * 4];
                for (int i = 0; i < size * size; i++)
                    for (int channel = 0; channel < 4; channel++)
                        texels[i * 4 + channel] = (byte)MathF.Round(Math.Clamp(fields[channel][i], 0f, 1f) * 255f);
                result[level] = texels;
                if (size > 1)
                {
                    for (int channel = 0; channel < 4; channel++)
                        fields[channel] = Halve(fields[channel], size);
                    size /= 2;
                }
            }
            return result;
        }

        /// <summary>Gradient noise of some octaves, the first of this many waves across the texture, each twice as many and half as strong.</summary>
        private static float[] Fbm(int frequency, int octaves, uint salt)
        {
            float[] values = new float[Size * Size];
            float amplitude = 1f;
            for (int octave = 0; octave < octaves; octave++)
            {
                uint octaveSalt = salt * 16u + (uint)octave;
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                        values[y * Size + x] += amplitude * GradientNoise((x + 0.5f) * frequency / Size, (y + 0.5f) * frequency / Size, frequency, octaveSalt);
                frequency *= 2;
                amplitude *= 0.5f;
            }
            return values;
        }

        /// <summary>
        /// Perlin's gradient noise that repeats every <paramref name="period"/> units (its lattice wraps round), with a
        /// quintic fade, so that a relief from it has no creases along the lattice.
        /// </summary>
        private static float GradientNoise(float x, float y, int period, uint salt)
        {
            int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
            float fx = x - x0, fy = y - y0;
            float ux = Fade(fx), uy = Fade(fy);
            float a = Corner(x0, y0, fx, fy, period, salt);
            float b = Corner(x0 + 1, y0, fx - 1f, fy, period, salt);
            float c = Corner(x0, y0 + 1, fx, fy - 1f, period, salt);
            float d = Corner(x0 + 1, y0 + 1, fx - 1f, fy - 1f, period, salt);
            return Lerp(Lerp(a, b, ux), Lerp(c, d, ux), uy);
        }

        /// <summary>The gradient of a corner of the lattice (its direction from a hash) dotted with the offset from it.</summary>
        private static float Corner(int x, int y, float dx, float dy, int period, uint salt)
        {
            float angle = Hash(Modulo(x, period), Modulo(y, period), salt) * (MathF.PI * 2f);
            return MathF.Cos(angle) * dx + MathF.Sin(angle) * dy;
        }

        /// <summary>
        /// The distance to the nearest point of a jittered lattice that wraps round, divided by the size of the point (the
        /// nearest by that), as a part of the most a distance can be.
        /// </summary>
        private static float[] Cellular()
        {
            float[] values = new float[Size * Size];
            float square = (float)Size / CellPoints;
            //The farthest a texel can be from the nearest point
            float most = (0.5f + CellJitter) * MathF.Sqrt(2f);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float px = (x + 0.5f) / square, py = (y + 0.5f) / square;
                    int cx = (int)MathF.Floor(px), cy = (int)MathF.Floor(py);
                    float best = float.MaxValue;
                    for (int j = -1; j <= 1; j++)
                    {
                        for (int i = -1; i <= 1; i++)
                        {
                            int sx = cx + i, sy = cy + j;
                            int wx = Modulo(sx, CellPoints), wy = Modulo(sy, CellPoints);
                            float siteX = sx + 0.5f + CellJitter * (Hash(wx, wy, CellSalt * 3u) * 2f - 1f);
                            float siteY = sy + 0.5f + CellJitter * (Hash(wx, wy, CellSalt * 3u + 1u) * 2f - 1f);
                            //Sizes from small to full, most of them small (a point near the small end makes no stone)
                            float size = PointSizeLeast + (1f - PointSizeLeast) * MathF.Pow(Hash(wx, wy, CellSalt * 3u + 2u), 1.5f);
                            float dx = px - siteX, dy = py - siteY;
                            best = MathF.Min(best, MathF.Sqrt(dx * dx + dy * dy) / size);
                        }
                    }
                    values[y * Size + x] = MathF.Min(best / most, 1f);
                }
            }
            return values;
        }

        /// <summary>Spreads a field round 0.5: <see cref="Spread"/> standard deviations from its mean reach 0 and 1.</summary>
        private static void Normalize(float[] values)
        {
            float mean = 0f;
            foreach (float value in values)
                mean += value;
            mean /= values.Length;
            float scale = 0.5f / (Spread * MathF.Sqrt(Variance(values)));
            for (int i = 0; i < values.Length; i++)
                values[i] = 0.5f + (values[i] - mean) * scale;
        }

        private static float Variance(float[] values)
        {
            double sum = 0, squares = 0;
            foreach (float value in values)
            {
                sum += value;
                squares += value * value;
            }
            double mean = sum / values.Length;
            return (float)(squares / values.Length - mean * mean);
        }

        /// <summary>The field at half the size: each texel the mean of four.</summary>
        private static float[] Halve(float[] values, int size)
        {
            int half = size / 2;
            float[] result = new float[half * half];
            for (int y = 0; y < half; y++)
                for (int x = 0; x < half; x++)
                    result[y * half + x] = (values[2 * y * size + 2 * x] + values[2 * y * size + 2 * x + 1]
                        + values[(2 * y + 1) * size + 2 * x] + values[(2 * y + 1) * size + 2 * x + 1]) / 4f;
            return result;
        }

        /// <summary>A random value 0 to 1 of a point of a lattice.</summary>
        private static float Hash(int x, int y, uint salt)
        {
            return Unit(unchecked(Mix(Mix(Mix(salt) + (uint)y) + (uint)x)));
        }

        /// <summary>The top 24 bits of a hash as a value 0 to 1.</summary>
        private static float Unit(uint hash) => (hash >> 8) * (1f / 16777216f);

        /// <summary>
        /// Chris Wellons' lowbias32: every bit of the result depends on every bit of the value. A hash of several numbers
        /// adds each to the mix of the ones before it (a mix of their products XORed together repeats: -1 and 1 of two
        /// coordinates cancel out).
        /// </summary>
        private static uint Mix(uint h)
        {
            unchecked
            {
                h ^= h >> 16;
                h *= 0x7feb352du;
                h ^= h >> 15;
                h *= 0x846ca68bu;
                h ^= h >> 16;
                return h;
            }
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static int Modulo(int value, int period) => ((value % period) + period) % period;

        private static int Wrap(int texel) => Modulo(texel, Size);
    }
}
