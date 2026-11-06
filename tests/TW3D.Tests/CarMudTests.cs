using TW3D.GameObjects;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>
    /// How muddy the car gets (#60): clean off the earth, muddier the longer and the wilder it drives on it, the wheels of
    /// one axle by themselves, the treads faster and the body less than the wheels, half as much on a transition, never past
    /// all of it; off the earth the wheels wash clean, the treads first, while the body and the holders keep their mud.
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
            foreach (bool front in new[] { true, false })
            {
                Assert.Equal(0f, mud.Wheels(front));
                Assert.Equal(0f, mud.Treads(front));
                Assert.Equal(0f, mud.Holder(front));
            }
            Assert.Equal(0f, mud.Body);
        }

        [Fact]
        public void NoMudOffTheEarth()
        {
            CarMud mud = new CarMud();
            Drive(mud, 30f, earth: 0f, roll: 1f, slide: 1f);
            mud.Wheel(true, 0f, 1f, 1f, 1f, 0.1f);
            Assert.Equal(0f, mud.Wheels(true));
            Assert.Equal(0f, mud.Treads(true));
            Assert.Equal(0f, mud.Body);
        }

        [Fact]
        public void TheMudGrowsWithTheTimeOnTheEarth()
        {
            CarMud mud = new CarMud();
            Drive(mud, 5f, roll: 0.5f);
            float early = mud.Wheels(true);
            Drive(mud, 5f, roll: 0.5f);
            Assert.True(early > 0f);
            Assert.True(mud.Wheels(true) > early);
        }

        [Fact]
        public void RollingFastAndSlidingMuddyMore()
        {
            CarMud slow = new CarMud(), fast = new CarMud(), sliding = new CarMud();
            Drive(slow, 5f, roll: 0.1f);
            Drive(fast, 5f, roll: 1f);
            Drive(sliding, 5f, roll: 0.1f, slide: 1f);
            Assert.True(fast.Wheels(true) > slow.Wheels(true));
            Assert.True(sliding.Wheels(true) > fast.Wheels(true));
        }

        [Fact]
        public void AHardLandingSplashesAtOnce()
        {
            CarMud soft = new CarMud(), hard = new CarMud();
            soft.Wheel(false, 1f, 0f, 0f, 0.1f, 0f);
            hard.Wheel(false, 1f, 0f, 0f, 1f, 0f);
            Assert.True(hard.Wheels(false) > soft.Wheels(false));
            Assert.Equal(CarMud.LandShare, hard.Wheels(false), 5);
        }

        [Fact]
        public void EachAxleCollectsItsOwnMud()
        {
            CarMud mud = new CarMud();
            Drive(mud, 5f, front: false, roll: 1f);
            Assert.Equal(0f, mud.Wheels(true));
            Assert.Equal(0f, mud.Holder(true));
            Assert.True(mud.Wheels(false) > 0f);
            Assert.True(mud.Holder(false) > 0f);
        }

        [Fact]
        public void TheTreadsGetMoreAndTheBodyLessThanTheWheels()
        {
            CarMud mud = new CarMud();
            Drive(mud, 10f, roll: 1f);
            Assert.True(mud.Treads(true) > mud.Wheels(true));
            Assert.True(mud.Body > 0f);
            Assert.True(mud.Body < mud.Wheels(true));
        }

        [Fact]
        public void ATransitionGivesLessMud()
        {
            CarMud earth = new CarMud(), transition = new CarMud();
            Drive(earth, 5f, roll: 1f);
            Drive(transition, 5f, earth: 0.5f, roll: 1f);
            Assert.True(transition.Wheels(true) > 0f);
            Assert.True(transition.Wheels(true) < earth.Wheels(true));
        }

        [Fact]
        public void TheMudNeverGetsPastAllOfIt()
        {
            CarMud mud = new CarMud();
            Drive(mud, 600f, roll: 1f, slide: 1f);
            for (int i = 0; i < 100; i++)
                mud.Wheel(true, 1f, 1f, 1f, 1f, 1f);
            Assert.InRange(mud.Wheels(true), 0.99f, 1f);
            Assert.InRange(mud.Treads(true), 0.99f, 1f);
            Assert.InRange(mud.Body, 0.9f, 1f);
        }

        [Fact]
        public void TheWheelsWashCleanOffTheEarthTheTreadsFirst()
        {
            CarMud mud = new CarMud();
            mud.Set(0.8f);
            Drive(mud, 3f, earth: 0f, roll: 1f);
            Assert.True(mud.Treads(true) < mud.Wheels(true));
            Assert.True(mud.Wheels(true) < 0.8f);
            //One wheel of the axle (two of the three are on the ground mostly, twice as fast)
            Drive(mud, 60f, earth: 0f, roll: 1f);
            Assert.True(mud.Wheels(true) < 0.1f);
            Assert.True(mud.Treads(true) < 0.01f);
        }

        [Fact]
        public void TheBodyAndTheHoldersKeepTheirMud()
        {
            CarMud mud = new CarMud();
            mud.Set(0.6f);
            Drive(mud, 60f, earth: 0f, roll: 1f, slide: 1f);
            Assert.Equal(0.6f, mud.Body);
            Assert.Equal(0.6f, mud.Holder(true));
            Assert.Equal(0.6f, mud.Wheels(false));
        }

        [Fact]
        public void StandingStillWashesNothing()
        {
            CarMud mud = new CarMud();
            mud.Set(0.5f);
            Drive(mud, 30f, earth: 0f);
            Assert.Equal(0.5f, mud.Wheels(true));
            Assert.Equal(0.5f, mud.Treads(true));
        }

        [Fact]
        public void TheDevelopmentKeySetsEveryAmount()
        {
            CarMud mud = new CarMud();
            mud.Set(0.75f);
            Assert.Equal(0.75f, mud.Wheels(true));
            Assert.Equal(0.75f, mud.Treads(false));
            Assert.Equal(0.75f, mud.Holder(false));
            Assert.Equal(0.75f, mud.Body);
            mud.Set(2f);
            Assert.Equal(1f, mud.Body);
        }
    }
}
