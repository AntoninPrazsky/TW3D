using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TW3D.GameObjects;
using TW3D.Graphics;
using TW3D.Shared;

namespace TW3D.Editor
{
    /// <summary>
    /// The turning 3D previews of the palette: every block of the catalog and every model of the dynamic objects, each
    /// drawn in a cell of one render target (the atlas) on a turntable, seen from the angle of the catalog images (a
    /// little from above and from the front-left, see tools/CatalogRender) at a distance that fits its bounding sphere
    /// in the cell, in the lighting of 2016 on a dark grey background. The atlas is bound to ImGui as one texture; the
    /// palette shows a cell by its UV rectangle (<see cref="CellUv"/>). Drawn at the very start of a frame, before the
    /// scene: switching render targets may discard the screen on DesktopGL. All the previews turn in phase; the hovered
    /// one turns faster and falls back into phase soon after the mouse leaves it.
    /// </summary>
    sealed class BlockPreviews : IDisposable
    {
        /// <summary>The size of a cell in pixels on a 1920 × 1080 window, scaled with the window.</summary>
        public const float ReferenceCellSize = 96f;
        //One turn of the turntable takes this long (seconds), and the hovered preview turns this many times faster
        private const float TurnTime = 8f;
        private const float HoverSpeed = 3f;
        //A preview the mouse has left falls back into phase this fast (the time constant of the easing, seconds), so
        //that a sweep across the palette does not leave a trail of fast previews behind
        private const float RephaseTime = 0.12f;
        //The camera: its field of view and how much of the cell the bounding sphere of a model takes; a model never
        //fills its sphere, so the sphere may reach a little beyond the cell
        private static readonly float FieldOfView = MathHelper.ToRadians(40f);
        private const float Fit = 0.92f;
        //Where the camera is relative to the middle of a block: the direction of the camera of the catalog images (see
        //CatalogRender, in the coordinates of the scene: the camera is on the -z side, as the camera of the editor)
        private static readonly Vector3 CameraDirection = Vector3.Normalize(new Vector3(1.4236505f, 1.5330524f, -3.9857758f));
        //A dark grey a little lighter than the windows of ImGui (their background is 0.06), so that the cells read as tiles
        private static readonly Color Background = new Color(38, 38, 42);
        private const int MultiSampleCount = 4;

        /// <summary>A model in its cell: its bone transforms and bounding sphere found once.</summary>
        private sealed class Preview
        {
            public Model Model;
            public Matrix[] BoneTransforms;
            //In the units of the model, with the bone transforms applied
            public BoundingSphere Bounds;
        }

        private readonly GraphicsDevice device;
        private readonly ImGuiRenderer imgui;
        private readonly List<Preview> previews = new List<Preview>();
        private readonly Dictionary<string, int> blockIndices = new Dictionary<string, int>();
        private readonly Dictionary<string, int> objectModelIndices = new Dictionary<string, int>();
        //How far ahead of the others a preview has turned (radians, the hovered one turns faster); 0 when in phase
        private readonly float[] extraAngles;
        //The grid of the atlas: as square as possible
        private readonly int columns, rows;
        private RenderTarget2D atlas;
        private IntPtr textureId;
        private int cellSize;
        private float angle;

        public BlockPreviews(GraphicsDevice device, LevelAssets assets, ImGuiRenderer imgui)
        {
            this.device = device;
            this.imgui = imgui;
            foreach (BlockType block in assets.Catalog.Blocks)
            {
                blockIndices.Add(block.Name, previews.Count);
                previews.Add(Describe(assets.BlockModel(block)));
            }
            foreach (string model in assets.Catalog.ObjectModels)
            {
                objectModelIndices.Add(model, previews.Count);
                previews.Add(Describe(assets.ObjectModel(model)));
            }
            extraAngles = new float[previews.Count];
            columns = (int)Math.Ceiling(Math.Sqrt(previews.Count));
            rows = (previews.Count + columns - 1) / columns;
        }

        private static Preview Describe(Model model)
        {
            Preview preview = new Preview { Model = model, BoneTransforms = new Matrix[model.Bones.Count] };
            model.CopyAbsoluteBoneTransformsTo(preview.BoneTransforms);
            bool first = true;
            foreach (ModelMesh mesh in model.Meshes)
            {
                BoundingSphere bounds = mesh.BoundingSphere.Transform(preview.BoneTransforms[mesh.ParentBone.Index]);
                preview.Bounds = first ? bounds : BoundingSphere.CreateMerged(preview.Bounds, bounds);
                first = false;
            }
            if (preview.Bounds.Radius <= 0f)
                preview.Bounds.Radius = Units.BlockModelSize / 2f;
            return preview;
        }

        /// <summary>The id of the atlas for ImGui.Image and ImGui.ImageButton; valid after the first <see cref="Draw"/>.</summary>
        public IntPtr TextureId => textureId;

        /// <summary>The preview the mouse is over (it turns faster); -1 for none. Set by the palette during its layout.</summary>
        public int Hovered { get; set; } = -1;

