using System.Text.Json;
using System.Text.Json.Serialization;
using TW3D.Graphics;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>
    /// What the Shadow detail row of the Options menu promises (Graphics/ShadowDetails): each detail doubles the shadow map,
    /// medium is the map the game always had, the deep map never gets smaller than it was, a click steps up and wraps
    /// round, left and right step and stop, a Raspberry Pi stops at medium, a detail the game does not know is the default,
    /// and the settings file keeps the detail by its name.
    /// </summary>
    public class ShadowDetailTests
    {
        [Theory]
        [InlineData(ShadowDetail.Low, 1024, 1024)]
        [InlineData(ShadowDetail.Medium, 2048, 1024)]
        [InlineData(ShadowDetail.High, 4096, 2048)]
        public void EachDetailDoublesTheShadowMap(ShadowDetail detail, int size, int deepSize)
        {
            Assert.Equal(size, ShadowDetails.MapSize(detail));
            Assert.Equal(deepSize, ShadowDetails.DeepMapSize(detail));
        }

        [Fact]
        public void TheDefaultIsHighOnADesktopAndLowOnAPi()
        {
            //The CI runs the tests on Windows and on Linux on ARM64, which the game takes for a Raspberry Pi
            Assert.Equal(PiGraphics.Active ? ShadowDetail.Low : ShadowDetail.High, ShadowDetails.Default);
        }

        [Fact]
        public void APiStopsAtMediumAsItsGpuHasNoWiderTexture()
        {
            Assert.Equal(PiGraphics.Active ? ShadowDetail.Medium : ShadowDetail.High, ShadowDetails.Highest);
            //The texture of the shadow maps of the highest detail of a Pi fits the 4096 texels of its GPU, the one of high does not
            Assert.True(ShadowDetails.MapSize(ShadowDetail.Medium) + ShadowDetails.DeepMapSize(ShadowDetail.Medium) <= 4096);
            Assert.True(ShadowDetails.MapSize(ShadowDetail.High) + ShadowDetails.DeepMapSize(ShadowDetail.High) > 4096);
            //High, from a settings file brought from a desktop, is the highest there
            Assert.Equal(ShadowDetails.Highest, ShadowDetails.Valid(ShadowDetail.High));
        }

        [Fact]
        public void EnterStepsUpAndWrapsRound()
        {
            Assert.Equal(ShadowDetail.Medium, ShadowDetails.Next(ShadowDetail.Low));
            Assert.Equal(PiGraphics.Active ? ShadowDetail.Low : ShadowDetail.High, ShadowDetails.Next(ShadowDetail.Medium));
            Assert.Equal(ShadowDetail.Low, ShadowDetails.Next(ShadowDetail.High));
        }

        [Theory]
        [InlineData(ShadowDetail.Low, -1, ShadowDetail.Low)]
        [InlineData(ShadowDetail.Low, 1, ShadowDetail.Medium)]
        [InlineData(ShadowDetail.Medium, -1, ShadowDetail.Low)]
        public void LeftAndRightStepAndStop(ShadowDetail detail, int step, ShadowDetail expected)
        {
            Assert.Equal(expected, ShadowDetails.Step(detail, step));
        }

        [Fact]
        public void RightStopsAtTheHighest()
        {
            Assert.Equal(ShadowDetails.Highest, ShadowDetails.Step(ShadowDetails.Highest, 1));
            Assert.Equal(ShadowDetails.Highest - 1, ShadowDetails.Step(ShadowDetails.Highest, -1));
            Assert.Equal(ShadowDetails.Highest, ShadowDetails.Step(ShadowDetail.High, 1));
        }

        [Fact]
        public void AnUnknownDetailIsTheDefault()
        {
            ShadowDetail unknown = (ShadowDetail)7;
            Assert.Equal(ShadowDetails.Default, ShadowDetails.Valid(unknown));
            Assert.Equal(ShadowDetails.MapSize(ShadowDetails.Default), ShadowDetails.MapSize(unknown));
            Assert.Equal(ShadowDetails.Default.ToString(), ShadowDetails.Name(unknown));
        }

        /// <summary>As GameSettings keeps it in settings.json.</summary>
        private sealed class Saved
        {
            [JsonConverter(typeof(JsonStringEnumConverter<ShadowDetail>))]
            public ShadowDetail ShadowDetail { get; set; }
        }

        [Fact]
        public void TheSettingsFileKeepsTheDetailByItsName()
        {
            JsonSerializerOptions options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            string json = JsonSerializer.Serialize(new Saved { ShadowDetail = ShadowDetail.Low }, options);
            Assert.Equal("{\"shadowDetail\":\"Low\"}", json);
            Assert.Equal(ShadowDetail.High, JsonSerializer.Deserialize<Saved>("{\"shadowDetail\":\"High\"}", options).ShadowDetail);
        }
    }
}
