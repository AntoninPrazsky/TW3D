# Levels

## Level editor

A new editor, driven by the mouse with a user interface of
[Dear ImGui](https://github.com/ocornut/imgui), is being built into the game
([issue #20](https://github.com/AntoninPrazsky/TW3D/issues/20) has the plan and the controls):

```sh
dotnet run --project src/TW3D.Game -- --editor          # the level edited last, or a new level
dotnet run --project src/TW3D.Game -- --editor lock     # a level of content/Levels, or a level file
dotnet run --project src/TW3D.Game -- --editor --levels ../scratch-levels   # another levels folder
```

A Debug build of the game also has it in the main menu (*Level editor*, the level edited
last or a new level; Esc returns to the menu).

The editor opens and saves levels in the levels folder: `content/Levels` of the repository
(found above the program, where `TW3D.slnx` is), so that the levels of the game are edited
in place; `--levels <folder>` gives another folder, and a game that is not in a repository
uses `%LOCALAPPDATA%/TW3D/Levels`. The File menu has New (Ctrl+N, a level with the start
block only), Open (Ctrl+O: the levels of the folder with their order, mood, number of blocks
and preview, and a field for any level file, also an old `.lev`), Open recent (the last 8
levels opened or saved), Save (Ctrl+S) and Save as (Ctrl+Shift+S, a name in the folder; an
existing file is replaced only when confirmed). A new level and an old `.lev` file are saved
through Save as, as `.json`. New, Open, Esc and the close button of the window ask "Save
changes?" while the level has unsaved changes (Enter saves, D drops them, Esc cancels). The
title of the window shows the name of the level with `*` while it has unsaved changes, the
status bar the file (its tooltip the full path and the levels folder) and the problems found
when saving. The editor remembers the recent levels and the last level with its camera in
`%LOCALAPPDATA%/TW3D/editor.json`; started without a level, it opens the last one again
where it was left. A level saved into `content/Levels` of the repository is also copied to
`Content/Levels` of the build output, so that the level select and the game see it at once,
without a rebuild. Opening a level that a script of `tools/levelgen` generates (see below)
puts a warning in the status bar: running the script again overwrites the changes made in the
editor.

So far it shows the level as the game draws it, with a camera driven by the mouse (the
middle button pans, the wheel zooms towards the cursor, Alt + the left button orbits) and
by the keys of the old editor (W/S/A/D and Q/E fly, the numeric keypad turns, Home frames
the level). Blocks are edited in the active layer (Tab, Right Shift and Right Ctrl choose
it): the left button places the chosen block in the cell under the cursor and dragging
paints, Left Shift + the left button erases, the right button picks the block under the
cursor as the chosen one, Ctrl + the wheel shifts it up or down. The keys of the old
editor do the same: the arrows move the cursor cell, Space places, Delete erases,
PageUp / PageDown shift, F marks the finish, comma and period choose the block, M the
mood. Every change can be undone (Ctrl+Z, Ctrl+Y and the Edit menu, which names the
step); a painted stroke is one step. The lighting is switched by F9, F10, F11; F1 lists
the controls. The Palette window shows every block of the catalog as a turning 3D preview,
in the groups of the [catalog page](catalog/catalog.html) and searchable by name, and a
click makes it the chosen block (its Objects tab chooses the dynamic object to place, for
the Object tool to come). Selection, dynamic objects and the test drive come with the next
issues.

### The old editor

```sh
dotnet run --project src/TW3D.LevelEditor
dotnet run --project src/TW3D.LevelEditor -- --level content/Levels/lock.json
```

The editor (Windows only) asks for the level to open, or opens the one given by
`--level <file>`. Its controls are described in
[`dokumentace-editoru-trati.pdf`](bachelor-thesis/dokumentace-editoru-trati.pdf) (in
Czech). The editor opens levels in the JSON format as well as old `.lev` files, and
saves JSON. F marks the block of the middle layer under the selector as the finish of
the level (again, it stops being it); P tells whether a block is the finish. It checks
the blocks and objects against the [catalog](content.md#catalog) (F1 lists the blocks;
decoration blocks cannot be placed in the middle layer).

The editor draws the level the same way as the game: F9 switches to the lighting of the
2016 version, F10 the shadows, and M the mood of the level, which is saved with it.

A new level saved to `content/Levels/` appears in the level select automatically (after
a rebuild, which copies it to the build output; the new editor copies the levels it saves
there at once, so they appear without a rebuild). Its preview image is
`content/Levels/levimgs/<name>.png` (700 × 394, a screenshot of the track without the car:
press H while driving; built by the content pipeline: add a `/build` line for it at the end
of `content/Content.mgcb`); a level without one is shown
as a dark rectangle with its name.

## Level format

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
- `par` (optional): the time a good run takes, in seconds (see
  [a run of a level](playing.md#a-run-of-a-level)). Without it the game estimates it:
  half a second for each grid cell the finish is from the start (across and up or
  down), at least 10 seconds.
- `middle`: blocks of the physics layer, `back` and `front`: decoration layers.
- Blocks: `x`, `y` on the level grid (one cell = 22.5 physics units), `shift`
  moves the block vertically in steps of 2.5 physics units. `"finish": true` makes a
  block of the middle layer the finish of the level (one at most): the run ends when
  the middle of the car reaches its cell. A level without a finish has no end.
- Objects: `x`, `y` in physics units.

## Converted and generated levels

The levels lock, startblock and tunel were converted from the original `.lev`
format of the 2016 version, which the editor can still open.

The training levels (training1 to training3, the first in the level select, see the
tutorial in [Playing](playing.md#tutorial)) and the summit level are generated by scripts in
`tools/levelgen/`, which keep the road continuous and add the decorations; see its
[README](../tools/levelgen/README.md). When the level is changed in the editor,
carry the changes over to the script, otherwise running it again overwrites them.
