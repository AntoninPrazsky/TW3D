using System.IO;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using nkast.Aether.Physics2D.Collision.Shapes;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Dynamics;
using TW3D.Shared;

namespace TW3D.GameObjects
{
    /// <summary>Collision categories of the bodies in the physics world.</summary>
    static class CollisionCategories
    {
        public const Category CarBody = Category.Cat1;
        public const Category Holder = Category.Cat2;
        public const Category Wheel = Category.Cat3;

        /// <summary>The track where only the body of the car touches it (e.g. the arch of a tunnel, lower above the wheels).</summary>
        public const Category TrackForBody = Category.Cat4;

        /// <summary>The track where only the wheels touch it (e.g. the end of a plate, longer below the wheels).</summary>
        public const Category TrackForWheels = Category.Cat5;

        /// <summary>The track where both touch it (most of it).</summary>
        public const Category Track = TrackForBody | TrackForWheels;
    }

    /// <summary>
    /// The collision shapes of the models (content/collision.json, generated from the models by tools/CollisionGen),
    /// loaded once and shared by all levels.
    /// </summary>
    static class ModelCollision
    {
        private static CollisionShapes shapes;

        public static void Load()
        {
            if (shapes != null)
                return;
            using (Stream stream = TitleContainer.OpenStream("Content/" + CollisionShapes.FileName))
                shapes = CollisionShapes.Load(stream);
        }

        /// <summary>Adds the collision shape of a model to a body, with the density 1.</summary>
        /// <param name="model">Content name of the model.</param>
        /// <param name="category">Collision category of the shape; a block with separate shapes for the body and the wheels of the car gets their categories.</param>
        public static void AddTo(Body body, string model, Category category)
        {
            CollisionShape shape = shapes.Find(model)
                ?? throw new InvalidDataException("The model " + model + " has no collision shape in " + CollisionShapes.FileName + ".");
            if (shape.Shape != null)
                AddPolygons(body, shape.Shape, category);
            else
            {
                AddPolygons(body, shape.Body, CollisionCategories.TrackForBody);
                AddPolygons(body, shape.Wheels, CollisionCategories.TrackForWheels);
            }
        }

        private static void AddPolygons(Body body, List<float[]> polygons, Category category)
        {
            foreach (float[] polygon in polygons)
            {
                //The shapes are in the units of the models (see Units)
                Vertices vertices = new Vertices(polygon.Length / 2);
                for (int i = 0; i < polygon.Length; i += 2)
                    vertices.Add(new Vector2(polygon[i], polygon[i + 1]) * Units.ModelToPhysics);
                Fixture fixture = body.CreateFixture(new PolygonShape(vertices, 1f));
                fixture.CollisionCategories = category;
            }
        }
    }
}
