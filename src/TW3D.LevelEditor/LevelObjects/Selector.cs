using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TW3D.Shared;

namespace LevelEditor.LevelObjects
{
    /// <summary>
    /// The selector: a frame on the level grid, moved by the arrow keys, where blocks are placed (Space), changed
    /// and deleted, in the layer being edited (Right Shift: the back layer, Right Ctrl: the front layer). With Caps Lock
    /// on, the keys work with the dynamic objects instead: Space places one at the selector, the mouse selects (left)
    /// or deletes (right) one, the arrows move the selected ones. The console of the editor shows what happens and
    /// asks for names (Insert). The keys are described in the documentation of the editor (docs/bachelor-thesis).
    /// </summary>
    class Selector : GameObject
    {
        //The layer being edited, and the console colour showing it
        private enum Layer
        {
            Middle,
            Back,
            Front
        }

        //How far the arrows move a selected dynamic object (units of the editor), and the decimal places of its position
        private const float ObjectStep = 0.1f;
        private const int ObjectPositionDecimals = 1;

        private readonly Level level;
        private readonly Model model;
        private KeyboardState keys, previousKeys;
        private bool mouseWasDown;

        private Layer layer = Layer.Middle;
        //The block placed by Space and the shift it gets; the dynamic object placed by Space with Caps Lock on
        private string block = "a1";
        private float shift;
        private string objectType = "Bar", objectModel = "bar1";

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        /// <summary>Presses Caps Lock: the names typed into the console with it on would be in capitals.</summary>
        private static void ToggleCapsLock()
        {
            const byte capsLock = 0x14, scanCode = 0x45;
            const uint extendedKey = 0x1, keyUp = 0x2;
            keybd_event(capsLock, scanCode, extendedKey, UIntPtr.Zero);
            keybd_event(capsLock, scanCode, extendedKey | keyUp, UIntPtr.Zero);
        }

        public Selector(GraphicsDeviceManager graphics, Camera3D camera, ContentManager content, Level level)
        {
            this.graphics = graphics;
            this.camera = camera;
            this.level = level;
            model = content.Load<Model>("Levels/selector");
            Console.Title = "Level Editor Console";
            Console.WriteLine("Level Editor initialized\n\n");
        }

        /// <summary>The blocks of the layer being edited.</summary>
        private List<TrackBlock> Blocks => layer == Layer.Back ? level.Back : layer == Layer.Front ? level.Front : level.Middle;

        /// <summary>The block of the layer being edited under the selector; null if there is none.</summary>
        private TrackBlock BlockHere => Blocks.Find(candidate => candidate.Position == position);

        private bool Pressed(Keys key) => keys.IsKeyDown(key) && previousKeys.IsKeyUp(key);

        private static Vector2 Round(Vector2 objectPosition)
        {
            return new Vector2((float)Math.Round(objectPosition.X, ObjectPositionDecimals), (float)Math.Round(objectPosition.Y, ObjectPositionDecimals));
        }

        public override void Update()
        {
            previousKeys = keys;
            keys = Keyboard.GetState();
            //With Caps Lock on, the keys work with the dynamic objects instead of the blocks
            bool objects = Console.CapsLock;

            Move(objects);

            if (Pressed(Keys.Delete))
                DeleteBlock();
            if (Pressed(Keys.F1))
                ListBlocks();
            if (Pressed(Keys.F11))
                ChooseBlock(-1);
            if (Pressed(Keys.F12))
                ChooseBlock(1);
            if (Pressed(Keys.PageUp))
                ShiftBlock(-1);
            if (Pressed(Keys.PageDown))
                ShiftBlock(1);
            if (Pressed(Keys.Insert))
            {
                if (objects)
                    AskForObject();
                else
                    AskForBlock();
            }
            if (Pressed(Keys.Space))
            {
                if (objects)
                    PlaceObject();
                else
                    PlaceBlock();
            }
            if (Pressed(Keys.P))
                Describe();
            if (Pressed(Keys.F))
                ToggleFinish();
            if (Pressed(Keys.O))
                level.ReadFile();
            if (Pressed(Keys.End))
                level.WriteFile();
            if (Pressed(Keys.RightShift))
                Edit(layer == Layer.Back ? Layer.Middle : Layer.Back);
            if (Pressed(Keys.RightControl))
                Edit(layer == Layer.Front ? Layer.Middle : Layer.Front);

            if (objects)
                PickObject();
        }

