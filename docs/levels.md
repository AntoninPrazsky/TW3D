# Levels

## Level editor

The level editor is a part of the game: the level is drawn exactly as in the game, a
test drive starts in the same window, and it runs wherever the game runs on a desktop:
on Windows and on Linux, but not yet on a Raspberry Pi (the `ImGui.NET` package has no
native library for Linux on ARM64) and not on a phone. It is driven by
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

The main menu of the game has it too (*Level editor*, in the top right corner; the level
edited last or a new level; Esc returns to the menu), in a Debug and a Release build alike,
so that the players can build levels of their own. The Android build leaves the editor out;
on a Raspberry Pi the main menu has no *Level editor* and `--editor` quits with a message.

The editor opens and saves levels in the levels folder: `content/Levels` of the repository
(found above the program, where `TW3D.slnx` is), so that the levels of the game are edited
in place; `--levels <folder>` gives another folder (a scratch folder is the way to try the
editor out), and a game that is not in a repository (a downloaded game) uses
`%LOCALAPPDATA%/TW3D/Levels` (`~/.local/share/TW3D/Levels` on Linux). The level select
lists the levels of the game only, so a level of a player is driven by the test drive of
the editor (F5).

### The screen

The scene is seen by a free camera: the middle button pans along the track, the wheel zooms
towards the cursor, Alt + the left button orbits around the point under the cursor,
W/S/A/D and Q/E fly, the numeric keypad turns, and Home frames the whole level. A level has
seven layers a cell apart: the middle layer, the road the car drives on, the three back layers
of decorations behind it (back, back 2 and back 3, one, two and three cells behind the road)
and the three front layers of decorations in front of it (front, front 2 and front 3, one, two
and three cells in front of the road, as deep as the back at the owner's wish; the camera of
the game is 40 physics units in front of the road, so it sees front 2 and front 3 only when it
turns, at the start). The layer being edited is changed by Tab (Left Shift + Tab the other way),
Right Shift goes a layer further back as soon as it is pressed (from the middle or a front layer
to the back layer, then back 2, back 3 and the middle layer again) and Right Ctrl a layer
further front when tapped alone (from the middle or a back layer to the front layer, then front
2, front 3 and the middle layer again). The View menu hides each of the back and the front layers, the objects and
the scenery (they are not drawn and cannot be edited while hidden: the status bar says "The
back 2 layer is hidden." when a key, a click or a command of the Edit menu would change
them), the grid around the cursor cell and the red bars of the road gaps;
nothing hidden is saved or affects the test drive. The lighting is switched by F9 (the
lighting of 2016), F10 (shadows) and Shift+F10 (ambient occlusion), as in the game, M changes
the mood of the level, and F11 switches between full screen and a window.

The menu bar has the File, Edit, View, Level and Help menus. The status bar along the bottom
shows the cursor cell, the layer, the active tool, the chosen block and object (and the
chosen prop with its depth and turn while the Scenery tool is active), the file of
the level (its tooltip the full path and the levels folder; `*` while it has unsaved
changes), how many problems and road gaps the level has, the warning about a generated
level (below) and what was done last. The title of the window shows the name of the level
with `*` while it has unsaved changes. Every change can be undone (Ctrl+Z, Ctrl+Y and the
Edit menu, which names the step); a painted stroke, a drag of an object or a prop and a
command on a selection are one step each.

### Tools

The mouse and most keys go to the active tool: B makes the Place tool active (blocks), O the
Object tool (dynamic objects), V the Select tool (many blocks) and P the Scenery tool (props
beside the track); View > Tool lists them.

The **Place tool** edits blocks in the active layer. The left button places the chosen block
in the cell under the cursor and dragging paints, Left Shift + the left button erases, the
right button picks the block under the cursor as the chosen one (with its shift and its
turn), and Ctrl + the wheel shifts the block up or down. The arrows move the cursor cell,
Space places, Delete erases, PageUp / PageDown shift, F marks the block at the cursor cell
as the finish (again, it stops being it), T turns the chosen block round (again, back),
comma and period choose the previous and the next block of the catalog, and R switches road
mode (below). The chosen block is shown see-through, glowing a little green, where Space or a
click would place it: at the cursor cell with the chosen shift and turn, in road mode where it
joins the road. It is not shown where the same block is already, nor while Left Shift is held
(a click erases then).

