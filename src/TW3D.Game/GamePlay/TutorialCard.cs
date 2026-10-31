using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TW3D.Input;
using TW3D.Screens;

namespace TW3D.GamePlay
{
    /// <summary>
    /// Draws the card of the tutorial (see <see cref="Tutorial"/>) at the top of the screen under the time of the run:
    /// the glyph of the control for the device the player uses last (a key, a gamepad button or stick, or the on-screen
    /// control for touch and the mouse), the instruction beside it, a short line under it, and the praise under them
    /// when the action is done. It arrives with a bounce, bobs slowly while it is up, and has no plate.
    /// </summary>
    static class TutorialCard
    {
        //The font scales of the instruction (larger than the time of the run: it is the text a new player must read),
        //of the line under it and of the praise
        private const float CaptionScale = 0.8f;
        private const float DetailScale = 0.42f;
        private const float PraiseScale = 0.6f;
        //The height of a glyph (a gamepad button) relative to the capitals of the instruction
        private const float GlyphSize = 2.2f;
        //Reference pixels above the card and between the glyph and the instruction
        private const float TopGap = 28f;
        private const float GlyphGap = 20f;
        //How long a new step and the praise kick (seconds)
        private const float KickTime = 0.3f;
        //The space between two glyphs of one control (the arrow keys), relative to the height of a glyph
        private const float GlyphSpacing = 0.15f;

        //PromptFont's glyphs (see content/Fonts/promptFont.spritefont); the A button sets their size
        private const char PadA = '⇓';
        private const char PadB = '⇒';
        private const char PadLeftStickRight = '⇀';
        private const char PadLeftStickUpDown = '⇅';
        private const char KeyRight = '→';
        private const char KeyUp = '↑';
        private const char KeyDown = '↓';
        private const char KeySpace = '␺';
        private const char KeyLeftCtrl = '⑍';

        //The glyphs of the fonts; SpriteFont.GetGlyphs makes a new dictionary each time
        private static readonly Dictionary<SpriteFont, Dictionary<char, SpriteFont.Glyph>> glyphs = new Dictionary<SpriteFont, Dictionary<char, SpriteFont.Glyph>>();

        /// <summary>Draws the card, if there is one; called between SpriteBatch.Begin and End.</summary>
        /// <param name="top">Where the card may begin (under the time of the run).</param>
        public static void Draw(ScreenManager manager, Tutorial tutorial, OnScreenControls controls, float top)
        {
            Lesson lesson = tutorial.Lesson;
            if (lesson == null)
                return;

            SpriteFont font = manager.Fonts.Menu;
            float ui = manager.UiScale;
            LessonStep step = tutorial.CurrentStep;
            string caption = step?.Caption ?? lesson.Caption;

            //Arriving from smaller, overshooting a little; a new step kicks it; leaving, it fades out
            float arrival = MathHelper.Clamp(tutorial.Age / Tutorial.ArriveTime, 0f, 1f);
            float size = MathHelper.Lerp(0.6f, 1f, BackOut(arrival)) * (1f + 0.12f * Kick(tutorial.Step > 0 ? tutorial.StepAge : float.MaxValue));
            float alpha = MathHelper.Clamp(arrival * 3f, 0f, 1f) * (1f - tutorial.Leaving);
            if (alpha <= 0f)
                return;

            //The card is laid out at its full size and scaled about the middle of its first row, so nothing moves as it
            //arrives, bobs or kicks. The row is centred on the capitals of the instruction (the line of the font has
            //room above and below them).
            SpriteFont.Glyph capital = Glyphs(font)['H'];
            float rowHeight = capital.BoundsInTexture.Height * CaptionScale * ui * GlyphSize;
            float rowCentreY = top + TopGap * ui + rowHeight / 2f + MathF.Sin(tutorial.Age * 2.4f) * 3f * ui;
            float centreX = manager.Viewport.Width / 2f;

            float captionScale = CaptionScale * ui * size;
            Vector2 captionSize = font.MeasureString(caption) * captionScale;
            float capitalsCentre = (capital.Cropping.Y + capital.BoundsInTexture.Height / 2f) * captionScale;
            float glyphHeight = rowHeight * size;
            bool onScreen = manager.Input.LastDevice == InputDevice.Touch || manager.Input.LastDevice == InputDevice.Mouse;
            bool gamePad = manager.Input.LastDevice == InputDevice.GamePad;
            float glyphWidth = step == null ? 0f
                : onScreen ? controls.IconWidth(step.Control, glyphHeight)
                : GlyphsWidth(manager.Fonts.Prompts, GlyphsOf(step.Control, gamePad), glyphHeight);
            float gap = step == null ? 0f : GlyphGap * ui * size;
            float left = centreX - (glyphWidth + gap + captionSize.X) / 2f;

            if (step != null)
            {
                if (onScreen)
                {
                    //The on-screen controls are light and half transparent; a shadow sets them off the sky like the texts
                    Vector2 iconCentre = new Vector2(left + glyphWidth / 2f, rowCentreY);
                    controls.DrawIcon(manager.SpriteBatch, step.Control, iconCentre + ShadowOffset(manager), glyphHeight, Color.Black * (0.7f * alpha), tutorial.StepAge);
                    controls.DrawIcon(manager.SpriteBatch, step.Control, iconCentre, glyphHeight, Color.White * alpha, tutorial.StepAge);
                }
                else
                    DrawGlyphs(manager, GlyphsOf(step.Control, gamePad), new Vector2(left, rowCentreY), glyphHeight, alpha);
            }
            manager.DrawShadowedText(font, caption, new Vector2(left + glyphWidth + gap, rowCentreY - capitalsCentre),
                Color.White * alpha, Color.Black * alpha, captionScale);

            //The line under it, then the praise, each centred; the praise has its own row, so nothing moves when it comes
            float y = rowCentreY + rowHeight / 2f * size;
            if (step?.Detail != null)
                y += DrawCentred(manager, step.Detail, centreX, y, DetailScale * ui * size, Color.LightGray * alpha, alpha);
            if (tutorial.PraiseAge is float praiseAge)
            {
                float praiseAlpha = alpha * MathHelper.Clamp(praiseAge * 6f, 0f, 1f);
                DrawCentred(manager, lesson.Praise, centreX, y, PraiseScale * ui * size * (1f + 0.35f * Kick(praiseAge)),
                    MainMenuScreen.Yellow * praiseAlpha, praiseAlpha);
            }
        }

