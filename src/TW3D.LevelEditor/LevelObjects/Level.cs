using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using System.Windows.Forms;
using TW3D.Graphics;
using TW3D.Shared;

namespace LevelEditor.LevelObjects
{
    class TrackBlock
    {
        public Vector2 blockPosition { get; set; }
        public Vector2 blockAbsPosition { get { return new Vector2(blockPosition.X, blockPosition.Y + blockShift); } }
        public float blockShift { get; set; }
        public Model blockModel { get; set; }
        public string modelName { get; set; }
    }

    class EditedObject
    {
        public Vector2 objPosition { get; set; }
        public Model objModel { get; set; }
        public string objName { get; set; }
        public string objModelName { get; set; }
        public bool isSelected { get; set; }
    }

    static class TrackBlockModels
    {
        public static Dictionary<string, Model> Models = new Dictionary<string, Model>();
    }

    static class TrackPropModels
    {
        public static Dictionary<string, Model> Models = new Dictionary<string, Model>();
    }

    class Level : GameObject
    {
        //Positions in the editor are in 3D units: one grid cell of a level, one step of the vertical shift,
        //and the ratio of physics units (used by the level file for dynamic objects) to 3D units
        private float GridSize => CTW3D(9f);
        private float ShiftStep => CTW3D(1f);
        private const float PhysicsTo3D = 10f;

        public List<TrackBlock> trackBlocks { get; set; }
        public List<TrackBlock> backTrackBlocks { get; set; }
        public List<TrackBlock> frontTrackBlocks { get; set; }

        public List<EditedObject> gObjects { get; set; }

        /// <summary>The blocks and dynamic objects the level can use.</summary>
        public Catalog Catalog { get; }

        //Position of the level in the game's level select, kept from the loaded file
        private int? order;

        /// <summary>The light of the level.</summary>
        public LevelMood Mood { get; set; }

        /// <param name="fileName">Level file to open; null = ask for it.</param>
        public Level(GraphicsDeviceManager ScreenManager, Camera3D Camera, Catalog catalog, Vector2 Position, float Scale, string fileName = null)
        {
            screenManager = ScreenManager;
            camera = Camera;
            Catalog = catalog;
            position = Position;
            scale = Scale;

            trackBlocks = new List<TrackBlock>();
            backTrackBlocks = new List<TrackBlock>();
            frontTrackBlocks = new List<TrackBlock>();

            gObjects = new List<EditedObject>();

            if (fileName != null)
                Load(fileName);
            else
                ReadFile();
        }

        /// <summary>Asks for a level file and opens it.</summary>
        public void ReadFile()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            openFileDialog.InitialDirectory = "C:\\levels";
            openFileDialog.Filter = "Levels (*.json, *.lev)|*.json;*.lev|All files (*.*)|*.*";
            openFileDialog.RestoreDirectory = true;

            if (openFileDialog.ShowDialog() != DialogResult.OK)
                return;

            Load(openFileDialog.FileName);
        }

