using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TW3D.GameObjects;
using TW3D.Graphics;
using TW3D.Shared;

namespace TW3D.Editor
{
    /// <summary>
    /// The level being edited drawn as the game draws it: the blocks of the three layers and the dynamic objects of an
    /// <see cref="EditedLevel"/>, with the models of the <see cref="LevelAssets"/> and without the physics. It draws
    /// whatever the document has in every frame. Blocks and objects the catalog does not know are left out.
    /// </summary>
    sealed class LevelView : GameObject
    {
        //Distance of the back and front layers from the middle one (physics units), as in the game's Level
        private const float LayerDepth = Units.GridCell;

        //The models by the name of the block, and by the type and the model of an object the catalog knows
        private readonly Dictionary<string, Model> blockModels = new Dictionary<string, Model>();
        private readonly Dictionary<(string Type, string Model), Model> objectModels = new Dictionary<(string, string), Model>();

        public LevelView(LevelAssets assets)
        {
            foreach (BlockType block in assets.Catalog.Blocks)
                blockModels[block.Name] = assets.BlockModel(block);
            foreach (ObjectType type in assets.Catalog.Objects)
                foreach (string model in type.Models)
                    objectModels[(type.Type, model)] = assets.ObjectModel(model);
        }

        /// <summary>The level drawn.</summary>
        public EditedLevel Level { get; set; }

        /// <summary>The depth of a layer in physics units (the back layer is behind the middle one, the front in front of it).</summary>
        public static float Depth(Layer layer)
        {
            return layer == Layer.Back ? -LayerDepth : layer == Layer.Front ? LayerDepth : 0f;
        }

        /// <summary>The z of a layer in the scene.</summary>
        public static float SceneDepth(Layer layer) => -Depth(layer) * Units.PhysicsToScene;

        /// <summary>One cell of the grid in scene units.</summary>
        public const float CellSize = Units.GridCell * Units.PhysicsToScene;

        /// <summary>Where a point of a layer given in physics units is in the scene: a physics point (x, y) is drawn at (-x, -y) scaled, see <see cref="Units"/>.</summary>
        public static Vector3 ScenePosition(Layer layer, Vector2 physics)
        {
            return new Vector3(-physics * Units.PhysicsToScene, SceneDepth(layer));
        }

        /// <summary>Where the middle of a cell of a layer is in the scene.</summary>
        public static Vector3 ScenePosition(Layer layer, Cell cell)
        {
            return ScenePosition(layer, new Vector2(cell.X * Units.GridCell, cell.Y * Units.GridCell));
        }

        /// <summary>The cell a point of the scene falls in, whichever layer plane it is on (the inverse of <see cref="ScenePosition(Layer, Cell)"/>).</summary>
        public static Cell CellAt(Vector3 scene)
        {
            return Cell.Of(-scene.X / Units.PhysicsToScene, -scene.Y / Units.PhysicsToScene);
        }

        /// <summary>The position of a block in physics units: its cell, moved down by its shift.</summary>
        public static Vector2 PhysicsPosition(PlacedBlock block)
        {
            return new Vector2(block.Cell.X * Units.GridCell, block.Cell.Y * Units.GridCell + block.Shift * Units.ShiftStep);
        }

        public override void Draw(SceneRenderer renderer)
        {
            if (Level == null)
                return;
            DrawLayer(renderer, Layer.Back);
            DrawLayer(renderer, Layer.Middle);
            DrawLayer(renderer, Layer.Front);
            IReadOnlyList<PlacedObject> objects = Level.Objects;
            for (int i = 0; i < objects.Count; i++)
            {
                PlacedObject obj = objects[i];
                if (objectModels.TryGetValue((obj.Type, obj.Model), out Model model))
                    DrawModel(renderer, model, new Vector3(obj.X, obj.Y, 0f), 0f, false);
            }
        }

        private void DrawLayer(SceneRenderer renderer, Layer layer)
        {
            IReadOnlyList<PlacedBlock> blocks = Level.Blocks(layer);
            float depth = Depth(layer);
            for (int i = 0; i < blocks.Count; i++)
            {
                PlacedBlock block = blocks[i];
                if (blockModels.TryGetValue(block.Name, out Model model))
                    DrawModel(renderer, model, new Vector3(PhysicsPosition(block), depth), 0f, false);
            }
        }

        /// <summary>
        /// The box around all the blocks in the scene, each as large as its cell in its layer; null for a level without
        /// blocks.
        /// </summary>
        public BoundingBox? SceneBounds()
        {
            if (Level == null)
                return null;
            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            float half = CellSize / 2f;
            bool any = false;
            foreach (Layer layer in new[] { Layer.Back, Layer.Middle, Layer.Front })
            {
                foreach (PlacedBlock block in Level.Blocks(layer))
                {
                    Vector3 centre = ScenePosition(layer, PhysicsPosition(block));
                    min = Vector3.Min(min, centre - new Vector3(half));
                    max = Vector3.Max(max, centre + new Vector3(half));
                    any = true;
                }
            }
            return any ? new BoundingBox(min, max) : null;
        }
    }
}
