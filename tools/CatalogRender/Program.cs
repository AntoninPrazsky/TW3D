using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using TW3D.Graphics;
using TW3D.Shared;

namespace TW3D.CatalogRender
{
    /// <summary>
    /// Renders images of track blocks for the block catalog (docs/catalog/bl) the way the 2016 images look:
    /// the block with the selector of the level editor, in the editor's lighting, on a gray background.
    /// </summary>
    internal sealed class CatalogRenderer : Game
    {
        private const int Width = 481;
        private const int Height = 477;

        //The camera the 2016 catalog images were taken with in the level editor. It was not saved anywhere,
        //so it was found by fitting renders of the selector to the catalog images (the edges of the selector
        //are 0.14 px apart on average). Position in the editor's 3D units relative to the block, the angles
        //of the editor's camera (Camera3D: leftrightRot, updownRot), a roll of practically zero, the focal
        //length in pixels and the point of the image the camera looks at (the images are cut out of the window).
        private static readonly Vector3 CameraPosition = new Vector3(1.4236505f, 1.5330524f, -3.9857758f);
        private const float Yaw = -3.5365972f;
        private const float Pitch = -0.40779745f;
        private const float Roll = -0.0022125329f;
        private const float FocalLength = 620.38416f;
        private const float CenterX = 239.49712f;
        private const float CenterY = 224.61302f;

        //Renders of the right block differ from its catalog image by about 8–16 (RMS of the colour channels,
        //0–255; the 2016 images were drawn by XNA); much more means the image shows another block
        private const double MismatchLimit = 30;

        //Blocks whose 2016 image was taken from another position, as they reach out of their cell
        private static readonly HashSet<string> OwnFraming = new HashSet<string> { "lo" };

        private readonly string command;
        private readonly string[] arguments;
        private readonly string repository;
        private readonly GraphicsDeviceManager graphics;
        private RenderTarget2D target;
        private Model selector;

        public int ExitCode { get; private set; }

        private CatalogRenderer(string command, string[] arguments, string repository)
        {
            this.command = command;
            this.arguments = arguments;
            this.repository = repository;
            graphics = new GraphicsDeviceManager(this) { PreferMultiSampling = false };
            Content.RootDirectory = "Content";
        }

        private string CatalogImages => Path.Combine(repository, "docs", "catalog", "bl");

        protected override void LoadContent()
        {
            target = new RenderTarget2D(GraphicsDevice, Width, Height, false, SurfaceFormat.Color, DepthFormat.Depth24);
            selector = Content.Load<Model>("Levels/selector");

            try
            {
                if (command == "render")
                    Render();
                else
                    Check();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                ExitCode = 1;
            }
            Exit();
        }

        /// <summary>render [--out folder] block...: writes the images of the blocks (by default into the catalog).</summary>
        private void Render()
        {
            string folder = CatalogImages;
            List<string> blocks = new List<string>();
            for (int i = 0; i < arguments.Length; i++)
            {
                if (arguments[i] == "--out" && i + 1 < arguments.Length)
                    folder = arguments[++i];
                else
                    blocks.Add(arguments[i]);
            }
            if (blocks.Count == 0)
                throw new ArgumentException("No blocks to render.");

            Directory.CreateDirectory(folder);
            foreach (string block in blocks)
            {
                string file = Path.Combine(folder, block + ".png");
                using (Texture2D image = RenderBlock(block))
                using (FileStream stream = File.Create(file))
                    image.SaveAsPng(stream, Width, Height);
                Console.WriteLine(file);
            }
        }

        /// <summary>check: compares renders of all blocks of the catalog with their images.</summary>
        private void Check()
        {
            Catalog catalog = Catalog.Load(Path.Combine(repository, "content", "catalog.json"));
            foreach (BlockType block in catalog.Blocks)
            {
                string file = Path.Combine(CatalogImages, block.Name + ".png");
                if (!File.Exists(file))
                {
                    Console.WriteLine($"{block.Name,-6} no image");
                    ExitCode = 1;
                    continue;
                }
                if (OwnFraming.Contains(block.Name))
                {
                    Console.WriteLine($"{block.Name,-6}  -     the image has its own framing, not compared");
                    continue;
                }

                double difference;
                using (Texture2D render = RenderBlock(block.Name))
                using (FileStream stream = File.OpenRead(file))
                using (Texture2D image = Texture2D.FromStream(GraphicsDevice, stream))
                    difference = Difference(render, image);

                bool mismatch = difference > MismatchLimit;
                if (mismatch)
                    ExitCode = 1;
                Console.WriteLine($"{block.Name,-6} {difference,6:F1}{(mismatch ? "  the image shows another block" : "")}");
            }
        }

