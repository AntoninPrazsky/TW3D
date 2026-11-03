using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TW3D.Screens;

namespace TW3D.GamePlay
{
    /// <summary>
    /// The speed of the car in km/h on a round dial with a needle, as the cars of the past had them, at the left edge of
    /// the screen in the middle of its height: behind the car, which drives to the right (the screen ahead of it stays
    /// clear) and is in the middle, above the joystick of the on-screen controls in the bottom left corner, never low
    /// enough to touch it. Nothing of it is a picture: the dial (a
    /// chrome bezel, a dark face, the ticks, a red zone), the needle and its cap are drawn by the code into textures as
    /// large as they are on the screen, smoothed at their edges, made again when the size changes; the numbers and
    /// "km/h" are written by the font of the menus. The needle follows the speed with a little lag, as a real one does.
    /// </summary>
    sealed class Speedometer : IDisposable
    {
        /// <summary>The end of the scale, km/h.</summary>
        public const float MaxSpeed = 160f;
        //Numbers every this many km/h, and a short tick between them; the red zone from this speed on
        private const int NumberStep = 20;
        private const int TickStep = 10;
        private const float RedFrom = 130f;
        //The scale spans this angle (radians), symmetric about the top of the dial
        private static readonly float Sweep = MathHelper.ToRadians(240f);
        //The size of the dial at the reference resolution (pixels, scaled as the on-screen controls are), how far it
        //stands from the left edge of the screen (as far as the joystick under it) and at least how far above the joystick
        private const float ReferenceSize = 215f;
        private const float EdgeMargin = 48f;
        private const float JoystickGap = 24f;
        //How fast the needle follows the speed (1 / seconds)
        private const float NeedleRate = 8f;

        /// <summary>
        /// A physics unit in metres: the car is 21.4 physics units long (8.56 model units), a buggy of about 3.5 m. With
        /// <see cref="GameplayScreen"/>'s game speed (the simulation runs faster than real time) the cars reach about
        /// 80–100 km/h on their own and more down a long slope.
        /// </summary>
        private const float MetresPerUnit = 3.5f / 21.4f;

        //Radii on the dial, as parts of its radius: the bezel, the face, the ticks, the red zone and the numbers
        private const float BezelInner = 0.88f;
        private const float TickOuter = 0.84f, LongTick = 0.15f, ShortTick = 0.08f;
        private const float RedOuter = 0.84f, RedInner = 0.78f;
        private const float NumbersRadius = 0.54f;

        private static readonly Color FaceCentre = new Color(40, 42, 47), FaceEdge = new Color(14, 15, 17);
        private static readonly Color BezelLight = new Color(232, 234, 238), BezelDark = new Color(72, 75, 82);
        private static readonly Color Marks = new Color(238, 238, 232);
        private static readonly Color Red = new Color(214, 36, 30);
        private static readonly Color NeedleColour = new Color(255, 96, 34), NeedleEdge = new Color(150, 40, 16);

        private readonly GraphicsDevice device;
        private Texture2D dial, needle, cap;
        private int size;
        //The speed the needle shows (km/h)
        private float shown;

        public Speedometer(GraphicsDevice device)
        {
            this.device = device;
        }

        /// <summary>The speed of a car in km/h.</summary>
        /// <param name="unitsPerSecond">Its speed in physics units per second of game time.</param>
        /// <param name="gameSpeed">Game seconds in a real second.</param>
        public static float Kmh(float unitsPerSecond, float gameSpeed) => unitsPerSecond * gameSpeed * MetresPerUnit * 3.6f;

        /// <summary>The needle moves towards a speed.</summary>
        /// <param name="kmh">The speed of the car, km/h.</param>
        /// <param name="elapsed">Real seconds since the last update.</param>
        public void Update(float kmh, float elapsed)
        {
            shown += (Math.Min(kmh, MaxSpeed * 1.04f) - shown) * (1f - MathF.Exp(-NeedleRate * elapsed));
        }

