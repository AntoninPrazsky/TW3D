# Levels

## Level editor

The level editor is a part of the game: the level is drawn exactly as in the game, a
test drive starts in the same window, and it runs wherever the game runs. It is driven by
the mouse, with a user interface of [Dear ImGui](https://github.com/ocornut/imgui) over
the scene. F1 (Help > Controls) lists every control in the editor itself and has a short
guide to building a level; the tables there are the reference, this section describes the
parts. The editor of 2016 was a console program driven by the keyboard; its documentation,
in Czech, is in
[`bachelor-thesis/dokumentace-editoru-trati.pdf`](bachelor-thesis/dokumentace-editoru-trati.pdf)
and describes that editor, not this one.

### Starting it

```sh
dotnet run --project src/TW3D.Game -- --editor          # the level edited last, or a new level
dotnet run --project src/TW3D.Game -- --editor lock     # a level of content/Levels, or a level file
dotnet run --project src/TW3D.Game -- --editor --levels ../scratch-levels   # another levels folder
```

A Debug build of the game also has it in the main menu (*Level editor*, in the top right
corner; the level edited last or a new level; Esc returns to the menu). The Android build
leaves the editor out.

The editor opens and saves levels in the levels folder: `content/Levels` of the repository
(found above the program, where `TW3D.slnx` is), so that the levels of the game are edited
in place; `--levels <folder>` gives another folder (a scratch folder is the way to try the
editor out), and a game that is not in a repository uses `%LOCALAPPDATA%/TW3D/Levels`.

### The screen

The scene is seen by a free camera: the middle button pans along the track, the wheel zooms
towards the cursor, Alt + the left button orbits around the point under the cursor,
W/S/A/D and Q/E fly, the numeric keypad turns, and Home frames the whole level. A level has
three layers, the back and front layers of decorations and the middle layer, the road the
car drives on; the layer being edited is changed by Tab, Right Shift toggles the back layer
as soon as it is pressed and Right Ctrl the front layer when tapped alone. The View menu
hides the back layer, the
front layer and the objects (they are not drawn and cannot be edited while hidden: the
status bar says "The back layer is hidden." when a key, a click or a command of the Edit
menu would change them), the grid around the cursor cell and the red bars of the road gaps;
nothing hidden is saved or affects the test drive. The lighting is switched by F9 (the
lighting of 2016), F10 (shadows) and F11 (ambient occlusion), as in the game, and M changes
the mood of the level.

The menu bar has the File, Edit, View, Level and Help menus. The status bar along the bottom
shows the cursor cell, the layer, the active tool, the chosen block and object, the file of
the level (its tooltip the full path and the levels folder; `*` while it has unsaved
changes), how many problems and road gaps the level has, the warning about a generated
level (below) and what was done last. The title of the window shows the name of the level
with `*` while it has unsaved changes. Every change can be undone (Ctrl+Z, Ctrl+Y and the
Edit menu, which names the step); a painted stroke, a drag of an object and a command on a
selection are one step each.

### Tools

The mouse and most keys go to the active tool: B makes the Place tool active (blocks), O the
Object tool (dynamic objects) and V the Select tool (many blocks); View > Tool lists them.

The **Place tool** edits blocks in the active layer. The left button places the chosen block
in the cell under the cursor and dragging paints, Left Shift + the left button erases, the
right button picks the block under the cursor as the chosen one (with its shift), and
Ctrl + the wheel shifts the block up or down. The arrows move the cursor cell, Space places,
Delete erases, PageUp / PageDown shift, F marks the block at the cursor cell as the finish
(again, it stops being it), comma and period choose the previous and the next block of the
catalog, and R switches road mode (below).

The **Object tool** edits the dynamic objects (boxes, bars, ramps...), which are always on
the plane of the middle layer. The left button adds the chosen object at the point under
the cursor (rounded to a tenth of a physics unit) and selects it, or selects the object
under the cursor, and dragging moves it; Left Shift + the left button removes the object
under the cursor, and the right button picks its type and model as the chosen object. Space
adds the chosen object at the top of the cursor cell, so that it drops onto the block there
(two objects are never put in one place), Delete removes the selected object, the arrows
move it by a physics unit (Ctrl + the arrows by a tenth), or move the cursor cell while
nothing is selected, and Esc clears the selection (a second Esc leaves the editor).

The **Select tool** works on many blocks of the active layer at once. A click selects a
block (Ctrl adds or removes it), a drag from an empty cell draws a rectangle that selects
the blocks in it, a drag of a selected block moves the selection by whole cells, Ctrl+A
selects the layer, and Space adds the block at the cursor cell to the selection, or takes
it out again. The arrows move the selection (Ctrl + the arrows only the cursor cell),
Delete erases it, PageUp / PageDown shift it, Ctrl+C / Ctrl+X / Ctrl+V copy, cut and
paste it (the leftmost of the copied
blocks, the lowest of those if several, goes to the cursor cell), and H mirrors it left to
right, swapping every block for its mirrored variant where the catalog has one (`b2` and
`b2f`, `s1` and `s1f`...), so that a climb becomes a descent.

### The windows

The windows can be moved, resized and docked; the editor remembers where they are. The View
menu shows and hides the Palette, the Inspector and the Level window, and F1 shows the
Controls window.

- **Palette** shows every block of the catalog as a turning 3D preview, in the groups of
  the [catalog page](catalog/catalog.html) and searchable by name; a click makes the block
  the chosen one and the Place tool active. Decorations are greyed out while the middle
  layer is active. Its Objects tab shows the models of the dynamic objects, and a click
  makes one the chosen object and the Object tool active.
- **Inspector** shows what the active tool works on: the block at the cursor cell of the
  active layer (its name, cell, shift and whether it is the finish, and the surface heights
  of a road block), the selected blocks (how many, and the cells they span) with buttons
  that mirror and delete them, or the selected object with its type, model and position,
  which can be typed (each edit one step of undo), and a button that removes it.
- **Level** is everything about the level that is not a block. It shows the name of the
  level (the full path as a tooltip) and edits the mood (Day, Sunset or Night, at once), the
  position in the level select (the checkbox *In the level select*, and the number; levels
  without one come after the others, sorted by name) and the par time (the checkbox *Par
  time* and the seconds; while the level has none, the window says what the game estimates,
  "estimated: 10.0 s"). Each edit is one step of undo, saved with the level. It counts the
  blocks of each layer and the objects and gives the cells the level spans. Below are the
  problems that stop the level from being played (no finish, no block at the start, an
  object over nothing...) and, apart from them, the gaps of the road, folded away when
  there are more than ten. Clicking a line with a place points the camera at it (the
  distance is kept, but no greater than a readable one; a problem of the whole level, like
  the missing finish, has none and is plain text). The lists follow the level as it is
  edited, and Ctrl+Z takes a fix back.

### Road mode and the gaps

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
heights, a red bar is drawn at the edge, from one surface to the other, and the Level
window lists the gap. A gap left on purpose is marked, not refused: the problems that stop
the level from being played are counted apart from the gaps, in the status bar ("1
problem", "12 gaps") and in the message after opening or saving a level ("Opened
lock.json; 12 road gaps.").

### Test drive

F5 drives the level in the game at once, from the start cell [0, 0] (Shift+F5 from the
cursor cell), with the car and the wheels chosen in the garage. The level goes in memory,
nothing is saved and no record is kept, and Esc, the pause menu or the results bring the
editor back as it was, with its camera and undo history. A level without a finish or
without a block at the start can still be driven (the status bar warns); one the game
cannot build is refused.

### Files

The File menu has New (Ctrl+N, a level with the start block only, at [0, 0], where the car
starts), Open (Ctrl+O: the levels of the folder with their order, mood, number of blocks
and preview, and a field for any level file, also an old `.lev`), Open recent (the last 8
levels opened or saved), Save (Ctrl+S) and Save as (Ctrl+Shift+S, a name in the folder; an
existing file is replaced only when confirmed). A new level and an old `.lev` file are saved
through Save as, as `.json`. New, Open, Esc and the close button of the window ask "Save
changes?" while the level has unsaved changes (Enter saves, D drops them, Esc cancels).
Opening or saving a level says in the status bar how many problems and road gaps it has,
and the first problem ("Saved x.json; 1 problem: The level has no finish.; 3 road gaps.").

A level saved into `content/Levels` of the repository is also copied to `Content/Levels` of
the build output, so that the level select and the game see it at once, without a rebuild.
A level saved there by the editor appears in the level select at once; a level added by
hand needs a rebuild, which copies it to the build output. Its preview image is
`content/Levels/levimgs/<name>.png` (700 × 394, a screenshot of the track without the car:
press H while driving; built by the content pipeline: add a `/build` line for it at the end
of `content/Content.mgcb`); a level without one is shown as a dark rectangle with its name.

The editor keeps two files in `%LOCALAPPDATA%/TW3D` (the folder with the settings of the
game): `editor.json` has the recent levels and the last level with its camera (started
without a level, the editor opens the last one again where it was left), and
`editor-layout.ini` has the positions and sizes of the windows. They can be deleted at any
time.

### Generated levels

Opening a level that a script of `tools/levelgen` generates (see below) puts a warning in
the status bar: running the script again overwrites the changes made in the editor. To keep
them, carry the changes over to the script.

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
  In the editor, M switches it and the Level window sets it.
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
[README](../tools/levelgen/README.md). When the level is changed in the editor (which
warns when such a level is opened), carry the changes over to the script, otherwise
running it again overwrites them.
