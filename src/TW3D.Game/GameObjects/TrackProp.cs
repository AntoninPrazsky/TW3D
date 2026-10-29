using System.Collections.Generic;
using nkast.Aether.Physics2D.Collision.Shapes;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Dynamics;
using TW3D.DrawingSystem;
using TW3D.ScreenSystem;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using nkast.Aether.Physics2D.Dynamics.Joints;
using System;

namespace TW3D.GameObjects
{
    class TrackProp : GameObject
    {
        public Body propBody { get; set; }
        private Model model;
        private float vis;
        
        public TrackProp(World World, ScreenManager ScreenManager, Camera2D Camera, Vector2 Position, string ObjName, Model ObjModel, List<Vertices> ListOfVertices)
        {
            world = World;
            screenManager = ScreenManager;
            camera = Camera;
            position = Position;
            model = ObjModel;

            if(ListOfVertices != null)
            {
                propBody = CreatePolyBody(ListOfVertices, BodyType.Dynamic);
                propBody.Position = position;
            }
            else
            {
                propBody = world.CreateBody(Position);
                if (ObjName == "Box")
                {
                    PolygonShape rect;
                    rect = new PolygonShape(PolygonTools.CreateRectangle(3.2f, 3.2f, Vector2.Zero, 0), 1f);
                    Fixture fixture = propBody.CreateFixture(rect);
                }
                if (ObjName == "Bar")
                {
                    CircleShape circ;
                    circ = new CircleShape(3f, 1f);
                    Fixture fixture = propBody.CreateFixture(circ);
                }
            }
            propBody.BodyType = BodyType.Dynamic;
            switch (ObjName)
            {
                case "Box":
                        propBody.SetFriction(0.5f);
                        propBody.Mass = 50f;
                        propBody.SetRestitution(0.1f);
                        //vis = 0.6f;
                        vis = 1.2f;
                    break;
                case "Bar":
                        propBody.SetFriction(0.7f);
                        propBody.Mass = 10f;
                        propBody.SetRestitution(0.3f);
                        //vis = 0.4f;
                        vis = 0.8f;
                    break;
                case "Ramp":
                        propBody.SetFriction(1f);
                        propBody.Mass = 100f;
                        propBody.SetRestitution(0f);
                        //vis = 1.2f;
                        vis = 2.4f;
                    break;
                case "Tb":
                        propBody.SetFriction(0.9f);
                        propBody.Mass = 70f;
                        propBody.SetRestitution(0f);
                        //vis = 0.3f;
                        vis = 0.6f;
                    break;
                case "Co":
                        propBody.SetFriction(0.9f);
                        propBody.Mass = 500f;
                        propBody.SetRestitution(0f);
                        //vis = 1.8f;
                        vis = 3.6f;
                    break;
                case "Mov":
                        propBody.SetFriction(0.45f);
                        propBody.Mass = 200f;
                        propBody.SetRestitution(0f);
                        //vis = 2.2f;
                        vis = 4.4f;
                    break;
            }
        }

        public override void Update()
        {
            boundingBox = new BoundingBox(new Vector3(-propBody.Position.X / 10f - vis, -propBody.Position.Y / 10f - vis, 0f),
                                            new Vector3(-propBody.Position.X / 10f + vis, -propBody.Position.Y / 10f + vis, 0f));
        }

        public override void Draw()
        {
            DrawModel(model, new Vector3(propBody.Position.X, propBody.Position.Y, 0f), propBody.Rotation, false, true);
        }
    }
}
