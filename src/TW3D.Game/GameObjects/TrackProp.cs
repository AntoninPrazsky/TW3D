using System;
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

        /// <param name="modelName">One of the models of the object type.</param>
        public TrackProp(World World, ScreenManager ScreenManager, ChaseCamera Camera, PhysicsClock Physics, Vector2 Position, ObjectType type, string modelName)
        {
            world = World;
            screenManager = ScreenManager;
            camera = Camera;
            physics = Physics;
            position = Position;
            model = TrackPropModels.Models[modelName];

            switch (type.Shape)
            {
                case ObjectShape.Model:
                    propBody = CreateModelBody(ObjectType.ModelAsset(modelName), BodyType.Dynamic);
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

            //A contact bounces as much as the more bouncy of its two bodies. The track bounces fully (the car
            //bounces on it), so an object resting on it would hop for ever and never fall asleep: the contacts
            //of an object bounce at most as much as the object itself.
            float restitution = type.Restitution;
            propBody.OnCollision += (sender, other, contact) =>
            {
                contact.Restitution = Math.Min(contact.Restitution, restitution);
                return true;
            };
        }

        public override void Draw(SceneRenderer renderer)
        {
            DrawModel(renderer, model, new Vector3(DrawPosition(propBody), 0f), DrawRotation(propBody), false);
        }
    }
}
