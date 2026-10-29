using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TW3D.ScreenSystem;

namespace TW3D.GameObjects
{
    class Sky : GameObject
    {
        Model sky;

        public Sky(ScreenManager ScreenManager, Camera2D Camera)
        {
            screenManager = ScreenManager;
            camera = Camera;
            sky = screenManager.Content.Load<Model>("Env/sky");
        }

        public override void Draw()
        {
            DrawModel(sky, new Vector3(camera.TrackingBody.Position.X, camera.TrackingBody.Position.Y, -25f), 0f, false, false);
        }
    }
}
