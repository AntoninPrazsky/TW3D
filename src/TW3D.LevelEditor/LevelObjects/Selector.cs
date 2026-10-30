using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Input;
using System.Runtime.InteropServices;
using TW3D.Shared;

namespace LevelEditor.LevelObjects
{
    class Selector : GameObject
    {
        public Model selectorModel;

        private bool riPr, lePr, upPr, doPr, pgUp, pgDo, inPr, spPr, pPr, shPr, ctPr, f1Pr, f11Pr, f12Pr, mlePr, finPr;
        private List<TrackBlock> track;
        private List<TrackBlock> bSide;
        private List<TrackBlock> fSide;
        private List<EditedObject> gObjects;
        private bool bEdit, fEdit;
        private string defaultBlock = "a1";
        private float defaultShift = 0f;
        private Level level;
        private float posZ = 0f;
        private List<TrackBlock> where;

        private string objType = "Bar", objModel = "bar1";

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private void CapsLock()
        {
            const int KEYEVENTF_EXTENDEDKEY = 0x1;
            const int KEYEVENTF_KEYUP = 0x2;
            keybd_event(0x14, 0x45, KEYEVENTF_EXTENDEDKEY, (UIntPtr)0);
            keybd_event(0x14, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP,
            (UIntPtr)0);
        }

        private Vector2 RoundPos(Vector2 pos)
        {
            decimal x = (decimal)pos.X;
            decimal y = (decimal)pos.Y;
            x = Math.Round(x, 1);
            y = Math.Round(y, 1);
            return new Vector2((float)x, (float)y);
        }

        public Selector(GraphicsDeviceManager ScreenManager, Camera3D Camera, Vector2 Position, float Scale, ContentManager ContentManager, Level Level)
        {
            screenManager = ScreenManager;
            camera = Camera;
            position = Position;
            scale = Scale;
            contentManager = ContentManager;
            level = Level;
            track = level.trackBlocks;
            bSide = level.backTrackBlocks;
            fSide = level.frontTrackBlocks;
            gObjects = level.gObjects;
            selectorModel = contentManager.Load<Model>("Levels/selector");
            where = track;
            Console.Title = "Level Editor Console";
            Console.WriteLine("Level Editor initialized\n\n");
        }