A block of the road can be **turned** round by 180° about the vertical axis: its road then
runs the other way, so `b2`, a descent, turned is a climb, and the walls `t1` and `u1` turned
stand at the other side of their cell. The blocks that turn are the slopes (`b1`–`b4`,
`c1`–`c3`, `d1`–`d3`, with `b0`, `c0` and `d0`), the crests `e0`–`e3` and the bends `e12`–`e34`, the road ends `r1`–`r3`, the rocks `rk1`–`rk3`, the stairs `s1` and `s2`, the steps `k1`–`k3` and the walls `t1` and `u1` (`"turns": true` in the
catalog); a tunnel or a decoration turned would show its back, so it does not turn, but for
the ground under a slope (`b2g` turned under `b2f`) and the rocks, hills and copses of the
backdrop (`bk1`, `bk2`, `bk4`); a retaining wall under a turned slope is its mirror image
(`b2wf`). The
status bar says "turned" after the chosen block, and the Inspector after the block at the
cursor cell. (The 2016 version had a mirrored model of each, `b2f`, `t2`...; the level
generator still names a turned block so.)

The **Object tool** edits the dynamic objects (boxes, bars, ramps...), which are always on
the plane of the middle layer. The left button adds the chosen object at the point under
the cursor (rounded to a tenth of a physics unit) and selects it, or selects the object
under the cursor, and dragging moves it; Left Shift + the left button removes the object
under the cursor, and the right button picks its type and model as the chosen object. Space
adds the chosen object at the top of the cursor cell, so that it drops onto the block there
(two objects are never put in one place), Delete removes the selected object, the arrows
move it by a physics unit (Ctrl + the arrows by a tenth), or move the cursor cell while
nothing is selected, and Esc clears the selection (a second Esc leaves the editor).

The **Scenery tool** edits the props of the scenery: traffic cones, street lamps, utility
poles, fences, guardrails, posts with a chain and a judges' stand, which stand beside the
track and have no physics (the car drives through them). A guardrail is a cell long, so that
Space at the cells of a straight road puts a continuous one along it. A prop stands at a depth: negative behind the road, positive in front of it;
the road reaches 11.25 physics units to either side and each of the other layers the next
22.5. New props go to the chosen depth (-10 at first: on the road, behind where the car
drives), and the cursor cell and its grid are on its plane. The left button adds the chosen
prop at the point under the cursor and selects it, or selects the prop under the cursor, and
dragging moves it on the plane of its depth; the mouse places and drags in steps of 1.25
physics units, on which the surfaces of the flat road blocks lie, so that a prop stands on
the road and two pieces of the fence (half a cell long each) fill a cell. Left Shift + the
left button removes the prop under the cursor, and the right button picks its model, turn
and depth as the chosen ones. Space adds the chosen prop in the middle of the cursor cell,
standing on the road block there, Delete removes the selected prop, the arrows move it by
1.25 (Ctrl + the arrows by a tenth) or move the cursor cell while nothing is selected, T
turns it by a quarter, and PageUp / PageDown move it away from the camera / towards it;
without a selection T and PageUp / PageDown change the turn and the depth of the props to be
added. Esc clears the selection.

