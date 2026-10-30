using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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

        /// <summary>The light of the level.</summary>
        public LevelMood Mood { get; }

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
            Mood = data.Mood;

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
                Body trackBlockBody = CreateModelBody(type.ModelAsset, BodyType.Static, CollisionCategories.Track);
                trackBlockBody.Position = trackBlock.blockAbsPosition;
                trackBlockBody.SetRestitution(1f);
                trackBlockBody.SetFriction(1f);
                trackBlock.blockBody = trackBlockBody;
            }

            return trackBlock;
        }

        private TrackProp CreateObject(ObjectPlacement placement, ObjectType type)
        {
            return new TrackProp(world, screenManager, camera, physics, new Vector2(placement.X, placement.Y), type, placement.Model);
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
