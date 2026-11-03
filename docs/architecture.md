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
| `content/` | Shared game content: 3D models (`.obj` + `.mtl`, built by the content pipeline `Content.mgcb`), images of the user interface and previews of the levels (`.png`), precompiled fonts (`.xnb`), the catalog (`catalog.json`), the collision shapes of the models (`collision.json`, generated) and levels (`Levels/*.json`). Built and copied to the build output as `Content/`. See [Game content](content.md). |
| `art/wings/` | The original Wings 3D files of the models (2014–2015), see its [README](../art/wings/README.md) |
| `tools/levelgen/` | Level generator: levels written as Node.js scripts |
| `tools/garagegen/` | Builds the 3D garage behind the menus and the 3D logo of the main menu (`content/Garage/`) and the scenery (`content/Scenery/`) from their Wings 3D files (Python; `wings.py` reads the files) |
| `tools/fontgen/` | Builds the font of the menus (`content/Fonts/TripleWheelDisplay.ttf` and its `menuFont<size>.spritefont` in four sizes) from Orbitron (Python with fontTools) |
| `tools/CatalogRender/` | Renders images of blocks for the catalog and checks the catalog images |
| `tools/CollisionGen/` | Generates the collision shapes from the models (run by the build of the game), see its [README](../tools/CollisionGen/README.md) |
| `tools/ContentPipeline/` | Extension of the content pipeline: keeps the metal of the materials and the lamps of the cars (built and loaded by the content build) |
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
| `Graphics/` | Camera following the car (3D view and debug view), the renderer of the 3D scene (`SceneRenderer`: lighting, shadows and the sky; `Lighting` and `SkyLook`: the moods), frame rate counter |
| `Input/` | Unified keyboard / mouse / touch / gamepad input (with the device used last) and the on-screen controls |
| `Audio/` | The sound effects: `Synth` (the arithmetic of tones, noise, filters and reverb), `SoundBakes` (the recipes), `GameSounds` (played by the screens) |
| `Editor/` | The level editor (`--editor`), left out of the Android build. The document: `EditedLevel` (the level being edited, without MonoGame, every change a command with undo), `LevelReport` and `RoadGap` (its problems and the gaps of its road). The screen: `EditorScreen` (routes the input and draws the parts), `EditorCamera` (the free camera driven by the mouse), `LevelView` (the level drawn without physics), `EditorMarkers` (the cursor, the grid, the finish and the selections drawn over the scene) and `HiddenLayerGuard`. The tools: `BlockTool` (Place), `ObjectTool`, `SelectTool` and `SceneryTool`, with `EditorTool` and `ChosenObject`. The windows: `PaletteWindow`, `InspectorWindow`, `LevelWindow`, `HelpWindow`, `StatusBar`, `EditorMenus` and `InterfaceLayout` (the font, the style and the dock space). The session: `LevelSession` (opens, saves and guards the level), `LevelFolder`, `FileDialogs`, `PendingAction` and `EditorSettings` (the recent levels and the camera), and `TestDriveStart` (hands the level to a `GameplayScreen`). The previews: `BlockPreviews` (the turning 3D images of the palette) and `ModelBounds`. The renderer of ImGui: `ImGuiRenderer` (Dear ImGui drawn by MonoGame, from the `ImGui.NET` package). |

## Frames and physics steps

The game draws one frame per refresh of the monitor (e.g. 75 frames per second
on a 75 Hz monitor), with 8× multisample antialiasing (fewer samples if the
graphics card does not support 8). The physics simulation is independent of that: it always
runs 60 fixed steps of 1/60 s per game second, and the positions of moving bodies are
interpolated between the last two steps for drawing.

The game runs 1.5 times as fast as real time (`GameSpeed` in `GameplayScreen`). For the
physics engine the world is huge (the car is about 20 metres long), so at real time
everything moved as in slow motion; running faster is the same as shrinking the whole
world while keeping its look. Everything keeps its paths, so the levels play the same,
only faster.

## Lighting

The models are lit by the game's own shader (`content/Effects/Scene.fx`): the sun with
its shadows (a shadow map fitted to the part of the scene the camera sees), the light of
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
beams a little apart, the rear lights red behind the car.

The 2016 version lit the models by three coloured directional lights of
`BasicEffect` (F9 switches to it). The level editor draws the level the same way as the
game (`SceneRenderer` is shared; F9 and F10 work there too, M switches the mood of the
level, which is saved with it), CatalogRender with the 2016
lighting, as the catalog images were taken so.

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
