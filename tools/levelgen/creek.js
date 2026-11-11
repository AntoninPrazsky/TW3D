// Generates content/Levels/creek.json: a field track and a long concrete road over a creek, the off-road and the classic
// style by turns. From the start a meadow track and a muddy dip, the run down to the creek, a bridge
// over it on steel pillars and a long, gentle concrete road with bends, where the wheels of the muddy car wash clean
// and leave tracks of mud, then a concrete kicker and a jump back into the mud, a washboard, a step down and a small
// kicker, and concrete to the finish.
//
//   node tools/levelgen/creek.js [output.json]

"use strict";

const path = require("path");
const { Track } = require("./lib.js");

const Cell = 22.5;
const top = Track.top;

const t = new Track(-3, 0);
t.style = "offroad";
t.mood = "day";
// Par: a fifth over the scripted full-throttle runs (26.5 to 29.5 s), until the owner's own runs set it
t.par = 35;

// ---- Road (middle layer, takes part in the physics) ----

// Start: wall behind the car, checkered start area, start gate with the START sign (classic: they have no off-road look)
t.put("middle", "u1f", -4, -4).put("middle", "t1f", -4, -13).put("middle", "t1f", -4, -22);
t.road("a1", "a1", "xa1", "xa1", "xa1", "sb");
t.put("middle", "sg", 2, -4).put("middle", "st2", 2, -13);

// 1. The meadow track: a washboard, a gentle dip with a high washboard at its bottom, met at some 50 units a second
// (crates on the meadow were pushed down into the dip ahead of the car, which ran onto them and turned over)
const meadow = t.x;
t.road("a1", "a1", "a1", "a1", "a1", "w1", "a1", "a1");
const dip = t.x;
t.road("e0", "b0", "b0", "d0", "a1", "w2", "a1", "d0f", "b0f", "b0f", "e0f", "a1");

// 2. The run down to the creek, rutted, with small rocks
const run = t.x;
t.road("a1", "rk1", "a1", "a1", "e1", "b1", "b1", "d1", "a1", "a1");

// 3. Concrete: a bridge over the creek on steel pillars, its deck flush with the road
const concrete = t.x;
t.withStyle("classic", () => t.road("a1", "a1"));
const bridge = t.x, bridgeH = t.h;
const BridgeCells = 6;
for (let i = 0; i < BridgeCells; i++) t.put("middle", "br1", bridge + i, bridgeH);
t.gap(BridgeCells);

// 4. The long concrete road on, gentle (the gentlest slopes and curves: on concrete the cars reach 80 units a second,
// and a steeper hill threw them ten cells through the air), while the wheels wash clean
const road = t.x;
t.withStyle("classic", () => t.road(
  "a1", "a1", "e0", "b0", "b0", "b0", "d0", "a1", "a1", "c0", "a1", "a1", "c0", "a1", "a1",
  "d0f", "b0f", "b0f", "e0f", "a1"));

// 5. A flat run to the concrete kicker and the jump back into the mud, the landing four steps below the lip (gentle
// kicker d0f: off a curved one the cars turned nose down in the air, see valley.js). No run down to it: on the flat the
// cars reach 55 to 70 units a second, and at that speed they flew off any crest before a descent, over the kicker,
// and fell into the gap
t.withStyle("classic", () => t.road("a1", "a1", "a1", "a1", "a1", "a1"));
const lip = t.x;
t.withStyle("classic", () => t.road("d0f"));
const lipH = t.h;
t.gap(3);
t.h = lipH + 4;

// 6. Mud: the landing, a low washboard and a step down, four flat cells after each, and a small kicker with six flat
// cells after it. Nothing rougher: the cars come here at 65 to 75 units a second, and a step up, a log, rocks or high
// washboards met at that speed threw them onto their roofs (a low washboard throws them up too, but they land on the
// flat after it)
const mud = t.x;
t.road("r2f", "a1", "a1", "a1", "a1", "a1", "a1", "w1", "a1", "a1", "a1", "a1", "k1", "a1", "a1", "a1", "a1", "d0f");
t.step(2);
t.road("a1", "a1", "a1", "a1", "a1", "a1");