        /// <summary>The glyphs of a control: the key or keys, or the gamepad button or stick.</summary>
        private static string GlyphsOf(CarControl control, bool gamePad)
        {
            return control switch
            {
                CarControl.Throttle => gamePad ? PadLeftStickRight.ToString() : KeyRight.ToString(),
                CarControl.Axles => gamePad ? PadLeftStickUpDown.ToString() : new string(new[] { KeyUp, KeyDown }),
                CarControl.Brake => gamePad ? PadA.ToString() : KeySpace.ToString(),
                _ => gamePad ? PadB.ToString() : KeyLeftCtrl.ToString()
            };
        }

        private static Dictionary<char, SpriteFont.Glyph> Glyphs(SpriteFont font)
        {
            if (!glyphs.TryGetValue(font, out Dictionary<char, SpriteFont.Glyph> fontGlyphs))
            {
                fontGlyphs = font.GetGlyphs();
                glyphs[font] = fontGlyphs;
            }
            return fontGlyphs;
        }

        /// <summary>The scale of PromptFont that makes a gamepad button as tall as a glyph is to be.</summary>
        private static float GlyphScale(SpriteFont font, float height)
        {
            return height / Glyphs(font)[PadA].BoundsInTexture.Height;
        }

        private static float GlyphsWidth(SpriteFont font, string text, float height)
        {
            float width = (text.Length - 1) * GlyphSpacing * height;
            foreach (char glyph in text)
                width += Glyphs(font)[glyph].BoundsInTexture.Width * GlyphScale(font, height);
            return width;
        }

        /// <summary>
        /// Draws glyphs of PromptFont in a row from a point on the left, their ink centred on its height, shadowed like
        /// the texts.
        /// </summary>
        private static void DrawGlyphs(ScreenManager manager, string text, Vector2 leftCentre, float height, float alpha)
        {
            SpriteFont font = manager.Fonts.Prompts;
            float scale = GlyphScale(font, height);
            float x = leftCentre.X;
            foreach (char glyph in text)
            {
                Rectangle bounds = Glyphs(font)[glyph].BoundsInTexture;
                Vector2 origin = new Vector2(0f, bounds.Height / 2f);
                Vector2 position = new Vector2(x, leftCentre.Y);
                manager.SpriteBatch.Draw(font.Texture, position + ShadowOffset(manager), bounds, Color.Black * alpha, 0f, origin, scale, SpriteEffects.None, 0f);
                manager.SpriteBatch.Draw(font.Texture, position, bounds, Color.White * alpha, 0f, origin, scale, SpriteEffects.None, 0f);
                x += bounds.Width * scale + GlyphSpacing * height;
            }
        }

        /// <summary>The offset of the shadows, as ScreenManager.DrawShadowedText has it.</summary>
        private static Vector2 ShadowOffset(ScreenManager manager)
        {
            return new Vector2(Math.Max(1f, 2f * manager.UiScale));
        }

        /// <summary>Draws a text centred under a point; returns its height.</summary>
        private static float DrawCentred(ScreenManager manager, string text, float centreX, float top, float scale, Color colour, float alpha)
        {
            SpriteFont font = manager.Fonts.Menu;
            Vector2 size = font.MeasureString(text) * scale;
            manager.DrawShadowedText(font, text, new Vector2(centreX - size.X / 2f, top), colour, Color.Black * alpha, scale);
            return size.Y;
        }

        /// <summary>1 at the start of a kick, falling to 0 over its time.</summary>
        private static float Kick(float age)
        {
            return age < KickTime ? 1f - age / KickTime : 0f;
        }

        /// <summary>An ease that overshoots a little and settles (0..1 to 0..1).</summary>
        private static float BackOut(float t)
        {
            const float overshoot = 1.70158f;
            float u = t - 1f;
            return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
        }
    }
}
