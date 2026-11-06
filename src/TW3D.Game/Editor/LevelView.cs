using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TW3D.GameObjects;
using TW3D.Graphics;
using TW3D.Shared;

namespace TW3D.Editor
{
    /// <summary>
    /// The level being edited drawn as the game draws it: the blocks of the layers, the dynamic objects and the
    /// scenery of an <see cref="EditedLevel"/>, with the models of the <see cref="LevelAssets"/> and without the physics,
    /// and the block the Place tool would place, see-through (<see cref="Preview"/>). It draws whatever the document has
    /// in every frame. Blocks, objects and props the catalog does not know are left out.
    /// </summary>
    sealed class LevelView : GameObject
    {
        //Distance between two layers (physics units), as in the game's Level
        private const float LayerDepth = Units.GridCell;
        //The preview of the block to be placed: see-through, glowing a little green like the selector, and a little larger
        //than its cell, so that a face it shares with a block of the level (the front of a road) is in front of it
        private const float PreviewAlpha = 0.5f;
        private static readonly Vector3 PreviewGlow = new Vector3(0.03f, 0.12f, 0.05f);
        private const float PreviewScale = 1.01f;

        //The layers that are drawn, by the Layer enum (View menu); the middle layer always is
        private readonly bool[] shown = new bool[Layers.Count];

        //The models of the blocks (with their looks), and by the type and the model of an object the catalog knows
        private readonly LevelAssets assets;
        private readonly Dictionary<(string Type, string Model), Model> objectModels = new Dictionary<(string, string), Model>();
        private readonly Dictionary<string, Model> sceneryModels = new Dictionary<string, Model>();

        public LevelView(LevelAssets assets)
        {
            this.assets = assets;
            foreach (ObjectType type in assets.Catalog.Objects)
                foreach (string model in type.Models)
                    objectModels[(type.Type, model)] = assets.ObjectModel(model);
            foreach (SceneryType prop in assets.Catalog.Scenery)
                sceneryModels[prop.Model] = assets.SceneryModel(prop.Model);
            Array.Fill(shown, true);
        }

        /// <summary>The level drawn.</summary>
        public EditedLevel Level { get; set; }

        /// <summary>
        /// The block the Place tool would place and its layer (see <see cref="BlockTool.Preview"/>), drawn see-through
        /// over the level; null for none. It is not in the level.
        /// </summary>
        public (Layer Layer, PlacedBlock Block)? Preview { get; set; }

        /// <summary>The layer is drawn (View menu); the middle layer always is.</summary>
        public bool Shows(Layer layer) => layer == Layer.Middle || shown[(int)layer];

        /// <summary>Shows or hides a layer (View menu); the middle layer cannot be hidden.</summary>
        public void Show(Layer layer, bool show)
        {
            if (layer != Layer.Middle)
                shown[(int)layer] = show;
        }

        /// <summary>The dynamic objects are drawn.</summary>
        public bool ShowObjects { get; set; } = true;

        /// <summary>The props of the scenery are drawn.</summary>
        public bool ShowScenery { get; set; } = true;

        /// <summary>
        /// Why the tool cannot edit what it works on now, in the words of the status bar: the layer it works on or the
        /// objects or the scenery are hidden. Null when what it works on is seen. The Object tool works on the objects, the
        /// Scenery tool on the scenery, the others on a layer (the Select tool on the props of the layer with its blocks:
        /// a hidden scenery leaves them out of the selection, see <see cref="SelectTool"/>).
        /// </summary>
        public string HiddenNote(EditorTool tool, Layer layer)
        {
            if (tool == EditorTool.Object)
                return ShowObjects ? null : "The objects are hidden.";
            if (tool == EditorTool.Scenery)
                return ShowScenery ? null : "The scenery is hidden.";
            return Shows(layer) ? null : $"The {layer.Name()} layer is hidden.";
        }

        /// <summary>The depth of a layer in physics units (the back layers are behind the middle one, the front in front of it).</summary>
        public static float Depth(Layer layer) => layer.Cells() * LayerDepth;

        /// <summary>The z of a layer in the scene.</summary>
        public static float SceneDepth(Layer layer) => SceneDepth(Depth(layer));

        /// <summary>The z in the scene of a depth in physics units (of a prop of the scenery: negative behind the road).</summary>
        public static float SceneDepth(float depth) => -depth * Units.PhysicsToScene;

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
            foreach (Layer layer in Layers.All)
                if (Shows(layer))
                    DrawLayer(renderer, layer);
            if (Preview is (Layer previewLayer, PlacedBlock preview) && BlockModel(preview) is Model previewModel)
            {
                Matrix world = Matrix.CreateScale(PreviewScale)
                    * ModelWorld(new Vector3(PhysicsPosition(preview), Depth(previewLayer)), 0f, preview.Turned) * Placement;
                renderer.SetGhost(previewModel, world, PreviewAlpha, PreviewGlow);
            }
            if (ShowScenery)
            {
                IReadOnlyList<PlacedScenery> props = Level.Scenery;
                for (int i = 0; i < props.Count; i++)
                {
                    PlacedScenery prop = props[i];
                    if (sceneryModels.TryGetValue(prop.Model, out Model model))
                        renderer.Add(model, SceneryProp.World(prop.X, prop.Y, prop.Z, prop.Turn));
                }
            }
            if (!ShowObjects)
                return;
            IReadOnlyList<PlacedObject> objects = Level.Objects;
            for (int i = 0; i < objects.Count; i++)
            {
                PlacedObject obj = objects[i];
                if (objectModels.TryGetValue((obj.Type, obj.Model), out Model model))
                    DrawModel(renderer, model, new Vector3(obj.X, obj.Y, 0f), 0f, false);
            }
        }

        /// <summary>The model a block is drawn with: its look in its style or the style of the level; null for a block the catalog does not know.</summary>
        private Model BlockModel(PlacedBlock block)
        {
            BlockType type = assets.Catalog.FindBlock(block.Name);
            return type == null ? null : assets.BlockModel(type, block.Style ?? Level?.Style);
        }

        private void DrawLayer(SceneRenderer renderer, Layer layer)
        {
            IReadOnlyList<PlacedBlock> blocks = Level.Blocks(layer);
            float depth = Depth(layer);
            for (int i = 0; i < blocks.Count; i++)
            {
                PlacedBlock block = blocks[i];
                if (BlockModel(block) is Model model)
                    DrawModel(renderer, model, new Vector3(PhysicsPosition(block), depth), 0f, block.Turned, block: true);
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
            foreach (Layer layer in Layers.All)
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
