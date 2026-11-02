using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace TW3D.Editor
{
    /// <summary>Where the camera of the editor was: the point it looked at, its turn and its distance (see EditorCamera).</summary>
    sealed class CameraView
    {
        /// <summary>The point the camera looked at (scene units).</summary>
        public float TargetX { get; set; }
        public float TargetY { get; set; }
        public float TargetZ { get; set; }

        /// <summary>Turn around the vertical axis, radians.</summary>
        public float Yaw { get; set; }

        /// <summary>Turn up or down, radians.</summary>
        public float Pitch { get; set; }

        /// <summary>How far the camera is from the point it looks at.</summary>
        public float Distance { get; set; }
    }

    /// <summary>
    /// What the level editor remembers between runs, saved in the user's application data the way the settings of the
    /// game are: the levels opened or saved last (for File > Open recent), the last level, which the editor opens when
    /// it is started without one, and where the camera looked at it. The window is the game's, so its size is not here.
    /// </summary>
    sealed class EditorSettings
    {
        /// <summary>How many levels the recent list keeps.</summary>
        public const int MaxRecent = 8;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        /// <summary>The file the game keeps the settings of the editor in.</summary>
        public static string DefaultPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TW3D", "editor.json");

        /// <summary>The full paths of the levels opened or saved last, the most recent first, each once, at most <see cref="MaxRecent"/>.</summary>
        public List<string> Recent { get; set; } = new List<string>();

        /// <summary>The full path of the level edited last; null when there is none.</summary>
        public string LastLevel { get; set; }

        /// <summary>Where the camera looked at the last level; null when it is not known (the level is framed).</summary>
        public CameraView Camera { get; set; }

        /// <summary>Puts a level at the top of the recent list (a file already in it moves there), keeping at most <see cref="MaxRecent"/>.</summary>
        public void AddRecent(string file)
        {
            Recent.RemoveAll(recent => string.Equals(recent, file, StringComparison.OrdinalIgnoreCase));
            Recent.Insert(0, file);
            if (Recent.Count > MaxRecent)
                Recent.RemoveRange(MaxRecent, Recent.Count - MaxRecent);
        }

        /// <summary>The saved settings; the defaults if there are none or they cannot be read. A recent list edited by hand is put in order.</summary>
        public static EditorSettings Load(string path)
        {
            EditorSettings settings = null;
            try
            {
                if (File.Exists(path))
                    settings = JsonSerializer.Deserialize<EditorSettings>(File.ReadAllText(path), JsonOptions);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException)
            {
                Debug.WriteLine("Editor settings not read: " + e.Message);
            }
            settings ??= new EditorSettings();

            List<string> recent = settings.Recent ?? new List<string>();
            settings.Recent = new List<string>();
            for (int i = recent.Count - 1; i >= 0; i--)
                if (!string.IsNullOrWhiteSpace(recent[i]))
                    settings.AddRecent(recent[i]);
            return settings;
        }

        /// <summary>Saves the settings; a file that cannot be written is left as it is (the editor works on without it).</summary>
        public void Save(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.WriteLine("Editor settings not saved: " + e.Message);
            }
        }
    }
}
