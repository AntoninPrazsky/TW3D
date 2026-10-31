// Generates content/Levels/training3.json: the third training level (see the tutorial in docs/playing.md):
// a long flight of stairs right after the start. Turning the axles alone, the car stops on the way up and
// rolls back; with the throttle held as well (or braking while the axles turn) it gets up to the finish.
//
//   node tools/levelgen/training3.js [output.json]

"use strict";

const path = require("path");
const { Track } = require("./lib.js");

const t = new Track(-3, 0);
// The long climb at sunset
t.mood = "sunset";
// Par: a good run with the throttle and the axles, a little slower than the owner's best (11.5 s);
// walking up with the brake takes about 30 s
t.par = 15;

// ---- Road (middle layer, takes part in the physics) ----

// Start: wall behind the car, checkered start area, start gate with the START sign
t.put("middle", "u2", -4, -4).put("middle", "t2", -4, -13).put("middle", "t2", -4, -22);
t.road("a1", "a1", "xa1", "xa1", "xa1", "sb");
t.put("middle", "sg", 2, -4).put("middle", "st2", 2, -13);

// The stairs, with no run-up: six flights' worth in one, 60 units up
const stairs = t.x;
t.road("s1f", "s1f", "s1f", "s1f", "s1f", "s1f");
t.road("a1");

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
t.checkered("back", finish, finish + 2, finishH).checkered("front", finish, finish + 2, finishH);
for (const layer of ["back", "front"]) {
  t.put(layer, "u1", wall, finishH - 4);
  for (let i = 1; i <= 3; i++) t.put(layer, "t1", wall, finishH - 4 - 9 * i);
}

const output = process.argv[2] || path.join(__dirname, "..", "..", "content", "Levels", "training3.json");
t.save(output, 3);
console.log(`${output}: ${t.middle.length} road blocks, ${t.back.length + t.front.length} decorations; stairs at column ${stairs}, finish at ${finish}`);
