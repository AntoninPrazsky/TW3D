using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>
    /// The colours of the cars chosen in the garage: the palette (CarColours.cs, <see cref="PaintColours"/>) and how the
    /// settings keep the colours (GameSettings): an old file without them, the round trip, a car no longer in the catalog.
    /// </summary>
    public sealed class CarColoursTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "TW3D-CarColoursTests-" + Guid.NewGuid().ToString("N"));

        private string SettingsFile => Path.Combine(folder, "TW3D", "settings.json");

        public void Dispose()
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        [Fact]
        public void ThePaletteIsOriginalAndTwelveColours()
        {
            Assert.Same(PaintColours.Original, PaintColours.All[0]);
            Assert.True(PaintColours.Original.IsOriginal);
            Assert.Equal(13, PaintColours.All.Count);
            IEnumerable<PaintColour> colours = PaintColours.All.Skip(1);
            Assert.All(colours, colour => Assert.False(colour.IsOriginal));
            Assert.Equal(12, colours.Select(colour => colour.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Equal(12, colours.Select(colour => colour.Name).Distinct().Count());
            Assert.All(colours, colour => Assert.All(new[] { colour.R, colour.G, colour.B }, c => Assert.InRange(c, 0f, 1f)));
        }

        [Fact]
        public void AKeyFindsItsColourAndAnythingElseIsOriginal()
        {
            Assert.Equal("Red", PaintColours.Find("red").Name);
            Assert.Equal("Red", PaintColours.Find("RED").Name);
            Assert.Same(PaintColours.Original, PaintColours.Find(null));
            Assert.Same(PaintColours.Original, PaintColours.Find("chartreuse"));
        }

        [Fact]
        public void StepsGoRoundThePalette()
        {
            Assert.Equal(PaintColours.All[1].Key, PaintColours.Step(null, 1));
            Assert.Equal(PaintColours.All[^1].Key, PaintColours.Step(null, -1));
            Assert.Null(PaintColours.Step(PaintColours.All[^1].Key, 1));
            Assert.Equal(PaintColours.All[2].Key, PaintColours.Step(PaintColours.All[1].Key, 1));
            //A key the palette does not have counts as Original
            Assert.Equal(PaintColours.All[1].Key, PaintColours.Step("chartreuse", 1));

            //Every colour is reached, once each, going round
            string key = null;
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < PaintColours.All.Count; i++)
            {
                key = PaintColours.Step(key, 1);
                Assert.True(seen.Add(key ?? ""));
            }
            Assert.Null(key);
        }

        [Fact]
        public void AnOldSettingsFileHasTheOriginalColours()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile));
            File.WriteAllText(SettingsFile, "{ \"car\": \"car2\", \"wheels\": { \"car2\": { \"front\": \"wheel\", \"rear\": \"wheel2\" } } }");

            GameSettings settings = GameSettings.Load(SettingsFile);
            Assert.Equal("car2", settings.Car);
            Assert.NotNull(settings.Colours);
            Assert.Empty(settings.Colours);
            CarColours colours = settings.ColoursOf("car2");
            Assert.Null(colours.Primary);
            Assert.Null(colours.Secondary);
            Assert.Null(colours.FrontRims);
            Assert.Null(colours.RearRims);
            Assert.NotNull(settings.ColoursOf(null));
        }

        [Fact]
        public void ColoursSurviveARoundTripPerCar()
        {
            GameSettings settings = new GameSettings();
            settings.Colours["car"] = new CarColours { Primary = "blue", Secondary = "white", FrontRims = "black", RearRims = "yellow" };
            settings.Colours["car3"] = new CarColours { Primary = "red" };
            settings.Save(SettingsFile);

            string json = File.ReadAllText(SettingsFile);
            Assert.Contains("\"colours\"", json);
            Assert.Contains("\"frontRims\"", json);

            GameSettings loaded = GameSettings.Load(SettingsFile);
            CarColours buggy = loaded.ColoursOf("car");
            Assert.Equal("blue", buggy.Primary);
            Assert.Equal("white", buggy.Secondary);
            Assert.Equal("black", buggy.FrontRims);
            Assert.Equal("yellow", buggy.RearRims);
            CarColours crawler = loaded.ColoursOf("car3");
            Assert.Equal("red", crawler.Primary);
            Assert.Null(crawler.Secondary);
            //A car without colours of its own has the original ones
            Assert.Null(loaded.ColoursOf("car2").Primary);
        }

        [Fact]
        public void TheColoursOfACarNoLongerInTheCatalogAreKept()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile));
            File.WriteAllText(SettingsFile, "{ \"colours\": { \"car9\": { \"primary\": \"pink\", \"rearRims\": \"chartreuse\" } } }");

            GameSettings settings = GameSettings.Load(SettingsFile);
            Assert.Equal("pink", settings.ColoursOf("car9").Primary);
            //A colour the palette does not have (any more) is the original one
            Assert.Same(PaintColours.Original, PaintColours.Find(settings.ColoursOf("car9").RearRims));

            settings.Save(SettingsFile);
            Assert.Equal("pink", GameSettings.Load(SettingsFile).ColoursOf("car9").Primary);
        }

        [Fact]
        public void ACloneIsChangedAlone()
        {
            CarColours colours = new CarColours { Primary = "red" };
            CarColours clone = colours.Clone();
            clone.Primary = "blue";
            Assert.Equal("red", colours.Primary);
        }
    }
}
