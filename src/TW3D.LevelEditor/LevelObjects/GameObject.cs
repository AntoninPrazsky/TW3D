using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using System;
using TW3D.Graphics;

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

        /// <summary>Projection of the editor's camera.</summary>
        public static Matrix CreateProjection(Viewport viewport, bool orthographic)
        {
            return orthographic
                ? Matrix.CreateOrthographic(viewport.Width / 250f, viewport.Height / 250f, 1f, 50f)
                : Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(45f), viewport.AspectRatio, 0.1f, 500f);
        }

        /// <summary>World matrix of a model at a position of the editor.</summary>
        protected Matrix ModelWorld(Vector3 modelPosition)
        {
            return Matrix.CreateScale(scale) * Matrix.CreateTranslation(-modelPosition);
        }

        /// <summary>
        /// Draws a model right away, with the lighting of the 2016 version (the level itself is drawn
        /// by the scene renderer; this is for the helpers of the editor, such as the selector).
        /// </summary>
        public void DrawModel(Model model, Vector3 modelPosition)
        {
            // set GraphicsDevice parameters to default after spritebatch work
            screenManager.GraphicsDevice.BlendState = BlendState.Opaque;
            screenManager.GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            screenManager.GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            screenManager.GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

            Matrix world = ModelWorld(modelPosition);
            projection = CreateProjection(screenManager.GraphicsDevice.Viewport, camera.IsOrtographic);

            Matrix[] transforms = new Matrix[model.Bones.Count];
            model.CopyAbsoluteBoneTransformsTo(transforms);

            foreach (ModelMesh mesh in model.Meshes)
            {
                foreach (BasicEffect effect in mesh.Effects)
                {
                    Lighting.ApplyClassic(effect);
                    effect.View = camera.viewMatrix;
                    effect.Projection = projection;
                    effect.World = transforms[mesh.ParentBone.Index] * world;
                }
                mesh.Draw();
            }
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

        public virtual void Draw(SceneRenderer renderer) { }

    }
}
