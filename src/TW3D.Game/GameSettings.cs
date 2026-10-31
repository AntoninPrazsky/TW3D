using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using TW3D.Graphics;
using TW3D.Shared;

namespace TW3D
{
    /// <summary>The wheels chosen for a car: the models of the wheels on its front and rear axle (see Catalog.Wheels).</summary>
    public sealed class CarWheels
    {
        public string Front { get; set; }

        public string Rear { get; set; }
    }

    /// <summary>
    /// The settings of the game: the graphics and display options (the Options menu) and the car and its wheels chosen
    /// in the garage, saved in the user's application data.
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

        /// <summary>The cards of the tutorial while driving.</summary>
        public bool Tutorial { get; set; } = true;

        /// <summary>The car chosen in the garage: the model of its body (see Catalog.Cars).</summary>
        public string Car { get; set; } = "car";

        /// <summary>The wheels chosen in the garage for the cars, by the models of their bodies.</summary>
        public Dictionary<string, CarWheels> Wheels { get; set; } = new Dictionary<string, CarWheels>();

        /// <summary>The wheels chosen for a car; the wheels it comes with if none were chosen.</summary>
        public CarWheels WheelsOf(CarType car)
        {
            return Wheels.TryGetValue(car.Model, out CarWheels wheels) && wheels != null
                ? wheels
                : new CarWheels { Front = car.Wheel, Rear = car.Wheel };
        }

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