        /// <summary>The arrows move the selector by cells, with Caps Lock on the selected dynamic objects by a tenth.</summary>
        private void Move(bool objects)
        {
            Vector2 step = Vector2.Zero;
            if (Pressed(Keys.Right)) step.X += 1f;
            if (Pressed(Keys.Left)) step.X -= 1f;
            if (Pressed(Keys.Up)) step.Y -= 1f;
            if (Pressed(Keys.Down)) step.Y += 1f;
            if (step == Vector2.Zero)
                return;

            if (objects)
            {
                foreach (EditedObject obj in level.Objects)
                    if (obj.IsSelected)
                        obj.Position = Round(obj.Position + step * ObjectStep);
                return;
            }

            position += step * GridCell;
            //The camera follows the selector, unless Num Lock is on
            if (!Console.NumberLock)
            {
                Vector3 camera = this.camera.Position;
                if (step.X != 0f)
                    camera.X = -position.X;
                if (step.Y != 0f)
                    camera.Y = -position.Y + 5f;
                this.camera.Position = camera;
            }
        }

        private void DeleteBlock()
        {
            if (Blocks.Remove(BlockHere))
                Console.WriteLine("Block at " + position + " was deleted.");
        }

        private void ListBlocks()
        {
            Console.WriteLine("\n\nAll blocks in the catalog:\n");
            foreach (BlockType type in level.Catalog.Blocks)
                Console.WriteLine(type.Collision ? type.Name : type.Name + " (decoration, back or front layer only)");
            Console.WriteLine("\n");
        }

        /// <summary>The next (1) or the previous (-1) block of the catalog becomes the block to place.</summary>
        private void ChooseBlock(int direction)
        {
            List<BlockType> blocks = level.Catalog.Blocks;
            int index = blocks.FindIndex(type => type.Name == block) + direction;
            if (index < 0 || index >= blocks.Count)
            {
                Console.WriteLine("Current block " + block + " is the " + (direction < 0 ? "first" : "last") + " block in the catalog.");
                return;
            }
            Console.WriteLine("Default block was changed from " + block + " to " + blocks[index].Name);
            block = blocks[index].Name;
        }

        /// <summary>Shifts the block under the selector down (1) or up (-1) by a step; new blocks get the same shift.</summary>
        private void ShiftBlock(int direction)
        {
            TrackBlock here = BlockHere;
            if (here == null)
                return;
            here.Shift += direction * ShiftStep;
            shift = here.Shift;
            Console.WriteLine("Shift was " + (direction < 0 ? "decreased" : "increased") + " to: " + shift);
        }

        private void AskForBlock()
        {
            Console.Write("Type block name: ");
            string input = Console.ReadLine();
            if (level.Catalog.FindBlock(input) != null)
            {
                block = input;
                Console.WriteLine("Default block was set to: " + block);
            }
            else
                Console.WriteLine("This block name does not exist: " + input + "\nDefault block is: " + block);
        }

        private void AskForObject()
        {
            //Typed with Caps Lock off, which is switched on again afterwards
            ToggleCapsLock();
            Console.Write("Type object type: ");
            string inputType = Console.ReadLine();
            Console.Write("Type object model name: ");
            string inputModel = Console.ReadLine();
            ObjectType type = level.Catalog.FindObject(inputType);
            if (type != null && type.Models.Contains(inputModel))
            {
                objectType = inputType;
                objectModel = inputModel;
                Console.WriteLine("Object type set to: " + objectType + ", object model set to: " + objectModel);
            }
            else
            {
                Console.WriteLine("Object type " + inputType + " with model " + inputModel + " does not exist. Available types and models:");
                foreach (ObjectType available in level.Catalog.Objects)
                    Console.WriteLine("  " + available.Type + ": " + string.Join(", ", available.Models));
                Console.WriteLine("Object type is: " + objectType + "; Object model is: " + objectModel);
            }
            ToggleCapsLock();
        }

        /// <summary>Places the block to place under the selector, or changes the block there into it.</summary>
        private void PlaceBlock()
        {
            if (layer == Layer.Middle && !level.Catalog.FindBlock(block).Collision)
            {
                //The game needs a collision shape for every block of the middle (physics) layer
                Console.WriteLine("Block " + block + " is decoration without a collision shape; it can only be placed in the back or front layer.");
                return;
            }

            TrackBlock here = BlockHere;
            if (here != null)
            {
                here.Model = level.BlockModel(block);
                here.Name = block;
                Console.WriteLine("Block model at " + position + " was changed to model: " + block);
                return;
            }

            Blocks.Add(new TrackBlock { Name = block, Model = level.BlockModel(block), Position = position, Shift = shift });
            Console.WriteLine("New block was placed at " + position + "; Model: " + block + "; Shift: " + shift);
        }

