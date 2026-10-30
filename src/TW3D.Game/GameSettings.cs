using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using TW3D.Graphics;

namespace TW3D
{
    /// <summary>
    /// The settings of the game: the graphics and display options (the Options menu) and the car chosen in the garage,
    /// saved in the user's application data.
    /// </summary>
    public sealed class GameSettings
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        /// <summary>The look of the 2016 version (its lighting, no shadows nor ambient occlusion).</summary>
        public bool Classic { get; set; }

        public bool Shadows { get; set; } = true;

        public bool AmbientOcclusion { get; set; } = true;

        /// <summary>Full screen; otherwise a window (on a desktop).</summary>
        public bool FullScreen { get; set; } = true;

        /// <summary>Frames per second in the top left corner.</summary>
        public bool ShowFrameRate { get; set; } = true;

        /// <summary>The car chosen in the garage: the model of its body (see Catalog.Cars).</summary>
        public string Car { get; set; } = "car";

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TW3D", "settings.json");

        /// <summary>The saved settings; the defaults if there are none or they cannot be read.</summary>
        public static GameSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new GameSettings();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException)
            {
                Debug.WriteLine("Settings not read: " + e.Message);
            }
            return new GameSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.WriteLine("Settings not saved: " + e.Message);
            }
        }

        /// <summary>Sets up a renderer by the settings.</summary>
        public void Apply(SceneRenderer renderer)
        {
            renderer.Classic = Classic;
            renderer.Shadows = Shadows;
            renderer.AmbientOcclusion = AmbientOcclusion;
        }
    }
}
