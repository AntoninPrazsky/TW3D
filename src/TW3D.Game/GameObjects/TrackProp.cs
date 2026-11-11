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
    /// <summary>A dynamic object of a level; its shape and physical properties come from the catalog.</summary>
    class TrackProp : GameObject
    {
        private readonly Model model;

        public Body Body { get; }

        /// <summary>The surface of its model, which the skid marks lie on; null if the model has none.</summary>
        public ModelSurface Surface { get; }

        /// <param name="modelName">One of the models of the object type.</param>
        public TrackProp(World world, ScreenManager screenManager, ChaseCamera camera, PhysicsClock physics, Vector2 position,
            ObjectType type, string modelName, LevelAssets assets)
        {
            this.world = world;
            this.screenManager = screenManager;
            this.camera = camera;
            this.physics = physics;
            this.position = position;
            model = assets.ObjectModel(modelName);
            Surface = assets.ObjectSurface(modelName);

            switch (type.Shape)
            {
                case ObjectShape.Model:
                    Body = CreateModelBody(ObjectType.ModelAsset(modelName), BodyType.Dynamic);
                    Body.Position = position;
                    break;
                case ObjectShape.Rectangle:
                    Body = world.CreateBody(position);
                    Body.CreateFixture(new PolygonShape(PolygonTools.CreateRectangle(type.Size, type.Size, Vector2.Zero, 0), 1f));
                    break;
                case ObjectShape.Circle:
                    Body = world.CreateBody(position);
                    Body.CreateFixture(new CircleShape(type.Size, 1f));
                    break;
                default:
                    throw new ArgumentException("Unknown shape " + type.Shape + " of the object type " + type.Type + ".");
            }

            Body.BodyType = BodyType.Dynamic;
            //The body knows its type, so a hit sounds of its material (see GamePlay.ImpactSounds)
            Body.Tag = type;
            Body.SetFriction(type.Friction);
            Body.Mass = type.Mass;
            Body.SetRestitution(type.Restitution);

            //A contact bounces as much as the more bouncy of its two bodies, and the track bounces a little (see
            //Level.TrackRestitution): the contacts of an object bounce at most as much as the object itself, so that
            //an object that does not bounce at all (the trailer, a container) does not hop on the track either.
            float restitution = type.Restitution;
            Body.OnCollision += (sender, other, contact) =>
            {
                contact.Restitution = Math.Min(contact.Restitution, restitution);
                return true;
            };
        }

        public override void Draw(SceneRenderer renderer)
        {
            DrawModel(renderer, model, new Vector3(DrawPosition(Body), 0f), DrawRotation(Body), false);
        }
    }
}