        /// <summary>Places the dynamic object to place at the selector, unless another one is there.</summary>
        private void PlaceObject()
        {
            Vector2 objectPosition = Round(position);
            EditedObject there = level.Objects.Find(obj => obj.Position == objectPosition);
            if (there != null)
            {
                Console.WriteLine("Cannot place new object on this position: " + objectPosition
                    + "\nThis position is already occupied by another object (Type: " + there.Type + "; Model: " + there.ModelName + ").");
                return;
            }

            level.Objects.Add(new EditedObject { Type = objectType, ModelName = objectModel, Model = level.ObjectModel(objectModel), Position = objectPosition });
            Console.WriteLine("New object was placed at " + objectPosition + "; Type: " + objectType + "; Model: " + objectModel);
        }

        /// <summary>Tells what is under the selector.</summary>
        private void Describe()
        {
            Console.WriteLine(position);
            TrackBlock here = BlockHere;
            Console.WriteLine(here != null
                ? "There is a block on this position. Model: " + here.Name + "; Shift: " + here.Shift + (here.Finish ? "; the finish" : "")
                : "This position is empty.");
        }

        /// <summary>
        /// The block of the middle layer under the selector becomes the finish of the level (the only one),
        /// or stops being it.
        /// </summary>
        private void ToggleFinish()
        {
            TrackBlock here = layer == Layer.Middle ? BlockHere : null;
            if (here == null)
                Console.WriteLine("The finish can only be a block of the middle layer.");
            else if (here.Finish)
            {
                here.Finish = false;
                Console.WriteLine("Block " + here.Name + " at " + position + " is no longer the finish; the level has none.");
            }
            else
            {
                foreach (TrackBlock other in level.Middle)
                    other.Finish = false;
                here.Finish = true;
                Console.WriteLine("Block " + here.Name + " at " + position + " is the finish of the level.");
            }
        }

        /// <summary>Switches the layer being edited; the console shows the back layer green, the front layer blue.</summary>
        private void Edit(Layer newLayer)
        {
            layer = newLayer;
            switch (layer)
            {
                case Layer.Back:
                    Console.BackgroundColor = ConsoleColor.DarkGreen;
                    Console.WriteLine("Back side edit turned on.");
                    break;
                case Layer.Front:
                    Console.BackgroundColor = ConsoleColor.Blue;
                    Console.WriteLine("Front side edit turned on.");
                    break;
                default:
                    Console.BackgroundColor = ConsoleColor.Black;
                    Console.WriteLine("Back and front side edit turned off.");
                    break;
            }
        }

        /// <summary>The mouse selects (the left button) or deletes (the right button) the dynamic object under it.</summary>
        private void PickObject()
        {
            MouseState mouse = Mouse.GetState();
            bool left = mouse.LeftButton == ButtonState.Pressed;
            bool down = left || mouse.RightButton == ButtonState.Pressed;
            bool pressed = down && !mouseWasDown;
            mouseWasDown = down;
            if (!pressed)
                return;

            Ray ray = camera.PickRay(CreateProjection(graphics.GraphicsDevice.Viewport, camera.IsOrthographic), graphics.GraphicsDevice.Viewport);
            EditedObject hit = level.Objects.Find(obj => RayHits(ray, new Vector3(obj.Position, 0f)));
            if (left)
            {
                foreach (EditedObject obj in level.Objects)
                    obj.IsSelected = obj == hit;
                if (hit != null)
                    Console.WriteLine("Game object selected.\n Type: " + hit.Type + "\n Model: " + hit.ModelName + "\n Position: " + hit.Position);
            }
            else if (hit != null)
            {
                level.Objects.Remove(hit);
                Console.WriteLine("Game object deleted.\n Type: " + hit.Type + "\n Model: " + hit.ModelName + "\n Position: " + hit.Position);
            }
        }

        public override void Draw()
        {
            float depth = layer == Layer.Back ? -GridCell : layer == Layer.Front ? GridCell : 0f;
            DrawModel(model, new Vector3(position, depth));
        }
    }
}
