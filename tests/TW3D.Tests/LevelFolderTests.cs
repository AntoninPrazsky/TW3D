using System;
using System.Collections.Generic;
using System.IO;
using TW3D.Editor;
using TW3D.Shared;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>
    /// The levels folder of the editor (Editor/LevelFolder): which folder it is, the level argument of --editor, the names
    /// typed in Save as, the list of the Open dialog, the generated levels and the copy into the build output. Every test
    /// works in a temporary folder of its own, with a fake repository where it needs one.
    /// </summary>
    public sealed class LevelFolderTests : IDisposable
    {
        private const string LevelText = "{\n  \"format\": 2,\n  \"order\": 3,\n  \"mood\": \"night\",\n  \"middle\": [\n    {\"block\":\"a1\",\"x\":0,\"y\":0,\"shift\":0},\n    {\"block\":\"a1\",\"x\":1,\"y\":0,\"shift\":0}\n  ],\n  \"back\": [],\n  \"front\": [],\n  \"objects\": []\n}\n";

        private readonly string root = Path.Combine(Path.GetTempPath(), "TW3D-LevelFolderTests-" + Guid.NewGuid().ToString("N"));

        public LevelFolderTests()
        {
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        /// <summary>A fake repository in the temporary folder: TW3D.slnx, content/Levels, the program in src/bin, the application data apart.</summary>
        private (string Repository, string Levels, string Program, string LocalData) MakeRepository()
        {
            string repository = Path.Combine(root, "repo");
            string levels = Path.Combine(repository, "content", "Levels");
            string program = Path.Combine(repository, "src", "TW3D.Game", "bin", "Debug");
            Directory.CreateDirectory(levels);
            Directory.CreateDirectory(program);
            File.WriteAllText(Path.Combine(repository, "TW3D.slnx"), "<Solution />");
            return (repository, levels, program, Path.Combine(root, "local"));
        }

        [Fact]
        public void TheRepositoryLevelsAreTheDefault()
        {
            (string repository, string levels, string program, string localData) = MakeRepository();
            LevelFolder folder = LevelFolder.Find(null, program, localData);
            Assert.True(LevelFolder.SamePath(levels, folder.FullPath));
            Assert.True(folder.IsRepository);
            Assert.True(LevelFolder.SamePath(repository, folder.Repository));
            Assert.True(LevelFolder.SamePath(Path.Combine(program, "Content", "Levels"), folder.BuildLevels));
        }

        [Fact]
        public void TheLevelsArgumentWins()
        {
            (string _, string levels, string program, string localData) = MakeRepository();
            string scratch = Path.Combine(root, "scratch");
            LevelFolder folder = LevelFolder.Find(scratch, program, localData);
            Assert.True(LevelFolder.SamePath(scratch, folder.FullPath));
            Assert.False(folder.IsRepository);

            //The repository's folder given by the argument is still the repository's
            Assert.True(LevelFolder.Find(levels + Path.DirectorySeparatorChar, program, localData).IsRepository);
        }

        [Fact]
        public void WithoutARepositoryTheLevelsAreInTheApplicationData()
        {
            string program = Path.Combine(root, "game");
            string localData = Path.Combine(root, "local");
            Directory.CreateDirectory(program);
            LevelFolder folder = LevelFolder.Find(null, program, localData);
            Assert.True(LevelFolder.SamePath(Path.Combine(localData, "TW3D", "Levels"), folder.FullPath));
            Assert.Null(folder.Repository);
            Assert.False(folder.IsRepository);
            Assert.False(Directory.Exists(folder.FullPath));
        }

        [Fact]
        public void TheLevelArgumentIsAFileOrANameOfTheFolder()
        {
            LevelFolder folder = new LevelFolder(Path.Combine(root, "levels"), null, null);
            string other = Path.Combine(root, "elsewhere.level");
            File.WriteAllText(other, "");
            Assert.Equal(other, folder.Resolve(other));
            Assert.Equal(Path.Combine(folder.FullPath, "lock.json"), folder.Resolve("lock"));
            Assert.Equal(Path.Combine(folder.FullPath, "lock.json"), folder.Resolve("lock.json"));
            Assert.Equal(Path.Combine(folder.FullPath, "lock.json"), folder.FileOf("lock"));
        }

        [Theory]
        [InlineData("mytest", "mytest")]
        [InlineData("  mytest  ", "mytest")]
        [InlineData("mytest.json", "mytest")]
        [InlineData("mytest.JSON", "mytest")]
        [InlineData("my test", "my test")]
        [InlineData("my.level", "my.level")]
        [InlineData("console", "console")]
        [InlineData("com10", "com10")]
        [InlineData("lpt0", "lpt0")]
        [InlineData("my nul", "my nul")]
        public void GoodNamesAreTakenWithoutTheExtension(string typed, string name)
        {
            Assert.Null(LevelFolder.CheckName(typed));
            Assert.Equal(name, LevelFolder.NormalName(typed));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(".json")]
        [InlineData(".")]
        [InlineData("..")]
        [InlineData("..json")]
        [InlineData("a/b")]
        [InlineData("a\\b")]
        [InlineData("a:b")]
        [InlineData("a*b")]
        [InlineData("a?b")]
        [InlineData("a\"b")]
        [InlineData("a<b>")]
        [InlineData("a|b")]
        [InlineData("a\tb")]
        [InlineData("nul")]
        [InlineData("NUL.json")]
        [InlineData("con")]
        [InlineData("Aux")]
        [InlineData("prn")]
        [InlineData("com1")]
        [InlineData("COM9")]
        [InlineData("lpt1")]
        [InlineData("nul.backup")]
        [InlineData("con .old")]
        public void BadNamesAreRefusedWithAReason(string typed)
        {
            string reason = LevelFolder.CheckName(typed);
            Assert.False(string.IsNullOrWhiteSpace(reason));
        }

        [Fact]
        public void TheListHasEveryLevelByNameAndTheUnreadableOnes()
        {
            string levels = Path.Combine(root, "levels");
            Directory.CreateDirectory(levels);
            File.WriteAllText(Path.Combine(levels, "b.json"), LevelText);
            File.WriteAllText(Path.Combine(levels, "a.json"), "{\"format\": 2}");
            File.WriteAllText(Path.Combine(levels, "c.json"), "{ not json");
            File.WriteAllText(Path.Combine(levels, "notes.txt"), "");

            List<LevelEntry> entries = new LevelFolder(levels, null, null).List(out string error);
            Assert.Null(error);
            Assert.Equal(new[] { "a", "b", "c" }, entries.ConvertAll(entry => entry.Name));

            Assert.True(entries[0].Readable);
            Assert.Null(entries[0].Order);
            Assert.Equal(0, entries[0].Blocks);

            Assert.True(entries[1].Readable);
            Assert.Equal(3, entries[1].Order);
            Assert.Equal(LevelMood.Night, entries[1].Mood);
            Assert.Equal(2, entries[1].Blocks);
            Assert.Equal(Path.Combine(new LevelFolder(levels, null, null).FullPath, "b.json"), entries[1].File);

            Assert.False(entries[2].Readable);
            Assert.False(string.IsNullOrEmpty(entries[2].Error));
        }

        [Fact]
        public void AMissingFolderHasNoLevels()
        {
            Assert.Empty(new LevelFolder(Path.Combine(root, "missing"), null, null).List(out string error));
            Assert.Null(error);
        }

        [Fact]
        public void OnlyPngPreviewsAreFound()
        {
            string levels = Path.Combine(root, "levels");
            Directory.CreateDirectory(Path.Combine(levels, "levimgs"));
            File.WriteAllText(Path.Combine(levels, "levimgs", "new.png"), "");
            File.WriteAllText(Path.Combine(levels, "levimgs", "old.xnb"), "");
            LevelFolder folder = new LevelFolder(levels, null, null);
            Assert.Equal(Path.Combine(folder.FullPath, "levimgs", "new.png"), folder.Preview("new"));
            Assert.Null(folder.Preview("old"));
            Assert.Null(folder.Preview("none"));
        }

        [Fact]
        public void GeneratedLevelsOfTheRepositoryAreKnown()
        {
            (string repository, string levels, string program, string localData) = MakeRepository();
            Directory.CreateDirectory(Path.Combine(repository, "tools", "levelgen"));
            File.WriteAllText(Path.Combine(repository, "tools", "levelgen", "summit.js"), "");
            LevelFolder folder = LevelFolder.Find(null, program, localData);

            Assert.Equal("tools/levelgen/summit.js", folder.GeneratorOf(Path.Combine(levels, "summit.json")));
            Assert.Null(folder.GeneratorOf(Path.Combine(levels, "lock.json")));
            //A copy elsewhere is not the generated level
            Assert.Null(folder.GeneratorOf(Path.Combine(root, "scratch", "summit.json")));
            Assert.Null(folder.GeneratorOf(null));
        }

        [Fact]
        public void LevelsOfTheRepositoryAreCopiedToTheBuildOutput()
        {
            (string _, string levels, string program, string localData) = MakeRepository();
            LevelFolder folder = LevelFolder.Find(null, program, localData);
            string file = Path.Combine(levels, "mytest.json");
            File.WriteAllText(file, LevelText);

            string copy = folder.CopyToBuild(file);
            Assert.Equal(Path.Combine(folder.BuildLevels, "mytest.json"), copy);
            Assert.Equal(LevelText, File.ReadAllText(copy));

            //A file elsewhere is not copied
            string other = Path.Combine(root, "other.json");
            File.WriteAllText(other, LevelText);
            Assert.Null(folder.CopyToBuild(other));
        }

        [Fact]
        public void LevelsOfAnotherFolderAreNotCopied()
        {
            (string _, string _, string program, string localData) = MakeRepository();
            string scratch = Path.Combine(root, "scratch");
            Directory.CreateDirectory(scratch);
            LevelFolder folder = LevelFolder.Find(scratch, program, localData);
            string file = Path.Combine(scratch, "mytest.json");
            File.WriteAllText(file, LevelText);
            Assert.Null(folder.CopyToBuild(file));
            Assert.False(Directory.Exists(folder.BuildLevels));
        }

        [Fact]
        public void ContainsMeansRightInTheFolder()
        {
            LevelFolder folder = new LevelFolder(Path.Combine(root, "levels"), null, null);
            Assert.True(folder.Contains(Path.Combine(root, "levels", "a.json")));
            Assert.True(folder.Contains(Path.Combine(root, "LEVELS", "a.json")));
            Assert.False(folder.Contains(Path.Combine(root, "levels", "sub", "a.json")));
            Assert.False(folder.Contains(Path.Combine(root, "a.json")));
        }
    }
}
