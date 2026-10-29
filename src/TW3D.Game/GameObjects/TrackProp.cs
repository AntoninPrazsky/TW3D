using System.Collections.Generic;
using nkast.Aether.Physics2D.Collision.Shapes;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Dynamics;
using TW3D.GamePlay;
using TW3D.Graphics;
using TW3D.Screens;
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
        
        public TrackProp(World World, ScreenManager ScreenManager, ChaseCamera Camera, PhysicsClock Physics, Vector2 Position, string ObjName, Model ObjModel, List<Vertices> ListOfVertices)
        {
            world = World;
            screenManager = ScreenManager;
            camera = Camera;
            physics = Physics;
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
                    break;
                case "Bar":
                        propBody.SetFriction(0.7f);
                        propBody.Mass = 10f;
                        propBody.SetRestitution(0.3f);
                    break;
                case "Ramp":
                        propBody.SetFriction(1f);
                        propBody.Mass = 100f;
                        propBody.SetRestitution(0f);
                    break;
                case "Tb":
                        propBody.SetFriction(0.9f);
                        propBody.Mass = 70f;
                        propBody.SetRestitution(0f);
                    break;
                case "Co":
                        propBody.SetFriction(0.9f);
                        propBody.Mass = 500f;
                        propBody.SetRestitution(0f);
                    break;
                case "Mov":
                        propBody.SetFriction(0.45f);
                        propBody.Mass = 200f;
                        propBody.SetRestitution(0f);
                    break;
            }
        }

        public override void Draw()
        {
            DrawModel(model, new Vector3(DrawPosition(propBody), 0f), DrawRotation(propBody), false, true);
        }
    }
}
