// Generates content/Levels/valley.json: the off-road level (#57). A long dirt track through a valley, where the car
// gets muddy all over, with clean concrete parts where its wheels wash and leave tracks of mud: from the start the
// earth with washboards, logs and rocks, a gentle dip, the run down to a kicker and a long jump over a ravine onto
// concrete, a bridge on steel pillars and steps down, a concrete kicker and a jump back into the mud, a boulder to
// climb by turning the axles, small kickers and a hill, and a last stretch of concrete to the finish.
//
//   node tools/levelgen/valley.js [output.json]

"use strict";

const path = require("path");
const { Track } = require("./lib.js");

const top = Track.top;

const t = new Track(-3, 0);
t.style = "offroad";
// Daylight, so that the mud on the car shows
t.mood = "day";
// Par: to be set from the owner's runs
t.par = 75;

// The long jumps: cells of nothing between the lip and the landing, and how many steps the landing is below the lip.
// Their lips are the gentle kicker d0f: off the curved kicker d1f the cars turned nose down in the air by 60° to 135°
// and landed on their roofs (scripted runs, 2026-11-11), off d0f by 30° to 45°, flying some five cells at full speed
const Jump1 = { gap: 3, drop: 4 };
const Jump2 = { gap: 3, drop: 4 };

// ---- Road (middle layer, takes part in the physics) ----

// Start: wall behind the car, checkered start area, start gate with the START sign (classic: they have no off-road look)
t.put("middle", "u1f", -4, -4).put("middle", "t1f", -4, -13).put("middle", "t1f", -4, -22);
t.road("a1", "a1", "xa1", "xa1", "xa1", "sb");
t.put("middle", "sg", 2, -4).put("middle", "st2", 2, -13);

// 1. Into the mud: washboards, a low log, small rocks
const mud1 = t.x;
t.road("a1", "a1", "w1", "a1", "w2", "a1", "a1", "l1", "a1", "rk1", "a1", "a1");

// 2. A gentle dip with long crests and sags (the gentlest slope, b0), a washboard at its bottom (big rocks there threw
// a car at full speed onto its roof)
const dip = t.x;
t.road("e0", "b0", "b0", "d0", "a1", "w1", "a1", "d0f", "b0f", "b0f", "e0f", "a1");

// 3. A small kicker: a gentle lip, and the road two steps lower after it, flat where the car lands (the curved
// kicker d1f with a drop of 3 threw the light Sand Rail onto its roof)
t.road("a1", "d0f");
t.step(2);
t.road("a1", "a1", "a1", "a1");

// 4. The run down to the first kicker and the jump over the ravine onto the concrete, lower than the lip
const run1 = t.x;
t.road("a1", "b1", "b1", "b1", "b1", "d1", "a1", "a1");
const lip1 = t.x;
t.road("d0f");
const lip1H = t.h;
t.gap(Jump1.gap);
t.h = lip1H + Jump1.drop;

// 5. Concrete: the landing (a road end towards the ravine), a bridge on steel pillars over a gap (its deck flush with
// the road), steps and stairs down
const clean1 = t.x, clean1H = t.h;
t.withStyle("classic", () => t.road("r2f", "a1", "a1", "a1", "a1", "a1"));
const bridge = t.x, bridgeH = t.h;
const BridgeCells = 4;
for (let i = 0; i < BridgeCells; i++) t.put("middle", "br1", bridge + i, bridgeH);
t.gap(BridgeCells);
t.withStyle("classic", () => t.road("a1", "a1", "k1", "a1", "k2", "a1", "s2", "a1", "a1"));

// 6. The run down to the concrete kicker and the jump back into the mud
const run2 = t.x;
t.withStyle("classic", () => t.road("b1", "b1", "b1", "d1", "a1", "a1"));
const lip2 = t.x;
t.withStyle("classic", () => t.road("d0f"));
const lip2H = t.h;
t.gap(Jump2.gap);
t.h = lip2H + Jump2.drop;

// 7. Mud again: the landing, a gentle rise and a boulder on the flat after it, to brake for (the axles get the car
// over it; a car hitting it at full speed turns over, and so did cars still tilted from the rise), a step up and big
// rocks and a high log after it (where the car is slow), a hill with long crests, small kickers (three flat cells after each,
// as a fast car lands far), washboards and rocks, a step down and a second, longer hill
const mud2 = t.x;
t.road("r2f", "a1", "a1", "a1", "d0f", "b0f", "b0f", "e0f", "a1", "a1", "a1", "rk3", "a1", "a1", "k1f", "a1", "rk2", "a1", "a1", "a1", "l2", "a1");
const hill = t.x;
t.road("d1f", "b1f", "b1f", "e12f", "e1f", "a1", "a1", "e1", "e12", "b2", "d2", "a1", "a1");
t.road("w2", "a1", "a1", "d0f");
t.step(2);
t.road("a1", "a1", "a1", "rk1", "a1", "a1", "w1", "a1", "k3", "a1", "a1", "a1");
const hill2 = t.x;
t.road("d0f", "b0f", "b0f", "b0f", "e0f", "a1", "l2", "a1", "a1", "e1", "b1", "b1", "d1", "a1", "a1", "d0f");
t.step(2);
t.road("a1", "a1", "a1", "l1", "a1", "a1", "w2", "a1", "a1");

