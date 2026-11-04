using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;

namespace TW3D.Shared
{
    /// <summary>A track block on the level grid.</summary>
    public sealed class BlockPlacement
    {
        /// <summary>Name of the block type in the catalog.</summary>
        public string Block { get; set; }

        /// <summary>Grid position; one grid cell is 22.5 physics units.</summary>
        public int X { get; set; }
        public int Y { get; set; }

        /// <summary>Vertical shift in steps of 1/9 of a grid cell (2.5 physics units).</summary>
        public int Shift { get; set; }

        /// <summary>The finish of the level (a block of the middle layer): the car reaching its cell finishes the level.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Finish { get; set; }

        /// <summary>
        /// How many cells behind the road a block of the back layer stands: 1 (the default) to
        /// <see cref="LevelData.BackLayers"/>. The blocks of the middle and the front layer have 1.
        /// </summary>
        [JsonIgnore]
        public int Depth { get; set; } = 1;

        //The depth in the file: left out when it is 1, as in the levels made before there were deeper back layers
        [JsonInclude]
        [JsonPropertyName("depth")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        private int? FileDepth
        {
            get => Depth == 1 ? null : Depth;
            set => Depth = value ?? 1;
        }
    }

    /// <summary>A dynamic object.</summary>
    public sealed class ObjectPlacement
    {
        /// <summary>Name of the object type in the catalog.</summary>
        public string Type { get; set; }

        /// <summary>One of the models of the object type.</summary>
        public string Model { get; set; }

        /// <summary>Position in physics units.</summary>
        public float X { get; set; }
        public float Y { get; set; }
    }

    /// <summary>A prop of the scenery: it stands beside the track, without physics.</summary>
    public sealed class SceneryPlacement
    {
        /// <summary>A quarter turn is this many radians; <see cref="Turn"/> counts them.</summary>
        public const float QuarterTurn = (float)(System.Math.PI / 2);

        /// <summary>The model of the prop in the catalog (see <see cref="SceneryType.Model"/>).</summary>
        public string Model { get; set; }

        /// <summary>Where its foot stands in physics units, as the position of a dynamic object (y grows downward).</summary>
        public float X { get; set; }
        public float Y { get; set; }

        /// <summary>
        /// The depth in physics units: negative behind the road, positive in front of it. The road of the middle layer
        /// reaches half a cell (11.25) to either side; the back and front layers stand one cell (22.5) away from it.
        /// </summary>
        public float Z { get; set; }

        /// <summary>Quarter turns about the vertical axis, 0 to 3: a fence along the road turned once runs across it.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Turn { get; set; }
    }

    /// <summary>The light of a level.</summary>
    public enum LevelMood
    {
        Day,
        Sunset,
        Night
    }

    /// <summary>
    /// A level: track blocks in layers, dynamic objects and scenery. The middle layer is the road; behind it are up to
    /// <see cref="BackLayers"/> back layers (one list, by the <see cref="BlockPlacement.Depth"/> of the blocks), in front
    /// of it one front layer. Only the middle layer takes part in the physics simulation; the back and front layers are
    /// decoration, and so is the scenery.
    /// </summary>
    public sealed class LevelData
    {
        public const int CurrentFormat = 1;

        /// <summary>
        /// How many layers there are behind the road, a cell apart. In front of it there is only one: the camera is
        /// 40 physics units in front of the road, so a second front layer would be behind it.
        /// </summary>
        public const int BackLayers = 3;

        /// <summary>Version of the file format.</summary>
        public int Format { get; set; } = CurrentFormat;

        /// <summary>
        /// Position of the level in the level select (optional). Levels are sorted by it;
        /// levels without it come after the others, sorted by name.
        /// </summary>
        public int? Order { get; set; }

        /// <summary>The light of the level (optional, a day by default).</summary>
        public LevelMood Mood { get; set; }

