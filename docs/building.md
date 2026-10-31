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

The game has been tested on Windows. DesktopGL also supports Linux and macOS, but
the game has not been tested there yet. The level editor runs on Windows only.

## Android

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
