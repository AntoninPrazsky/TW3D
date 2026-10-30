using nkast.Aether.Physics2D.Dynamics;
using TW3D.GamePlay;
using TW3D.Graphics;
using TW3D.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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

        /// <summary>Moves the drawn object in the 3D world, wherever its bodies are (the cars standing in the garage of the menu).</summary>
        public Matrix Placement { get; set; } = Matrix.Identity;

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

        /// <summary>A body with the collision shape of a model (see <see cref="ModelCollision"/>).</summary>
        /// <param name="model">Content name of the model.</param>
        public Body CreateModelBody(string model, BodyType bodyType, Category category = Category.Cat1)
        {
            Body body = world.CreateBody(Vector2.Zero, 0f, bodyType);
            ModelCollision.AddTo(body, model, category);
            return body;
        }

        /// <summary>Adds a model to the scene, at a position and rotation of the physics world.</summary>
        /// <param name="asBackground">
        /// Draw it behind everything else, unlit and without the depth buffer (used for the sky).
        /// </param>
        public void DrawModel(SceneRenderer renderer, Model model, Vector3 modelPosition, float modelRotation, bool flip, bool asBackground = false)
        {
            Matrix world = ModelWorld(modelPosition, modelRotation, flip) * Placement;
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
