using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace TW3D.Shared
{
    /// <summary>
    /// The collision shape of a model: convex polygons in the plane (x, y) of the model, in the units of the model.
    /// Each polygon is a list of coordinates x0, y0, x1, y1, ... counterclockwise.
    /// </summary>
    public sealed class CollisionShape
    {
        /// <summary>What everything collides with; null when the body and the wheels of the car each collide with their own part.</summary>
        public List<float[]> Shape { get; set; }

        /// <summary>What the body of the car collides with (a block where it differs from <see cref="Wheels"/>, e.g. the arch of a tunnel).</summary>
        public List<float[]> Body { get; set; }

        /// <summary>What the wheels of the car collide with.</summary>
        public List<float[]> Wheels { get; set; }
    }

    /// <summary>
    /// Collision shapes of the models (content/collision.json), keyed by the content name of the model
    /// (e.g. "Levels/mod/a1", "Env/co", "car"). Generated from the models by tools/CollisionGen.
    /// </summary>
    public sealed class CollisionShapes
    {
        public const string FileName = "collision.json";

        public Dictionary<string, CollisionShape> Models { get; set; } = new Dictionary<string, CollisionShape>();

        public CollisionShape Find(string model) => Models.TryGetValue(model, out CollisionShape shape) ? shape : null;

        public static CollisionShapes Load(Stream stream)
        {
            CollisionShapes shapes = JsonSerializer.Deserialize<CollisionShapes>(stream, JsonOptions.Default);
            if (shapes == null)
                throw new InvalidDataException("The collision shapes are empty.");
            return shapes;
        }

        /// <summary>Writes the shapes as JSON with one polygon per line, which keeps the file readable and easy to compare.</summary>
        public string ToJson()
        {
            StringBuilder json = new StringBuilder();
            json.Append("{\n  \"models\": {");
            bool firstModel = true;
            foreach (KeyValuePair<string, CollisionShape> model in Models.OrderBy(model => model.Key, System.StringComparer.Ordinal))
            {
                json.Append(firstModel ? "\n" : ",\n");
                firstModel = false;
                json.Append("    ").Append(JsonSerializer.Serialize(model.Key)).Append(": {");
                bool firstPart = true;
                AppendPart(json, "shape", model.Value.Shape, ref firstPart);
                AppendPart(json, "body", model.Value.Body, ref firstPart);
                AppendPart(json, "wheels", model.Value.Wheels, ref firstPart);
                json.Append("\n    }");
            }
            json.Append("\n  }\n}\n");
            return json.ToString();
        }

        private static void AppendPart(StringBuilder json, string name, List<float[]> polygons, ref bool first)
        {
            if (polygons == null)
                return;
            json.Append(first ? "\n" : ",\n");
            first = false;
            json.Append("      \"").Append(name).Append("\": [");
            for (int i = 0; i < polygons.Count; i++)
            {
                json.Append(i == 0 ? "\n" : ",\n");
                json.Append("        [").Append(string.Join(", ", polygons[i].Select(c => c.ToString("R", CultureInfo.InvariantCulture)))).Append(']');
            }
            json.Append(polygons.Count > 0 ? "\n      ]" : "]");
        }
    }
}
