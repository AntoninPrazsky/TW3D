# Triple Wheel 3D (TW3D)

An obstacle course driving game with 3D graphics and 2D physics: a car with
rotating axles has to get through the track by rotating and locking its axles.
The project was created in 2016 as an appendix to a bachelor's thesis (sample
game + level editor) and was migrated to current tools in 2026.

The game is built on the [MonoGame](https://monogame.net/) framework and the
Farseer Physics engine. The physics runs in 2D, and the 3D models are drawn at
the positions of the physics bodies.

## Repository structure

| Path | Contents |
|---|---|
| `src/TW3D.Game/` | The game (`TW3D.exe`) |
| `src/TW3D.LevelEditor/` | Level editor (`LevelEditor.exe`), Windows only |
| `src/FarseerPhysics/` | Farseer Physics 3.5 engine (third-party code) |
| `content/` | Shared game content: compiled models, textures and fonts (`.xnb`) and levels (`Levels/*.lev`). Copied to the build output as `Content/`. |
| `docs/bachelor-thesis/` | Original documentation from the bachelor's thesis (in Czech, describes the 2016 state) |
| `docs/catalog/` | Catalog of track blocks and dynamic objects. Open `catalog.html` in a browser. |

## Requirements

- [.NET SDK 10](https://dotnet.microsoft.com/download) or newer
- Optionally an IDE: Visual Studio 2022 (17.13+) / 2026, JetBrains Rider or VS Code with C# Dev Kit

Packages (MonoGame 3.8 DesktopGL) are restored automatically from NuGet. Their
versions are managed centrally in `Directory.Packages.props`.

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
| `--level <name>` | Skips the menu and loads the level directly (`lock`, `startblock`, `tunel`). Esc returns to the menu. |

## Game controls

| Key | Action |
|---|---|
| → / ← | Accelerate / reverse |
| ↑ / ↓ | Rotate the axles clockwise / counterclockwise |
| Space | Brake all wheels |
| Left Ctrl | Lock the axle rotation |
| Esc | Back to the menu |
| F1–F8 | Physics simulation debug view (Farseer DebugView) |

The menus can be operated with the mouse. By default the game reads the
keyboard while driving. To control the car with the on-screen virtual joystick,
change `VirtualState = HandleKeyboard();` to
`VirtualState = HandleVirtualStick();` in
`src/TW3D.Game/ScreenSystem/InputHelper.cs`.

The editor controls are described in
`docs/bachelor-thesis/dokumentace-editoru-trati.pdf` (in Czech). Save a new
level to `content/Levels/` and add it to the list in `Game1.Initialize`
(`selLevScreen.AddLevelItem(...)`). Its preview image in the menu is loaded
from `content/Levels/levimgs/<name>.xnb`.

## Game content

The `content/` folder contains only **compiled** `.xnb` files, originally built
with XNA 4.0. The source 3D models and textures are not in the repository, so
the content can currently only be used, not modified. Source files exist only
for the fonts (`*.spritefont`).

The `.lev` level format is plain text, with records separated by `|`:

```
<block> x,y,shift|...      blocks of the middle (physics) layer
*<block> x,y,shift|...     blocks of the decorative back layer
#<block> x,y,shift|...     blocks of the decorative front layer
<Type> <model> x,y|...     dynamic objects (Box, Bar, Tb, Co, …)
```

In the game, the physics shape of each block is created by vectorizing the
bitmap `content/Levels/bit/<block>.xnb`.

## History

- The `bachelor-thesis-2016` tag marks the original state of the bachelor's
  thesis appendix (UWP, MonoGame 3.5, Visual Studio 2015), without build
  output and binaries.
- In 2026 the project was migrated to .NET 10 + MonoGame 3.8 DesktopGL. Since
  then the game runs as a regular desktop application instead of a UWP app.
  It has been tested on Windows. DesktopGL also supports Linux and macOS, but
  the game has not been tested there yet.

## License

- **Own code, game content (`content/`) and documentation:** [MIT](LICENSE),
  © 2016–2026 Antonín Pražský.
- **Farseer Physics Engine** (`src/FarseerPhysics/`) and the files derived from
  its samples (`src/TW3D.Game/ScreenSystem/`, `src/TW3D.Game/DrawingSystem/`,
  `src/TW3D.Game/ConvertUnits.cs`): Microsoft Public License (Ms-PL), see
  [`src/FarseerPhysics/LICENSE.txt`](src/FarseerPhysics/LICENSE.txt).
  Farseer is based on Box2D (© Erin Catto).

## Roadmap

Issues found during the migration, roughly in order of importance:

1. **Replace Farseer with [Aether.Physics2D](https://github.com/nkast/Aether.Physics2D)**,
   the maintained successor of Farseer (NuGet `Aether.Physics2D.MG`). After that,
   `src/FarseerPhysics/` can be deleted. The API differs mainly in how bodies are
   created (`World.CreateBody` instead of `BodyFactory`).
2. **Separate own code from the Farseer sample framework.** `ScreenSystem/` and
   `DrawingSystem/` come from the Farseer Samples and contain unused parts too
   (e.g. `MessageBoxScreen`, the "Welcome, betatesters!" text in `MainGame`).
3. **Source models and textures.** Without them the content cannot be changed.
   If they turn up (e.g. `.blend`/`.fbx`), add them to `content/` and set up the
   MonoGame Content Pipeline (`.mgcb`).
4. **A single shared list of blocks and objects.** Loading of the 69 models is
   written out by hand twice (`MainGame.LoadContent` and `Editor.LoadContent`),
   and the `.lev` parser exists twice as well (game and editor). A candidate for
   a shared library and a data file (e.g. JSON).
5. **A more robust level format.** The parser reads the file character by
   character. The editor saves coordinates using `float.ToString()` with the
   system culture, so on Czech Windows a non-integer coordinate would get a
   decimal comma clashing with the separator. Use `CultureInfo.InvariantCulture`,
   or switch to JSON.
6. **The level list in the menu** is hard-coded in `Game1.Initialize`, including
   duplicate test entries. It would be better to load it from the `Levels` folder.
7. **Unfinished menu:** Garage, About and Options. Switching between keyboard and
   touch controls requires a code change.
8. **Editor:** default folder `C:\levels`, Windows-only dependencies (WinForms
   dialogs, `keybd_event` for Caps Lock) and controls spread over two windows.
   A possible replacement: UI directly in the editor window (e.g. ImGui.NET).
9. **Automated tests and CI**, e.g. tests of the level parser and builds via
   GitHub Actions.
