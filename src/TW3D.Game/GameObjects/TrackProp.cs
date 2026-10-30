using System.Collections.Generic;
using nkast.Aether.Physics2D.Collision.Shapes;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Dynamics;
using TW3D.GamePlay;
using TW3D.Graphics;
using TW3D.Screens;
using TW3D.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TW3D.GameObjects
{
    /// <summary>A dynamic object of the level; its shape and physical properties come from the catalog.</summary>
    class TrackProp : GameObject
    {
        public Body propBody { get; set; }
        private Model model;

        /// <param name="ListOfVertices">Shape from the collision bitmap; only for objects with the bitmap shape.</param>
        public TrackProp(World World, ScreenManager ScreenManager, ChaseCamera Camera, PhysicsClock Physics, Vector2 Position, ObjectType type, Model ObjModel, List<Vertices> ListOfVertices)
        {
            world = World;
            screenManager = ScreenManager;
            camera = Camera;
            physics = Physics;
            position = Position;
            model = ObjModel;

            switch (type.Shape)
            {
                case ObjectShape.Bitmap:
                    propBody = CreatePolyBody(ListOfVertices, BodyType.Dynamic);
                    propBody.Position = position;
                    break;
                case ObjectShape.Rectangle:
                    propBody = world.CreateBody(Position);
                    propBody.CreateFixture(new PolygonShape(PolygonTools.CreateRectangle(type.Size, type.Size, Vector2.Zero, 0), 1f));
                    break;
                case ObjectShape.Circle:
                    propBody = world.CreateBody(Position);
                    propBody.CreateFixture(new CircleShape(type.Size, 1f));
                    break;
            }

            propBody.BodyType = BodyType.Dynamic;
            propBody.SetFriction(type.Friction);
            propBody.Mass = type.Mass;
            propBody.SetRestitution(type.Restitution);
        }

        public override void Draw(SceneRenderer renderer)
        {
            DrawModel(renderer, model, new Vector3(DrawPosition(propBody), 0f), DrawRotation(propBody), false);
        }
    }
}
