using System.Collections.Generic;
using System.Linq;
using TW3D.Editor;
using TW3D.Shared;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>
    /// The styles of the track in the document of the level editor (#58): the style of the level and of the blocks are
    /// set with undo, a block without a look in a style keeps its own, and both are saved and read back.
    /// </summary>
    public class EditedLevelStyleTests
    {
        private static readonly Catalog Catalog = Repository.LoadCatalog();

        [Fact]
        public void TheStyleOfTheLevelIsSetWithUndo()
        {
            EditedLevel level = EditedLevel.New(Catalog);
            level.SetStyle("offroad");
            Assert.Equal("offroad", level.Style);
            Assert.Equal("offroad", level.ToData().Style);
            Assert.Equal("Style", level.UndoName);
            level.Undo();
            Assert.Null(level.Style);
            Assert.DoesNotContain("style", LevelFile.ToJson(level.ToData()));
        }

        [Fact]
        public void OnlyTheBlocksWithALookTakeAStyleInOneStep()
        {
            EditedLevel level = EditedLevel.New(Catalog);
            Assert.Null(level.Place(Layer.Middle, new Cell(1, 0), "xa1", 0));
            List<PlacedBlock> blocks = level.Blocks(Layer.Middle).ToList();

            Assert.Equal(1, level.StyleBlocks(Layer.Middle, blocks, "offroad-classic"));
            Assert.Equal("offroad-classic", level.BlockAt(Layer.Middle, new Cell(0, 0)).Style);
            Assert.Null(level.BlockAt(Layer.Middle, new Cell(1, 0)).Style);
            Assert.Contains("{\"block\":\"a1\",\"x\":0,\"y\":0,\"shift\":0,\"style\":\"offroad-classic\"}", LevelFile.ToJson(level.ToData()));
            Assert.DoesNotContain(level.Problems(), problem => problem.ToString().Contains("style"));

            level.Undo();
            Assert.Null(level.BlockAt(Layer.Middle, new Cell(0, 0)).Style);
        }

        [Fact]
        public void TheStylesAreReadBackAsTheyWereSaved()
        {
            EditedLevel level = EditedLevel.New(Catalog);
            level.SetStyle("offroad");
            level.StyleBlocks(Layer.Middle, level.Blocks(Layer.Middle).ToList(), Catalog.ClassicStyle);
            string json = LevelFile.ToJson(level.ToData());
            EditedLevel read = new EditedLevel(Catalog, LevelFile.Parse(json), null);
            Assert.Equal("offroad", read.Style);
            Assert.Equal(Catalog.ClassicStyle, read.BlockAt(Layer.Middle, new Cell(0, 0)).Style);
            Assert.Equal(json, LevelFile.ToJson(read.ToData()));
        }

        [Fact]
        public void PlacingABlockKeepsTheStyleItIsGiven()
        {
            EditedLevel level = EditedLevel.New(Catalog);
            Assert.Null(level.Place(Layer.Middle, new Cell(1, 0), "a1", 0, false, "offroad"));
            Assert.Equal("offroad", level.BlockAt(Layer.Middle, new Cell(1, 0)).Style);
            //The same block again with another style changes only the style
            Assert.Null(level.Place(Layer.Middle, new Cell(1, 0), "a1", 0, false, null));
            Assert.Null(level.BlockAt(Layer.Middle, new Cell(1, 0)).Style);
            level.Undo();
            Assert.Equal("offroad", level.BlockAt(Layer.Middle, new Cell(1, 0)).Style);
        }

        [Fact]
        public void TheEditorNamesTheStylesAndOffersTheTransitions()
        {
            Assert.Equal("Off-road", StyleNames.Name("offroad"));
            Assert.Equal("Off-road to classic", StyleNames.Name("offroad-classic"));
            Assert.Equal("The level's", StyleNames.Name(null));
            Assert.Equal(new string[] { null, "classic", "offroad", "classic-offroad", "offroad-classic" }, StyleNames.BlockStyles(Catalog).ToArray());
            Assert.All(StyleNames.BlockStyles(Catalog).Where(style => style != null), style => Assert.True(Catalog.IsStyleOrTransition(style)));
        }
    }
}