The **Select tool** works on many blocks of the active layer at once, and on the props of
the scenery that stand in the layer (by their depth; a prop is in the cell of its foot). A
click selects a cell, its block and its props (Ctrl adds or removes them), a drag from an
empty cell draws a rectangle that selects the blocks and the props in it, a drag of a
selected cell moves the selection by whole cells, Ctrl+A selects the layer with its props,
and Space adds the cell at the cursor to the selection, or takes it out again. The arrows
move the selection (Ctrl + the arrows only the cursor cell), Delete erases it, PageUp /
PageDown shift it, Ctrl+C / Ctrl+X / Ctrl+V copy, cut and paste it (the leftmost of the
copied cells, the lowest of those if several, goes to the cursor cell; the props keep their
depth relative to the layer), T turns the selected blocks round in their cells (the turned
ones back; blocks that do not turn stay as they are) and the selected props by a quarter,
and H mirrors it left to right, turning every block that turns round, so that a climb becomes
a descent (the barriers `bl2` and `bl2f`, `bl2s` and `bl2sf`, which are mirror images of each
other, swap), and turning the props as their mirror images. Edit > Style gives the selected
blocks a style of their own, as the style switch of the Palette gives it to the placed ones (*The level's* takes it away), in one step of undo; the
blocks without a look in the style stay as they are, and the status bar says how many took
it. Where the road passes from one style into another (a dirt road meeting the start block, a
tunnel or a classic road), the game and the editor draw the block of the styled side as the
transition by themselves, so the road needs nothing more; the Inspector says "Drawn as: off-road
to classic" there. A transition given to a block itself stays. Ctrl+PageUp /
Ctrl+PageDown (Edit > Move to the layer behind / in front) move the selection into the
next layer behind or in front, in the same cells, the props as far in depth; that layer
becomes the active one and the selection stays. A block of that layer in a cell they come to
is erased, decorations do not go into the middle layer and the finish stays there. While the
View menu hides the scenery, the props are left out of the selection.

### The windows

The windows can be moved, resized and docked; the editor remembers where they are. The View
menu shows and hides the Palette, the Inspector and the Level window, and F1 shows the
Controls window.

- **Palette** shows every block of the catalog as a turning 3D preview, in the groups of
  the catalog (Slopes, Tunnels...; as on the [catalog page](catalog/catalog.html)), with what
  the block is as a tooltip ("b2: Slope, 4 steps a cell (24°)"), and searchable by both; a
  click makes the block the chosen one and the Place tool active. Its style switch (*Style: The level's* at the
  top) gives the placed blocks a style of their own: classic, off-road, or a transition
  between two, left to right as the block is placed unturned (*Off-road to classic*: the
  dirt road ends at the right edge of the block); a block without a look in it takes the
  level's. The previews show the blocks in that style (or the level's), and the status bar
  names it after the chosen block. Decorations are greyed out while the middle
  layer is active. Its Objects tab shows the models of the dynamic objects, and a click
  makes one the chosen object and the Object tool active; its Scenery tab does the same for
  the props of the scenery and the Scenery tool.
- **Inspector** shows what the active tool works on: the block at the cursor cell of the
  active layer (its name, what it is and whether it is turned, its style, its own or the level's, its
  cell, shift and whether it is the finish, and the surface heights of a road block), the selected blocks (how many, and the
  cells they span) with buttons that turn, mirror and delete them, the selected object with its type, model and position,
  which can be typed (each edit one step of undo), and a button that removes it, or the
  selected prop with its position and depth to type, its turn as four buttons and a button
  that removes it (without a selection, the depth and the turn of the props to be added).
