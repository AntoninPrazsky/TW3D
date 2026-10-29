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
| `src/TW3D.Shared/` | Library shared by the game and the editor: the catalog of blocks and objects and the level file format |
| `content/` | Shared game content: compiled models, textures and fonts (`.xnb`), the catalog (`catalog.json`) and levels (`Levels/*.json`). Copied to the build output as `Content/`. |
| `tools/levelgen/` | Level generator: levels written as Node.js scripts |
| `docs/bachelor-thesis/` | Original documentation from the bachelor's thesis (in Czech, describes the 2016 state) |
| `docs/catalog/` | Catalog of track blocks and dynamic objects. Open `catalog.html` in a browser. |

## Requirements

- [.NET SDK 10](https://dotnet.microsoft.com/download) or newer
- Optionally an IDE: Visual Studio 2022 (17.13+) / 2026, JetBrains Rider or VS Code with C# Dev Kit

Packages (MonoGame 3.8 DesktopGL, Aether.Physics2D 2.2) are restored
automatically from NuGet. Their versions are managed centrally in
`Directory.Packages.props`.

## Building and running

```sh
dotnet build TW3D.slnx

# game
dotnet run --project src/TW3D.Game
dotnet run --project src/TW3D.Game -- --windowed --level lock

# level editor
dotnet run --project src/TW3D.LevelEditor
```

Game arguments for development:

| Argument | Meaning |
|---|---|
| `--windowed` | 1280×720 window instead of full screen |
| `--size WxH` | Window of the given size, e.g. `--size 2560x1080` for testing other aspect ratios (the window can also be resized) |
| `--level <name>` | Skips the menu and loads the level directly (the file name in `content/Levels` without `.json`, e.g. `lock`). Esc returns to the menu. |

## Controls

The user interface is designed for desktop monitors at 1920×1080 and scaled
uniformly to the screen, so it fits any aspect ratio (16:9, 21:9, 4:3, …).
It keeps a margin of 48 px (at 1080p) from the screen edges; the level select
uses as many columns as fit the screen.

**Menus:** mouse (click, wheel and dragging in the level select), arrow keys and
Enter, or a gamepad (D-pad, A). Esc goes back; in the main menu it quits the game.

**Driving:** the keyboard, the on-screen controls (operated by the mouse or by
touch) and a gamepad all work at the same time.

| Key | On screen / gamepad | Action |
|---|---|---|
| → / ← | joystick right / left, left stick | Accelerate / reverse |
| ↑ / ↓ | joystick up / down, left stick | Rotate the axles clockwise / counterclockwise |
| Space | brake button, A | Brake all wheels |
| Left Ctrl | axle lock button, B | Lock the axle rotation |
| Esc | Back | Back to the level select |

The car can also be grabbed with the mouse and dragged around.

Development keys while driving:

| Key | Action |
|---|---|
| F1–F8 | Physics simulation debug view (Aether.Physics2D DebugView), gamepad Start toggles most of it |
| Num 8 / 2 / 4 / 6 | Move the camera away from the car |
| Page Up / Page Down | Zoom of the debug view |
| Home | Reset the camera |
| F12 | Frames per second (shown by default in Debug builds) |

The editor controls are described in
`docs/bachelor-thesis/dokumentace-editoru-trati.pdf` (in Czech). The editor
opens levels in the JSON format as well as old `.lev` files, and saves JSON.
It checks the blocks and objects against the catalog (F1 lists the blocks;
decoration blocks cannot be placed in the middle layer). A new level saved to
`content/Levels/` appears in the level select automatically (after a rebuild,
which copies it to the build output). Its preview image is loaded from
`content/Levels/levimgs/<name>.xnb`; a level without one is shown as a dark
rectangle with its name.

## Code structure (game)

| Folder | Contents |
|---|---|
| `Screens/` | Screen stack (`ScreenManager`) and the logo, background, main menu and level select screens |
| `GamePlay/` | `GameplayScreen`: one level with the car, physics, camera and debug view; `PhysicsClock`: fixed-step simulation |
| `GameObjects/` | The car, the level (built from a level file and the catalog), the sky and dynamic objects |
| `Graphics/` | Camera following the car (3D view and debug view), frame rate counter |
| `Input/` | Unified keyboard / mouse / touch / gamepad input and the on-screen controls |

The game draws one frame per refresh of the monitor (e.g. 75 frames per second
on a 75 Hz monitor), with 8× multisample antialiasing (fewer samples if the
graphics card does not support 8). The physics simulation is independent of that: it always
runs 60 fixed steps of 1/60 s per second, and the positions of moving bodies are
interpolated between the last two steps for drawing.

## Game content

The `content/` folder contains only **compiled** `.xnb` files, originally built
with XNA 4.0. The source files (3D models, images) are not in the repository, so
the content can currently only be used, not modified. Source files exist only
for the fonts (`*.spritefont`). The 3D models have no textures, they are made of
coloured polygons only.

### Catalog

`content/catalog.json` lists everything the levels are built from, for both the
game and the editor:

- **blocks**: the name (model `Levels/mod/<name>`) and whether the block has a
  collision bitmap (`Levels/bit/<name>`). Only blocks with a collision bitmap can
  be in the middle (physics) layer; the others are decoration for the back and
  front layers.
- **objects**: dynamic object types with their shape (`rectangle`, `circle`, or
  `bitmap` from `Env/bit/<model>`), size, friction, mass, restitution and the
  models they can use (`Env/<model>`).

A new block or object only needs its compiled model (and collision bitmap) in
`content/` and an entry in the catalog.

### Level format

Levels are JSON files with one block or object per line:

```json
{
  "format": 1,
  "order": 1,
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
- `middle`: blocks of the physics layer, `back` and `front`: decoration layers.
- Blocks: `x`, `y` on the level grid (one cell = 22.5 physics units), `shift`
  moves the block vertically in steps of 2.5 physics units.
- Objects: `x`, `y` in physics units.

In the game, the physics shape of each block is created by vectorizing its
collision bitmap. The levels lock, startblock and tunel were converted from the
original `.lev` format of the 2016 version, which the editor can still open.

The summit level is generated by a script in `tools/levelgen/`, which keeps the
road continuous and adds the decorations; see its
[README](tools/levelgen/README.md). Edit the script rather than the JSON file.

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

## License

[MIT](LICENSE), © 2016–2026 Antonín Pražský: the code, the game content
(`content/`) and the documentation.

The physics engine **Aether.Physics2D** is used as a NuGet package and is licensed
under the Microsoft Public License (Ms-PL). It is based on Farseer Physics
(© Ian Qvist) and Box2D (© Erin Catto).

## Roadmap

Issues found during the migration, roughly in order of importance:

1. **Source models.** Without them the content cannot be changed. If they turn
   up (e.g. `.blend`/`.fbx`), add them to `content/` and set up the MonoGame
   Content Pipeline (`.mgcb`).
2. **Unfinished menu:** Garage, About and Options.
3. **Editor:** default folder `C:\levels`, Windows-only dependencies (WinForms
   dialogs, `keybd_event` for Caps Lock) and controls spread over two windows.
   A possible replacement: UI directly in the editor window (e.g. ImGui.NET).
4. **Automated tests and CI**, e.g. tests of the level format and builds via
   GitHub Actions.
