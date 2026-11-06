using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using TW3D.Graphics;
using TW3D.Shared;

namespace TW3D.GameObjects
{
    /// <summary>
    /// What the levels are built from: the catalog of the blocks, dynamic objects and scenery (content/catalog.json), the
    /// models of all of them (the blocks with their looks in the styles of the track), the collision shapes of the models
    /// and the surfaces of the models of the objects. Loaded when the first level loads and shared by all levels.
    /// </summary>
    sealed class LevelAssets
    {
        private static LevelAssets loaded;

        private readonly Dictionary<string, Model> blockModels = new Dictionary<string, Model>();
        private readonly Dictionary<string, Model> objectModels = new Dictionary<string, Model>();
        private readonly Dictionary<string, ModelSurface> objectSurfaces = new Dictionary<string, ModelSurface>();
        private readonly Dictionary<string, Model> sceneryModels = new Dictionary<string, Model>();

        private LevelAssets(ContentManager content)
        {
            using (Stream stream = TitleContainer.OpenStream("Content/" + Catalog.FileName))
                Catalog = Catalog.Load(stream);
            ModelCollision.Load();

            foreach (BlockType block in Catalog.Blocks)
            {
                blockModels.Add(block.ModelAsset, content.Load<Model>(block.ModelAsset));
                if (block.Looks != null)
                    foreach (string style in block.Looks.Keys)
                        blockModels.TryAdd(block.LookAsset(style), content.Load<Model>(block.LookAsset(style)));
            }
            foreach (string model in Catalog.ObjectModels)
            {
                objectModels.Add(model, content.Load<Model>(ObjectType.ModelAsset(model)));
                objectSurfaces.Add(model, ModelSurface.Of(objectModels[model]));
            }
            foreach (SceneryType prop in Catalog.Scenery)
                sceneryModels.Add(prop.Model, content.Load<Model>(prop.ModelAsset));
        }

        /// <summary>The assets, loaded if they were not loaded yet (loading all the models takes a while).</summary>
        public static LevelAssets Load(ContentManager content)
        {
            return loaded ??= new LevelAssets(content);
        }

        public Catalog Catalog { get; }

        /// <summary>The model a block is drawn with in a style (its look, see <see cref="BlockType.LookAsset"/>); null for the classic one.</summary>
        public Model BlockModel(BlockType block, string style = null) => blockModels[block.LookAsset(style)];

        /// <param name="model">One of the models of a dynamic object type (see <see cref="ObjectType.Models"/>).</param>
        public Model ObjectModel(string model) => objectModels[model];

        /// <summary>The surface of a model of a dynamic object type, which the skid marks lie on; null if it has none.</summary>
        public ModelSurface ObjectSurface(string model) => objectSurfaces[model];

        /// <param name="model">The model of a prop of the scenery (see <see cref="SceneryType.Model"/>).</param>
        public Model SceneryModel(string model) => sceneryModels[model];
    }
}
