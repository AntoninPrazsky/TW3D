using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace TW3D.Shared
{
    /// <summary>
    /// Reading and writing level files. Levels are stored as JSON with one block or object per line:
    /// <code>
    /// {
    ///   "format": 1,
    ///   "middle": [
    ///     {"block":"a1","x":0,"y":0,"shift":0},
    ///     ...
    ///   ],
    ///   "back": [ ... ],
    ///   "front": [ ... ],
    ///   "objects": [
    ///     {"type":"Bar","model":"bar1","x":238,"y":-4},
    ///     ...
    ///   ]
    /// }
    /// </code>
    /// Files in the original .lev format of the 2016 version can be read too.
    /// </summary>
    public static class LevelFile
    {
        public const string Extension = ".json";
        public const string LegacyExtension = ".lev";

        public static LevelData Load(string path)
        {
            return Parse(File.ReadAllText(path));
        }

        public static LevelData Load(Stream stream)
        {
            using (StreamReader reader = new StreamReader(stream))
                return Parse(reader.ReadToEnd());
        }

        /// <summary>Reads a level in the JSON format, or in the legacy .lev format.</summary>
        public static LevelData Parse(string text)
        {
            if (!text.TrimStart().StartsWith("{"))
                return LegacyLevelFormat.Parse(text);

            LevelData level = JsonSerializer.Deserialize<LevelData>(text, JsonOptions.Default);
            if (level == null)
                throw new InvalidDataException("The level file is empty.");
            if (level.Format > LevelData.CurrentFormat)
                throw new InvalidDataException($"The level file has format {level.Format}, but only formats up to {LevelData.CurrentFormat} are supported.");
            return level;
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
            AppendList(json, "middle", level.Middle, false);
            AppendList(json, "back", level.Back, false);
            AppendList(json, "front", level.Front, false);
            AppendList(json, "objects", level.Objects, true);
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
