using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using TW3D.Graphics;
using TW3D.Shared;

namespace TW3D.Editor
{
    /// <summary>
    /// The helpers drawn over the scene of the editor, after the renderer has drawn the level, with the depth buffer on
    /// so that they sit in the scene: the selector of 2016 (Levels/selector, a green wire cube) at the cursor cell on
    /// the plane of the active layer and a thin grid of a few cells around it so that the cells are seen (while the
    /// Place tool is active), tinted copies of the selector marking the finish block and the start cell [0, 0] of the
    /// middle layer, and tinted copies scaled around the bounding spheres of the hovered and the selected dynamic
    /// object (while the Object tool is active). The selector is drawn with BasicEffect and the lighting of 2016, as
    /// the old editor drew it.
    /// </summary>
    sealed class EditorMarkers
    {
        private const string SelectorModel = "Levels/selector";
        //How many cells of the grid are drawn around the cursor on each side, and the colour of its lines (premultiplied)
        private const int GridRadius = 3;
        private static readonly Color GridColor = new Color(255, 255, 230) * 0.45f;
        //The markers are drawn a little smaller than the selector, so that both are seen when the cursor is on a marked cell
        private const float MarkerScale = 0.9f;
        private static readonly Vector3 FinishTint = new Vector3(1f, 0.85f, 0f);
        private static readonly Vector3 StartTint = new Vector3(0.2f, 0.6f, 1f);
        //The hovered and the selected object; the hovered one is marked a little larger, so that both marks are seen
        //on an object that is both
        private static readonly Vector3 HoverTint = new Vector3(0.85f, 0.85f, 0.85f);
        private static readonly Vector3 SelectedTint = new Vector3(1f, 0.3f, 0.75f);
        private const float HoverScale = 1.12f;
        //Markers glow a little, so that they are seen in the night too
        private const float MarkerGlow = 0.35f;
        private static readonly Cell StartCell = new Cell(0, 0);

        private readonly GraphicsDevice device;
        private readonly Model selector;
        private readonly Matrix[] boneTransforms;
        //The colours of the materials of the selector, put back after a tinted draw (the effects are shared)
        private readonly Vector3[] diffuse, emissive;
        private readonly BasicEffect lineEffect;
        //The lines of the grid around the origin: moved to the cursor by the world matrix
        private readonly VertexPositionColor[] grid;
        private readonly int gridLines;

        public EditorMarkers(GraphicsDevice device, ContentManager content)
        {
            this.device = device;
            selector = content.Load<Model>(SelectorModel);
            boneTransforms = new Matrix[selector.Bones.Count];
            selector.CopyAbsoluteBoneTransformsTo(boneTransforms);

            int effects = 0;
            foreach (ModelMesh mesh in selector.Meshes)
                effects += mesh.Effects.Count;
            diffuse = new Vector3[effects];
            emissive = new Vector3[effects];
            int index = 0;
            foreach (ModelMesh mesh in selector.Meshes)
            {
                foreach (BasicEffect effect in mesh.Effects)
                {
                    diffuse[index] = effect.DiffuseColor;
                    emissive[index] = effect.EmissiveColor;
                    index++;
                }
            }

            lineEffect = new BasicEffect(device) { VertexColorEnabled = true, LightingEnabled = false, TextureEnabled = false };
            gridLines = (GridRadius * 2 + 2) * 2;
            grid = new VertexPositionColor[gridLines * 2];
            float cell = LevelView.CellSize;
            float half = (GridRadius + 0.5f) * cell;
            index = 0;
            for (int i = -GridRadius; i <= GridRadius + 1; i++)
            {
                float offset = (i - 0.5f) * cell;
                grid[index++] = new VertexPositionColor(new Vector3(offset, -half, 0f), GridColor);
                grid[index++] = new VertexPositionColor(new Vector3(offset, half, 0f), GridColor);
                grid[index++] = new VertexPositionColor(new Vector3(-half, offset, 0f), GridColor);
                grid[index++] = new VertexPositionColor(new Vector3(half, offset, 0f), GridColor);
            }
        }

