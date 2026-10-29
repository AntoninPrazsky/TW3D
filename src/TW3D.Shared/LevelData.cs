using System.Collections.Generic;

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
