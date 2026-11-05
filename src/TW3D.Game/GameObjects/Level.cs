using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public TrackBlock(BlockType type, Model model, Vector2 position, float depth, bool turned)
        {
            Type = type;
            Model = model;
            Position = position;
            Depth = depth;
            Turned = turned;
        }

        public BlockType Type { get; }

        public Model Model { get; }

        /// <summary>Position in physics units, with the vertical shift of the block.</summary>
        public Vector2 Position { get; }

        /// <summary>How far in front of the road the block stands, in physics units: negative behind it, 0 on it.</summary>
        public float Depth { get; }

        /// <summary>The block is turned round by 180° about the vertical axis (see <see cref="BlockPlacement.Turned"/>).</summary>
        public bool Turned { get; }

        /// <summary>The physics body of a block of the middle layer; null for the decoration of the back and front layers.</summary>
        public Body Body { get; set; }
    }

    /// <summary>
    /// A level built from its data (its file, see <see cref="LevelFile"/>, or the level editor's): the track blocks of
    /// the middle layer, which the car drives on, the decoration blocks of the back layers behind it and of the front
    /// layer in front of it, the dynamic objects, and the scenery beside the track (static props without physics).
    /// </summary>
    class Level : GameObject
    {
        //Distance between the layers: the front layer and the first back layer are one cell from the middle (physics)
        //layer, the deeper back layers a cell further each
        private const float LayerDepth = Units.GridCell;

        private readonly List<TrackBlock> middle = new List<TrackBlock>();
        private readonly List<TrackBlock> back = new List<TrackBlock>();
        private readonly List<TrackBlock> front = new List<TrackBlock>();
        private readonly List<TrackProp> objects = new List<TrackProp>();
        //The props of the scenery: their models and where they stand, which never changes
        private readonly List<(Model Model, Matrix World)> scenery = new List<(Model, Matrix)>();
        //The cell of the finish block, from its corner with the least coordinates (physics units); null without a finish
        private readonly (Vector2 Min, Vector2 Max)? finishArea;

        /// <summary>The light of the level.</summary>
        public LevelMood Mood { get; }

        /// <summary>The time a good run of the level takes, in seconds (see <see cref="RaceScore"/>).</summary>
        public float Par { get; }

        /// <summary>The level has a finish block, so a run of it can end (see <see cref="IsAtFinish"/>).</summary>
        public bool HasFinish => finishArea.HasValue;

        /// <summary>
        /// How low the lowest block of the track reaches (physics y, which grows downwards); below it there is nothing to
        /// land on.
        /// </summary>
        public float Bottom { get; }

        /// <param name="fileName">Name of the level file in Content/Levels, without the extension.</param>
        public Level(World world, ScreenManager screenManager, ChaseCamera camera, PhysicsClock physics, LevelAssets assets, Vector2 position, string fileName)
            : this(world, screenManager, camera, physics, assets, position, Read(fileName), fileName)
        {
        }

        /// <param name="data">The level, from its file or from the level editor (a test drive).</param>
        /// <param name="name">What the message about the problems of the level calls it.</param>
        /// <exception cref="InvalidDataException">The level is not valid (see <see cref="LevelData.Validate"/>).</exception>
        public Level(World world, ScreenManager screenManager, ChaseCamera camera, PhysicsClock physics, LevelAssets assets, Vector2 position, LevelData data, string name)
        {
            this.world = world;
            this.screenManager = screenManager;
            this.camera = camera;
            this.physics = physics;
            this.position = position;

            List<string> problems = data.Validate(assets.Catalog);
            if (problems.Count > 0)
                throw new InvalidDataException("Level \"" + name + "\" is not valid:\n" + string.Join("\n", problems));

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
                middle.Add(CreateBlock(placement, assets, 0f, true));
            //A block is as large as its cell, around its position
            Bottom = middle.Count > 0 ? middle.Max(block => block.Position.Y) + Units.GridCell / 2f : 0f;
            foreach (BlockPlacement placement in data.Back)
                back.Add(CreateBlock(placement, assets, -placement.Depth * LayerDepth, false));
            foreach (BlockPlacement placement in data.Front)
                front.Add(CreateBlock(placement, assets, LayerDepth, false));
            foreach (ObjectPlacement placement in data.Objects)
                objects.Add(new TrackProp(world, screenManager, camera, physics, new Vector2(placement.X, placement.Y),
                    assets.Catalog.FindObject(placement.Type), placement.Model, assets));
            foreach (SceneryPlacement placement in data.Scenery)
                scenery.Add((assets.SceneryModel(placement.Model), SceneryProp.World(placement.X, placement.Y, placement.Z, placement.Turn)));
        }

        /// <summary>Reads a level file of Content/Levels (the name without the extension).</summary>
        private static LevelData Read(string fileName)
        {
            //File system access for MonoGame (works on all platforms)
            using (Stream stream = TitleContainer.OpenStream("Content/Levels/" + fileName + LevelFile.Extension))
                return LevelFile.Load(stream);
        }

        /// <summary>Where a block of the level grid is in the physics world (see <see cref="Units"/>).</summary>
        internal static Vector2 PhysicsPosition(BlockPlacement placement)
        {
            return new Vector2(placement.X * Units.GridCell, placement.Y * Units.GridCell + placement.Shift * Units.ShiftStep);
        }

        /// <param name="depth">How far in front of the road the block stands (physics units, negative behind it).</param>
        /// <param name="physical">The block is in the middle layer: it takes part in the physics simulation.</param>
        private TrackBlock CreateBlock(BlockPlacement placement, LevelAssets assets, float depth, bool physical)
        {
            BlockType type = assets.Catalog.FindBlock(placement.Block);
            TrackBlock block = new TrackBlock(type, assets.BlockModel(type), PhysicsPosition(placement), depth, placement.Turned);
            if (physical)
            {
                block.Body = CreateModelBody(type.ModelAsset, BodyType.Static, CollisionCategories.Track, placement.Turned);
                block.Body.Position = block.Position;
                block.Body.SetRestitution(1f);
                block.Body.SetFriction(1f);
            }
            return block;
        }

        /// <summary>
        /// The middle of the signal lamps of the start gate nearest to a point (the meshes "Signal…" of a block of the
        /// middle layer, see SceneRenderer.StartLights), in physics units; null when no block within a few cells of it
        /// has any.
        /// </summary>
        public Vector2? StartSignals(Vector2 near)
        {
            Vector2? nearest = null;
            foreach (TrackBlock block in middle)
            {
                if (!(SignalsOf(block.Model) is Vector2 centre))
                    continue;
                //A model unit is -2.5 physics units; a turned block has the lamps on its other side
                Vector2 signals = block.Position + new Vector2(block.Turned ? -centre.X : centre.X, centre.Y) * Units.ModelToPhysics;
                if (Math.Abs(signals.X - near.X) > SignalsReach)
                    continue;
                if (!nearest.HasValue || Vector2.DistanceSquared(signals, near) < Vector2.DistanceSquared(nearest.Value, near))
                    nearest = signals;
            }
            return nearest;
        }

        //How far from the start (physics units) the signal lamps of a start gate may be to be framed by the camera
        private const float SignalsReach = 4f * Units.GridCell;

        /// <summary>The middle of the signal lamps of a model in its own units (x, y); null for a model without them.</summary>
        private static Vector2? SignalsOf(Model model)
        {
            Matrix[] transforms = null;
            Vector3 sum = Vector3.Zero;
            int count = 0;
            foreach (ModelMesh mesh in model.Meshes)
            {
                if (mesh.Name == null || !mesh.Name.StartsWith("Signal", StringComparison.Ordinal))
                    continue;
                if (transforms == null)
                {
                    transforms = new Matrix[model.Bones.Count];
                    model.CopyAbsoluteBoneTransformsTo(transforms);
                }
                sum += Vector3.Transform(mesh.BoundingSphere.Center, transforms[mesh.ParentBone.Index]);
                count++;
            }
            return count == 0 ? null : new Vector2(sum.X, sum.Y) / count;
        }

        /// <summary>The point (the middle of the car, in physics units) is in the cell of the finish block.</summary>
        public bool IsAtFinish(Vector2 point)
        {
            if (!(finishArea is (Vector2 min, Vector2 max)))
                return false;
            return point.X >= min.X && point.X <= max.X && point.Y >= min.Y && point.Y <= max.Y;
        }

        /// <summary>
        /// The surface of the model of a body of the level, in the coordinates of the model; null for the track and for the
        /// objects whose models have none (see <see cref="ModelSurface"/>).
        /// </summary>
        public ModelSurface SurfaceOf(Body body)
        {
            foreach (TrackProp obj in objects)
            {
                if (obj.Body == body)
                    return obj.Surface;
            }
            return null;
        }

        public override void Update()
        {
            foreach (TrackProp obj in objects)
                obj.Update();
        }

        public override void Draw(SceneRenderer renderer)
        {
            //Models outside the camera view are skipped by the renderer
            DrawLayer(renderer, back);
            DrawLayer(renderer, middle);
            DrawLayer(renderer, front);
            foreach ((Model model, Matrix world) in scenery)
                renderer.Add(model, world * Placement);
            foreach (TrackProp obj in objects)
                obj.Draw(renderer);
        }

        private void DrawLayer(SceneRenderer renderer, List<TrackBlock> layer)
        {
            foreach (TrackBlock block in layer)
                DrawModel(renderer, block.Model, new Vector3(block.Position, block.Depth), 0f, block.Turned, block: true);
        }
    }
}