        private Texture2D RenderBlock(string block)
        {
            Matrix rotation = Matrix.CreateRotationZ(Roll) * Matrix.CreateRotationX(Pitch) * Matrix.CreateRotationY(Yaw);
            Matrix view = Matrix.CreateLookAt(CameraPosition, CameraPosition + Vector3.Transform(-Vector3.UnitZ, rotation),
                Vector3.Transform(Vector3.UnitY, rotation));
            //A perspective projection whose centre is not in the middle of the image
            const float near = 0.1f;
            Matrix projection = Matrix.CreatePerspectiveOffCenter(-CenterX / FocalLength * near, (Width - CenterX) / FocalLength * near,
                -(Height - CenterY) / FocalLength * near, CenterY / FocalLength * near, near, 500f);

            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.Clear(Color.Gray);
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            Model model;
            try
            {
                model = Content.Load<Model>("Levels/mod/" + block);
            }
            catch (ContentLoadException)
            {
                throw new ArgumentException($"Block \"{block}\" has no model (content/Levels/mod/{block}.xnb).");
            }
            DrawModel(model, view, projection);
            DrawModel(selector, view, projection);
            GraphicsDevice.SetRenderTarget(null);

            Color[] pixels = new Color[Width * Height];
            target.GetData(pixels);
            Texture2D image = new Texture2D(GraphicsDevice, Width, Height);
            image.SetData(pixels);
            return image;
        }

        /// <summary>Draws a model at the block position the way the level editor does (GameObject.DrawModel).</summary>
        private static void DrawModel(Model model, Matrix view, Matrix projection)
        {
            Matrix world = Matrix.CreateScale(Units.ModelToScene);
            Matrix[] transforms = new Matrix[model.Bones.Count];
            model.CopyAbsoluteBoneTransformsTo(transforms);

            foreach (ModelMesh mesh in model.Meshes)
            {
                foreach (BasicEffect effect in mesh.Effects)
                {
                    Lighting.ApplyClassic(effect);
                    effect.View = view;
                    effect.Projection = projection;
                    effect.World = transforms[mesh.ParentBone.Index] * world;
                }
                mesh.Draw();
            }
        }

        /// <summary>
        /// RMS of the colour channels. Some catalog images are a pixel smaller; the render is compared with
        /// each corner of them and the best match counts.
        /// </summary>
        private static double Difference(Texture2D render, Texture2D image)
        {
            Color[] a = new Color[render.Width * render.Height];
            Color[] b = new Color[image.Width * image.Height];
            render.GetData(a);
            image.GetData(b);

            double best = double.MaxValue;
            foreach (int dx in new[] { 0, image.Width - render.Width })
            {
                foreach (int dy in new[] { 0, image.Height - render.Height })
                {
                    //Pixel (x, y) of the render is compared with pixel (x + dx, y + dy) of the image
                    int left = Math.Max(0, -dx), top = Math.Max(0, -dy);
                    int right = Math.Min(render.Width, image.Width - dx), bottom = Math.Min(render.Height, image.Height - dy);
                    double sum = 0;
                    for (int y = top; y < bottom; y++)
                    {
                        for (int x = left; x < right; x++)
                        {
                            Color p = a[y * render.Width + x], q = b[(y + dy) * image.Width + x + dx];
                            sum += Square(p.R - q.R) + Square(p.G - q.G) + Square(p.B - q.B);
                        }
                    }
                    best = Math.Min(best, Math.Sqrt(sum / (3.0 * (right - left) * (bottom - top))));
                }
            }
            return best;
        }

        private static double Square(int value) => (double)value * value;

        /// <summary>The repository root: the first folder up from the program that contains TW3D.slnx.</summary>
        private static string FindRepository()
        {
            for (DirectoryInfo folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
                if (File.Exists(Path.Combine(folder.FullName, "TW3D.slnx")))
                    return folder.FullName;
            throw new DirectoryNotFoundException("The repository (TW3D.slnx) was not found above " + AppContext.BaseDirectory);
        }

        private static int Main(string[] args)
        {
            if (args.Length == 0 || (args[0] != "render" && args[0] != "check"))
            {
                Console.WriteLine("Usage: CatalogRender render [--out folder] block...");
                Console.WriteLine("       CatalogRender check");
                return 2;
            }

            using (CatalogRenderer renderer = new CatalogRenderer(args[0], args.Skip(1).ToArray(), FindRepository()))
            {
                renderer.Run();
                return renderer.ExitCode;
            }
        }
    }
}
