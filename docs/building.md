# Building and running

## Requirements

- [.NET SDK 10](https://dotnet.microsoft.com/download) or newer
- Optionally an IDE: Visual Studio 2022 (17.13+) / 2026, JetBrains Rider or VS Code with C# Dev Kit

Packages (MonoGame 3.8 DesktopGL, Aether.Physics2D 2.2) are restored
automatically from NuGet. Their versions are managed centrally in
`Directory.Packages.props`. The build also restores the content pipeline tool
(`dotnet-mgcb`, listed in `.config/dotnet-tools.json`) and builds the models with it.

## Desktop

```sh
dotnet build TW3D.slnx

# game
dotnet run --project src/TW3D.Game
dotnet run --project src/TW3D.Game -- --windowed --level lock

# level editor (the game with the editor screen)
dotnet run --project src/TW3D.Game -- --editor lock
```

Game arguments for development:

| Argument | Meaning |
|---|---|
| `--windowed` | 1280×720 window instead of full screen |
| `--size WxH` | Window of the given size, e.g. `--size 2560x1080` for testing other aspect ratios (the window can also be resized) |
| `--level <name>` | Skips the menu and loads the level directly (the file name in `content/Levels` without `.json`, e.g. `lock`). Esc returns to the menu. |
| `--tutorial` | Shows the tutorial's lessons of each level again, learnt or not, saving nothing |
| `--tutorial demo` | Shows every lesson of the tutorial in turn on any level, each done by itself after two seconds (for screenshots), saving nothing |
| `--sounds <folder>` | Writes every sound effect as a WAV file into the folder and quits, to listen to them |
| `--editor [<level>]` | Opens the level editor (see [Levels](levels.md#level-editor)) instead of the game, in a window: with a level name of the levels folder (`lock`) or a level file; without one, the level edited last (a new level when there is none) |
| `--levels <folder>` | The levels folder of the editor, where it opens and saves levels, instead of `content/Levels` of the repository (e.g. a scratch folder for trying the editor out) |
| `--render <height>` | Draws the 3D scene at the height of the Resolution row of Options nearest to the one given (e.g. `--render 720`), whatever Options say, without saving it (for tests and screenshots); the row cannot be changed meanwhile |

The game runs on Windows and on Linux, a Raspberry Pi 5 included (see below); DesktopGL
also supports macOS, which has not been tried. The level editor is a part of the game; the
Android build leaves it out, and it does not run on a Raspberry Pi yet (the `ImGui.NET`
package has no native library for Linux ARM64).

## Linux and a Raspberry Pi

The same commands build and run the game on Linux, also on a Raspberry Pi 5 (Raspberry Pi
OS, .NET SDK 10 for ARM64):

```sh
dotnet tool restore
dotnet build src/TW3D.Game -c Release
src/TW3D.Game/bin/Release/net10.0/TW3D
```

The models, the fonts and the images are built there as on Windows, but the shaders are
not: the effect compiler of MonoGame needs the shader compiler of Direct3D, which runs only
on Windows (elsewhere through Wine). Their code for DesktopGL is the same on every system,
so the content build on Windows keeps it in the repository: `PrecompiledEffectProcessor` of
`tools/ContentPipeline` compiles `content/Effects/Scene.fx` and writes its code to
`content/Effects/DesktopGL/Scene.mgfx`, with `Scene.txt` saying which source (a SHA-256)
and which version of MonoGame it was compiled from. A build elsewhere takes that code, and
fails with an explanation when the shader has changed since. **A changed shader: build the
game on Windows and commit the files it rewrites in `content/Effects/DesktopGL` with it.**
CI fails without them and attaches the code it compiled to the run (the artifact
`effects-desktopgl`).

On a Raspberry Pi (any Linux on ARM64, `Graphics/PiGraphics`) the game draws the 3D scene
at 1280×720 (until another Resolution is chosen in Options), scaled up to the screen, with
the user interface at the full resolution over it, without multisampling and without ambient occlusion by default (Options can switch them on)
and with a smaller shadow map: its GPU drew a level at 1920×1080 with the graphics of a
desktop in 49 ms a frame. A level then holds 60 frames per second, the garage of the menus
about 45.

## Tests and CI

```sh
dotnet test TW3D.slnx
```

`tests/TW3D.Tests` (xUnit) tests what needs no graphics device: the shipped content (every
level fits the catalog, has a finish and a preview and is written as `LevelFile` writes
it; every model of the catalog exists and is built, and the physics has its collision
shape), the score of a run, the rules of the tutorial and the sound effects (each peaks at 0.9,
ends quietly, is heard on small speakers and is not shrill). The files of the game it tests
are compiled into it, as they have no MonoGame in them.

Every push runs `.github/workflows/build.yml` on GitHub Actions: the solution built in
Debug and Release, the tests, a check that the collision shapes and the generated
training levels are what their generators make (`tools/CollisionGen`,
`tools/levelgen/training*.js`) and that the code of the shaders kept for Linux is of
their sources, the Android build, and the game built and tested on Linux ARM64 (as on a
Raspberry Pi), which takes the code of the shaders compiled on Windows (see
[Linux and a Raspberry Pi](#linux-and-a-raspberry-pi)).

## Android

`src/TW3D.Android` builds the game for Android phones (Android 7.0 and newer). It
compiles the code of `src/TW3D.Game` (linked, not copied); what differs between the
platforms is marked `#if ANDROID`: full screen in landscape, a twice as big user
interface, 4× multisampling (Options can switch it off), the level list read from the app and the highest
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
