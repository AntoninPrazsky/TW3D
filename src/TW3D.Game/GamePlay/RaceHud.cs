using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TW3D.Screens;

namespace TW3D.GamePlay
{
    /// <summary>
    /// What a run shows over the level: the time of the run at the top of the screen with the par time of the level
    /// below it, and in the middle of the screen the countdown (3, 2, 1, Start) and the finish.
    /// </summary>
    static class RaceHud
    {
        //How long "Start" shows after the start (seconds); the font scales of the countdown, of the time and of the par time
        private const float StartShown = 0.8f;
        private const float CountdownScale = 2.6f;
        private const float TimeScale = 0.6f;
        private const float ParScale = 0.35f;

        /// <summary>Draws the display of a run; called between SpriteBatch.Begin and End. Returns where the time and the par time end.</summary>
        /// <param name="timeAlpha">How visible the time and the par time are: they fade out as the results come.</param>
        public static float Draw(ScreenManager manager, Race race, float timeAlpha)
        {
            SpriteFont font = manager.Fonts.Menu;
            float uiScale = manager.UiScale;
            Rectangle safe = manager.SafeArea;
            float centreX = manager.Viewport.Width / 2f;

            string time = RaceScore.FormatTime(race.State == RaceState.Countdown ? 0f : race.Time);
            Color timeColour = race.State == RaceState.Finished ? MainMenuScreen.Yellow : Color.White;
            Vector2 timeSize = DrawTime(manager, time, new Vector2(centreX, safe.Top), timeColour * timeAlpha, TimeScale * uiScale, timeAlpha);
            float bottom = safe.Top + timeSize.Y;
            if (race.HasFinish)
            {
                string par = "Par " + RaceScore.FormatTime(race.Par);
                Vector2 parSize = font.MeasureString(par) * ParScale * uiScale;
                manager.DrawShadowedText(font, par, new Vector2(centreX - parSize.X / 2f, bottom), Color.LightGray * timeAlpha, Color.Black * timeAlpha, ParScale * uiScale);
                bottom += parSize.Y;
            }

            //3, 2, 1: each number appears large and shrinks a little; then Start, fading out; at the finish Finish,
            //until the results come
            switch (race.State)
            {
                case RaceState.Countdown:
                    int number = (int)MathF.Ceiling(race.Countdown);
                    float age = number - race.Countdown;
                    DrawBig(manager, number.ToString(), Color.White, 1f + 0.3f * MathF.Max(0f, 1f - age * 4f), MathHelper.Clamp((1f - age) * 4f, 0f, 1f));
                    break;
                case RaceState.Racing when race.Time < StartShown:
                    DrawBig(manager, "Start", MainMenuScreen.Yellow, 1f, 1f - race.Time / StartShown);
                    break;
                case RaceState.Finished when !race.ResultsDue:
                    DrawBig(manager, "Finish", MainMenuScreen.Yellow, 1f, 1f);
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
            SpriteFont font = manager.Fonts.Menu;
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

        /// <summary>Draws a text large in the middle of the screen.</summary>
        /// <param name="size">The size relative to the size of the countdown.</param>
        private static void DrawBig(ScreenManager manager, string text, Color colour, float size, float alpha)
        {
            SpriteFont font = manager.Fonts.Menu;
            float scale = CountdownScale * size * manager.UiScale;
            Vector2 textSize = font.MeasureString(text) * scale;
            Vector2 position = new Vector2(manager.Viewport.Width, manager.Viewport.Height) / 2f - textSize / 2f;
            manager.DrawShadowedText(font, text, position, colour * alpha, Color.Black * alpha, scale);
        }
    }
}
