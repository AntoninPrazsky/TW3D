using System.Collections.Generic;
using nkast.Aether.Physics2D.Collision.Shapes;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Dynamics;
using TW3D.GamePlay;
using TW3D.Graphics;
using TW3D.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using nkast.Aether.Physics2D.Common.PolygonManipulation;
using nkast.Aether.Physics2D.Common.Decomposition;
using System;

namespace TW3D.GameObjects
{
    class GameObject
    {
        protected World world;
        protected ScreenManager screenManager;
        protected Vector2 position;
        protected ChaseCamera camera;
        protected PhysicsClock physics;
        protected const float scale = 0.25f;

        /// <summary>Position of a body to draw (interpolated between the last two simulation steps).</summary>
        protected Vector2 DrawPosition(Body body)
        {
            return physics != null ? physics.Position(body) : body.Position;
        }

        /// <summary>Rotation of a body to draw (interpolated between the last two simulation steps).</summary>
        protected float DrawRotation(Body body)
        {
            return physics != null ? physics.Rotation(body) : body.Rotation;
        }

        public List<Vertices> CreateListOfVertices(string textureName, float distanceReduction)
        {
            Texture2D texture;

            texture = screenManager.Content.Load<Texture2D>(textureName);
            
            //Array for the bitmap data
            uint[] data = new uint[texture.Width * texture.Height];
            texture.GetData(data);

            //Find the vertices that form the outline of the shape in the original bitmap
            Vertices vertices = PolygonTools.CreatePolygon(data, texture.Width);

            //Centre of the bitmap as the centroid
            Vector2 centroid = new Vector2(-texture.Width / 2, -texture.Height / 2);

            //polygon centroid vs. bitmap "centroid"

            //Translate the vertices so that the found polygon is centred on the centroid
            vertices.Translate(ref centroid);

            vertices = SimplifyTools.ReduceByDistance(vertices, distanceReduction);
            List<Vertices> list = Triangulate.ConvexPartition(vertices, TriangulationAlgorithm.Bayazit);
            
            //Ratio between the image size in the bitmap and the shape size in the physics simulation (constant)
            Vector2 vertScale = new Vector2(0.2524f) * scale;

            foreach (Vertices ver in list)
            {
                ver.Scale(ref vertScale);
            }

            return list;
        }

        public Body CreatePolyBody(string textureName, float distanceReduction, BodyType bodyType)
        {
            List<Vertices> list = CreateListOfVertices(textureName, distanceReduction);
            return world.CreateCompoundPolygon(list, 1f, Vector2.Zero, 0f, bodyType);
        }

        public Body CreatePolyBody(List<Vertices> listOfVertices, BodyType bodyType)
        {
            return world.CreateCompoundPolygon(listOfVertices, 1f, Vector2.Zero, 0f, bodyType);
        }

        /// <summary>Adds a model to the scene, at a position and rotation of the physics world.</summary>
        /// <param name="asBackground">
        /// Draw it behind everything else, unlit and without the depth buffer (used for the sky).
        /// </param>
        public void DrawModel(SceneRenderer renderer, Model model, Vector3 modelPosition, float modelRotation, bool flip, bool asBackground = false)
        {
            Matrix world = ModelWorld(modelPosition, modelRotation, flip);
            if (asBackground)
                renderer.SetBackground(model, world);
            else
                renderer.Add(model, world);
        }

        /// <summary>World matrix of a model at a position and rotation of the physics world.</summary>
        /// <param name="flip">Turned around the vertical axis (the far side of the car).</param>
        protected static Matrix ModelWorld(Vector3 modelPosition, float modelRotation, bool flip)
        {
            float convCon = 10f;

            return (flip ? (Matrix.CreateRotationY(MathHelper.ToRadians(180f)) * (Matrix.CreateRotationZ(modelRotation))) : (Matrix.CreateRotationZ(modelRotation)))
               * Matrix.CreateScale(scale) * Matrix.CreateTranslation(-modelPosition / convCon);
        }

        public Ray GetPickRay(float x, float y)
        {
            return camera.GetPickRay(new Vector2(x, y), screenManager.GraphicsDevice.Viewport);
        }

        public virtual void Update() { }

        public virtual void Draw(SceneRenderer renderer) { }

    }
}
