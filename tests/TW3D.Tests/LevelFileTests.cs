using System.IO;
using TW3D.Shared;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>
    /// The level file (Shared/LevelFile): what it reads and what it refuses, and how it writes a turned block.
    /// </summary>
    public class LevelFileTests
    {
        private const string Middle = "\"middle\":[{\"block\":\"b2\",\"x\":1,\"y\":0,\"shift\":-4,\"turned\":true}]";

        [Fact]
        public void ReadsATurnedBlock()
        {
            LevelData level = LevelFile.Parse("{\"format\":2," + Middle + "}");
            BlockPlacement block = Assert.Single(level.Middle);
            Assert.Equal("b2", block.Block);
            Assert.True(block.Turned);
            Assert.Empty(level.Validate(Repository.LoadCatalog()));
        }

        [Fact]
        public void WritesTheTurnOnlyOfATurnedBlockAfterTheShift()
        {
            LevelData level = new LevelData();
            level.Middle.Add(new BlockPlacement { Block = "a1", X = 0, Y = 0 });
            level.Middle.Add(new BlockPlacement { Block = "b2", X = 1, Y = 0, Shift = -4, Turned = true, Finish = true });
            string json = LevelFile.ToJson(level);
            Assert.StartsWith("{\n  \"format\": 2,\n", json);
            Assert.Contains("    {\"block\":\"a1\",\"x\":0,\"y\":0,\"shift\":0},\n", json);
            Assert.Contains("    {\"block\":\"b2\",\"x\":1,\"y\":0,\"shift\":-4,\"turned\":true,\"finish\":true}\n", json);
        }

        [Fact]
        public void RefusesALevelOfFormatOne()
        {
            //Format 1 had the mirrored f models; it is not converted on load
            InvalidDataException e = Assert.Throws<InvalidDataException>(() => LevelFile.Parse("{\"format\":1,\"middle\":[{\"block\":\"b2f\",\"x\":1,\"y\":0,\"shift\":-4}]}"));
            Assert.Contains("format 1", e.Message);
        }

        [Fact]
        public void RefusesALevelOfANewerFormat()
        {
            InvalidDataException e = Assert.Throws<InvalidDataException>(() => LevelFile.Parse("{\"format\":3," + Middle + "}"));
            Assert.Contains("format 3", e.Message);
        }

        [Fact]
        public void RefusesAFileThatIsNotJson()
        {
            //Such as a level of the 2016 version, which is no longer read
            Assert.Throws<InvalidDataException>(() => LevelFile.Parse("bl|a1|0|0|0|"));
            Assert.Throws<InvalidDataException>(() => LevelFile.Parse(""));
        }

        [Fact]
        public void ATurnedBlockThatDoesNotTurnIsAProblem()
        {
            LevelData level = LevelFile.Parse("{\"format\":2,\"middle\":[{\"block\":\"tu1v\",\"x\":3,\"y\":-1,\"shift\":0,\"turned\":true}]}");
            Assert.Equal(new[] { "Block \"tu1v\" at [3, -1] in the middle layer is turned, but it cannot be turned." },
                level.Validate(Repository.LoadCatalog()));
        }
    }
}
