using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using TW3D.Shared;

namespace TW3D.Graphics
{
    /// <summary>The lamps among the materials of a model: of a car, or of a street lamp of the scenery.</summary>
    public enum Lamp
    {
        None,
        Headlight,
        RearLight,
        /// <summary>The lens of a street lamp: it shines at night and lights the scene below it.</summary>
        StreetLight,
        /// <summary>A red start light of the start gate, which shines as <see cref="SceneRenderer.StartLights"/> says.</summary>
        SignalRed,
        /// <summary>A lit window of a building: it shines at night, without a glow, and lights nothing around it.</summary>
        WindowLit
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
    /// and its glow are at its middle. The start lights of the start gate ("signalred…") shine as a run counts its start
    /// down (<see cref="StartLights"/>). The lenses of the street lamps ("streetlight…") of any model shine so at night
    /// (when the lighting has the headlights on), and the <see cref="StreetLightCount"/> of them nearest to the camera
    /// light the scene below them. The lit windows of a building ("windowlit…") shine at night too, evenly, without a
    /// glow, and light nothing: by day they are the glass they are made of. The painted parts of a model (the paint of a
    /// car, the rims of a wheel, see <see cref="PaintPart"/>) are drawn in the colours of the <see cref="Paint"/> the model
    /// is added with, the colours chosen in the garage. A part whose material names a <see cref="Surface"/> ("asphalt…",
    /// "earth…") is drawn by the technique of its surface at the <see cref="SurfaceDetail"/>, its colour and relief computed
    /// per pixel from the noise of <see cref="SurfaceNoise"/>, laid on it by the cell of the level grid the model stands in.
    /// A model added with <see cref="Mud"/> (the car driven on earth) is drawn with the mud over its opaque parts but its
    /// lamps, by the techniques of the mud, at the medium and the high <see cref="SurfaceDetail"/> (none at the low one).
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
        private const float StreetLightGlowRadius = 0.45f;
        private const float SignalGlowRadius = 0.15f;
        private const float GlowStrength = 0.8f;
        private const float DaylightGlowStrength = 0.45f;
        private const float GlowPull = 0.8f;
        private const int GlowTextureSize = 64;
        //The noise of the sky (see CreateSkyNoise): its size (as NOISE_SIZE of Scene.fx), the periods of the lattices
        //of its octaves of clouds in texels, and how fast the wind moves the clouds (texture coordinates per second)
        private const int SkyNoiseSize = 256;
        private static readonly int[] CloudPeriods = { 64, 32, 16, 8 };
        private static readonly Vector2 CloudWind = new Vector2(0.004f, 0.0015f);
        //The surfaces (see Surface): their noise is laid on a model by the cells of the level grid (scene units)
        private const float SurfaceCell = Units.GridCell * Units.PhysicsToScene;
        //The grey of the road of the classic blocks (as their materials have it, sRGB), which TestSurface draws as a
        //surface, and the colours of the earth and the grass of the off-road blocks (as tools/blender/offroad.py makes
        //their materials), which the test surfaces have there and which blend at the verge of the off-road road
        private const float ClassicRoadGrey = 0.22744f;
        private static readonly Vector3 OffroadEarthColour = new Vector3(0.38f, 0.3f, 0.22f);
        private static readonly Vector3 OffroadGrassColour = new Vector3(0.25f, 0.33f, 0.14f);
        //The colour of the mud on the car (sRGB): the earth of the off-road blocks, dried lighter (the shader makes it a little
        //lighter where it is thin, a little darker where thick); the rims of a wheel get this part of the mud of the wheel
        private static readonly Vector3 MudColour = new Vector3(0.5f, 0.41f, 0.31f);
        private const float RimMud = 0.7f;

        //The shadow map covers the part of the scene the camera sees: the 3D models of the road and of the
        //layers next to it lie between these planes (the front layer and the first back layer are 2.25
        //units from the physics plane z = 0, the blocks are 2.25 units deep), at most ShadowDistance from
        //the camera. It reaches this far towards the sun beyond it, for the shadows of objects above the
        //screen. Its size is of the ShadowDetail.
        private const float SceneNearZ = -3.5f;
        private const float SceneFarZ = 3.5f;
        private const float ShadowCasterReach = 10f;
        //The deep shadow map covers the scene from SceneNearZ to here, the deeper back layers too (the third is
        //6.75 units behind the physics plane), for what the shadow map does not reach. Coarser, as what is that
        //deep is far from the camera, and drawn only while something stands there. Both are parts of one texture,
        //the deep one beside the other, so that the shader looks into one texture as before.
        private const float DeepFarZ = 8f;
        //The part of the scene a shadow map covers is a box in the view of the sun, whose sides grow and shrink in
        //steps of this many to a doubling (about 4 %), so that a texel keeps its size while the camera turns a little;
        //a side shrinks only when it is this many times larger than it has to be
        private const float CoverStepsPerDoubling = 16f;
        private const float CoverShrink = 1.2f;
        //The smallest side of that box (world units), for a camera that sees nothing between the planes of the scene
        private const float MinCoverSize = 0.5f;
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
        //The street lamps nearest to the camera light the scene (the other four lights of Scene.fx) at night, as spot
        //lights shining down from their lenses, in their emissive colours this many times brighter, within the inner and
        //outer angle (degrees) of their cones, reaching this far (world units; a lamp is 2 high, a cell 2.25 long)
        public const int StreetLightCount = 4;
        private const int LightCount = CarLightCount + StreetLightCount;
        private const float StreetLightIntensity = 12f;
        private const float StreetLightInnerAngle = 25f;
        private const float StreetLightOuterAngle = 55f;
        private const float StreetLightRange = 6f;

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
            /// <summary>A lamp, shining in its emissive colour only when it is on.</summary>
            public Lamp Lamp;
            /// <summary>The column of a start light of the start gate: 0 the leftmost seen by the driver (see <see cref="StartLights"/>).</summary>
            public int Column;
            /// <summary>A part drawn in the colour of the paint of its model, if the paint has one for it.</summary>
            public PaintPart Paint;
            /// <summary>The emissive colour of the material as it is (for the classic look, which changes it for lamps).</summary>
            public Vector3 ClassicEmissive;
            /// <summary>The surface the part is drawn as, named by its material; <see cref="Surface.None"/> for its plain colour.</summary>
            public Surface Surface;
            /// <summary>The material is the grey of the road of the classic blocks (see <see cref="TestSurface"/>).</summary>
            public bool ClassicRoad;

            public bool IsTransparent => Alpha < 1f;
        }

        /// <summary>The parts and bounds of a model, found once per model.</summary>
        private sealed class ModelData
        {
            public ModelPart[] Parts;
            public BoundingSphere Bounds;
            public LampData[] Lamps;
        }

