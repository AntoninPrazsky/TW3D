using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using System.Windows.Forms;
using System.Text;

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
        private string _fileName;
        private string _levelData;

        public List<TrackBlock> trackBlocks { get; set; }
        public List<TrackBlock> backTrackBlocks { get; set; }
        public List<TrackBlock> frontTrackBlocks { get; set; }

        private List<TrackBlock> where;

        public List<EditedObject> gObjects { get; set; }
        private List<string> gObNames;

        public string getLevelData
        {
            get { return _levelData; }
        }

        public Level(GraphicsDeviceManager ScreenManager, Camera3D Camera, Vector2 Position, string fileName, float Scale)
        {
            screenManager = ScreenManager;
            camera = Camera;
            position = Position;
            scale = Scale;
            
            _fileName = fileName;

            trackBlocks = new List<TrackBlock>();
            backTrackBlocks = new List<TrackBlock>();
            frontTrackBlocks = new List<TrackBlock>();

            gObjects = new List<EditedObject>();
            gObNames = new List<string>();
            gObNames.Add("Box");
            gObNames.Add("Bar");
            gObNames.Add("Tb");
            gObNames.Add("Ramp");
            gObNames.Add("Co");
            gObNames.Add("Mov");

            where = trackBlocks;

            ReadFile();
        }

        public void ReadFile()
        {
            Stream stream = null;
            OpenFileDialog openFileDialog = new OpenFileDialog();
            
            openFileDialog.InitialDirectory = "C:\\levels";
            openFileDialog.Filter = "Level files (*.lev)|*.lev";
            openFileDialog.FilterIndex = 2;
            openFileDialog.RestoreDirectory = true;
            
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    if ((stream = openFileDialog.OpenFile()) != null)
                        using (stream)
                        {
                            TextReader tr = new StreamReader(stream);
                            _levelData = tr.ReadToEnd();
                            Console.WriteLine("File " + openFileDialog.FileName + " loaded.\n");
                        }

                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: Could not read file from disk. Original error: " + ex.Message);
                }

                trackBlocks.Clear();
                backTrackBlocks.Clear();
                frontTrackBlocks.Clear();
                gObjects.Clear();

                string modelName = "";
                string x = "";
                string y = "";
                string shift = "";
                where = trackBlocks;

                string gOName = "";

                for (int i = 0; i < _levelData.Length; i++)
                {
                    if (_levelData[i].ToString() == "*") { where = backTrackBlocks; if (i < _levelData.Length) i++;}
                    if (_levelData[i].ToString() == "#") { where = frontTrackBlocks; if (i < _levelData.Length) i++;}
                    if (i == _levelData.Length) break;

                    if (Char.IsLetter(_levelData[i])) 
                    {
                        while (!Char.IsSeparator(_levelData[i]))
                        {
                            modelName += _levelData[i];
                            i++;
                        }

                        i++;


                        //--Game Objects
                        if (gObNames.Contains(modelName)) 
                        {
                            while (!Char.IsSeparator(_levelData[i])) 
                            {
                                gOName += _levelData[i];
                                i++;
                            }
                            i++;
                        }
                        //--End Game Objects

                        while (Char.IsNumber(_levelData[i]) || _levelData[i].ToString() == "-")
                        {
                            x += _levelData[i];
                            i++;
                        }

                        i++;

                        while (Char.IsNumber(_levelData[i]) || _levelData[i].ToString() == "-")
                        {
                            y += _levelData[i];
                            i++;
                        }

                        if (!gObNames.Contains(modelName))
                        {
                            i++;

                            while (_levelData[i].ToString() != "|")
                            {
                                shift += _levelData[i];
                                i++;
                            }
                        }

                        //--Game Objects
                        if (gOName != "")
                        {
                            EditedObject gObj = new EditedObject();
                            gObj.objName = modelName;
                            gObj.objModelName = gOName;
                            gObj.objPosition = new Vector2(float.Parse(x), float.Parse(y)) / 10;
                            gObj.objModel = TrackPropModels.Models[gOName];
                            gObjects.Add(gObj);
                            
                        }
                        //--End Game Objects
                        else { 
                        Vector2 bodyPosition = new Vector2(float.Parse(x), float.Parse(y)) * CTW3D(9f);
                        TrackBlock trackBlock = new TrackBlock();
                        trackBlock.blockPosition = bodyPosition;
                        trackBlock.blockShift = CTW3D(float.Parse(shift));
                        trackBlock.blockModel = TrackBlockModels.Models[modelName];
                        trackBlock.modelName = modelName;
                        where.Add(trackBlock);
                        }
                        modelName = "";
                        x = "";
                        y = "";
                        shift = "";
                        gOName = "";
                    }
                }

            }
        }

        public void WriteFile()
        {
            StringBuilder saveData = new StringBuilder();
            foreach(TrackBlock tb in trackBlocks)
            {
                saveData.Append(tb.modelName + " " + revCTW3D(tb.blockPosition.X) + "," + revCTW3D(tb.blockPosition.Y) + "," + revCTW3D(tb.blockShift) * 9f + "|");
            }
            saveData.Append("*");
            foreach(TrackBlock tb in backTrackBlocks)
            {
                saveData.Append(tb.modelName + " " + revCTW3D(tb.blockPosition.X) + "," + revCTW3D(tb.blockPosition.Y) + "," + revCTW3D(tb.blockShift) * 9f + "|");
            }
            saveData.Append("#");
            foreach (TrackBlock tb in frontTrackBlocks)
            {
                saveData.Append(tb.modelName + " " + revCTW3D(tb.blockPosition.X) + "," + revCTW3D(tb.blockPosition.Y) + "," + revCTW3D(tb.blockShift) * 9f + "|");
            }
            foreach(EditedObject ob in gObjects)
            {
                saveData.Append(ob.objName + " " + ob.objModelName + " " + ob.objPosition.X * 10 + "," + ob.objPosition.Y * 10 + "|");
            }

            Stream stream;
            SaveFileDialog saveFileDialog = new SaveFileDialog();

            saveFileDialog.InitialDirectory = "C:\\levels";
            saveFileDialog.Filter = "Level files (*.lev)|*.lev";
            saveFileDialog.FilterIndex = 2;
            saveFileDialog.RestoreDirectory = true;

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                if ((stream = saveFileDialog.OpenFile()) != null)
                {
                    StreamWriter sw = new StreamWriter(stream);
                    sw.Write(saveData);
                    sw.Flush();
                    sw.Close();
                    stream.Close();
                    Console.WriteLine("File " + saveFileDialog.FileName + " saved.");
                }
            }
        }

        public override void Draw()
        {
            foreach (TrackBlock tb in trackBlocks)
            {
                DrawModel(tb.blockModel, new Vector3(tb.blockAbsPosition.X, tb.blockAbsPosition.Y, 0f), true);
            }
            foreach (TrackBlock tb in backTrackBlocks)
            {
                DrawModel(tb.blockModel, new Vector3(tb.blockAbsPosition.X, tb.blockAbsPosition.Y, CTW3D(-9f)), true);
            }
            foreach (TrackBlock tb in frontTrackBlocks)
            {
                DrawModel(tb.blockModel, new Vector3(tb.blockAbsPosition.X, tb.blockAbsPosition.Y, CTW3D(9f)), true);
            }
            foreach (EditedObject obj in gObjects)
            {
                DrawModel(obj.objModel, new Vector3(obj.objPosition.X, obj.objPosition.Y, 0f), true);
            }
        }
    }
}
