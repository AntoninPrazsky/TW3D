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
    /// <summary>A track block as placed in a level.</summary>
    sealed class TrackBlock
    {
        public TrackBlock(BlockType type, Model model, Vector2 position)
        {
            Type = type;
            Model = model;
            Position = position;
        }

        public BlockType Type { get; }

        public Model Model { get; }

        /// <summary>Position in physics units, with the vertical shift of the block.</summary>
        public Vector2 Position { get; }

        /// <summary>The physics body of a block of the middle layer; null for the decoration of the back and front layers.</summary>
        public Body Body { get; set; }
    }

    /// <summary>
    /// A level built from its file (see <see cref="LevelFile"/>): the track blocks of the middle layer, which the car
    /// drives on, the decoration blocks of the back and front layers behind and in front of it, and the dynamic objects.
    /// </summary>
    class Level : GameObject
    {
        //Distance of the back and front decoration layers from the middle (physics) layer: one cell
        private const float LayerDepth = Units.GridCell;

        private readonly List<TrackBlock> middle = new List<TrackBlock>();
        private readonly List<TrackBlock> back = new List<TrackBlock>();
        private readonly List<TrackBlock> front = new List<TrackBlock>();
        private readonly List<TrackProp> objects = new List<TrackProp>();
        //The cell of the finish block, from its corner with the least coordinates (physics units); null without a finish
        private readonly (Vector2 Min, Vector2 Max)? finishArea;

        /// <summary>The light of the level.</summary>
        public LevelMood Mood { get; }

        /// <summary>The time a good run of the level takes, in seconds (see <see cref="RaceScore"/>).</summary>
        public float Par { get; }

        /// <summary>The level has a finish block, so a run of it can end (see <see cref="IsAtFinish"/>).</summary>
        public bool HasFinish => finishArea.HasValue;

        /// <param name="fileName">Name of the level file in Content/Levels, without the extension.</param>
        public Level(World world, ScreenManager screenManager, ChaseCamera camera, PhysicsClock physics, LevelAssets assets, Vector2 position, string fileName)
        {
            this.world = world;
            this.screenManager = screenManager;
            this.camera = camera;
            this.physics = physics;
            this.position = position;

            //File system access for MonoGame (works on all platforms)
            LevelData data;
            using (Stream stream = TitleContainer.OpenStream("Content/Levels/" + fileName + LevelFile.Extension))
                data = LevelFile.Load(stream);

            List<string> problems = data.Validate(assets.Catalog);
            if (problems.Count > 0)
                throw new InvalidDataException("Level \"" + fileName + "\" is not valid:\n" + string.Join("\n", problems));

            Mood = data.Mood;
            Par = RaceScore.Par(data);
            BlockPlacement finish = data.FinishBlock;
            if (finish != null)
            {
                //A block is as large as its cell, around its position
                Vector2 centre = PhysicsPosition(finish);
                finishArea = (centre - new Vector2(Units.GridCell / 2f), centre + new Vector2(Units.GridCell / 2f));
            }

            foreach (BlockPlacement placement in data.Middle)
                middle.Add(CreateBlock(placement, assets, true));
            foreach (BlockPlacement placement in data.Back)
                back.Add(CreateBlock(placement, assets, false));
            foreach (BlockPlacement placement in data.Front)
                front.Add(CreateBlock(placement, assets, false));
            foreach (ObjectPlacement placement in data.Objects)
                objects.Add(new TrackProp(world, screenManager, camera, physics, new Vector2(placement.X, placement.Y),
                    assets.Catalog.FindObject(placement.Type), placement.Model, assets));
        }

        /// <summary>Where a block of the level grid is in the physics world (see <see cref="Units"/>).</summary>
        private static Vector2 PhysicsPosition(BlockPlacement placement)
        {
            return new Vector2(placement.X * Units.GridCell, placement.Y * Units.GridCell + placement.Shift * Units.ShiftStep);
        }

        /// <param name="physical">The block is in the middle layer: it takes part in the physics simulation.</param>
        private TrackBlock CreateBlock(BlockPlacement placement, LevelAssets assets, bool physical)
        {
            BlockType type = assets.Catalog.FindBlock(placement.Block);
            TrackBlock block = new TrackBlock(type, assets.BlockModel(type), PhysicsPosition(placement));
            if (physical)
            {
                block.Body = CreateModelBody(type.ModelAsset, BodyType.Static, CollisionCategories.Track);
                block.Body.Position = block.Position;
                block.Body.SetRestitution(1f);
                block.Body.SetFriction(1f);
            }
            return block;
        }

        /// <summary>The point (the middle of the car, in physics units) is in the cell of the finish block.</summary>
        public bool IsAtFinish(Vector2 point)
        {
            if (!(finishArea is (Vector2 min, Vector2 max)))
                return false;
            return point.X >= min.X && point.X <= max.X && point.Y >= min.Y && point.Y <= max.Y;
        }

        public override void Update()
        {
            foreach (TrackProp obj in objects)
                obj.Update();
        }

        public override void Draw(SceneRenderer renderer)
        {
            //Models outside the camera view are skipped by the renderer
            DrawLayer(renderer, back, -LayerDepth);
            DrawLayer(renderer, middle, 0f);
            DrawLayer(renderer, front, LayerDepth);
            foreach (TrackProp obj in objects)
                obj.Draw(renderer);
        }

        private void DrawLayer(SceneRenderer renderer, List<TrackBlock> layer, float depth)
        {
            foreach (TrackBlock block in layer)
                DrawModel(renderer, block.Model, new Vector3(block.Position, depth), 0f, false);
        }
    }
}
