using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TW3D.Shared
{
    /// <summary>A kind of track block, placed on the level grid.</summary>
    public sealed class BlockType
    {
        public string Name { get; set; }

        /// <summary>
        /// The block has a collision bitmap, so it can be placed in the middle (physics) layer.
        /// Blocks without it are decoration for the back and front layers only.
        /// </summary>
        public bool Collision { get; set; }

        [JsonIgnore]
        public string ModelAsset => "Levels/mod/" + Name;

        [JsonIgnore]
        public string CollisionAsset => "Levels/bit/" + Name;
    }

    public enum ObjectShape
    {
        /// <summary>A square with the half side <see cref="ObjectType.Size"/>.</summary>
        Rectangle,
        /// <summary>A circle with the radius <see cref="ObjectType.Size"/>.</summary>
        Circle,
        /// <summary>The shape is read from the collision bitmap of the model.</summary>
        Bitmap
    }

    /// <summary>A kind of dynamic object (moved by the physics simulation) with the models it can use.</summary>
    public sealed class ObjectType
    {
        public string Type { get; set; }
        public ObjectShape Shape { get; set; }

        /// <summary>Half side of a rectangle or radius of a circle, in physics units.</summary>
        public float Size { get; set; }

        public float Friction { get; set; }
        public float Mass { get; set; }
        public float Restitution { get; set; }
        public List<string> Models { get; set; } = new List<string>();

        public static string ModelAsset(string model) => "Env/" + model;

        public static string CollisionAsset(string model) => "Env/bit/" + model;
    }

    /// <summary>
    /// All track blocks and dynamic objects the levels are built from.
    /// Shared by the game and the level editor; stored in content/catalog.json.
    /// </summary>
    public sealed class Catalog
    {
        public const string FileName = "catalog.json";

        public List<BlockType> Blocks { get; set; } = new List<BlockType>();
        public List<ObjectType> Objects { get; set; } = new List<ObjectType>();

        public BlockType FindBlock(string name) => Blocks.FirstOrDefault(block => block.Name == name);

        public ObjectType FindObject(string type) => Objects.FirstOrDefault(obj => obj.Type == type);

        /// <summary>Models of all dynamic objects (each listed once).</summary>
        public IEnumerable<string> ObjectModels => Objects.SelectMany(obj => obj.Models).Distinct();

        public static Catalog Load(Stream stream)
        {
            Catalog catalog = JsonSerializer.Deserialize<Catalog>(stream, JsonOptions.Default);
            if (catalog == null)
                throw new InvalidDataException("The catalog is empty.");
            return catalog;
        }

        public static Catalog Load(string path)
        {
            using (FileStream stream = File.OpenRead(path))
                return Load(stream);
        }
    }
}
