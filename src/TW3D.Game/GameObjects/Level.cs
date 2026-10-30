using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Dynamics;
using TW3D.GamePlay;
using TW3D.Graphics;
using TW3D.Screens;
using TW3D.Shared;

namespace TW3D.GameObjects
{
    class TrackBlock
    {
        public Body blockBody { get; set; }
        public Vector2 blockPosition { get; set; }
        public Vector2 blockAbsPosition { get { return new Vector2(blockPosition.X, blockPosition.Y + blockShift); } }
        public float blockShift { get; set; }
        public Model blockModel { get; set; }
        public string modelName { get; set; }
    }

    static class TrackBlockVertices //Vertices found in the block bitmaps
    {
        public static Dictionary<string, List<Vertices>> BlockVertices = new Dictionary<string,List<Vertices>>();
    }

    static class TrackPropVertices //Vertices found in the dynamic object bitmaps
    {
        public static Dictionary<string, List<Vertices>> ObjectVertices = new Dictionary<string, List<Vertices>>();
    }

    static class TrackBlockModels //Track block models
    {
        public static Dictionary<string, Model> Models = new Dictionary<string, Model>();
    }

    static class TrackPropModels //Dynamic object models
    {
        public static Dictionary<string, Model> Models = new Dictionary<string, Model>();
    }


    class Level : GameObject
    {
        //One grid cell of a level is 22.5 physics units, one step of the vertical shift 2.5 physics units
        private const float GridSize = 22.5f;
        private const float ShiftStep = 2.5f;
        //Distance of the back and front decoration layers from the middle (physics) layer
        private const float LayerDepth = 22.5f;

        private readonly List<TrackBlock> trackBlocks = new List<TrackBlock>();
        private readonly List<TrackBlock> backTrackBlocks = new List<TrackBlock>();
        private readonly List<TrackBlock> frontTrackBlocks = new List<TrackBlock>();
        private readonly List<TrackProp> gObjects = new List<TrackProp>();

        public Level(World World, ScreenManager ScreenManager, ChaseCamera Camera, PhysicsClock Physics, Catalog catalog, Vector2 Position, string fileName)
        {
            world = World;
            screenManager = ScreenManager;
            camera = Camera;
            physics = Physics;
            position = Position;

            //File system access for MonoGame (works on all platforms)
            LevelData data;
            using (Stream stream = TitleContainer.OpenStream("Content/Levels/" + fileName + LevelFile.Extension))
                data = LevelFile.Load(stream);

            List<string> problems = data.Validate(catalog);
            if (problems.Count > 0)
                throw new InvalidDataException("Level \"" + fileName + "\" is not valid:\n" + string.Join("\n", problems));

            foreach (BlockPlacement placement in data.Middle)
                trackBlocks.Add(CreateBlock(placement, catalog.FindBlock(placement.Block), true));
            foreach (BlockPlacement placement in data.Back)
                backTrackBlocks.Add(CreateBlock(placement, catalog.FindBlock(placement.Block), false));
            foreach (BlockPlacement placement in data.Front)
                frontTrackBlocks.Add(CreateBlock(placement, catalog.FindBlock(placement.Block), false));
            foreach (ObjectPlacement placement in data.Objects)
                gObjects.Add(CreateObject(placement, catalog.FindObject(placement.Type)));
        }

        private TrackBlock CreateBlock(BlockPlacement placement, BlockType type, bool physical)
        {
            TrackBlock trackBlock = new TrackBlock();
            trackBlock.blockPosition = new Vector2(placement.X, placement.Y) * GridSize;
            trackBlock.blockShift = placement.Shift * ShiftStep;
            trackBlock.blockModel = TrackBlockModels.Models[type.Name];
            trackBlock.modelName = type.Name;

            //Only the blocks of the middle layer take part in the physics simulation
            if (physical)
            {
                //The shape of a block is found in its collision bitmap only once and then reused
                if (!TrackBlockVertices.BlockVertices.TryGetValue(type.Name, out List<Vertices> vertices))
                {
                    vertices = CreateListOfVertices(type.CollisionAsset, 10f);
                    TrackBlockVertices.BlockVertices.Add(type.Name, vertices);
                }

                Body trackBlockBody = CreatePolyBody(vertices, BodyType.Static);
                trackBlockBody.Position = trackBlock.blockAbsPosition;
                trackBlockBody.SetRestitution(1f);
                trackBlockBody.SetFriction(1f);
                trackBlock.blockBody = trackBlockBody;
            }

            return trackBlock;
        }

        private TrackProp CreateObject(ObjectPlacement placement, ObjectType type)
        {
            //Objects with a bitmap shape: the bitmap of the model is vectorized only once and then reused
            List<Vertices> vertices = null;
            if (type.Shape == ObjectShape.Bitmap && !TrackPropVertices.ObjectVertices.TryGetValue(placement.Model, out vertices))
            {
                vertices = CreateListOfVertices(ObjectType.CollisionAsset(placement.Model), 10f);
                TrackPropVertices.ObjectVertices.Add(placement.Model, vertices);
            }

            return new TrackProp(world, screenManager, camera, physics, new Vector2(placement.X, placement.Y), type, TrackPropModels.Models[placement.Model], vertices);
        }

        public override void Update()
        {
            foreach (GameObject obj in gObjects)
            {
                obj.Update();
            }
        }

        public override void Draw(SceneRenderer renderer)
        {
            //Models outside the camera view are skipped by the renderer
            foreach (TrackBlock tb in backTrackBlocks)
                DrawModel(renderer, tb.blockModel, new Vector3(tb.blockAbsPosition.X, tb.blockAbsPosition.Y, -LayerDepth), 0f, false);

            foreach (TrackBlock tb in trackBlocks)
                DrawModel(renderer, tb.blockModel, new Vector3(tb.blockBody.Position.X, tb.blockBody.Position.Y, 0f), 0f, false);

            foreach (TrackBlock tb in frontTrackBlocks)
                DrawModel(renderer, tb.blockModel, new Vector3(tb.blockAbsPosition.X, tb.blockAbsPosition.Y, LayerDepth), 0f, false);

            foreach (TrackProp obj in gObjects)
                obj.Draw(renderer);
        }
    }
}
