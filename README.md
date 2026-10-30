# Triple Wheel 3D (TW3D)

An obstacle course driving game with 3D graphics and 2D physics: a car with
rotating axles has to get through the track by rotating and locking its axles.
The project was created in 2016 as an appendix to a bachelor's thesis (sample
game + level editor) and was migrated to current tools in 2026.

The game is built on the [MonoGame](https://monogame.net/) framework and the
[Aether.Physics2D](https://github.com/nkast/Aether.Physics2D) physics engine
(the maintained successor of Farseer Physics, which the game originally used).
The physics runs in 2D, and the 3D models are drawn at the positions of the
physics bodies.

## Repository structure

| Path | Contents |
|---|---|
| `src/TW3D.Game/` | The game (`TW3D.exe`) |
| `src/TW3D.LevelEditor/` | Level editor (`LevelEditor.exe`), Windows only |
| `src/TW3D.Shared/` | Library shared by the game, the editor and the tools: the catalog of blocks and objects, the level file format, the collision shapes and the units of the coordinates (`Units`: the models, the physics world and the scene) |
| `content/` | Shared game content: 3D models (`.obj` + `.mtl`, built by the content pipeline `Content.mgcb`), precompiled textures and fonts (`.xnb`), the catalog (`catalog.json`), the collision shapes of the models (`collision.json`, generated) and levels (`Levels/*.json`). Built and copied to the build output as `Content/`. |
| `art/wings/` | The original Wings 3D files of the models (2014–2015), see its [README](art/wings/README.md) |
| `tools/levelgen/` | Level generator: levels written as Node.js scripts |
| `tools/garagegen/` | Builds the 3D garage behind the menus (`content/Garage/`) from its Wings 3D files (Python) |
| `tools/CatalogRender/` | Renders images of blocks for the catalog and checks the catalog images |
| `tools/CollisionGen/` | Generates the collision shapes from the models (run by the build of the game), see its [README](tools/CollisionGen/README.md) |
| `tools/ContentPipeline/` | Extension of the content pipeline: keeps the metal of the materials and the lamps of the cars (built and loaded by the content build) |
| `docs/bachelor-thesis/` | Original documentation from the bachelor's thesis (in Czech, describes the 2016 state) |
| `docs/catalog/` | Catalog of track blocks and dynamic objects. Open `catalog.html` in a browser. |

## Requirements

- [.NET SDK 10](https://dotnet.microsoft.com/download) or newer
- Optionally an IDE: Visual Studio 2022 (17.13+) / 2026, JetBrains Rider or VS Code with C# Dev Kit

Packages (MonoGame 3.8 DesktopGL, Aether.Physics2D 2.2) are restored
automatically from NuGet. Their versions are managed centrally in
`Directory.Packages.props`. The build also restores the content pipeline tool
(`dotnet-mgcb`, listed in `.config/dotnet-tools.json`) and builds the models with it.

## Building and running

```sh
dotnet build TW3D.slnx

# game
dotnet run --project src/TW3D.Game
dotnet run --project src/TW3D.Game -- --windowed --level lock

# level editor
dotnet run --project src/TW3D.LevelEditor
dotnet run --project src/TW3D.LevelEditor -- --level content/Levels/lock.json
```

Game arguments for development:

| Argument | Meaning |
|---|---|
| `--windowed` | 1280×720 window instead of full screen |
| `--size WxH` | Window of the given size, e.g. `--size 2560x1080` for testing other aspect ratios (the window can also be resized) |
| `--level <name>` | Skips the menu and loads the level directly (the file name in `content/Levels` without `.json`, e.g. `lock`). Esc returns to the menu. |

### Android

`src/TW3D.Android` builds the game for Android phones (Android 7.0 and newer). It
compiles the code of `src/TW3D.Game` (linked, not copied); what differs between the
platforms is marked `#if ANDROID`: full screen in landscape, a twice as big user
interface, 4× multisampling, the level list read from the app and the highest
refresh rate of the display. The project is not in `TW3D.slnx`, so the desktop build
needs no Android tools.

Once, the Android workload, the Android SDK and JDK 17 (the SDK in
`%LOCALAPPDATA%\Android\Sdk` is found automatically, the JDK has to be given):

```sh
dotnet workload install android
dotnet build src/TW3D.Android -t:InstallAndroidDependencies -f net10.0-android -p:AndroidSdkDirectory=<SDK folder> -p:JavaSdkDirectory=<JDK folder> -p:AcceptAndroidSDKLicenses=True
```

Installing on a phone connected by USB with USB debugging enabled:

```sh
dotnet build src/TW3D.Android -t:Install -p:JavaSdkDirectory=<JDK folder>
```

The debug build installs the code next to the app (fast deployment), so its APK
file alone does not run. A standalone APK comes from the release build, with the
code compiled ahead of time for arm64 (phones) and x86_64:

```sh
dotnet publish src/TW3D.Android -c Release -p:JavaSdkDirectory=<JDK folder>
```

It writes `src/TW3D.Android/bin/Release/net10.0-android/publish/io.github.antoninprazsky.tw3d-Signed.apk`
(and an `.aab` bundle for Google Play). Copy the APK to a phone and open it there
(the phone asks to allow installing apps from that source), or install it with
`adb install`. It is signed with the Android debug key of the computer that built
it; an app signed with another key (another computer, or a release key for Google
Play) cannot update it, the old one has to be uninstalled first.

## Controls

The user interface is designed for desktop monitors at 1920×1080 and scaled
uniformly to the screen, so it fits any aspect ratio (16:9, 21:9, 4:3, …).
It keeps a margin of 48 px (at 1080p) from the screen edges; the level select
uses as many columns as fit the screen.

**Menus:** mouse (click, wheel and dragging in the level select), arrow keys and
Enter, a gamepad (D-pad, A) or touch. Esc (the gamepad Back button, the back
gesture or button of a phone) goes back; in the main menu it quits the game.

**Garage:** the cars to choose from, one at a time; dragging the car sideways (with a
finger or the mouse) or the arrows next to the name (or left and right) show the others.
The car shown is the one to drive. On the right its wheels are chosen, for the front and
the rear axle each (any wheels fit any car; up and down choose the row, left and right the
wheels); on the left bars show how fast, heavy and gripping the car is with them. The cars
differ in their top speed and weight (the Sand Rail is faster and lighter than the Buggy),
the wheels in their grip and weight (the off-road wheels grip better, the racing ones are
lighter). The choices are saved with the options, the wheels for each car.

**About:** what the game is, who made it and the link to its source code on GitHub, which
opens in the web browser (clicked, or chosen by the keys and Enter).

**Options:** full screen or a window (on a desktop; the window opens maximized and
can be resized), the look of the game (new, or classic: as the 2016 version looked),
shadows, ambient occlusion and the frame rate (frames per second in the top left
corner). With some graphics drivers the game stutters in full screen (measured on an
AMD Radeon with OpenGL), but not in a window. They are saved in `TW3D/settings.json` in the
user's local application data (on Windows `%LOCALAPPDATA%\TW3D`).

**Driving:** the keyboard, the on-screen controls (operated by the mouse or by
touch) and a gamepad all work at the same time.

| Key | On screen / gamepad | Action |
|---|---|---|
| → / ← | joystick right / left, left stick | Accelerate / reverse |
| ↑ / ↓ | joystick up / down, left stick | Rotate the axles clockwise / counterclockwise |
| Space | brake button, A | Brake all wheels |
| Left Ctrl | axle lock button, B | Lock the axle rotation |
| Esc, P | pause button (top right), Back, the back gesture of a phone | Pause: resume, restart, the level select or the main menu |

The car can also be grabbed with the mouse and dragged around. The game also pauses
when its window loses the focus, or when a phone switches to another app.

**A run of a level** starts with a countdown (3, 2, 1, Start), while the car is held
still. The time at the top runs from the start until the car reaches the finish of the
level (its finish block, see the level format); below it is the par time of the level,
the time a good run takes. The score is 1000 × par time / time: finishing in the par
time scores 1000 points, faster more and slower less (half the time, twice the points).
A run within the par time gets three stars, within one and a half of it two, otherwise
one. The results offer the level select, the level again and the next level in a row
along the bottom of the screen (left and right choose between them). The best
time of each level is kept (in `TW3D/records.json` next to the settings), and the level
select shows it with its stars. A run in which the car was dragged by hand does not count.

Development keys while driving:

| Key | Action |
|---|---|
| F1–F8 | Physics simulation debug view (Aether.Physics2D DebugView), gamepad Start toggles most of it |
| Num 8 / 2 / 4 / 6 | Move the camera away from the car |
| Page Up / Page Down | Zoom of the debug view |
| Home | Reset the camera |
| F9 | The lighting of the 2016 version, for comparison |
| F10 | Shadows on / off |
| F11 | Ambient occlusion on / off |
| M | Switches the mood of the level (day, sunset, night) to try it; the level file stays as it is |
| F12 | Frames per second on / off (also in Options) |

The editor asks for the level to open, or opens the one given by `--level <file>`.
Its controls are described in
`docs/bachelor-thesis/dokumentace-editoru-trati.pdf` (in Czech). The editor
opens levels in the JSON format as well as old `.lev` files, and saves JSON.
F marks the block of the middle layer under the selector as the finish of the level
(again, it stops being it); P tells whether a block is the finish.
It checks the blocks and objects against the catalog (F1 lists the blocks;
decoration blocks cannot be placed in the middle layer). A new level saved to
`content/Levels/` appears in the level select automatically (after a rebuild,
which copies it to the build output). Its preview image is loaded from
`content/Levels/levimgs/<name>.xnb`; a level without one is shown as a dark
rectangle with its name.

## Code structure (game)

| Folder | Contents |
|---|---|
| `Screens/` | Screen stack (`ScreenManager`) and the logo, background, main menu, level select, garage, about, options, pause and results screens |
| `GamePlay/` | `GameplayScreen`: one level with the car, physics, camera, debug view and the run (countdown, time, finish); `PhysicsClock`: fixed-step simulation; `RaceScore`: the score and stars of a run |
| `GameObjects/` | The car, the level (built from a level file and `LevelAssets`: the catalog, the models of its blocks and objects and the collision shapes, loaded once), the sky, dynamic objects and the 3D garage behind the menus (`GarageScene`) |
| `Graphics/` | Camera following the car (3D view and debug view), the renderer of the 3D scene (`SceneRenderer`: lighting and shadows), frame rate counter |
| `Input/` | Unified keyboard / mouse / touch / gamepad input and the on-screen controls |

The game draws one frame per refresh of the monitor (e.g. 75 frames per second
on a 75 Hz monitor), with 8× multisample antialiasing (fewer samples if the
graphics card does not support 8). The physics simulation is independent of that: it always
runs 60 fixed steps of 1/60 s per game second, and the positions of moving bodies are
interpolated between the last two steps for drawing.

The models are lit by the game's own shader (`content/Effects/Scene.fx`): the sun with
its shadows (a shadow map fitted to the part of the scene the camera sees), the light of
the sky from above and of the ground from below, darkened in corners and crevices by
ambient occlusion (`content/Effects/Occlusion.fx`, computed at half the resolution of the
screen from the normals and depths the camera sees), highlights by the specular colour of
the materials and a rim of sky light. The light is computed in linear colour space and
mapped to the screen by a filmic curve, and the distance fades into the colour of the
sky. Glass (a material with an opacity `d` below 1 in its `.mtl` file: the windows of the
cars) lets the scene through, except where it reflects the light. Metal (the rims of the
wheels, the engines, the tubes) has no diffuse light, it mirrors the sky, the ground and
the sun in its own colour, sharply if it is smooth (chrome), blurred if it is rough. The
lamps of the cars shine in their emissive colour (`Ke`) only when they are on: the
headlights at night, the rear lights dimly with them and fully when the car brakes (in
any weather), each with a glow around it that the wheels of the car do not hide. Each
lamp also lights the scene as a spot light of its colour: the two headlights ahead, their
beams a little apart, the rear lights red behind the car. The 2016 version lit the models by three coloured directional lights of
`BasicEffect` (F9 switches to it). The level editor draws the level the same way as the
game (`SceneRenderer` is shared; F9 and F10 work there too, M switches the mood of the
level, which is saved with it), CatalogRender with the 2016
lighting, as the catalog images were taken so.

The game runs 1.5 times as fast as real time (`GameSpeed` in `GameplayScreen`). For the
physics engine the world is huge (the car is about 20 metres long), so at real time
everything moved as in slow motion; running faster is the same as shrinking the whole
world while keeping its look. Everything keeps its paths, so the levels play the same,
only faster.

## Game content

The **3D models** are in `content/` as `.obj` + `.mtl` files (Wavefront OBJ, a text
format that Blender and most 3D programs open and save). The MonoGame Content
Pipeline builds them into `.xnb` files whenever the game, the editor or
CatalogRender is built: `content/Content.mgcb` lists them, and the projects run it
through the `MonoGame.Content.Builder.Task` package. The models have no textures,
they are made of coloured polygons only (the colour is the diffuse colour `Kd` of
the material); only the sky has a texture (`Env/sky.png`). The materials of metal parts
also have the metallic `Pm` (0–1) and the roughness `Pr` (0–1, 0 = a mirror) of the PBR
extension of MTL. The importer of MonoGame leaves them out (it reads only what XNA's
`BasicEffect` has), so the models are processed by `MtlModelProcessor` of
`tools/ContentPipeline`, which reads them from the `.mtl` file and keeps them with the
model. It also marks the lamps of the cars, by the names of their materials: the lenses
of the headlights are named `headlight…`, of the rear lights `taillight…`. Each lamp is
an object of its own in the `.obj` file (`HeadlightLeft` …), the game finds its middle
as the middle of the object.

The models were modelled in Wings 3D in 2014–2015 and exported to DirectX `.x`
files, which XNA 4.0 compiled for the 2016 version. In 2026 those exports were
converted to OBJ: the same polygons, normals and material colours, in the
right-handed coordinates of the game (DirectX `.x` is left-handed, the XNA importer
mirrored the z axis). Builds of the converted models were checked against the
2016 `.xnb` files: the same surfaces and colours, they look the same. The
original Wings 3D files are in [`art/wings/`](art/wings/README.md).

The **garage** behind the menus (`content/Garage/`) is 3D since 2026; in 2016 it was a
picture rendered from the garage scene of 2015 (`art/wings/Garage/garage.wings`). The
hall is new, generated by `tools/garagegen/build.py` (Python 3 with NumPy): the floor, the
walls, the garage door and the lines of the parking bay. From the Wings 3D files it takes
the windows, the lift, the furniture and the tools, and the poles outside, with smooth
normals by the hard edges of Wings and metal materials where there was metal. The
landscape behind the windows is the sky of the levels (`Env/sky.png`). Run the script after
changing the `.wings` files or the script and commit its output. The car chosen in the
garage stands on the lift facing the camera, the other cars in a row along its axis in the
order of the catalog (the next ones behind it). The menus look into the garage as the
picture of 2016 did; the garage screen moves the camera to the car, and to the wheels of
the axle being chosen.

The other files are still the **precompiled** `.xnb` files from XNA 4.0, without
their sources in the repository: the images of the user interface (`Common/`),
the level previews (`Levels/levimgs/`) and the fonts (only the font descriptions
`*.spritefont` are here).

### Editing models

In Blender: *File → Import → Wavefront (.obj)*, edit, then *File → Export →
Wavefront (.obj)* over the original file. The default settings work: the import
converts the axes (OBJ has Y up, Blender Z up) and the export converts them back.
Keep the export of normals and materials on and *Triangulated Mesh* off (the
content pipeline triangulates the polygons itself); turn *PBR Extensions* on, so that
the metallic and the roughness of the materials are kept. The next build of the game
builds the changed model and generates its collision shape again (commit
`content/collision.json` with the model).

### Catalog

`content/catalog.json` lists everything the levels are built from, for both the
game and the editor:

- **blocks**: the name (model `Levels/mod/<name>`) and whether the block has a
  collision shape (`collision`). Only blocks with a collision shape can be in the
  middle (physics) layer; the others are decoration for the back and front layers.
- **objects**: dynamic object types with their shape (`rectangle`, `circle`, or
  `model`: the collision shape of the model), size, friction, mass, restitution and
  the models they can use (`Env/<model>`).
- **cars**: the cars of the garage: the name, the model of the body, the wheels it comes
  with, the mass of the body and the top speed (how fast the motors turn the wheels at
  most, radians per second). All cars have the same chassis (the holders of the wheels,
  `holder`), so any wheels fit any car.
- **wheels**: the wheels of the garage: the name, the model, the friction of the tyre (its
  grip on the track, whose friction is 1) and the mass of one wheel.

A new block, object, car or wheel only needs its model in `content/` (and a `/build` line
in `content/Content.mgcb`) and an entry in the catalog.

### Collision shapes

The physics shapes of the blocks, the dynamic objects and the cars are generated
from their models by [`tools/CollisionGen`](tools/CollisionGen/README.md) into
`content/collision.json`: convex polygons, whatever of a model a car can touch
as it drives in the plane z = 0. The build of the game runs it whenever a model
or the catalog changes. The cars are up to 4.35 model units wide across the plane:
their bodies touch what is within |z| ≤ 1.49 of a block, their wheels at the sides
what is within 1.42 ≤ |z| ≤ 2.18, so the arch of a tunnel is lower for the wheels
than for the body, and the pillars of gates at the sides of the road are left out.

Until 2026 the shapes were traced from collision bitmaps (screenshots of the models
from Wings 3D, some edited by hand) every time a level loaded; they are kept as
images in `docs/catalog/bl/cols/`.

### Level format

Levels are JSON files with one block or object per line:

```json
{
  "format": 1,
  "order": 1,
  "mood": "sunset",
  "middle": [
    {"block":"a1","x":0,"y":0,"shift":0},
    ...
  ],
  "back": [ ... ],
  "front": [ ... ],
  "objects": [
    {"type":"Bar","model":"bar1","x":238,"y":-4},
    ...
  ]
}
```

- `order` (optional): position in the level select. Levels without it come after
  the others, sorted by name.
- `mood` (optional): the light of the level: `day` (without it), `sunset` (a low
  orange sun and long shadows) or `night` (moonlight, the headlights of the car on).
  In the editor, M switches it.
- `par` (optional): the time a good run takes, in seconds (see a run of a level under
  Controls). Without it the game estimates it: half a second for each grid cell the
  finish is from the start (across and up or down), at least 10 seconds.
- `middle`: blocks of the physics layer, `back` and `front`: decoration layers.
- Blocks: `x`, `y` on the level grid (one cell = 22.5 physics units), `shift`
  moves the block vertically in steps of 2.5 physics units. `"finish": true` makes a
  block of the middle layer the finish of the level (one at most): the run ends when
  the middle of the car reaches its cell. A level without a finish has no end.
- Objects: `x`, `y` in physics units.

The levels lock, startblock and tunel were converted from the original `.lev`
format of the 2016 version, which the editor can still open.

The summit level is generated by a script in `tools/levelgen/`, which keeps the
road continuous and adds the decorations; see its
[README](tools/levelgen/README.md). When the level is changed in the editor,
carry the changes over to the script, otherwise running it again overwrites them.

## History

- The `bachelor-thesis-2016` tag marks the original state of the bachelor's
  thesis appendix (UWP, MonoGame 3.5, Visual Studio 2015), without build
  output and binaries.
- In 2026 the project was migrated to .NET 10 + MonoGame 3.8 DesktopGL. Since
  then the game runs as a regular desktop application instead of a UWP app.
  It has been tested on Windows. DesktopGL also supports Linux and macOS, but
  the game has not been tested there yet.
- The embedded Farseer Physics 3.5 source code was then replaced by the
  Aether.Physics2D NuGet package. The car handles the same in the first
  seconds of a scripted test drive; later the runs diverge slightly, as is
  normal for physics simulations with a different implementation.
- Finally the user interface framework taken over from the Farseer Physics
  samples (screens, menus, input, camera) was replaced by new code, and the user
  interface was made to scale to any aspect ratio (it used to be too big and
  partly off the screen on 21:9 monitors).
- The original model files turned up on an old disk; the models are now built
  from OBJ sources by the MonoGame Content Pipeline instead of being only
  precompiled `.xnb` files (see [Game content](#game-content)).
- The collision shapes are generated from the models while building instead of
  being traced from the collision bitmaps at runtime (see
  [Collision shapes](#collision-shapes)).

## License

[MIT](LICENSE), © 2014–2026 Antonín Pražský: the code, the game content
(`content/`) and the documentation.

The physics engine **Aether.Physics2D** is used as a NuGet package and is licensed
under the Microsoft Public License (Ms-PL). It is based on Farseer Physics
(© Ian Qvist) and Box2D (© Erin Catto).

## Roadmap

Issues found during the migration, roughly in order of importance:

1. **Sources of the other content:** the images and fonts are still only
   precompiled `.xnb` files.
2. **Editor:** default folder `C:\levels`, Windows-only dependencies (WinForms
   dialogs, `keybd_event` for Caps Lock) and controls spread over two windows.
   A possible replacement: UI directly in the editor window (e.g. ImGui.NET).
3. **Automated tests and CI**, e.g. tests of the level format and builds via
   GitHub Actions.
