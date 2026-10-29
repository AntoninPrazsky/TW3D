using System.Collections.Generic;
using FarseerPhysics.Collision.Shapes;
using FarseerPhysics.Common;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Factories;
using TW3D.DrawingSystem;
using TW3D.ScreenSystem;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FarseerPhysics.Common.PolygonManipulation;
using FarseerPhysics.Common.Decomposition;
using System;

namespace TW3D.GameObjects
{
    class GameObject
    {
        protected World world;
        protected ScreenManager screenManager;
        protected Vector2 position;
        protected Camera2D camera;
        protected const float scale = 0.25f;

        protected Matrix view;
        protected Matrix projection;
        public BoundingFrustum boundingFrustum { get; set; }
        public BoundingBox boundingBox { get; set; }

        //protected const float farPlane = 22f; //Short distance of the far clipping plane (short view distance)
        protected const float farPlane = 50f; //Long distance of the far clipping plane (medium view distance)

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
            return BodyFactory.CreateCompoundPolygon(world, list, 1f, bodyType);
        }

        public Body CreatePolyBody(List<Vertices> listOfVertices, BodyType bodyType)
        {
            return BodyFactory.CreateCompoundPolygon(world, listOfVertices, 1f, bodyType);
        }

        public void DrawModel(Model model, Vector3 modelPosition, float modelRotation, bool flip, bool lighting)
        {

            screenManager.GraphicsDevice.BlendState = BlendState.Opaque;
            screenManager.GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            screenManager.GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            screenManager.GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

            float convCon = 10f;

            Matrix world = new Matrix();

            world = (flip ? (Matrix.CreateRotationY(MathHelper.ToRadians(180f)) * (Matrix.CreateRotationZ(modelRotation))) : (Matrix.CreateRotationZ(modelRotation)))
               * Matrix.CreateScale(scale) * Matrix.CreateTranslation(-modelPosition / convCon);
            
            // -5 +5
            Vector3 cameraTarget = new Vector3(((-camera.TrackingBody.Position.X) / convCon) - 0.5f, ((-camera.TrackingBody.Position.Y) / convCon), 0f);

            float convCamPos = screenManager.GraphicsDevice.Viewport.Height / 3f;
          
            // 0 +1
            Vector3 cameraPosition = new Vector3(-camera.Position.X / convCamPos, (-camera.Position.Y) / convCamPos + 2f, -4f);
            
            view = Matrix.CreateLookAt(cameraPosition, cameraTarget, new Vector3((float)Math.Cos(-camera.Rotation + Math.PI / 2), (float)Math.Sin(-camera.Rotation + Math.PI / 2), 0));
            
            projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(45f), screenManager.GraphicsDevice.Viewport.AspectRatio, 0.1f, farPlane);
            
            //View frustum for determining visibility
            boundingFrustum = new BoundingFrustum(view * projection);
            
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

        public Ray GetPickRay(float x, float y)
        {
            Vector3 nearsource = new Vector3(x, y, 0f);
            Vector3 farsource = new Vector3(x, y, 1f);

            Matrix wor = Matrix.CreateTranslation(0, 0, 0);

            Vector3 nearPoint = screenManager.GraphicsDevice.Viewport.Unproject(nearsource, projection, view, wor);

            Vector3 farPoint = screenManager.GraphicsDevice.Viewport.Unproject(farsource, projection, view, wor);

            //Create a ray from the near clipping plane to the far clipping plane
            Vector3 direction = farPoint - nearPoint;
            direction.Normalize();
            Ray pickRay = new Ray(nearPoint, direction);

            return pickRay;
        }

        public virtual void Update() { }

        public virtual void Draw() { }

    }
}
