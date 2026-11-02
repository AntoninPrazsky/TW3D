using Microsoft.Xna.Framework;
using TW3D.GameObjects;
using TW3D.Graphics;
using TW3D.Shared;

namespace TW3D.Editor
{
    /// <summary>
    /// The level being edited drawn as the game draws it: the blocks of the three layers and the dynamic objects of a
    /// <see cref="LevelData"/>, with the models of the <see cref="LevelAssets"/> and without the physics. It draws
    /// whatever the data has in every frame, so the data is changed in place. Blocks and objects the catalog does not
    /// know are left out.
    /// </summary>
    sealed class LevelView : GameObject
    {
        //Distance of the back and front layers from the middle one (physics units), as in the game's Level
        private const float LayerDepth = Units.GridCell;

        private readonly LevelAssets assets;

        public LevelView(LevelAssets assets)
        {
            this.assets = assets;
        }

        /// <summary>The level drawn.</summary>
        public LevelData Data { get; set; }

        /// <summary>The depth of a layer in physics units (the back layer is behind the middle one, the front in front of it).</summary>
        public static float Depth(Layer layer)
        {
            return layer == Layer.Back ? -LayerDepth : layer == Layer.Front ? LayerDepth : 0f;
        }

        /// <summary>The z of a layer in the scene.</summary>
        public static float SceneDepth(Layer layer) => -Depth(layer) * Units.PhysicsToScene;

        public override void Draw(SceneRenderer renderer)
        {
            if (Data == null)
                return;
            DrawLayer(renderer, Data.Back, Depth(Layer.Back));
            DrawLayer(renderer, Data.Middle, Depth(Layer.Middle));
            DrawLayer(renderer, Data.Front, Depth(Layer.Front));
            foreach (ObjectPlacement placement in Data.Objects)
            {
                ObjectType type = assets.Catalog.FindObject(placement.Type);
                if (type != null && type.Models.Contains(placement.Model))
                    DrawModel(renderer, assets.ObjectModel(placement.Model), new Vector3(placement.X, placement.Y, 0f), 0f, false);
            }
        }

        private void DrawLayer(SceneRenderer renderer, System.Collections.Generic.List<BlockPlacement> layer, float depth)
        {
            foreach (BlockPlacement placement in layer)
            {
                BlockType type = assets.Catalog.FindBlock(placement.Block);
                if (type != null)
                    DrawModel(renderer, assets.BlockModel(type), new Vector3(Level.PhysicsPosition(placement), depth), 0f, false);
            }
        }

        /// <summary>
        /// The box around all the blocks in the scene, each as large as its cell in its layer; null for a level without
        /// blocks.
        /// </summary>
        public BoundingBox? SceneBounds()
        {
            if (Data == null)
                return null;
            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            float half = Units.GridCell * Units.PhysicsToScene / 2f;
            bool any = false;
            foreach ((System.Collections.Generic.List<BlockPlacement> blocks, Layer layer) in new[] { (Data.Back, Layer.Back), (Data.Middle, Layer.Middle), (Data.Front, Layer.Front) })
            {
                foreach (BlockPlacement placement in blocks)
                {
                    //A physics point (x, y) is drawn at (-x, -y) scaled, see Units
                    Vector2 position = -Level.PhysicsPosition(placement) * Units.PhysicsToScene;
                    Vector3 centre = new Vector3(position, SceneDepth(layer));
                    min = Vector3.Min(min, centre - new Vector3(half));
                    max = Vector3.Max(max, centre + new Vector3(half));
                    any = true;
                }
            }
            return any ? new BoundingBox(min, max) : null;
        }
    }
}
