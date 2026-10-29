using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;

namespace LevelEditor.LevelObjects
{
    class Sky : GameObject
    {
        public Model sky;

        public Sky(GraphicsDeviceManager ScreenManager, Camera3D Camera, ContentManager ContentManager, float Scale)
        {
            screenManager = ScreenManager;
            camera = Camera;
            position = Vector2.Zero;
            scale = Scale;
            contentManager = ContentManager;
            sky = contentManager.Load<Model>("Env/sky");
        }

        public override void Draw()
        {
            DrawModel(sky, new Vector3(-camera.cameraPosition.X + 50f, 0, -10f), false);
        }
    }
}
