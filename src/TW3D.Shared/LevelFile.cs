using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace TW3D.Shared
{
    /// <summary>
    /// Reading and writing level files. Levels are stored as JSON with one block or object per line:
    /// <code>
    /// {
    ///   "format": 2,
    ///   "order": 1,
    ///   "mood": "sunset",
    ///   "middle": [
    ///     {"block":"a1","x":0,"y":0,"shift":0},
    ///     {"block":"b2","x":1,"y":0,"shift":-4,"turned":true},
    ///     ...
    ///   ],
    ///   "back": [ ... ],
    ///   "front": [ ... ],
    ///   "objects": [
    ///     {"type":"Bar","model":"bar1","x":238,"y":-4},
    ///     ...
    ///   ],
    ///   "scenery": [
    ///     {"model":"lamp","x":45,"y":-1.25,"z":-8.75,"turn":1},
    ///     ...
    ///   ]
    /// }
    /// </code>
    /// The scenery is written only when the level has some, and a block's "turned" only when it is.
    /// </summary>
    public static class LevelFile
    {
        public const string Extension = ".json";

        public static LevelData Load(string path)
        {
            return Parse(File.ReadAllText(path));
        }

        public static LevelData Load(Stream stream)
        {
            using (StreamReader reader = new StreamReader(stream))
                return Parse(reader.ReadToEnd());
        }

        /// <summary>
        /// Reads a level in the JSON format of <see cref="LevelData.CurrentFormat"/>. Anything else is refused: a file that
        /// is not JSON (such as a level of the 2016 version) and a level of another format.
        /// </summary>
        public static LevelData Parse(string text)
        {
            if (!text.TrimStart().StartsWith("{"))
                throw new InvalidDataException("The file is not a level: a level file is JSON.");

            LevelData level = JsonSerializer.Deserialize<LevelData>(text, JsonOptions.Default);
            if (level == null)
                throw new InvalidDataException("The level file is empty.");
            if (level.Format > LevelData.CurrentFormat)
                throw new InvalidDataException($"The level file has format {level.Format}, but only formats up to {LevelData.CurrentFormat} are supported.");
            if (level.Format < LevelData.CurrentFormat)
                throw new InvalidDataException($"The level file has format {level.Format}, which is no longer read: since format {LevelData.CurrentFormat} a block is turned instead of using its mirrored \"f\" model.");
            //The scenery is optional: "scenery": null is a level without it
            level.Scenery ??= new List<SceneryPlacement>();
            return level;
        }

        /// <summary>
        /// Names of the levels (*.json files) in the directory, in the order for the level select:
        /// by <see cref="LevelData.Order"/>, then the levels without it by name. Files that are not
        /// valid levels are left out. A missing directory gives an empty list.
        /// </summary>
        public static List<string> FindLevels(string directory)
        {
            if (!Directory.Exists(directory))
                return new List<string>();

            return SortLevels(Directory.GetFiles(directory, "*" + Extension).Select(Path.GetFileName),
                fileName => File.OpenRead(Path.Combine(directory, fileName)));
        }

        /// <summary>
        /// Names of the levels in the order for the level select, like <see cref="FindLevels"/>, for level files
        /// that are not in a directory (e.g. packed in an Android app).
        /// </summary>
        /// <param name="fileNames">File names of the levels, with the extension; other files are ignored.</param>
        /// <param name="open">Opens a level file by its name.</param>
        public static List<string> SortLevels(IEnumerable<string> fileNames, Func<string, Stream> open)
        {
            List<(string Name, int? Order)> levels = new List<(string, int?)>();
            foreach (string fileName in fileNames.Where(name => name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    using (Stream stream = open(fileName))
                        levels.Add((Path.GetFileNameWithoutExtension(fileName), Load(stream).Order));
                }
                catch (Exception e) when (e is IOException || e is InvalidDataException || e is JsonException)
                {
                    Debug.WriteLine("Level " + fileName + " left out: " + e.Message);
                }
            }

            return levels
                .OrderBy(level => level.Order.HasValue ? 0 : 1)
                .ThenBy(level => level.Order ?? 0)
                .ThenBy(level => level.Name, StringComparer.OrdinalIgnoreCase)
                .Select(level => level.Name)
                .ToList();
        }

        public static void Save(LevelData level, string path)
        {
            File.WriteAllText(path, ToJson(level), new UTF8Encoding(false));
        }

        /// <summary>Writes the level as JSON with one block or object per line, which keeps the files readable and easy to compare.</summary>
        public static string ToJson(LevelData level)
        {
            StringBuilder json = new StringBuilder();
            json.Append("{\n");
            json.Append("  \"format\": ").Append(level.Format).Append(",\n");
            if (level.Order.HasValue)
                json.Append("  \"order\": ").Append(level.Order.Value).Append(",\n");
            if (level.Mood != LevelMood.Day)
                json.Append("  \"mood\": ").Append(JsonSerializer.Serialize(level.Mood, JsonOptions.Default)).Append(",\n");
            if (level.Par.HasValue)
                json.Append("  \"par\": ").Append(JsonSerializer.Serialize(level.Par.Value, JsonOptions.Default)).Append(",\n");
            if (level.Style != null)
                json.Append("  \"style\": ").Append(JsonSerializer.Serialize(level.Style, JsonOptions.Default)).Append(",\n");
            bool scenery = level.Scenery != null && level.Scenery.Count > 0;
            AppendList(json, "middle", level.Middle, false);
            AppendList(json, "back", level.Back, false);
            AppendList(json, "front", level.Front, false);
            AppendList(json, "objects", level.Objects, !scenery);
            //Written only when the level has some, so that the levels without it stay as they were
            if (scenery)
                AppendList(json, "scenery", level.Scenery, true);
            json.Append("}\n");
            return json.ToString();
        }

        private static void AppendList<T>(StringBuilder json, string name, List<T> items, bool last)
        {
            json.Append("  \"").Append(name).Append("\": [");
            for (int i = 0; i < items.Count; i++)
            {
                json.Append(i == 0 ? "\n" : ",\n");
                json.Append("    ").Append(JsonSerializer.Serialize(items[i], JsonOptions.Default));
            }
            json.Append(items.Count > 0 ? "\n  ]" : "]");
            json.Append(last ? "\n" : ",\n");
        }
    }
}
