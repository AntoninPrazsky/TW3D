using System;
using TW3D.Shared;

namespace TW3D.Editor
{
    /// <summary>
    /// The layers of a level from the back to the front, a cell apart: the three back layers, the middle layer (the
    /// road) and the front layer (see <see cref="LevelData.BackLayers"/>). The editor edits one at a time.
    /// </summary>
    enum Layer
    {
        Back3,
        Back2,
        Back,
        Middle,
        Front
    }

    /// <summary>What the layers are: their depths, their names and the back layers of the level file.</summary>
    static class Layers
    {
        /// <summary>All the layers from the back to the front.</summary>
        public static readonly Layer[] All = Enum.GetValues<Layer>();

        /// <summary>How many layers there are.</summary>
        public static int Count => All.Length;

        /// <summary>How many cells in front of the road the layer is: -3 to -1 for the back layers, 0 for the middle, 1 for the front.</summary>
        public static int Cells(this Layer layer) => (int)layer - (int)Layer.Middle;

        /// <summary>The layer is one of the back layers.</summary>
        public static bool IsBack(this Layer layer) => layer < Layer.Middle;

        /// <summary>How many cells behind the road a back layer is, as the level file keeps it (<see cref="BlockPlacement.Depth"/>).</summary>
        public static int BackDepth(this Layer layer) => -layer.Cells();

        /// <summary>The back layer a given number of cells behind the road (1 to <see cref="LevelData.BackLayers"/>; nearer and further ones give the nearest back layer there is).</summary>
        public static Layer OfBackDepth(int depth) => (Layer)((int)Layer.Middle - Math.Clamp(depth, 1, LevelData.BackLayers));

        /// <summary>The layer a given number of cells in front of the road, the nearest one there is beyond the first and the last.</summary>
        public static Layer OfCells(int cells) => (Layer)Math.Clamp((int)Layer.Middle + cells, 0, All.Length - 1);

        /// <summary>The name of the layer in the texts of the editor: "back", "back 2", "back 3", "middle", "front".</summary>
        public static string Name(this Layer layer) => layer switch
        {
            Layer.Back3 => "back 3",
            Layer.Back2 => "back 2",
            Layer.Back => "back",
            Layer.Middle => "middle",
            _ => "front"
        };

        /// <summary>The name with a capital letter, for the menus: "Back 2".</summary>
        public static string Title(this Layer layer)
        {
            string name = layer.Name();
            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }
    }
}
