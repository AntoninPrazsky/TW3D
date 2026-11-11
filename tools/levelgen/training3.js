// Generates content/Levels/training3.json: the third training level, the lesson of the brake (#19): a sliding ramp.
// After a straight the car runs up the ramp of a loose trailer onto its bed and brakes: the trailer slides on with the
// car on it until the jaws of the lock catch its front, and the car climbs onto the road above by turning the axles
// with the throttle held. A car that does not brake runs over the trailer and stops in front of the lock, where it
// cannot climb the wall (the restart hint of the tutorial tells it to restart). The distances are those of the lock
// level (the lock block in the tenth cell from the start): with a longer run-up the car would fly over the lock.
//
//   node tools/levelgen/training3.js [output.json]

"use strict";

const path = require("path");
const { Track } = require("./lib.js");

const Cell = 22.5;
const top = Track.top;

const t = new Track(-3, 0);
// The lesson in the evening light
t.mood = "sunset";
t.par = 20;

// ---- Road (middle layer, takes part in the physics) ----

// Start: wall behind the car, checkered start area, start gate with the START sign
t.put("middle", "u1f", -4, -4).put("middle", "t1f", -4, -13).put("middle", "t1f", -4, -22);
t.road("a1", "a1", "xa1", "xa1", "xa1", "sb");
t.put("middle", "sg", 2, -4).put("middle", "st2", 2, -13);

// The straight to the trailer, the trailer before the lock and the lock block 7.5 units up
const trailer = t.x;
t.road("a1", "a1", "a1", "a1", "a1", "a1", "a1");
const lock = t.x;
// The trailer stands 3 cells before the lock block, 3.6 units nearer than in the lock level, so that a car that brakes as
// soon as it is on the trailer slides it into the lock (scripted drives of the three cars, 2026-11-11: braked on the
// trailer 12 of 18 runs got to the finish, the rest stopped mostly in climbing off it; none of 8 without the brake got
// there; at 2.9 cells one car got there without braking)
t.obj("Mov", "mov1", (lock - 3) * Cell, top(t.h) - 3.74);
t.step(-3);
t.road("lo", "a1", "a1");

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
console.log(`${output}: ${t.middle.length} road blocks, ${t.back.length + t.front.length} decorations; trailer run from column ${trailer}, lock at ${lock}, finish at ${finish}`);
