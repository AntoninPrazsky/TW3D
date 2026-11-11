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
        //How the problems about the gaps of a road begin
        private const string GapMessage = "The road has a gap";

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

        /// <summary>
        /// A block of the catalog by the shorthand of the level generator: its name, or the name of a block that turns
        /// with an "f" after it for that block turned ("b2f" is b2 turned; "bl2f" is a block of its own).
        /// </summary>
        private static (string Name, bool Turned) Named(string name)
        {
            if (Catalog.FindBlock(name) != null)
                return (name, false);
            string stem = name.Substring(0, name.Length - 1);
            Assert.True(name.EndsWith("f", StringComparison.Ordinal) && Catalog.FindBlock(stem)?.Turns == true, $"No block {name}");
            return (stem, true);
        }

        /// <summary>A placed block in the shorthand of <see cref="Named"/>: "b2f" for b2 turned.</summary>
        private static string Look(PlacedBlock block) => block.Name + (block.Turned ? "f" : "");

        /// <summary>Places a block named in the shorthand of <see cref="Named"/>; the reason of a refusal, null when done.</summary>
        private static string Place(EditedLevel level, Layer layer, Cell cell, string name, int shift)
        {
            (string block, bool turned) = Named(name);
            return level.Place(layer, cell, block, shift, turned);
        }

        /// <summary><see cref="EditedLevel.RoadShift(Cell, string, bool, int, bool, PlacedBlock, out Cell)"/> of a block named in the shorthand of <see cref="Named"/>.</summary>
        private static int RoadShift(EditedLevel level, Cell at, string name, int chosenShift, bool leftwards, PlacedBlock joined, out Cell cell)
        {
            (string block, bool turned) = Named(name);
            return level.RoadShift(at, block, turned, chosenShift, leftwards, joined, out cell);
        }

        private static int RoadShift(EditedLevel level, Cell at, string name, int chosenShift, bool leftwards, out Cell cell)
        {
            return RoadShift(level, at, name, chosenShift, leftwards, null, out cell);
        }

        [Theory]
        [MemberData(nameof(ShippedLevels))]
        public void SavesAShippedLevelAsItWasRead(string level)
        {
            EditedLevel edited = Load(level);
            Assert.Equal(FileText(level), Json(edited));
            Assert.False(edited.IsDirty);
            Assert.Equal(Repository.LevelPath(level), edited.File);
            //The levels built by hand have gaps in their roads (found by RoadGaps): nothing else is wrong with them
            Assert.All(edited.Problems(), problem => Assert.StartsWith(GapMessage, problem.Message));
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
        public void AnUnknownSceneryIsRefused()
        {
            EditedLevel level = Fresh();
            int version = level.Version;
            Assert.NotNull(level.AddScenery("no-such-prop", 0f, -1.25f, -10f, 0));
            Assert.Empty(level.Scenery);
            Assert.Equal(version, level.Version);
        }

        [Fact]
        public void SceneryIsAddedMovedTurnedAndRemovedWithUndo()
        {
            EditedLevel level = Fresh();
            Assert.Null(level.AddScenery("lamp", 10f, -1.25f, -12.5f, 0));
            //A turn is taken of a whole turn
            Assert.Null(level.AddScenery("cone", 20f, -1.25f, -8f, 5));
            Assert.Equal("Add Traffic cone", level.UndoName);
            PlacedScenery lamp = level.Scenery[0];
            PlacedScenery cone = level.Scenery[1];
            Assert.Equal(1, cone.Turn);

            level.MoveScenery(lamp, 11f, -2.5f, -15f);
            Assert.Equal((11f, -2.5f, -15f), (lamp.X, lamp.Y, lamp.Z));
            int version = level.Version;
            level.MoveScenery(lamp, 11f, -2.5f, -15f);
            Assert.Equal(version, level.Version);
            level.TurnScenery(lamp, -1);
            Assert.Equal(3, lamp.Turn);
            Assert.Equal("Turn Street lamp", level.UndoName);
            version = level.Version;
            level.TurnScenery(lamp, 7);
            Assert.Equal(version, level.Version);

            level.RemoveScenery(lamp);
            Assert.Equal(new[] { cone }, level.Scenery);
            level.Undo();
            Assert.Equal(new[] { lamp, cone }, level.Scenery);
            level.Undo();
            Assert.Equal(0, lamp.Turn);
            level.Undo();
            Assert.Equal((10f, -1.25f, -12.5f), (lamp.X, lamp.Y, lamp.Z));
            level.Undo();
            Assert.Equal(new[] { lamp }, level.Scenery);
            level.Redo();
            Assert.Equal(new[] { lamp, cone }, level.Scenery);
        }

        [Fact]
        public void SceneryIsSavedAndReadBack()
        {
            EditedLevel level = Fresh();
            Assert.Null(level.AddScenery("fence", -5.63f, -1.25f, -10f, 1));
            Assert.Null(level.AddScenery("pole", 30f, -1.25f, -30f, 0));
            string json = Json(level);
            Assert.Contains("\"scenery\": [", json);
            Assert.Contains("{\"model\":\"fence\",\"x\":-5.63,\"y\":-1.25,\"z\":-10,\"turn\":1}", json);
            //A turn of 0 is left out
            Assert.Contains("{\"model\":\"pole\",\"x\":30,\"y\":-1.25,\"z\":-30}", json);

            EditedLevel read = new EditedLevel(Catalog, LevelFile.Parse(json), null);
            Assert.Equal(json, Json(read));
            Assert.Equal(2, read.Scenery.Count);
            Assert.DoesNotContain(read.Problems(), problem => problem.Message.Contains("scenery", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void SceneryBelongsToTheLayerItStandsIn()
        {
            EditedLevel level = Fresh();
            foreach (float z in new[] { -100f, -40f, -30f, -11.3f, -11.25f, 0f, 11.25f, 11.3f, 40f })
                Assert.Null(level.AddScenery("cone", 0f, -1.25f, z, 0));
            Assert.Equal(new[] { Layer.Back3, Layer.Back2, Layer.Back, Layer.Back, Layer.Middle, Layer.Middle, Layer.Middle, Layer.Front, Layer.Front },
                level.Scenery.Select(prop => prop.Layer));
        }

        [Fact]
        public void MirroredSceneryGoesAcrossTheAxisAndTurnsAsItsMirrorImage()
        {
            EditedLevel level = Fresh();
            Assert.Null(level.AddScenery("lamp", 5f, -1.25f, -12.5f, 0));
            Assert.Null(level.AddScenery("fence", 30.3f, -1.25f, -10f, 1));
            PlacedScenery lamp = level.Scenery[0], fence = level.Scenery[1];
            //The axis between the columns 0 and 1: a cell's half to the right of the middle of column 0
            Assert.True(level.MirrorScenery(level.Scenery, 1));
            Assert.Equal((17.5f, 2), (lamp.X, lamp.Turn));
            Assert.Equal((-7.8f, 1), (fence.X, fence.Turn));
            Assert.Equal(-12.5f, lamp.Z);
            Assert.Equal("Mirror", level.UndoName);
            level.Undo();
            Assert.Equal((5f, 0), (lamp.X, lamp.Turn));
            Assert.Equal((30.3f, 1), (fence.X, fence.Turn));
            //A turned fence standing on the axis stays as it is: nothing to record
            Assert.Null(level.AddScenery("fence", 11.25f, -1.25f, -10f, 1));
            int version = level.Version;
            Assert.False(level.MirrorScenery(new[] { level.Scenery[2] }, 1));
            Assert.Equal(version, level.Version);
        }

        [Fact]
        public void BadSceneryOfAFileIsAProblem()
        {
            LevelData data = EditedLevel.New(Catalog).ToData();
            data.Scenery.Add(new SceneryPlacement { Model = "no-such-prop", X = 1f, Y = 2f, Z = 3f });
            data.Scenery.Add(new SceneryPlacement { Model = "cone", X = 1f, Y = 2f, Z = 3f, Turn = 4 });
            EditedLevel level = new EditedLevel(Catalog, data, null);
            List<string> messages = level.Problems().Select(problem => problem.Message).ToList();
            Assert.Contains("Unknown scenery \"no-such-prop\" at [1, 2, 3].", messages);
            Assert.Contains("Scenery \"cone\" at [1, 2, 3] has turn 4; a turn is 0 to 3 quarter turns.", messages);
            //The game's check finds the same
            Assert.Equal(2, data.Validate(Catalog).Count(problem => problem.Contains("cenery", StringComparison.Ordinal)));
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
        public void TheEditorKeepsADebugLevelAndSwitchesIt()
        {
            EditedLevel level = new EditedLevel(Catalog, new LevelData { Debug = true }, null);
            Assert.True(level.Debug);
            Assert.True(level.ToData().Debug);
            int version = level.Version;
            level.SetDebug(true);
            Assert.Equal(version, level.Version);
            level.SetDebug(false);
            Assert.Equal("Debug level", level.UndoName);
            Assert.False(level.ToData().Debug);
            level.Undo();
            Assert.True(level.Debug);
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
            Assert.False(problem.IsRoadGap);
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

        [Fact]
        public void ALineToTheSameCellPassesNoCell()
        {
            Assert.Empty(new Cell(2, 3).LineTo(new Cell(2, 3)));
        }

        [Fact]
        public void ALineAlongARowPassesEveryCellButTheFirst()
        {
            Assert.Equal(new[] { new Cell(1, 0), new Cell(2, 0), new Cell(3, 0) }, new Cell(0, 0).LineTo(new Cell(3, 0)));
            Assert.Equal(new[] { new Cell(-1, 5), new Cell(-2, 5) }, new Cell(0, 5).LineTo(new Cell(-2, 5)));
        }

        [Fact]
        public void ALineAlongAColumnPassesEveryCellButTheFirst()
        {
            Assert.Equal(new[] { new Cell(0, -1), new Cell(0, -2), new Cell(0, -3) }, new Cell(0, 0).LineTo(new Cell(0, -3)));
            Assert.Equal(new[] { new Cell(4, 2), new Cell(4, 3) }, new Cell(4, 1).LineTo(new Cell(4, 3)));
        }

        [Fact]
        public void ADiagonalLineSkipsNoCellAlongItsLongerAxis()
        {
            Assert.Equal(new[] { new Cell(1, 1), new Cell(2, 1), new Cell(3, 2), new Cell(4, 2) }, new Cell(0, 0).LineTo(new Cell(4, 2)));
            Assert.Equal(new[] { new Cell(1, -1), new Cell(2, -2) }, new Cell(0, 0).LineTo(new Cell(2, -2)));
        }

        [Fact]
        public void ASteepLineSkipsNoCellAlongItsLongerAxis()
        {
            //The longer axis is y here, so x is the one rounded
            Assert.Equal(new[] { new Cell(0, 1), new Cell(1, 2), new Cell(1, 3) }, new Cell(0, 0).LineTo(new Cell(1, 3)));
            Assert.Equal(new[] { new Cell(-1, -1), new Cell(-1, -2), new Cell(-2, -3), new Cell(-2, -4) }, new Cell(0, 0).LineTo(new Cell(-2, -4)));
        }

        /// <summary>A new level with the hill b2, b2, c2 turned, a1 at [1..4, 0] after the start block.</summary>
        private static EditedLevel Hill()
        {
            EditedLevel level = EditedLevel.New(Catalog);
            string[] names = { "b2", "b2", "c2f", "a1" };
            for (int i = 0; i < names.Length; i++)
                Assert.Null(Place(level, Layer.Middle, new Cell(i + 1, 0), names[i], 0));
            return level;
        }

        /// <summary>A level of the middle layer made of the blocks given as (name, x) (see <see cref="Named"/>), all at y = 0, in that order.</summary>
        private static EditedLevel Row((string Name, int X)[] blocks)
        {
            LevelData data = new LevelData();
            foreach ((string name, int x) in blocks)
            {
                (string block, bool turned) = Named(name);
                data.Middle.Add(new BlockPlacement { Block = block, Turned = turned, X = x, Y = 0 });
            }
            return new EditedLevel(Catalog, data, null);
        }

        private static List<PlacedBlock> Between(EditedLevel level, int fromX, int toX)
        {
            return level.Blocks(Layer.Middle).Where(block => block.Cell.X >= fromX && block.Cell.X <= toX).ToList();
        }

        [Fact]
        public void MoveBlocksMovesThemKeepingIdentityOrderShiftAndFinish()
        {
            EditedLevel level = Hill();
            Assert.True(level.Shift(Layer.Middle, new Cell(2, 0), 3));
            Assert.Null(level.SetFinish(Layer.Middle, new Cell(4, 0)));
            List<PlacedBlock> selection = Between(level, 2, 4);
            List<PlacedBlock> before = level.Blocks(Layer.Middle).ToList();

            Assert.True(level.MoveBlocks(Layer.Middle, selection, 0, -2));

            Assert.Equal(before, level.Blocks(Layer.Middle));
            Assert.Equal(new Cell(2, -2), selection[0].Cell);
            Assert.Equal(3, selection[0].Shift);
            Assert.Equal(new Cell(4, -2), selection[2].Cell);
            Assert.True(selection[2].Finish);
            Assert.Equal(new Cell(1, 0), before[1].Cell);
            Assert.Equal("Move", level.UndoName);
        }

        [Fact]
        public void MoveBlocksErasesWhatIsUnderTheTargetsAndUndoPutsItBackInPlace()
        {
            EditedLevel level = Hill();
            string json = Json(level);
            List<PlacedBlock> selection = Between(level, 0, 1);
            PlacedBlock under = level.BlockAt(Layer.Middle, new Cell(2, 0));

            level.MoveBlocks(Layer.Middle, selection, 1, 0);

            Assert.DoesNotContain(under, level.Blocks(Layer.Middle));
            Assert.Equal(4, level.Blocks(Layer.Middle).Count);
            Assert.Equal(new Cell(1, 0), selection[0].Cell);
            //The block moved onto the cell of another one that moves on is not erased with the ones under the targets
            Assert.Same(selection[1], level.BlockAt(Layer.Middle, new Cell(2, 0)));

            level.Undo();
            Assert.Equal(json, Json(level));
            Assert.Same(under, level.Blocks(Layer.Middle)[2]);
            level.Redo();
            Assert.DoesNotContain(under, level.Blocks(Layer.Middle));
            level.Undo();
            Assert.Equal(json, Json(level));
        }

        [Fact]
        public void MoveBlocksOntoACellWithABaseAndAGateErasesBoth()
        {
            EditedLevel level = Row(new[] { ("a1", 0), ("a1", 3), ("sg", 3), ("b1", 2) });
            string json = Json(level);
            List<PlacedBlock> selection = new List<PlacedBlock> { level.Blocks(Layer.Middle)[3] };

            level.MoveBlocks(Layer.Middle, selection, 1, 0);

            Assert.Equal(2, level.Blocks(Layer.Middle).Count);
            Assert.Same(selection[0], level.BlockAt(Layer.Middle, new Cell(3, 0)));
            level.Undo();
            Assert.Equal(json, Json(level));
            Assert.Equal("sg", level.BlockAt(Layer.Middle, new Cell(3, 0)).Name);
        }

        [Fact]
        public void MoveBlocksOfBothBlocksOfACellKeepsThemTogether()
        {
            EditedLevel level = Row(new[] { ("a1", 0), ("sg", 0) });
            List<PlacedBlock> both = level.Blocks(Layer.Middle).ToList();

            level.MoveBlocks(Layer.Middle, both, 2, 1);

            Assert.All(both, block => Assert.Equal(new Cell(2, 1), block.Cell));
            Assert.Equal(2, level.Blocks(Layer.Middle).Count);
            level.Undo();
            Assert.All(both, block => Assert.Equal(new Cell(0, 0), block.Cell));
        }

        [Fact]
        public void MoveBlocksLeavesOutBlocksThatAreNotInTheLayer()
        {
            EditedLevel level = Hill();
            int version = level.Version;
            level.MoveBlocks(Layer.Back, Between(level, 1, 2), 1, 0);
            level.MoveBlocks(Layer.Middle, new[] { new PlacedBlock(new Cell(9, 9), "a1", 0, false) }, 1, 0);
            Assert.Equal(version, level.Version);
            Assert.Equal("Place a1", level.UndoName);
        }

        [Fact]
        public void MoveBlocksWithNoBlocksOrNoMoveRecordsNothing()
        {
            EditedLevel level = Hill();
            string json = Json(level);
            int version = level.Version;
            string undo = level.UndoName;

            //The result tells whether a step was recorded, so that the editor does not claim a move that was not made
            Assert.False(level.MoveBlocks(Layer.Middle, new List<PlacedBlock>(), 1, 0));
            Assert.False(level.MoveBlocks(Layer.Middle, Between(level, 1, 4), 0, 0));
            Assert.False(level.MoveBlocks(Layer.Middle, new[] { new PlacedBlock(new Cell(9, 9), "a1", 0, false) }, 1, 0));

            Assert.Equal(version, level.Version);
            Assert.Equal(undo, level.UndoName);
            Assert.Equal(json, Json(level));
        }

        [Fact]
        public void MirrorBlocksTurnsAHillIntoADescentInMirroredOrderAndTurns()
        {
            EditedLevel level = Hill();
            string json = Json(level);
            List<PlacedBlock> selection = Between(level, 1, 4);

            level.MirrorBlocks(Layer.Middle, selection);

            //b2, b2, c2 turned, a1 at 1..4 become a1, c2, b2 turned, b2 turned at 1..4: the same blocks, in the same
            //places of the file, the blocks that turn turned round (a1 does not turn and looks the same mirrored)
            string[] expected = { "a1", "c2", "b2f", "b2f" };
            for (int x = 1; x <= 4; x++)
                Assert.Equal(expected[x - 1], Look(level.BlockAt(Layer.Middle, new Cell(x, 0))));
            Assert.Same(selection[3], level.BlockAt(Layer.Middle, new Cell(1, 0)));
            Assert.Equal(new Cell(4, 0), selection[0].Cell);
            Assert.Equal("Mirror", level.UndoName);

            level.Undo();
            Assert.Equal(json, Json(level));
            level.Redo();
            level.MirrorBlocks(Layer.Middle, selection);
            Assert.Equal(json, Json(level));
        }

        [Fact]
        public void MirrorBlocksKeepsShiftFinishAndTheNameOfABlockWithoutAMirror()
        {
            EditedLevel level = Hill();
            Assert.Null(level.Place(Layer.Middle, new Cell(5, 0), "lo", 0));
            Assert.Null(level.SetFinish(Layer.Middle, new Cell(1, 0)));
            Assert.True(level.Shift(Layer.Middle, new Cell(5, 0), -2));
            List<PlacedBlock> selection = Between(level, 1, 5);

            level.MirrorBlocks(Layer.Middle, selection);

            PlacedBlock lo = level.BlockAt(Layer.Middle, new Cell(1, 0));
            Assert.Equal("lo", lo.Name);
            Assert.Equal(-2, lo.Shift);
            Assert.True(selection[0].Finish);
            Assert.Equal(new Cell(5, 0), selection[0].Cell);
        }

        [Fact]
        public void MirrorBlocksOfOneBlockTurnsItWhereItIs()
        {
            EditedLevel level = Hill();
            PlacedBlock block = level.BlockAt(Layer.Middle, new Cell(1, 0));

            level.MirrorBlocks(Layer.Middle, new[] { block });

            Assert.Equal("b2", block.Name);
            Assert.True(block.Turned);
            Assert.Equal(new Cell(1, 0), block.Cell);
            level.MirrorBlocks(Layer.Middle, new[] { block });
            Assert.False(block.Turned);
        }

        [Fact]
        public void MirrorBlocksSwapsTheDecorationsWithAMirroredVariantAndKeepsTheirTurn()
        {
            EditedLevel level = Row();
            Assert.Null(level.Place(Layer.Back, new Cell(0, 0), "bl2", 0));
            Assert.Null(level.Place(Layer.Back, new Cell(1, 0), "bl2s", 0));
            Assert.Null(level.Place(Layer.Back, new Cell(2, 0), "bl1", 0));
            List<PlacedBlock> blocks = level.Blocks(Layer.Back).ToList();

            level.MirrorBlocks(Layer.Back, blocks);

            //bl2 and bl2f are mirror images, not turns: the names swap; bl1 has no variant and stays
            Assert.Equal(new[] { "bl1", "bl2sf", "bl2f" }, new[] { 0, 1, 2 }.Select(x => Look(level.BlockAt(Layer.Back, new Cell(x, 0)))));
            Assert.All(blocks, block => Assert.False(block.Turned));
        }

        [Fact]
        public void MirrorBlocksErasesWhatIsUnderTheTargetsAndUndoPutsItBack()
        {
            //The gate sg is in the cell [1, 0] with a1
            EditedLevel level = Row(new[] { ("b1", 0), ("a1", 1), ("sg", 1), ("a1", 2), ("c1", 3), ("a1", 4), ("a1", 5) });
            string json = Json(level);
            IReadOnlyList<PlacedBlock> blocks = level.Blocks(Layer.Middle);

            //The ends swap (x' = 0 + 5 - x); the cells between are not targets, so nothing is erased
            PlacedBlock first = blocks[0], last = blocks[6];
            level.MirrorBlocks(Layer.Middle, new[] { first, last });
            Assert.Equal(7, blocks.Count);
            Assert.Equal("b1f", Look(first));
            Assert.Equal(new Cell(5, 0), first.Cell);
            Assert.Equal(new Cell(0, 0), last.Cell);
            level.Undo();
            Assert.Equal(json, Json(level));

            //The a1 at [1, 0] and the c1 at [3, 0] swap; the gate in [1, 0] is not among them and is erased
            PlacedBlock left = blocks[1], right = blocks[4];
            level.MirrorBlocks(Layer.Middle, new[] { left, right });
            Assert.Equal(6, blocks.Count);
            Assert.Same(right, level.BlockAt(Layer.Middle, new Cell(1, 0)));
            Assert.Equal("c1f", Look(right));
            level.Undo();
            Assert.Equal(json, Json(level));
            Assert.Equal("sg", level.Blocks(Layer.Middle)[2].Name);
        }

        [Fact]
        public void MirrorBlocksOfABlockInTheMiddleColumnKeepsItsCompanion()
        {
            EditedLevel level = Row(new[] { ("b1", 0), ("sg", 0), ("a1", 1) });
            PlacedBlock middle = level.Blocks(Layer.Middle)[0];

            Assert.True(level.MirrorBlocks(Layer.Middle, new[] { middle }));

            //A block that stays in its cell displaces nobody
            Assert.Equal(3, level.Blocks(Layer.Middle).Count);
            Assert.Equal("b1f", Look(level.Blocks(Layer.Middle)[0]));
        }

        [Fact]
        public void MirrorBlocksWithNoBlocksOrBlocksThatStayRecordsNothing()
        {
            EditedLevel level = Hill();
            int version = level.Version;
            Assert.False(level.MirrorBlocks(Layer.Middle, new List<PlacedBlock>()));
            //A block that neither turns nor has a mirrored variant alone stays as it is, and the result says that no
            //step was recorded
            Assert.False(level.MirrorBlocks(Layer.Middle, new[] { level.BlockAt(Layer.Middle, new Cell(4, 0)) }));
            Assert.Equal(version, level.Version);
        }

        [Fact]
        public void MoveAndMirrorInsideACompoundAreOneStep()
        {
            EditedLevel level = Hill();
            string json = Json(level);
            List<PlacedBlock> selection = Between(level, 1, 4);
            using (level.BeginCompound("Both"))
            {
                level.MoveBlocks(Layer.Middle, selection, 3, 0);
                level.MirrorBlocks(Layer.Middle, selection);
            }
            Assert.Equal("Both", level.UndoName);
            level.Undo();
            Assert.Equal(json, Json(level));
        }

        [Theory]
        [MemberData(nameof(ShippedLevels))]
        public void MovingAndMirroringTheWholeMiddleLayerOfAShippedLevelCanBeUndone(string level)
        {
            EditedLevel edited = Load(level);
            List<PlacedBlock> all = edited.Blocks(Layer.Middle).ToList();
            int first = all[0].Cell.X;

            edited.MoveBlocks(Layer.Middle, all, 1, 0);
            Assert.Equal(first + 1, all[0].Cell.X);
            Assert.NotEqual(FileText(level), Json(edited));
            edited.Undo();
            Assert.Equal(FileText(level), Json(edited));
            Assert.False(edited.IsDirty);

            edited.MirrorBlocks(Layer.Middle, all);
            edited.Undo();
            Assert.Equal(FileText(level), Json(edited));
            //The layer mirrored twice is the layer
            edited.MirrorBlocks(Layer.Middle, all);
            edited.MirrorBlocks(Layer.Middle, all);
            Assert.Equal(FileText(level), Json(edited));
        }

        [Fact]
        public void MirrorNameSwapsEveryBlockWithItsFVariant()
        {
            (string Plain, string Mirrored)[] pairs = { ("bl2", "bl2f"), ("bl2s", "bl2sf") };
            foreach ((string plain, string mirrored) in pairs)
            {
                Assert.Equal(mirrored, EditedLevel.MirrorName(Catalog, plain));
                Assert.Equal(plain, EditedLevel.MirrorName(Catalog, mirrored));
            }

            //Every block of the catalog that ends in f and whose stem is a block is one of a pair
            int found = 0;
            foreach (BlockType block in Catalog.Blocks.Where(block => block.Name.EndsWith("f", StringComparison.Ordinal)))
            {
                string stem = block.Name.Substring(0, block.Name.Length - 1);
                if (Catalog.FindBlock(stem) == null)
                    continue;
                found++;
                Assert.Equal(stem, EditedLevel.MirrorName(Catalog, block.Name));
                Assert.Equal(block.Name, EditedLevel.MirrorName(Catalog, stem));
            }
            Assert.True(found >= pairs.Length);
        }

        [Fact]
        public void MirrorNameKeepsABlockWithoutAMirroredVariant()
        {
            Assert.Equal("kea1f", EditedLevel.MirrorName(Catalog, "kea1f"));
            //A block that turns has no mirrored variant: it is turned instead
            Assert.Equal("b1", EditedLevel.MirrorName(Catalog, "b1"));
            Assert.Equal("lo", EditedLevel.MirrorName(Catalog, "lo"));
            Assert.Equal("nothing", EditedLevel.MirrorName(Catalog, "nothing"));
        }

        [Fact]
        public void MirrorNameTwiceGivesTheBlockBack()
        {
            foreach (BlockType block in Catalog.Blocks)
                Assert.Equal(block.Name, EditedLevel.MirrorName(Catalog, EditedLevel.MirrorName(Catalog, block.Name)));
        }

        [Fact]
        public void TurnBlocksTurnsTheBlocksThatTurnInTheirCellsAsOneStep()
        {
            EditedLevel level = Hill();
            Assert.Null(level.SetFinish(Layer.Middle, new Cell(2, 0)));
            Assert.True(level.Shift(Layer.Middle, new Cell(2, 0), 3));
            string json = Json(level);
            List<PlacedBlock> selection = Between(level, 1, 4);
            List<PlacedBlock> before = level.Blocks(Layer.Middle).ToList();

            //b2, b2, c2 turned turn; a1 does not
            Assert.Equal(3, level.TurnBlocks(Layer.Middle, selection));

            Assert.Equal(new[] { "b2f", "b2f", "c2", "a1" }, selection.Select(Look));
            Assert.Equal(before, level.Blocks(Layer.Middle));
            Assert.Equal(new[] { 1, 2, 3, 4 }, selection.Select(block => block.Cell.X));
            Assert.Equal(3, selection[1].Shift);
            Assert.True(selection[1].Finish);
            Assert.Equal("Turn", level.UndoName);

            level.Undo();
            Assert.Equal(json, Json(level));
            level.Redo();
            Assert.Equal("b2f", Look(selection[0]));
            //Turned twice, the blocks are as they were
            level.TurnBlocks(Layer.Middle, selection);
            Assert.Equal(json, Json(level));
        }

        [Fact]
        public void TurnBlocksOfOneBlockNamesTheStepAfterIt()
        {
            EditedLevel level = Hill();
            Assert.Equal(1, level.TurnBlocks(Layer.Middle, new[] { level.BlockAt(Layer.Middle, new Cell(1, 0)) }));
            Assert.Equal("Turn b2", level.UndoName);
        }

        [Fact]
        public void TurnBlocksRecordsNothingForBlocksThatDoNotTurnOrAreNotInTheLayer()
        {
            EditedLevel level = Hill();
            int version = level.Version;
            string undo = level.UndoName;
            Assert.Equal(0, level.TurnBlocks(Layer.Middle, new List<PlacedBlock>()));
            Assert.Equal(0, level.TurnBlocks(Layer.Middle, new[] { level.BlockAt(Layer.Middle, new Cell(4, 0)) }));
            Assert.Equal(0, level.TurnBlocks(Layer.Back, Between(level, 1, 3)));
            Assert.Equal(0, level.TurnBlocks(Layer.Middle, new[] { new PlacedBlock(new Cell(9, 9), "b1", 0, false) }));
            Assert.Equal(version, level.Version);
            Assert.Equal(undo, level.UndoName);
        }

        [Fact]
        public void PlaceTurnedReplacesTheBlockNotTurnedAndRefusesABlockThatDoesNotTurn()
        {
            EditedLevel level = Hill();
            string json = Json(level);
            PlacedBlock block = level.BlockAt(Layer.Middle, new Cell(1, 0));

            Assert.Null(level.Place(Layer.Middle, new Cell(1, 0), "b2", 0, true));
            Assert.Same(block, level.BlockAt(Layer.Middle, new Cell(1, 0)));
            Assert.True(block.Turned);
            Assert.Equal("Place b2 turned", level.UndoName);
            //The same block with the same shift and turn records nothing
            int version = level.Version;
            Assert.Null(level.Place(Layer.Middle, new Cell(1, 0), "b2", 0, true));
            Assert.Equal(version, level.Version);
            level.Undo();
            Assert.Equal(json, Json(level));

            Assert.Equal("Block a1 cannot be turned.", level.Place(Layer.Middle, new Cell(5, 0), "a1", 0, true));
            Assert.Equal("Block tu1v cannot be turned.", level.Place(Layer.Middle, new Cell(5, 0), "tu1v", 0, true));
            Assert.Equal(json, Json(level));
        }

        [Fact]
        public void ATurnedBlockIsSavedAndReadBackTurned()
        {
            EditedLevel level = Row(("a1", 0, 0, 0), ("b2f", 1, 0, -4));
            string json = Json(level);
            Assert.Contains("{\"block\":\"b2\",\"x\":1,\"y\":0,\"shift\":-4,\"turned\":true}", json);
            //A block that is not turned writes no turn
            Assert.Contains("{\"block\":\"a1\",\"x\":0,\"y\":0,\"shift\":0}", json);

            EditedLevel again = new EditedLevel(Catalog, LevelFile.Parse(json), null);
            Assert.True(again.BlockAt(Layer.Middle, new Cell(1, 0)).Turned);
            Assert.Equal(json, Json(again));
            Assert.Empty(again.RoadGaps());
        }

        [Fact]
        public void ATurnedBlockThatCannotTurnIsAProblem()
        {
            LevelData data = new LevelData();
            data.Middle.Add(new BlockPlacement { Block = "a1", X = 0, Y = 0, Turned = true, Finish = true });
            EditedLevel level = new EditedLevel(Catalog, data, null);
            const string Message = "Block \"a1\" at [0, 0] in the middle layer is turned, but it cannot be turned.";
            LevelProblem problem = Assert.Single(level.Problems());
            Assert.Equal(Message, problem.Message);
            Assert.Equal(new Cell(0, 0), problem.Cell);
            Assert.Equal(new[] { Message }, data.Validate(Catalog));
            //Mirrored or turned, it stays turned, and so the problem stays: only fixing it by hand removes it
            Assert.Equal(0, level.TurnBlocks(Layer.Middle, level.Blocks(Layer.Middle)));
        }

        [Fact]
        public void ATurnedRoadBlockJoinsTheRoadWithTheEdgesOfItsProfileSwapped()
        {
            //b2 turned climbs: it starts 4 steps below its base, so after the flat start its base is 4 steps up
            EditedLevel level = EditedLevel.New(Catalog);
            Assert.Equal(-4, level.RoadShift(new Cell(1, 0), "b2", true, 0, false, out Cell cell));
            Assert.Equal(new Cell(1, 0), cell);
            //Not turned, it descends from the surface of the start block: its base stays at 0
            Assert.Equal(0, level.RoadShift(new Cell(1, 0), "b2", false, 0, false, out cell));
        }

        /// <summary>A level of road blocks only (no start block), each given as name (see <see cref="Named"/>), x, y, shift.</summary>
        private static EditedLevel Row(params (string Block, int X, int Y, int Shift)[] blocks)
        {
            EditedLevel level = new EditedLevel(Catalog, new LevelData(), null);
            foreach ((string block, int x, int y, int shift) in blocks)
                Assert.Null(Place(level, Layer.Middle, new Cell(x, y), block, shift));
            return level;
        }

        /// <summary>
        /// Places a road block the way road mode does: where RoadShift says, the stroke continuing from the cell given
        /// (the neighbours are looked for in the column of it).
        /// </summary>
        private static Cell PlaceOnRoad(EditedLevel level, int x, string block, bool leftwards = false)
        {
            int shift = RoadShift(level, new Cell(x, 0), block, 0, leftwards, out Cell cell);
            Assert.Null(Place(level, Layer.Middle, cell, block, shift));
            return cell;
        }

        [Fact]
        public void EveryRoadBlockHasAProfileOfTwoNumbers()
        {
            List<BlockType> road = Catalog.Blocks.Where(block => block.IsRoad).ToList();
            Assert.NotEmpty(road);
            Assert.All(road, block => Assert.Equal(2, block.Profile.Length));
            //A road block has a collision shape: it can be in the middle layer
            Assert.All(road, block => Assert.True(block.Collision));
            //Turned, a block has the edges of its profile swapped: b2 descends, b2 turned climbs
            Assert.Equal(new[] { 0, 4 }, Catalog.FindBlock("b2").PlacedProfile(false));
            Assert.Equal(new[] { 4, 0 }, Catalog.FindBlock("b2").PlacedProfile(true));
            Assert.False(Catalog.FindBlock(Decoration).IsRoad);
            Assert.Null(Catalog.FindBlock(Decoration).PlacedProfile(true));
        }

        [Fact]
        public void TheBaseAndTheSurfaceHeightsFollowTheShiftAndTheProfile()
        {
            EditedLevel level = Row(("b2f", 3, -1, 1), ("a1", 4, 2, -3));
            PlacedBlock climb = level.BlockAt(Layer.Middle, new Cell(3, -1));
            PlacedBlock flat = level.BlockAt(Layer.Middle, new Cell(4, 2));
            //9 * y + shift, and heights grow downward: b2 turned is [4, 0]
            Assert.Equal(-8, EditedLevel.Base(climb));
            Assert.Equal(-4, level.SurfaceLeft(climb));
            Assert.Equal(-8, level.SurfaceRight(climb));
            Assert.Equal(15, EditedLevel.Base(flat));
            Assert.Equal(15, level.SurfaceLeft(flat));
            Assert.Equal(15, level.SurfaceRight(flat));
        }

        [Fact]
        public void ABlockWithoutAProfileHasNoSurface()
        {
            EditedLevel level = Row();
            Assert.Null(level.Place(Layer.Back, new Cell(0, 0), Decoration, 0));
            Assert.Null(level.Place(Layer.Back, new Cell(1, 0), "a1", 0));
            PlacedBlock decoration = level.BlockAt(Layer.Back, new Cell(0, 0));
            Assert.Null(level.SurfaceLeft(decoration));
            Assert.Null(level.SurfaceRight(decoration));
            //An unknown block has none either
            PlacedBlock unknown = new PlacedBlock(new Cell(0, 0), "nothing", 0, false);
            Assert.Null(level.SurfaceLeft(unknown));
        }

        [Theory]
        [InlineData("training2")]
        [InlineData("training3")]
        [InlineData("summit")]
        public void TheLevelsOfTheGeneratorHaveNoRoadGaps(string name)
        {
            //Proof that the profiles of the catalog are those the generator builds its roads with
            EditedLevel level = Load(name);
            Assert.Empty(level.RoadGaps());
            Assert.DoesNotContain(level.Problems(), problem => problem.Message.StartsWith(GapMessage, StringComparison.Ordinal));
        }

        [Fact]
        public void TheOnlyGapOfTheFirstTrainingLevelIsItsJump()
        {
            //training1.js leaves the road with step(2) before the small jump on purpose, which the editor marks as a gap
            EditedLevel level = Load("training1");
            RoadGap gap = Assert.Single(level.RoadGaps());
            Assert.Equal(new Cell(13, 0), gap.A.Cell);
            Assert.Equal(new Cell(14, 0), gap.B.Cell);
            Assert.Equal(2, gap.Steps);
        }

        [Fact]
        public void TheGapsOfTheDocksAreTheLockAndTheEdgesOfTheContainerYard()
        {
            //docks.js steps the road up 3 at the lock block (the front of the trailer meets the road there, as in the lock
            //level) and sinks the floor of the container yard 5 steps below the road; the editor takes the floor for the
            //road block of the cell over the ledge, where both stand. The jump across the harbour is no gap: no road block
            //stands over the water. (Positive: the road goes up.)
            EditedLevel level = Load("docks");
            Assert.Equal(new[] { (9, 10, 3), (18, 19, -5), (24, 25, 5) },
                level.RoadGaps().Select(gap => (gap.A.Cell.X, gap.B.Cell.X, gap.LeftHeight - gap.RightHeight)));
        }

        [Fact]
        public void ARowOfBlocksThatJoinHasNoGapsAndAJumpInItHasOne()
        {
            EditedLevel level = Row(("b2f", 1, 0, 0), ("b2f", 2, 0, -4), ("a1", 3, 0, -4));
            Assert.Empty(level.RoadGaps());

            //The flat block is down at its base 0: the road breaks by the 4 steps the climb has gone up
            Assert.Null(level.Place(Layer.Middle, new Cell(3, 0), "a1", 0));
            RoadGap gap = Assert.Single(level.RoadGaps());
            Assert.Equal(new Cell(2, 0), gap.A.Cell);
            Assert.Equal(new Cell(3, 0), gap.B.Cell);
            Assert.Equal(-4, gap.LeftHeight);
            Assert.Equal(0, gap.RightHeight);
            Assert.Equal(4, gap.Steps);
        }

        [Fact]
        public void AGapOfEightStepsIsFoundAndReportedAsAProblemWithItsPlace()
        {
            EditedLevel level = Row(("b2f", 1, 0, 0), ("b2f", 2, 0, -4), ("b2f", 3, -1, 1), ("a1", 4, 0, 0));
            RoadGap gap = Assert.Single(level.RoadGaps());
            Assert.Equal(new Cell(3, -1), gap.A.Cell);
            Assert.Equal(new Cell(4, 0), gap.B.Cell);
            Assert.Equal(-8, gap.LeftHeight);
            Assert.Equal(0, gap.RightHeight);

            LevelProblem problem = level.Problems().Single(found => found.Message.StartsWith(GapMessage, StringComparison.Ordinal));
            Assert.Equal("The road has a gap between [3, -1] and [4, 0]: the surfaces differ by 8 steps.", problem.Message);
            Assert.Equal(Layer.Middle, problem.Layer);
            Assert.Equal(new Cell(3, -1), problem.Cell);
            Assert.True(problem.IsRoadGap);
        }

        [Fact]
        public void AGapOfOneStepSaysStep()
        {
            EditedLevel level = Row(("a1", 0, 0, 0), ("a1", 1, 0, 1));
            Assert.Contains(level.Problems(), problem => problem.Message.EndsWith("differ by 1 step.", StringComparison.Ordinal));
        }

        [Fact]
        public void TheEndOfTheRoadAndBlocksOutsideTheRoadAreNoGaps()
        {
            //The road ends at x = 2: there is no road block in the next column, whatever is in the other layers
            EditedLevel level = Row(("a1", 0, 0, 0), ("a1", 1, 0, 0), ("a1", 2, 0, 0));
            Assert.Null(level.Place(Layer.Back, new Cell(3, 5), "a1", 0));
            Assert.Null(level.Place(Layer.Front, new Cell(3, 5), "a1", 0));
            Assert.Empty(level.RoadGaps());
            //A column without a road block between is not a gap either: the road is broken there on purpose
            Assert.Null(level.Place(Layer.Middle, new Cell(4, 0), "a1", 5));
            Assert.Empty(level.RoadGaps());
        }

        [Fact]
        public void TheLastRoadBlockOfACellCountsAndABlockWithoutAProfileIsNotOne()
        {
            EditedLevel level = Row(("a1", 0, 0, 0), ("a1", 1, 0, 3));
            Assert.Single(level.RoadGaps());
            //Another road block in the same cell, placed later, is the one that counts (the file may hold two)
            LevelData data = level.ToData();
            data.Middle.Add(new BlockPlacement { Block = "a1", X = 1, Y = 0, Shift = 0 });
            Assert.Empty(new EditedLevel(Catalog, data, null).RoadGaps());
            //A wall or a gate in the cell is no road block: it does not take the place of the road block under it
            data = level.ToData();
            data.Middle.Add(new BlockPlacement { Block = "xa6", X = 1, Y = 0, Shift = 0 });
            Assert.Null(Catalog.FindBlock("xa6").Profile);
            Assert.Single(new EditedLevel(Catalog, data, null).RoadGaps());
        }

        [Fact]
        public void WithSeveralRoadBlocksInAColumnTheNearestInHeightIsTheNext()
        {
            //Two roads, one 9 steps (a cell) below the other, side by side: each joins its own
            EditedLevel level = Row(("a1", 0, 0, 0), ("a1", 1, 0, 0), ("a1", 0, 1, 0), ("a1", 1, 1, 0));
            Assert.Empty(level.RoadGaps());
            //The lower road goes up a step at the junction: the gap is the one of its own road
            Assert.Null(level.Place(Layer.Middle, new Cell(1, 1), "a1", -1));
            RoadGap gap = Assert.Single(level.RoadGaps());
            Assert.Equal(new Cell(0, 1), gap.A.Cell);
            Assert.Equal(new Cell(1, 1), gap.B.Cell);
        }

        [Fact]
        public void TheGapsFollowEveryChangeAndUndo()
        {
            EditedLevel level = Row(("a1", 0, 0, 0), ("a1", 1, 0, 0));
            Assert.Empty(level.RoadGaps());
            Assert.True(level.Shift(Layer.Middle, new Cell(1, 0), 2));
            Assert.Single(level.RoadGaps());
            level.Undo();
            Assert.Empty(level.RoadGaps());
            level.Redo();
            Assert.Single(level.RoadGaps());
            Assert.True(level.Erase(Layer.Middle, new Cell(1, 0)));
            Assert.Empty(level.RoadGaps());
        }

        [Fact]
        public void RoadShiftJoinsTheClimbOfThreeBlocksFromTheCellOfTheFirst()
        {
            //The first block placed by hand, the others by the road: [1, 0] shift 0, [2, 0] shift -4, [3, -1] shift 1
            EditedLevel level = Row(("b2f", 1, 0, 0));
            int second = RoadShift(level, new Cell(2, 0), "b2f", 0, false, out Cell secondCell);
            Assert.Equal(new Cell(2, 0), secondCell);
            Assert.Equal(-4, second);
            Assert.Null(Place(level, Layer.Middle, secondCell, "b2f", second));

            int third = RoadShift(level, new Cell(3, 0), "b2f", 0, false, out Cell thirdCell);
            Assert.Equal(new Cell(3, -1), thirdCell);
            Assert.Equal(1, third);
            Assert.Null(Place(level, Layer.Middle, thirdCell, "b2f", third));
            Assert.Empty(level.RoadGaps());
        }

        [Fact]
        public void RoadShiftJoinsToTheBlockOnTheLeftOfAStart()
        {
            //The start block a1 is flat at 0: a climb that starts at its surface has its base 4 steps up
            EditedLevel level = EditedLevel.New(Catalog);
            int shift = RoadShift(level, new Cell(1, 5), "b2f", 7, false, out Cell cell);
            Assert.Equal(new Cell(1, 0), cell);
            Assert.Equal(-4, shift);
        }

        [Fact]
        public void RoadShiftPaintsLeftwardsFromTheBlockOnTheRight()
        {
            //A descent to the right of a flat block, painted from the right: b2 ends 4 steps down at its right edge [0, 4]
            EditedLevel level = Row(("a1", 5, 0, 0));
            int shift = RoadShift(level, new Cell(4, 0), "b2", 0, true, out Cell cell);
            //The right edge of b2 (base + 4) meets the left edge of a1 (0): base -4
            Assert.Equal(new Cell(4, 0), cell);
            Assert.Equal(-4, shift);
            Assert.Null(level.Place(Layer.Middle, cell, "b2", shift));
            Assert.Empty(level.RoadGaps());

            //Further leftwards b2 again: its right edge meets the left edge of the b2 placed, at base + 0 = -4
            int next = RoadShift(level, new Cell(3, 0), "b2", 0, true, out Cell nextCell);
            Assert.Equal(new Cell(3, -1), nextCell);
            Assert.Equal(1, next);
            Assert.Null(level.Place(Layer.Middle, nextCell, "b2", next));
            Assert.Empty(level.RoadGaps());
        }

        [Fact]
        public void RoadShiftTakesTheNeighbourInTheDirectionOfThePaintingFirst()
        {
            //Both neighbours exist, a flat road at 0 on the left and one at 20 steps on the right
            EditedLevel level = Row(("a1", 0, 0, 0), ("a1", 2, 2, 2));
            int rightwards = RoadShift(level, new Cell(1, 0), "a1", 0, false, out Cell rightwardsCell);
            Assert.Equal(new Cell(1, 0), rightwardsCell);
            Assert.Equal(0, rightwards);
            int leftwards = RoadShift(level, new Cell(1, 0), "a1", 0, true, out Cell leftwardsCell);
            //20 steps: two cells down and 2 steps
            Assert.Equal(new Cell(1, 2), leftwardsCell);
            Assert.Equal(2, leftwards);
        }

        [Fact]
        public void RoadShiftFallsBackToTheOtherSideAndThenToTheChosenShift()
        {
            //No neighbour on the left: the one on the right is joined although the painting goes rightwards
            EditedLevel level = Row(("a1", 3, 0, 4));
            int shift = RoadShift(level, new Cell(2, 0), "a1", 0, false, out Cell cell);
            Assert.Equal(new Cell(2, 0), cell);
            Assert.Equal(4, shift);

            //No neighbour at all: the cursor cell and the chosen shift
            shift = RoadShift(level, new Cell(7, -3), "a1", 5, false, out cell);
            Assert.Equal(new Cell(7, -3), cell);
            Assert.Equal(5, shift);
            shift = RoadShift(level, new Cell(7, -3), "a1", 5, true, out cell);
            Assert.Equal(new Cell(7, -3), cell);
            Assert.Equal(5, shift);
        }

        [Fact]
        public void RoadShiftKeepsTheChosenShiftOfABlockWithoutAProfile()
        {
            EditedLevel level = Row(("a1", 0, 0, 0));
            int shift = RoadShift(level, new Cell(1, 4), Decoration, -2, false, out Cell cell);
            Assert.Equal(new Cell(1, 4), cell);
            Assert.Equal(-2, shift);
            //A wall of the middle layer next to the road has no profile either
            shift = RoadShift(level, new Cell(1, 4), "xa6", 3, false, out cell);
            Assert.Equal(new Cell(1, 4), cell);
            Assert.Equal(3, shift);
        }

        [Fact]
        public void RoadShiftPicksTheNeighbourNearestToTheChosenHeight()
        {
            //Two roads in the column to the left, a cell apart: the cursor is next to the lower one
            EditedLevel level = Row(("a1", 0, 0, 0), ("a1", 0, 1, 0));
            int shift = RoadShift(level, new Cell(1, 1), "a1", 0, false, out Cell cell);
            Assert.Equal(new Cell(1, 1), cell);
            Assert.Equal(0, shift);
            shift = RoadShift(level, new Cell(1, 0), "a1", 0, false, out cell);
            Assert.Equal(new Cell(1, 0), cell);
            Assert.Equal(0, shift);
        }

        [Fact]
        public void ARoadPaintedByRoadShiftHasNoGapsAndSurvivesSaving()
        {
            //A hill: up three, a flat piece, down three, built block by block
            EditedLevel level = Row(("a1", 0, 0, 0));
            string[] hill = { "b2f", "b2f", "b2f", "a1", "b2", "b2", "b2", "a1" };
            for (int i = 0; i < hill.Length; i++)
                PlaceOnRoad(level, i + 1, hill[i]);
            Assert.Empty(level.RoadGaps());
            Assert.Equal(hill.Length + 1, level.Blocks(Layer.Middle).Count);

            //The shifts written to the file give the same road when it is read again
            EditedLevel again = new EditedLevel(Catalog, LevelFile.Parse(Json(level)), null);
            Assert.Equal(Json(level), Json(again));
            Assert.Empty(again.RoadGaps());
            //The hill is back at the height it began: the last flat block is on the surface of the first
            Assert.Equal(0, again.SurfaceRight(again.Blocks(Layer.Middle)[hill.Length]));
        }

        [Fact]
        public void ARoadPaintedLeftwardsHasNoGapsEither()
        {
            EditedLevel level = Row(("a1", 10, 0, 0));
            string[] hill = { "b2", "b2", "b2", "a1", "b2f", "b2f" };
            for (int i = 0; i < hill.Length; i++)
                PlaceOnRoad(level, 9 - i, hill[i], true);
            Assert.Empty(level.RoadGaps());
        }

        [Fact]
        public void AStrokeJoinsTheBlockItPlacedBeforeNotAnOldRoadBlockInTheColumn()
        {
            //The new b2f stands at [4, -1] shift 1 (base -8); the old flat a1 is still at [4, 0] (base 0)
            EditedLevel level = Row(("a1", 3, 0, 0), ("a1", 4, 0, 0), ("b2f", 4, -1, 1));
            PlacedBlock placed = level.BlockAt(Layer.Middle, new Cell(4, -1));

            //Without it the nearest in height wins, the old one: the climb breaks
            int shift = RoadShift(level, new Cell(5, 0), "b2f", 0, false, out Cell cell);
            Assert.Equal(new Cell(5, 0), cell);
            Assert.Equal(-4, shift);

            //With the block of the stroke it goes on from the surface of that one: left edge -8, base -12
            shift = RoadShift(level, new Cell(5, 0), "b2f", 0, false, placed, out cell);
            Assert.Equal(new Cell(5, -1), cell);
            Assert.Equal(-3, shift);
            Assert.Null(Place(level, Layer.Middle, cell, "b2f", shift));
            Assert.Equal(-8, level.SurfaceRight(placed));
            Assert.Equal(-8, level.SurfaceLeft(level.BlockAt(Layer.Middle, cell)));
        }

        [Fact]
        public void ABlockOfAnotherColumnOrOneErasedIsNotTheJoinedNeighbour()
        {
            EditedLevel level = Row(("a1", 0, 0, 0), ("a1", 1, 0, 0), ("a1", 3, 4, 0));
            PlacedBlock far = level.BlockAt(Layer.Middle, new Cell(3, 4));
            //Not in the column next to the block: the neighbour is found by the heights as usual
            int shift = RoadShift(level, new Cell(2, 0), "a1", 0, false, far, out Cell cell);
            Assert.Equal(new Cell(2, 0), cell);
            Assert.Equal(0, shift);
            //Erased since, with no other road next to the block: the chosen shift
            PlacedBlock gone = level.BlockAt(Layer.Middle, new Cell(1, 0));
            Assert.True(level.Erase(Layer.Middle, gone));
            Assert.True(level.Erase(Layer.Middle, far));
            shift = RoadShift(level, new Cell(2, 0), "a1", 3, false, gone, out cell);
            Assert.Equal(new Cell(2, 0), cell);
            Assert.Equal(3, shift);
        }

        [Fact]
        public void TheOldRoadBlockAtTheCellPointedAtIsReplacedWhenTheNewOneGoesToAnotherCell()
        {
            EditedLevel level = Row(("a1", 3, 0, 0), ("a1", 4, 0, 0), ("a1", 4, 5, 0), ("a1", 4, -6, 0));
            //The climb block goes to [4, -1] shift 1, the mouse is at [4, 0]: the flat block there is the one to go
            PlacedBlock replaced = level.RoadBlockReplaced(new Cell(4, 0), new Cell(4, -1), 1);
            Assert.Equal(new Cell(4, 0), replaced.Cell);
            //Pointing at an empty cell, the road block within a cell of the new height goes; a road far below or above (a bridge) stays
            replaced = level.RoadBlockReplaced(new Cell(4, 2), new Cell(4, -1), 1);
            Assert.Equal(new Cell(4, 0), replaced.Cell);
            //The block of the cell it is placed at is replaced by the placing, and nothing else is near
            Assert.Null(level.RoadBlockReplaced(new Cell(4, 0), new Cell(4, 0), -4));
            Assert.Null(level.RoadBlockReplaced(new Cell(4, 2), new Cell(4, 2), 0));
            //No road in the column at all
            Assert.Null(level.RoadBlockReplaced(new Cell(9, 0), new Cell(9, -1), 1));
        }

        [Fact]
        public void TheGapsAreReportedAfterTheProblemsThatStopTheLevelFromBeingPlayed()
        {
            EditedLevel level = Row(("a1", 1, 0, 0), ("a1", 2, 0, 3));
            List<string> messages = level.Problems().Select(problem => problem.Message).ToList();
            int finish = messages.IndexOf("The level has no finish.");
            int gap = messages.FindIndex(message => message.StartsWith(GapMessage, StringComparison.Ordinal));
            Assert.True(finish >= 0 && gap > finish);
            Assert.Equal(messages.Count - 1, gap);
            Assert.Contains(messages, message => message.StartsWith("There is no block of the middle layer at the start cell", StringComparison.Ordinal));
        }
    }
}
