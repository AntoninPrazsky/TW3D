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
        /// What the block is, in a few words ("Slope, 4 steps a cell (24°)"): the editor shows it with the name (the
        /// Palette's tooltip and search, the Inspector) and the catalog page beside the picture.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// The family of the block ("Slopes"): the Palette of the editor shows the blocks in these groups, in the order the
        /// groups first appear in the catalog, as the catalog page does. A block without one goes to the last group.
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// The block has a collision shape (generated from its model, see <see cref="CollisionShapes"/>),
        /// so it can be placed in the middle (physics) layer. Blocks without it are decoration for the back and front layers only.
        /// </summary>
        public bool Collision { get; set; }

        /// <summary>
        /// For a road block, [left, right]: the height of the road surface at the left and the right edge of the block
        /// relative to its base, in shift steps (2.5 physics units; one cell is 9 steps); heights grow downward, as
        /// physics y does. A climb to the right is [4, 0]: it starts 4 steps below its base and ends at it. The editor
        /// joins the blocks of a road with it and the level generator builds roads from it. Null for a block that is not
        /// a road block (decoration, walls, gates).
        /// </summary>
        public int[] Profile { get; set; }

        /// <summary>
        /// The block may be turned round by 180° about the vertical axis (<see cref="BlockPlacement.Turned"/>): its road
        /// then runs the other way, and it looks the same from behind. A tunnel or a decoration turned would show its
        /// back, so it does not turn; the ground under a slope (the same across its cell, turned with its road block) and
        /// the hills and trees of the backdrop do.
        /// </summary>
        public bool Turns { get; set; }

        /// <summary>
        /// The looks of the block in the styles of the track other than the classic one (#58), by the name of the style or of
        /// a transition between two styles (see <see cref="Catalog.Styles"/>): the name of the model of the look in
        /// Levels/mod. A look shares everything else with the block (its collision shape, <see cref="Profile"/> and
        /// turning); only its model differs. Null for a block that is classic in every style.
        /// </summary>
        public Dictionary<string, string> Looks { get; set; }

        /// <summary>The block is a road block: it has a surface <see cref="Profile"/>.</summary>
        [JsonIgnore]
        public bool IsRoad => Profile != null;

        /// <summary>The block has a look in a style (the classic look is every block's).</summary>
        public bool HasLook(string style)
        {
            return style == null || style == Catalog.ClassicStyle || (Looks != null && Looks.ContainsKey(style));
        }

        /// <summary>
        /// The model the block is drawn with in a style: the model of its look, or its own model where it has no look in
        /// the style (null and <see cref="Catalog.ClassicStyle"/> are the classic look). Its collision shape is always that
        /// of <see cref="ModelAsset"/>.
        /// </summary>
        public string LookAsset(string style)
        {
            return style != null && Looks != null && Looks.TryGetValue(style, out string model) ? "Levels/mod/" + model : ModelAsset;
        }

        /// <summary>
        /// The <see cref="Profile"/> of the block as placed, [left, right]: turned, the edges swap, so a turned climb
        /// [4, 0] is a descent [0, 4]. Null for a block that is not a road block.
        /// </summary>
        public int[] PlacedProfile(bool turned)
        {
            if (Profile == null || Profile.Length != 2)
                return null;
            return turned ? new[] { Profile[1], Profile[0] } : Profile;
        }

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

    /// <summary>What a dynamic object is made of: how it sounds when it hits something.</summary>
    public enum ObjectMaterial
    {
        Metal,
        Wood,
        Plastic
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

        /// <summary>What it is made of (how it sounds when it hits something): metal if not given, wood, plastic.</summary>
        public ObjectMaterial Material { get; set; }

        public static string ModelAsset(string model) => "Env/" + model;
    }

    /// <summary>
    /// A kind of scenery: a static prop that stands beside the track (a traffic cone, a street lamp...). It has no physics;
    /// the car never meets it. Not to be confused with the decoration blocks of the back and front layers, nor with the
    /// dynamic objects, which are physics bodies.
    /// </summary>
    public sealed class SceneryType
    {
        /// <summary>The name shown in the editor.</summary>
        public string Name { get; set; }

        /// <summary>Content name of the model in Scenery/ (e.g. "cone"); the levels name the prop by it.</summary>
        public string Model { get; set; }

        [JsonIgnore]
        public string ModelAsset => "Scenery/" + Model;
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

        /// <summary>How much higher its engine sounds than the one baked (octaves): a sportier car revs higher.</summary>
        public float EnginePitch { get; set; }

        /// <summary>How fast its engine follows the wheels (1 / seconds): a sportier car revs up faster.</summary>
        public float EngineResponse { get; set; } = 3f;

        /// <summary>
        /// Where the exhaust blows out (the end of the pipe, in the coordinates of the model: x to the back of the car, y
        /// up), one on each side of the car (z and -z); null for a car without an exhaust.
        /// </summary>
        public float[] Exhaust { get; set; }
    }

    /// <summary>
    /// A style of the track besides the classic one (#58): its name (as the levels and the looks of the blocks name it),
    /// its title in the game, and the friction of its surface (the classic slab's is <see cref="Catalog.ClassicFriction"/>).
    /// </summary>
    public sealed class StyleType
    {
        public string Name { get; set; }

        /// <summary>The name shown in the game ("Off-road").</summary>
        public string Title { get; set; }

        /// <summary>The friction of its surface: of the earth of the off-road style.</summary>
        public float Friction { get; set; } = Catalog.ClassicFriction;
    }

    /// <summary>Wheels for the cars.</summary>
    public sealed class WheelType
    {
        /// <summary>The name shown in the garage.</summary>
        public string Name { get; set; }

        /// <summary>Content name of the model of a wheel (e.g. "wheel").</summary>
        public string Model { get; set; }

        /// <summary>
        /// Content name of the model mirrored front to back, for the near side of a car: it turns its tread the same way
        /// as the model turned around on the far side (built from the same file, see content/Content.mgcb).
        /// </summary>
        [JsonIgnore]
        public string MirroredModel => Model + "-mirrored";

        /// <summary>Friction of the tyre: its grip on the track.</summary>
        public float Friction { get; set; } = 1f;

        /// <summary>
        /// How much better or worse the tyre grips on the surface of a style than its <see cref="Friction"/> says (a factor
        /// of the friction of the contact), by the name of the style: an off-road tyre grips the earth better, a racing slick
        /// worse. A style it does not name is 1, and so is the classic one.
        /// </summary>
        public Dictionary<string, float> Grip { get; set; }

        /// <summary>
        /// The factor of the grip of the tyre in a look of the track (a style, or a transition between two, which takes the
        /// mean of both; see <see cref="Catalog.Styles"/>).
        /// </summary>
        public float GripFactor(string look)
        {
            if (look != null && look.IndexOf('-') is int dash and > 0)
                return (GripFactor(look.Substring(0, dash)) + GripFactor(look.Substring(dash + 1))) / 2f;
            return look != null && Grip != null && Grip.TryGetValue(look, out float factor) ? factor : 1f;
        }

        /// <summary>Mass of one wheel of the physics (a pair of wheels, one on each side of the car).</summary>
        public float Mass { get; set; } = 10f;
    }

    /// <summary>
    /// All track blocks, dynamic objects and scenery the levels are built from, and the cars and their wheels.
    /// Shared by the game and the level editor; stored in content/catalog.json.
    /// </summary>
    public sealed class Catalog
    {
        public const string FileName = "catalog.json";

        /// <summary>The style every block has, the blocks of 2016, which a level or a block has unless it names another.</summary>
        public const string ClassicStyle = "classic";

        /// <summary>The friction of the surface of the classic blocks.</summary>
        public const float ClassicFriction = 1f;

        /// <summary>
        /// The styles of the track besides the classic one (#58), as the levels and the looks of the blocks name them
        /// (<see cref="BlockType.Looks"/>): "offroad". A block can also have the look of a transition between two styles,
        /// named "first-second" ("offroad-classic"): the first style at the left edge of the block placed unturned, the
        /// second at its right edge (turned, the other way round).
        /// </summary>
        public List<StyleType> Styles { get; set; } = new List<StyleType>();

        public List<BlockType> Blocks { get; set; } = new List<BlockType>();
        public List<ObjectType> Objects { get; set; } = new List<ObjectType>();
        public List<SceneryType> Scenery { get; set; } = new List<SceneryType>();
        public List<CarType> Cars { get; set; } = new List<CarType>();
        public List<WheelType> Wheels { get; set; } = new List<WheelType>();

        public BlockType FindBlock(string name) => Blocks.FirstOrDefault(block => block.Name == name);

        public ObjectType FindObject(string type) => Objects.FirstOrDefault(obj => obj.Type == type);

        /// <summary>The scenery with the model; null for a model the catalog does not have.</summary>
        public SceneryType FindScenery(string model) => Scenery.FirstOrDefault(prop => prop.Model == model);

        /// <summary>The car with the body model; the first car if there is no such car.</summary>
        public CarType FindCar(string model) => Cars.FirstOrDefault(car => car.Model == model) ?? Cars.FirstOrDefault();

        /// <summary>The wheels with the model; the first wheels if there are no such wheels.</summary>
        public WheelType FindWheel(string model) => Wheels.FirstOrDefault(wheel => wheel.Model == model) ?? Wheels.FirstOrDefault();

        /// <summary>The name is a style: <see cref="ClassicStyle"/> or one of <see cref="Styles"/>.</summary>
        public bool IsStyle(string name) => name == ClassicStyle || FindStyle(name) != null;

        /// <summary>The style of the name (not the classic one); null for none.</summary>
        public StyleType FindStyle(string name) => Styles.FirstOrDefault(style => style.Name == name);

        /// <summary>
        /// The friction of the surface of a look of the track: of its style (the classic slab's for the classic one and an
        /// unknown one), the mean of both for a transition between two.
        /// </summary>
        public float SurfaceFriction(string look)
        {
            if (look != null && look.IndexOf('-') is int dash and > 0)
                return (SurfaceFriction(look.Substring(0, dash)) + SurfaceFriction(look.Substring(dash + 1))) / 2f;
            return FindStyle(look)?.Friction ?? ClassicFriction;
        }

        /// <summary>
        /// How hard a tyre grips the surface of a look of the track: the friction of the contact as the physics mixes the
        /// friction of the tyre and of the surface (the square root of their product, Aether.Physics2D's
        /// <c>Settings.MixFriction</c>), times the grip of the tyre on that surface (<see cref="WheelType.GripFactor"/>).
        /// The bars Grip of the garage show it.
        /// </summary>
        public float ContactGrip(WheelType wheel, string look)
        {
            return System.MathF.Sqrt(wheel.Friction * SurfaceFriction(look)) * wheel.GripFactor(look);
        }

        /// <summary>
        /// The name is a style or a transition between two different styles ("offroad-classic"), which a placed block may
        /// have (see <see cref="BlockType.Looks"/>).
        /// </summary>
        public bool IsStyleOrTransition(string name)
        {
            if (name == null)
                return false;
            if (IsStyle(name))
                return true;
            string[] parts = name.Split('-');
            return parts.Length == 2 && parts[0] != parts[1] && IsStyle(parts[0]) && IsStyle(parts[1]);
        }

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
