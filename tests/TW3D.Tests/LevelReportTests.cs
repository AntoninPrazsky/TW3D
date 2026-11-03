using System.Collections.Generic;
using System.Linq;
using TW3D.Editor;
using TW3D.GamePlay;
using TW3D.Shared;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>
    /// What the Level window and the status bar of the editor tell about the level (Editor/LevelReport): the problems and
    /// the gaps of the road apart, the estimated par time, the counts and the cells, and that it is brought up to date
    /// only when the document changes.
    /// </summary>
    public class LevelReportTests
    {
        private static readonly Catalog Catalog = Repository.LoadCatalog();

        private static EditedLevel Load(string level)
        {
            string path = Repository.LevelPath(level);
            return new EditedLevel(Catalog, LevelFile.Load(path), path);
        }

        [Fact]
        public void ReportsALevelOfTheGameWithItsGapsApartFromItsProblems()
        {
            EditedLevel level = Load("lock");
            LevelReport report = new LevelReport();
            report.Update(level);

            Assert.Same(level, report.Level);
            Assert.Equal("lock", report.Name);
            Assert.Empty(report.Problems);
            Assert.NotEmpty(report.Gaps);
            Assert.All(report.Gaps, gap => Assert.True(gap.IsRoadGap));
            Assert.Null(report.ProblemsText);
            Assert.Equal(report.Gaps.Count + (report.Gaps.Count == 1 ? " gap" : " gaps"), report.GapsText);
            Assert.Equal($"Road gaps ({report.Gaps.Count})###gaps", report.GapsHeader);
            Assert.Equal($"Blocks: {level.Blocks(Layer.Middle).Count} middle, {level.Blocks(Layer.Back).Count} back, {level.Blocks(Layer.Front).Count} front",
                report.BlocksText);
            Assert.Equal("Objects: " + level.Objects.Count, report.ObjectsText);
            Assert.Equal("Scenery: 0 props", report.SceneryText);
            Assert.True(report.HasCells);
            Assert.StartsWith("Cells: [", report.CellsText);
        }

        [Fact]
        public void AFreshLevelHasOneBlockOneProblemAndTheLeastEstimate()
        {
            LevelReport report = new LevelReport();
            report.Update(EditedLevel.New(Catalog));

            Assert.Equal("new level", report.Name);
            LevelProblem problem = Assert.Single(report.Problems);
            Assert.Equal("The level has no finish.", problem.Message);
            Assert.False(problem.IsRoadGap);
            Assert.Equal("1 problem", report.ProblemsText);
            Assert.Null(report.GapsText);
            Assert.Equal("Road gaps (0)###gaps", report.GapsHeader);
            Assert.Equal("estimated: 10.0 s", report.EstimateText);
            Assert.Equal("Blocks: 1 middle, 0 back, 0 front", report.BlocksText);
            Assert.Equal("Objects: 0", report.ObjectsText);
            Assert.Equal("Scenery: 0 props", report.SceneryText);
            Assert.Equal("Cells: [0, 0] to [0, 0]", report.CellsText);
        }

        [Fact]
        public void FollowsTheDocumentThroughChangesAndUndo()
        {
            EditedLevel level = EditedLevel.New(Catalog);
            LevelReport report = new LevelReport();
            report.Update(level);

            Assert.Null(level.Place(Layer.Middle, new Cell(1, 0), "a1", 0));
            Assert.Null(level.Place(Layer.Back, new Cell(-2, 3), "a1", 0));
            Assert.Null(level.SetFinish(Layer.Middle, new Cell(1, 0)));
            report.Update(level);
            Assert.Empty(report.Problems);
            Assert.Null(report.ProblemsText);
            Assert.Equal("Blocks: 2 middle, 1 back, 0 front", report.BlocksText);
            Assert.Equal("Cells: [-2, 0] to [1, 3]", report.CellsText);

            level.SetPar(42.5f);
            report.Update(level);
            Assert.Equal(42.5f, report.Estimate);

            level.Undo();
            level.Undo();
            level.Undo();
            level.Undo();
            report.Update(level);
            Assert.Equal("1 problem", report.ProblemsText);
            Assert.Equal("Blocks: 1 middle, 0 back, 0 front", report.BlocksText);
        }

        [Fact]
        public void TheEstimateIsTheGamesOwnForALevelWithoutAPar()
        {
            EditedLevel level = Load("lock");
            LevelReport report = new LevelReport();
            report.Update(level);
            Assert.Null(level.Par);
            Assert.Equal(RaceScore.Par(level.ToData()), report.Estimate);
        }

        [Fact]
        public void IsRecomputedOnlyWhenTheDocumentChanges()
        {
            EditedLevel level = EditedLevel.New(Catalog);
            LevelReport report = new LevelReport();
            report.Update(level);
            string text = report.CellsText;
            LevelProblem first = report.Problems[0];

            //The same document in the same version: nothing is made again
            report.Update(level);
            Assert.Same(text, report.CellsText);
            Assert.Same(first, report.Problems[0]);

            level.SetMood(LevelMood.Night);
            report.Update(level);
            Assert.NotSame(first, report.Problems[0]);

            //Another document, though in the same version
            EditedLevel other = EditedLevel.New(Catalog);
            other.SetMood(LevelMood.Night);
            Assert.Equal(level.Version, other.Version);
            LevelProblem before = report.Problems[0];
            report.Update(other);
            Assert.Same(other, report.Level);
            Assert.NotSame(before, report.Problems[0]);
        }

        [Fact]
        public void SummarisesTheProblemsAndTheGapsApartForTheStatusBar()
        {
            //Both of the kinds a level can have: the first problem's text is kept, the gaps are counted after it
            List<LevelProblem> found = Load("lock").Problems();
            int gaps = found.Count(problem => problem.IsRoadGap);
            Assert.True(gaps > 1);
            Assert.Equal(gaps + " road gaps", LevelReport.Summary(found));

            LevelProblem problem = new LevelProblem("The level has no finish.");
            LevelProblem gap = new LevelProblem("Gap.") { IsRoadGap = true };
            Assert.Equal("1 problem: The level has no finish.", LevelReport.Summary(new List<LevelProblem> { problem }));
            Assert.Equal("2 problems: The level has no finish.; 1 road gap",
                LevelReport.Summary(new List<LevelProblem> { problem, new LevelProblem("Another."), gap }));
            Assert.Null(LevelReport.Summary(new List<LevelProblem>()));
        }

        [Fact]
        public void ALevelWithoutBlocksHasNoCells()
        {
            EditedLevel level = EditedLevel.New(Catalog);
            Assert.True(level.Erase(Layer.Middle, new Cell(0, 0)));
            LevelReport report = new LevelReport();
            report.Update(level);
            Assert.False(report.HasCells);
            Assert.Equal("Cells: none", report.CellsText);
        }
    }
}