        /// <summary>Draws the speedometer; called between SpriteBatch.Begin and End.</summary>
        /// <param name="alpha">How visible it is (it fades out as the results come).</param>
        /// <param name="joystickTop">The top of the joystick of the on-screen controls (screen y): the dial stays above it.</param>
        public void Draw(ScreenManager manager, float alpha, float joystickTop)
        {
            if (alpha <= 0f)
                return;
            int wanted = Math.Max(32, (int)MathF.Round(ReferenceSize * manager.ControlsScale));
            if (wanted != size)
                Build(wanted);

            Viewport viewport = manager.Viewport;
            float radius = size / 2f;
            float x = EdgeMargin * manager.ControlsScale + radius;
            float y = Math.Min(viewport.Height / 2f, joystickTop - JoystickGap * manager.ControlsScale - radius);
            Vector2 centre = new Vector2(x, Math.Max(radius, y));
            SpriteBatch batch = manager.SpriteBatch;
            batch.Draw(dial, centre - new Vector2(radius), Color.White * alpha);

            //The numbers upright around the scale, and the unit under the middle
            GameFont font = manager.Fonts.Menu;
            float numberScale = radius * 0.155f / font.MeasureString("0").Y;
            for (int speed = 0; speed <= MaxSpeed; speed += NumberStep)
            {
                string text = speed.ToString();
                Vector2 textSize = font.MeasureString(text) * numberScale;
                Vector2 at = centre + Direction(Angle(speed)) * radius * NumbersRadius;
                font.Draw(batch, text, at - textSize / 2f, (speed >= RedFrom ? Red : Marks) * alpha, numberScale);
            }
            float unitScale = numberScale * 0.85f;
            Vector2 unitSize = font.MeasureString("km/h") * unitScale;
            font.Draw(batch, "km/h", centre + new Vector2(0f, radius * 0.38f) - unitSize / 2f, Marks * (0.75f * alpha), unitScale);

            //The needle turns about its pivot, the cap covers it
            batch.Draw(needle, centre, null, Color.White * alpha, Angle(shown), new Vector2(needle.Width / 2f, NeedlePivot * radius), 1f, SpriteEffects.None, 0f);
            batch.Draw(cap, centre - new Vector2(cap.Width / 2f), Color.White * alpha);
        }

        /// <summary>The angle of a speed on the scale (radians, clockwise from the top of the dial).</summary>
        private static float Angle(float speed) => -Sweep / 2f + MathHelper.Clamp(speed / MaxSpeed, -0.02f, 1.04f) * Sweep;

        /// <summary>The direction of an angle on the screen (y down).</summary>
        private static Vector2 Direction(float angle) => new Vector2(MathF.Sin(angle), -MathF.Cos(angle));

        //The needle: its length from the pivot to the tip and of the tail behind it, its half width at the pivot, at the
        //tip and at the end of the tail (parts of the radius of the dial); the pivot's distance from the top of its texture
        private const float NeedleLength = 0.8f, NeedleTail = 0.18f;
        private const float NeedleBase = 0.035f, NeedleTip = 0.008f, NeedleTailEnd = 0.022f;
        private const float NeedlePivot = NeedleLength + 0.02f;
        private const float CapRadius = 0.09f;

        /// <summary>Draws the dial, the needle and the cap into textures for a diameter of the dial in pixels.</summary>
        private void Build(int diameter)
        {
            Release();
            size = diameter;
            float radius = diameter / 2f;
            dial = Paint(diameter, diameter, (x, y) => DialPixel((x - radius) / radius, (y - radius) / radius, 1f / radius));

            int needleWidth = (int)MathF.Ceiling(NeedleBase * 2f * radius) + 4;
            int needleHeight = (int)MathF.Ceiling((NeedlePivot + NeedleTail + 0.02f) * radius);
            needle = Paint(needleWidth, needleHeight, (x, y) => NeedlePixel((x - needleWidth / 2f) / radius, NeedlePivot - y / radius, 1f / radius));

            int capSize = (int)MathF.Ceiling(CapRadius * 2f * radius) + 2;
            cap = Paint(capSize, capSize, (x, y) => CapPixel((x - capSize / 2f) / radius, (y - capSize / 2f) / radius, 1f / radius));
        }

