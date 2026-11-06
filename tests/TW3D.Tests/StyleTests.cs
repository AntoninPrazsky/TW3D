using System.Collections.Generic;
using System.IO;
using System.Linq;
using TW3D.Shared;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>
    /// The styles of the track (#58): the looks of the blocks in the catalog name styles and models that are built, a look
    /// shares the collision of its block (only the model differs), and a level and a placed block have a style that the
    /// level file keeps and the validation checks.
    /// </summary>
    public class StyleTests
    {
        private readonly Catalog catalog = Repository.LoadCatalog();

        private IEnumerable<(BlockType Block, string Style, string Model)> Looks()
        {
            return catalog.Blocks.Where(block => block.Looks != null)
                .SelectMany(block => block.Looks.Select(look => (block, look.Key, look.Value)));
        }

        [Fact]
        public void TheOffroadStyleHasItsLooksAndTransitions()
        {
            Assert.Contains("offroad", catalog.Styles);
            BlockType road = catalog.FindBlock("a1");
            Assert.True(road.HasLook("offroad"));
            Assert.True(road.HasLook("offroad-classic"));
            Assert.True(road.HasLook("classic-offroad"));
            Assert.True(road.HasLook(null));
            Assert.True(road.HasLook(Catalog.ClassicStyle));
            Assert.False(catalog.FindBlock("sg").HasLook("offroad"));
        }

        [Fact]
        public void EveryLookNamesAStyleOrATransitionOfTwo()
        {
            Assert.NotEmpty(Looks());
            Assert.All(Looks(), look => Assert.True(catalog.IsStyleOrTransition(look.Style), look.Block.Name + ": " + look.Style));
            Assert.False(catalog.IsStyleOrTransition("offroad-offroad"));
            Assert.False(catalog.IsStyleOrTransition("gravel"));
            Assert.False(catalog.IsStyleOrTransition("offroad-classic-offroad"));
        }

        [Fact]
        public void EveryLookIsAModelThatIsBuilt()
        {
            HashSet<string> built = Repository.BuiltContent();
            List<string> missing = Looks().Select(look => look.Block.LookAsset(look.Style))
                .Where(model => !File.Exists(Path.Combine(Repository.Content, model + ".obj")) || !built.Contains(model + ".obj"))
                .ToList();
            Assert.Empty(missing);
        }

        /// <summary>
        /// A look is drawn with its own model but collides as its block (the physics takes the shape of the block's model),
        /// so the look must not be a block of its own nor have a shape of its own that could be taken by mistake.
        /// </summary>
        [Fact]
        public void ALookSharesTheCollisionOfItsBlock()
        {
            CollisionShapes shapes = Repository.LoadCollisionShapes();
            Assert.All(Looks(), look =>
            {
                Assert.NotNull(shapes.Find(look.Block.ModelAsset));
                Assert.Null(shapes.Find(look.Block.LookAsset(look.Style)));
                Assert.Null(catalog.FindBlock(look.Model));
            });
            BlockType road = catalog.FindBlock("a1");
            Assert.Equal("Levels/mod/a1", road.LookAsset(null));
            Assert.Equal("Levels/mod/a1", road.LookAsset("classic"));
            Assert.Equal("Levels/mod/a1o", road.LookAsset("offroad"));
            Assert.Equal("Levels/mod/sg", catalog.FindBlock("sg").LookAsset("offroad"));
        }

        [Fact]
        public void TheFileKeepsTheStyleOfTheLevelAndOfABlock()
        {
            LevelData level = new LevelData { Par = 10f, Style = "offroad" };
            level.Middle.Add(new BlockPlacement { Block = "a1", X = 0, Y = 0 });
            level.Middle.Add(new BlockPlacement { Block = "a1", X = 1, Y = 0, Style = "offroad-classic", Finish = true });
            string json = LevelFile.ToJson(level);
            Assert.Contains("  \"par\": 10,\n  \"style\": \"offroad\",\n  \"middle\": [", json);
            Assert.Contains("    {\"block\":\"a1\",\"x\":0,\"y\":0,\"shift\":0},\n", json);
            Assert.Contains("    {\"block\":\"a1\",\"x\":1,\"y\":0,\"shift\":0,\"finish\":true,\"style\":\"offroad-classic\"}\n", json);

            LevelData read = LevelFile.Parse(json);
            Assert.Equal("offroad", read.Style);
            Assert.Equal("offroad", read.StyleOf(read.Middle[0]));
            Assert.Equal("offroad-classic", read.StyleOf(read.Middle[1]));
            Assert.Empty(read.Validate(catalog));
        }

        [Fact]
        public void ALevelWithoutAStyleIsWrittenAsBefore()
        {
            LevelData level = new LevelData();
            level.Middle.Add(new BlockPlacement { Block = "a1", X = 0, Y = 0 });
            Assert.DoesNotContain("style", LevelFile.ToJson(level));
            Assert.Null(level.StyleOf(level.Middle[0]));
        }

        [Fact]
        public void AnUnknownStyleOrAStyleWithoutALookIsAProblem()
        {
            LevelData level = LevelFile.Parse("{\"format\":2,\"style\":\"gravel\",\"middle\":["
                + "{\"block\":\"a1\",\"x\":0,\"y\":0,\"shift\":0,\"style\":\"mud\"},"
                + "{\"block\":\"xa1\",\"x\":1,\"y\":0,\"shift\":0,\"style\":\"offroad\"}]}");
            Assert.Equal(new[]
            {
                "The level has the unknown style \"gravel\".",
                "Block \"a1\" at [0, 0] in the middle layer has the unknown style \"mud\".",
                "Block \"xa1\" at [1, 0] in the middle layer has the style \"offroad\", but no look in it."
            }, level.Validate(catalog));
        }

        /// <summary>A block without a look in the style of the level is drawn classic: that is no problem.</summary>
        [Fact]
        public void TheStyleOfTheLevelLeavesTheBlocksWithoutALookClassic()
        {
            LevelData level = LevelFile.Parse("{\"format\":2,\"style\":\"offroad\",\"middle\":[{\"block\":\"xa1\",\"x\":0,\"y\":0,\"shift\":0}]}");
            Assert.Empty(level.Validate(catalog));
            Assert.Equal("Levels/mod/xa1", catalog.FindBlock("xa1").LookAsset(level.StyleOf(level.Middle[0])));
        }
    }
}
