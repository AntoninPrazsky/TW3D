using System.Collections.Generic;
using System.IO;
using System.Linq;
using TW3D.Shared;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>The shipped levels (content/Levels): valid against the catalog, playable to an end, in their own format.</summary>
    public class LevelTests
    {
        public static IEnumerable<object[]> ShippedLevels => Repository.LevelNames().Select(name => new object[] { name });

        [Theory]
        [MemberData(nameof(ShippedLevels))]
        public void FitsTheCatalog(string level)
        {
            List<string> problems = LevelFile.Load(Repository.LevelPath(level)).Validate(Repository.LoadCatalog());
            Assert.Empty(problems);
        }

        [Theory]
        [MemberData(nameof(ShippedLevels))]
        public void HasAFinish(string level)
        {
            Assert.NotNull(LevelFile.Load(Repository.LevelPath(level)).FinishBlock);
        }

        /// <summary>The file is written as LevelFile writes it (the editor and the level generator), so a save changes nothing.</summary>
        [Theory]
        [MemberData(nameof(ShippedLevels))]
        public void IsInItsOwnFormat(string level)
        {
            string text = File.ReadAllText(Repository.LevelPath(level)).Replace("\r\n", "\n");
            Assert.Equal(text, LevelFile.ToJson(LevelFile.Parse(text)));
        }

        /// <summary>The level select shows a picture for it: a precompiled preview of 2016, or a PNG the content pipeline builds.</summary>
        [Theory]
        [MemberData(nameof(ShippedLevels))]
        public void HasAPreview(string level)
        {
            string previews = Path.Combine(Repository.Levels, "levimgs");
            bool precompiled = File.Exists(Path.Combine(previews, level + ".xnb"));
            bool built = File.Exists(Path.Combine(previews, level + ".png"))
                && Repository.BuiltContent().Contains("Levels/levimgs/" + level + ".png");
            Assert.True(precompiled || built, $"No preview of {level}: content/Levels/levimgs/{level}.png with a /build line in Content.mgcb");
        }

        [Fact]
        public void TheTrainingLevelsComeFirst()
        {
            Assert.Equal(new[] { "training1", "training2", "training3" }, Repository.LevelNames().Take(3));
        }
    }
}
