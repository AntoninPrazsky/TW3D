// The debug levels: tracks for development and testing, under DEBUG LEVELS in the level select (#57, #68).
//   node tools/levelgen/debug.js        # writes content/Levels/test-*.json
// Each is a short test of one thing, with the start and the finish of the training levels:
//   test-flat, test-flat-offroad          a long flat road: acceleration, top speed, braking (classic, earth)
//   test-climb24/34/42[-offroad]          a slope of 24°, 34° or 42° climbed after 3.5 cells of run-up; stop on it and
//                                         start again to test a hill start, brake on it to test holding
//   test-drop                             a drop of 20 units onto a flat road, then two humps: the bounce of the car
//   test-step-k1, -k2, -k3, test-stairs-s2   one step up of 2.5, 5 or 7.5 units, or the stairs of 5, before the finish
"use strict";
const path = require("path");
const { Track } = require("./lib.js");

const out = path.join(__dirname, "..", "..", "content", "Levels");
let order = 0;

/** A debug level: the start of the training levels, the road the test builds, the finish and the end wall. */
function level(name, style, build) {
  const t = new Track(-3, 0);
  t.debug = true;
  if (style) t.style = style;
  t.put("middle", "u1f", -4, -4).put("middle", "t1f", -4, -13).put("middle", "t1f", -4, -22);
  t.road("a1", "a1", "xa1", "xa1", "xa1", "sb");
  t.put("middle", "sg", 2, -4).put("middle", "st2", 2, -13);
  build(t);
  const finish = t.x;
  t.road("xa1", "xa1", "xa1");
  t.put("middle", "g", finish + 1, t.h - 4, { finish: true }).put("middle", "st", finish + 1, t.h - 13);
  t.road("a1", "a1", "a1", "a1");
  const wall = t.x;
  t.road("u1");
  for (let i = 1; i <= 4; i++) t.put("middle", "t1", wall, t.h - 4 - 9 * i);
  t.par = 60;
  t.save(path.join(out, name + ".json"), ++order);
  console.log(`${name}: finish at column ${finish}`);
}

for (const [suffix, style] of [["", null], ["-offroad", "offroad"]]) {
  level("test-flat" + suffix, style, t => t.road(...Array(26).fill("a1")));
}
for (const [angle, block, cells] of [[24, "b2f", 3], [34, "b3f", 2], [42, "b4f", 2]]) {
  for (const [suffix, style] of [["", null], ["-offroad", "offroad"]]) {
    level(`test-climb${angle}${suffix}`, style, t => t.road("a1", ...Array(cells).fill(block), "a1", "a1", "a1", "a1"));
  }
}
level("test-drop", null, t => {
  t.road("a1", "a1", "a1", "a1");
  t.step(8);
  t.road(...Array(12).fill("a1"));
  t.road("c1f", "c1", "a1", "a1", "c1f", "c1", "a1", "a1", "a1", "a1");
});
for (const [name, step] of [["test-step-k1", "k1f"], ["test-step-k2", "k2f"], ["test-step-k3", "k3f"], ["test-stairs-s2", "s2f"]]) {
  level(name, null, t => t.road("a1", "a1", "a1", step, "a1", "a1"));
}
