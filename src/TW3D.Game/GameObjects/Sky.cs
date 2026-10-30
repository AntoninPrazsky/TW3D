using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TW3D.Graphics;
using TW3D.Screens;

namespace TW3D.GameObjects
{
    /// <summary>The sky around the car: a dome that moves with it, drawn behind everything else.</summary>
    class Sky : GameObject
    {
        //How far behind the car the dome is centred (physics units)
        private const float Depth = -25f;

        private readonly Model model;

        public Sky(ScreenManager screenManager, ChaseCamera camera)
        {
            this.screenManager = screenManager;
            this.camera = camera;
            model = screenManager.Content.Load<Model>("Env/sky");
        }

        /// <summary>
        /// The sky is drawn first, as a background: it does not use the depth buffer, so it never hides
        /// the track. (Drawn with depth, the small sky dome used to cut off the track further away.)
        /// </summary>
        public override void Draw(SceneRenderer renderer)
        {
            DrawModel(renderer, model, new Vector3(camera.Focus, Depth), 0f, false, asBackground: true);
        }
    }
}
