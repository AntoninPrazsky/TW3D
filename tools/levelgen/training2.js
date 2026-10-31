// Generates content/Levels/training2.json: the second training level (see the tutorial in docs/playing.md):
// a short flat run, then two flights of stairs the throttle alone does not get the car up, but turning
// the axles does, and the finish.
//
//   node tools/levelgen/training2.js [output.json]

"use strict";

const path = require("path");
const { Track } = require("./lib.js");

const t = new Track(-3, 0);

// ---- Road (middle layer, takes part in the physics) ----

// Start: wall behind the car, checkered start area, start gate with the START sign
t.put("middle", "u2", -4, -4).put("middle", "t2", -4, -13).put("middle", "t2", -4, -22);
t.road("a1", "a1", "xa1", "xa1", "xa1", "sb");
t.put("middle", "sg", 2, -4).put("middle", "st2", 2, -13);

// Drive: a flat run
t.road("a1", "a1", "a1");

// Stairs, two flights with a landing: the throttle alone gets stuck on them (and the tutorial's hint says
// to turn the axles), turning the axles walks the car up. A step of 5 units instead flips a fast car.
const stairs = t.x;
t.road("s1f", "s1f");
t.road("a1", "a1");
t.road("s1f", "s1f");
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

const output = process.argv[2] || path.join(__dirname, "..", "..", "content", "Levels", "training2.json");
t.save(output, 2);
console.log(`${output}: ${t.middle.length} road blocks, ${t.back.length + t.front.length} decorations; stairs at column ${stairs}, finish at ${finish}`);