// 7. Concrete to the finish
const finishRoad = t.x;
t.withStyle("classic", () => t.road("a1", "a1", "a1"));

// Finish: checkered floor and the finish gate, then the wall at the end, which stops the car
const finish = t.x, finishH = t.h;
t.road("xa1", "xa1", "xa1");
t.put("middle", "g", finish + 1, t.h - 4, { finish: true }).put("middle", "st", finish + 1, t.h - 13);
t.withStyle("classic", () => t.road("a1", "a1"));
const wall = t.x;
t.road("u1");
for (let i = 1; i <= 4; i++) t.put("middle", "t1", wall, t.h - 4 - 9 * i);

// ---- Decorations (back and front layers, no physics) ----

// The start and the finish: checkered areas, first, so that the ground goes under them
t.checkered("back", -1, 1, 0).checkered("front", -1, 1, 0);
t.checkered("back", finish, finish + 2, finishH).checkered("front", finish, finish + 2, finishH);
for (const layer of ["back", "front"]) {
  t.put(layer, "u1", wall, finishH - 4);
  for (let i = 1; i <= 3; i++) t.put(layer, "t1", wall, finishH - 4 - 9 * i);
}

// The creek under the bridge: its banks of meadow three cells under the deck, the pillars standing on them
const creekH = bridgeH + 27;
const floor = Math.max(creekH, ...[...t.cells.values()].map(cell => Math.max(cell.left, cell.right))) + 9;
for (let x = bridge; x < bridge + BridgeCells; x++)
  for (const layer of ["back", "front", "back2", "front2", "front3"]) {
    t.put(layer, "a1g", x, creekH, { style: "offroad" });
    for (let base = creekH + 9; base < floor + 5; base += 9) t.put(layer, "g2", x, base, { style: "offroad" });
  }
for (const x of [bridge + 1, bridge + 4]) {
  t.put("middle", "br2", x, bridgeH + 9);
  for (let base = bridgeH + 18; base < creekH; base += 9) t.put("middle", "br3", x, base);
}

// The countryside: ground, meadows, retaining walls under the concrete, hills behind
t.countryside(-3, wall, { floor });
// Hills behind the creek too, on its banks
for (let x = bridge; x < bridge + BridgeCells; x++)
  t.backdrop("back3", x, x, creekH, [["bk4", "bk2", null][x % 3]], { ground: "a1g", style: "offroad" });

// ---- Scenery (props beside the track, no physics) ----

// Guardrails along both edges of the bridge, facing the road, and the concrete's utility poles
for (let x = bridge; x < bridge + BridgeCells; x++) {
  t.prop("guardrail", x * Cell, top(bridgeH), 10.5, 2);
  t.prop("guardrail", x * Cell, top(bridgeH), -10.5, 0);
}
for (const x of [concrete, road + 1, road + 7, road + 12, road + 18, road + 23, finishRoad + 1]) {
  const surface = t.surface(x);
  if (surface !== undefined) t.prop("pole", x * Cell, top(surface), -11, 1);
}
// Fences by the field track
for (const x of [meadow + 1, meadow + 6, dip + 5, run + 2, mud + 3, mud + 11]) {
  const surface = t.surface(x);
  if (surface !== undefined) for (const half of [0.25, 0.75]) t.prop("fence", (x + half) * Cell, top(surface), -14);
}

const output = process.argv[2] || path.join(__dirname, "..", "..", "content", "Levels", "creek.json");
t.save(output, 10);
console.log(`${output}: ${t.middle.length} road blocks, ${t.back.length + t.back2.length + t.back3.length + t.front.length + t.front2.length + t.front3.length} decorations, ${t.objects.length} objects, ${t.scenery.length} props; meadow ${meadow}, dip ${dip}, run ${run}, concrete ${concrete}, bridge ${bridge} (h ${bridgeH}), road ${road}, lip ${lip} (h ${lipH}), mud ${mud}, finish ${finish}`);
