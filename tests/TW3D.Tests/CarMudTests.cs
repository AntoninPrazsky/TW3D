using TW3D.GameObjects;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>
    /// How muddy the car gets (#60): clean off the earth, muddier the longer and the wilder it drives on it, the wheels of
    /// one axle by themselves, the body less than the wheels, half as much on a transition, and never past all of it.
    /// </summary>
    public class CarMudTests
    {
        //Drives one wheel of an axle for a while in steps of a frame
        private static void Drive(CarMud mud, float seconds, bool front = true, float earth = 1f, float roll = 0f, float slide = 0f)
        {
            const float Frame = 1f / 60f;
            for (float time = 0f; time < seconds; time += Frame)
                mud.Wheel(front, earth, roll, slide, 0f, Frame);
        }

        [Fact]
        public void ACarStartsClean()
        {
            CarMud mud = new CarMud();
            Assert.Equal(0f, mud.Front);
            Assert.Equal(0f, mud.Rear);
            Assert.Equal(0f, mud.Body);
        }

        [Fact]
        public void NoMudOffTheEarth()
        {
            CarMud mud = new CarMud();
            Drive(mud, 30f, earth: 0f, roll: 1f, slide: 1f);
            mud.Wheel(true, 0f, 1f, 1f, 1f, 0.1f);
            Assert.Equal(0f, mud.Front);
            Assert.Equal(0f, mud.Body);
        }

        [Fact]
        public void TheMudGrowsWithTheTimeOnTheEarth()
        {
            CarMud mud = new CarMud();
            Drive(mud, 5f, roll: 0.5f);
            float early = mud.Front;
            Drive(mud, 5f, roll: 0.5f);
            Assert.True(early > 0f);
            Assert.True(mud.Front > early);
        }

        [Fact]
        public void RollingFastAndSlidingMuddyMore()
        {
            CarMud slow = new CarMud(), fast = new CarMud(), sliding = new CarMud();
            Drive(slow, 5f, roll: 0.1f);
            Drive(fast, 5f, roll: 1f);
            Drive(sliding, 5f, roll: 0.1f, slide: 1f);
            Assert.True(fast.Front > slow.Front);
            Assert.True(sliding.Front > fast.Front);
        }

        [Fact]
        public void AHardLandingSplashesAtOnce()
        {
            CarMud soft = new CarMud(), hard = new CarMud();
            soft.Wheel(false, 1f, 0f, 0f, 0.1f, 0f);
            hard.Wheel(false, 1f, 0f, 0f, 1f, 0f);
            Assert.True(hard.Rear > soft.Rear);
            Assert.Equal(CarMud.LandShare, hard.Rear, 5);
        }

        [Fact]
        public void EachAxleCollectsItsOwnMud()
        {
            CarMud mud = new CarMud();
            Drive(mud, 5f, front: false, roll: 1f);
            Assert.Equal(0f, mud.Front);
            Assert.True(mud.Rear > 0f);
        }

        [Fact]
        public void TheBodyGetsLessThanTheWheels()
        {
            CarMud mud = new CarMud();
            Drive(mud, 10f, roll: 1f);
            Assert.True(mud.Body > 0f);
            Assert.True(mud.Body < mud.Front);
        }

        [Fact]
        public void ATransitionGivesLessMud()
        {
            CarMud earth = new CarMud(), transition = new CarMud();
            Drive(earth, 5f, roll: 1f);
            Drive(transition, 5f, earth: 0.5f, roll: 1f);
            Assert.True(transition.Front > 0f);
            Assert.True(transition.Front < earth.Front);
        }

        [Fact]
        public void TheMudNeverGetsPastAllOfIt()
        {
            CarMud mud = new CarMud();
            Drive(mud, 600f, roll: 1f, slide: 1f);
            for (int i = 0; i < 100; i++)
                mud.Wheel(true, 1f, 1f, 1f, 1f, 1f);
            Assert.InRange(mud.Front, 0.99f, 1f);
            Assert.InRange(mud.Body, 0.9f, 1f);
        }

        [Fact]
        public void TheDevelopmentKeySetsEveryAmount()
        {
            CarMud mud = new CarMud();
            mud.Set(0.75f);
            Assert.Equal(0.75f, mud.Front);
            Assert.Equal(0.75f, mud.Rear);
            Assert.Equal(0.75f, mud.Body);
            mud.Set(2f);
            Assert.Equal(1f, mud.Body);
        }
    }
}
