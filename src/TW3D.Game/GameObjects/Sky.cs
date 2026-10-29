using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TW3D.Graphics;
using TW3D.Screens;

namespace TW3D.GameObjects
{
    class Sky : GameObject
    {
        Model sky;

        public Sky(ScreenManager ScreenManager, ChaseCamera Camera)
        {
            screenManager = ScreenManager;
            camera = Camera;
            sky = screenManager.Content.Load<Model>("Env/sky");
        }

        public override void Draw()
        {
            DrawModel(sky, new Vector3(camera.Target.Position.X, camera.Target.Position.Y, -25f), 0f, false, false);
        }
    }
}
