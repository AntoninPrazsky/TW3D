using System.Collections.Generic;
using System.Linq;
using TW3D.Editor;
using TW3D.Shared;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>
    /// The layers behind the road (issue #40): the depth of the back blocks in the level file, what the game checks of
    /// it, and the editor's layers, which load and save them, count them and move a selection between the layers.
    /// </summary>
    public class BackLayerTests
    {
        private const string Road = "a1";
        private const string Decoration = "kea1a";
        private static readonly Catalog Catalog = Repository.LoadCatalog();

        private static string Json(EditedLevel level) => LevelFile.ToJson(level.ToData());

        /// <summary>A new level with a road block beside the start and a decoration in each back layer at [3, 0].</summary>
        private static EditedLevel WithBackLayers()
        {
            EditedLevel level = EditedLevel.New(Catalog);
            Assert.Null(level.Place(Layer.Middle, new Cell(1, 0), Road, 0));
            Assert.Null(level.Place(Layer.Back, new Cell(3, 0), Decoration, 0));
            Assert.Null(level.Place(Layer.Back2, new Cell(3, 0), Road, 1));
            Assert.Null(level.Place(Layer.Back3, new Cell(3, 0), Decoration, 2));
            return level;
        }

        [Fact]
        public void TheLayersRunFromTheBackToTheFront()
        {
            Assert.Equal(new[] { Layer.Back3, Layer.Back2, Layer.Back, Layer.Middle, Layer.Front }, Layers.All);
            Assert.Equal(new[] { -3, -2, -1, 0, 1 }, Layers.All.Select(layer => layer.Cells()));
            Assert.Equal(new[] { "back 3", "back 2", "back", "middle", "front" }, Layers.All.Select(layer => layer.Name()));
            Assert.Equal("Back 2", Layer.Back2.Title());
            Assert.Equal(LevelData.BackLayers, Layers.All.Count(layer => layer.IsBack()));
            Assert.Equal(new[] { Layer.Back, Layer.Back2, Layer.Back3 }, new[] { 1, 2, 3 }.Select(Layers.OfBackDepth));
            Assert.Equal(Layer.Back3, Layers.OfBackDepth(7));
            Assert.Equal(Layer.Back, Layers.OfBackDepth(0));
        }

        [Fact]
        public void TheDepthIsWrittenOnlyForTheDeeperBackLayers()
        {
            string json = Json(WithBackLayers());
            Assert.Contains("{\"block\":\"kea1a\",\"x\":3,\"y\":0,\"shift\":0}", json);
            Assert.Contains("{\"block\":\"a1\",\"x\":3,\"y\":0,\"shift\":1,\"depth\":2}", json);
            Assert.Contains("{\"block\":\"kea1a\",\"x\":3,\"y\":0,\"shift\":2,\"depth\":3}", json);
            Assert.DoesNotContain("\"depth\":1", json);

            LevelData data = LevelFile.Parse(json);
            Assert.Equal(new[] { 1, 2, 3 }, data.Back.Select(block => block.Depth));
            Assert.All(data.Middle.Concat(data.Front), block => Assert.Equal(1, block.Depth));
            Assert.Empty(data.Validate(Catalog));
        }

        [Fact]
        public void TheEditorReadsTheBackLayersByDepthAndWritesThemBackUnchanged()
        {
            string json = Json(WithBackLayers());
            EditedLevel read = new EditedLevel(Catalog, LevelFile.Parse(json), null);
            Assert.Equal(new[] { Decoration }, read.Blocks(Layer.Back).Select(block => block.Name));
            Assert.Equal(new[] { (Road, 1) }, read.Blocks(Layer.Back2).Select(block => (block.Name, block.Shift)));
            Assert.Equal(new[] { (Decoration, 2) }, read.Blocks(Layer.Back3).Select(block => (block.Name, block.Shift)));
            Assert.Equal(json, Json(read));
        }

        [Fact]
        public void ALevelWithoutDepthsHasOnlyTheFirstBackLayer()
        {
            LevelData data = LevelFile.Parse("{\"format\":2,\"middle\":[{\"block\":\"a1\",\"x\":0,\"y\":0,\"shift\":0}],"
                + "\"back\":[{\"block\":\"kea1a\",\"x\":0,\"y\":0,\"shift\":0}],\"front\":[],\"objects\":[]}");
            Assert.Equal(1, data.Back.Single().Depth);
            EditedLevel level = new EditedLevel(Catalog, data, null);
            Assert.Single(level.Blocks(Layer.Back));
            Assert.Empty(level.Blocks(Layer.Back2));
            Assert.Empty(level.Blocks(Layer.Back3));
        }

        [Fact]
        public void ADepthOutOfRangeIsAProblem()
        {
            LevelData data = new LevelData();
            data.Middle.Add(new BlockPlacement { Block = Road, X = 0, Y = 0, Finish = true });
            data.Back.Add(new BlockPlacement { Block = Decoration, X = 1, Y = 0, Depth = LevelData.BackLayers + 1 });
            data.Back.Add(new BlockPlacement { Block = Decoration, X = 2, Y = 0, Depth = 0 });
            data.Front.Add(new BlockPlacement { Block = Decoration, X = 3, Y = 0, Depth = 2 });
            List<string> problems = data.Validate(Catalog);
            Assert.Equal(3, problems.Count);
            Assert.Contains(problems, problem => problem.Contains("[1, 0]") && problem.Contains("depth 4"));
            Assert.Contains(problems, problem => problem.Contains("[2, 0]") && problem.Contains("depth 0"));
            Assert.Contains(problems, problem => problem.Contains("[3, 0]") && problem.Contains("only the blocks of the back layer"));
        }

        [Fact]
        public void ThePropsBelongToTheLayersByDepth()
        {
            Assert.Equal(new[] { -3, -3, -2, -2, -1, -1, 0, 0, 0, 1, 1 },
                new[] { -100f, -60f, -56.25f, -40f, -33.75f, -11.3f, -11.25f, 0f, 11.25f, 11.3f, 40f }.Select(PlacedScenery.LayerCells)
                    .Select(cells => Layers.OfCells(cells).Cells()));
        }

        [Fact]
        public void TheReportCountsTheDeeperBackLayersWhenTheyHaveBlocks()
        {
            LevelReport report = new LevelReport();
            EditedLevel level = WithBackLayers();
            report.Update(level);
            Assert.Equal("Blocks: 2 middle, 1 back, 1 back 2, 1 back 3, 0 front", report.BlocksText);
        }

        [Fact]
        public void ASelectionMovesToAnotherLayerWithItsPropsAndBackAgainByUndo()
        {
            EditedLevel level = WithBackLayers();
            Assert.Null(level.Place(Layer.Back, new Cell(4, 0), Road, 0));
            Assert.Null(level.AddScenery("cone", 3f * Units.GridCell, -1.25f, -25f, 0));
            PlacedScenery cone = level.Scenery.Single();
            PlacedBlock kerb = level.BlockAt(Layer.Back, new Cell(3, 0));
            PlacedBlock road = level.BlockAt(Layer.Back, new Cell(4, 0));
            PlacedBlock displaced = level.BlockAt(Layer.Back2, new Cell(3, 0));
            string before = Json(level);

            Assert.Null(level.MoveToLayer(Layer.Back, new[] { kerb, road }, new[] { cone }, Layer.Back2));

            Assert.Empty(level.Blocks(Layer.Back));
            //The block that was in a cell they came to is erased; they go after the others, in the order given
            Assert.Equal(new[] { kerb, road }, level.Blocks(Layer.Back2));
            Assert.Equal(-47.5f, cone.Z);
            Assert.Equal(Layer.Back2, cone.Layer);
            Assert.Equal("Move to the back 2 layer", level.UndoName);

            level.Undo();
            Assert.Equal(before, Json(level));
            Assert.Equal(new[] { kerb, road }, level.Blocks(Layer.Back));
            Assert.Same(displaced, level.BlockAt(Layer.Back2, new Cell(3, 0)));
            level.Redo();
            Assert.Equal(new[] { kerb, road }, level.Blocks(Layer.Back2));
        }

        [Fact]
        public void TheMiddleLayerTakesNoDecorationAndKeepsTheFinish()
        {
            EditedLevel level = WithBackLayers();
            int version = level.Version;
            PlacedBlock decoration = level.BlockAt(Layer.Back, new Cell(3, 0));
            Assert.Contains("decoration", level.MoveToLayer(Layer.Back, new[] { decoration }, new PlacedScenery[0], Layer.Middle));

            Assert.Null(level.SetFinish(Layer.Middle, new Cell(1, 0)));
            version = level.Version;
            PlacedBlock finish = level.BlockAt(Layer.Middle, new Cell(1, 0));
            Assert.Contains("finish", level.MoveToLayer(Layer.Middle, new[] { finish }, new PlacedScenery[0], Layer.Back));
            Assert.Equal(version, level.Version);

            //A road block may go into any layer, and back into the middle one
            PlacedBlock start = level.BlockAt(Layer.Middle, new Cell(0, 0));
            Assert.Null(level.MoveToLayer(Layer.Middle, new[] { start }, new PlacedScenery[0], Layer.Front));
            Assert.Same(start, level.BlockAt(Layer.Front, new Cell(0, 0)));
            Assert.Null(level.MoveToLayer(Layer.Front, new[] { start }, new PlacedScenery[0], Layer.Middle));
            Assert.Same(start, level.BlockAt(Layer.Middle, new Cell(0, 0)));
        }

        [Fact]
        public void TheProblemsOfTheDeeperLayersNameThem()
        {
            LevelData data = LevelFile.Parse(Json(WithBackLayers()));
            data.Back.Add(new BlockPlacement { Block = "no-such-block", X = 5, Y = 0, Depth = 3 });
            EditedLevel level = new EditedLevel(Catalog, data, null);
            LevelProblem problem = level.Problems().Single(found => found.Message.Contains("no-such-block"));
            Assert.Contains("in the back 3 layer", problem.Message);
            Assert.Equal(Layer.Back3, problem.Layer);
        }
    }
}
