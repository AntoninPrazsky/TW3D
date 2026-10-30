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
| `side-view.ps1` | Side view of a level's road as an image (Windows PowerShell), to check that it is continuous |

## Heights

Heights are in shift steps of 2.5 physics units and grow downwards; one grid cell is
22.5 physics units, i.e. 9 steps. A block's position is `9 * y + shift` (its *base*).

`Profiles` in `lib.js` lists, for every road block, the height of the road surface at its left
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
t.obj("Box", "box", 50, Track.top(0) - 3.3);
for (const run of t.flatRuns()) t.kerbs(run.from, run.to, run.h);
t.mood = "sunset";                       // the light: "day" (default), "sunset" or "night"
t.save("content/Levels/example.json");
```

The car starts at x = -1.6, so the road has to be flat at height 0 around column 0.

Decoration helpers (back and front layers, no physics): `kerbs` (kerbs with end pieces along
a flat run), `barrier`, `railing`, `checkered`, `copyRoad` (a wider road or copies elsewhere),
and `stairs` (a footpath beside `b2f` climbs); `bothSides` uses `copyRoad` or `stairs` in both
layers. Barriers and railings stand on the back edge of the road, so dynamic objects go through
them; keep them away from places the objects can get to.

The camera looks from the front and from above, so the front layer is close to it: blocks there
should stay at the road surface or below it, or they hide the car. The back layer is seen mostly
above the road surface.

## Playability

The car climbs 24° slopes (`b2f`) even from a standstill, but not 34° ones (`b3f`) without
a run-up, and with the throttle alone it gets over a step of 2.5 units but not one of 5 units.
After long descents it is fast and flips easily over upward kinks, jumps and walls.