        /// <summary>Draws the markers of the finish and the start, and the grid and the selector at the cursor cell of a layer.</summary>
        /// <param name="layer">The layer of the cursor: the active layer for the Place tool, the middle one for the Object tool.</param>
        /// <param name="cursor">The cursor cell; null for no grid and no selector.</param>
        /// <param name="finish">The finish block of the level; null when it has none.</param>
        public void Draw(Matrix view, Matrix projection, Layer layer, Cell? cursor, PlacedBlock finish)
        {
            if (cursor.HasValue)
                DrawGrid(view, projection, layer, cursor.Value);

            SetSolidStates();
            if (finish != null)
                DrawSelector(view, projection, LevelView.ScenePosition(Layer.Middle, finish.Cell), MarkerScale, FinishTint);
            DrawSelector(view, projection, LevelView.ScenePosition(Layer.Middle, StartCell), MarkerScale, StartTint);
            if (cursor.HasValue)
                DrawSelector(view, projection, LevelView.ScenePosition(layer, cursor.Value), 1f, null);
        }

        /// <summary>Marks the hovered and the selected dynamic object by selectors as large as their bounding spheres.</summary>
        /// <param name="hovered">The bounding sphere of the hovered object in the scene; null for none.</param>
        /// <param name="selected">The bounding sphere of the selected object in the scene; null for none.</param>
        public void DrawObjects(Matrix view, Matrix projection, BoundingSphere? hovered, BoundingSphere? selected)
        {
            if (!hovered.HasValue && !selected.HasValue)
                return;
            SetSolidStates();
            //The selector is a cube as large as a cell: scaled so that its side is the diameter of the sphere
            if (selected.HasValue)
                DrawSelector(view, projection, selected.Value.Center, selected.Value.Radius * 2f / LevelView.CellSize, SelectedTint);
            if (hovered.HasValue)
                DrawSelector(view, projection, hovered.Value.Center, hovered.Value.Radius * 2f / LevelView.CellSize * HoverScale, HoverTint);
        }

        private void SetSolidStates()
        {
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullCounterClockwise;
            device.SamplerStates[0] = SamplerState.LinearWrap;
        }

        /// <summary>Unlit lines on the plane of the layer, mixed over the scene, not hiding what is behind them.</summary>
        private void DrawGrid(Matrix view, Matrix projection, Layer layer, Cell cursor)
        {
            device.BlendState = BlendState.AlphaBlend;
            device.DepthStencilState = DepthStencilState.DepthRead;
            device.RasterizerState = RasterizerState.CullNone;
            lineEffect.World = Matrix.CreateTranslation(LevelView.ScenePosition(layer, cursor));
            lineEffect.View = view;
            lineEffect.Projection = projection;
            foreach (EffectPass pass in lineEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.LineList, grid, 0, gridLines);
            }
        }

        /// <param name="tint">The colour the selector is drawn in instead of its own; null for its own.</param>
        private void DrawSelector(Matrix view, Matrix projection, Vector3 position, float scale, Vector3? tint)
        {
            Matrix world = Matrix.CreateScale(Units.ModelToScene * scale) * Matrix.CreateTranslation(position);
            int index = 0;
            foreach (ModelMesh mesh in selector.Meshes)
            {
                foreach (BasicEffect effect in mesh.Effects)
                {
                    Lighting.ApplyClassic(effect);
                    effect.View = view;
                    effect.Projection = projection;
                    effect.World = boneTransforms[mesh.ParentBone.Index] * world;
                    if (tint.HasValue)
                    {
                        effect.DiffuseColor = tint.Value;
                        effect.EmissiveColor = tint.Value * MarkerGlow;
                    }
                    else
                    {
                        effect.DiffuseColor = diffuse[index];
                        effect.EmissiveColor = emissive[index];
                    }
                    index++;
                }
                mesh.Draw();
            }
        }
    }
}