        /// <summary>A lamp of a model: the middle and radius of its mesh, the colour it shines in, its column (a signal lamp).</summary>
        private readonly struct LampData
        {
            public LampData(Lamp lamp, BoundingSphere bounds, Vector3 colour, int column)
            {
                Lamp = lamp;
                Bounds = bounds;
                Colour = colour;
                Column = column;
            }

            public Lamp Lamp { get; }
            public BoundingSphere Bounds { get; }
            public Vector3 Colour { get; }
            public int Column { get; }
        }

        private readonly struct DrawItem
        {
            public DrawItem(ModelData model, Matrix world, Paint paint, bool block, Mud mud)
            {
                Model = model;
                World = world;
                Paint = paint;
                Block = block;
                Mud = mud;
            }

            public ModelData Model { get; }
            public Matrix World { get; }
            public Paint Paint { get; }
            public bool Block { get; }
            public Mud Mud { get; }
        }

        private readonly GraphicsDevice device;
        private readonly Effect effect, occlusionEffect;
        private readonly EffectTechnique litTechnique, depthTechnique, normalDepthTechnique;
        private readonly EffectParameter worldParameter, diffuseParameter, emissiveParameter, specularParameter, alphaParameter,
            metallicParameter, roughnessParameter;
        //The techniques of the surfaces, by the surface and the detail (with the filter of the shadows of litTechnique), the
        //parameters of the cell of a model drawn as a surface, and the noise the surfaces are drawn from
        private readonly EffectTechnique[,] surfaceTechniques;
        private readonly EffectParameter surfaceOriginParameter, surfaceSeedsLowParameter, surfaceSeedsHighParameter, surfaceVergeParameter;
        private readonly Texture2D surfaceNoise;
        //The techniques of the mud by the detail (none at the low one) and the parameters of a part drawn with it
        private readonly EffectTechnique[] mudTechniques;
        private readonly EffectParameter mudSpaceParameter, mudAmountParameter, mudTreadParameter, mudAxlesParameter, mudBodyParameter;
        //The texture of both shadow maps, of the detail it was made for, the sizes of the maps in it, and the sides of the
        //boxes each of them covered in the last frame (see FitLight)
        private RenderTarget2D shadowMap;
        private ShadowDetail shadowMapDetail;
        private int shadowMapSize, deepShadowMapSize;
        private Vector2 shadowCover, deepShadowCover;
        //The marks on the track for this frame (see AddMarks) and what draws them
        private readonly List<(VertexPositionColor[] Vertices, int Triangles)> marks = new List<(VertexPositionColor[], int)>();
        private BasicEffect markEffect;
        //The smoke for this frame (see AddSmoke) and what draws it: soft puffs facing the camera
        private readonly List<(Vector3 Centre, float Radius, float Opacity, Vector3 Tint)> puffs = new List<(Vector3, float, float, Vector3)>();
        private readonly List<VertexPositionColorTexture> smokeVertices = new List<VertexPositionColorTexture>();
        private readonly Texture2D smokeTexture;
        private readonly BasicEffect smokeEffect;
        //The pieces of earth for this frame (see AddDebris) and what draws them: small lumps facing the camera
        private readonly List<(Vector3 Centre, float Radius, Vector3 Colour, float Alpha)> debris = new List<(Vector3, float, Vector3, float)>();
        private readonly List<VertexPositionColorTexture> debrisVertices = new List<VertexPositionColorTexture>();
        private readonly Texture2D debrisTexture;
        private readonly BasicEffect debrisEffect;
        private RenderTarget2D normalDepthMap, occlusionMap, blurredOcclusionMap;
        //The scene drawn smaller than the screen and scaled up to it (see Resolution), the height it is drawn at for the
        //screen and the choice it was worked out for, and the size the scene of this frame is drawn at
        private RenderTarget2D sceneTarget;
        private int sceneHeight;
        private (int, int, int?, float?) sceneHeightKey = (-1, -1, null, null);
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
        private readonly List<Vector3> sceneCorners = new List<Vector3>();
        private readonly Dictionary<Model, ModelData> models = new Dictionary<Model, ModelData>();
        private readonly List<DrawItem> items = new List<DrawItem>();
        //The glows of the shining lamps: a soft round texture added to the screen
        private readonly BasicEffect glowEffect;
        private readonly Texture2D glowTexture;
        private readonly List<VertexPositionColorTexture> glowVertices = new List<VertexPositionColorTexture>();
        //The transparent parts of the visible models, drawn after the others
        private readonly List<(ModelPart Part, Matrix World, float Distance, Paint Paint)> transparentParts =
            new List<(ModelPart, Matrix, float, Paint)>();

        private Matrix view;
        private Matrix projection;
        private Vector3 eye;
        private bool sky;
        private readonly Texture2D skyNoise;
        //The time the clouds drift by, shared by all renderers so that they do not jump between screens
        private static readonly System.Diagnostics.Stopwatch SkyClock = System.Diagnostics.Stopwatch.StartNew();
        private Model overlay;
        private Matrix overlayWorld;
        //The model of this frame drawn see-through (see SetGhost); null for none
        private Model ghost;
        private Matrix ghostWorld;
        private float ghostAlpha;
        private Vector3 ghostGlow;
        //The see-through model is drawn twice: its depth alone, then its colour where its depth is, so that only its
        //nearest surfaces are seen, not those behind them through them
        private static readonly BlendState DepthOnly = new BlendState { Name = "DepthOnly", ColorWriteChannels = ColorWriteChannels.None };
        private static readonly DepthStencilState DepthAtMost = new DepthStencilState
        {
            Name = "DepthAtMost",
            DepthBufferEnable = true,
            DepthBufferWriteEnable = false,
            DepthBufferFunction = CompareFunction.LessEqual
        };
        //The car: its lamps shining as spot lights, which lamp each is, where it is (relative to the car) and where it
        //points, the colour of its light when fully on
        private bool car, braking;
        private Vector3 carPosition;
        private readonly Lamp[] lights = new Lamp[LightCount];
        private readonly Vector3[] lightOffsets = new Vector3[LightCount];
        private readonly Vector3[] lightDirections = new Vector3[LightCount];
        private readonly Vector3[] lightColours = new Vector3[LightCount];
        private readonly Vector3[] lightLevels = new Vector3[LightCount];
        private readonly Vector2[] lightCones = new Vector2[LightCount];
        private readonly float[] lightRanges = new float[LightCount];
        //The street lamps of the frame: how far each is from the camera, where it is and its colour
        private readonly List<(float Distance, Vector3 Position, Vector3 Colour)> streetLamps = new List<(float, Vector3, Vector3)>();

