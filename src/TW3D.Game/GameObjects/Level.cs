using System.Collections.Generic;
using nkast.Aether.Physics2D.Collision.Shapes;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Dynamics;
using TW3D.Graphics;
using TW3D.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using nkast.Aether.Physics2D.Dynamics.Joints;
using System;
using System.IO;

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
        public List<Vertices> blockVertices { get; set; }
       
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
        private string fileName;
        private string levelData;

        private List<TrackBlock> trackBlocks { get; set; }
        private List<TrackBlock> backTrackBlocks { get; set; }
        private List<TrackBlock> frontTrackBlocks { get; set; }

        private List<TrackBlock> where;

        private List<GameObject> gObjects;
        private List<string> gObNames;

        /*
        public string getLevelData
        {
            get { return levelData; }
        }
        */

        private float CTW3D(float value) { return value * 2.5f; }

        public Level(World World, ScreenManager ScreenManager, ChaseCamera Camera, Vector2 Position, string fileName)
        {
            world = World;
            screenManager = ScreenManager;
            camera = Camera;
            position = Position;
          
            this.fileName = fileName;

            trackBlocks = new List<TrackBlock>();
            backTrackBlocks = new List<TrackBlock>();
            frontTrackBlocks = new List<TrackBlock>();

            gObjects = new List<GameObject>();
            gObNames = new List<string>();
            gObNames.Add("Box");
            gObNames.Add("Bar");
            gObNames.Add("Tb");
            gObNames.Add("Ramp");
            gObNames.Add("Co");
            gObNames.Add("Mov");

            where = trackBlocks;

            ReadFile();

            string modelName = "";
            string x = "";
            string y = "";
            string shift = "";
            bool bodiesLoaded = false;
            string gOName = "";

            //ScreenManager.Game.SuppressDraw();

            for (int i = 0; i < levelData.Length; i++)
            {

                //An asterisk in the loaded file means that back blocks follow, a hash means that front blocks follow.
                //In the file format, these blocks always come after the blocks with a physical representation (the middle blocks).
                //So an asterisk or a hash means that all blocks with a physical representation have already been loaded (bodiesLoaded = true) and we will load
                //the back (backTrackBlock) or front (frontTrackBlocks) blocks.
                if (levelData[i].ToString() == "*") { where = backTrackBlocks; bodiesLoaded = true; if (i < levelData.Length) i++; }
                if (levelData[i].ToString() == "#") { where = frontTrackBlocks; bodiesLoaded = true; if (i < levelData.Length) i++; }
                if (i == levelData.Length) break; //The whole file has been read.

                if (Char.IsLetter(levelData[i])) //A letter means that a model name follows.
                {
                    while (!Char.IsSeparator(levelData[i])) //Read the model name until a separator is reached.
                    {
                        modelName += levelData[i];
                        i++;
                    }

                    i++; //The current position is a separator, move on.

                    //--Game Objects
                    if (gObNames.Contains(modelName)) //The gObNames list contains the names of game objects that are not track blocks.
                    {
                        while (!Char.IsSeparator(levelData[i])) //In the file, the name of a game object is followed by the name of its model.
                        {
                            gOName += levelData[i];
                            i++;
                        }
                        i++;
                    }
                    //--End Game Objects

                    while (Char.IsNumber(levelData[i]) || levelData[i].ToString() == "-") //Read the first coordinate (digits or a minus sign)
                    {
                        x += levelData[i];
                        i++;
                    }

                    i++; //The current position is not a digit, move on.

                    while (Char.IsNumber(levelData[i]) || levelData[i].ToString() == "-") //Read the second coordinate
                    {
                        y += levelData[i];
                        i++;
                    }

                    if (!gObNames.Contains(modelName)) //The model name is not one of the game objects that are not track blocks (i.e. it is a track block).
                    {
                        i++; //After reading the second coordinate the current character is not a digit, move on.

                        while (levelData[i].ToString() != "|") //For track blocks, the coordinates in the file are followed by a third number specifying the vertical shift
                        {
                            shift += levelData[i];
                            i++;
                        }
                    }
                    //--Game Objects
                    if (gOName != "")
                    {
                        //These two special game objects do not need a physics bitmap (in this case modelName is the name of the object type, not of the model)
                        if (modelName == "Bar" || modelName == "Box")
                        {
                            TrackProp obj = new TrackProp(world, screenManager, camera, new Vector2(float.Parse(x), float.Parse(y)), modelName, TrackPropModels.Models[gOName], null);
                            gObjects.Add(obj);
                        }
                        else //These objects need a physics bitmap
                        { 
                            //Check whether the physics bitmap has not been vectorized yet
                            if (!TrackPropVertices.ObjectVertices.ContainsKey(gOName))
                            {
                                //If it has not been vectorized yet, vectorize it and store the result
                                TrackPropVertices.ObjectVertices.Add(gOName, CreateListOfVertices("Env/bit/" + gOName, 10f));
                            }
                            TrackProp obj = new TrackProp(world, screenManager, camera, new Vector2(float.Parse(x), float.Parse(y)), modelName, TrackPropModels.Models[gOName], TrackPropVertices.ObjectVertices[gOName]);
                            gObjects.Add(obj);
                        }
                    }
                    //--End Game Objects
                    else 
                    { 
                        Vector2 bodyPosition = new Vector2(float.Parse(x), float.Parse(y)) * CTW3D(9f);
                        if (shift != "0") bodyPosition = new Vector2(bodyPosition.X, bodyPosition.Y);
    
                        TrackBlock trackBlock = new TrackBlock();
                        trackBlock.blockPosition = bodyPosition;
                        trackBlock.blockShift = CTW3D(float.Parse(shift));

                        if (!bodiesLoaded)
                        {
                            Body trackBlockBody;
                            if(TrackBlockVertices.BlockVertices.ContainsKey(modelName))
                            {
                                trackBlockBody = CreatePolyBody(TrackBlockVertices.BlockVertices[modelName], BodyType.Static);
                            }
                            else
                            {
                                TrackBlockVertices.BlockVertices.Add(modelName, CreateListOfVertices("Levels/bit/" + modelName, 10f));
                                trackBlockBody = CreatePolyBody(TrackBlockVertices.BlockVertices[modelName], BodyType.Static);
                            }

                            trackBlockBody.Position = trackBlock.blockAbsPosition;
                            trackBlockBody.SetRestitution(1f);
                            trackBlockBody.SetFriction(1f);
                            trackBlock.blockBody = trackBlockBody;
                        }
                        else trackBlock.blockBody = null;

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

        private void ReadFile()
        {
            //File system access for Windows Phone 8
            /*
            var resource = System.Windows.Application.GetResourceStream(new Uri(@"Content\Levels\" + _fileName + ".lev", UriKind.Relative));
            using (StreamReader sr = new StreamReader(resource.Stream))
            {
                _levelData = sr.ReadToEnd();
            }
            */

            //File system access for MonoGame (works on all platforms)
            using (var stream = TitleContainer.OpenStream("Content/Levels/" + fileName + ".lev"))
            {
                using (var reader = new StreamReader(stream))
                {
                    levelData = reader.ReadToEnd();
                }
            }
        }

        public override void Update()
        {
            foreach (GameObject obj in gObjects)
            {
                obj.Update();
            }
        }

        public override void Draw()
        {
            //Models outside the camera view are skipped by DrawModel
            foreach (TrackBlock tb in backTrackBlocks)
                DrawModel(tb.blockModel, new Vector3(tb.blockAbsPosition.X, tb.blockAbsPosition.Y, CTW3D(-9f)), 0f, false, true);

            foreach (TrackBlock tb in trackBlocks)
                DrawModel(tb.blockModel, new Vector3(tb.blockBody.Position.X, tb.blockBody.Position.Y, 0f), 0f, false, true);

            foreach (TrackBlock tb in frontTrackBlocks)
                DrawModel(tb.blockModel, new Vector3(tb.blockAbsPosition.X, tb.blockAbsPosition.Y, CTW3D(9f)), 0f, false, true);

            foreach (TrackProp obj in gObjects)
                obj.Draw();
        }
    }
}
