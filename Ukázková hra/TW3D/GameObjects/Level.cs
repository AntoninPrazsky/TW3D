using System.Collections.Generic;
using FarseerPhysics.Collision.Shapes;
using FarseerPhysics.Common;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Factories;
using FarseerPhysics.Graphics.DrawingSystem;
using FarseerPhysics.Graphics.ScreenSystem;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FarseerPhysics.Dynamics.Joints;
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
        public BoundingBox boundingBox { get; set; }
       
    }

    static class TrackBlockVertices //Vrcholy nalezené v bitmapách bloků
    {
        public static Dictionary<string, List<Vertices>> BlockVertices = new Dictionary<string,List<Vertices>>();
    }

    static class TrackPropVertices //Vrcholy nalezené v bitmapách dynamických objektů
    {
        public static Dictionary<string, List<Vertices>> ObjectVertices = new Dictionary<string, List<Vertices>>();
    }

    static class TrackBlockModels //Modely bloků trati
    {
        public static Dictionary<string, Model> Models = new Dictionary<string, Model>();
    }

    static class TrackPropModels //Modely dynamických objektů
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

        public Level(World World, ScreenManager ScreenManager, Camera2D Camera, Vector2 Position, string fileName)
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

            boundingFrustum = new BoundingFrustum(new Matrix());

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

                //Hvězdička v načítaném souboru značí, že bude následovat zápis zadních bloků, křížek značí, že bude následovat zápis předních bloků.
                //Zápis těchto bloků ve formátu souboru následuje vždy až po načtení bloků s fyzikální reprezentací (prostředních bloků).
                //Hvězdička nebo křížek tedy značí, že všechny bloky s fyzikální reprezentací už byly načteny (bodiesLoaded = true) a budeme načítat
                //zadní (backTrackBlock) nebo přední (frontTrackBlocks) bloky.
                if (levelData[i].ToString() == "*") { where = backTrackBlocks; bodiesLoaded = true; if (i < levelData.Length) i++; }
                if (levelData[i].ToString() == "#") { where = frontTrackBlocks; bodiesLoaded = true; if (i < levelData.Length) i++; }
                if (i == levelData.Length) break; //Celý soubor byl přečten.

                if (Char.IsLetter(levelData[i])) //Písmeno znamená, že bude následovat název modelu.
                {
                    while (!Char.IsSeparator(levelData[i])) //Čtení názvu modelu, dokud se nenarazí na separátor.
                    {
                        modelName += levelData[i];
                        i++;
                    }

                    i++; //Aktuální pozice je separátor, dojde k posunutí.

                    //--Game Objects
                    if (gObNames.Contains(modelName)) //List gObNames obsahuje jména herních objektů, které nejsou bloky trati.
                    {
                        while (!Char.IsSeparator(levelData[i])) //Po názvu herního objektu v souboru následuje název jeho modelu.
                        {
                            gOName += levelData[i];
                            i++;
                        }
                        i++;
                    }
                    //--End Game Objects

                    while (Char.IsNumber(levelData[i]) || levelData[i].ToString() == "-") //Přečtení první souřadnice (číslo nebo znak mínus)
                    {
                        x += levelData[i];
                        i++;
                    }

                    i++; //Aktuální pozice není číslo, dojde k posunutí.

                    while (Char.IsNumber(levelData[i]) || levelData[i].ToString() == "-") //Přečtení druhé souřadnice
                    {
                        y += levelData[i];
                        i++;
                    }

                    if (!gObNames.Contains(modelName)) //Název modelu není herním objektem, které nejsou bloky trati.
                    {
                        i++; //Po přečtení druhé souřadnice aktuální znak není číslo, dojde k posunutí.

                        while (levelData[i].ToString() != "|") //U bloků trati ve formátu souboru následuje po souřadnicích třetí číslo určující vertikální posunutí
                        {
                            shift += levelData[i];
                            i++;
                        }
                    }
                    //--Game Objects
                    if (gOName != "")
                    {
                        //Tyto dva speciální herní objekty nepotřebují bitmapovou fyzikální mapu (modelName je v tomto případě název typu objektu, ne modelu)
                        if (modelName == "Bar" || modelName == "Box")
                        {
                            TrackProp obj = new TrackProp(world, screenManager, camera, new Vector2(float.Parse(x), float.Parse(y)), modelName, TrackPropModels.Models[gOName], null);
                            gObjects.Add(obj);
                        }
                        else //Tyto objekty potřebují bitmapovou fyzikální mapu
                        { 
                            //Podíváme se, jestli fyzikální bitmapa ještě nebyla vektorizovaná
                            if (!TrackPropVertices.ObjectVertices.ContainsKey(gOName))
                            {
                                //Pokud nebyla vektorizovaná, vektorizujeme ji a uložíme si výsledek
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
                            trackBlockBody.Restitution = 1f;
                            trackBlockBody.Friction = 1f;
                            trackBlock.blockBody = trackBlockBody;
                        }
                        else trackBlock.blockBody = null;

                        float pos = 0;
                        if (where == backTrackBlocks) pos = CTW3D(9f) / 10f;
                        if (where == frontTrackBlocks) pos = CTW3D(-9f) / 10f;
                        //float vis = 1.8f; //Malá dohlednost
                        float vis = 5f; //Velká dohlednost
                        
                        trackBlock.boundingBox = new BoundingBox(
                                new Vector3(-trackBlock.blockAbsPosition.X / 10f - vis, -trackBlock.blockAbsPosition.Y / 10f - vis, pos), 
                                new Vector3(-trackBlock.blockAbsPosition.X / 10f + vis, -trackBlock.blockAbsPosition.Y / 10f + vis, pos));
                        
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
            //Verze přístupu k souborovému systému pro Windows Phone 8
            /*
            var resource = System.Windows.Application.GetResourceStream(new Uri(@"Content\Levels\" + _fileName + ".lev", UriKind.Relative));
            using (StreamReader sr = new StreamReader(resource.Stream))
            {
                _levelData = sr.ReadToEnd();
            }
            */

            //Verze přístupu k souborovému systému pro univerzální Windows 10
            using (var stream = TitleContainer.OpenStream(@"Content\Levels\" + fileName + ".lev"))
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
            
            foreach (TrackBlock tb in backTrackBlocks)
            {
                if (boundingFrustum.Contains(tb.boundingBox) != ContainmentType.Disjoint)
                    DrawModel(tb.blockModel, new Vector3(tb.blockAbsPosition.X, tb.blockAbsPosition.Y, CTW3D(-9f)), 0f, false, true);
            }
            
            foreach (TrackBlock tb in trackBlocks)
            {
                if (boundingFrustum.Contains(tb.boundingBox) != ContainmentType.Disjoint)
                {
                    //tb.blockBody.Enabled = true;
                    DrawModel(tb.blockModel, new Vector3(tb.blockBody.Position.X, tb.blockBody.Position.Y, 0f), 0f, false, true);
                }
                //else tb.blockBody.Enabled = false;
            }
            
            foreach (TrackBlock tb in frontTrackBlocks)
            {
                if (boundingFrustum.Contains(tb.boundingBox) != ContainmentType.Disjoint)
                    DrawModel(tb.blockModel, new Vector3(tb.blockAbsPosition.X, tb.blockAbsPosition.Y, CTW3D(9f)), 0f, false, true);
            }

            foreach (TrackProp obj in gObjects)
            {
                if (boundingFrustum.Contains(obj.boundingBox) != ContainmentType.Disjoint)
                {
                    //obj.propBody.Enabled = true;
                    obj.Draw();
                }
                //Přerušení výpočtu fyzikální simulace objektů, které jsou mimo promítací kužel
                //else obj.propBody.Enabled = false;
            }

        }
    }
}
