using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using System;

namespace LevelEditor.LevelObjects
{
    class GameObject
    {
        public GraphicsDeviceManager screenManager { get; set; }
        protected Vector2 position;
        protected float scale;
        protected Camera3D camera;
        public Matrix projection { get; set; }
        protected ContentManager contentManager;

        public float CTW3D(float value) { return value * 2.5f / 10f; }
        public float revCTW3D(float value) { return value / 2.5f * 10f / 9f; }

        public void DrawModel(Model model, Vector3 modelPosition, bool lighting)
        {
            // set GraphicsDevice parameters to default after spritebatch work
            screenManager.GraphicsDevice.BlendState = BlendState.Opaque;
            screenManager.GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            screenManager.GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            screenManager.GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

            Matrix world = Matrix.CreateScale(scale) * Matrix.CreateTranslation(new Vector3(-modelPosition.X, -modelPosition.Y, -modelPosition.Z));

            //Rotate camera around
            //Vector3 cameraRotPosition = Vector3.Transform(cameraPosition - cameraTarget, Matrix.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(90))) + cameraTarget;

            //Matrix view = Matrix.CreateLookAt(cameraPosition, cameraTarget, Vector3.Up);
            
            projection = camera.IsOrtographic ? Matrix.CreateOrthographic((float)screenManager.GraphicsDevice.Viewport.Width/250, (float)screenManager.GraphicsDevice.Viewport.Height/250, 1f, 50f) : Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(45f), screenManager.GraphicsDevice.Viewport.AspectRatio, 0.1f, 500f);
            
            //System.Diagnostics.Debug.WriteLine(Camera.Position);

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

                        effect.View = camera.viewMatrix;
                        effect.Projection = projection;
                        effect.World = transforms[mesh.ParentBone.Index] * world;
                    }
                    mesh.Draw();
                }
            }
            else { model.Draw(world, camera.viewMatrix, projection); }
        }

        public bool CheckRayIntersection(Ray ray, Vector3 modelPosition)
        {
            Vector3 modPos = new Vector3(-modelPosition.X, -modelPosition.Y, 0f);
            float selectedDistance = float.MaxValue;
            BoundingSphere sphere = new BoundingSphere(modPos, 0.25f);

            Nullable<float> result = ray.Intersects(sphere);
            if (result.HasValue == true)
            {
                if (result.Value < selectedDistance)
                {
                    return true;
                }
            }
            return false;
        }
        
        public virtual void Update() { }

        public virtual void Draw() { }

    }
}
