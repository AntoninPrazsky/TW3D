using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using TW3D.Graphics;
using TW3D.Shared;

namespace LevelEditor.LevelObjects
{
    /// <summary>A track block of the level being edited.</summary>
    class TrackBlock
    {
        /// <summary>Name of the block in the catalog.</summary>
        public string Name { get; set; }

        public Model Model { get; set; }

        /// <summary>Position on the level grid in the units of the editor, without the vertical shift.</summary>
        public Vector2 Position { get; set; }

        /// <summary>Vertical shift in the units of the editor (steps of <see cref="GameObject.ShiftStep"/>).</summary>
        public float Shift { get; set; }

        /// <summary>The finish of the level (a block of the middle layer).</summary>
        public bool Finish { get; set; }

        /// <summary>Where the block is drawn: its position with the shift.</summary>
        public Vector2 DrawPosition => new Vector2(Position.X, Position.Y + Shift);
    }

    /// <summary>A dynamic object of the level being edited.</summary>
    class EditedObject
    {
        /// <summary>Name of the object type in the catalog.</summary>
        public string Type { get; set; }

        /// <summary>One of the models of the object type.</summary>
        public string ModelName { get; set; }

        public Model Model { get; set; }

        /// <summary>Position in the units of the editor.</summary>
        public Vector2 Position { get; set; }

        public bool IsSelected { get; set; }
    }

    /// <summary>
    /// The level being edited: the blocks of its three layers and its dynamic objects, read from and written to a
    /// level file (see <see cref="LevelFile"/>). The selector changes the lists in place.
    /// </summary>
    class Level : GameObject
    {
        //The default folder of the file dialogs
        private const string LevelsFolder = "C:\\levels";

        private readonly Dictionary<string, Model> blockModels = new Dictionary<string, Model>();
        private readonly Dictionary<string, Model> objectModels = new Dictionary<string, Model>();
        //Position of the level in the game's level select and its par time, kept from the loaded file
        private int? order;
        private float? par;

        public List<TrackBlock> Middle { get; } = new List<TrackBlock>();
        public List<TrackBlock> Back { get; } = new List<TrackBlock>();
        public List<TrackBlock> Front { get; } = new List<TrackBlock>();
        public List<EditedObject> Objects { get; } = new List<EditedObject>();

        /// <summary>The blocks and dynamic objects the level can use.</summary>
        public Catalog Catalog { get; }

        /// <summary>The light of the level.</summary>
        public LevelMood Mood { get; set; }

        /// <param name="fileName">Level file to open; null = ask for it.</param>
        public Level(Catalog catalog, ContentManager content, string fileName = null)
        {
            Catalog = catalog;
            foreach (BlockType block in catalog.Blocks)
                blockModels.Add(block.Name, content.Load<Model>(block.ModelAsset));
            foreach (string model in catalog.ObjectModels)
                objectModels.Add(model, content.Load<Model>(ObjectType.ModelAsset(model)));

            if (fileName != null)
                Load(fileName);
            else
                ReadFile();
        }

        /// <summary>The model of a block of the catalog.</summary>
        public Model BlockModel(string name) => blockModels[name];

        /// <summary>The model of a dynamic object (one of the models of its type).</summary>
        public Model ObjectModel(string name) => objectModels[name];

        /// <summary>Asks for a level file and opens it.</summary>
        public void ReadFile()
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                InitialDirectory = LevelsFolder,
                Filter = "Levels (*.json, *.lev)|*.json;*.lev|All files (*.*)|*.*",
                RestoreDirectory = true
            };
            if (dialog.ShowDialog() == DialogResult.OK)
                Load(dialog.FileName);
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
            Middle.Clear();
            Back.Clear();
            Front.Clear();
            Objects.Clear();
            order = data.Order;
            par = data.Par;
            Mood = data.Mood;

            foreach (string problem in data.Validate(Catalog))
                Console.WriteLine("Warning: " + problem + " Skipped.");

            AddBlocks(data.Middle, Middle, true);
            AddBlocks(data.Back, Back, false);
            AddBlocks(data.Front, Front, false);

