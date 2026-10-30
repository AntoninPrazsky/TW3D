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
        /// The block has a collision shape (generated from its model, see <see cref="CollisionShapes"/>),
        /// so it can be placed in the middle (physics) layer. Blocks without it are decoration for the back and front layers only.
        /// </summary>
        public bool Collision { get; set; }

        [JsonIgnore]
        public string ModelAsset => "Levels/mod/" + Name;
    }

    public enum ObjectShape
    {
        /// <summary>A square with the half side <see cref="ObjectType.Size"/>.</summary>
        Rectangle,
        /// <summary>A circle with the radius <see cref="ObjectType.Size"/>.</summary>
        Circle,
        /// <summary>The collision shape of the model (see <see cref="CollisionShapes"/>).</summary>
        Model
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
    }

    /// <summary>
    /// A car to choose in the garage. All cars have the same chassis: the holders of the wheels and their places; any
    /// wheels fit any car, on each axle other ones.
    /// </summary>
    public sealed class CarType
    {
        /// <summary>The name shown in the garage.</summary>
        public string Name { get; set; }

        /// <summary>Content name of the model of the body (e.g. "car").</summary>
        public string Model { get; set; }

        /// <summary>The model of the wheels the car comes with (see <see cref="Catalog.Wheels"/>).</summary>
        public string Wheel { get; set; }

        /// <summary>Mass of the body (the holders of the wheels and the wheels add their own).</summary>
        public float Mass { get; set; } = 200f;

        /// <summary>How fast the motors turn the wheels at most (radians per second): the top speed of the car.</summary>
        public float TopSpeed { get; set; } = 50f;
    }

    /// <summary>Wheels for the cars.</summary>
    public sealed class WheelType
    {
        /// <summary>The name shown in the garage.</summary>
        public string Name { get; set; }

        /// <summary>Content name of the model of a wheel (e.g. "wheel").</summary>
        public string Model { get; set; }

        /// <summary>Friction of the tyre: its grip on the track.</summary>
        public float Friction { get; set; } = 1f;

        /// <summary>Mass of one wheel.</summary>
        public float Mass { get; set; } = 30f;
    }

    /// <summary>
    /// All track blocks and dynamic objects the levels are built from, and the cars and their wheels.
    /// Shared by the game and the level editor; stored in content/catalog.json.
    /// </summary>
    public sealed class Catalog
    {
        public const string FileName = "catalog.json";

        public List<BlockType> Blocks { get; set; } = new List<BlockType>();
        public List<ObjectType> Objects { get; set; } = new List<ObjectType>();
        public List<CarType> Cars { get; set; } = new List<CarType>();
        public List<WheelType> Wheels { get; set; } = new List<WheelType>();

        public BlockType FindBlock(string name) => Blocks.FirstOrDefault(block => block.Name == name);

        public ObjectType FindObject(string type) => Objects.FirstOrDefault(obj => obj.Type == type);

        /// <summary>The car with the body model; the first car if there is no such car.</summary>
        public CarType FindCar(string model) => Cars.FirstOrDefault(car => car.Model == model) ?? Cars.FirstOrDefault();

        /// <summary>The wheels with the model; the first wheels if there are no such wheels.</summary>
        public WheelType FindWheel(string model) => Wheels.FirstOrDefault(wheel => wheel.Model == model) ?? Wheels.FirstOrDefault();

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
