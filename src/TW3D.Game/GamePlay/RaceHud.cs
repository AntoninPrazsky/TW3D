using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TW3D.Screens;

namespace TW3D.GamePlay
{
    /// <summary>
    /// What a run shows over the level: the time of the run at the top of the screen with the par time of the level
    /// below it, and in the place of the time during the countdown the start lights, five red lights which come on one by
    /// one as those of the start gate do and go out at the start, as in Formula 1; in the middle of the screen the finish,
    /// or the game over. The speedometer at the bottom is the <see cref="Speedometer"/>.
    /// </summary>
    static class RaceHud
    {
        //How long the start lights fade out after the start, and when the time then begins to fade in and how long it takes
        //(seconds); the font scales of the finish, of the time and of the par time
        private const float LightsFade = 0.5f;
        private const float TimeComes = 0.4f;
        private const float TimeFade = 0.4f;
        private const float BigScale = 2.6f;
        private const float TimeScale = 0.6f;
        private const float ParScale = 0.35f;
        //The start lights: the size of a light (pixels at the reference size of the screen), of its black bezel and of its
        //glow when it is on, and the distance of the lights (parts of the size); their middle, as a part of the height of
        //the line of the time, from its top (the middle of its digits)
        private const float LightSize = 28f;
        private const float BezelSize = 1.3f;
        private const float GlowSize = 2.6f;
        private const float LightSpacing = 1.45f;
        private const float LightsMiddle = 0.5f;
        //The colours of a start light on and off, and of its glow (added to what is under it)
        private static readonly Color LightOn = new Color(255, 36, 24);
        private static readonly Color LightOff = new Color(58, 10, 8);
        private static readonly Color LightGlow = new Color(255, 40, 20, 0) * 0.75f;
        //The colour of Game over: a soft red
        private static readonly Color GameOverColour = new Color(255, 120, 100);
        //The size of the textures of the start lights (pixels)
        private const int DiscTexture = 64;

        private static Texture2D disc, glow;

        /// <summary>Draws the display of a run; called between SpriteBatch.Begin and End. Returns where the time and the par time end.</summary>
        /// <param name="timeAlpha">How visible the time and the par time are: they fade out as the results come.</param>
        public static float Draw(ScreenManager manager, Race race, float timeAlpha)
        {
            GameFont font = manager.Fonts.Menu;
            float uiScale = manager.UiScale;
            Rectangle safe = manager.SafeArea;
            float centreX = manager.Viewport.Width / 2f;

            //The start lights take the place of the time during the countdown (it would show nothing but zero): on as
            //those of the start gate, out at the start and fading away as the time comes. So the display is no taller
            //for them, and the start gate framed by the camera above the car stays clear of it (on a phone too).
            float lights = race.State == RaceState.Countdown ? 1f
                : race.State == RaceState.Racing ? 1f - SmoothStep(race.Time / LightsFade) : 0f;
            float timeShown = timeAlpha * (race.State == RaceState.Countdown ? 0f
                : race.State == RaceState.Racing ? SmoothStep((race.Time - TimeComes) / TimeFade) : 1f);
            string time = RaceScore.FormatTime(race.State == RaceState.Countdown ? 0f : race.Time);
            Color timeColour = race.State == RaceState.Finished ? MainMenuScreen.Yellow : Color.White;
            Vector2 timeSize = DrawTime(manager, time, new Vector2(centreX, safe.Top), timeColour * timeShown, TimeScale * uiScale, timeShown);
            if (lights > 0f)
                DrawStartLights(manager, new Vector2(centreX, safe.Top + timeSize.Y * LightsMiddle), LightSize * uiScale, race.RedSignals, lights * timeAlpha);

            float bottom = safe.Top + timeSize.Y;
            if (race.HasFinish)
            {
                string par = "Par " + RaceScore.FormatTime(race.Par);
                Vector2 parSize = font.MeasureString(par) * ParScale * uiScale;
                manager.DrawShadowedText(font, par, new Vector2(centreX - parSize.X / 2f, bottom), Color.LightGray * timeAlpha, Color.Black * timeAlpha, ParScale * uiScale);
                bottom += parSize.Y;
            }

            //At the finish Finish, until the results come, or Game over when the car fell off the level
            switch (race.State)
            {
                case RaceState.Finished when !race.ResultsDue:
                    DrawBig(manager, "Finish", MainMenuScreen.Yellow);
                    break;
                case RaceState.Lost when !race.ResultsDue:
                    DrawBig(manager, "Game over", GameOverColour);
                    break;
            }
            return bottom;
        }

