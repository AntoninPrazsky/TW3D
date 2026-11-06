using System.Collections.Generic;
using System.Linq;
using TW3D.Shared;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>
    /// The looks of the road (Shared/RoadLooks, #58): a road in a style passes into the classic one by the look of a
    /// transition on the block of the styled side, turned as the block is, and keeps what a block says itself.
    /// </summary>
    public class RoadLooksTests
    {
        private static readonly Catalog Catalog = Repository.LoadCatalog();

        private static string[] Looks(string levelStyle, params RoadBlock[] blocks) => RoadLooks.Of(blocks, levelStyle, Catalog);

        private static RoadBlock Block(string name, int x, int shift = 0, bool turned = false, string style = null) =>
            new RoadBlock(name, x, 0, shift, turned, style);

        [Fact]
        public void ADirtRoadSinksIntoTheClassicBlocksAtItsEnds()
        {
            //xa1 (the start pad) has no off-road look: it stays classic, and the dirt road takes the transitions next to it
            Assert.Equal(new[] { "classic", "classic-offroad", "offroad", "offroad-classic", "classic" },
                Looks("offroad", Block("xa1", 0), Block("a1", 1), Block("a1", 2), Block("a1", 3), Block("xa1", 4)));
        }

        [Fact]
        public void ATurnedBlockTakesTheTransitionTheOtherWayRound()
        {
            //b2 descends 4 steps (profile [0, 4]); turned it climbs them, from the start pad at -4 to the road at -8
            Assert.Equal(new[] { "classic", "offroad-classic", "offroad" },
                Looks("offroad", Block("xa1", 0, -4), Block("b2", 1, -8, true), Block("a1", 2, -8)));
        }

        [Fact]
        public void TheClassicLevelHasNoTransitionsAndABlockOfItsOwnStyleGetsThem()
        {
            Assert.Equal(new[] { "classic", "classic", "classic" }, Looks(null, Block("a1", 0), Block("a1", 1), Block("a1", 2)));
            //The end of the road (nothing on the right) is no joint
            Assert.Equal(new[] { "classic", "classic-offroad", "offroad" },
                Looks(null, Block("a1", 0), Block("a1", 1, style: "offroad"), Block("a1", 2, style: "offroad")));
        }

        [Fact]
        public void AnOwnTransitionStaysAndALoneBlockMeetingTheClassicOnBothSidesKeepsItsStyle()
        {
            Assert.Equal(new[] { "classic", "offroad", "classic" },
                Looks(null, Block("a1", 0), Block("a1", 1, style: "offroad"), Block("a1", 2)));
            //A transition of its own ends the road in the classic style: the dirt road after it comes back by a transition
            Assert.Equal(new[] { "offroad", "offroad-classic", "classic-offroad" },
                Looks("offroad", Block("a1", 0), Block("a1", 1, style: "offroad-classic"), Block("a1", 2)));
        }

        [Fact]
        public void ARoadThatDoesNotJoinGetsNoTransition()
        {
            //The start pad 9 steps lower is not joined: a gap in the road, the dirt road ends as it is
            Assert.Equal(new[] { "classic", "offroad", "offroad" },
                Looks("offroad", Block("xa1", 0, 9), Block("a1", 1), Block("a1", 2)));
        }

        public static IEnumerable<object[]> OffroadLevels => Repository.LevelNames().Where(name => name.EndsWith("-offroad")).Select(name => new object[] { name });

        /// <summary>The off-road copies of the levels name no transition of their own: the road finds them.</summary>
        [Theory]
        [MemberData(nameof(OffroadLevels))]
        public void TheOffroadLevelsFindTheirTransitions(string level)
        {
            LevelData data = LevelFile.Load(Repository.LevelPath(level));
            Assert.Equal("offroad", data.Style);
            string[] looks = RoadLooks.Of(data.Middle.Select(RoadBlock.Of).ToList(), data.Style, Catalog);
            Assert.Contains(looks, look => look.Contains('-'));
            Assert.All(data.Middle, block => Assert.Null(block.Style));
        }
    }
}