        public SceneRenderer(GraphicsDevice device, ContentManager content)
        {
            this.device = device;
            effect = content.Load<Effect>("Effects/Scene");
            litTechnique = effect.Techniques[SmoothShadows ? "Lit" : "LitCoarseShadows"];
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
            Surface[] surfaces = Enum.GetValues<Surface>();
            SurfaceDetail[] details = Enum.GetValues<SurfaceDetail>();
            surfaceTechniques = new EffectTechnique[surfaces.Length, details.Length];
            foreach (Surface surface in surfaces)
                if (surface != Surface.None)
                    foreach (SurfaceDetail detail in details)
                        surfaceTechniques[(int)surface, (int)detail] = effect.Techniques[(SmoothShadows ? "Lit" : "LitCoarseShadows") + surface + detail];
            surfaceOriginParameter = effect.Parameters["SurfaceOrigin"];
            surfaceSeedsLowParameter = effect.Parameters["SurfaceSeedsLow"];
            surfaceSeedsHighParameter = effect.Parameters["SurfaceSeedsHigh"];
            surfaceVergeParameter = effect.Parameters["SurfaceVerge"];
            surfaceNoise = CreateSurfaceNoise(device);
            mudTechniques = new EffectTechnique[details.Length];
            foreach (SurfaceDetail detail in details)
                if (detail != SurfaceDetail.Low)
                    mudTechniques[(int)detail] = effect.Techniques[(SmoothShadows ? "Lit" : "LitCoarseShadows") + "Mud" + detail];
            mudSpaceParameter = effect.Parameters["MudSpace"];
            mudAmountParameter = effect.Parameters["MudAmount"];
            mudTreadParameter = effect.Parameters["MudTread"];
            mudAxlesParameter = effect.Parameters["MudAxles"];
            mudBodyParameter = effect.Parameters["MudBody"];

            glowTexture = CreateGlowTexture(device);
            smokeTexture = CreateSmokeTexture(device);
            debrisTexture = CreateDebrisTexture(device);
            debrisEffect = new BasicEffect(device) { TextureEnabled = true, VertexColorEnabled = true, Texture = debrisTexture };
            skyNoise = CreateSkyNoise(device);
            smokeEffect = new BasicEffect(device) { TextureEnabled = true, VertexColorEnabled = true, Texture = smokeTexture };
            glowEffect = new BasicEffect(device) { TextureEnabled = true, VertexColorEnabled = true, Texture = glowTexture };

            //Until the deep shadow map is drawn its view is not looked into, but the shader computes it (not divided by 0)
            effect.Parameters["DeepLightViewProjection"].SetValue(Matrix.Identity);
            effect.Parameters["DeepShadowStrength"].SetValue(0f);
            effect.Parameters["OcclusionDepthRange"].SetValue(OcclusionDepthRange);

            occlusionEffect.Parameters["DepthRange"].SetValue(OcclusionDepthRange);
            occlusionEffect.Parameters["Radius"].SetValue(OcclusionRadius);
            occlusionEffect.Parameters["Intensity"].SetValue(OcclusionIntensity);
            occlusionEffect.Parameters["Bias"].SetValue(OcclusionBias);

            for (int i = 0; i < LightCount; i++)
            {
                bool headlight = i < HeadlightCount, streetLight = i >= CarLightCount;
                lightCones[i] = streetLight ? Cone(StreetLightInnerAngle, StreetLightOuterAngle)
                    : Cone(headlight ? HeadlightInnerAngle : RearLightInnerAngle, headlight ? HeadlightOuterAngle : RearLightOuterAngle);
                lightRanges[i] = streetLight ? StreetLightRange : headlight ? HeadlightRange : RearLightRange;
            }
            effect.Parameters["LightCone"].SetValue(lightCones);
            effect.Parameters["LightRange"].SetValue(lightRanges);
        }

        /// <summary>
        /// The shadows are filtered smoothly (technique Lit of Scene.fx): their edges move with the scene, not in the steps
        /// of the texels of the shadow map. Not on a phone, whose pixel shaders compute in half precision (MonoGame declares
        /// them mediump on OpenGL ES): a position in a texture thousands of texels wide has no fraction of a texel left there,
        /// so a phone keeps the coarser filter that does not need it (LitCoarseShadows).
        /// </summary>
        private static bool SmoothShadows =>
#if ANDROID
            false;
#else
            true;
#endif

        /// <summary>
        /// (Re)creates the texture of the shadow maps when the <see cref="ShadowDetail"/> changes: the shadow map on the left,
        /// the deep one in the top right corner.
        /// </summary>
        private void UpdateShadowMap()
        {
            ShadowDetail detail = ShadowDetails.Valid(ShadowDetail);
            if (shadowMap != null && detail == shadowMapDetail)
                return;
            shadowMap?.Dispose();
            shadowMapDetail = detail;
            shadowMapSize = ShadowDetails.MapSize(detail);
            deepShadowMapSize = ShadowDetails.DeepMapSize(detail);
            shadowCover = deepShadowCover = Vector2.Zero;
            shadowMap = new RenderTarget2D(device, shadowMapSize + deepShadowMapSize, shadowMapSize, false, SurfaceFormat.Color, DepthFormat.Depth24);
        }

