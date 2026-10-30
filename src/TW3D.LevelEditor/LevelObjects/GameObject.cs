using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TW3D.Graphics;
using TW3D.Shared;

namespace LevelEditor.LevelObjects
{
    /// <summary>
    /// Something drawn in the editor: the level or the selector. Positions in the editor are in scene units with the
    /// sign of the physics world (a position p is drawn at -p): a tenth of the physics units of the level file,
    /// see <see cref="Units"/>.
    /// </summary>
    class GameObject
    {
        /// <summary>Physics units (of the level file) in one unit of the editor (1 / <see cref="Units.PhysicsToScene"/>).</summary>
        protected const float PhysicsPerUnit = 10f;

        /// <summary>One cell of the level grid (the size of a block) in the units of the editor.</summary>
        public const float GridCell = Units.GridCell / PhysicsPerUnit;

        /// <summary>One step of the vertical shift of a block in the units of the editor.</summary>
        public const float ShiftStep = Units.ShiftStep / PhysicsPerUnit;

        protected GraphicsDeviceManager graphics;
        protected Camera3D camera;
        protected Vector2 position;

        /// <summary>Projection of the editor's camera.</summary>
        public static Matrix CreateProjection(Viewport viewport, bool orthographic)
        {
            return orthographic
                ? Matrix.CreateOrthographic(viewport.Width / 250f, viewport.Height / 250f, 1f, 50f)
                : Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(45f), viewport.AspectRatio, 0.1f, 500f);
        }

        /// <summary>World matrix of a model at a position of the editor.</summary>
        protected static Matrix ModelWorld(Vector3 modelPosition)
        {
            return Matrix.CreateScale(Units.ModelToScene) * Matrix.CreateTranslation(-modelPosition);
        }

        /// <summary>
        /// Draws a model right away, with the lighting of the 2016 version (the level itself is drawn
        /// by the scene renderer; this is for the helpers of the editor, such as the selector).
        /// </summary>
        public void DrawModel(Model model, Vector3 modelPosition)
        {
            GraphicsDevice device = graphics.GraphicsDevice;
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullCounterClockwise;
            device.SamplerStates[0] = SamplerState.LinearWrap;

            Matrix world = ModelWorld(modelPosition);
            Matrix projection = CreateProjection(device.Viewport, camera.IsOrthographic);
            Matrix[] transforms = new Matrix[model.Bones.Count];
            model.CopyAbsoluteBoneTransformsTo(transforms);

            foreach (ModelMesh mesh in model.Meshes)
            {
                foreach (BasicEffect effect in mesh.Effects)
                {
                    Lighting.ApplyClassic(effect);
                    effect.View = camera.View;
                    effect.Projection = projection;
                    effect.World = transforms[mesh.ParentBone.Index] * world;
                }
                mesh.Draw();
            }
        }

        /// <summary>The ray (from the camera through a point of the screen) hits a thing at a position of the editor.</summary>
        public static bool RayHits(Ray ray, Vector3 position)
        {
            return ray.Intersects(new BoundingSphere(-position, 0.25f)).HasValue;
        }

        public virtual void Update() { }

        public virtual void Draw() { }

        public virtual void Draw(SceneRenderer renderer) { }
    }
}