- **Level** is everything about the level that is not a block. It shows the name of the
  level (the full path as a tooltip) and edits the mood (Day, Sunset or Night, at once), the
  style of the track (Classic or Off-road, at once: the blocks with a look in it are drawn
  so, the others classic; see [the level format](#level-format)), the
  position in the level select (the checkbox *In the level select*, and the number; levels
  without one come after the others, sorted by name), whether it is a debug level (the
  checkbox *Debug level*, see [the level format](#level-format)) and the par time (the checkbox *Par
  time* and the seconds; while the level has none, the window says what the game estimates,
  "estimated: 10.0 s"). Each edit is one step of undo, saved with the level. It counts the
  blocks of each layer, the objects and the props of the scenery and gives the cells the
  level spans. Below are the
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
(see [content](content.md#catalog)); a turned block has the two swapped. With road mode on, a road block placed in the middle
layer (the back and front layers place as usual) goes into the column of the cursor cell at
the height where its left edge meets the right edge of the road block in the column to the
left, or its right edge the left edge of the block to the right when the stroke paints
leftwards. So a hill painted with the mouse or built with Space comes out continuous, the
cell and the shift being chosen for you ("Placed b2 turned at [3, -1], shift 1."). In a stroke
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
and preview, and a field for any level file), Open recent (the last 8 levels opened or
saved), Save (Ctrl+S) and Save as (Ctrl+Shift+S, a name in the folder; an existing file is
replaced only when confirmed). A new level is saved through Save as. New, Open, Esc and the close button of the window ask "Save
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
  "format": 2,
  "order": 1,
  "mood": "sunset",
  "style": "offroad",
  "debug": true,
  "middle": [
    {"block":"a1","x":0,"y":0,"shift":0},
    {"block":"b2","x":1,"y":0,"shift":-4,"turned":true},
    {"block":"a1","x":2,"y":0,"shift":-4,"style":"offroad-classic"},
    ...
  ],
  "back": [
    {"block":"kea1b","x":0,"y":0,"shift":0},
    {"block":"za","x":4,"y":-1,"shift":0,"depth":2},
    ...
  ],
  "front": [ ... ],
  "objects": [
    {"type":"Bar","model":"bar1","x":238,"y":-4},
    ...
  ],
  "scenery": [
    {"model":"lamp","x":45,"y":-1.25,"z":-12.5,"turn":1},
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
- `style` (optional, written only when it is set): the style of the track (#58): every
  block that has a look in it is drawn so (`offroad`: a dirt road with ruts and grass
  verges), the others classic, as the blocks of 2016 look. A block can have a `style` of its
  own: another style, or a transition between two, `first-second` (`offroad-classic`: the
  off-road style at the left edge of the block, the classic one at its right edge; turned,
  the other way round; see [the catalog](content.md#catalog)). Where the road of the middle
  layer passes from one style into another, the block of the styled side is drawn as the
  transition without being told (`Shared/RoadLooks`: the road blocks joined as the road mode
  of the editor joins them, a block of the classic style or without a look in the style on
  the other side), so a level of the off-road style is just `"style": "offroad"`. A style
  only changes how a block looks: its collision is that of the classic block, so a level
  plays the same in every style.
- `debug` (optional, written only when it is set): a level for development and testing.
  The level select shows the debug levels after all the others, from a row of their own under
  the title *DEBUG LEVELS* (for now to every player), sorted among themselves by `order` and
  name; the next level after one is the next debug level, and a debug level has no tutorial.
  It needs no preview image: the level select draws its name instead. The debug levels of
  `tools/levelgen/debug.js` (`test-*`) are the test tracks of #57 and #68: a flat road and
  climbs of 24°, 34° and 42° on the slab and on the earth, a drop onto a road with humps, and
  single steps of 2.5, 5 and 7.5 units and the stairs `s2`.
- `middle`: blocks of the physics layer, `back` and `front`: decoration layers. The back
  layers are one list: `depth` (1 when left out, as in every level made before 2026-11) is
  how many cells behind the road a block of `back` stands, 1 to 3; so are the front layers,
  `depth` 1 to 3 cells in front of the road.
- Blocks: `x`, `y` on the level grid (one cell = 22.5 physics units), `shift`
  moves the block vertically in steps of 2.5 physics units. `"turned": true` (written
  only when it is) turns a block that turns round by 180° about the vertical axis, in its
  cell: its model, its collision shape and its profile. `"finish": true` makes a
  block of the middle layer the finish of the level (one at most): the run ends when
  the middle of the car reaches its cell. A level without a finish has no end.
- `format`: 2. The game and the editor read only this format. Format 1 had the mirrored
  models of the blocks (`b2f` instead of `b2` turned) and is refused, and so is the format
  of the 2016 version.
- Objects: `x`, `y` in physics units.
- `scenery` (optional, written only when the level has some): the props beside the track,
  without physics. `model` is a prop of the `scenery` list of the catalog, `x`, `y` where
  its foot stands in physics units, `z` its depth (negative behind the road, positive in
  front of it; the road of the middle layer reaches 11.25 to either side, the other layers
  stand a cell, 22.5, apart), and `turn` (0 when left out) the quarter turns
  about the vertical axis, 0 to 3: a fence along the road turned once runs across it. The
  surface of a flat road block is 1.25 above the height of its profile, so a prop standing
  on the road of the start cell has `"y": -1.25`.

## Converted and generated levels

The levels lock, startblock and tunel were converted from the original `.lev`
format of the 2016 version, and on 2026-11-04 with summit from format 1 to format 2 (every
mirrored f model became its block turned, in the same cell).

The training levels (training1 to training3, the first in the level select, see the
tutorial in [Playing](playing.md#tutorial)), the summit level and the docks level (the last one,
which uses every kind of block, object and prop) are generated by scripts in
`tools/levelgen/`, which keep the road continuous and add the decorations; see its
[README](../tools/levelgen/README.md). When the level is changed in the editor (which
warns when such a level is opened), carry the changes over to the script, otherwise
running it again overwrites them.