        /// <summary>
        /// Gives the shader the texture of the shadow maps and where the maps are in it; every frame, as the effect is
        /// shared by all renderers, whose details may differ.
        /// </summary>
        private void SetShadowMap()
        {
            float width = shadowMap.Width, height = shadowMap.Height;
            effect.Parameters["ShadowMap"].SetValue(shadowMap);
            effect.Parameters["ShadowTexel"].SetValue(new Vector2(1f / width, 1f / height));
            effect.Parameters["ShadowScale"].SetValue(new Vector2(shadowMapSize / width, shadowMapSize / height));
            effect.Parameters["DeepShadowScale"].SetValue(new Vector2(deepShadowMapSize / width, deepShadowMapSize / height));
            effect.Parameters["DeepShadowOffset"].SetValue(new Vector2(shadowMapSize / width, 0f));
            effect.Parameters["ShadowMargin"].SetValue(ShadowMargin / shadowMapSize);
            effect.Parameters["DeepShadowMargin"].SetValue(ShadowMargin / deepShadowMapSize);
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

        /// <summary>The detail of the shadows: the size of the shadow map (see <see cref="ShadowDetails"/>).</summary>
        public ShadowDetail ShadowDetail { get; set; } = ShadowDetails.Default;

        /// <summary>The detail of the surfaces (see <see cref="Surface"/> and <see cref="SurfaceDetails"/>).</summary>
        public SurfaceDetail SurfaceDetail { get; set; } = SurfaceDetails.Default;

        /// <summary>
        /// For development (T while driving): the road of the classic blocks (the parts of the grey of its material, in the
        /// models added as blocks) drawn as this surface, to try the surfaces out on any level before the blocks have looks
        /// of their own (#58). <see cref="Surface.None"/> draws them as they are.
        /// </summary>
        public Surface TestSurface { get; set; }

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
        /// The scene may be drawn smaller than the screen and scaled up to it, at the height of <see cref="Resolution"/>.
        /// Off for a scene that is drawn over afterwards with its depth buffer (the markers of the level editor), and for
        /// the track alone that the previews of the levels are taken of (H while driving).
        /// </summary>
        public bool MayDrawSmaller { get; set; } = true;

        /// <summary>
        /// The part of the height of the screen the scene is drawn at, as chosen in the Options menu (see
        /// <see cref="RenderResolution"/>; 1 is native); null while none was chosen: native, or 720 rows on a Raspberry Pi
        /// (see <see cref="PiGraphics"/>).
        /// </summary>
        public float? Resolution { get; set; }

        /// <summary>The height the scenes of this run are drawn at, whatever was chosen (the --render argument); null for none.</summary>
        public static int? PinnedHeight { get; set; }

        /// <summary>
        /// The height the scene is drawn at on a screen of a size (see <see cref="RenderResolution.SceneHeight"/>): the
        /// pinned one, the one chosen, or the default (720 rows at most on a Raspberry Pi, else native).
        /// </summary>
        /// <param name="resolution">The part of the height chosen; null while none was chosen.</param>
        public static int SceneHeight(int screenWidth, int screenHeight, float? resolution)
        {
            return RenderResolution.SceneHeight(screenWidth, screenHeight, PinnedHeight, resolution,
                PiGraphics.Active ? PiGraphics.SceneHeight : null);
        }

        /// <summary>Colour of the screen where the scene and the background do not reach.</summary>
        public Color ClearColor { get; set; } = Color.Black;

        /// <summary>
        /// How many columns of the red start lights of the start gates are on, for this frame (see Begin, which switches
        /// them off): the columns from the left seen by the driver, as the start lights of Formula 1 come on. Set by a run
        /// of a level during its countdown; off everywhere else (the menus, the level editor) and when the run starts.
        /// </summary>
        public int StartLights { get; set; }

        /// <summary>Starts a frame seen by a camera.</summary>
        public void Begin(Matrix view, Matrix projection)
        {
            this.view = view;
            this.projection = projection;
            marks.Clear();
            puffs.Clear();
            debris.Clear();
            eye = Matrix.Invert(view).Translation;
            frustum.Matrix = view * projection;
            items.Clear();
            sky = false;
            overlay = null;
            ghost = null;
            StartLights = 0;
            car = false;
            braking = false;
            Array.Clear(lights);
        }

        /// <summary>
        /// Marks lying on the track (the skid marks and the tracks of mud of the tyres): triangles of half transparent
        /// vertices (colours premultiplied by their alpha), drawn over the track and under the glass, in a haze of the
        /// light of the scene (as the pieces of earth are: a black mark stays black). For this frame (see Begin); each
        /// call adds a set of them.
        /// </summary>
        public void AddMarks(VertexPositionColor[] vertices, int triangles)
        {
            if (triangles > 0)
                marks.Add((vertices, triangles));
        }

        /// <summary>The light the marks and the pieces of earth take: bright by day, warm at sunset, dark at night.</summary>
        private Vector3 HazeLight => Classic ? new Vector3(0.85f) : Vector3.Lerp(Lighting.HazeColor, new Vector3(0.7f), 0.35f) * 1.3f;

        private void DrawMarks()
        {
            if (marks.Count == 0)
                return;
            markEffect ??= new BasicEffect(device) { VertexColorEnabled = true, LightingEnabled = false, TextureEnabled = false };
            markEffect.World = Matrix.Identity;
            markEffect.View = view;
            markEffect.Projection = projection;
            markEffect.DiffuseColor = HazeLight;
            device.BlendState = BlendState.AlphaBlend;
            device.DepthStencilState = DepthStencilState.DepthRead;
            device.RasterizerState = RasterizerState.CullNone;
            markEffect.CurrentTechnique.Passes[0].Apply();
            foreach ((VertexPositionColor[] vertices, int triangles) in marks)
                device.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, triangles);
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
        /// <param name="paint">The colours of its painted parts (the paint of a car, the rims of a wheel); by default their own.</param>
        /// <param name="block">The model is a block of a level (see <see cref="TestSurface"/>).</param>
        /// <param name="mud">The mud on it (a model of the car); by default none.</param>
        public void Add(Model model, Matrix world, Paint paint = default, bool block = false, Mud mud = default)
        {
            items.Add(new DrawItem(GetModelData(model), world, paint, block, mud));
        }

        /// <summary>The colour of the material of a painted part of a model (the colour it has without a paint); null if it has none.</summary>
        public static Vector3? MaterialColour(Model model, PaintPart paint)
        {
            foreach (ModelMesh mesh in model.Meshes)
                foreach (ModelMeshPart part in mesh.MeshParts)
                    if ((PaintPart)(int)Finish(part)[3] == paint && part.Effect is BasicEffect material)
                        return material.DiffuseColor;
            return null;
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

        /// <summary>
        /// The street lamps nearest to the camera take the lights after those of the car: at night only, as their lenses
        /// shine then (see <see cref="LampGlow"/>), from the middle of the lens, down.
        /// </summary>
        private void CollectStreetLights()
        {
            for (int i = CarLightCount; i < LightCount; i++)
                lights[i] = Lamp.None;
            if (!Lighting.Headlights)
                return;
            streetLamps.Clear();
            foreach (DrawItem item in items)
            {
                foreach (LampData lamp in item.Model.Lamps)
                {
                    if (lamp.Lamp != Lamp.StreetLight)
                        continue;
                    Vector3 centre = Vector3.Transform(lamp.Bounds.Center, item.World);
                    streetLamps.Add((Vector3.DistanceSquared(centre, eye), centre, lamp.Colour));
                }
            }
            streetLamps.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            for (int n = 0; n < StreetLightCount && n < streetLamps.Count; n++)
            {
                int slot = CarLightCount + n;
                lights[slot] = Lamp.StreetLight;
                //Relative to the car, as the shader has all its lamps
                lightOffsets[slot] = streetLamps[n].Position - carPosition;
                lightDirections[slot] = Vector3.Down;
                lightColours[slot] = ToLinear(streetLamps[n].Colour) * StreetLightIntensity;
            }
        }

        /// <summary>How much a lamp shines now: 0 = off, 1 = fully.</summary>
        /// <param name="column">The column of a signal lamp of the start gate.</param>
        private float LampGlow(Lamp lamp, int column = 0)
        {
            bool lightsOn = car && Lighting.Headlights;
            switch (lamp)
            {
                case Lamp.Headlight: return lightsOn ? 1f : 0f;
                case Lamp.RearLight: return braking ? 1f : lightsOn ? RearLightDim : 0f;
                case Lamp.StreetLight: return Lighting.Headlights ? 1f : 0f;
                case Lamp.SignalRed: return column < StartLights ? 1f : 0f;
                case Lamp.WindowLit: return Lighting.Headlights ? 1f : 0f;
                default: return 1f;
            }
        }

        /// <summary>A start light of the start gate, which shines by day as well as at night.</summary>
        private static bool IsSignal(Lamp lamp) => lamp == Lamp.SignalRed;

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
        /// Sets a model drawn see-through, for this frame (see Begin): the block the Place tool of the level editor would
        /// place. It is drawn after the scene, hidden where the scene is in front of it, only its nearest surfaces, lit
        /// as the scene is but without the ambient occlusion (that of the scene behind it); it casts no shadow.
        /// </summary>
        /// <param name="alpha">How much it hides what is behind it, 0..1.</param>
        /// <param name="glow">Light it gives off besides its own (linear colour), so that it is told from the blocks of the scene.</param>
        public void SetGhost(Model model, Matrix world, float alpha, Vector3 glow)
        {
            ghost = model;
            ghostWorld = world;
            ghostAlpha = alpha;
            ghostGlow = glow;
        }

        /// <summary>
        /// Frees the textures, render targets and effects the renderer made itself. The effects of the content manager
        /// (Scene, Occlusion) are shared by all renderers and stay.
        /// </summary>
        public void Dispose()
        {
            shadowMap?.Dispose();
            normalDepthMap?.Dispose();
            occlusionMap?.Dispose();
            blurredOcclusionMap?.Dispose();
            smokeTexture.Dispose();
            debrisTexture.Dispose();
            debrisEffect.Dispose();
            glowTexture.Dispose();
            skyNoise.Dispose();
            surfaceNoise.Dispose();
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
                UpdateShadowMap();
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
            DrawDebris();
            DrawSmoke();
            DrawLampGlows();
            DrawOverlay();
            if (target != null)
                ShowScene(target);
        }

        /// <summary>
        /// The target the scene of this frame is drawn into when it is drawn smaller than the screen: the height of
        /// <see cref="SceneHeight"/>, at the exact shape of the screen (see <see cref="RenderResolution"/>), multisampled
        /// as the screen is but on a phone (see <see cref="SceneTargetSamples"/>). Null when it is drawn straight on the screen, as it always was: at the native height, while
        /// it may not be drawn smaller (<see cref="MayDrawSmaller"/>), into a render target of the caller (the previews of
        /// the level editor) or into a part of the screen.
        /// </summary>
        private RenderTarget2D SceneTarget()
        {
            PresentationParameters screen = device.PresentationParameters;
            Viewport viewport = device.Viewport;
            if (!MayDrawSmaller || device.RenderTargetCount > 0
                || viewport.X != 0 || viewport.Y != 0 || viewport.Width != screen.BackBufferWidth || viewport.Height != screen.BackBufferHeight)
                return null;
            //The list of heights is worked out again only when the screen or the choice changes
            var key = (screen.BackBufferWidth, screen.BackBufferHeight, PinnedHeight, Resolution);
            if (key != sceneHeightKey)
            {
                sceneHeightKey = key;
                sceneHeight = SceneHeight(screen.BackBufferWidth, screen.BackBufferHeight, Resolution);
            }
            int height = sceneHeight;
            if (height <= 0 || height >= screen.BackBufferHeight)
                return null;

            int width = RenderResolution.WidthAt(screen.BackBufferWidth, screen.BackBufferHeight, height);
            if (sceneTarget == null || sceneTarget.Width != width || sceneTarget.Height != height)
            {
                sceneTarget?.Dispose();
                sceneTarget = new RenderTarget2D(device, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24,
                    SceneTargetSamples(screen), RenderTargetUsage.DiscardContents);
            }
            return sceneTarget;
        }

        /// <summary>
        /// The samples per pixel of the target the scene is drawn into when it is smaller than the screen: as many as the
        /// screen has, but none on a phone. MonoGame resolves a multisampled render target by glBlitFramebuffer, which it
        /// does not have on Android: the game crashed there at the first frame drawn smaller (2026-11-04).
        /// </summary>
        private static int SceneTargetSamples(PresentationParameters screen)
        {
#if ANDROID
            return 0;
#else
            return screen.MultiSampleCount;
#endif
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
        /// A piece of earth thrown up by a tyre (see GameObjects/DirtSpray) facing the camera, lit as the scene is; for this
        /// frame (see Begin).
        /// </summary>
        /// <param name="radius">Scene units.</param>
        /// <param name="colour">Its colour (sRGB, as the materials have them).</param>
        /// <param name="alpha">How much of it is seen, 0..1 (it fades out).</param>
        public void AddDebris(Vector3 centre, float radius, Vector3 colour, float alpha)
        {
            if (alpha > 0f)
                debris.Add((centre, radius, colour, alpha));
        }

        /// <summary>
        /// A lump of earth: an uneven disc, lighter on its upper left side and darker on its lower right, as a small round
        /// clod lit from above looks; its edge a texel soft.
        /// </summary>
        private static Texture2D CreateDebrisTexture(GraphicsDevice device)
        {
            const int size = 32;
            Color[] pixels = new Color[size * size];
            float centre = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - centre) / centre, dy = (y - centre) / centre;
                    float distance = MathF.Sqrt(dx * dx + dy * dy);
                    float angle = MathF.Atan2(dy, dx);
                    float edge = 0.78f + 0.12f * MathF.Sin(3f * angle + 0.7f) + 0.07f * MathF.Sin(5f * angle + 2.1f);
                    float alpha = MathHelper.Clamp((edge - distance) * centre, 0f, 1f);
                    //Lit from the upper left: lighter there, darker towards the lower right
                    float shade = MathHelper.Clamp(0.85f - 0.35f * (dx + dy) / 1.4f - 0.15f * distance, 0.35f, 1.15f) / 1.15f;
                    pixels[y * size + x] = new Color(shade * alpha, shade * alpha, shade * alpha, alpha);
                }
            }
            Texture2D texture = new Texture2D(device, size, size);
            texture.SetData(pixels);
            return texture;
        }

