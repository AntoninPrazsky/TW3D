using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace TW3D
{
    /// <summary>
    /// The best times of the levels and the lessons of the tutorial taught, saved in the user's application data next to
    /// the settings.
    /// </summary>
    public sealed class GameRecords
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        /// <summary>The best time of each level finished (seconds), by the name of the level.</summary>
        public Dictionary<string, float> BestTimes { get; set; } = new Dictionary<string, float>();

        /// <summary>The lessons of the tutorial taught, by their keys (see GamePlay.Tutorial.Lessons).</summary>
        public HashSet<string> Lessons { get; set; } = new HashSet<string>();

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TW3D", "records.json");

        /// <summary>The best time of a level; null if it was not finished yet.</summary>
        public float? BestTime(string level)
        {
            return BestTimes.TryGetValue(level, out float time) ? time : null;
        }

        /// <summary>Records the time of a run of a level; true (and saved) if it is the best one.</summary>
        public bool Add(string level, float time)
        {
            if (BestTime(level) <= time)
                return false;
            BestTimes[level] = time;
            Save();
            return true;
        }

        /// <summary>Records a lesson of the tutorial as taught.</summary>
        public void AddLesson(string key)
        {
            if (Lessons.Add(key))
                Save();
        }

        /// <summary>The saved records; none if there are none or they cannot be read.</summary>
        public static GameRecords Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<GameRecords>(File.ReadAllText(FilePath), JsonOptions) ?? new GameRecords();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException)
            {
                Debug.WriteLine("Records not read: " + e.Message);
            }
            return new GameRecords();
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.WriteLine("Records not saved: " + e.Message);
            }
        }
    }
}
