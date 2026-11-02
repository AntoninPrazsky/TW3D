using System;
using System.Collections.Generic;
using System.IO;
using TW3D.Editor;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>What the level editor remembers (Editor/EditorSettings): the file round trip, the rules of the recent list, the defaults for a file that cannot be read.</summary>
    public sealed class EditorSettingsTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "TW3D-EditorSettingsTests-" + Guid.NewGuid().ToString("N"));

        private string SettingsFile => Path.Combine(folder, "TW3D", "editor.json");

        public void Dispose()
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        [Fact]
        public void SettingsSurviveARoundTrip()
        {
            EditorSettings settings = new EditorSettings
            {
                LastLevel = @"D:\levels\lock.json",
                Camera = new CameraView { TargetX = 1.5f, TargetY = -2.25f, TargetZ = 0.125f, Yaw = 0.3f, Pitch = -0.4f, Distance = 22f }
            };
            settings.AddRecent(@"D:\levels\a.json");
            settings.AddRecent(@"D:\levels\lock.json");
            settings.Save(SettingsFile);

            Assert.Contains("\"lastLevel\"", File.ReadAllText(SettingsFile));
            EditorSettings loaded = EditorSettings.Load(SettingsFile);
            Assert.Equal(new[] { @"D:\levels\lock.json", @"D:\levels\a.json" }, loaded.Recent);
            Assert.Equal(@"D:\levels\lock.json", loaded.LastLevel);
            Assert.Equal(1.5f, loaded.Camera.TargetX);
            Assert.Equal(-2.25f, loaded.Camera.TargetY);
            Assert.Equal(0.125f, loaded.Camera.TargetZ);
            Assert.Equal(0.3f, loaded.Camera.Yaw);
            Assert.Equal(-0.4f, loaded.Camera.Pitch);
            Assert.Equal(22f, loaded.Camera.Distance);
        }

        [Fact]
        public void TheRecentListHasTheLatestFirstEachOnceAndAtMostEight()
        {
            EditorSettings settings = new EditorSettings();
            for (int i = 0; i < 10; i++)
                settings.AddRecent($@"D:\levels\level{i}.json");
            Assert.Equal(EditorSettings.MaxRecent, settings.Recent.Count);
            Assert.Equal(@"D:\levels\level9.json", settings.Recent[0]);
            Assert.Equal(@"D:\levels\level2.json", settings.Recent[7]);

            //A file already in the list moves to the top, whatever the case of its path
            settings.AddRecent(@"d:\LEVELS\level5.json");
            Assert.Equal(EditorSettings.MaxRecent, settings.Recent.Count);
            Assert.Equal(@"d:\LEVELS\level5.json", settings.Recent[0]);
            Assert.Single(settings.Recent.FindAll(file => file.EndsWith("level5.json", StringComparison.OrdinalIgnoreCase)));
            Assert.Equal(@"D:\levels\level9.json", settings.Recent[1]);
        }

        [Fact]
        public void ARecentListEditedByHandIsPutInOrder()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile));
            List<string> recent = new List<string> { @"C:\a.json", @"C:\A.JSON", "", @"C:\b.json" };
            for (int i = 0; i < 10; i++)
                recent.Add($@"C:\x{i}.json");
            File.WriteAllText(SettingsFile, System.Text.Json.JsonSerializer.Serialize(new { recent }));

            EditorSettings loaded = EditorSettings.Load(SettingsFile);
            Assert.Equal(EditorSettings.MaxRecent, loaded.Recent.Count);
            Assert.Equal(@"C:\a.json", loaded.Recent[0]);
            Assert.Equal(@"C:\b.json", loaded.Recent[1]);
            Assert.Equal(@"C:\x0.json", loaded.Recent[2]);
        }

        [Fact]
        public void ACorruptFileGivesTheDefaults()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile));
            File.WriteAllText(SettingsFile, "{ \"recent\": [ not json");
            EditorSettings loaded = EditorSettings.Load(SettingsFile);
            Assert.Empty(loaded.Recent);
            Assert.Null(loaded.LastLevel);
            Assert.Null(loaded.Camera);
        }

        [Fact]
        public void AMissingFileGivesTheDefaults()
        {
            EditorSettings loaded = EditorSettings.Load(SettingsFile);
            Assert.Empty(loaded.Recent);
            Assert.Null(loaded.LastLevel);
            Assert.Null(loaded.Camera);
        }
    }
}