        /// <summary>
        /// Draws the pieces of earth, the furthest first, facing the camera and hidden by what is in front of them, in the
        /// light of the scene (a haze of it: bright by day, warm at sunset, dark at night).
        /// </summary>
        private void DrawDebris()
        {
            if (debris.Count == 0)
                return;
            Matrix camera = Matrix.Invert(view);
            Vector3 right = camera.Right, up = camera.Up;
            Vector3 light = HazeLight;
            debris.Sort((a, b) => Vector3.DistanceSquared(b.Centre, eye).CompareTo(Vector3.DistanceSquared(a.Centre, eye)));
            debrisVertices.Clear();
            foreach ((Vector3 centre, float radius, Vector3 tint, float alpha) in debris)
            {
                //Premultiplied by its alpha, as the texture is
                Color colour = new Color(new Vector4(Vector3.Min(light * tint, Vector3.One) * alpha, alpha));
                Vector3 x = right * radius, y = up * radius;
                debrisVertices.Add(new VertexPositionColorTexture(centre - x + y, colour, new Vector2(0f, 0f)));
                debrisVertices.Add(new VertexPositionColorTexture(centre + x + y, colour, new Vector2(1f, 0f)));
                debrisVertices.Add(new VertexPositionColorTexture(centre - x - y, colour, new Vector2(0f, 1f)));
                debrisVertices.Add(new VertexPositionColorTexture(centre + x + y, colour, new Vector2(1f, 0f)));
                debrisVertices.Add(new VertexPositionColorTexture(centre + x - y, colour, new Vector2(1f, 1f)));
                debrisVertices.Add(new VertexPositionColorTexture(centre - x - y, colour, new Vector2(0f, 1f)));
            }

            debrisEffect.View = view;
            debrisEffect.Projection = projection;
            device.BlendState = BlendState.AlphaBlend;
            device.DepthStencilState = DepthStencilState.DepthRead;
            device.RasterizerState = RasterizerState.CullNone;
            device.SamplerStates[0] = SamplerState.LinearClamp;
            debrisEffect.CurrentTechnique.Passes[0].Apply();
            VertexPositionColorTexture[] vertices = debrisVertices.ToArray();
            device.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length / 3);
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullCounterClockwise;
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