        /// <summary>The index of the preview of a block of the catalog; -1 for an unknown block.</summary>
        public int BlockIndex(string block) => blockIndices.TryGetValue(block, out int index) ? index : -1;

        /// <summary>The index of the preview of a model of the dynamic objects; -1 for an unknown model.</summary>
        public int ObjectModelIndex(string model) => objectModelIndices.TryGetValue(model, out int index) ? index : -1;

        /// <summary>The corners of the cell of a preview in the atlas, as ImGui takes them (0 to 1, (0, 0) at the top left).</summary>
        public (System.Numerics.Vector2 Min, System.Numerics.Vector2 Max) CellUv(int index)
        {
            float column = index % columns, row = index / columns;
            return (new System.Numerics.Vector2(column / columns, row / rows), new System.Numerics.Vector2((column + 1) / columns, (row + 1) / rows));
        }

        /// <summary>
        /// Draws every preview into its cell of the atlas, turned on by the time since the last frame. Called at the start
        /// of the frame, before anything is drawn on the screen; the atlas is rebuilt when the size of a cell changes.
        /// </summary>
        /// <param name="cellSize">The size of a cell in pixels.</param>
        /// <param name="elapsed">Seconds since the last frame.</param>
        public void Draw(int cellSize, float elapsed)
        {
            if (cellSize != this.cellSize)
                Rebuild(cellSize);
            Turn(elapsed);

            device.SetRenderTarget(atlas);
            device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Background, 1f, 0);
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullCounterClockwise;
            device.SamplerStates[0] = SamplerState.LinearWrap;
            for (int i = 0; i < previews.Count; i++)
            {
                device.Viewport = new Viewport(i % columns * cellSize, i / columns * cellSize, cellSize, cellSize);
                DrawPreview(previews[i], angle + extraAngles[i]);
            }
            device.SetRenderTarget(null);
        }

        /// <summary>
        /// The turntable goes on: all the previews together, the hovered one faster; one the mouse has left eases back
        /// into phase by the shorter way round (forward or back) instead of finishing a whole extra turn.
        /// </summary>
        private void Turn(float elapsed)
        {
            float step = elapsed * MathHelper.TwoPi / TurnTime;
            angle = (angle + step) % MathHelper.TwoPi;
            float ease = MathF.Exp(-elapsed / RephaseTime);
            for (int i = 0; i < extraAngles.Length; i++)
            {
                if (i == Hovered)
                {
                    extraAngles[i] = (extraAngles[i] + step * (HoverSpeed - 1f)) % MathHelper.TwoPi;
                    continue;
                }
                if (extraAngles[i] == 0f)
                    continue;
                float target = extraAngles[i] < MathHelper.Pi ? 0f : MathHelper.TwoPi;
                extraAngles[i] = target + (extraAngles[i] - target) * ease;
                //Close enough to look in phase: a whole turn ahead is the same as none
                if (Math.Abs(extraAngles[i] - target) < 0.001f)
                    extraAngles[i] = 0f;
            }
        }

        /// <summary>The model turned around the vertical axis through the middle of its bounding sphere, the camera fitting the sphere.</summary>
        private void DrawPreview(Preview preview, float turn)
        {
            float radius = preview.Bounds.Radius;
            float distance = radius / MathF.Sin(FieldOfView / 2f) * Fit;
            Matrix world = Matrix.CreateTranslation(-preview.Bounds.Center) * Matrix.CreateRotationY(turn);
            Matrix view = Matrix.CreateLookAt(CameraDirection * distance, Vector3.Zero, Vector3.Up);
            Matrix projection = Matrix.CreatePerspectiveFieldOfView(FieldOfView, 1f, (distance - radius) / 2f, distance + radius * 2f);
            foreach (ModelMesh mesh in preview.Model.Meshes)
            {
                foreach (BasicEffect effect in mesh.Effects)
                {
                    Lighting.ApplyClassic(effect);
                    effect.World = preview.BoneTransforms[mesh.ParentBone.Index] * world;
                    effect.View = view;
                    effect.Projection = projection;
                }
                mesh.Draw();
            }
        }

        /// <summary>
        /// A new atlas for a size of the cells, bound to ImGui in place of the old one. Multisampled where the device
        /// allows it: MonoGame clamps the count asked for to what the device supports (none at all when it has no
        /// multisampling) instead of refusing it, so there is no fallback to make here.
        /// </summary>
        private void Rebuild(int cellSize)
        {
            this.cellSize = cellSize;
            Release();
            atlas = new RenderTarget2D(device, columns * cellSize, rows * cellSize, false, SurfaceFormat.Color, DepthFormat.Depth24,
                MultiSampleCount, RenderTargetUsage.DiscardContents);
            textureId = imgui.BindTexture(atlas);
        }

        private void Release()
        {
            if (atlas == null)
                return;
            imgui.UnbindTexture(textureId);
            atlas.Dispose();
            atlas = null;
        }

        public void Dispose()
        {
            Release();
        }
    }
}
