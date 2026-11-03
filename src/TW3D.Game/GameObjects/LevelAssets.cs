using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using TW3D.Shared;

namespace TW3D.GameObjects
{
    /// <summary>
    /// What the levels are built from: the catalog of the blocks, dynamic objects and scenery (content/catalog.json), the
    /// models of all of them and the collision shapes of the models. Loaded when the first level loads and shared by all levels.
    /// </summary>
    sealed class LevelAssets
    {
        private static LevelAssets loaded;

        private readonly Dictionary<string, Model> blockModels = new Dictionary<string, Model>();
        private readonly Dictionary<string, Model> objectModels = new Dictionary<string, Model>();
        private readonly Dictionary<string, Model> sceneryModels = new Dictionary<string, Model>();

        private LevelAssets(ContentManager content)
        {
            using (Stream stream = TitleContainer.OpenStream("Content/" + Catalog.FileName))
                Catalog = Catalog.Load(stream);
            ModelCollision.Load();

            foreach (BlockType block in Catalog.Blocks)
                blockModels.Add(block.Name, content.Load<Model>(block.ModelAsset));
            foreach (string model in Catalog.ObjectModels)
                objectModels.Add(model, content.Load<Model>(ObjectType.ModelAsset(model)));
            foreach (SceneryType prop in Catalog.Scenery)
                sceneryModels.Add(prop.Model, content.Load<Model>(prop.ModelAsset));
        }

        /// <summary>The assets, loaded if they were not loaded yet (loading all the models takes a while).</summary>
        public static LevelAssets Load(ContentManager content)
        {
            return loaded ??= new LevelAssets(content);
        }

        public Catalog Catalog { get; }

        public Model BlockModel(BlockType block) => blockModels[block.Name];

        /// <param name="model">One of the models of a dynamic object type (see <see cref="ObjectType.Models"/>).</param>
        public Model ObjectModel(string model) => objectModels[model];

        /// <param name="model">The model of a prop of the scenery (see <see cref="SceneryType.Model"/>).</param>
        public Model SceneryModel(string model) => sceneryModels[model];
    }
}
