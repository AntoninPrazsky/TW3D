using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TW3D.Audio;
using TW3D.GamePlay;
using TW3D.Input;

namespace TW3D.Screens
{
    /// <summary>
    /// The results of a level, over the level at its finish: the stars, the time, the score and the best time in the
    /// middle, along the bottom the level select, the level again and the next level. Esc (Back) goes to the level
    /// select. A run lost (the car fell off the level) gets them too, titled Game over: no stars, no time. A test drive
    /// of the level editor leaves them for the editor instead of the level select.
    /// </summary>
    public sealed class ResultsScreen : ListMenuScreen
    {
        //How much the level behind is darkened; the font scale of the results, the size of the stars (reference pixels)
        //and the line spacing relative to the line height
        private const float Dimming = 0.55f;
        private const float TextScale = 0.6f;
        private const float StarSize = 72f;
        private const float LineSpacing = 1.3f;
        //The stars ring one after another as the results come, a major chord, and a new record after them (seconds)
        private const float FirstStarTime = 0.35f;
        private const float StarInterval = 0.22f;
        private const float RecordDelay = 0.15f;
        private static readonly float[] StarPitches = { 0f, 4f / 12f, 7f / 12f };

        private readonly int stars;
        private readonly string[] lines;
        private readonly string note;
        private readonly bool noteHighlighted;
        private Action leave;
        private readonly bool record;
        //Seconds since the results came, the stars rung so far and whether the record has rung
        private float age;
        private int starsRung;
        private bool recordRung;

        /// <param name="time">The time of the run, in seconds.</param>
        /// <param name="par">The par time of the level (see <see cref="RaceScore"/>).</param>
        /// <param name="previousBest">The best time of the level before this run; null if it was not finished before.</param>
        /// <param name="record">The run is the best one.</param>
        /// <param name="counted">The run counts (the car was not moved by hand).</param>
        /// <param name="nextLevel">Starts the next level; null if this is the last one.</param>
        /// <param name="restart">Starts the level again.</param>
        /// <param name="leave">Leaves the level for the level select (or the level editor: see <paramref name="leaveText"/>).</param>
        /// <param name="leaveText">The button of <paramref name="leave"/>.</param>
        public ResultsScreen(float time, float par, float? previousBest, bool record, bool counted, Action nextLevel, Action restart, Action leave,
            string leaveText = "Level select")
            : base("Finish")
        {
            IsPopup = true;
            ItemsInRow = true;
            FadeInTime = TimeSpan.FromSeconds(0.4);
            FadeOutTime = TimeSpan.FromSeconds(0.25);
            this.record = record && counted;

            stars = RaceScore.Stars(time, par);
            lines = new[]
            {
                "Time " + RaceScore.FormatTime(time),
                "Score " + RaceScore.Score(time, par)
            };
            if (!counted)
                note = "Moved by hand: no record";
            else if (record)
            {
                note = "New record!";
                noteHighlighted = true;
            }
            else if (previousBest.HasValue)
                note = "Best " + RaceScore.FormatTime(previousBest.Value);

            AddButtons(nextLevel, restart, leave, leaveText);
        }

        /// <summary>The results of a run lost: the car fell off the level, so there are no stars and no time.</summary>
        /// <param name="previousBest">The best time of the level; null if it was not finished before.</param>
        /// <param name="nextLevel">Starts the next level; null if this is the last one.</param>
        /// <param name="restart">Starts the level again.</param>
        /// <param name="leave">Leaves the level for the level select (or the level editor: see <paramref name="leaveText"/>).</param>
        /// <param name="leaveText">The button of <paramref name="leave"/>.</param>
        public ResultsScreen(float? previousBest, Action nextLevel, Action restart, Action leave, string leaveText = "Level select")
            : base("Game over")
        {
            IsPopup = true;
            ItemsInRow = true;
            FadeInTime = TimeSpan.FromSeconds(0.4);
            FadeOutTime = TimeSpan.FromSeconds(0.25);

            lines = new[] { "Fell off the track" };
            if (previousBest.HasValue)
                note = "Best " + RaceScore.FormatTime(previousBest.Value);
            AddButtons(nextLevel, restart, leave, leaveText);
        }

        private void AddButtons(Action nextLevel, Action restart, Action leave, string leaveText)
        {
            this.leave = leave;
            //From left to right: back, again, on
            AddItem(new MenuItem { Text = () => leaveText, Activate = () => Leave(leave) });
            AddItem(new MenuItem { Text = () => "Restart", Activate = () => Leave(restart) });
            if (nextLevel != null)
                AddItem(new MenuItem { Text = () => "Next level", Activate = () => Leave(nextLevel) });
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            age += (float)gameTime.ElapsedGameTime.TotalSeconds;
            while (starsRung < stars && age >= FirstStarTime + starsRung * StarInterval)
                Manager.Sounds.Play(Sound.Star, StarPitches[Math.Min(starsRung++, StarPitches.Length - 1)]);
            if (record && !recordRung && age >= FirstStarTime + stars * StarInterval + RecordDelay)
            {
                Manager.Sounds.Play(Sound.NewRecord);
                recordRung = true;
            }
        }

        private void Leave(Action action)
        {
            Close();
            action();
        }

        public override void HandleInput(InputState input, GameTime gameTime)
        {
            if (input.BackPressed)
            {
                Manager.Sounds.Play(Sound.MenuBack);
                Leave(leave);
            }
            else
                base.HandleInput(input, gameTime);
        }

        private float LineHeight => Manager.Fonts.Menu.MeasureString("M").Y * TextScale * Manager.UiScale * LineSpacing;

        protected override float ContentHeight => (StarSize * Manager.UiScale + LineHeight * (lines.Length + 1)) * 1.1f;

        protected override void DrawBackdrop()
        {
            Manager.SpriteBatch.Draw(Manager.Pixel, Manager.Viewport.Bounds, Color.Black * (Dimming * Visibility));
        }

        protected override void DrawContent(float top, float slide, float alpha)
        {
            GameFont font = Manager.Fonts.Menu;
            float uiScale = Manager.UiScale;
            float width = Manager.Viewport.Width;

            float starSize = StarSize * uiScale;
            StarIcons.Draw(Manager.SpriteBatch, new Vector2((width - StarIcons.Width(starSize)) / 2f + slide, top), starSize, stars, alpha);

            float y = top + starSize * 1.15f;
            foreach (string line in lines)
            {
                DrawLine(font, line, y, slide, Color.White * alpha, alpha);
                y += LineHeight;
            }
            if (note != null)
                DrawLine(font, note, y, slide, (noteHighlighted ? MainMenuScreen.Yellow : Color.LightGray) * alpha, alpha);
        }

        private void DrawLine(GameFont font, string text, float y, float slide, Color color, float alpha)
        {
            float scale = TextScale * Manager.UiScale;
            float textWidth = font.MeasureString(text).X * scale;
            Manager.DrawShadowedText(font, text, new Vector2((Manager.Viewport.Width - textWidth) / 2f + slide, y), color, Color.DarkSlateGray * (alpha * alpha), scale);
        }
    }
}
