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
and right edge relative to its base, measured on the collision bitmaps. For example `b2`
(a 24° descent) is `[0, 4]`: it starts at its base and ends 4 steps below it, so it descends
10 physics units. A block missing there cannot be used with `road()`, but can still be placed
directly with `put()`.

## Turned blocks

A block that turns (`"turns": true` in the catalog: the slopes, the crests and bends, the road
ends, the rocks, the stairs `s1` and `s2`, the steps `k1` to `k3` and the walls `t1` and `u1`) is named turned with an `f` after its name, as the mirrored models of the 2016
version were: `"b2f"` is `b2` turned round, a 24° climb with the profile `[4, 0]`, and the
level file gets `{"block":"b2","x":…,"turned":true}`. The walls turned are `"t1f"` and
`"u1f"`. A name of the catalog is always the block itself (`"bl2f"` and `"kea1f"` are blocks of
their own), and a name that is neither is an error.

## Building a level

```js
const { Track } = require("./lib.js");
const t = new Track(-3, 0);              // first column and surface height
t.road("a1", "a1", "c2f", "c2", "a1");    // road blocks, one column each
t.step(-1);                              // a step up by 2.5 units
t.road("r2").gap(1).road("r2f");         // a gap of two cells between two road ends
t.put("back", "st2", 2, -5);             // any block in any layer, by its base
t.put("back3", "za", 4, -9);             // three cells behind the road ("back2": two)
t.obj("Box", "box", 50, Track.top(0) - 3.3);
t.prop("lamp", 45, -1.25, -12.5, 1);     // a street lamp behind the road, turned (scenery: no physics)
for (const run of t.flatRuns()) t.kerbs(run.from, run.to, run.h);
t.mood = "sunset";                       // the light: "day" (default), "sunset" or "night"
t.par = 20;                              // the par time in seconds (default: estimated by the game)
t.style = "offroad";                     // the style of the track (default: classic)
t.withStyle("classic", () => t.road("a1", "a1"));  // a part of the road in a style of its own
t.save("content/Levels/example.json");
```

The car starts at x = -1.6, so the road has to be flat at height 0 around column 0.

`prop(model, x, y, z, turn)` adds a prop of the scenery (no physics): its foot at `x`, `y` in
physics units (the road of a flat block at height `h` is at `h * 2.5 - 1.25`), at the depth
`z` (negative behind the road, positive in front of it), turned by quarter turns.

The layers are `"middle"` (the road, with physics), `"back"`, `"back2"` and `"back3"` (one,
two and three cells behind the road; the level file keeps them in one list with the `depth` of
the blocks) and `"front"` (one cell in front of it).

Decoration helpers (back and front layers, no physics): `fillUnder(from, to, { layers, depth, floor,
wall })` (ground under the road blocks of the columns, the shape of each: `b2g` under `b2`, `b2g` turned under `b2f`,
`a1g` under every flat block, and `depth` whole cells of `g2` under it or as many as reach the height `floor`, in the
front and the first back layer by default, in the style of its road block; `wall: true` puts the retaining wall
`b2w`, `b2wf`, `a1w`, `g2w` instead, in the front layer), `backdrop(layer, from, to, h, pattern, { ground, style })`
(the blocks of the pattern by turns along a deeper layer on the ground at height h: rocks `bk1`, hills `bk2`, halls
`bk3`, copses `bk4`, the natural ones turned as `bk2f`; `ground: "a1g", style: "offroad"` stands them on grass), `kerbs` (kerbs with end pieces along
a flat run), `barrier`, `railing`, `checkered`, `copyRoad` (a wider road or copies elsewhere),
and `stairs` (a footpath beside `b2f` climbs); `checkered`, `copyRoad` and `stairs` take any
of the back layers too, and `bothSides` uses `copyRoad` or `stairs` in the back and the front
layer. Barriers and railings stand on the back edge of the road, so dynamic objects go through
them; keep them away from places the objects can get to.

The camera looks from the front and from above, so the front layer is close to it: blocks there
should stay at the road surface or below it, or they hide the car. The back layers are seen mostly
above the road surface, the deeper ones higher on the screen.

## Playability

The car climbs 24° slopes (`b2f`, `b2` turned) even from a standstill, but not 34° ones (`b3f`) without
a run-up, and with the throttle alone it gets over a step of 2.5 units but not one of 5 units.
The steps of #61 (scripted drives of each car on 2026-11-09, up to the step on the throttle): every car
climbs `k1f` (2.5 units) on the throttle alone, none climbs `k2f` or `s2f` (5 units) so. Holding the
throttle and turning the axles (↑) at the step got the Buggy over `k2f` and `s2f` and the Sand Rail and
the Crawler over `k2f` (not every time); turning them the other way (↓), or braking while turning them,
did not. No car got over `k3f` (7.5 units) so: it needs a player's skill, if it can be climbed at all.

A car stays on the road over a crest only while it is slow enough for the curve: the gravity is 12
physics units a second squared (game time), so below about the square root of 12 times the radius
of the crest. The crests `e1`, `e2` and `e3` round over in one cell (a radius of about 101, 50 and
34 units: some 35, 25 and 20 units a second), so at full throttle every car takes off over them as
over a sharp crest. A longer crest is `e1` and then the bends (`e1`, `e12`, `e23`...; turned for the
top of a climb: `b2f`, `e12f`, `e1f`, `a1`), with the radius of `e1` all the way, the gentlest the
grid of whole shift steps allows: on 2026-11-09 the Crawler at full throttle stayed on the road over
the top of a hill of `b2f` rounded so (the climb had slowed it), where it took off over one rounded by
`e2f`; after a flat run it still flew over `e1`, `e12`, `e23` into `b3`.

A gap between road ends (`r1`–`r3`) on a flat road is a trap rather than a jump: on 2026-11-09 the Crawler
at full throttle dropped its front wheels into a gap of one cell (`r2`, `r2f`) and turned over on the far
edge, and fell into one of two cells. Jump a gap from a kicker (`d1f`, `d2f`), as the shipped levels do.

A low ceiling (`br1` hung over the road, placed with `put`): every car passes under one 4 shift steps above
the road (10 units), and every car gets stuck under one 3 steps above it (2026-11-09).

Round obstacles do not stop the triple wheels: every car got over half a cylinder lying across the road up
to 7.5 units high on the throttle alone, even from a run-up of a cell and a half (2026-11-09), where a step of
5 units stops them. So the washboards `w1`, `w2`, the logs `l1`, `l2` (the high one throws a fast car up) and
the rocks `rk1`, `rk2` shake the car but need nothing; the boulder `rk3` (sheer faces, about 5.5 units) stops
every car on the throttle alone, and turning the axles at it got every car over it from a short run-up.
After long descents it is fast and flips easily over upward kinks, jumps and walls.

The pits of the road hold barrels: `ba1` one, `ba2` two (a barrel is 6 units across). A barrel standing on the rim of
a pit (3.6 units from the middle of a `ba1` block, 6.6 of a `ba2`) tips in at a touch, and the car drives over it; at
speed the car flies over the pits anyway. The ramp object (`rm`, 26 units long and 4.7 high at its curved end) lying on
the road at a ledge, overhanging it, throws a fast car over the containers (`co`, `co2`, 13.8 high) standing in a yard
12.5 units below. The sliding ramp (`mov1`, a trailer) needs the lock block `lo` 7.5 units up and a run-up as in the
lock level: the car runs onto its bed and brakes, the trailer slides into the lock, and the car climbs onto the road
above by turning the axles. `docks.js` has the distances of the lock level for both.