            foreach (ObjectPlacement placement in data.Objects)
            {
                ObjectType type = Catalog.FindObject(placement.Type);
                if (type == null || !type.Models.Contains(placement.Model))
                    continue;

                Objects.Add(new EditedObject
                {
                    Type = placement.Type,
                    ModelName = placement.Model,
                    Model = ObjectModel(placement.Model),
                    Position = new Vector2(placement.X, placement.Y) / PhysicsPerUnit
                });
            }

            Console.WriteLine("File " + fileName + " loaded.");
            BlockPlacement finish = data.FinishBlock;
            Console.WriteLine(finish != null
                ? "Finish: block " + finish.Block + " at [" + finish.X + ", " + finish.Y + "].\n"
                : "The level has no finish (F marks the block of the middle layer under the selector).\n");
            if (Path.GetExtension(fileName).Equals(LevelFile.LegacyExtension, StringComparison.OrdinalIgnoreCase))
                Console.WriteLine("The level was converted from the old .lev format; it will be saved as " + LevelFile.Extension + ".\n");
        }

        /// <param name="physicsLayer">The middle layer, where only blocks with a collision shape may be.</param>
        private void AddBlocks(List<BlockPlacement> placements, List<TrackBlock> layer, bool physicsLayer)
        {
            foreach (BlockPlacement placement in placements)
            {
                BlockType block = Catalog.FindBlock(placement.Block);
                if (block == null || (physicsLayer && !block.Collision))
                    continue;

                layer.Add(new TrackBlock
                {
                    Name = placement.Block,
                    Model = BlockModel(placement.Block),
                    Position = new Vector2(placement.X, placement.Y) * GridCell,
                    Shift = placement.Shift * ShiftStep,
                    Finish = physicsLayer && placement.Finish
                });
            }
        }

        /// <summary>Asks for a file and writes the level into it.</summary>
        public void WriteFile()
        {
            LevelData data = new LevelData { Order = order, Par = par, Mood = Mood };
            data.Middle.AddRange(ToPlacements(Middle));
            data.Back.AddRange(ToPlacements(Back));
            data.Front.AddRange(ToPlacements(Front));
            foreach (EditedObject obj in Objects)
            {
                //Rounded, so that the conversion from the editor's units does not produce values like 94.00001
                data.Objects.Add(new ObjectPlacement
                {
                    Type = obj.Type,
                    Model = obj.ModelName,
                    X = (float)Math.Round(obj.Position.X * PhysicsPerUnit, 2),
                    Y = (float)Math.Round(obj.Position.Y * PhysicsPerUnit, 2)
                });
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                InitialDirectory = LevelsFolder,
                Filter = "Levels (*.json)|*.json",
                DefaultExt = LevelFile.Extension,
                RestoreDirectory = true
            };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                LevelFile.Save(data, dialog.FileName);
                Console.WriteLine("File " + dialog.FileName + " saved.");
            }
        }

        private static IEnumerable<BlockPlacement> ToPlacements(List<TrackBlock> layer)
        {
            foreach (TrackBlock block in layer)
            {
                yield return new BlockPlacement
                {
                    Block = block.Name,
                    X = (int)Math.Round(block.Position.X / GridCell),
                    Y = (int)Math.Round(block.Position.Y / GridCell),
                    Shift = (int)Math.Round(block.Shift / ShiftStep),
                    Finish = block.Finish
                };
            }
        }

        public override void Draw(SceneRenderer renderer)
        {
            DrawLayer(renderer, Middle, 0f);
            DrawLayer(renderer, Back, -GridCell);
            DrawLayer(renderer, Front, GridCell);
            foreach (EditedObject obj in Objects)
                renderer.Add(obj.Model, ModelWorld(new Vector3(obj.Position, 0f)));
        }

        private static void DrawLayer(SceneRenderer renderer, List<TrackBlock> layer, float depth)
        {
            foreach (TrackBlock block in layer)
                renderer.Add(block.Model, ModelWorld(new Vector3(block.DrawPosition, depth)));
        }
    }
}
