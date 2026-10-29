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

        /// <param name="asBackground">
        /// Draw without the depth buffer, so the model never hides anything drawn after it (used for the sky).
        /// </param>
        public void DrawModel(Model model, Vector3 modelPosition, float modelRotation, bool flip, bool lighting, bool asBackground = false)
        {
            float convCon = 10f;

            Matrix world = new Matrix();

            world = (flip ? (Matrix.CreateRotationY(MathHelper.ToRadians(180f)) * (Matrix.CreateRotationZ(modelRotation))) : (Matrix.CreateRotationZ(modelRotation)))
               * Matrix.CreateScale(scale) * Matrix.CreateTranslation(-modelPosition / convCon);

            //Models whose bounding sphere is completely outside the camera view are not drawn
            if (camera.Frustum.Contains(ModelBounds(model).Transform(world)) == ContainmentType.Disjoint)
                return;

            screenManager.GraphicsDevice.BlendState = BlendState.Opaque;
            screenManager.GraphicsDevice.DepthStencilState = asBackground ? DepthStencilState.None : DepthStencilState.Default;
            screenManager.GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            screenManager.GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

            Matrix view = camera.View;
            Matrix projection = camera.Projection;

            if (lighting)
            {
                Matrix[] transforms = new Matrix[model.Bones.Count];
                model.CopyAbsoluteBoneTransformsTo(transforms);
                foreach (ModelMesh mesh in model.Meshes)
                {
                    foreach (BasicEffect effect in mesh.Effects)
                    {
                        effect.PreferPerPixelLighting = true;
                        effect.LightingEnabled = true;
                        effect.AmbientLightColor = new Vector3(0.2f, 0.2f, 0.2f);

                        effect.SpecularColor = new Vector3(1, 1, 1);
                        effect.SpecularPower = 30;

                        effect.DirectionalLight0.Direction = new Vector3(1, -1, 0);
                        effect.DirectionalLight0.DiffuseColor = new Vector3(0.6f, 0.6f, 0.6f);
                        effect.DirectionalLight0.SpecularColor = new Vector3(1, 1, 1);

                        effect.DirectionalLight1.Direction = new Vector3(0, -1, -1);
                        effect.DirectionalLight1.DiffuseColor = new Color(90, 166, 206).ToVector3() / 2;
                        effect.DirectionalLight1.SpecularColor = new Color(90, 166, 206).ToVector3();
                        effect.DirectionalLight1.Enabled = true;

                        effect.DirectionalLight2.Direction = new Vector3(-1, -1, 0);
                        effect.DirectionalLight2.DiffuseColor = new Color(74, 125, 156).ToVector3() / 2;
                        effect.DirectionalLight2.SpecularColor = new Color(74, 125, 156).ToVector3();
                        effect.DirectionalLight2.Enabled = true;

                        effect.View = view;
                        effect.Projection = projection;
                        effect.World = transforms[mesh.ParentBone.Index] * world;
                    }
                    mesh.Draw();
                }
            }
            else { model.Draw(world, view, projection); }
        }

        private static readonly Dictionary<Model, BoundingSphere> modelBounds = new Dictionary<Model, BoundingSphere>();

        /// <summary>Bounding sphere of the whole model in model coordinates (computed once per model).</summary>
        private static BoundingSphere ModelBounds(Model model)
        {
            if (modelBounds.TryGetValue(model, out BoundingSphere bounds))
                return bounds;

            Matrix[] transforms = new Matrix[model.Bones.Count];
            model.CopyAbsoluteBoneTransformsTo(transforms);
            bool first = true;
            foreach (ModelMesh mesh in model.Meshes)
            {
                BoundingSphere meshBounds = mesh.BoundingSphere.Transform(transforms[mesh.ParentBone.Index]);
                bounds = first ? meshBounds : BoundingSphere.CreateMerged(bounds, meshBounds);
                first = false;
            }

            modelBounds[model] = bounds;
            return bounds;
        }

        public Ray GetPickRay(float x, float y)
        {
            return camera.GetPickRay(new Vector2(x, y), screenManager.GraphicsDevice.Viewport);
        }

        public virtual void Update() { }

        public virtual void Draw() { }

    }
}
