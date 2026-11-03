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
the level). The level is edited with the active tool: B makes the Place tool active
(blocks), V the Select tool (many blocks), O the Object tool (dynamic objects); View >
Tool lists them, and the status bar names the active one. With the Place tool, blocks are
edited in the active layer (Tab, Right Shift and Right Ctrl, pressed alone, choose it): the left button places the chosen block in the cell
under the cursor and dragging paints, Left Shift + the left button erases, the right button
picks the block under the cursor as the chosen one, Ctrl + the wheel shifts it up or down.
The keys of the old editor do the same: the arrows move the cursor cell, Space places,
Delete erases, PageUp / PageDown shift, F marks the finish, comma and period choose the
block, R switches road mode (below), M the mood. With the Object tool, dynamic objects
(boxes, bars, ramps...) are edited on the plane of the middle layer, where they always are:
the left button adds the chosen
object at the point under the cursor (rounded to a tenth of a physics unit) and selects it,
or selects the object under the cursor, and dragging moves it; Left Shift + the left button
removes the object under the cursor, the right button picks its type and model as the
chosen object. Space adds the chosen object at the top of the cursor cell, so that it drops
onto the block there (the cursor cell is drawn with the Object tool too, and follows the
mouse; two objects are never put in one place), Delete removes the selected object, the arrows move it by a physics
unit (Ctrl + the arrows by a tenth), or move the cursor cell while nothing is selected, and
Esc clears the selection (a second Esc leaves the editor). Every change can be undone
(Ctrl+Z, Ctrl+Y and the Edit menu, which names the step); a painted stroke and a drag of an
object are one step each. The lighting is switched by F9, F10, F11; F1 lists the controls.
The Palette window shows every block of the catalog as a turning 3D preview, in the groups
of the [catalog page](catalog/catalog.html) and searchable by name, and a click makes it the
chosen block and the Place tool active; its Objects tab shows the models of the dynamic
objects, and a click makes one the chosen object and the Object tool active. The Inspector
window shows what the active tool works on: the block at the cursor cell of the active
layer (its name, cell, shift and finish), or the selected object with its type, model and position,
which can be typed (each edit one step of undo, applied to the object it was begun on), and a
button that removes it (an object only under the cursor is shown to read). F5 drives the level in the game at once, from the start cell,
with the car and the wheels chosen in the garage (Shift+F5 from the cursor cell): the level
goes in memory, nothing is saved and no record is kept, and Esc, the pause menu or the
results bring the editor back as it was, with its camera and undo history. A level without
a finish or without a block at the start can still be driven (the status bar warns); one
the game cannot build is refused. With the Select tool (V) many blocks of the active layer
are worked on at once: a click selects a block (Ctrl adds or removes it), a drag from an
empty cell draws a rectangle that selects the blocks in it, a drag of a selected block moves
the selection by whole cells, Ctrl+A selects the layer, and Space adds the block at the
cursor cell. The arrows move the selection (Ctrl + the arrows only the cursor cell), Delete
erases it, PageUp / PageDown shift it, Ctrl+C / Ctrl+X / Ctrl+V copy, cut and paste it (the
leftmost of the copied blocks, the lowest of those if several, goes to the cursor cell), and
H mirrors it left to right, swapping every block for its mirrored variant where the catalog
has one (`b2` and `b2f`, `s1` and `s1f`...), so that a climb becomes a descent. Each of
these is one step of undo.

Road mode (R, or Level > Road mode; the status bar says "Road" after the chosen block while
it is on) makes the Place tool join the road as the level generator does. Every road block
of the catalog has a `profile`, the height of its surface at the left and the right edge
(see [content](content.md#catalog)). With road mode on, a road block placed in the middle
layer (the back and front layers place as usual) goes into the column of the cursor cell at
the height where its left edge meets the right edge of the road block in the column to the
left, or its right edge the left edge of the block to the right when the stroke paints
leftwards. So a hill painted with the mouse or built with Space comes out continuous, the
cell and the shift being chosen for you ("Placed b2f at [3, -1], shift 1."). In a stroke
each block joins the one the stroke placed just before it, and an old road block it would
stand on, at the cell pointed at or within a cell of the new height, is replaced by it, all
in one step of undo. Where the column on the preferred side has no road block, the other
side's is joined; the shift chosen by hand applies only to a block with no neighbour at
all, and blocks without a profile are placed as usual.

Wherever two neighbouring road blocks of the middle layer meet with different surface
heights, a red bar is drawn at the edge, from one surface to the other, and the problems
reported when the level is opened or saved count the gap (after the problems that stop the
level from being played); a gap left on purpose is marked, not refused. The Inspector shows
the surface heights of the road block at the cursor cell and the number of gaps of the
level.

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