        private void Load(string fileName)
        {
            LevelData data;
            try
            {
                data = LevelFile.Load(fileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: Could not read the level file. Original error: " + ex.Message);
                return;
            }

            //The lists are cleared and refilled, because the selector keeps references to them
            trackBlocks.Clear();
            backTrackBlocks.Clear();
            frontTrackBlocks.Clear();
            gObjects.Clear();
            order = data.Order;
            Mood = data.Mood;

            foreach (string problem in data.Validate(Catalog))
                Console.WriteLine("Warning: " + problem + " Skipped.");

            AddBlocks(data.Middle, trackBlocks, true);
            AddBlocks(data.Back, backTrackBlocks, false);
            AddBlocks(data.Front, frontTrackBlocks, false);

            foreach (ObjectPlacement placement in data.Objects)
            {
                ObjectType type = Catalog.FindObject(placement.Type);
                if (type == null || !type.Models.Contains(placement.Model))
                    continue;

                EditedObject gObj = new EditedObject();
                gObj.objName = placement.Type;
                gObj.objModelName = placement.Model;
                gObj.objPosition = new Vector2(placement.X, placement.Y) / PhysicsTo3D;
                gObj.objModel = TrackPropModels.Models[placement.Model];
                gObjects.Add(gObj);
            }

            Console.WriteLine("File " + fileName + " loaded.\n");
            if (Path.GetExtension(fileName).Equals(LevelFile.LegacyExtension, StringComparison.OrdinalIgnoreCase))
                Console.WriteLine("The level was converted from the old .lev format; it will be saved as " + LevelFile.Extension + ".\n");
        }

        /// <param name="physicsLayer">The middle layer, where only blocks with a collision bitmap may be.</param>
        private void AddBlocks(List<BlockPlacement> placements, List<TrackBlock> layer, bool physicsLayer)
        {
            foreach (BlockPlacement placement in placements)
            {
                BlockType block = Catalog.FindBlock(placement.Block);
                if (block == null || (physicsLayer && !block.Collision))
                    continue;

                TrackBlock trackBlock = new TrackBlock();
                trackBlock.blockPosition = new Vector2(placement.X, placement.Y) * GridSize;
                trackBlock.blockShift = placement.Shift * ShiftStep;
                trackBlock.blockModel = TrackBlockModels.Models[placement.Block];
                trackBlock.modelName = placement.Block;
                layer.Add(trackBlock);
            }
        }

        public void WriteFile()
        {
            LevelData data = new LevelData { Order = order, Mood = Mood };
            data.Middle.AddRange(ToPlacements(trackBlocks));
            data.Back.AddRange(ToPlacements(backTrackBlocks));
            data.Front.AddRange(ToPlacements(frontTrackBlocks));
            foreach (EditedObject ob in gObjects)
            {
                //Rounded, so that the conversion from the editor's 3D units does not produce values like 94.00001
                data.Objects.Add(new ObjectPlacement
                {
                    Type = ob.objName,
                    Model = ob.objModelName,
                    X = (float)Math.Round(ob.objPosition.X * PhysicsTo3D, 2),
                    Y = (float)Math.Round(ob.objPosition.Y * PhysicsTo3D, 2)
                });
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog();

            saveFileDialog.InitialDirectory = "C:\\levels";
            saveFileDialog.Filter = "Levels (*.json)|*.json";
            saveFileDialog.DefaultExt = LevelFile.Extension;
            saveFileDialog.RestoreDirectory = true;

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                LevelFile.Save(data, saveFileDialog.FileName);
                Console.WriteLine("File " + saveFileDialog.FileName + " saved.");
            }
        }

        private IEnumerable<BlockPlacement> ToPlacements(List<TrackBlock> layer)
        {
            foreach (TrackBlock tb in layer)
            {
                yield return new BlockPlacement
                {
                    Block = tb.modelName,
                    X = (int)Math.Round(tb.blockPosition.X / GridSize),
                    Y = (int)Math.Round(tb.blockPosition.Y / GridSize),
                    Shift = (int)Math.Round(tb.blockShift / ShiftStep)
                };
            }
        }

        public override void Draw(SceneRenderer renderer)
        {
            foreach (TrackBlock tb in trackBlocks)
                renderer.Add(tb.blockModel, ModelWorld(new Vector3(tb.blockAbsPosition.X, tb.blockAbsPosition.Y, 0f)));
            foreach (TrackBlock tb in backTrackBlocks)
                renderer.Add(tb.blockModel, ModelWorld(new Vector3(tb.blockAbsPosition.X, tb.blockAbsPosition.Y, CTW3D(-9f))));
            foreach (TrackBlock tb in frontTrackBlocks)
                renderer.Add(tb.blockModel, ModelWorld(new Vector3(tb.blockAbsPosition.X, tb.blockAbsPosition.Y, CTW3D(9f))));
            foreach (EditedObject obj in gObjects)
                renderer.Add(obj.objModel, ModelWorld(new Vector3(obj.objPosition.X, obj.objPosition.Y, 0f)));
        }
    }
}
