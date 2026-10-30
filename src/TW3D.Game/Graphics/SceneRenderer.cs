using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TW3D.Graphics
{
    /// <summary>
    /// Draws the 3D scene of one frame: the models added between <see cref="Begin"/> and <see cref="End"/>,
    /// lit by the scene shader (Effects/Scene.fx), and the sky behind them.
    /// </summary>
    /// <remarks>
    /// <see cref="Classic"/> switches to the lighting of the 2016 version (BasicEffect with three
    /// directional lights), to compare the two.
    /// </remarks>
    public sealed class SceneRenderer
    {
        //The light of the scene, in linear colours. The sun shines from above and from the left,
        //a little from the side of the camera: the faces turned to the camera are lit as well,
        //and the shadows fall to the right onto the track.
        private static readonly Vector3 SunDirection = Vector3.Normalize(new Vector3(0.65f, 1f, -0.4f));
        private static readonly Vector3 SunColor = new Vector3(1f, 0.93f, 0.82f) * 2.3f;
        private static readonly Vector3 SkyLight = new Vector3(0.5f, 0.68f, 0.9f) * 0.55f;
        private static readonly Vector3 GroundLight = new Vector3(0.32f, 0.28f, 0.24f) * 0.35f;
        private static readonly Vector3 RimLight = new Vector3(0.55f, 0.7f, 0.9f) * 0.35f;
        private const float Exposure = 1f;
        //Strength of the sun's highlight relative to the specular colour of a material
        private const float SpecularStrength = 0.6f;

        //The distance fades into the colour of the sky at the horizon (on the screen, not linear)
        private static readonly Vector3 HazeColor = new Color(176, 235, 255).ToVector3();
        private const float HazeStart = 8f;
        private const float HazeEnd = 60f;
        private const float HazeMax = 0.55f;

        //The shadow map covers the part of the scene the camera sees: the 3D models lie between
        //these planes (the back and front decoration layers are 2.25 units from the physics plane
        //z = 0, the blocks are 2.25 units deep). It reaches this far towards the sun beyond it,
        //for the shadows of objects above the screen.
        private const int ShadowMapSize = 2048;
        private const float SceneNearZ = -3.5f;
        private const float SceneFarZ = 3.5f;
        private const float ShadowCasterReach = 10f;
        //Against shadow acne: the lookup moves this many shadow map texels along the normal,
        //and the depth is compared with this tolerance (in world units)
        private const float NormalOffsetTexels = 1.5f;
        private const float ShadowBiasWorld = 0.02f;

        /// <summary>A part of a model with one material, ready to draw.</summary>
        private sealed class ModelPart
        {
            public Matrix Transform;
            public ModelMeshPart MeshPart;
            public BasicEffect Effect;
            public Vector3 Diffuse;
            public Vector3 Emissive;
            public float Specular;
        }

        /// <summary>The parts and bounds of a model, found once per model.</summary>
        private sealed class ModelData
        {
            public ModelPart[] Parts;
            public BoundingSphere Bounds;
        }

        private readonly struct DrawItem
        {
            public DrawItem(ModelData model, Matrix world)
            {
                Model = model;
                World = world;
            }

            public ModelData Model { get; }
            public Matrix World { get; }
        }

        private readonly GraphicsDevice device;
        private readonly Effect effect;
        private readonly EffectTechnique litTechnique, depthTechnique;
        private readonly EffectParameter worldParameter, diffuseParameter, emissiveParameter, specularParameter;
        private readonly RenderTarget2D shadowMap;
        private readonly BoundingFrustum lightFrustum = new BoundingFrustum(Matrix.Identity);
        private readonly Vector3[] frustumCorners = new Vector3[8];
        private readonly Vector3[] sceneCorners = new Vector3[8];
        private readonly Dictionary<Model, ModelData> models = new Dictionary<Model, ModelData>();
        private readonly List<DrawItem> items = new List<DrawItem>();

        private ChaseCamera camera;
        private Model background;
        private Matrix backgroundWorld;

        public SceneRenderer(GraphicsDevice device, ContentManager content)
        {
            this.device = device;
            effect = content.Load<Effect>("Effects/Scene");
            litTechnique = effect.Techniques["Lit"];
            depthTechnique = effect.Techniques["Depth"];
            worldParameter = effect.Parameters["World"];
            diffuseParameter = effect.Parameters["DiffuseColor"];
            emissiveParameter = effect.Parameters["EmissiveColor"];
            specularParameter = effect.Parameters["Specular"];

            effect.Parameters["SunDirection"].SetValue(SunDirection);
            effect.Parameters["SunColor"].SetValue(SunColor);
            effect.Parameters["SkyLight"].SetValue(SkyLight);
            effect.Parameters["GroundLight"].SetValue(GroundLight);
            effect.Parameters["RimLight"].SetValue(RimLight);
            effect.Parameters["Exposure"].SetValue(Exposure);
            effect.Parameters["HazeColor"].SetValue(HazeColor);
            effect.Parameters["Haze"].SetValue(new Vector3(HazeStart, 1f / (HazeEnd - HazeStart), HazeMax));

            shadowMap = new RenderTarget2D(device, ShadowMapSize, ShadowMapSize, false, SurfaceFormat.Color, DepthFormat.Depth24);
            effect.Parameters["ShadowTexel"].SetValue(1f / ShadowMapSize);
        }

        /// <summary>Draw with the lighting of the 2016 version.</summary>
        public bool Classic { get; set; }

        /// <summary>The sun casts shadows.</summary>
        public bool Shadows { get; set; } = true;

        /// <summary>Starts a frame seen by the camera.</summary>
        public void Begin(ChaseCamera camera)
        {
            this.camera = camera;
            items.Clear();
            background = null;
        }

        /// <summary>Adds a model to the scene.</summary>
        public void Add(Model model, Matrix world)
        {
            items.Add(new DrawItem(GetModelData(model), world));
        }

        /// <summary>
        /// Sets the model drawn behind everything else, unlit and without the depth buffer (the sky),
        /// so it never hides the scene.
        /// </summary>
        public void SetBackground(Model model, Matrix world)
        {
            background = model;
            backgroundWorld = world;
        }

        /// <summary>Draws the frame.</summary>
        public void End()
        {
            if (!Classic && Shadows)
            {
                DrawShadowMap();
                //Switching render targets may discard the screen
                device.Clear(Color.Black);
            }
            effect.Parameters["ShadowStrength"].SetValue(Shadows ? 1f : 0f);

            device.BlendState = BlendState.Opaque;
            device.RasterizerState = RasterizerState.CullCounterClockwise;

            if (background != null)
            {
                device.DepthStencilState = DepthStencilState.None;
                device.SamplerStates[0] = SamplerState.LinearWrap;
                background.Draw(backgroundWorld, camera.View, camera.Projection);
            }

            device.DepthStencilState = DepthStencilState.Default;
            if (Classic)
                DrawClassic();
            else
                DrawLit();
        }

        /// <summary>Draws the depth of the scene seen from the sun into the shadow map.</summary>
        private void DrawShadowMap()
        {
            UpdateLight();

            device.SetRenderTarget(shadowMap);
            device.Clear(Color.White);
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullNone;

            effect.CurrentTechnique = depthTechnique;
            EffectPass pass = depthTechnique.Passes[0];
            foreach (DrawItem item in items)
            {
                if (lightFrustum.Contains(item.Model.Bounds.Transform(item.World)) == ContainmentType.Disjoint)
                    continue;

                foreach (ModelPart part in item.Model.Parts)
                {
                    worldParameter.SetValue(part.Transform * item.World);
                    pass.Apply();
                    DrawPart(part.MeshPart);
                }
            }

            device.SetRenderTarget(null);
        }

        /// <summary>
        /// Fits the view of the sun to the part of the scene the camera sees, so the shadow map
        /// is used as fully as possible.
        /// </summary>
        private void UpdateLight()
        {
            //Where the edges of the camera view cross the planes the scene lies between
            camera.Frustum.GetCorners(frustumCorners);
            for (int i = 0; i < 4; i++)
            {
                Vector3 near = frustumCorners[i];
                Vector3 direction = frustumCorners[i + 4] - near;
                sceneCorners[i] = near + direction * MathHelper.Clamp((SceneNearZ - near.Z) / direction.Z, 0f, 1f);
                sceneCorners[i + 4] = near + direction * MathHelper.Clamp((SceneFarZ - near.Z) / direction.Z, 0f, 1f);
            }

            Vector3 center = Vector3.Zero;
            foreach (Vector3 corner in sceneCorners)
                center += corner / sceneCorners.Length;
            float radius = 0f;
            foreach (Vector3 corner in sceneCorners)
                radius = Math.Max(radius, Vector3.Distance(center, corner));
            //In steps, so the size of a texel does not change with every turn of the camera
            radius = MathF.Ceiling(radius * 2f) / 2f;

            //The sun looks at the sphere around them. Its position is rounded to whole texels of the
            //shadow map, so the shadows do not flicker when the camera moves.
            Matrix lightView = Matrix.CreateLookAt(Vector3.Zero, -SunDirection, Vector3.Up);
            Vector3 lightCenter = Vector3.Transform(center, lightView);
            float texel = 2f * radius / ShadowMapSize;
            lightCenter.X = MathF.Round(lightCenter.X / texel) * texel;
            lightCenter.Y = MathF.Round(lightCenter.Y / texel) * texel;
            float distance = -lightCenter.Z;
            Matrix lightProjection = Matrix.CreateOrthographicOffCenter(lightCenter.X - radius, lightCenter.X + radius,
                lightCenter.Y - radius, lightCenter.Y + radius, distance - radius - ShadowCasterReach, distance + radius);

            Matrix lightViewProjection = lightView * lightProjection;
            lightFrustum.Matrix = lightViewProjection;
            effect.Parameters["LightViewProjection"].SetValue(lightViewProjection);
            effect.Parameters["NormalOffset"].SetValue(NormalOffsetTexels * texel);
            effect.Parameters["ShadowBias"].SetValue(ShadowBiasWorld / (2f * radius + ShadowCasterReach));
        }

        private void DrawLit()
        {
            effect.Parameters["ViewProjection"].SetValue(camera.View * camera.Projection);
            effect.Parameters["CameraPosition"].SetValue(camera.Eye);
            effect.Parameters["ShadowMap"].SetValue(shadowMap);
            effect.CurrentTechnique = litTechnique;
            EffectPass pass = litTechnique.Passes[0];

            foreach (DrawItem item in items)
            {
                if (!IsVisible(item))
                    continue;

                foreach (ModelPart part in item.Model.Parts)
                {
                    worldParameter.SetValue(part.Transform * item.World);
                    diffuseParameter.SetValue(part.Diffuse);
                    emissiveParameter.SetValue(part.Emissive);
                    specularParameter.SetValue(part.Specular);
                    pass.Apply();
                    DrawPart(part.MeshPart);
                }
            }
        }

        /// <summary>The lighting of the 2016 version: BasicEffect with three directional lights.</summary>
        private void DrawClassic()
        {
            foreach (DrawItem item in items)
            {
                if (!IsVisible(item))
                    continue;

                foreach (ModelPart part in item.Model.Parts)
                {
                    BasicEffect basic = part.Effect;
                    basic.PreferPerPixelLighting = true;
                    basic.LightingEnabled = true;
                    basic.AmbientLightColor = new Vector3(0.2f, 0.2f, 0.2f);
                    basic.SpecularColor = new Vector3(1, 1, 1);
                    basic.SpecularPower = 30;

                    basic.DirectionalLight0.Direction = new Vector3(1, -1, 0);
                    basic.DirectionalLight0.DiffuseColor = new Vector3(0.6f, 0.6f, 0.6f);
                    basic.DirectionalLight0.SpecularColor = new Vector3(1, 1, 1);

                    basic.DirectionalLight1.Direction = new Vector3(0, -1, -1);
                    basic.DirectionalLight1.DiffuseColor = new Color(90, 166, 206).ToVector3() / 2;
                    basic.DirectionalLight1.SpecularColor = new Color(90, 166, 206).ToVector3();
                    basic.DirectionalLight1.Enabled = true;

                    basic.DirectionalLight2.Direction = new Vector3(-1, -1, 0);
                    basic.DirectionalLight2.DiffuseColor = new Color(74, 125, 156).ToVector3() / 2;
                    basic.DirectionalLight2.SpecularColor = new Color(74, 125, 156).ToVector3();
                    basic.DirectionalLight2.Enabled = true;

                    basic.View = camera.View;
                    basic.Projection = camera.Projection;
                    basic.World = part.Transform * item.World;
                    basic.CurrentTechnique.Passes[0].Apply();
                    DrawPart(part.MeshPart);
                }
            }
        }

        /// <summary>Models whose bounding sphere is completely outside the camera view are not drawn.</summary>
        private bool IsVisible(DrawItem item)
        {
            return camera.Frustum.Contains(item.Model.Bounds.Transform(item.World)) != ContainmentType.Disjoint;
        }

        private void DrawPart(ModelMeshPart part)
        {
            device.SetVertexBuffer(part.VertexBuffer);
            device.Indices = part.IndexBuffer;
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, part.VertexOffset, part.StartIndex, part.PrimitiveCount);
        }

        private ModelData GetModelData(Model model)
        {
            if (models.TryGetValue(model, out ModelData data))
                return data;

            Matrix[] transforms = new Matrix[model.Bones.Count];
            model.CopyAbsoluteBoneTransformsTo(transforms);

            List<ModelPart> parts = new List<ModelPart>();
            BoundingSphere bounds = new BoundingSphere();
            bool first = true;
            foreach (ModelMesh mesh in model.Meshes)
            {
                Matrix transform = transforms[mesh.ParentBone.Index];
                BoundingSphere meshBounds = mesh.BoundingSphere.Transform(transform);
                bounds = first ? meshBounds : BoundingSphere.CreateMerged(bounds, meshBounds);
                first = false;

                foreach (ModelMeshPart meshPart in mesh.MeshParts)
                {
                    BasicEffect material = (BasicEffect)meshPart.Effect;
                    Vector3 specular = material.SpecularColor;
                    parts.Add(new ModelPart
                    {
                        Transform = transform,
                        MeshPart = meshPart,
                        Effect = material,
                        //The colours of the models were picked on the screen, the light works with linear ones
                        Diffuse = ToLinear(material.DiffuseColor),
                        Emissive = ToLinear(material.EmissiveColor),
                        Specular = (specular.X + specular.Y + specular.Z) / 3f * SpecularStrength
                    });
                }
            }

            data = new ModelData { Parts = parts.ToArray(), Bounds = bounds };
            models.Add(model, data);
            return data;
        }

        private static Vector3 ToLinear(Vector3 color)
        {
            return new Vector3(MathF.Pow(color.X, 2.2f), MathF.Pow(color.Y, 2.2f), MathF.Pow(color.Z, 2.2f));
        }
    }
}
