// Generates content/Levels/training1.json: the first training level (see the tutorial in docs/playing.md):
// just driving, over two humps and a small jump, to the finish.
//
//   node tools/levelgen/training1.js [output.json]

"use strict";

const path = require("path");
const { Track } = require("./lib.js");

const t = new Track(-3, 0);
// Par: a good run, a little slower than the owner's best (8.6 s)
t.par = 10;

// ---- Road (middle layer, takes part in the physics) ----

// Start: wall behind the car, checkered start area, start gate with the START sign
t.put("middle", "u2", -4, -4).put("middle", "t2", -4, -13).put("middle", "t2", -4, -22);
t.road("a1", "a1", "xa1", "xa1", "xa1", "sb");
t.put("middle", "sg", 2, -4).put("middle", "st2", 2, -13);
t.road("a1", "a1");

// Humps 5 units high with flat road between them (a hump of 10 units throws a fast car onto its roof)
const humps = t.x;
t.road("c1f", "c1", "a1", "a1", "c1f", "c1", "a1", "a1");

// A small jump: a ramp 5 units up, then the road goes on at the height it came from; a slow car just
// drops the step down, a fast one flies off the ramp
const jump = t.x;
t.road("b1f");
t.step(2);
t.road("a1", "a1", "a1");

// Finish: checkered floor and the finish gate, then the wall at the end, which stops the car
const finish = t.x, finishH = t.h;
t.road("xa1", "xa1", "xa1");
t.put("middle", "g", finish + 1, t.h - 4, { finish: true }).put("middle", "st", finish + 1, t.h - 13);
t.road("a1", "a1");
const wall = t.x;
t.road("u1");
for (let i = 1; i <= 4; i++) t.put("middle", "t1", wall, t.h - 4 - 9 * i);

// ---- Decorations (back and front layers, no physics) ----

for (const run of t.flatRuns()) t.kerbs(run.from, run.to, run.h);
t.remove("back", "kea1d", -4);
t.checkered("back", -1, 1, 0).checkered("front", -1, 1, 0);
// A wider road over the humps
t.bothSides("copyRoad", humps, humps + 1).bothSides("copyRoad", humps + 4, humps + 5);
t.checkered("back", finish, finish + 2, finishH).checkered("front", finish, finish + 2, finishH);
for (const layer of ["back", "front"]) {
  t.put(layer, "u1", wall, finishH - 4);
  for (let i = 1; i <= 3; i++) t.put(layer, "t1", wall, finishH - 4 - 9 * i);
}

const output = process.argv[2] || path.join(__dirname, "..", "..", "content", "Levels", "training1.json");
t.save(output, 1);
console.log(`${output}: ${t.middle.length} road blocks, ${t.back.length + t.front.length} decorations; humps at column ${humps}, jump at ${jump}, finish at ${finish}`);
