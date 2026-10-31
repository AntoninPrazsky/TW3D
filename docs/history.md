# History and roadmap

## History

- The `bachelor-thesis-2016` tag marks the original state of the bachelor's
  thesis appendix (UWP, MonoGame 3.5, Visual Studio 2015), without build
  output and binaries. Its documentation (in Czech) is in
  [`bachelor-thesis/`](bachelor-thesis/).
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
  precompiled `.xnb` files (see [Game content](content.md)).
- The collision shapes are generated from the models while building instead of
  being traced from the collision bitmaps at runtime (see
  [Collision shapes](content.md#collision-shapes)).

## Roadmap

Issues found during the migration, roughly in order of importance:

1. **Sources of the other content:** the images and fonts are still only
   precompiled `.xnb` files.
2. **Editor:** default folder `C:\levels`, Windows-only dependencies (WinForms
   dialogs, `keybd_event` for Caps Lock) and controls spread over two windows.
   A possible replacement: UI directly in the editor window (e.g. ImGui.NET).
3. **Automated tests and CI**, e.g. tests of the level format and builds via
   GitHub Actions.
