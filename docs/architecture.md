# Architecture

The game is built on the [MonoGame](https://monogame.net/) framework and the
[Aether.Physics2D](https://github.com/nkast/Aether.Physics2D) physics engine
(the maintained successor of Farseer Physics, which the game originally used).
The physics runs in 2D, and the 3D models are drawn at the positions of the
physics bodies.

## Repository structure

| Path | Contents |
|---|---|
| `src/TW3D.Game/` | The game (`TW3D.exe`), with the level editor (`Editor/`, see [Levels](levels.md#level-editor)) |
| `src/TW3D.Android/` | The game for Android phones, built from the code of `src/TW3D.Game` (see [Building](building.md#android)) |
| `src/TW3D.Shared/` | Library shared by the game, the editor and the tools: the catalog of blocks, objects and scenery, the level file format, the collision shapes and the units of the coordinates (`Units`: the models, the physics world and the scene) |
| `content/` | Shared game content: 3D models (`.obj` + `.mtl`, built by the content pipeline `Content.mgcb`), images of the user interface and previews of the levels (`.png`), fonts (`.spritefont` descriptions of the `.ttf` files), the catalog (`catalog.json`), the collision shapes of the models (`collision.json`, generated) and levels (`Levels/*.json`). Built and copied to the build output as `Content/`. See [Game content](content.md). |
| `art/wings/` | The original Wings 3D files of the models (2014–2015), see its [README](../art/wings/README.md) |
| `tools/levelgen/` | Level generator: levels written as Node.js scripts |
| `tools/garagegen/` | Builds the 3D garage behind the menus and the 3D logo of the main menu (`content/Garage/`) and the scenery (`content/Scenery/`) from their Wings 3D files (Python; `wings.py` reads the files) |
| `tools/fontgen/` | Builds the font of the menus (`content/Fonts/TripleWheelDisplay.ttf` and its `menuFont<size>.spritefont` in four sizes) from Orbitron (Python with fontTools) |
| `tools/CatalogRender/` | Renders images of blocks for the catalog and checks the catalog images |
| `tools/CollisionGen/` | Generates the collision shapes from the models (run by the build of the game), see its [README](../tools/CollisionGen/README.md) |
| `tools/ContentPipeline/` | Extension of the content pipeline: keeps the metal of the materials and the lamps of the cars and the street lamps (built and loaded by the content build) |
| `tests/TW3D.Tests/` | Tests of the content, the score and the tutorial (see [Building](building.md#tests-and-ci)) |
| `.github/workflows/` | The build of every push on GitHub Actions |
| `docs/bachelor-thesis/` | Original documentation from the bachelor's thesis (in Czech, describes the 2016 state) |
| `docs/catalog/` | Catalog of track blocks and dynamic objects. Open `catalog.html` in a browser. |

## Code structure (game)

| Folder | Contents |
|---|---|
| `Screens/` | Screen stack (`ScreenManager`), the fonts (`GameFonts`; `GameFont`: the font of the menus in several sizes, drawn from the one that fits), the items of a menu and their focus (`MenuItems`) and the logo, background, main menu, level select, garage, about, options, pause and results screens |
| `GamePlay/` | `GameplayScreen`: one level with the car, physics, camera and debug view, also the test drive of the level editor (the level from memory, from any cell, without the tutorial and the records); `Race`: a run of the level (countdown, time, finish) and `RaceHud`, what it shows on the screen, and `Speedometer`, the dial of the speed in km/h drawn by the code; `CarDrag`: the car dragged by hand; `DevelopmentKeys`: the keys for development; `Tutorial`: the lessons of the tutorial and their state, and `TutorialCard`, its card on the screen; `PhysicsClock`: fixed-step simulation; `RaceScore`: the score and stars of a run |
| `GameObjects/` | The car, the level (built from a level file, or the level editor's data in a test drive, and `LevelAssets`: the catalog, the models of its blocks, objects and scenery and the collision shapes, loaded once; `SceneryProp` places a prop of the scenery), dynamic objects, the skid marks of the tyres (`SkidMarks`), the smoke of the tyres and the exhaust (`TyreSmoke`, `ExhaustSmoke`, both clouds of `Smoke`) and the 3D garage behind the menus with the 3D logo of the main menu (`GarageScene`) |
| `Graphics/` | Camera following the car (3D view and debug view), the renderer of the 3D scene (`SceneRenderer`: lighting, shadows and the sky; `Lighting` and `SkyLook`: the moods; `Paint`: the colours the painted parts of a model are drawn in; `Surface`, `SurfaceNoise` and `SurfaceDetail`: the surfaces drawn per pixel; `SettingsGraphics`: what the settings set up in it), frame rate counter |
| `Input/` | Unified keyboard / mouse / touch / gamepad input (with the device used last) and the on-screen controls |
| `Audio/` | The sound effects: `Synth` (the arithmetic of tones, noise, filters and reverb), `SoundBakes` (the recipes), `GameSounds` (played by the screens) |
| `Editor/` | The level editor (`--editor`), left out of the Android build. The document: `EditedLevel` (the level being edited, without MonoGame, every change a command with undo), `LevelReport` and `RoadGap` (its problems and the gaps of its road). The screen: `EditorScreen` (routes the input and draws the parts), `EditorCamera` (the free camera driven by the mouse), `LevelView` (the level drawn without physics), `EditorMarkers` (the cursor, the grid, the finish and the selections drawn over the scene) and `HiddenLayerGuard`. The tools: `BlockTool` (Place), `ObjectTool`, `SelectTool` and `SceneryTool`, with `EditorTool` and `ChosenObject`. The windows: `PaletteWindow`, `InspectorWindow`, `LevelWindow`, `HelpWindow`, `StatusBar`, `EditorMenus` and `InterfaceLayout` (the font, the style and the dock space). The session: `LevelSession` (opens, saves and guards the level), `LevelFolder`, `FileDialogs`, `PendingAction` and `EditorSettings` (the recent levels and the camera), and `TestDriveStart` (hands the level to a `GameplayScreen`). The previews: `BlockPreviews` (the turning 3D images of the palette) and `ModelBounds`. The renderer of ImGui: `ImGuiRenderer` (Dear ImGui drawn by MonoGame, from the `ImGui.NET` package). |

## Frames and physics steps

The game draws one frame per refresh of the monitor (e.g. 75 frames per second
on a 75 Hz monitor), with 8× multisample antialiasing on Windows and 4× elsewhere (Options > Antialiasing switches
it off; the screen gets its samples when the window is made, so a switch comes with the next start)
(MonoGame does not lower the count, and with more samples than the graphics card has
there is no window at all); a Raspberry Pi draws the 3D scene at 1280×720 without
multisampling instead, scaled up under the user interface (`PiGraphics`, see
[Building](building.md#linux-and-a-raspberry-pi)). The Resolution of Options does the same
anywhere: below native, `SceneRenderer.End` draws the scene into a render target of its own,
multisampled as the screen is (not on Android: MonoGame resolves a multisampled target by
`glBlitFramebuffer`, which it lacks there), at a height of `RenderResolution` (the exact shape of the
screen), and scales it up to the screen with one bilinear sample; the ambient occlusion and
the sky follow the size of the target, and the menus and the display of a run are drawn over
it at the size of the screen. At native the scene is drawn straight on the screen, as before. The physics simulation is independent of that: it always
runs 60 fixed steps of 1/60 s per game second, and the positions of moving bodies are
interpolated between the last two steps for drawing.

The game runs 1.5 times as fast as real time (`GameSpeed` in `GameplayScreen`). For the
physics engine the world is huge (the car is about 20 metres long), so at real time
everything moved as in slow motion; running faster is the same as shrinking the whole
world while keeping its look. Everything keeps its paths, so the levels play the same,
only faster.

## Lighting

The models are lit by the game's own shader (`content/Effects/Scene.fx`): the sun with
its shadows (a shadow map fitted to the part of the scene the camera sees, from the front
layer to the first back layer, and a coarser one beside it in the same texture for the
deeper back layers, drawn only while the camera sees something there; each covers the box
around that part in the view of the sun, its size of the Shadow detail of Options, its
edges filtered smoothly, but on phones, whose half precision keeps a coarser filter), the light of
the sky from above and of the ground from below, darkened in corners and crevices by
ambient occlusion (`content/Effects/Occlusion.fx`, computed at half the resolution of the
screen from the normals and depths the camera sees), highlights by the specular colour of
the materials and a rim of sky light. The light is computed in linear colour space and
mapped to the screen by a filmic curve, and the distance fades into the colour of the
sky.

Glass (a material with an opacity `d` below 1 in its `.mtl` file: the windows of the
cars) lets the scene through, except where it reflects the light. Metal (the rims of the
wheels, the engines, the tubes) has no diffuse light, it mirrors the sky, the ground and
the sun in its own colour, sharply if it is smooth (chrome), blurred if it is rough.

The lamps of the cars shine in their emissive colour (`Ke`) only when they are on: the
headlights at night, the rear lights dimly with them and fully when the car brakes (in
any weather), each with a glow around it that the wheels of the car do not hide. Each
lamp also lights the scene as a spot light of its colour: the two headlights ahead, their
beams a little apart, the rear lights red behind the car. The street lamps of the scenery
(the lens of the material `streetlight…`) shine at night with a glow too, and the four nearest
to the camera light the road and the props below them as spot lights pointing down, warm
white: `Scene.fx` has eight lights, four of the car and four of the street lamps. The lit
windows of a building (`windowlit…`, the judges' stand) shine at night evenly, without a glow,
and light nothing. The
start lights of the start gate count the start down in any weather (`SceneRenderer.StartLights`,
set from `Race.RedSignals` by the run, off everywhere else): five pairs of red lamps, as in
Formula 1, come on pair by pair during the countdown and go out at the start; `RaceHud`
draws the same row of five lights in the place of the time.

The 2016 version lit the models by three coloured directional lights of
`BasicEffect` (F9 switches to it). The level editor draws the level the same way as the
game (`SceneRenderer` is shared; F9 and F10 work there too, M switches the mood of the
level, which is saved with it), CatalogRender with the 2016
lighting, as the catalog images were taken so (and with the renderer of the game for the
images of a test surface, `render --surface`).

### Surfaces

A part of a model whose material names a surface (`asphalt…`, `earth…`, `grass…`, `Graphics/Surface`) gets
its detail per pixel (#58): its colour varies (patches, the stones of the aggregate, clods,
pebbles) and its relief tilts the normal, so that it catches the light; its shape, the light and
the shadows stay those of the scene, and the shadow map, the rim and the ambient occlusion keep
the normal of the face. The detail is computed, not a picture of a surface: a small texture of
noise the game computes when it starts (`Graphics/SurfaceNoise`: mottle, the slope of a grain
and cellular noise of points of many sizes, 256 × 256 texels with their mipmaps, the same every
time), which the shader composes into a surface (`content/Effects/Surfaces.fxh`, included by
`Scene.fx`). A texture, not noise computed in the shader as in BS3D, because a phone's pixel
shader computes in half precision, where hashes fall apart into patterns; a lookup costs
little on the phone and the Pi, and the mipmaps fade the detail out in the distance instead of
letting it shimmer (the edges of the stones are softened by the footprint of the pixel, as
multisampling smooths only the edges of polygons).

The noise is laid on a face by the cell of the level grid the model stands in: the vertex
shader passes the position from the middle of that cell (`SurfaceOrigin`), in numbers small
enough for half precision, and the face takes the noise on the plane it is turned to most
(the top on x and z, the front on x and y, the sides on z and y), once per cell and four times
per cell (the coarse and the fine lookup). The noise repeats exactly once per cell, so a surface
joins the surface of the next block without a seam, a turned block included; what varies from
block to block comes from random values of the corners of the cells (`SurfaceNoise.CornerSeed`),
which neighbours share, blended across the cell. Each surface has a technique for each detail
(the Surface detail of Options: Low one lookup and no relief, Medium two, High three) and each
filter of the shadows, `Lit<Surface><Detail>` and `LitCoarseShadows<Surface><Detail>`, so that
nothing branches in the pixel shader (Shader Model 3.0 refuses a derivative in a branch).

The asphalt is a test of the machinery; the earth and the grass draw the off-road style of the track (#57),
whose blocks have the shape of a dirt road (ruts, a crown, grass verges) and name it by their
materials. The racing style (#56) adds its surfaces (a kerb, grass) as further techniques.

The earth of the shoulder and the grass of the verge of an off-road block meet along the edge of
their faces, 3.35 model units from the middle of the road (`tools/blender/offroad.py`), but are not
drawn so: both techniques compute the look of the earth and of the grass from the same lookups
and blend them by how far the point is across the road, ragged by the mottle of the noise
(`SurfaceVergeGrass`), so that the grass reaches into the earth and the earth shows through the
grass over about a model unit. The face of either computes the same at the edge they share (the
colours of both come in `VergeEarth` and `VergeGrass`, as the generator makes the materials), so
no line shows there. The blend costs arithmetic, not lookups; a test surface (T) has none
(`SurfaceVerge` 0).

### Mud on the car

The car gets muddy on the earth (#60). `GameObjects/CarMud` keeps how much mud the wheels of
each axle, the treads of their tyres, the holder of the axle and the body have, 0 clean to 1; the
wheels on the ground report to it as they throw the earth up (`DirtSpray`, which measures how
fast each slides, how fast the car rolls and how hard a wheel lands). On the earth: a little
standing, more rolling fast, much more sliding or spinning, a splash at a hard landing, half on a
transition block; the treads twice as much, the body a quarter. Off the earth the wheels wash
clean as they roll and spin (with two wheels of an axle on the ground about a tenth of their mud
a second at full speed, the treads one and a half times as fast); the body and the holders keep
theirs. Each amount grows by a part of what is still clean and washes by a part of what is muddy,
so it stays between 0 and 1; a new run (a new `Car`) and the garage start clean. The mud is wet
when fresh and dries: each part has a wetness, which the fresh mud of the earth raises (driving
on the earth keeps it wet) and which falls by a twentieth a second (`CarMud.Dry`), so the car is
dark as it comes off the earth, gets lighter as the mud dries, and dark again from the next
earth; the shader darkens wet mud and gives it a little gloss, thick mud staying wet longer.

What the treads wipe off is left on the road: `GameObjects/TyreMarks` lays the marks of the
tyres, the dark skid marks where they slip and the tracks of the mud where a wheel with mud on
its tread rolls off the earth, light brown smears (each piece a little more or less covering, at
random) as strong as the square root of the mud on the treads, so they fade as the treads wipe
clean. Both lie on what the wheel stands on, the track or the surface of an object's model, and
move with it; the renderer draws them in a haze of the light of the scene, so a brown track is
dark at night. A mark grows by a piece sooner where the ground turns, so that it follows a bend,
and starts anew where the wheel goes up or down a step or round a sharp corner, so that no piece
spans the air between two faces of the stairs.

`Car.Draw` adds its models with a `Graphics/Mud` (the amount, and for the body where its axles
are in its model, where the physics holds them), and the renderer draws their opaque parts but
the lamps by the techniques `LitMud<Detail>` and `LitCoarseShadowsMud<Detail>`
(`content/Effects/Mud.fxh`): the same light, with the mud laid over the colour and the finish of
the material per pixel, matt, a little lighter where it is thin and darker where thick (no more,
or a patch near the edge of the mud looks of another colour), with a little relief at the High
detail; none at the Low one (the Raspberry Pi). The mud takes the place of the whole finish of
the material (its highlight and its own light too, so a glossy stripe under it is mud like the
rest). It takes less of the light of the sun and more of the sky and the ground than a painted
face (`MUD_SUN`, `MUD_AMBIENT`), the light of the sky half in the hue of the sun: its crumbs
scatter the light evenly, and taken fully, the faces turned to the sun looked of a lighter,
warmer mud than the faces in the shade, grey under the blue sky. The mud is laid in the model's own coordinates,
not the world's, so it stays put on a turning wheel and needs nothing marked in the models: a
wheel and a holder get it all over, the tyre more than the middle, the rims less, and the tread
(the outermost faces of a wheel turned away from its axis) has the amount of the treads, which
the road wipes first; the body first around and behind its axles and on its lower parts, then further up
as the amount grows. The cars share the layout of the physics and are about the same size, so
the same distances (in model units) suit every car and wheel. Splashes rather than a layer:
blotches where the mottle of the noise of the surfaces, laid on the model, reaches above an edge
that drops as the amount grows, and specks of its cellular noise around them. The vertex shader
passes the position in the model in the outputs the surfaces use for theirs (`Local`, `Plane`:
the coordinates of the noise and how much of the tread of a wheel the point is), so the mud needs
no more of them, and the numbers stay small for half precision. The pixel shader must not read the
`World` matrix: MonoGame sizes a parameter the shaders share by its first use, and a pixel shader
reading a part of it got it wrong, which changed the light of every part of a car as soon as any
of it had mud (the relief of the mud tilts the normal in the world, as the surfaces do).

### Styles of the track

A level, and any block of it, has a style (#58): the classic one of 2016 or `offroad`, a dirt
road. A style only changes the model a block is drawn with, its look (`BlockType.Looks` in the
catalog, `BlockType.LookAsset`): `Level` builds the physics of a block from the shape of its
own model as before and draws the model of its look in the style of the block or of the level
(`LevelData.StyleOf`), and the editor draws the same (`LevelView`). Where the road passes
from one style into another, the block of the styled side is drawn as the transition between
them (`offroad-classic`, a look of its own): `Shared/RoadLooks` finds it from the road as the
level editor joins it (a road block and the road block of the next column at the height of its
edge), for the game and the editor alike, so the levels do not name the transitions. A
transition given to a block in the level stays.

### The sky

The sky has no picture: the technique Sky of `Scene.fx` computes it for every pixel of
the screen from the direction the pixel looks in (`SceneRenderer.SetSky`, drawn behind
everything, the same in the lighting of 2016). Each mood has its `SkyLook`
(`Graphics/SkyLook.cs`, in `Lighting`): a smooth ramp from the colour of the horizon (the
colour of the haze) to the colour overhead, a glow and a disc of the sun (the moon at
night), stars at night, clouds on a plane high above, distant hills along the horizon and
the ground below them. The clouds, the stars and a dithering against bands in the ramps
come from a small noise texture the renderer generates when it starts (soft tiling value
noise for the clouds, a random value per texel for the rest), so nothing of the sky is
loaded and it costs a few texture reads per pixel, also on a phone. The clouds drift
slowly with the wind.

The levels hang high above the land, and the camera of the game looks down at the car:
their horizon lies 40° below the eye (`SceneRenderer.HorizonDip`), so that the sky fills
the view behind the track and the land shows only far below it, as the picture of the sky
of 2016 had it. The garage stands on the ground, its windows look out at the sky of a day
with its horizon at the eye.
