# Level generator

Levels can be written as small [Node.js](https://nodejs.org/) scripts instead of being built
block by block in the editor. The script describes the road as a sequence of blocks and the
builder places each block so that it starts at the height where the previous one ended.

```sh
node tools/levelgen/summit.js        # writes content/Levels/summit.json
node tools/levelgen/summit.js out.json
```

Rebuild the game afterwards, so the level gets copied to the build output.

## Files

| File | Contents |
|---|---|
| `lib.js` | `Track`: the road builder and helpers for decorations |
| `summit.js` | The *summit* level |
| `docks.js` | The *docks* level: every kind of block, object and prop, and the three back layers; a sliding ramp to brake on and climb from by turning the axles, pits for barrels, a ramp over the roofs of containers, a hump to brake for, stairs, and a jump across the harbour that needs full speed |
| `training1.js`, `training2.js`, `training3.js` | The training levels, the first three of the level select: driving over humps and a small jump, stairs, long stairs |
| `side-view.ps1` | Side view of a level's road as an image (Windows PowerShell), to check that it is continuous |

## Heights

Heights are in shift steps of 2.5 physics units and grow downwards; one grid cell is
22.5 physics units, i.e. 9 steps. A block's position is `9 * y + shift` (its *base*).

The `profile` of the blocks in `content/catalog.json` (`Profiles` in `lib.js` reads them from there,
the level editor uses them too) gives, for every road block, the height of the road surface at its left
and right edge relative to its base, measured on the collision bitmaps. For example `b2f`
(a 24° climb) is `[4, 0]`: it starts 4 steps below its base and ends at it, so it climbs
10 physics units. A block missing there cannot be used with `road()`, but can still be placed
directly with `put()`.

## Building a level

```js
const { Track } = require("./lib.js");
const t = new Track(-3, 0);              // first column and surface height
t.road("a1", "a1", "c2f", "c2", "a1");    // road blocks, one column each
t.step(-1);                              // a step up by 2.5 units
t.put("back", "st2", 2, -5);             // any block in any layer, by its base
t.put("back3", "za", 4, -9);             // three cells behind the road ("back2": two)
t.obj("Box", "box", 50, Track.top(0) - 3.3);
t.prop("lamp", 45, -1.25, -12.5, 1);     // a street lamp behind the road, turned (scenery: no physics)
for (const run of t.flatRuns()) t.kerbs(run.from, run.to, run.h);
t.mood = "sunset";                       // the light: "day" (default), "sunset" or "night"
t.par = 20;                              // the par time in seconds (default: estimated by the game)
t.save("content/Levels/example.json");
```

The car starts at x = -1.6, so the road has to be flat at height 0 around column 0.

`prop(model, x, y, z, turn)` adds a prop of the scenery (no physics): its foot at `x`, `y` in
physics units (the road of a flat block at height `h` is at `h * 2.5 - 1.25`), at the depth
`z` (negative behind the road, positive in front of it), turned by quarter turns.

The layers are `"middle"` (the road, with physics), `"back"`, `"back2"` and `"back3"` (one,
two and three cells behind the road; the level file keeps them in one list with the `depth` of
the blocks) and `"front"` (one cell in front of it).

Decoration helpers (back and front layers, no physics): `kerbs` (kerbs with end pieces along
a flat run), `barrier`, `railing`, `checkered`, `copyRoad` (a wider road or copies elsewhere),
and `stairs` (a footpath beside `b2f` climbs); `checkered`, `copyRoad` and `stairs` take any
of the back layers too, and `bothSides` uses `copyRoad` or `stairs` in the back and the front
layer. Barriers and railings stand on the back edge of the road, so dynamic objects go through
them; keep them away from places the objects can get to.

The camera looks from the front and from above, so the front layer is close to it: blocks there
should stay at the road surface or below it, or they hide the car. The back layers are seen mostly
above the road surface, the deeper ones higher on the screen.

## Playability

The car climbs 24° slopes (`b2f`) even from a standstill, but not 34° ones (`b3f`) without
a run-up, and with the throttle alone it gets over a step of 2.5 units but not one of 5 units.
After long descents it is fast and flips easily over upward kinks, jumps and walls.

The pits of the road hold barrels: `ba1` one, `ba2` two (a barrel is 6 units across). A barrel standing on the rim of
a pit (3.6 units from the middle of a `ba1` block, 6.6 of a `ba2`) tips in at a touch, and the car drives over it; at
speed the car flies over the pits anyway. The ramp object (`rm`, 26 units long and 4.7 high at its curved end) lying on
the road at a ledge, overhanging it, throws a fast car over the containers (`co`, `co2`, 13.8 high) standing in a yard
12.5 units below. The sliding ramp (`mov1`, a trailer) needs the lock block `lo` 7.5 units up and a run-up as in the
lock level: the car runs onto its bed and brakes, the trailer slides into the lock, and the car climbs onto the road
above by turning the axles. `docks.js` has the distances of the lock level for both.
