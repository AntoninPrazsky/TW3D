using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace TW3D.Graphics
{
    /// <summary>
    /// The graphics options of the game (the Options menu), saved in the user's application data.
    /// </summary>
    public sealed class GraphicsSettings
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

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TW3D", "settings.json");

        /// <summary>The saved settings; the defaults if there are none or they cannot be read.</summary>
        public static GraphicsSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<GraphicsSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new GraphicsSettings();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException)
            {
                Debug.WriteLine("Settings not read: " + e.Message);
            }
            return new GraphicsSettings();
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