        /// <summary>The noise of the surfaces (see <see cref="SurfaceNoise"/>), with its mipmaps.</summary>
        private static Texture2D CreateSurfaceNoise(GraphicsDevice device)
        {
            byte[][] levels = SurfaceNoise.Levels;
            Texture2D texture = new Texture2D(device, SurfaceNoise.Size, SurfaceNoise.Size, true, SurfaceFormat.Color);
            for (int level = 0; level < Math.Min(levels.Length, texture.LevelCount); level++)
                texture.SetData(level, null, levels[level], 0, levels[level].Length);
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
                    float glow = LampGlow(lamp.Lamp, lamp.Column);
                    if (glow <= 0f)
                        continue;
                    Vector3 centre = Vector3.Transform(lamp.Bounds.Center, item.World);
                    centre += Vector3.Normalize(eye - centre) * GlowPull;
                    float radius = (lamp.Lamp == Lamp.Headlight ? HeadlightGlowRadius : lamp.Lamp == Lamp.StreetLight ? StreetLightGlowRadius
                        : IsSignal(lamp.Lamp) ? SignalGlowRadius : RearLightGlowRadius) * MathF.Sqrt(glow);
                    //A signal is meant to be seen in daylight too
                    Color colour = new Color(lamp.Colour * (glow * (IsSignal(lamp.Lamp) ? GlowStrength : strength)));
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

            Matrix lightViewProjection = FitLight(SceneFarZ, shadowMapSize, "", lightFrustum, ref shadowCover);
            if (deep)
            {
                //The depth pass draws with the matrix of the shadow map it draws into
                effect.Parameters["LightViewProjection"].SetValue(FitLight(DeepFarZ, deepShadowMapSize, "Deep", deepLightFrustum, ref deepShadowCover));
                device.Viewport = new Viewport(shadowMapSize, 0, deepShadowMapSize, deepShadowMapSize);
                DrawDepth(deepLightFrustum);
                effect.Parameters["LightViewProjection"].SetValue(lightViewProjection);
            }
            effect.Parameters["DeepShadowStrength"].SetValue(deep ? 1f : 0f);
            device.Viewport = new Viewport(0, 0, shadowMapSize, shadowMapSize);
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
        /// <param name="cover">The sides of the box the map covered in the last frame (zero for none); gets those of this one.</param>
        private Matrix FitLight(float farZ, int size, string prefix, BoundingFrustum light, ref Vector2 cover)
        {
            FindSceneCorners(farZ);

            //The map covers the box around them in the view of the sun. (Until 2026-11-04 it covered a sphere around
            //them, which took more than twice the width of the box on a wide screen: the texels were that much coarser.)
            Matrix lightView = Matrix.CreateLookAt(Vector3.Zero, -Lighting.SunDirection, Vector3.Up);
            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            foreach (Vector3 corner in sceneCorners)
            {
                Vector3 point = Vector3.Transform(corner, lightView);
                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
            }
            //The sides of the box go in steps, so that a texel keeps its size while the camera turns a little; the edge of
            //the map that is not looked up (and a texel the rounding below may move it by) lies outside the box
            float usable = 1f - 2f * (ShadowMargin + 1f) / size;
            cover = new Vector2(CoverSide((max.X - min.X) / usable, cover.X), CoverSide((max.Y - min.Y) / usable, cover.Y));
            //Its position is rounded to whole texels of the map, so that the shadows do not flicker when the camera moves
            Vector2 texel = cover / size;
            float centreX = MathF.Round((min.X + max.X) / 2f / texel.X) * texel.X;
            float centreY = MathF.Round((min.Y + max.Y) / 2f / texel.Y) * texel.Y;
            //The sun looks along -z of its view: what is nearest to it has the greatest z
            float nearest = -max.Z - ShadowCasterReach, furthest = -min.Z;
            Matrix lightProjection = Matrix.CreateOrthographicOffCenter(centreX - cover.X / 2f, centreX + cover.X / 2f,
                centreY - cover.Y / 2f, centreY + cover.Y / 2f, nearest, furthest);

            Matrix lightViewProjection = lightView * lightProjection;
            light.Matrix = lightViewProjection;
            effect.Parameters[prefix + "LightViewProjection"].SetValue(lightViewProjection);
            effect.Parameters[prefix + "NormalOffset"].SetValue(NormalOffsetTexels * Math.Max(texel.X, texel.Y));
            effect.Parameters[prefix + "ShadowBias"].SetValue(ShadowBiasWorld / (furthest - nearest));
            return lightViewProjection;
        }

        /// <summary>
        /// Finds the corners of the part of the scene the camera sees from SceneNearZ to a depth, at most ShadowDistance
        /// from the camera (along the edges of its view): the corners of the view cut so short that lie between the planes
        /// of the two depths, and where its edges cross those planes. (Only where the four edges from the camera crossed
        /// the planes until 2026-11-04: a camera that looks along the road, as during the countdown, has an edge that
        /// does not reach them, and the scene seen beside that edge was left out.)
        /// </summary>
        /// <param name="farZ">How deep the part of the scene reaches (scene z).</param>
        private void FindSceneCorners(float farZ)
        {
            frustum.GetCorners(frustumCorners);
            for (int i = 0; i < 4; i++)
            {
                Vector3 direction = frustumCorners[i + 4] - frustumCorners[i];
                frustumCorners[i + 4] = frustumCorners[i] + direction * Math.Min(1f, ShadowDistance / direction.Length());
            }
            sceneCorners.Clear();
            foreach (Vector3 corner in frustumCorners)
                if (corner.Z >= SceneNearZ && corner.Z <= farZ)
                    sceneCorners.Add(corner);
            foreach ((int from, int to) in FrustumEdges)
            {
                AddCrossing(frustumCorners[from], frustumCorners[to], SceneNearZ);
                AddCrossing(frustumCorners[from], frustumCorners[to], farZ);
            }
            //A camera that sees nothing between the planes: what it sees nearest, where no shadow matters
            if (sceneCorners.Count == 0)
                for (int i = 0; i < 4; i++)
                    sceneCorners.Add(frustumCorners[i]);
        }

        /// <summary>Where an edge from a to b crosses the plane of a depth, if it does, is a corner of the part of the scene.</summary>
        private void AddCrossing(Vector3 a, Vector3 b, float z)
        {
            if ((a.Z - z) * (b.Z - z) < 0f)
                sceneCorners.Add(Vector3.Lerp(a, b, (z - a.Z) / (b.Z - a.Z)));
        }

        /// <summary>The edges of a view by the corners of BoundingFrustum.GetCorners: the near face, the far face and from one to the other.</summary>
        private static readonly (int, int)[] FrustumEdges =
        {
            (0, 1), (1, 2), (2, 3), (3, 0),
            (4, 5), (5, 6), (6, 7), (7, 4),
            (0, 4), (1, 5), (2, 6), (3, 7)
        };

        /// <summary>
        /// A side of the box a shadow map covers (see <see cref="FitLight"/>): the side of the last frame while it is long
        /// enough and not too long, else the step (of <see cref="CoverStepsPerDoubling"/> to a doubling) just long enough.
        /// </summary>
        /// <param name="needed">How long the side has to be.</param>
        /// <param name="last">The side of the last frame; 0 for none.</param>
        private static float CoverSide(float needed, float last)
        {
            needed = Math.Max(needed, MinCoverSize);
            if (last >= needed && last <= needed * CoverShrink)
                return last;
            return MathF.Pow(2f, MathF.Ceiling(MathF.Log2(needed) * CoverStepsPerDoubling) / CoverStepsPerDoubling);
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
            CollectStreetLights();
            for (int i = 0; i < LightCount; i++)
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
            SetShadowMap();
            UpdateOcclusionTargets();
            effect.Parameters["OcclusionStrength"].SetValue(AmbientOcclusion ? 1f : 0f);
            effect.Parameters["OcclusionMap"].SetValue(blurredOcclusionMap);
            effect.Parameters["SurfaceNoise"].SetValue(surfaceNoise);
            effect.Parameters["SurfaceCell"].SetValue(SurfaceCell);
            effect.Parameters["MudColor"].SetValue(ToLinear(MudColour));
            effect.Parameters["VergeEarth"].SetValue(ToLinear(OffroadEarthColour));
            effect.Parameters["VergeGrass"].SetValue(ToLinear(OffroadGrassColour));
            effect.CurrentTechnique = litTechnique;
            EffectPass pass = litTechnique.Passes[0];

            CollectParts();
            foreach (DrawItem item in items)
            {
                if (!IsVisible(item))
                    continue;

                foreach (ModelPart part in item.Model.Parts)
                    if (!part.IsTransparent)
                        DrawLitPart(part, part.Transform * item.World, pass, paint: item.Paint, block: item.Block, mud: item.Mud);
            }

            DrawMarks();

            //Glass over what is behind it, without hiding it from the glass further back
            device.BlendState = BlendState.NonPremultiplied;
            device.DepthStencilState = DepthStencilState.DepthRead;
            foreach ((ModelPart part, Matrix world, float distance, Paint paint) in transparentParts)
                DrawLitPart(part, world, pass, paint: paint);

            if (ghost != null)
            {
                ModelPart[] parts = GetModelData(ghost).Parts;
                //The occlusion map is of the scene behind it
                effect.Parameters["OcclusionStrength"].SetValue(0f);
                device.BlendState = DepthOnly;
                device.DepthStencilState = DepthStencilState.Default;
                foreach (ModelPart part in parts)
                    DrawLitPart(part, part.Transform * ghostWorld, pass);
                device.BlendState = BlendState.NonPremultiplied;
                device.DepthStencilState = DepthAtMost;
                foreach (ModelPart part in parts)
                    DrawLitPart(part, part.Transform * ghostWorld, pass, ghostAlpha, ghostGlow);
            }
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
        }

        /// <param name="pass">The pass of the plain parts; a part with a surface is drawn by the technique of its surface.</param>
        /// <param name="alpha">A part of its own opacity it is drawn with (the see-through model).</param>
        /// <param name="glow">Light added to its own (the see-through model).</param>
        /// <param name="paint">The colours of the painted parts of its model.</param>
        /// <param name="block">Its model is a block of a level (see <see cref="TestSurface"/>).</param>
        /// <param name="mud">The mud on its model.</param>
        private void DrawLitPart(ModelPart part, Matrix world, EffectPass pass, float alpha = 1f, Vector3 glow = default, Paint paint = default,
            bool block = false, Mud mud = default)
        {
            Vector3 diffuse = paint.Of(part.Paint) is Vector3 colour ? ToLinear(colour) : part.Diffuse;
            Surface surface = part.Surface;
            if (block && part.ClassicRoad && TestSurface != Surface.None)
            {
                surface = TestSurface;
                if (surface == Surface.Earth)
                    diffuse = ToLinear(OffroadEarthColour);
                else if (surface == Surface.Grass)
                    diffuse = ToLinear(OffroadGrassColour);
            }
            if (surface != Surface.None)
            {
                pass = surfaceTechniques[(int)surface, (int)SurfaceDetails.Valid(SurfaceDetail)].Passes[0];
                SetSurfaceCell(world.Translation);
                //The verge of the off-road road, on the earth and the grass of a block's own materials
                surfaceVergeParameter.SetValue(surface == part.Surface && (surface == Surface.Earth || surface == Surface.Grass) ? 1f : 0f);
            }
            else if (!mud.IsClean && !part.IsTransparent && part.Lamp == Lamp.None && mudTechniques[(int)SurfaceDetails.Valid(SurfaceDetail)] is EffectTechnique muddy)
            {
                pass = muddy.Passes[0];
                mudSpaceParameter.SetValue(part.Transform);
                mudAmountParameter.SetValue(mud.Amount * (part.Paint == PaintPart.Rim ? RimMud : 1f));
                mudTreadParameter.SetValue(mud.Tread);
                mudAxlesParameter.SetValue(mud.Axles ?? Vector4.Zero);
                mudBodyParameter.SetValue(mud.Axles.HasValue ? 1f : 0f);
            }
            worldParameter.SetValue(world);
            diffuseParameter.SetValue(diffuse);
            emissiveParameter.SetValue((part.Lamp == Lamp.None ? part.Emissive : part.Emissive * (LampGlow(part.Lamp, part.Column) * LampBrightness)) + glow);
            specularParameter.SetValue(part.Specular);
            alphaParameter.SetValue(part.Alpha * alpha);
            metallicParameter.SetValue(part.Metallic);
            roughnessParameter.SetValue(part.Roughness);
            pass.Apply();
            DrawPart(part.MeshPart);
        }

        /// <summary>
        /// Lays the noise of the surfaces on a model by the cell of the level grid it stands in (the nearest to its origin):
        /// the middle of the cell, and the random values of its corners, which the cells around it share.
        /// </summary>
        private void SetSurfaceCell(Vector3 origin)
        {
            int x = (int)MathF.Round(origin.X / SurfaceCell), y = (int)MathF.Round(origin.Y / SurfaceCell), z = (int)MathF.Round(origin.Z / SurfaceCell);
            surfaceOriginParameter.SetValue(new Vector3(x, y, z) * SurfaceCell);
            //A cell's corners are x and x + 1 (at the lower x and the higher one), the same of y and z
            surfaceSeedsLowParameter.SetValue(new Vector4(SurfaceNoise.CornerSeed(x, y, z), SurfaceNoise.CornerSeed(x + 1, y, z),
                SurfaceNoise.CornerSeed(x, y, z + 1), SurfaceNoise.CornerSeed(x + 1, y, z + 1)));
            surfaceSeedsHighParameter.SetValue(new Vector4(SurfaceNoise.CornerSeed(x, y + 1, z), SurfaceNoise.CornerSeed(x + 1, y + 1, z),
                SurfaceNoise.CornerSeed(x, y + 1, z + 1), SurfaceNoise.CornerSeed(x + 1, y + 1, z + 1)));
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
                    transparentParts.Add((part, world, distance, item.Paint));
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
                        DrawClassicPart(part, part.Transform * item.World, paint: item.Paint);
            }

            //BasicEffect multiplies its colour by its alpha
            DrawMarks();

            device.BlendState = BlendState.AlphaBlend;
            device.DepthStencilState = DepthStencilState.DepthRead;
            foreach ((ModelPart part, Matrix world, float distance, Paint paint) in transparentParts)
                DrawClassicPart(part, world, paint: paint);

            if (ghost != null)
            {
                ModelPart[] parts = GetModelData(ghost).Parts;
                device.BlendState = DepthOnly;
                device.DepthStencilState = DepthStencilState.Default;
                foreach (ModelPart part in parts)
                    DrawClassicPart(part, part.Transform * ghostWorld);
                device.BlendState = BlendState.AlphaBlend;
                device.DepthStencilState = DepthAtMost;
                foreach (ModelPart part in parts)
                    DrawClassicPart(part, part.Transform * ghostWorld, ghostAlpha, ghostGlow);
            }
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
        }