        /// <summary>A texture from a function of the middle of each pixel; the colours are premultiplied by alpha, as SpriteBatch draws them.</summary>
        private Texture2D Paint(int width, int height, Func<float, float, Vector4> pixel)
        {
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector4 colour = pixel(x + 0.5f, y + 0.5f);
                    pixels[y * width + x] = new Color(colour.X * colour.W, colour.Y * colour.W, colour.Z * colour.W, colour.W);
                }
            }
            Texture2D texture = new Texture2D(device, width, height);
            texture.SetData(pixels);
            return texture;
        }

        /// <summary>How much of a pixel a shape covers, from the distance of its middle from the edge (negative inside).</summary>
        private static float Cover(float distance, float pixel) => MathHelper.Clamp(0.5f - distance / pixel, 0f, 1f);

        /// <summary>A pixel of the dial at a point (parts of the radius from the middle, y down).</summary>
        private static Vector4 DialPixel(float x, float y, float pixel)
        {
            float r = MathF.Sqrt(x * x + y * y);
            float outside = Cover(r - 1f, pixel);
            if (outside <= 0f)
                return Vector4.Zero;

            //The bezel: chrome, light from the top left, rounded across its width; the face dark, lighter in the middle
            Vector2 towards = r > 0f ? new Vector2(x, y) / r : Vector2.Zero;
            float light = 0.5f - 0.5f * Vector2.Dot(towards, Vector2.Normalize(new Vector2(1f, 1f)));
            float across = MathHelper.Clamp((r - BezelInner) / (1f - BezelInner), 0f, 1f);
            float round = MathF.Sin(across * MathHelper.Pi);
            Vector3 bezel = Vector3.Lerp(BezelDark.ToVector3(), BezelLight.ToVector3(), MathHelper.Clamp(light * 0.8f + round * 0.35f, 0f, 1f));
            Vector3 face = Vector3.Lerp(FaceCentre.ToVector3(), FaceEdge.ToVector3(), MathHelper.Clamp(r / BezelInner, 0f, 1f));
            Vector3 colour = Vector3.Lerp(face, bezel, Cover(BezelInner - r, pixel));

            //A dark line where the face meets the bezel
            colour = Vector3.Lerp(colour, new Vector3(0.03f), Cover(MathF.Abs(r - BezelInner) - 0.008f, pixel) * 0.8f);

            if (r < BezelInner)
            {
                //The red zone: a band along the end of the scale
                float angle = MathF.Atan2(x, -y);
                float from = Angle(RedFrom), to = Angle(MaxSpeed);
                float band = MathF.Max(MathF.Max(RedInner - r, r - RedOuter), MathF.Max(from - angle, angle - to) * r);
                colour = Vector3.Lerp(colour, Red.ToVector3(), Cover(band, pixel));

                //The ticks: a long one with every number, a short one between them
                for (int speed = 0; speed <= MaxSpeed; speed += TickStep)
                {
                    bool major = speed % NumberStep == 0;
                    float length = major ? LongTick : ShortTick;
                    float halfWidth = major ? 0.014f : 0.008f;
                    Vector2 direction = Direction(Angle(speed));
                    float along = x * direction.X + y * direction.Y;
                    float side = MathF.Abs(x * direction.Y - y * direction.X);
                    float distance = MathF.Max(side - halfWidth, MathF.Max(TickOuter - length - along, along - TickOuter));
                    colour = Vector3.Lerp(colour, (speed >= RedFrom ? new Color(255, 225, 220) : Marks).ToVector3(), Cover(distance, pixel));
                }
            }
            return new Vector4(colour, outside);
        }

        /// <summary>A pixel of the needle, pointing up: x across it, along its length up from the pivot (parts of the radius).</summary>
        private static Vector4 NeedlePixel(float x, float along, float pixel)
        {
            float halfWidth = along >= 0f
                ? MathHelper.Lerp(NeedleBase, NeedleTip, along / NeedleLength)
                : MathHelper.Lerp(NeedleBase, NeedleTailEnd, -along / NeedleTail);
            float distance = MathF.Max(MathF.Abs(x) - halfWidth, MathF.Max(along - NeedleLength, -along - NeedleTail));
            float cover = Cover(distance, pixel);
            if (cover <= 0f)
                return Vector4.Zero;
            //Darker along its edges, so it reads as a ridge
            float edge = MathHelper.Clamp(MathF.Abs(x) / MathF.Max(halfWidth, pixel), 0f, 1f);
            return new Vector4(Vector3.Lerp(NeedleColour.ToVector3(), NeedleEdge.ToVector3(), edge * edge), cover);
        }

        /// <summary>A pixel of the cap over the pivot: a small dome of dark chrome.</summary>
        private static Vector4 CapPixel(float x, float y, float pixel)
        {
            float r = MathF.Sqrt(x * x + y * y);
            float cover = Cover(r - CapRadius, pixel);
            if (cover <= 0f)
                return Vector4.Zero;
            float light = MathHelper.Clamp(0.55f - (x + y) / CapRadius * 0.35f, 0f, 1f);
            return new Vector4(Vector3.Lerp(new Vector3(0.08f), new Vector3(0.62f), light), cover);
        }

        private void Release()
        {
            dial?.Dispose();
            needle?.Dispose();
            cap?.Dispose();
        }

        public void Dispose()
        {
            Release();
        }
    }
}