        public override void Update()
        {
            KeyboardState keyState = Keyboard.GetState();
            //Right
            if (keyState.IsKeyDown(Keys.Right) && !riPr)
            {
                if (Console.CapsLock)
                {
                    foreach (EditedObject ob in gObjects)
                    {
                        if (ob.isSelected) ob.objPosition = RoundPos(new Vector2(ob.objPosition.X + 1 / 10f, ob.objPosition.Y));
                    }
                }
                else
                {
                    position = new Vector2(position.X + CTW3D(9f), position.Y);
                }
                riPr = true;
            }
            if (keyState.IsKeyUp(Keys.Right)) riPr = false;

            //Left
            if (keyState.IsKeyDown(Keys.Left) && !lePr)
            {
                if (Console.CapsLock)
                {
                    foreach (EditedObject ob in gObjects)
                    {
                        if (ob.isSelected) ob.objPosition = RoundPos(new Vector2(ob.objPosition.X - 1 / 10f, ob.objPosition.Y));
                    }
                }
                else
                {
                    position = new Vector2(position.X - CTW3D(9f), position.Y);
                }
                lePr = true;
            }
            if (keyState.IsKeyUp(Keys.Left)) lePr = false;

            //Up
            if (keyState.IsKeyDown(Keys.Up) && !upPr)
            {
                if (Console.CapsLock)
                {
                    foreach (EditedObject ob in gObjects)
                    {
                        if (ob.isSelected) ob.objPosition = RoundPos(new Vector2(ob.objPosition.X, ob.objPosition.Y - 1 / 10f));
                    }
                }
                else
                {
                    position = new Vector2(position.X, position.Y - CTW3D(9f));
                }
                upPr = true;
            }
            if (keyState.IsKeyUp(Keys.Up)) upPr = false;

            //Down
            if (keyState.IsKeyDown(Keys.Down) && !doPr)
            {
                if (Console.CapsLock)
                {
                    foreach (EditedObject ob in gObjects)
                    {
                        if (ob.isSelected) ob.objPosition = RoundPos(new Vector2(ob.objPosition.X, ob.objPosition.Y + 1 / 10f));
                    }
                }
                else
                {
                    position = new Vector2(position.X, position.Y + CTW3D(9f));
                }
                doPr = true;
            }
            if (keyState.IsKeyUp(Keys.Down)) doPr = false;

            if (!Console.NumberLock)
            {
                if (riPr || lePr) camera.cameraPosition = new Vector3(-position.X, camera.cameraPosition.Y, camera.cameraPosition.Z);
                if (upPr || doPr) camera.cameraPosition = new Vector3(camera.cameraPosition.X, -position.Y + 5f, camera.cameraPosition.Z);
            }

            //Delete
            if (keyState.IsKeyDown(Keys.Delete))
            {
                for (int i = 0; i < where.Count; i++) if (where[i].blockPosition == position)
                    {
                        where.RemoveAt(i);
                        Console.WriteLine("Block at " + position + " was deleted.");
                        break;
                    }
            }

            //f1
            if (keyState.IsKeyDown(Keys.F1) && !f1Pr)
            {
                Console.WriteLine("\n\nAll blocks in the catalog:\n");
                foreach (BlockType block in level.Catalog.Blocks)
                {
                    Console.WriteLine(block.Collision ? block.Name : block.Name + " (decoration, back or front layer only)");
                }
                Console.WriteLine("\n");
                f1Pr = true;
            }
            if (keyState.IsKeyUp(Keys.F1)) f1Pr = false;

            //f11
            if (keyState.IsKeyDown(Keys.F11) && !f11Pr)
            {
                string prev = "a1";
                if (defaultBlock == prev) Console.WriteLine("Current block " + defaultBlock + " is the first block in Dictionary.");
                else
                {
                    foreach (KeyValuePair<string, Model> entry in TrackBlockModels.Models)
                    {
                        if (defaultBlock == entry.Key)
                        {
                            Console.WriteLine("Default block was changed from " + defaultBlock + " to " + prev);
                            defaultBlock = prev;
                            break;
                        }
                        prev = entry.Key;
                    }
                }
                f11Pr = true;
            }
            if (keyState.IsKeyUp(Keys.F11)) f11Pr = false;

            //f12
            if (keyState.IsKeyDown(Keys.F12) && !f12Pr)
            {
                bool found = false;
                foreach (KeyValuePair<string, Model> entry in TrackBlockModels.Models)
                {
                    if (found)
                    {
                        Console.WriteLine("Default block was changed from " + defaultBlock + " to " + entry.Key);
                        defaultBlock = entry.Key;
                        found = false;
                        break;
                    }
                    if (defaultBlock == entry.Key)
                    {
                        found = true;
                    }
                }
                if (found) Console.WriteLine("Current block " + defaultBlock + " is the last block in dictionary.");
                f12Pr = true;
            }
            if (keyState.IsKeyUp(Keys.F12)) f12Pr = false;


            //PgUp PgDo
            if (keyState.IsKeyDown(Keys.PageUp) && !pgUp)
            {
                for (int i = 0; i < where.Count; i++) if (where[i].blockPosition == position)
                    {
                        where[i].blockShift -= CTW3D(1f);
                        defaultShift = where[i].blockShift;
                        Console.WriteLine("Shift was decreased to: " + defaultShift);
                        break;
                    }
                pgUp = true;
            }
            if (keyState.IsKeyUp(Keys.PageUp)) pgUp = false;

            if (keyState.IsKeyDown(Keys.PageDown) && !pgDo)
            {
                for (int i = 0; i < where.Count; i++) if (where[i].blockPosition == position)
                    {
                        where[i].blockShift += CTW3D(1f);
                        defaultShift = where[i].blockShift;
                        Console.WriteLine("Shift was increased to: " + defaultShift);
                        break;
                    }
                pgDo = true;
            }
            if (keyState.IsKeyUp(Keys.PageDown)) pgDo = false;

            // + - Change block
            if (keyState.IsKeyDown(Keys.Insert) && !inPr)
            {
                if (Console.CapsLock)
                {
                    CapsLock();
                    Console.Write("Type object type: ");
                    string inputType = Console.ReadLine();
                    Console.Write("Type object model name: ");
                    string inputModel = Console.ReadLine();
                    ObjectType type = level.Catalog.FindObject(inputType);
                    if (type != null && type.Models.Contains(inputModel))
                    {
                        objModel = inputModel;
                        objType = inputType;
                        Console.WriteLine("Object type set to: " + objType + ", object model set to: " + objModel);
                    }
                    else
                    {
                        Console.WriteLine("Object type " + inputType + " with model " + inputModel + " does not exist. Available types and models:");
                        foreach (ObjectType available in level.Catalog.Objects)
                            Console.WriteLine("  " + available.Type + ": " + string.Join(", ", available.Models));
                        Console.WriteLine("Object type is: " + objType + "; Object model is: " + objModel);
                    }
                    CapsLock();
                }
                else {
                    Console.Write("Type block name: ");
                    string input = Console.ReadLine();
                    if (TrackBlockModels.Models.ContainsKey(input))
                    {
                        defaultBlock = input;
                        Console.WriteLine("Default block was set to: " + defaultBlock);
                    }
                    else
                    {
                        Console.WriteLine("This block name does not exist: " + input + "\nDefault block is: " + defaultBlock);
                    }
                }
                inPr = true;
            }
            if (keyState.IsKeyUp(Keys.Add)) inPr = false;

            // Place Block
            if (keyState.IsKeyDown(Keys.Space) && !spPr)
            {
                if (Console.CapsLock)
                {
                    for (int i = 0; i < gObjects.Count; i++)
                    {
                        if (gObjects[i].objPosition == RoundPos(position))
                        {
                            Console.WriteLine("Cannot place new object on this position: " + RoundPos(position) + "\nThis position is already occupied by another object (Type: " + gObjects[i].objName + "; Model: " + gObjects[i].objModelName + ").");
                            spPr = true;
                            break;
                        }
                    }
                    if (!spPr)
                    {
                        EditedObject obj = new EditedObject();
                        obj.objModel = TrackPropModels.Models[objModel];
                        obj.objName = objType;
                        obj.objModelName = objModel;
                        obj.objPosition = RoundPos(position);
                        gObjects.Add(obj);
                        //obj.isSelected = true;
                        Console.WriteLine("New object was placet at " + obj.objPosition + "; Type: " + obj.objName + "; Model: " + obj.objModelName);
                    }
                }
                else if (where == track && !level.Catalog.FindBlock(defaultBlock).Collision)
                {
                    //The game needs a collision bitmap for every block of the middle (physics) layer
                    Console.WriteLine("Block " + defaultBlock + " is decoration without a collision bitmap; it can only be placed in the back or front layer.");
                }
                else
                {
                    for (int i = 0; i < where.Count; i++)
                    {
                        if (where[i].blockPosition == position)
                        {
                            where[i].blockModel = TrackBlockModels.Models[defaultBlock];
                            where[i].modelName = defaultBlock;
                            Console.WriteLine("Block model at " + position + " was changed to model: " + defaultBlock);
                            spPr = true;
                            break;
                        }
                    }
                    if (!spPr)
                    {
                        TrackBlock newTrackBlock = new TrackBlock();
                        newTrackBlock.blockModel = TrackBlockModels.Models[defaultBlock];
                        newTrackBlock.modelName = defaultBlock;
                        newTrackBlock.blockPosition = position;
                        newTrackBlock.blockShift = defaultShift;
                        where.Add(newTrackBlock);
                        Console.WriteLine("New block was placed at " + position + "; Model: " + defaultBlock + "; Shift: " + defaultShift);
                    }
                }
                spPr = true;
            }
            if (keyState.IsKeyUp(Keys.Space)) spPr = false;

            // info
            if (keyState.IsKeyDown(Keys.P) && !pPr)
            {
                Console.WriteLine(position);
                foreach (TrackBlock tb in where)
                {
                    if (tb.blockPosition == position)
                    {
                        Console.WriteLine("There is a block on this position. Model: " + tb.modelName + "; Shift: " + tb.blockShift + (tb.finish ? "; the finish" : ""));
                        pPr = true;
                    }
                }
                if (!pPr) { Console.WriteLine("This position is empty."); pPr = true; }

            }
            if (keyState.IsKeyUp(Keys.P)) pPr = false;

            //Finish: the block of the middle layer under the selector becomes the finish of the level (the only one),
            //or stops being it
            if (keyState.IsKeyDown(Keys.F) && !finPr)
            {
                TrackBlock block = track.Find(tb => tb.blockPosition == position);
                if (where != track || block == null)
                    Console.WriteLine("The finish can only be a block of the middle layer.");
                else if (block.finish)
                {
                    block.finish = false;
                    Console.WriteLine("Block " + block.modelName + " at " + position + " is no longer the finish; the level has none.");
                }
                else
                {
                    foreach (TrackBlock tb in track)
                        tb.finish = false;
                    block.finish = true;
                    Console.WriteLine("Block " + block.modelName + " at " + position + " is the finish of the level.");
                }
                finPr = true;
            }
            if (keyState.IsKeyUp(Keys.F)) finPr = false;

            if (keyState.IsKeyDown(Keys.O))
            {
                level.ReadFile();
            }

            if (keyState.IsKeyDown(Keys.End))
            {
                level.WriteFile();
            }

            //// back front Edit
            //.LeftShift (substituted with CapsLock because of the library?)
            if (keyState.IsKeyDown(Keys.RightShift) && !shPr)
            {
                if (bEdit) { bEdit = false; where = track; Console.BackgroundColor = ConsoleColor.Black; Console.WriteLine("Back side edit turned off."); }
                else { bEdit = true; fEdit = false; where = bSide; Console.BackgroundColor = ConsoleColor.DarkGreen; Console.WriteLine("Back side edit turned on."); }
                shPr = true;
            }
            //.LeftShift
            if (keyState.IsKeyUp(Keys.RightShift)) shPr = false;

            if (keyState.IsKeyDown(Keys.RightControl) && !ctPr)
            {
                if (fEdit) { fEdit = false; where = track; Console.BackgroundColor = ConsoleColor.Black; Console.WriteLine("Front side edit turned off."); }
                else { fEdit = true; bEdit = false; where = fSide; Console.BackgroundColor = ConsoleColor.Blue; Console.WriteLine("Front side edit turned on."); }
                ctPr = true;
            }
            if (keyState.IsKeyUp(Keys.RightControl)) ctPr = false;

            //// mouse
            if (Console.CapsLock)
            {
                MouseState mouseState = Mouse.GetState();
                if (mouseState.LeftButton == ButtonState.Pressed && !mlePr || mouseState.RightButton == ButtonState.Pressed && !mlePr)
                {
                    mlePr = true;
                    Ray pickRay = camera.getPickRay(projection, screenManager);
                    foreach (EditedObject ob in gObjects)
                    {
                        if (CheckRayIntersection(pickRay, new Vector3(ob.objPosition.X, ob.objPosition.Y, 0f)))
                        {
                            if (mouseState.LeftButton == ButtonState.Pressed)
                            {
                                ob.isSelected = true;
                                Console.WriteLine("Game object selected.\n Type: " + ob.objName + "\n Model: " + ob.objModelName + "\n Position: " + ob.objPosition);
                                break;
                            }
                            else
                            {
                                Console.WriteLine("Game object deleted.\n Type: " + ob.objName + "\n Model: " + ob.objModelName + "\n Position: " + ob.objPosition);
                                gObjects.Remove(ob);
                                break;
                            }
                        }
                        else ob.isSelected = false;
                    }
                }
                if (mouseState.LeftButton == ButtonState.Released) mlePr = false;
            }

        }

        public override void Draw()
        {
            if (bEdit) posZ = CTW3D(-9f);
            if (fEdit) posZ = CTW3D(9f);
            if (!bEdit && !fEdit) posZ = 0f;
            DrawModel(selectorModel, new Vector3(position.X, position.Y, posZ));
        }
    }
}