        /// <summary>
        /// Draws a time centred under a point, its digits in cells of the same width, so that it stands still as the
        /// digits change (the digits of the font are not equally wide). Returns its size.
        /// </summary>
        private static Vector2 DrawTime(ScreenManager manager, string text, Vector2 topCentre, Color colour, float scale, float alpha)
        {
            GameFont font = manager.Fonts.Menu;
            float digitWidth = 0f;
            for (char digit = '0'; digit <= '9'; digit++)
                digitWidth = Math.Max(digitWidth, font.MeasureString(digit.ToString()).X);

            float width = 0f;
            foreach (char c in text)
                width += char.IsDigit(c) ? digitWidth : font.MeasureString(c.ToString()).X;

            float x = topCentre.X - width * scale / 2f;
            foreach (char c in text)
            {
                string glyph = c.ToString();
                float glyphWidth = font.MeasureString(glyph).X;
                float cell = char.IsDigit(c) ? digitWidth : glyphWidth;
                manager.DrawShadowedText(font, glyph, new Vector2(x + (cell - glyphWidth) * scale / 2f, topCentre.Y), colour, Color.Black * alpha, scale);
                x += cell * scale;
            }
            return new Vector2(width, font.LineSpacing) * scale;
        }

        /// <summary>Draws a text large in the middle of the screen, in capitals.</summary>
        private static void DrawBig(ScreenManager manager, string text, Color colour)
        {
            //In capitals, as the titles are written
            text = text.ToUpperInvariant();
            GameFont font = manager.Fonts.Menu;
            float scale = BigScale * manager.UiScale;
            Vector2 textSize = font.MeasureString(text) * scale;
            Vector2 position = new Vector2(manager.Viewport.Width, manager.Viewport.Height) / 2f - textSize / 2f;
            manager.DrawShadowedText(font, text, position, colour, Color.Black, scale);
        }

        /// <summary>
        /// Draws the row of the start lights (<see cref="Race.SignalColumns"/> of them) centred on a point, the first
        /// <paramref name="lit"/> of them on: each a red lens in a black bezel, glowing when it is on.
        /// </summary>
        private static void DrawStartLights(ScreenManager manager, Vector2 centre, float size, int lit, float alpha)
        {
            SpriteBatch batch = manager.SpriteBatch;
            if (disc == null || disc.IsDisposed)
            {
                disc = Paint(batch.GraphicsDevice, r => MathHelper.Clamp((1f - r) * DiscTexture / 2f, 0f, 1f));
                glow = Paint(batch.GraphicsDevice, r => MathF.Pow(MathHelper.Clamp(1f - r, 0f, 1f), 2.2f));
            }

            float spacing = LightSpacing * size;
            for (int i = 0; i < Race.SignalColumns; i++)
            {
                Vector2 middle = new Vector2(centre.X + (i - (Race.SignalColumns - 1) / 2f) * spacing, centre.Y);
                bool on = i < lit;
                if (on)
                    batch.Draw(glow, Square(middle, GlowSize * size), LightGlow * alpha);
                batch.Draw(disc, Square(middle, BezelSize * size), Color.Black * (0.85f * alpha));
                batch.Draw(disc, Square(middle, size), (on ? LightOn : LightOff) * alpha);
                //A highlight of the lens, from the top left
                batch.Draw(disc, Square(middle - new Vector2(0.18f, 0.2f) * size, 0.32f * size), Color.White * ((on ? 0.45f : 0.12f) * alpha));
            }
        }

        /// <summary>The square of a size centred on a point.</summary>
        private static Rectangle Square(Vector2 middle, float size)
        {
            int side = Math.Max(1, (int)MathF.Round(size));
            return new Rectangle((int)MathF.Round(middle.X - side / 2f), (int)MathF.Round(middle.Y - side / 2f), side, side);
        }

        /// <summary>A white round texture whose alpha is a function of the distance from its middle (1 at its edge), premultiplied.</summary>
        private static Texture2D Paint(GraphicsDevice device, Func<float, float> alpha)
        {
            Color[] pixels = new Color[DiscTexture * DiscTexture];
            for (int y = 0; y < DiscTexture; y++)
            {
                for (int x = 0; x < DiscTexture; x++)
                {
                    float dx = (x + 0.5f) / DiscTexture * 2f - 1f, dy = (y + 0.5f) / DiscTexture * 2f - 1f;
                    pixels[y * DiscTexture + x] = Color.White * alpha(MathF.Sqrt(dx * dx + dy * dy));
                }
            }
            Texture2D texture = new Texture2D(device, DiscTexture, DiscTexture);
            texture.SetData(pixels);
            return texture;
        }

        private static float SmoothStep(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}
