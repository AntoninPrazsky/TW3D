using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TW3D.Editor;
using TW3D.Shared;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>
    /// The document of the level editor (Editor/EditedLevel): loading and saving the shipped levels unchanged, the
    /// commands with undo and redo, what they refuse, the compound steps, the dirty flag and the problems found.
    /// </summary>
    public class EditedLevelTests
    {
        //A block with a collision shape, one without (decoration), and a cell no shipped level reaches
        private const string Road = "a1";
        private const string Decoration = "kea1a";
        private static readonly Cell FarAway = new Cell(1000, 1000);
        private static readonly Cell Start = new Cell(0, 0);

        private static readonly Catalog Catalog = Repository.LoadCatalog();

        public static IEnumerable<object[]> ShippedLevels => Repository.LevelNames().Select(name => new object[] { name });

        private static string FileText(string level) => File.ReadAllText(Repository.LevelPath(level)).Replace("\r\n", "\n");

        private static EditedLevel Load(string level)
        {
            string path = Repository.LevelPath(level);
            return new EditedLevel(Catalog, LevelFile.Load(path), path);
        }

        /// <summary>A new level with a second road block beside the start, for the commands to work on.</summary>
        private static EditedLevel Fresh()
        {
            EditedLevel level = EditedLevel.New(Catalog);
            Assert.Null(level.Place(Layer.Middle, new Cell(1, 0), Road, 0));
            return level;
        }

        private static string Json(EditedLevel level) => LevelFile.ToJson(level.ToData());

        [Theory]
        [MemberData(nameof(ShippedLevels))]
        public void SavesAShippedLevelAsItWasRead(string level)
        {
            EditedLevel edited = Load(level);
            Assert.Equal(FileText(level), Json(edited));
            Assert.False(edited.IsDirty);
            Assert.Equal(Repository.LevelPath(level), edited.File);
            Assert.Empty(edited.Problems());
        }

        [Theory]
        [MemberData(nameof(ShippedLevels))]
        public void UndoingAllChangesToAShippedLevelGivesTheFileBack(string level)
        {
            EditedLevel edited = Load(level);
            //The cells of the first and the last block, picked before any command changes the list
            IReadOnlyList<PlacedBlock> middle = edited.Blocks(Layer.Middle);
            Cell erased = middle[0].Cell;
            Cell shifted = middle[middle.Count - 1].Cell;
            Assert.True(edited.Erase(Layer.Middle, erased));
            Assert.Null(edited.Place(Layer.Middle, FarAway, Road, 0));
            Assert.True(edited.Shift(Layer.Middle, shifted, 1));
            Assert.True(edited.IsDirty);
            Assert.NotEqual(FileText(level), Json(edited));

            edited.Undo();
            edited.Undo();
            edited.Undo();
            Assert.Equal(FileText(level), Json(edited));
            Assert.False(edited.IsDirty);
            Assert.False(edited.CanUndo);

            edited.Redo();
            edited.Redo();
            edited.Redo();
            Assert.True(edited.IsDirty);
            Assert.False(edited.CanRedo);
            Assert.NotEqual(FileText(level), Json(edited));
        }

        [Fact]
        public void PlaceAppendsABlockAndUndoTakesItAway()
        {
            EditedLevel level = Fresh();
            int version = level.Version;
            Assert.Null(level.Place(Layer.Middle, new Cell(2, 0), "c1", -4));
            Assert.Equal(version + 1, level.Version);
            PlacedBlock placed = level.BlockAt(Layer.Middle, new Cell(2, 0));
            Assert.Equal("c1", placed.Name);
            Assert.Equal(-4, placed.Shift);
            Assert.Same(placed, level.Blocks(Layer.Middle).Last());
            Assert.Equal("Place c1", level.UndoName);
            Assert.Null(level.RedoName);

            level.Undo();
            Assert.Equal(version + 2, level.Version);
            Assert.Null(level.BlockAt(Layer.Middle, new Cell(2, 0)));
            Assert.Equal("Place c1", level.RedoName);
            level.Redo();
            Assert.Equal(version + 3, level.Version);
            Assert.Equal("c1", level.BlockAt(Layer.Middle, new Cell(2, 0)).Name);
        }

        [Fact]
        public void PlaceOverABlockReplacesItInItsPlaceAndKeepsTheFinish()
        {
            EditedLevel level = Fresh();
            Assert.Null(level.Place(Layer.Middle, new Cell(2, 0), Road, 0));
            Assert.Null(level.SetFinish(Layer.Middle, new Cell(1, 0)));
            Assert.Null(level.Place(Layer.Middle, new Cell(1, 0), "c1", -4));

            PlacedBlock replaced = level.Blocks(Layer.Middle)[1];
            Assert.Equal(new Cell(1, 0), replaced.Cell);
            Assert.Equal("c1", replaced.Name);
            Assert.Equal(-4, replaced.Shift);
            Assert.True(replaced.Finish);
            Assert.Equal(3, level.Blocks(Layer.Middle).Count);

            level.Undo();
            Assert.Equal(Road, level.Blocks(Layer.Middle)[1].Name);
            Assert.Equal(0, level.Blocks(Layer.Middle)[1].Shift);
            Assert.True(level.Blocks(Layer.Middle)[1].Finish);
        }

        [Fact]
        public void PlacingTheSameBlockAgainRecordsNothing()
        {
            EditedLevel level = Fresh();
            int version = level.Version;
            Assert.Null(level.Place(Layer.Middle, new Cell(1, 0), Road, 0));
            Assert.Equal(version, level.Version);
            Assert.Equal("Place a1", level.UndoName);
        }

        [Fact]
        public void UndoPutsAnErasedBlockBackInItsPlace()
        {
            EditedLevel level = Fresh();
            Assert.Null(level.Place(Layer.Middle, new Cell(2, 0), Road, 0));
            int version = level.Version;
            Assert.True(level.Erase(Layer.Middle, new Cell(1, 0)));
            Assert.Equal(version + 1, level.Version);
            Assert.Null(level.BlockAt(Layer.Middle, new Cell(1, 0)));
            Assert.Equal(2, level.Blocks(Layer.Middle).Count);
            Assert.Equal("Erase", level.UndoName);

            level.Undo();
            Assert.Equal(new Cell(1, 0), level.Blocks(Layer.Middle)[1].Cell);
            Assert.Equal("Erase", level.RedoName);
            level.Redo();
            Assert.Null(level.BlockAt(Layer.Middle, new Cell(1, 0)));
            Assert.Equal(version + 3, level.Version);
        }

        /// <summary>A level with a base and a gate on it in one cell, as the shipped levels have: "sb" then "sg" at [2, 0].</summary>
        private static EditedLevel WithTwoBlocksInOneCell()
        {
            LevelData data = new LevelData();
            data.Middle.Add(new BlockPlacement { Block = Road, X = 0, Y = 0 });
            data.Middle.Add(new BlockPlacement { Block = "sb", X = 2, Y = 0 });
            data.Middle.Add(new BlockPlacement { Block = "sg", X = 2, Y = 0 });
            return new EditedLevel(Catalog, data, null);
        }

        [Fact]
        public void ACellWithTwoBlocksIsErasedOneBlockAtATimeFromTheLast()
        {
            EditedLevel level = WithTwoBlocksInOneCell();
            string original = Json(level);
            Cell cell = new Cell(2, 0);
            PlacedBlock gate = level.BlockAt(Layer.Middle, cell);
            Assert.Equal("sg", gate.Name);
            Assert.Same(level.Blocks(Layer.Middle)[2], gate);

            Assert.True(level.Erase(Layer.Middle, cell));
            Assert.Equal("sb", level.BlockAt(Layer.Middle, cell).Name);
            Assert.True(level.Erase(Layer.Middle, cell));
            Assert.Null(level.BlockAt(Layer.Middle, cell));

            level.Undo();
            level.Undo();
            Assert.Equal(new[] { Road, "sb", "sg" }, level.Blocks(Layer.Middle).Select(block => block.Name));
            Assert.Equal(original, Json(level));
            Assert.False(level.IsDirty);
        }

        [Fact]
        public void EraseAndShiftByBlockReachTheBaseUnderAGate()
        {
            EditedLevel level = WithTwoBlocksInOneCell();
            string original = Json(level);
            PlacedBlock baseBlock = level.Blocks(Layer.Middle)[1];
            PlacedBlock gate = level.Blocks(Layer.Middle)[2];
            Assert.Equal("sb", baseBlock.Name);

            Assert.True(level.Shift(Layer.Middle, baseBlock, 3));
            Assert.Equal(3, baseBlock.Shift);
            Assert.Equal(0, gate.Shift);
            Assert.True(level.Erase(Layer.Middle, baseBlock));
            Assert.Same(gate, level.BlockAt(Layer.Middle, new Cell(2, 0)));
            Assert.Equal(2, level.Blocks(Layer.Middle).Count);
            //The erased block is not in the layer any more, and never was in the back one
            Assert.False(level.Erase(Layer.Middle, baseBlock));
            Assert.False(level.Shift(Layer.Back, gate, 1));

            level.Undo();
            Assert.Same(baseBlock, level.Blocks(Layer.Middle)[1]);
            level.Undo();
            Assert.Equal(original, Json(level));
        }

        [Fact]
        public void ErasingAnEmptyCellRecordsNothing()
        {
            EditedLevel level = Fresh();
            int version = level.Version;
            Assert.False(level.Erase(Layer.Middle, FarAway));
            Assert.False(level.Erase(Layer.Back, Start));
            Assert.Equal(version, level.Version);
            Assert.Equal("Place a1", level.UndoName);
        }

        [Fact]
        public void ShiftAddsStepsAndUndoTakesThemBack()
        {
            EditedLevel level = Fresh();
            int version = level.Version;
            Assert.True(level.Shift(Layer.Middle, Start, 2));
            Assert.True(level.Shift(Layer.Middle, Start, -5));
            Assert.Equal(-3, level.BlockAt(Layer.Middle, Start).Shift);
            Assert.Equal(version + 2, level.Version);
            Assert.Equal("Shift", level.UndoName);

            level.Undo();
            Assert.Equal(2, level.BlockAt(Layer.Middle, Start).Shift);
            level.Undo();
            Assert.Equal(0, level.BlockAt(Layer.Middle, Start).Shift);
            Assert.Equal("Shift", level.RedoName);
            level.Redo();
            Assert.Equal(2, level.BlockAt(Layer.Middle, Start).Shift);
            Assert.False(level.Shift(Layer.Middle, FarAway, 1));
        }

        [Fact]
        public void TheFinishMovesToTheBlockSetAndTogglesOff()
        {
            EditedLevel level = Fresh();
            Assert.Null(level.Finish);
            int version = level.Version;
            Assert.Null(level.SetFinish(Layer.Middle, Start));
            Assert.Same(level.BlockAt(Layer.Middle, Start), level.Finish);
            Assert.Equal(version + 1, level.Version);
            Assert.Equal("Finish", level.UndoName);

            Assert.Null(level.SetFinish(Layer.Middle, new Cell(1, 0)));
            Assert.Same(level.BlockAt(Layer.Middle, new Cell(1, 0)), level.Finish);
            Assert.False(level.BlockAt(Layer.Middle, Start).Finish);
            Assert.Single(level.ToData().Middle, block => block.Finish);

            Assert.Null(level.SetFinish(Layer.Middle, new Cell(1, 0)));
            Assert.Null(level.Finish);

            level.Undo();
            Assert.Same(level.BlockAt(Layer.Middle, new Cell(1, 0)), level.Finish);
            level.Undo();
            Assert.Same(level.BlockAt(Layer.Middle, Start), level.Finish);
            Assert.False(level.BlockAt(Layer.Middle, new Cell(1, 0)).Finish);
            level.Undo();
            Assert.Null(level.Finish);
            Assert.Equal("Finish", level.RedoName);
            level.Redo();
            Assert.Same(level.BlockAt(Layer.Middle, Start), level.Finish);
        }

        [Fact]
        public void SetFinishMakesTheBlockTheOnlyFinishOfAFileWithTwo()
        {
            //An invalid file the author has to be able to repair with the finish command
            LevelData data = new LevelData();
            data.Middle.Add(new BlockPlacement { Block = Road, X = 0, Y = 0, Finish = true });
            data.Middle.Add(new BlockPlacement { Block = Road, X = 1, Y = 0, Finish = true });
            data.Middle.Add(new BlockPlacement { Block = Road, X = 2, Y = 0 });
            EditedLevel level = new EditedLevel(Catalog, data, null);
            string original = Json(level);
            Assert.Contains(level.Problems(), problem => problem.Message == "The level has more than one finish.");

            Assert.Null(level.SetFinish(Layer.Middle, new Cell(2, 0)));
            Assert.Equal(new[] { false, false, true }, level.Blocks(Layer.Middle).Select(block => block.Finish));
            level.Undo();
            Assert.Equal(new[] { true, true, false }, level.Blocks(Layer.Middle).Select(block => block.Finish));
            Assert.Equal(original, Json(level));
            Assert.False(level.IsDirty);

            //One of the two finishes: it stays the finish, alone
            Assert.Null(level.SetFinish(Layer.Middle, new Cell(1, 0)));
            Assert.Equal(new[] { false, true, false }, level.Blocks(Layer.Middle).Select(block => block.Finish));
            level.Undo();
            Assert.Equal(original, Json(level));
            level.Redo();
            Assert.Equal(new[] { false, true, false }, level.Blocks(Layer.Middle).Select(block => block.Finish));
        }

        [Fact]
        public void TheFinishIsRefusedOutsideTheMiddleLayerAndInAnEmptyCell()
        {
            EditedLevel level = Fresh();
            Assert.Null(level.Place(Layer.Back, Start, Decoration, 0));
            int version = level.Version;
            Assert.NotNull(level.SetFinish(Layer.Back, Start));
            Assert.NotNull(level.SetFinish(Layer.Middle, FarAway));
            Assert.Null(level.Finish);
            Assert.Equal(version, level.Version);
        }

        [Fact]
        public void DecorationIsRefusedInTheMiddleLayerOnly()
        {
            EditedLevel level = Fresh();
            int version = level.Version;
            string reason = level.Place(Layer.Middle, FarAway, Decoration, 0);
            Assert.Contains(Decoration, reason);
            Assert.Null(level.BlockAt(Layer.Middle, FarAway));
            Assert.Equal(version, level.Version);
            Assert.Null(level.Place(Layer.Back, FarAway, Decoration, 0));
            Assert.Null(level.Place(Layer.Front, FarAway, Decoration, 0));
        }

        [Fact]
        public void AnUnknownBlockIsRefused()
        {
            EditedLevel level = Fresh();
            int version = level.Version;
            Assert.NotNull(level.Place(Layer.Back, FarAway, "no-such-block", 0));
            Assert.Null(level.BlockAt(Layer.Back, FarAway));
            Assert.Equal(version, level.Version);
        }

        [Fact]
        public void AnUnknownObjectTypeOrModelIsRefused()
        {
            EditedLevel level = Fresh();
            int version = level.Version;
            Assert.NotNull(level.AddObject("NoSuchType", "box", 0f, -30f));
            Assert.NotNull(level.AddObject("Box", "bar1", 0f, -30f));
            Assert.Empty(level.Objects);
            Assert.Equal(version, level.Version);
        }

        [Fact]
        public void ObjectsAreAddedMovedAndRemovedWithUndo()
        {
            EditedLevel level = Fresh();
            Assert.Null(level.AddObject("Bar", "bar1", 10f, -30f));
            Assert.Null(level.AddObject("Box", "box", 20f, -30f));
            Assert.Equal("Add Box", level.UndoName);
            PlacedObject bar = level.Objects[0];
            PlacedObject box = level.Objects[1];

            level.MoveObject(bar, 12f, -31f);
            Assert.Equal(12f, bar.X);
            Assert.Equal(-31f, bar.Y);
            int version = level.Version;
            level.MoveObject(bar, 12f, -31f);
            Assert.Equal(version, level.Version);

            level.RemoveObject(bar);
            Assert.Equal(new[] { box }, level.Objects);
            level.Undo();
            Assert.Equal(new[] { bar, box }, level.Objects);
            level.Undo();
            Assert.Equal(10f, bar.X);
            Assert.Equal(-30f, bar.Y);
            level.Undo();
            Assert.Equal(new[] { bar }, level.Objects);
            level.Redo();
            Assert.Equal(new[] { bar, box }, level.Objects);
        }

        [Fact]
        public void ACompoundIsUndoneAtOnce()
        {
            EditedLevel level = Fresh();
            using (level.BeginCompound("Stroke"))
            {
                for (int x = 2; x < 6; x++)
                    Assert.Null(level.Place(Layer.Middle, new Cell(x, 0), Road, 0));
                Assert.NotNull(level.Place(Layer.Middle, new Cell(6, 0), Decoration, 0));
                Assert.Null(level.Place(Layer.Middle, new Cell(7, 0), Road, 0));
                Assert.False(level.CanUndo);
            }
            Assert.Equal(7, level.Blocks(Layer.Middle).Count);
            Assert.Equal("Stroke", level.UndoName);

            level.Undo();
            Assert.Equal(2, level.Blocks(Layer.Middle).Count);
            Assert.Equal("Place a1", level.UndoName);
            Assert.Equal("Stroke", level.RedoName);
            level.Redo();
            Assert.Equal(7, level.Blocks(Layer.Middle).Count);
        }

        [Fact]
        public void AnEmptyCompoundRecordsNothing()
        {
            EditedLevel level = Fresh();
            level.Undo();
            Assert.True(level.CanRedo);
            int version = level.Version;
            using (level.BeginCompound("Nothing"))
            {
                Assert.False(level.Erase(Layer.Middle, FarAway));
            }
            Assert.Equal(version, level.Version);
            Assert.False(level.CanUndo);
            Assert.True(level.CanRedo);
            Assert.Equal("Place a1", level.RedoName);
        }

        [Fact]
        public void NestedCompoundsAreOneStep()
        {
            EditedLevel level = Fresh();
            using (level.BeginCompound("Outer"))
            {
                Assert.Null(level.Place(Layer.Middle, new Cell(2, 0), Road, 0));
                using (level.BeginCompound("Inner"))
                {
                    Assert.Null(level.Place(Layer.Middle, new Cell(3, 0), Road, 0));
                    level.SetMood(LevelMood.Night);
                }
                Assert.False(level.CanUndo);
                Assert.Null(level.Place(Layer.Middle, new Cell(4, 0), Road, 0));
            }
            Assert.Equal("Outer", level.UndoName);
            level.Undo();
            Assert.Equal(2, level.Blocks(Layer.Middle).Count);
            Assert.Equal(LevelMood.Day, level.Mood);
            Assert.Equal("Place a1", level.UndoName);
        }

        [Fact]
        public void ANewCommandAfterUndoDiscardsTheRedo()
        {
            EditedLevel level = Fresh();
            Assert.Null(level.Place(Layer.Middle, new Cell(2, 0), Road, 0));
            level.Undo();
            Assert.True(level.CanRedo);
            Assert.Null(level.Place(Layer.Middle, new Cell(3, 0), Road, 0));
            Assert.False(level.CanRedo);
            Assert.Null(level.RedoName);
            Assert.Null(level.BlockAt(Layer.Middle, new Cell(2, 0)));
        }

        [Fact]
        public void SettingTheSameMoodOrderOrParRecordsNothing()
        {
            EditedLevel level = Fresh();
            int version = level.Version;
            level.SetMood(LevelMood.Day);
            level.SetOrder(null);
            level.SetPar(null);
            Assert.Equal(version, level.Version);
            Assert.Equal("Place a1", level.UndoName);

            level.SetMood(LevelMood.Sunset);
            level.SetOrder(7);
            level.SetPar(12.5f);
            Assert.Equal(version + 3, level.Version);
            Assert.Equal("Par", level.UndoName);
            LevelData data = level.ToData();
            Assert.Equal(LevelMood.Sunset, data.Mood);
            Assert.Equal(7, data.Order);
            Assert.Equal(12.5f, data.Par);
            level.Undo();
            Assert.Equal("Order", level.UndoName);
            level.Undo();
            Assert.Equal("Mood", level.UndoName);
            level.Undo();
            Assert.Equal(LevelMood.Day, level.Mood);
            Assert.Null(level.Order);
            Assert.Null(level.Par);
        }

        [Fact]
        public void TheLevelIsCleanAtTheSavedPointOfTheHistory()
        {
            EditedLevel level = EditedLevel.New(Catalog);
            Assert.False(level.IsDirty);
            Assert.Null(level.Place(Layer.Middle, new Cell(1, 0), Road, 0));
            Assert.True(level.IsDirty);
            level.MarkSaved();
            Assert.False(level.IsDirty);

            Assert.Null(level.Place(Layer.Middle, new Cell(2, 0), Road, 0));
            Assert.True(level.IsDirty);
            level.Undo();
            Assert.False(level.IsDirty);
            level.Undo();
            Assert.True(level.IsDirty);
            level.Redo();
            Assert.False(level.IsDirty);

            //The saved state is lost from the history: a new command after undoing past it
            level.Undo();
            Assert.Null(level.Place(Layer.Middle, new Cell(3, 0), Road, 0));
            Assert.True(level.IsDirty);
            level.Undo();
            Assert.True(level.IsDirty);
        }

        [Fact]
        public void TheLevelIsDirtyWhileACompoundWithChangesIsOpen()
        {
            EditedLevel level = EditedLevel.New(Catalog);
            using (level.BeginCompound("Stroke"))
            {
                Assert.False(level.IsDirty);
                Assert.Null(level.Place(Layer.Middle, new Cell(5, 0), Road, 0));
                Assert.True(level.IsDirty);
            }
            Assert.True(level.IsDirty);
            level.Undo();
            Assert.False(level.IsDirty);
        }

        [Fact]
        public void SavedAsGivesTheLevelItsFileAndMarksItSaved()
        {
            EditedLevel level = Fresh();
            Assert.Null(level.File);
            Assert.True(level.IsDirty);
            level.SavedAs(@"C:\levels\new.json");
            Assert.Equal(@"C:\levels\new.json", level.File);
            Assert.False(level.IsDirty);
        }

        [Fact]
        public void ANewLevelHasTheStartBlockAndNoFinish()
        {
            EditedLevel level = EditedLevel.New(Catalog);
            Assert.Null(level.File);
            Assert.False(level.IsDirty);
            Assert.False(level.CanUndo);
            PlacedBlock start = Assert.Single(level.Blocks(Layer.Middle));
            Assert.Equal(Start, start.Cell);
            Assert.Equal(Road, start.Name);
            Assert.Empty(level.Blocks(Layer.Back));
            Assert.Empty(level.Blocks(Layer.Front));
            Assert.Empty(level.Objects);
            LevelProblem problem = Assert.Single(level.Problems());
            Assert.Equal("The level has no finish.", problem.Message);
            Assert.Null(problem.Cell);
        }

        [Fact]
        public void ProblemsFindTheMissingStartBlock()
        {
            EditedLevel level = Fresh();
            Assert.Null(level.SetFinish(Layer.Middle, new Cell(1, 0)));
            Assert.True(level.Erase(Layer.Middle, Start));
            LevelProblem problem = Assert.Single(level.Problems());
            Assert.Contains("[0, 0]", problem.Message);
            Assert.Equal(Layer.Middle, problem.Layer);
            Assert.Equal(Start, problem.Cell);
        }

        [Fact]
        public void ProblemsFindAnObjectOverNothing()
        {
            EditedLevel level = Fresh();
            Assert.Null(level.SetFinish(Layer.Middle, new Cell(1, 0)));
            //Over the start block, two cells up: fine; over nothing: a problem
            Assert.Null(level.AddObject("Box", "box", 2f, -2f * Units.GridCell));
            Assert.Empty(level.Problems());
            Assert.Null(level.AddObject("Box", "box", 2f, -3f * Units.GridCell));
            Assert.Null(level.AddObject("Bar", "bar2", 5f * Units.GridCell, 0f));
            List<LevelProblem> problems = level.Problems();
            Assert.Equal(2, problems.Count);
            Assert.Equal("Object Box (box) at [2, -67.5] has nothing below it.", problems[0].Message);
            Assert.Equal(2f, problems[0].X);
            Assert.Equal(-67.5f, problems[0].Y);
            Assert.Null(problems[0].Cell);
            Assert.StartsWith("Object Bar (bar2)", problems[1].Message);
        }

        [Fact]
        public void ProblemsIncludeWhatTheDataValidationFinds()
        {
            //Every problem the data validation knows, in its words, and each with its place for the screen to go to
            LevelData data = new LevelData { Par = -1f };
            data.Middle.Add(new BlockPlacement { Block = Road, X = 0, Y = 0, Finish = true });
            data.Middle.Add(new BlockPlacement { Block = Decoration, X = 1, Y = 0 });
            data.Middle.Add(new BlockPlacement { Block = Road, X = 2, Y = 0, Finish = true });
            data.Back.Add(new BlockPlacement { Block = "no-such-block", X = 3, Y = 1 });
            data.Front.Add(new BlockPlacement { Block = Decoration, X = 4, Y = -1, Finish = true });
            data.Objects.Add(new ObjectPlacement { Type = "NoSuchType", Model = "box", X = 12f, Y = -30f });
            data.Objects.Add(new ObjectPlacement { Type = "Box", Model = "bar1", X = 20f, Y = -30f });
            EditedLevel level = new EditedLevel(Catalog, data, null);
            List<LevelProblem> problems = level.Problems();
            Assert.Equal(data.Validate(Catalog), problems.Select(problem => problem.Message));
            Assert.Equal(7, problems.Count);

            Assert.Equal(Layer.Middle, problems[0].Layer);
            Assert.Equal(new Cell(1, 0), problems[0].Cell);
            Assert.Equal(Layer.Back, problems[1].Layer);
            Assert.Equal(new Cell(3, 1), problems[1].Cell);
            Assert.Equal(Layer.Front, problems[2].Layer);
            Assert.Equal(new Cell(4, -1), problems[2].Cell);
            Assert.Null(problems[3].Cell);
            Assert.Null(problems[4].Cell);
            Assert.Equal(12f, problems[5].X);
            Assert.Equal(-30f, problems[5].Y);
            Assert.Equal(20f, problems[6].X);
            Assert.Null(problems[6].Cell);
        }

        [Fact]
        public void ALevelWithAFinishAStartAndGroundedObjectsHasNoProblems()
        {
            EditedLevel level = Fresh();
            Assert.Null(level.SetFinish(Layer.Middle, new Cell(1, 0)));
            Assert.Null(level.AddObject("Box", "box", 0f, -30f));
            level.SetPar(10f);
            Assert.Empty(level.Problems());
        }

        [Fact]
        public void ACellPrintsItsCoordinates()
        {
            Assert.Equal("[3, -1]", new Cell(3, -1).ToString());
            Assert.Equal(new Cell(1, -1), Cell.Of(22.5f, -30f));
        }
    }
}
