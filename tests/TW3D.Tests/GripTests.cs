using System.Linq;
using TW3D.Shared;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>
    /// The grip of the tyres on the surfaces of the track (#58): the classic slab grips as it always did, every wheel grips
    /// each surface differently, the wheels made for the earth win on it, none is hopeless, and a transition is the mean
    /// of its two surfaces.
    /// </summary>
    public class GripTests
    {
        private static readonly Catalog Catalog = Repository.LoadCatalog();

        private static WheelType Wheel(string name) => Catalog.Wheels.Single(wheel => wheel.Name == name);

        [Fact]
        public void TheClassicSlabGripsAsItAlwaysDid()
        {
            Assert.Equal(1f, Catalog.SurfaceFriction(null));
            Assert.Equal(1f, Catalog.SurfaceFriction(Catalog.ClassicStyle));
            Assert.All(Catalog.Wheels, wheel =>
            {
                Assert.Equal(1f, wheel.GripFactor(Catalog.ClassicStyle));
                Assert.Equal(1f, wheel.GripFactor(null));
                Assert.Equal(System.MathF.Sqrt(wheel.Friction), Catalog.ContactGrip(wheel, Catalog.ClassicStyle), 5);
            });
        }

        [Fact]
        public void EveryGripNamesAStyle()
        {
            Assert.All(Catalog.Wheels.Where(wheel => wheel.Grip != null).SelectMany(wheel => wheel.Grip.Keys),
                style => Assert.NotNull(Catalog.FindStyle(style)));
            Assert.All(Catalog.Styles, style => Assert.False(string.IsNullOrEmpty(style.Title)));
        }

        [Fact]
        public void TheOffroadWheelsWinOnTheEarthAndTheRacingWheelIsNotHopeless()
        {
            float offroad = Catalog.ContactGrip(Wheel("Off-road"), "offroad");
            float crawler = Catalog.ContactGrip(Wheel("Rock crawler"), "offroad");
            float racing = Catalog.ContactGrip(Wheel("Racing"), "offroad");
            //The tall knobby crawler grips the loose ground best, the off-road wheel next, the slick slides
            Assert.True(crawler > offroad);
            Assert.True(offroad > racing * 1.5f);
            Assert.True(racing > crawler * 0.45f);
            //On the earth the off-road wheels grip better than on the slab, the racing wheel worse
            Assert.True(offroad > Catalog.ContactGrip(Wheel("Off-road"), Catalog.ClassicStyle));
            Assert.True(racing < Catalog.ContactGrip(Wheel("Racing"), Catalog.ClassicStyle));
        }

        [Fact]
        public void ATransitionIsTheMeanOfItsSurfaces()
        {
            float earth = Catalog.SurfaceFriction("offroad");
            Assert.Equal((earth + 1f) / 2f, Catalog.SurfaceFriction("offroad-classic"), 5);
            Assert.Equal((earth + 1f) / 2f, Catalog.SurfaceFriction("classic-offroad"), 5);
            WheelType crawler = Wheel("Rock crawler");
            Assert.Equal((crawler.GripFactor("offroad") + 1f) / 2f, crawler.GripFactor("classic-offroad"), 5);
        }
    }
}
