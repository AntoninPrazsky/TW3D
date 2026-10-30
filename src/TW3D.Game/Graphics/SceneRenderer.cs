using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TW3D.Graphics
{
    /// <summary>The lamps of a car among the materials of its model (see <see cref="SceneRenderer.LampOf"/>).</summary>
    public enum Lamp
    {
        None,
        Headlight,
        RearLight
    }

    /// <summary>
    /// Draws the 3D scene of one frame: the models added between <see cref="Begin"/> and <see cref="End"/>,
    /// lit by the scene shader (Effects/Scene.fx), and the sky behind them. Used by the game and the level editor.
    /// </summary>
    /// <remarks>
    /// <see cref="Classic"/> switches to the lighting of the 2016 version (BasicEffect with three
    /// directional lights). Transparent materials (glass) are drawn after the others, from the back to the front;
    /// they cast no shadows and do not count for the ambient occlusion. Metal and smooth materials (the metallic
    /// and the roughness of a material, "Pm" and "Pr" in its MTL file, kept by the content pipeline in the Tag of
    /// its mesh parts) mirror the sky, the ground and the sun. The lamps of a car (materials named "headlight…" and
    /// "taillight…") shine in their emissive colour ("Ke") only when they are on (<see cref="SetCarLights"/>), with
    /// a glow around them; each lamp is a mesh of its own, the glow is at its middle.
    /// </remarks>
    public sealed class SceneRenderer
    {
        //Strength of the sun's highlight relative to the specular colour of a material
        private const float SpecularStrength = 0.6f;
        //What smooth materials mirror: the sky overhead in the colour of its light, the bright horizon in the colour
        //of the haze, and the ground below in the colour of its light, at these brightnesses
        private const float MirroredSkyLight = 2f;
        private const float MirroredHorizon = 1.5f;
        private const float MirroredGroundLight = 1.5f;
        //The lamps of the car shine in their emissive colour this many times brighter (the new look); the rear lights
        //shine this much with the headlights, fully when the car brakes
        private const float LampBrightness = 4f;
        private const float RearLightDim = 0.3f;
        //The glow around a shining lamp: its radius (world units) for a headlight and a rear light, its strength in
        //the dark and in daylight. It is moved this far towards the camera, so that the wheels of the car do not
        //hide it (the wheels at its sides are about 0.55 from the middle of the car)
        private const float HeadlightGlowRadius = 0.3f;
        private const float RearLightGlowRadius = 0.18f;
        private const float GlowStrength = 0.8f;
        private const float DaylightGlowStrength = 0.45f;
        private const float GlowPull = 0.8f;
        private const int GlowTextureSize = 64;

        //The shadow map covers the part of the scene the camera sees: the 3D models lie between
        //these planes (the back and front decoration layers are 2.25 units from the physics plane
        //z = 0, the blocks are 2.25 units deep), at most this far from the camera. It reaches
        //this far towards the sun beyond it, for the shadows of objects above the screen.
        private const int ShadowMapSize = 2048;
        private const float SceneNearZ = -3.5f;
        private const float SceneFarZ = 3.5f;
        private const float ShadowDistance = 40f;
        private const float ShadowCasterReach = 10f;
        //Against shadow acne: the lookup moves this many shadow map texels along the normal,
        //and the depth is compared with this tolerance (in world units)
        private const float NormalOffsetTexels = 1.5f;
        private const float ShadowBiasWorld = 0.02f;

        //Ambient occlusion: computed at half the resolution of the screen, up to this distance from
        //the camera, from points within the radius (world units) around each point
        private const float OcclusionDepthRange = 60f;
        private const float OcclusionRadius = 0.6f;
        private const float OcclusionIntensity = 1.6f;
        private const float OcclusionBias = 0.05f;

        //The headlights of the car: a warm spot light reaching this far (world units)
        private static readonly Vector3 HeadlightColor = new Vector3(1f, 0.88f, 0.68f) * 5f;
        private const float HeadlightInnerAngle = 16f;
        private const float HeadlightOuterAngle = 30f;
        private const float HeadlightRange = 14f;

        /// <summary>A part of a model with one material, ready to draw.</summary>
        private sealed class ModelPart
        {
            public Matrix Transform;
            public ModelMeshPart MeshPart;
            public BasicEffect Effect;
            public Texture2D Texture;
            public Vector3 Diffuse;
            public Vector3 Emissive;
            public float Specular;
            /// <summary>Opacity of the material (the "d" of the MTL file); less than 1 for glass.</summary>
            public float Alpha;
            /// <summary>1 for metal ("Pm" of the MTL file).</summary>
            public float Metallic;
            /// <summary>0 for a perfect mirror, 1 for a material mirroring nothing ("Pr" of the MTL file, 1 without it).</summary>
            public float Roughness;
            /// <summary>A lamp of the car, shining in its emissive colour only when it is on.</summary>
            public Lamp Lamp;
            /// <summary>The emissive colour of the material as it is (for the classic look, which changes it for lamps).</summary>
            public Vector3 ClassicEmissive;

            public bool IsTransparent => Alpha < 1f;
        }

        /// <summary>The parts and bounds of a model, found once per model.</summary>
        private sealed class ModelData
        {
            public ModelPart[] Parts;
            public BoundingSphere Bounds;
            public LampData[] Lamps;
        }

        /// <summary>A lamp of a car model: the middle and radius of its mesh, the colour it shines in.</summary>
        private readonly struct LampData
        {
            public LampData(Lamp lamp, BoundingSphere bounds, Vector3 colour)
            {
                Lamp = lamp;
                Bounds = bounds;
                Colour = colour;
            }

            public Lamp Lamp { get; }
            public BoundingSphere Bounds { get; }
            public Vector3 Colour { get; }
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
        private readonly Effect effect, occlusionEffect;
        private readonly EffectTechnique litTechnique, depthTechnique, normalDepthTechnique;
        private readonly EffectParameter worldParameter, diffuseParameter, emissiveParameter, specularParameter, alphaParameter,
            metallicParameter, roughnessParameter;
        private readonly RenderTarget2D shadowMap;
        private RenderTarget2D normalDepthMap, occlusionMap, blurredOcclusionMap;
        //The corners of the screen, for the passes over the whole screen
        private readonly VertexPositionTexture[] screenQuad =
        {
            new VertexPositionTexture(new Vector3(-1f, 1f, 0f), new Vector2(0f, 0f)),
            new VertexPositionTexture(new Vector3(1f, 1f, 0f), new Vector2(1f, 0f)),
            new VertexPositionTexture(new Vector3(-1f, -1f, 0f), new Vector2(0f, 1f)),
            new VertexPositionTexture(new Vector3(1f, -1f, 0f), new Vector2(1f, 1f))
        };
        private readonly BoundingFrustum frustum = new BoundingFrustum(Matrix.Identity);
        private readonly BoundingFrustum lightFrustum = new BoundingFrustum(Matrix.Identity);
        private readonly Vector3[] frustumCorners = new Vector3[8];
        private readonly Vector3[] sceneCorners = new Vector3[8];
        private readonly Dictionary<Model, ModelData> models = new Dictionary<Model, ModelData>();
        private readonly List<DrawItem> items = new List<DrawItem>();
        //The glows of the shining lamps: a soft round texture added to the screen
        private readonly BasicEffect glowEffect;
        private readonly Texture2D glowTexture;
        private readonly List<VertexPositionColorTexture> glowVertices = new List<VertexPositionColorTexture>();
        //The transparent parts of the visible models, drawn after the others
        private readonly List<(ModelPart Part, Matrix World, float Distance)> transparentParts = new List<(ModelPart, Matrix, float)>();

        private Matrix view;
        private Matrix projection;
        private Vector3 eye;
        private Model background;
        private Matrix backgroundWorld;
        private bool headlights, braking;
        private Vector3 headlightPosition, headlightDirection;

        public SceneRenderer(GraphicsDevice device, ContentManager content)
        {
            this.device = device;
            effect = content.Load<Effect>("Effects/Scene");
            litTechnique = effect.Techniques["Lit"];
            depthTechnique = effect.Techniques["Depth"];
            normalDepthTechnique = effect.Techniques["NormalDepth"];
            occlusionEffect = content.Load<Effect>("Effects/Occlusion");
            worldParameter = effect.Parameters["World"];
            diffuseParameter = effect.Parameters["DiffuseColor"];
            emissiveParameter = effect.Parameters["EmissiveColor"];
            specularParameter = effect.Parameters["Specular"];
            alphaParameter = effect.Parameters["Alpha"];
            metallicParameter = effect.Parameters["Metallic"];
            roughnessParameter = effect.Parameters["Roughness"];

            glowTexture = CreateGlowTexture(device);
            glowEffect = new BasicEffect(device) { TextureEnabled = true, VertexColorEnabled = true, Texture = glowTexture };

            shadowMap = new RenderTarget2D(device, ShadowMapSize, ShadowMapSize, false, SurfaceFormat.Color, DepthFormat.Depth24);
            effect.Parameters["ShadowTexel"].SetValue(1f / ShadowMapSize);
            effect.Parameters["OcclusionDepthRange"].SetValue(OcclusionDepthRange);

            occlusionEffect.Parameters["DepthRange"].SetValue(OcclusionDepthRange);
            occlusionEffect.Parameters["Radius"].SetValue(OcclusionRadius);
            occlusionEffect.Parameters["Intensity"].SetValue(OcclusionIntensity);
            occlusionEffect.Parameters["Bias"].SetValue(OcclusionBias);

            float inner = MathF.Cos(MathHelper.ToRadians(HeadlightInnerAngle));
            float outer = MathF.Cos(MathHelper.ToRadians(HeadlightOuterAngle));
            effect.Parameters["SpotCone"].SetValue(new Vector2(outer, 1f / (inner - outer)));
            effect.Parameters["SpotRange"].SetValue(HeadlightRange);
        }

        /// <summary>Draw with the lighting of the 2016 version.</summary>
        public bool Classic { get; set; }

        /// <summary>The sun casts shadows.</summary>
        public bool Shadows { get; set; } = true;

        /// <summary>Corners and crevices are darker (ambient occlusion).</summary>
        public bool AmbientOcclusion { get; set; } = true;

        /// <summary>The light of the scene.</summary>
        public Lighting Lighting { get; set; } = Lighting.Day;

        /// <summary>
        /// Distance added to where the haze starts and ends: for a camera further from the scene than
        /// the camera of the game (the level editor's).
        /// </summary>
        public float HazeOffset { get; set; }

        /// <summary>Colour of the screen where the scene and the background do not reach.</summary>
        public Color ClearColor { get; set; } = Color.Black;

        /// <summary>Starts a frame seen by a camera.</summary>
        public void Begin(Matrix view, Matrix projection)
        {
            this.view = view;
            this.projection = projection;
            eye = Matrix.Invert(view).Translation;
            frustum.Matrix = view * projection;
            items.Clear();
            background = null;
            headlights = false;
            braking = false;
        }

        /// <summary>Adds a model to the scene.</summary>
        public void Add(Model model, Matrix world)
        {
            items.Add(new DrawItem(GetModelData(model), world));
        }

        /// <summary>
        /// The lights of the car: the headlights, shining (with their lamps) if the lighting has them on, and the brake
        /// lights, shining when the car brakes. The rear lights shine dimly with the headlights.
        /// </summary>
        public void SetCarLights(Vector3 headlightPosition, Vector3 headlightDirection, bool braking)
        {
            headlights = true;
            this.headlightPosition = headlightPosition;
            this.headlightDirection = Vector3.Normalize(headlightDirection);
            this.braking = braking;
        }

        /// <summary>How much a lamp of the car shines now: 0 = off, 1 = fully.</summary>
        private float LampGlow(Lamp lamp)
        {
            bool lightsOn = headlights && Lighting.Headlights;
            switch (lamp)
            {
                case Lamp.Headlight: return lightsOn ? 1f : 0f;
                case Lamp.RearLight: return braking ? 1f : lightsOn ? RearLightDim : 0f;
                default: return 1f;
            }
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
            if (!Classic)
            {
                if (Shadows)
                    DrawShadowMap();
                if (AmbientOcclusion)
                    DrawOcclusionMap();
                //Switching render targets may discard the screen
                if (Shadows || AmbientOcclusion)
                    device.Clear(ClearColor);
            }

            device.BlendState = BlendState.Opaque;
            device.RasterizerState = RasterizerState.CullCounterClockwise;

            if (background != null)
            {
                device.DepthStencilState = DepthStencilState.None;
                device.SamplerStates[0] = SamplerState.LinearWrap;
                if (Classic)
                    background.Draw(backgroundWorld, view, projection);
                else
                    DrawSky();
            }

            device.DepthStencilState = DepthStencilState.Default;
            if (Classic)
                DrawClassic();
            else
                DrawLit();
            DrawLampGlows();
        }

        /// <summary>A white circle fading out to its edge; its alpha is the strength of the glow.</summary>
        private static Texture2D CreateGlowTexture(GraphicsDevice device)
        {
            Color[] pixels = new Color[GlowTextureSize * GlowTextureSize];
            float centre = (GlowTextureSize - 1) / 2f;
            for (int y = 0; y < GlowTextureSize; y++)
            {
                for (int x = 0; x < GlowTextureSize; x++)
                {
                    float distance = MathF.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre)) / centre;
                    float glow = MathHelper.Clamp(1f - distance, 0f, 1f);
                    pixels[y * GlowTextureSize + x] = new Color(1f, 1f, 1f, glow * glow * glow);
                }
            }
            Texture2D texture = new Texture2D(device, GlowTextureSize, GlowTextureSize);
            texture.SetData(pixels);
            return texture;
        }

        /// <summary>
        /// Adds the glows of the shining lamps to the screen, facing the camera. They are hidden by what is in front of
        /// them, but not by the car itself.
        /// </summary>
        private void DrawLampGlows()
        {
            glowVertices.Clear();
            Matrix camera = Matrix.Invert(view);
            Vector3 right = camera.Right, up = camera.Up;
            float strength = Lighting.Headlights ? GlowStrength : DaylightGlowStrength;
            foreach (DrawItem item in items)
            {
                if (item.Model.Lamps.Length == 0 || !IsVisible(item))
                    continue;

                foreach (LampData lamp in item.Model.Lamps)
                {
                    float glow = LampGlow(lamp.Lamp);
                    if (glow <= 0f)
                        continue;
                    Vector3 centre = Vector3.Transform(lamp.Bounds.Center, item.World);
                    centre += Vector3.Normalize(eye - centre) * GlowPull;
                    float radius = (lamp.Lamp == Lamp.Headlight ? HeadlightGlowRadius : RearLightGlowRadius) * MathF.Sqrt(glow);
                    Color colour = new Color(lamp.Colour * (glow * strength));
                    Vector3 x = right * radius, y = up * radius;
                    glowVertices.Add(new VertexPositionColorTexture(centre - x + y, colour, new Vector2(0f, 0f)));
                    glowVertices.Add(new VertexPositionColorTexture(centre + x + y, colour, new Vector2(1f, 0f)));
                    glowVertices.Add(new VertexPositionColorTexture(centre - x - y, colour, new Vector2(0f, 1f)));
                    glowVertices.Add(new VertexPositionColorTexture(centre + x + y, colour, new Vector2(1f, 0f)));
                    glowVertices.Add(new VertexPositionColorTexture(centre + x - y, colour, new Vector2(1f, 1f)));
                    glowVertices.Add(new VertexPositionColorTexture(centre - x - y, colour, new Vector2(0f, 1f)));
                }
            }
            if (glowVertices.Count == 0)
                return;

            glowEffect.View = view;
            glowEffect.Projection = projection;
            device.BlendState = BlendState.Additive;
            device.DepthStencilState = DepthStencilState.DepthRead;
            device.RasterizerState = RasterizerState.CullNone;
            device.SamplerStates[0] = SamplerState.LinearClamp;
            glowEffect.CurrentTechnique.Passes[0].Apply();
            VertexPositionColorTexture[] vertices = glowVertices.ToArray();
            device.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length / 3);
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullCounterClockwise;
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
                    if (part.IsTransparent)
                        continue;
                    worldParameter.SetValue(part.Transform * item.World);
                    pass.Apply();
                    DrawPart(part.MeshPart);
                }
            }

            device.SetRenderTarget(null);
        }

        /// <summary>
        /// Draws the ambient occlusion: the normals and depths seen by the camera, the occlusion
        /// computed from them, and the occlusion blurred.
        /// </summary>
        private void DrawOcclusionMap()
        {
            UpdateOcclusionTargets();

            device.SetRenderTarget(normalDepthMap);
            //Normal towards the camera, the greatest depth: nothing there
            device.Clear(new Color(128, 128, 255, 255));
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullCounterClockwise;

            effect.Parameters["View"].SetValue(view);
            effect.Parameters["ViewProjection"].SetValue(view * projection);
            effect.CurrentTechnique = normalDepthTechnique;
            EffectPass pass = normalDepthTechnique.Passes[0];
            foreach (DrawItem item in items)
            {
                if (!IsVisible(item))
                    continue;

                foreach (ModelPart part in item.Model.Parts)
                {
                    if (part.IsTransparent)
                        continue;
                    worldParameter.SetValue(part.Transform * item.World);
                    pass.Apply();
                    DrawPart(part.MeshPart);
                }
            }

            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone;
            occlusionEffect.Parameters["TargetSize"].SetValue(new Vector2(normalDepthMap.Width, normalDepthMap.Height));
            occlusionEffect.Parameters["TanHalfFov"].SetValue(new Vector2(1f / projection.M11, 1f / projection.M22));
            occlusionEffect.Parameters["NormalDepthMap"].SetValue(normalDepthMap);

            device.SetRenderTarget(occlusionMap);
            occlusionEffect.CurrentTechnique = occlusionEffect.Techniques["Occlusion"];
            occlusionEffect.CurrentTechnique.Passes[0].Apply();
            device.DrawUserPrimitives(PrimitiveType.TriangleStrip, screenQuad, 0, 2);

            device.SetRenderTarget(blurredOcclusionMap);
            occlusionEffect.Parameters["OcclusionMap"].SetValue(occlusionMap);
            occlusionEffect.CurrentTechnique = occlusionEffect.Techniques["Blur"];
            occlusionEffect.CurrentTechnique.Passes[0].Apply();
            device.DrawUserPrimitives(PrimitiveType.TriangleStrip, screenQuad, 0, 2);

            device.SetRenderTarget(null);
        }

        /// <summary>(Re)creates the textures of the ambient occlusion, at half the size of the screen.</summary>
        private void UpdateOcclusionTargets()
        {
            int width = Math.Max(1, device.PresentationParameters.BackBufferWidth / 2);
            int height = Math.Max(1, device.PresentationParameters.BackBufferHeight / 2);
            if (normalDepthMap != null && normalDepthMap.Width == width && normalDepthMap.Height == height)
                return;

            normalDepthMap?.Dispose();
            occlusionMap?.Dispose();
            blurredOcclusionMap?.Dispose();
            normalDepthMap = new RenderTarget2D(device, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24);
            occlusionMap = new RenderTarget2D(device, width, height, false, SurfaceFormat.Color, DepthFormat.None);
            blurredOcclusionMap = new RenderTarget2D(device, width, height, false, SurfaceFormat.Color, DepthFormat.None);
        }

        /// <summary>
        /// Fits the view of the sun to the part of the scene the camera sees, so the shadow map
        /// is used as fully as possible.
        /// </summary>
        private void UpdateLight()
        {
            //The part of each edge of the camera view that lies between the planes of the scene
            //(and not too far): the shadow map has to cover the ends of these parts
            frustum.GetCorners(frustumCorners);
            for (int i = 0; i < 4; i++)
            {
                Vector3 near = frustumCorners[i];
                Vector3 direction = frustumCorners[i + 4] - near;
                float end = Math.Min(1f, ShadowDistance / direction.Length());
                float from = 0f, to = end;
                if (Math.Abs(direction.Z) > 1e-6f)
                {
                    float a = (SceneNearZ - near.Z) / direction.Z;
                    float b = (SceneFarZ - near.Z) / direction.Z;
                    from = MathHelper.Clamp(Math.Min(a, b), 0f, end);
                    to = MathHelper.Clamp(Math.Max(a, b), 0f, end);
                }
                sceneCorners[i] = near + direction * from;
                sceneCorners[i + 4] = near + direction * to;
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
            Matrix lightView = Matrix.CreateLookAt(Vector3.Zero, -Lighting.SunDirection, Vector3.Up);
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

        /// <summary>The sky picture tinted by the colours of the light.</summary>
        private void DrawSky()
        {
            Lighting lighting = Lighting;
            effect.Parameters["SkyHorizon"].SetValue(lighting.SkyHorizon);
            effect.Parameters["SkyZenith"].SetValue(lighting.SkyZenith);
            effect.Parameters["SkyRecolor"].SetValue(lighting.SkyRecolor);
            effect.Parameters["SunGlow"].SetValue(lighting.SunGlow);
            effect.Parameters["SunDirection"].SetValue(lighting.SunDirection);
            effect.Parameters["ViewProjection"].SetValue(view * projection);
            effect.Parameters["CameraPosition"].SetValue(eye);
            EffectTechnique technique = effect.Techniques["Sky"];
            effect.CurrentTechnique = technique;

            foreach (ModelPart part in GetModelData(background).Parts)
            {
                worldParameter.SetValue(part.Transform * backgroundWorld);
                effect.Parameters["SkyTexture"].SetValue(part.Texture);
                technique.Passes[0].Apply();
                DrawPart(part.MeshPart);
            }
        }

        private void DrawLit()
        {
            Lighting lighting = Lighting;
            effect.Parameters["SunDirection"].SetValue(lighting.SunDirection);
            effect.Parameters["SunColor"].SetValue(lighting.SunColor);
            effect.Parameters["SkyLight"].SetValue(lighting.SkyLight);
            effect.Parameters["GroundLight"].SetValue(lighting.GroundLight);
            effect.Parameters["RimLight"].SetValue(lighting.RimLight);
            effect.Parameters["Exposure"].SetValue(lighting.Exposure);
            effect.Parameters["HazeColor"].SetValue(lighting.HazeColor);
            effect.Parameters["Haze"].SetValue(new Vector3(lighting.HazeStart + HazeOffset, 1f / (lighting.HazeEnd - lighting.HazeStart), lighting.HazeMax));
            effect.Parameters["MirroredSky"].SetValue(lighting.SkyLight * MirroredSkyLight);
            effect.Parameters["MirroredHorizon"].SetValue(ToLinear(lighting.HazeColor) * MirroredHorizon);
            effect.Parameters["MirroredGround"].SetValue(lighting.GroundLight * MirroredGroundLight);
            effect.Parameters["SpotPosition"].SetValue(headlightPosition);
            effect.Parameters["SpotDirection"].SetValue(headlights ? headlightDirection : Vector3.UnitX);
            effect.Parameters["SpotColor"].SetValue(headlights && lighting.Headlights ? HeadlightColor : Vector3.Zero);

            effect.Parameters["ViewProjection"].SetValue(view * projection);
            effect.Parameters["CameraPosition"].SetValue(eye);
            effect.Parameters["ShadowStrength"].SetValue(Shadows ? 1f : 0f);
            effect.Parameters["ShadowMap"].SetValue(shadowMap);
            UpdateOcclusionTargets();
            effect.Parameters["OcclusionStrength"].SetValue(AmbientOcclusion ? 1f : 0f);
            effect.Parameters["OcclusionMap"].SetValue(blurredOcclusionMap);
            effect.CurrentTechnique = litTechnique;
            EffectPass pass = litTechnique.Passes[0];

            CollectParts();
            foreach (DrawItem item in items)
            {
                if (!IsVisible(item))
                    continue;

                foreach (ModelPart part in item.Model.Parts)
                    if (!part.IsTransparent)
                        DrawLitPart(part, part.Transform * item.World, pass);
            }

            //Glass over what is behind it, without hiding it from the glass further back
            device.BlendState = BlendState.NonPremultiplied;
            device.DepthStencilState = DepthStencilState.DepthRead;
            foreach ((ModelPart part, Matrix world, float distance) in transparentParts)
                DrawLitPart(part, world, pass);
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
        }

        private void DrawLitPart(ModelPart part, Matrix world, EffectPass pass)
        {
            worldParameter.SetValue(world);
            diffuseParameter.SetValue(part.Diffuse);
            emissiveParameter.SetValue(part.Lamp == Lamp.None ? part.Emissive : part.Emissive * (LampGlow(part.Lamp) * LampBrightness));
            specularParameter.SetValue(part.Specular);
            alphaParameter.SetValue(part.Alpha);
            metallicParameter.SetValue(part.Metallic);
            roughnessParameter.SetValue(part.Roughness);
            pass.Apply();
            DrawPart(part.MeshPart);
        }

        /// <summary>Finds the transparent parts of the visible models and sorts them from the back to the front.</summary>
        private void CollectParts()
        {
            transparentParts.Clear();
            foreach (DrawItem item in items)
            {
                if (!IsVisible(item))
                    continue;
                foreach (ModelPart part in item.Model.Parts)
                {
                    if (!part.IsTransparent)
                        continue;
                    Matrix world = part.Transform * item.World;
                    float distance = Vector3.DistanceSquared(world.Translation, eye);
                    transparentParts.Add((part, world, distance));
                }
            }
            transparentParts.Sort((a, b) => b.Distance.CompareTo(a.Distance));
        }

        /// <summary>The lighting of the 2016 version: BasicEffect with three directional lights.</summary>
        private void DrawClassic()
        {
            CollectParts();
            foreach (DrawItem item in items)
            {
                if (!IsVisible(item))
                    continue;

                foreach (ModelPart part in item.Model.Parts)
                    if (!part.IsTransparent)
                        DrawClassicPart(part, part.Transform * item.World);
            }

            //BasicEffect multiplies its colour by its alpha
            device.BlendState = BlendState.AlphaBlend;
            device.DepthStencilState = DepthStencilState.DepthRead;
            foreach ((ModelPart part, Matrix world, float distance) in transparentParts)
                DrawClassicPart(part, world);
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
        }

        private void DrawClassicPart(ModelPart part, Matrix world)
        {
            BasicEffect basic = part.Effect;
            ApplyClassicLighting(basic);
            if (part.Lamp != Lamp.None)
                basic.EmissiveColor = part.ClassicEmissive * LampGlow(part.Lamp);
            basic.View = view;
            basic.Projection = projection;
            basic.World = world;
            basic.CurrentTechnique.Passes[0].Apply();
            DrawPart(part.MeshPart);
        }

        /// <summary>Sets up a BasicEffect with the lighting of the 2016 version.</summary>
        public static void ApplyClassicLighting(BasicEffect effect)
        {
            effect.PreferPerPixelLighting = true;
            effect.LightingEnabled = true;
            effect.AmbientLightColor = new Vector3(0.2f, 0.2f, 0.2f);

            effect.SpecularColor = new Vector3(1, 1, 1);
            effect.SpecularPower = 30;

            effect.DirectionalLight0.Direction = new Vector3(1, -1, 0);
            effect.DirectionalLight0.DiffuseColor = new Vector3(0.6f, 0.6f, 0.6f);
            effect.DirectionalLight0.SpecularColor = new Vector3(1, 1, 1);

            effect.DirectionalLight1.Direction = new Vector3(0, -1, -1);
            effect.DirectionalLight1.DiffuseColor = new Color(90, 166, 206).ToVector3() / 2;
            effect.DirectionalLight1.SpecularColor = new Color(90, 166, 206).ToVector3();
            effect.DirectionalLight1.Enabled = true;

            effect.DirectionalLight2.Direction = new Vector3(-1, -1, 0);
            effect.DirectionalLight2.DiffuseColor = new Color(74, 125, 156).ToVector3() / 2;
            effect.DirectionalLight2.SpecularColor = new Color(74, 125, 156).ToVector3();
            effect.DirectionalLight2.Enabled = true;
        }

        /// <summary>Models whose bounding sphere is completely outside the camera view are not drawn.</summary>
        private bool IsVisible(DrawItem item)
        {
            return frustum.Contains(item.Model.Bounds.Transform(item.World)) != ContainmentType.Disjoint;
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
            List<LampData> lamps = new List<LampData>();
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
                    Vector3 finish = Finish(meshPart);
                    parts.Add(new ModelPart
                    {
                        Transform = transform,
                        MeshPart = meshPart,
                        Effect = material,
                        Texture = material.Texture,
                        //The colours of the models were picked on the screen, the light works with linear ones
                        Diffuse = ToLinear(material.DiffuseColor),
                        Emissive = ToLinear(material.EmissiveColor),
                        ClassicEmissive = material.EmissiveColor,
                        Specular = (specular.X + specular.Y + specular.Z) / 3f * SpecularStrength,
                        Alpha = material.Alpha,
                        Metallic = finish.X,
                        Roughness = finish.Y,
                        Lamp = (Lamp)(int)finish.Z
                    });
                    if (parts[parts.Count - 1].Lamp != Lamp.None)
                        lamps.Add(new LampData(parts[parts.Count - 1].Lamp, meshBounds, material.EmissiveColor));
                }
            }

            data = new ModelData { Parts = parts.ToArray(), Bounds = bounds, Lamps = lamps.ToArray() };
            models.Add(model, data);
            return data;
        }

        /// <summary>
        /// What the content pipeline keeps of the material of a mesh part beyond BasicEffect (tools/ContentPipeline):
        /// metallic, roughness and the lamp (0 = none, 1 = headlight, 2 = rear light).
        /// </summary>
        private static Vector3 Finish(ModelMeshPart part)
        {
            return part.Tag is Vector3 finish ? finish : new Vector3(0f, 1f, 0f);
        }

        /// <summary>Which lamp of a car the material of a mesh part is.</summary>
        public static Lamp LampOf(ModelMeshPart part)
        {
            return (Lamp)(int)Finish(part).Z;
        }

        private static Vector3 ToLinear(Vector3 color)
        {
            return new Vector3(MathF.Pow(color.X, 2.2f), MathF.Pow(color.Y, 2.2f), MathF.Pow(color.Z, 2.2f));
        }
    }
}