        /// <summary>
        /// The time a good run takes, in seconds (optional): finishing in it scores 1000 points and three stars.
        /// Without it the game estimates it from how far the finish is.
        /// </summary>
        public float? Par { get; set; }

        /// <summary>The finish block of the level; null if it has none.</summary>
        [JsonIgnore]
        public BlockPlacement FinishBlock => Middle.FirstOrDefault(block => block.Finish);

        public List<BlockPlacement> Middle { get; set; } = new List<BlockPlacement>();
        public List<BlockPlacement> Back { get; set; } = new List<BlockPlacement>();
        public List<BlockPlacement> Front { get; set; } = new List<BlockPlacement>();
        public List<ObjectPlacement> Objects { get; set; } = new List<ObjectPlacement>();

        /// <summary>The props beside the track (optional in the file: a level without them leaves the list out).</summary>
        public List<SceneryPlacement> Scenery { get; set; } = new List<SceneryPlacement>();

        /// <summary>Checks the level against the catalog and returns a description of every problem found.</summary>
        public List<string> Validate(Catalog catalog)
        {
            List<string> problems = new List<string>();

            void CheckLayer(List<BlockPlacement> layer, string layerName, bool needsCollision)
            {
                foreach (BlockPlacement placement in layer)
                {
                    BlockType block = catalog.FindBlock(placement.Block);
                    if (block == null)
                        problems.Add($"Unknown block \"{placement.Block}\" at [{placement.X}, {placement.Y}] in the {layerName} layer.");
                    else if (needsCollision && !block.Collision)
                        problems.Add($"Block \"{placement.Block}\" at [{placement.X}, {placement.Y}] has no collision shape, so it cannot be in the {layerName} layer.");
                }
            }

            CheckLayer(Middle, "middle", true);
            CheckLayer(Back, "back", false);
            CheckLayer(Front, "front", false);
            foreach (BlockPlacement placement in Back.Where(block => block.Depth < 1 || block.Depth > BackLayers))
                problems.Add($"Block \"{placement.Block}\" at [{placement.X}, {placement.Y}] of the back layer has depth {placement.Depth}; the back layers are 1 to {BackLayers} cells behind the road.");
            foreach (BlockPlacement placement in Middle.Concat(Front).Where(block => block.Depth != 1))
                problems.Add($"Block \"{placement.Block}\" at [{placement.X}, {placement.Y}] has depth {placement.Depth}, but only the blocks of the back layer can stand deeper.");
            foreach (BlockPlacement placement in Back.Concat(Front).Where(block => block.Finish))
                problems.Add($"Block \"{placement.Block}\" at [{placement.X}, {placement.Y}] is a finish, but only a block of the middle layer can be.");
            if (Middle.Count(block => block.Finish) > 1)
                problems.Add("The level has more than one finish.");
            if (Par <= 0f)
                problems.Add("The par time of the level is not positive.");

            foreach (ObjectPlacement placement in Objects)
            {
                ObjectType type = catalog.FindObject(placement.Type);
                if (type == null)
                    problems.Add($"Unknown object type \"{placement.Type}\" at [{placement.X}, {placement.Y}].");
                else if (!type.Models.Contains(placement.Model))
                    problems.Add($"Object type \"{placement.Type}\" has no model \"{placement.Model}\" (at [{placement.X}, {placement.Y}]).");
            }

            foreach (SceneryPlacement placement in Scenery)
            {
                string at = string.Create(CultureInfo.InvariantCulture, $"[{placement.X}, {placement.Y}, {placement.Z}]");
                if (catalog.FindScenery(placement.Model) == null)
                    problems.Add($"Unknown scenery \"{placement.Model}\" at {at}.");
                if (placement.Turn < 0 || placement.Turn > 3)
                    problems.Add($"Scenery \"{placement.Model}\" at {at} has turn {placement.Turn}; a turn is 0 to 3 quarter turns.");
            }

            return problems;
        }
    }
}
