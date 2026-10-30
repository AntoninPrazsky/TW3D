using System.Collections.Generic;
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

    /// <summary>The light of a level.</summary>
    public enum LevelMood
    {
        Day,
        Sunset,
        Night
    }

    /// <summary>
    /// A level: track blocks in three layers and dynamic objects.
    /// Only the middle layer takes part in the physics simulation; the back and front layers are decoration.
    /// </summary>
    public sealed class LevelData
    {
        public const int CurrentFormat = 1;

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
                        problems.Add($"Block \"{placement.Block}\" at [{placement.X}, {placement.Y}] has no collision bitmap, so it cannot be in the {layerName} layer.");
                }
            }

            CheckLayer(Middle, "middle", true);
            CheckLayer(Back, "back", false);
            CheckLayer(Front, "front", false);
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

            return problems;
        }
    }
}
