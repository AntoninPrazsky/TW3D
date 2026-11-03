using System;
using System.Collections.Generic;
using TW3D.GamePlay;
using TW3D.Shared;

namespace TW3D.Editor
{
    /// <summary>
    /// The start of a test drive (F5: from the start cell, Shift+F5: from the cursor cell): the level being edited
    /// checked and handed, in memory, to a <see cref="GameplayScreen"/> with the car of the garage. Nothing is saved.
    /// </summary>
    static class TestDriveStart
    {
        /// <summary>
        /// The screen of the game that drives the level from a cell; null for a level the game cannot build. The note
        /// says why it was refused, or what would spoil the drive (no finish, no block under the car), for the status
        /// bar, where it is still read when the editor is back.
        /// </summary>
        /// <param name="ended">Called when the drive closes, or is closed.</param>
        public static GameplayScreen Create(EditedLevel level, Catalog catalog, Cell start, GameSettings settings, Action ended, out string note)
        {
            LevelData data = level.ToData();
            List<string> problems = data.Validate(catalog);
            if (problems.Count > 0)
            {
                note = "The level cannot be driven: " + problems[0];
                return null;
            }
            List<string> warnings = new List<string>();
            if (data.FinishBlock == null)
                warnings.Add("A test drive without a finish: the run has no end.");
            if (level.BlockAt(Layer.Middle, start) == null)
                warnings.Add($"There is no block at {start}, where the car starts: it falls.");
            note = warnings.Count > 0 ? string.Join(" ", warnings) : $"Test drive from {start}.";
            return GameplayScreen.TestDrive(data, start.X, start.Y, settings, ended);
        }
    }
}
