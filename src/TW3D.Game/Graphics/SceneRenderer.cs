using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TW3D.Graphics
{
    /// <summary>The lamps of a car among the materials of its model.</summary>
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
    /// "taillight…") shine in their emissive colour ("Ke") only when they are on (<see cref="SetCar"/>), with a glow
    /// around them, and light the scene as spot lights in that colour; each lamp is a mesh of its own, its light
    /// and its glow are at its middle.
    /// </remarks>
    public sealed class SceneRenderer : IDisposable
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
        //The noise of the sky (see CreateSkyNoise): its size (as NOISE_SIZE of Scene.fx), the periods of the lattices
        //of its octaves of clouds in texels, and how fast the wind moves the clouds (texture coordinates per second)
        private const int SkyNoiseSize = 256;
        private static readonly int[] CloudPeriods = { 64, 32, 16, 8 };
        private static readonly Vector2 CloudWind = new Vector2(0.004f, 0.0015f);

        //The shadow map covers the part of the scene the camera sees: the 3D models of the road and of the
        //layers next to it lie between these planes (the front layer and the first back layer are 2.25
        //units from the physics plane z = 0, the blocks are 2.25 units deep), at most ShadowDistance from
        //the camera. It reaches this far towards the sun beyond it, for the shadows of objects above the
        //screen.
        private static readonly int ShadowMapSize = PiGraphics.Active ? PiGraphics.ShadowMapSize : 2048;
        private const float SceneNearZ = -3.5f;
        private const float SceneFarZ = 3.5f;
        private const float ShadowCasterReach = 10f;
        //The deep shadow map covers the scene from SceneNearZ to here, the deeper back layers too (the third is
        //6.75 units behind the physics plane), for what the shadow map does not reach. Coarser, as what is that
        //deep is far from the camera, and drawn only while something stands there. Both are parts of one texture,
        //the deep one beside the other, so that the shader looks into one texture as before.
        private const int DeepShadowMapSize = 1024;
        private const float DeepFarZ = 8f;
        //The texels at the edges of a shadow map that are not looked up: the samples around them would fall outside
        private const float ShadowMargin = 3f;
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

        //The lamps of the car light the scene as spot lights (two headlights and two rear lights, as Scene.fx has them),
        //in their emissive colours this many times brighter, within the inner and outer angle (degrees) of their
        //cones, reaching this far (world units), pointing ahead or behind and this much down; the headlights turned
        //this much (degrees) to their sides, so that their beams part. The rear lights are weaker in daylight
        private const int HeadlightCount = 2;
        private const int CarLightCount = 4;
        private const float HeadlightIntensity = 4f;
        private const float HeadlightInnerAngle = 8f;
        private const float HeadlightOuterAngle = 20f;
        private const float HeadlightRange = 14f;
        private const float HeadlightDip = 0.3f;
        private const float HeadlightSpread = 8f;
        private const float RearLightIntensity = 2.5f;
        private const float RearLightInnerAngle = 30f;
        private const float RearLightOuterAngle = 75f;
        private const float RearLightRange = 5f;
        private const float RearLightDip = 0.5f;
        private const float RearLightDaylight = 0.3f;

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
        //The marks on the track for this frame (see SetMarks) and what draws them
        private VertexPositionColor[] marks;
        private int markTriangles;
        private BasicEffect markEffect;
        //The smoke for this frame (see AddSmoke) and what draws it: soft puffs facing the camera
        private readonly List<(Vector3 Centre, float Radius, float Opacity, Vector3 Tint)> puffs = new List<(Vector3, float, float, Vector3)>();
        private readonly List<VertexPositionColorTexture> smokeVertices = new List<VertexPositionColorTexture>();
        private readonly Texture2D smokeTexture;
        private readonly BasicEffect smokeEffect;
        private RenderTarget2D normalDepthMap, occlusionMap, blurredOcclusionMap;
        //The scene drawn smaller than the screen and scaled up to it (on a Raspberry Pi, see PiGraphics), and the size the
        //scene of this frame is drawn at
        private RenderTarget2D sceneTarget;
        private SpriteBatch sceneBatch;
        private Point sceneSize;
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
        private readonly BoundingFrustum deepLightFrustum = new BoundingFrustum(Matrix.Identity);
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
        private bool sky;
        private readonly Texture2D skyNoise;
        //The time the clouds drift by, shared by all renderers so that they do not jump between screens
        private static readonly System.Diagnostics.Stopwatch SkyClock = System.Diagnostics.Stopwatch.StartNew();
        private Model overlay;
        private Matrix overlayWorld;
        //The car: its lamps shining as spot lights, which lamp each is, where it is (relative to the car) and where it
        //points, the colour of its light when fully on
        private bool car, braking;
        private Vector3 carPosition;
        private readonly Lamp[] lights = new Lamp[CarLightCount];
        private readonly Vector3[] lightOffsets = new Vector3[CarLightCount];
        private readonly Vector3[] lightDirections = new Vector3[CarLightCount];
        private readonly Vector3[] lightColours = new Vector3[CarLightCount];
        private readonly Vector3[] lightLevels = new Vector3[CarLightCount];
        private readonly Vector2[] lightCones = new Vector2[CarLightCount];
        private readonly float[] lightRanges = new float[CarLightCount];

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
            smokeTexture = CreateSmokeTexture(device);
            skyNoise = CreateSkyNoise(device);
            smokeEffect = new BasicEffect(device) { TextureEnabled = true, VertexColorEnabled = true, Texture = smokeTexture };
            glowEffect = new BasicEffect(device) { TextureEnabled = true, VertexColorEnabled = true, Texture = glowTexture };

            //The shadow map on the left, the deep one in the top right corner
            float width = ShadowMapSize + DeepShadowMapSize, height = ShadowMapSize;
            shadowMap = new RenderTarget2D(device, (int)width, (int)height, false, SurfaceFormat.Color, DepthFormat.Depth24);
            effect.Parameters["ShadowTexel"].SetValue(new Vector2(1f / width, 1f / height));
            effect.Parameters["ShadowScale"].SetValue(new Vector2(ShadowMapSize / width, ShadowMapSize / height));
            effect.Parameters["DeepShadowScale"].SetValue(new Vector2(DeepShadowMapSize / width, DeepShadowMapSize / height));
            effect.Parameters["DeepShadowOffset"].SetValue(new Vector2(ShadowMapSize / width, 0f));
            effect.Parameters["ShadowMargin"].SetValue(ShadowMargin / ShadowMapSize);
            effect.Parameters["DeepShadowMargin"].SetValue(ShadowMargin / DeepShadowMapSize);
            //Until the deep shadow map is drawn its view is not looked into, but the shader computes it (not divided by 0)
            effect.Parameters["DeepLightViewProjection"].SetValue(Matrix.Identity);
            effect.Parameters["DeepShadowStrength"].SetValue(0f);
            effect.Parameters["OcclusionDepthRange"].SetValue(OcclusionDepthRange);

            occlusionEffect.Parameters["DepthRange"].SetValue(OcclusionDepthRange);
            occlusionEffect.Parameters["Radius"].SetValue(OcclusionRadius);
            occlusionEffect.Parameters["Intensity"].SetValue(OcclusionIntensity);
            occlusionEffect.Parameters["Bias"].SetValue(OcclusionBias);

            for (int i = 0; i < CarLightCount; i++)
            {
                bool headlight = i < HeadlightCount;
                lightCones[i] = Cone(headlight ? HeadlightInnerAngle : RearLightInnerAngle, headlight ? HeadlightOuterAngle : RearLightOuterAngle);
                lightRanges[i] = headlight ? HeadlightRange : RearLightRange;
            }
            effect.Parameters["LightCone"].SetValue(lightCones);
            effect.Parameters["LightRange"].SetValue(lightRanges);
        }

        /// <summary>The cone of a spot light as the shader has it.</summary>
        private static Vector2 Cone(float innerAngle, float outerAngle)
        {
            float inner = MathF.Cos(MathHelper.ToRadians(innerAngle));
            float outer = MathF.Cos(MathHelper.ToRadians(outerAngle));
            return new Vector2(outer, 1f / (inner - outer));
        }

        /// <summary>Draw with the lighting of the 2016 version.</summary>
        public bool Classic { get; set; }

        /// <summary>The sun casts shadows.</summary>
        public bool Shadows { get; set; } = true;

        /// <summary>
        /// The deep shadow map is drawn while something the camera sees stands deeper than the shadow map reaches (the
        /// deeper back layers of a level); without it the scene is shadowed as by the shadow map alone.
        /// </summary>
        public bool DeepShadows { get; set; }

        /// <summary>Corners and crevices are darker (ambient occlusion).</summary>
        public bool AmbientOcclusion { get; set; } = true;

        /// <summary>The light of the scene.</summary>
        public Lighting Lighting { get; set; } = Lighting.Day;

        /// <summary>
        /// Distance added to where the haze starts and ends: for a camera further from the scene than
        /// the camera of the game (the level editor's).
        /// </summary>
        public float HazeOffset { get; set; }

        /// <summary>
        /// How far from the camera the shadows reach (world units): the shadow map covers the scene up to there, so the
        /// shorter, the sharper the shadows.
        /// </summary>
        public float ShadowDistance { get; set; } = 40f;

        /// <summary>
        /// How far below the eye the horizon of the sky is (radians, see <see cref="SetSky"/>): 0 on the ground (the
        /// garage), <see cref="HighAbove"/> for the levels, which hang high above the land, so that the camera of the game,
        /// which looks down at the car, sees the sky behind the track and the land only far below it.
        /// </summary>
        public float HorizonDip { get; set; }

        /// <summary>The dip of the horizon of the levels (radians): about where the picture of the sky of 2016 had it.</summary>
        public const float HighAbove = 0.7f;

        /// <summary>
        /// The scene may be drawn smaller than the screen and scaled up to it, where the graphics say so (on a Raspberry Pi,
        /// see <see cref="PiGraphics"/>). Off for a scene that is drawn over afterwards with its depth buffer (the markers
        /// of the level editor).
        /// </summary>
        public bool MayDrawSmaller { get; set; } = true;

        /// <summary>Colour of the screen where the scene and the background do not reach.</summary>
        public Color ClearColor { get; set; } = Color.Black;

        /// <summary>Starts a frame seen by a camera.</summary>
        public void Begin(Matrix view, Matrix projection)
        {
            this.view = view;
            this.projection = projection;
            markTriangles = 0;
            puffs.Clear();
            eye = Matrix.Invert(view).Translation;
            frustum.Matrix = view * projection;
            items.Clear();
            sky = false;
            overlay = null;
            car = false;
            braking = false;
            Array.Clear(lights);
        }

        /// <summary>
        /// Marks lying on the track (the skid marks of the tyres): triangles of dark, half transparent vertices (colours
        /// premultiplied by their alpha), drawn over the track and under the glass, unlit. For this frame (see Begin).
        /// </summary>
        public void SetMarks(VertexPositionColor[] vertices, int triangles)
        {
            marks = vertices;
            markTriangles = triangles;
        }

        private void DrawMarks()
        {
            if (markTriangles == 0)
                return;
            markEffect ??= new BasicEffect(device) { VertexColorEnabled = true, LightingEnabled = false, TextureEnabled = false };
            markEffect.World = Matrix.Identity;
            markEffect.View = view;
            markEffect.Projection = projection;
            device.BlendState = BlendState.AlphaBlend;
            device.DepthStencilState = DepthStencilState.DepthRead;
            device.RasterizerState = RasterizerState.CullNone;
            markEffect.CurrentTechnique.Passes[0].Apply();
            device.DrawUserPrimitives(PrimitiveType.TriangleList, marks, 0, markTriangles);
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// A puff of smoke (of the tyres or the exhaust) facing the camera, its colour taken from the light of the scene;
        /// for this frame (see Begin).
        /// </summary>
        /// <param name="radius">Scene units.</param>
        /// <param name="opacity">How much it hides what is behind it in its middle, 0..1.</param>
        /// <param name="tint">Its colour, times a greyed haze of the light (white: as grey as the haze).</param>
        public void AddSmoke(Vector3 centre, float radius, float opacity, Vector3 tint)
        {
            if (opacity > 0f)
                puffs.Add((centre, radius, opacity, tint));
        }

        /// <summary>Adds a model to the scene.</summary>
        public void Add(Model model, Matrix world)
        {
            items.Add(new DrawItem(GetModelData(model), world));
        }

        /// <summary>
        /// The car, whose lamps (the lamps of its model) shine and light the scene: the headlights if the lighting has
        /// them on, the rear lights dimly with them and fully when the car brakes.
        /// </summary>
        /// <param name="forward">The direction the car faces in its model.</param>
        public void SetCar(Model model, Matrix world, Vector3 forward, bool braking)
        {
            car = true;
            this.braking = braking;
            carPosition = world.Translation;
            Vector3 ahead = Vector3.Normalize(Vector3.TransformNormal(forward, world));
            Vector3 up = Vector3.Normalize(Vector3.TransformNormal(Vector3.Up, world));
            Vector3 side = Vector3.Normalize(Vector3.Cross(ahead, up));
            float scale = Vector3.TransformNormal(Vector3.UnitX, world).Length();

            //The light comes from the front of a lamp: from its middle, as far ahead (or behind) as it is large
            int headlights = 0, rearLights = 0;
            foreach (LampData lamp in GetModelData(model).Lamps)
            {
                bool headlight = lamp.Lamp == Lamp.Headlight;
                int slot = headlight ? headlights++ : HeadlightCount + rearLights++;
                if (slot >= (headlight ? HeadlightCount : CarLightCount))
                    continue;
                Vector3 direction = headlight ? ahead : -ahead;
                lights[slot] = lamp.Lamp;
                lightOffsets[slot] = Vector3.Transform(lamp.Bounds.Center, world) + direction * (lamp.Bounds.Radius * scale) - carPosition;
                Vector3 turn = headlight ? side * (MathF.Sign(Vector3.Dot(lightOffsets[slot], side)) * MathF.Tan(MathHelper.ToRadians(HeadlightSpread))) : Vector3.Zero;
                lightDirections[slot] = Vector3.Normalize(direction + turn - up * (headlight ? HeadlightDip : RearLightDip));
                lightColours[slot] = ToLinear(lamp.Colour) * (headlight ? HeadlightIntensity : RearLightIntensity);
            }
        }

        /// <summary>How much a lamp of the car shines now: 0 = off, 1 = fully.</summary>
        private float LampGlow(Lamp lamp)
        {
            bool lightsOn = car && Lighting.Headlights;
            switch (lamp)
            {
                case Lamp.Headlight: return lightsOn ? 1f : 0f;
                case Lamp.RearLight: return braking ? 1f : lightsOn ? RearLightDim : 0f;
                default: return 1f;
            }
        }

        /// <summary>
        /// The sky of the <see cref="Lighting"/> is drawn behind everything else, for this frame (see Begin): computed from
        /// the direction of every pixel of the screen, infinitely far, and without the depth buffer, so it never hides the
        /// scene. Without it the screen is cleared to <see cref="ClearColor"/> there.
        /// </summary>
        public void SetSky()
        {
            sky = true;
        }

        /// <summary>
        /// Sets the model drawn over the whole scene (the 3D logo of the main menu), for this frame (see Begin): lit as the
        /// scene is, but over a depth buffer of its own, so that nothing of the scene ever cuts it, and without shadows or
        /// ambient occlusion, neither cast nor received.
        /// </summary>
        public void SetOverlay(Model model, Matrix world)
        {
            overlay = model;
            overlayWorld = world;
        }

        /// <summary>
        /// Frees the textures, render targets and effects the renderer made itself. The effects of the content manager
        /// (Scene, Occlusion) are shared by all renderers and stay.
        /// </summary>
        public void Dispose()
        {
            shadowMap.Dispose();
            normalDepthMap?.Dispose();
            occlusionMap?.Dispose();
            blurredOcclusionMap?.Dispose();
            smokeTexture.Dispose();
            glowTexture.Dispose();
            skyNoise.Dispose();
            sceneTarget?.Dispose();
            sceneBatch?.Dispose();
            smokeEffect.Dispose();
            glowEffect.Dispose();
            markEffect?.Dispose();
        }

        /// <summary>Draws the frame.</summary>
        public void End()
        {
            RenderTarget2D target = SceneTarget();
            sceneSize = target != null ? new Point(target.Width, target.Height)
                : new Point(device.PresentationParameters.BackBufferWidth, device.PresentationParameters.BackBufferHeight);
            if (!Classic)
            {
                if (Shadows)
                    DrawShadowMap();
                if (AmbientOcclusion)
                    DrawOcclusionMap();
            }
            if (target != null)
            {
                device.SetRenderTarget(target);
                device.Clear(ClearColor);
            }
            //Switching render targets may discard the screen
            else if (!Classic && (Shadows || AmbientOcclusion))
                device.Clear(ClearColor);

            device.BlendState = BlendState.Opaque;
            device.RasterizerState = RasterizerState.CullCounterClockwise;

            //The same sky in the lighting of 2016, which had a picture of one
            if (sky)
            {
                device.DepthStencilState = DepthStencilState.None;
                DrawSky();
            }

            device.DepthStencilState = DepthStencilState.Default;
            if (Classic)
                DrawClassic();
            else
                DrawLit();
            DrawSmoke();
            DrawLampGlows();
            DrawOverlay();
            if (target != null)
                ShowScene(target);
        }

        /// <summary>
        /// The target the scene of this frame is drawn into when it is drawn smaller than the screen (see
        /// <see cref="PiGraphics"/>): the height of <see cref="PiGraphics.SceneHeight"/>, the shape of the screen. Null when
        /// it is drawn straight on the screen: elsewhere, on a screen no higher than that, into a render target of the caller
        /// (the previews of the level editor) or into a part of the screen.
        /// </summary>
        private RenderTarget2D SceneTarget()
        {
            PresentationParameters screen = device.PresentationParameters;
            Viewport viewport = device.Viewport;
            int height = PiGraphics.SceneHeight;
            if (!PiGraphics.Active || !MayDrawSmaller || device.RenderTargetCount > 0 || screen.BackBufferHeight <= height
                || viewport.X != 0 || viewport.Y != 0 || viewport.Width != screen.BackBufferWidth || viewport.Height != screen.BackBufferHeight)
                return null;

            int width = Math.Max(1, (int)MathF.Round(screen.BackBufferWidth * (float)height / screen.BackBufferHeight));
            if (sceneTarget == null || sceneTarget.Width != width || sceneTarget.Height != height)
            {
                sceneTarget?.Dispose();
                sceneTarget = new RenderTarget2D(device, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24,
                    screen.MultiSampleCount, RenderTargetUsage.DiscardContents);
            }
            return sceneTarget;
        }

        /// <summary>The scene drawn smaller (see <see cref="SceneTarget"/>), scaled up over the whole screen.</summary>
        private void ShowScene(RenderTarget2D target)
        {
            device.SetRenderTarget(null);
            sceneBatch ??= new SpriteBatch(device);
            sceneBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            sceneBatch.Draw(target, new Rectangle(0, 0, device.Viewport.Width, device.Viewport.Height), Color.White);
            sceneBatch.End();
        }

        /// <summary>The overlay over everything drawn so far, in front of it (see <see cref="SetOverlay"/>).</summary>
        private void DrawOverlay()
        {
            if (overlay == null)
                return;
            device.Clear(ClearOptions.DepthBuffer, Color.Black, 1f, 0);
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullCounterClockwise;
            ModelData data = GetModelData(overlay);
            if (Classic)
            {
                foreach (ModelPart part in data.Parts)
                    if (!part.IsTransparent)
                        DrawClassicPart(part, part.Transform * overlayWorld);
                return;
            }
            //The parameters of the scene stay as DrawLit set them, but for the shadows and the occlusion, which are the scene's
            effect.Parameters["ShadowStrength"].SetValue(0f);
            effect.Parameters["OcclusionStrength"].SetValue(0f);
            effect.CurrentTechnique = litTechnique;
            EffectPass pass = litTechnique.Passes[0];
            foreach (ModelPart part in data.Parts)
                if (!part.IsTransparent)
                    DrawLitPart(part, part.Transform * overlayWorld, pass);
        }

        /// <summary>A white puff thinning out to its edge, a little uneven; its alpha is how thick it is.</summary>
        private static Texture2D CreateSmokeTexture(GraphicsDevice device)
        {
            Color[] pixels = new Color[GlowTextureSize * GlowTextureSize];
            float centre = (GlowTextureSize - 1) / 2f;
            for (int y = 0; y < GlowTextureSize; y++)
            {
                for (int x = 0; x < GlowTextureSize; x++)
                {
                    float dx = (x - centre) / centre, dy = (y - centre) / centre;
                    float distance = MathF.Sqrt(dx * dx + dy * dy);
                    //A few soft lumps around its middle
                    float lumps = 0.85f + 0.15f * MathF.Sin(3f * MathF.Atan2(dy, dx) + 4f * distance);
                    float thickness = MathHelper.Clamp(1f - distance / lumps, 0f, 1f);
                    float alpha = thickness * thickness * (3f - 2f * thickness);
                    pixels[y * GlowTextureSize + x] = new Color(alpha, alpha, alpha, alpha);
                }
            }
            Texture2D texture = new Texture2D(device, GlowTextureSize, GlowTextureSize);
            texture.SetData(pixels);
            return texture;
        }

        /// <summary>
        /// Draws the puffs of smoke over the scene, the furthest first, facing the camera and hidden by what is in front
        /// of them. Their colour is a greyed haze of the light (whitish by day, warm at sunset, dim at night), tinted.
        /// </summary>
        private void DrawSmoke()
        {
            if (puffs.Count == 0)
                return;
            Matrix camera = Matrix.Invert(view);
            Vector3 right = camera.Right, up = camera.Up;
            Vector3 light = Classic ? new Vector3(0.75f) : Vector3.Lerp(Lighting.HazeColor, new Vector3(0.6f), 0.4f);
            puffs.Sort((a, b) => Vector3.DistanceSquared(b.Centre, eye).CompareTo(Vector3.DistanceSquared(a.Centre, eye)));
            smokeVertices.Clear();
            foreach ((Vector3 centre, float radius, float opacity, Vector3 tint) in puffs)
            {
                //Premultiplied by its alpha
                Color colour = new Color(new Vector4(light * tint * opacity, opacity));
                Vector3 x = right * radius, y = up * radius;
                smokeVertices.Add(new VertexPositionColorTexture(centre - x + y, colour, new Vector2(0f, 0f)));
                smokeVertices.Add(new VertexPositionColorTexture(centre + x + y, colour, new Vector2(1f, 0f)));
                smokeVertices.Add(new VertexPositionColorTexture(centre - x - y, colour, new Vector2(0f, 1f)));
                smokeVertices.Add(new VertexPositionColorTexture(centre + x + y, colour, new Vector2(1f, 0f)));
                smokeVertices.Add(new VertexPositionColorTexture(centre + x - y, colour, new Vector2(1f, 1f)));
                smokeVertices.Add(new VertexPositionColorTexture(centre - x - y, colour, new Vector2(0f, 1f)));
            }

            smokeEffect.View = view;
            smokeEffect.Projection = projection;
            device.BlendState = BlendState.AlphaBlend;
            device.DepthStencilState = DepthStencilState.DepthRead;
            device.RasterizerState = RasterizerState.CullNone;
            device.SamplerStates[0] = SamplerState.LinearClamp;
            smokeEffect.CurrentTechnique.Passes[0].Apply();
            VertexPositionColorTexture[] vertices = smokeVertices.ToArray();
            device.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length / 3);
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// The noise the sky is made of. Red: soft value noise of a few octaves that tiles (the lattices' periods divide the
        /// size), stretched to the full range, of which the shader cuts the clouds. Green: an independent random value in
        /// every texel, which the shader reads at the middles of the texels (for the stars and the dithering), so the
        /// texture has no mipmaps. The same every time (a fixed seed).
        /// </summary>
        private static Texture2D CreateSkyNoise(GraphicsDevice device)
        {
            const int size = SkyNoiseSize;
            Random random = new Random(2026);
            float[] clouds = new float[size * size];
            float weight = 1f;
            foreach (int period in CloudPeriods)
            {
                int cells = size / period;
                float[] lattice = new float[cells * cells];
                for (int i = 0; i < lattice.Length; i++)
                    lattice[i] = (float)random.NextDouble();
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float fx = (float)x / period, fy = (float)y / period;
                        int x0 = (int)fx, y0 = (int)fy;
                        //Smoothstep between the lattice points, wrapping around the edges
                        float tx = fx - x0, ty = fy - y0;
                        tx = tx * tx * (3f - 2f * tx);
                        ty = ty * ty * (3f - 2f * ty);
                        float a = lattice[y0 % cells * cells + x0 % cells], b = lattice[y0 % cells * cells + (x0 + 1) % cells];
                        float c = lattice[(y0 + 1) % cells * cells + x0 % cells], d = lattice[(y0 + 1) % cells * cells + (x0 + 1) % cells];
                        clouds[y * size + x] += weight * MathHelper.Lerp(MathHelper.Lerp(a, b, tx), MathHelper.Lerp(c, d, tx), ty);
                    }
                }
                weight *= 0.5f;
            }
            float least = float.MaxValue, most = float.MinValue;
            foreach (float value in clouds)
            {
                least = Math.Min(least, value);
                most = Math.Max(most, value);
            }
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color((clouds[i] - least) / (most - least), (float)random.NextDouble(), 0f);
            Texture2D texture = new Texture2D(device, size, size, false, SurfaceFormat.Color);
            texture.SetData(pixels);
            return texture;
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

        /// <summary>
        /// Draws the depth of the scene seen from the sun into the shadow map, and into the deep shadow map while
        /// something the camera sees stands deeper than the shadow map reaches.
        /// </summary>
        private void DrawShadowMap()
        {
            bool deep = false;
            if (DeepShadows)
            {
                foreach (DrawItem item in items)
                {
                    if (item.Model.Bounds.Transform(item.World).Center.Z > SceneFarZ && IsVisible(item))
                    {
                        deep = true;
                        break;
                    }
                }
            }

            device.SetRenderTarget(shadowMap);
            device.Clear(Color.White);
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullNone;

            Matrix lightViewProjection = FitLight(SceneFarZ, ShadowMapSize, "", lightFrustum);
            if (deep)
            {
                //The depth pass draws with the matrix of the shadow map it draws into
                effect.Parameters["LightViewProjection"].SetValue(FitLight(DeepFarZ, DeepShadowMapSize, "Deep", deepLightFrustum));
                device.Viewport = new Viewport(ShadowMapSize, 0, DeepShadowMapSize, DeepShadowMapSize);
                DrawDepth(deepLightFrustum);
                effect.Parameters["LightViewProjection"].SetValue(lightViewProjection);
            }
            effect.Parameters["DeepShadowStrength"].SetValue(deep ? 1f : 0f);
            device.Viewport = new Viewport(0, 0, ShadowMapSize, ShadowMapSize);
            DrawDepth(lightFrustum);

            device.SetRenderTarget(null);
        }

        /// <summary>Draws the depth of what the view of the sun sees (the frustum) into the viewport of the shadow map.</summary>
        private void DrawDepth(BoundingFrustum light)
        {
            effect.CurrentTechnique = depthTechnique;
            EffectPass pass = depthTechnique.Passes[0];
            foreach (DrawItem item in items)
            {
                if (light.Contains(item.Model.Bounds.Transform(item.World)) == ContainmentType.Disjoint)
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

        /// <summary>(Re)creates the textures of the ambient occlusion, at half the size the scene is drawn at.</summary>
        private void UpdateOcclusionTargets()
        {
            int width = Math.Max(1, sceneSize.X / 2);
            int height = Math.Max(1, sceneSize.Y / 2);
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
        /// Fits the view of the sun to the part of the scene the camera sees from SceneNearZ to a depth, so a shadow
        /// map is used as fully as possible; sets the parameters of the shader for that map and gives the matrix.
        /// </summary>
        /// <param name="farZ">How deep the part of the scene reaches (scene z).</param>
        /// <param name="size">The size of the shadow map in texels.</param>
        /// <param name="prefix">What the names of the parameters of the shader start with: "" for the shadow map, "Deep" for the deep one.</param>
        /// <param name="light">Gets the view of the sun, to leave out what it does not see.</param>
        private Matrix FitLight(float farZ, int size, string prefix, BoundingFrustum light)
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
                    float b = (farZ - near.Z) / direction.Z;
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
            float texel = 2f * radius / size;
            lightCenter.X = MathF.Round(lightCenter.X / texel) * texel;
            lightCenter.Y = MathF.Round(lightCenter.Y / texel) * texel;
            float distance = -lightCenter.Z;
            Matrix lightProjection = Matrix.CreateOrthographicOffCenter(lightCenter.X - radius, lightCenter.X + radius,
                lightCenter.Y - radius, lightCenter.Y + radius, distance - radius - ShadowCasterReach, distance + radius);

            Matrix lightViewProjection = lightView * lightProjection;
            light.Matrix = lightViewProjection;
            effect.Parameters[prefix + "LightViewProjection"].SetValue(lightViewProjection);
            effect.Parameters[prefix + "NormalOffset"].SetValue(NormalOffsetTexels * texel);
            effect.Parameters[prefix + "ShadowBias"].SetValue(ShadowBiasWorld / (2f * radius + ShadowCasterReach));
            return lightViewProjection;
        }

        /// <summary>The sky picture tinted by the colours of the light.</summary>
        /// <summary>The sky of the lighting over the whole screen (see <see cref="SetSky"/> and the technique Sky of Scene.fx).</summary>
        private void DrawSky()
        {
            SkyLook look = Lighting.Sky;
            effect.Parameters["InverseViewProjection"].SetValue(Matrix.Invert(view * projection));
            effect.Parameters["CameraPosition"].SetValue(eye);
            effect.Parameters["SkyZenith"].SetValue(look.Zenith);
            effect.Parameters["SkyHorizon"].SetValue(look.Horizon);
            effect.Parameters["SkyGround"].SetValue(look.Ground);
            effect.Parameters["SkyHills"].SetValue(look.Hills);
            effect.Parameters["SkySun"].SetValue(look.SunDirection);
            effect.Parameters["SunDisc"].SetValue(look.SunDisc);
            effect.Parameters["SunDiscCos"].SetValue(MathF.Cos(look.SunRadius));
            effect.Parameters["SunGlow"].SetValue(look.SunGlow);
            effect.Parameters["Stars"].SetValue(look.Stars);
            effect.Parameters["CloudCover"].SetValue(look.Clouds);
            effect.Parameters["CloudLight"].SetValue(look.CloudLight);
            effect.Parameters["CloudShade"].SetValue(look.CloudShade);
            //The texture wraps: only the part of a turn counts, which keeps the shader's numbers small
            Vector2 drift = CloudWind * (float)SkyClock.Elapsed.TotalSeconds;
            effect.Parameters["CloudDrift"].SetValue(new Vector2(drift.X % 1f, drift.Y % 1f));
            effect.Parameters["SkyScreenSize"].SetValue(new Vector2(device.Viewport.Width, device.Viewport.Height));
            effect.Parameters["HorizonDip"].SetValue(MathF.Sin(HorizonDip));
            effect.Parameters["SkyNoise"].SetValue(skyNoise);
            EffectTechnique technique = effect.Techniques["Sky"];
            effect.CurrentTechnique = technique;
            device.RasterizerState = RasterizerState.CullNone;
            device.SamplerStates[0] = SamplerState.LinearWrap;
            technique.Passes[0].Apply();
            device.DrawUserPrimitives(PrimitiveType.TriangleStrip, screenQuad, 0, 2);
            device.RasterizerState = RasterizerState.CullCounterClockwise;
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
            for (int i = 0; i < CarLightCount; i++)
            {
                float level = lights[i] == Lamp.None ? 0f : LampGlow(lights[i]);
                if (lights[i] == Lamp.RearLight && !lighting.Headlights)
                    level *= RearLightDaylight;
                lightLevels[i] = lightColours[i] * level;
            }
            effect.Parameters["LightOrigin"].SetValue(carPosition);
            effect.Parameters["LightOffset"].SetValue(lightOffsets);
            effect.Parameters["LightDirection"].SetValue(lightDirections);
            effect.Parameters["LightColor"].SetValue(lightLevels);

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

            DrawMarks();

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
            DrawMarks();

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
            Lighting.ApplyClassic(basic);
            if (part.Lamp != Lamp.None)
                basic.EmissiveColor = part.ClassicEmissive * LampGlow(part.Lamp);
            basic.View = view;
            basic.Projection = projection;
            basic.World = world;
            basic.CurrentTechnique.Passes[0].Apply();
            DrawPart(part.MeshPart);
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

        private static Vector3 ToLinear(Vector3 color)
        {
            return new Vector3(MathF.Pow(color.X, 2.2f), MathF.Pow(color.Y, 2.2f), MathF.Pow(color.Z, 2.2f));
        }
    }
}
