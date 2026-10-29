using System;
using System.Linq;
using System.Globalization;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace TW3D.ScreenSystem
{
    /// <summary>
    /// Displays the FPS
    /// </summary>
    public class FrameRateCounter : DrawableGameComponent
    {
        private TimeSpan _elapsedTime = TimeSpan.Zero;
        private NumberFormatInfo _format;
        private int _frameCounter;
        private int _frameRate;
        private Vector2 _position;
        private ScreenManager _screenManager;

        public List<float> avgFPS { set; get; } //added

        public FrameRateCounter(ScreenManager screenManager)
            : base(screenManager.Game)
        {
            _screenManager = screenManager;
            _format = new NumberFormatInfo();
            _format.NumberDecimalSeparator = ".";
#if XBOX
            _position = new Vector2(55, 35);
#else
            _position = new Vector2(30, 25);
#endif
            avgFPS = new List<float>(); //added

        }

        public override void Update(GameTime gameTime)
        {
            _elapsedTime += gameTime.ElapsedGameTime;

            if (_elapsedTime <= TimeSpan.FromSeconds(1)) return;

            _elapsedTime -= TimeSpan.FromSeconds(1);
            _frameRate = _frameCounter;
            _frameCounter = 0;

            if (avgFPS.Count > 10000) avgFPS.Clear(); //added

        }

        public override void Draw(GameTime gameTime)
        {
            _frameCounter++;

            avgFPS.Add(_frameRate);
            string fps = string.Format(_format, "{0} fps\n{1}", _frameRate, avgFPS.Average()); //added second line
            _screenManager.SpriteBatch.Begin();
            _screenManager.SpriteBatch.DrawString(_screenManager.Fonts.FrameRateCounterFont, fps, _position + Vector2.One, Color.Black);
            _screenManager.SpriteBatch.DrawString(_screenManager.Fonts.FrameRateCounterFont, fps, _position, Color.White);
            _screenManager.SpriteBatch.End();
        }
    }
}