// 8. Concrete to the finish
const clean2 = t.x;
t.withStyle("classic", () => t.road("a1", "a1", "a1", "a1"));

// Finish: checkered floor and the finish gate, then the wall at the end, which stops the car
const finish = t.x, finishH = t.h;
t.road("xa1", "xa1", "xa1");
t.put("middle", "g", finish + 1, t.h - 4, { finish: true }).put("middle", "st", finish + 1, t.h - 13);
t.withStyle("classic", () => t.road("a1", "a1"));
const wall = t.x;
t.road("u1");
for (let i = 1; i <= 4; i++) t.put("middle", "t1", wall, t.h - 4 - 9 * i);

// ---- Decorations (back and front layers, no physics) ----

// The bridge: a pillar under the deck in its second and third cell, down to the floor of the ravine
const ravineFloor = bridgeH + 36;
for (const x of [bridge + 1, bridge + 2]) {
  t.put("middle", "br2", x, bridgeH + 9);
  for (let base = bridgeH + 18; base < ravineFloor; base += 9) t.put("middle", "br3", x, base);
}

// The start and the finish: checkered areas, first, so that the ground goes under them (see Track.fillUnder)
t.checkered("back", -1, 1, 0).checkered("front", -1, 1, 0);
t.checkered("back", finish, finish + 2, finishH).checkered("front", finish, finish + 2, finishH);

// Ground under the earth (earth and meadow) and retaining walls under the concrete, down to a common floor a cell under
// the lowest road
const floor = Math.max(...[...t.cells.values()].map(cell => Math.max(cell.left, cell.right))) + 9;
t.fillUnder(-3, run1 + 7, { floor });
t.fillUnder(lip1, lip1, { floor });
t.fillUnder(clean1, bridge - 1, { floor, layers: ["back"] }).fillUnder(clean1, bridge - 1, { floor, wall: true });
t.fillUnder(bridge + BridgeCells, lip2, { floor, layers: ["back"] }).fillUnder(bridge + BridgeCells, lip2, { floor, wall: true });
t.fillUnder(mud2, clean2 - 1, { floor });
t.fillUnder(clean2, wall, { floor, layers: ["back"] }).fillUnder(clean2, wall, { floor, wall: true });

// In front of the road, two and three cells (the camera sees them as it turns at the start): the meadow following the
// road, a cell lower in front of the concrete, so that its retaining walls rise above it (not under the bridge, where
// the ravine is)
const ahead = ["front2", "front3"];
t.fillUnder(-3, run1 + 7, { floor, layers: ahead }).fillUnder(lip1, lip1, { floor, layers: ahead });
t.fillUnder(clean1, lip2, { floor, layers: ahead, style: "offroad", lower: 9 });
t.fillUnder(mud2, clean2 - 1, { floor, layers: ahead });
t.fillUnder(clean2, wall, { floor, layers: ahead, style: "offroad", lower: 9 });

for (const layer of ["back", "front"]) {
  t.put(layer, "u1", wall, finishH - 4);
  for (let i = 1; i <= 3; i++) t.put(layer, "t1", wall, finishH - 4 - 9 * i);
}

// The valley behind: the meadow two cells behind the road follows it down; three cells behind, hills, rocks and copses
// on the meadow at the height of the road beside them
t.fillUnder(-3, run1 + 7, { floor, layers: ["back2"] });
t.fillUnder(clean1, clean2 + 3, { floor, layers: ["back2"] });
const hills = ["bk4", "bk2", null, "bk2f", "bk1", null, "bk4f", "bk2", null];
let backdropH = 0;
for (let x = -4; x <= wall + 1; x++) {
  const surface = t.surface(x);
  if (surface !== undefined) backdropH = Math.max(backdropH, Math.ceil(surface));
  t.backdrop("back3", x, x, backdropH, [hills[(x + 4) % hills.length]], { ground: "a1g", style: "offroad" });
}

// Utility poles along the concrete, fences by the mud
for (const x of [clean1 + 1, clean1 + 4, bridge + BridgeCells + 2, bridge + BridgeCells + 7, clean2 + 1]) {
  const surface = t.surface(x);
  if (surface !== undefined) t.prop("pole", x * 22.5, top(surface), -11, 1);
}
for (const x of [mud1 + 1, mud1 + 6, dip + 4, mud2 + 2, hill + 6, hill2 + 6]) {
  const surface = t.surface(x);
  if (surface !== undefined) for (const half of [0.25, 0.75]) t.prop("fence", (x + half) * 22.5, top(surface), -14);
}

const output = process.argv[2] || path.join(__dirname, "..", "..", "content", "Levels", "valley.json");
t.save(output, 9);
console.log(`${output}: ${t.middle.length} road blocks, ${t.back.length + t.back2.length + t.back3.length + t.front.length + t.front2.length + t.front3.length} decorations, ${t.scenery.length} props; mud from ${mud1}, dip ${dip}, lip ${lip1} (h ${lip1H}), concrete ${clean1}, bridge ${bridge}, lip ${lip2} (h ${lip2H}), mud ${mud2}, hill ${hill}, hill ${hill2}, concrete ${clean2}, finish ${finish}`);