        /// <param name="alpha">A part of its own opacity it is drawn with (the see-through model).</param>
        /// <param name="glow">Light added to its own (the see-through model).</param>
        /// <param name="paint">The colours of the painted parts of its model.</param>
        private void DrawClassicPart(ModelPart part, Matrix world, float alpha = 1f, Vector3 glow = default, Paint paint = default)
        {
            BasicEffect basic = part.Effect;
            Lighting.ApplyClassic(basic);
            if (part.Lamp != Lamp.None)
                basic.EmissiveColor = part.ClassicEmissive * LampGlow(part.Lamp, part.Column);
            //The effect is the model's, shared with every draw of it: what is changed for one draw is put back after it
            bool seeThrough = alpha < 1f || glow != Vector3.Zero;
            Vector3 emissive = basic.EmissiveColor;
            if (seeThrough)
            {
                basic.Alpha = part.Alpha * alpha;
                basic.EmissiveColor = emissive + glow;
            }
            Vector3? painted = paint.Of(part.Paint);
            Vector3 diffuse = basic.DiffuseColor;
            if (painted.HasValue)
                basic.DiffuseColor = painted.Value;
            basic.View = view;
            basic.Projection = projection;
            basic.World = world;
            basic.CurrentTechnique.Passes[0].Apply();
            DrawPart(part.MeshPart);
            if (seeThrough)
            {
                basic.Alpha = part.Alpha;
                basic.EmissiveColor = emissive;
            }
            if (painted.HasValue)
                basic.DiffuseColor = diffuse;
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
            //The lamps with a glow (all but the lit windows): their parts, the bounds of their meshes and the colours they shine in
            List<(ModelPart Part, BoundingSphere Bounds, Vector3 Colour)> lamps = new List<(ModelPart, BoundingSphere, Vector3)>();
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
                    float[] finish = Finish(meshPart);
                    Vector3 grey = material.DiffuseColor - new Vector3(ClassicRoadGrey);
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
                        Metallic = finish[0],
                        Roughness = finish[1],
                        Lamp = (Lamp)(int)finish[2],
                        Paint = (PaintPart)(int)finish[3],
                        Surface = (Surface)(int)finish[4],
                        ClassicRoad = MathF.Abs(grey.X) + MathF.Abs(grey.Y) + MathF.Abs(grey.Z) < 0.003f
                    });
                    if (parts[parts.Count - 1].Lamp != Lamp.None && parts[parts.Count - 1].Lamp != Lamp.WindowLit)
                        lamps.Add((parts[parts.Count - 1], meshBounds, material.EmissiveColor));
                }
            }

            //The start lights by their columns (the lamps one above the other are a column, lit together): from the one
            //furthest along +z of the model, the leftmost seen by a driver driving along +x of the physics (towards -x of
            //the scene)
            var columns = lamps.Where(lamp => IsSignal(lamp.Part.Lamp))
                .GroupBy(lamp => MathF.Round(lamp.Bounds.Center.Z, 2))
                .OrderByDescending(column => column.Key)
                .ToList();
            for (int column = 0; column < columns.Count; column++)
                foreach (var lamp in columns[column])
                    lamp.Part.Column = column;
            data = new ModelData
            {
                Parts = parts.ToArray(),
                Bounds = bounds,
                Lamps = lamps.Select(lamp => new LampData(lamp.Part.Lamp, lamp.Bounds, lamp.Colour, lamp.Part.Column)).ToArray()
            };
            models.Add(model, data);
            return data;
        }

        /// <summary>
        /// What the content pipeline keeps of the material of a mesh part beyond BasicEffect (tools/ContentPipeline):
        /// metallic, roughness, the lamp (the number of a <see cref="Lamp"/>: 0 = none, 1 = headlight, 2 = rear light, ...),
        /// the painted part (the number of a <see cref="PaintPart"/>) and the surface (the number of a <see cref="Surface"/>).
        /// A model built before the surfaces has the first four of them, before the paint the first three.
        /// </summary>
        private static float[] Finish(ModelMeshPart part)
        {
            return part.Tag is float[] finish && finish.Length >= 5 ? finish
                : part.Tag is Vector4 old ? new[] { old.X, old.Y, old.Z, old.W, 0f }
                : part.Tag is Vector3 older ? new[] { older.X, older.Y, older.Z, 0f, 0f }
                : new[] { 0f, 1f, 0f, 0f, 0f };
        }

        private static Vector3 ToLinear(Vector3 color)
        {
            return new Vector3(MathF.Pow(color.X, 2.2f), MathF.Pow(color.Y, 2.2f), MathF.Pow(color.Z, 2.2f));
        }
    }
}
