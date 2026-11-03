using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TW3D.GamePlay;

namespace TW3D.Editor
{
    /// <summary>
    /// What the Level window and the status bar tell about the edited level besides the level's own properties: the
    /// problems found, the gaps of the road apart from them, the estimated par time, the counts of the blocks, the objects
    /// and the props of the scenery, the cells the level spans, and the texts of all that. The document is asked again (the problems, the
    /// estimate that writes the level out) only when its <see cref="EditedLevel.Version"/> or file changes, not in every
    /// frame. Without MonoGame, so that it is tested.
    /// </summary>
    sealed class LevelReport
    {
        //A new level that has no file yet
        private const string NewLevelName = "new level";
        /// <summary>The road gaps are listed folded away from this many on.</summary>
        public const int FoldedGaps = 10;

        private EditedLevel level;
        private int version = -1;
        private string file;

        /// <summary>The problems that stop the level from being played, in the order of <see cref="EditedLevel.Problems"/>.</summary>
        public List<LevelProblem> Problems { get; } = new List<LevelProblem>();

        /// <summary>The gaps of the road, which may be left on purpose.</summary>
        public List<LevelProblem> Gaps { get; } = new List<LevelProblem>();

        /// <summary>The document the report is of.</summary>
        public EditedLevel Level => level;

        /// <summary>The par time in seconds the game takes when the level has none (the par time itself when it has one).</summary>
        public float Estimate { get; private set; }

        /// <summary>The level has at least one block.</summary>
        public bool HasCells { get; private set; }

        /// <summary>The lowest and the highest column and row of the cells with a block in any layer; meaningful with <see cref="HasCells"/>.</summary>
        public Cell Min { get; private set; }

        /// <summary>The highest column and row; see <see cref="Min"/>.</summary>
        public Cell Max { get; private set; }

        /// <summary>The file name without the extension, or "new level".</summary>
        public string Name { get; private set; }

        /// <summary>"estimated: 12.5 s".</summary>
        public string EstimateText { get; private set; }

        /// <summary>"Blocks: 107 middle, 75 back, 92 front".</summary>
        public string BlocksText { get; private set; }

        /// <summary>"Objects: 18".</summary>
        public string ObjectsText { get; private set; }

        /// <summary>"Scenery: 4 props", "Scenery: 1 prop".</summary>
        public string SceneryText { get; private set; }

        /// <summary>"Cells: [-3, -2] to [40, 5]" or "Cells: none".</summary>
        public string CellsText { get; private set; }

        /// <summary>"Road gaps (12)": the label of the header; its id stays the same as the number changes.</summary>
        public string GapsHeader { get; private set; }

        /// <summary>The status bar's "2 problems" or "1 problem"; null for none.</summary>
        public string ProblemsText { get; private set; }

        /// <summary>The status bar's "12 gaps" or "1 gap"; null for none.</summary>
        public string GapsText { get; private set; }

        /// <summary>Brings the report up to date with a document: nothing is done while it is the one, in the version, of the last call.</summary>
        public void Update(EditedLevel current)
        {
            if (current == level && current.Version == version && current.File == file)
                return;
            level = current;
            version = current.Version;
            file = current.File;
            Name = file != null ? Path.GetFileNameWithoutExtension(file) : NewLevelName;

            Problems.Clear();
            Gaps.Clear();
            foreach (LevelProblem problem in current.Problems())
                (problem.IsRoadGap ? Gaps : Problems).Add(problem);
            ProblemsText = Count(Problems.Count, "problem");
            GapsText = Count(Gaps.Count, "gap");
            GapsHeader = $"Road gaps ({Gaps.Count})###gaps";

            //A par time the level has is the estimate (as RaceScore says); writing the level out is for the other case only
            Estimate = current.Par ?? RaceScore.Par(current.ToData());
            EstimateText = "estimated: " + Estimate.ToString("0.0", CultureInfo.InvariantCulture) + " s";

            BlocksText = $"Blocks: {current.Blocks(Layer.Middle).Count} middle, {current.Blocks(Layer.Back).Count} back, {current.Blocks(Layer.Front).Count} front";
            ObjectsText = "Objects: " + current.Objects.Count;
            SceneryText = "Scenery: " + current.Scenery.Count + (current.Scenery.Count == 1 ? " prop" : " props");
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            HasCells = false;
            foreach (Layer layer in Enum.GetValues<Layer>())
            {
                foreach (PlacedBlock block in current.Blocks(layer))
                {
                    minX = Math.Min(minX, block.Cell.X);
                    minY = Math.Min(minY, block.Cell.Y);
                    maxX = Math.Max(maxX, block.Cell.X);
                    maxY = Math.Max(maxY, block.Cell.Y);
                    HasCells = true;
                }
            }
            Min = HasCells ? new Cell(minX, minY) : default;
            Max = HasCells ? new Cell(maxX, maxY) : default;
            CellsText = HasCells ? $"Cells: {Min} to {Max}" : "Cells: none";
        }

        /// <summary>
        /// What the status bar says after "Opened x.json" or "Saved x.json" about the problems found and the gaps of the road,
        /// counted apart: "1 problem: the first one's text; 3 road gaps", "12 road gaps"; null when there are none.
        /// </summary>
        /// <param name="found">What <see cref="EditedLevel.Problems"/> gives, the problems first and the gaps of the road after them.</param>
        public static string Summary(List<LevelProblem> found)
        {
            LevelProblem first = null;
            int problems = 0, gaps = 0;
            foreach (LevelProblem problem in found)
            {
                if (problem.IsRoadGap)
                    gaps++;
                else if (problems++ == 0)
                    first = problem;
            }
            string text = problems > 0 ? Count(problems, "problem") + ": " + first : null;
            if (gaps > 0)
                text = (text != null ? text + "; " : "") + Count(gaps, "road gap");
            return text;
        }

        /// <summary>"3 problems", "1 problem"; null for none.</summary>
        private static string Count(int count, string noun)
        {
            return count == 0 ? null : count + " " + noun + (count == 1 ? "" : "s");
        }
    }
}
