// Generates content/Levels/summit.json: climb a mountain, push the crates off the summit,
// race down and climb out of the valley to the finish.
//
//   node tools/levelgen/summit.js [output.json]

"use strict";

const path = require("path");
const { Track, Cell } = require("./lib.js");

const top = Track.top;
const t = new Track(-3, 0);

// ---- Road (middle layer, takes part in the physics) ----

// Start: wall behind the car, checkered start area, start gate with the START sign
t.put("middle", "u2", -4, -4).put("middle", "t2", -4, -13).put("middle", "t2", -4, -22);
t.road("a1", "a1", "xa1", "xa1", "xa1", "sb");
t.put("middle", "sg", 2, -4).put("middle", "st2", 2, -13);
t.road("a1", "a1");

// Rollers: humps 5, 10 and 15 units high
t.road("c1f", "c1", "a1", "c2f", "c2", "a1", "c3f", "c3", "a1", "a1");

// The mountain: three climbs with terraces between them. The slopes are at most 24°,
// so the car gets up even when it has stopped at the foot of a climb.
const climb1 = t.x;
t.road("d1f", "b1f", "b1f", "b1f");
const terrace1 = t.x;
t.road("a1", "a1", "a1");
const climb2 = t.x;
t.road("d2f", "b2f", "b2f", "b2f");
const terrace2 = t.x;
t.road("a1", "a1", "a1", "a1");
const climb3 = t.x;
t.road("d2f", "b2f", "b2f", "b2f");
const summit = t.x, summitH = t.h;
t.road("a1", "a1", "a1", "a1", "a1");

// Crates at the summit edge to push down
const edge = (t.x - 0.5) * Cell;
t.obj("Box", "box", edge - 4, top(summitH) - 3.3);
t.obj("Box", "box", edge - 11, top(summitH) - 3.3);
t.obj("Box", "box", edge - 7.5, top(summitH) - 9.9);

// Descent, steeper and steeper, then down to the valley
const descent = t.x;
t.road("b2", "b3", "b4", "b4", "b3", "d3");
const valley = t.x;
t.road("a1", "a1");

// A long climb out of the valley slows the fast car down before the finish
const climbOut = t.x;
t.road("d2f", "b2f", "b2f", "b2f", "b2f", "b2f", "b2f", "b2f", "b2f");
const plateau = t.x, plateauH = t.h;
t.road("a1");

// Finish on the plateau: checkered floor and the finish gate, then a pile of crates
// in front of the wall at the end, which stops the car
const finish = t.x;
t.road("xa1", "xa1", "xa1");
t.put("middle", "g", finish + 1, t.h - 4).put("middle", "st", finish + 1, t.h - 13);
t.road("a1", "a1", "a1");
const wall = t.x;
t.road("u1");
for (let i = 1; i <= 4; i++) t.put("middle", "t1", wall, t.h - 4 - 9 * i);
const crate = 6.5;
[4, 3, 2].forEach((count, row) => {
  for (let i = 0; i < count; i++)
    t.obj("Box", "box", (wall - 0.5) * Cell - crate * (count - i - 0.5) - row * crate / 2, top(t.h) - crate / 2 - 0.1 - row * crate);
});

// ---- Decorations (back and front layers, no physics) ----

// Kerbs along all flat parts of the road; the back one at the start has no end piece at the wall
for (const run of t.flatRuns()) t.kerbs(run.from, run.to, run.h);
t.remove("back", "kea1d", -4);

// Barriers and railings stand on the back edge of the road, where the crates (as deep as the road)
// would go through them. So they are only where no crate gets: not from the summit edge on.

// Start: checkered area on both sides of the start, barriers beside it
t.checkered("back", -1, 1, 0).checkered("front", -1, 1, 0);
t.barrier(-3, -2, 0).barrier(3, 4, 0);

// Rollers and the first, gentle climb: a wider road, except the columns with the kerb end pieces
t.bothSides("copyRoad", 6, 11).bothSides("copyRoad", climb1 + 1, climb1 + 3);

// Terraces: a barrier on the first one, a railing on the second
t.barrier(terrace1, terrace1 + 2, t.surface(terrace1));
t.railing(terrace2, terrace2 + 3, t.surface(terrace2));

// Footpaths with stairs beside the steeper climbs
t.bothSides("stairs", climb2, climb2 + 3).bothSides("stairs", climb3, climb3 + 3).bothSides("stairs", climbOut, climbOut + 8);

// Summit: a wider plateau with a railing
t.bothSides("copyRoad", summit, summit + 4);
t.railing(summit, summit + 2, summitH);

// Descent: a wide slope
t.bothSides("copyRoad", descent, descent + 5);

// Finish: checkered area on both sides, the end wall continues on both sides
t.checkered("back", finish, finish + 2, plateauH).checkered("front", finish, finish + 2, plateauH);
for (const layer of ["back", "front"]) {
  t.put(layer, "u1", wall, plateauH - 4);
  for (let i = 1; i <= 3; i++) t.put(layer, "t1", wall, plateauH - 4 - 9 * i);
}

const output = process.argv[2] || path.join(__dirname, "..", "..", "content", "Levels", "summit.json");
t.save(output);
console.log(`${output}: ${t.middle.length} road blocks, ${t.back.length + t.front.length} decorations, ${t.objects.length} objects`);
