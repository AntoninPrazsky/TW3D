// Generates content/Levels/quarry.json: a quarry at sunset, its concrete haul roads and its floor of earth by turns.
// From the start a concrete haul road, the run down into the quarry, its floor with washboards, a tunnel
// through the rock, stairs up out of the quarry with a boulder at their top to climb by turning the axles, the rim of the
// quarry, and a jump onto the concrete of the finish.
//
//   node tools/levelgen/quarry.js [output.json]

"use strict";

const path = require("path");
const { Track } = require("./lib.js");

const Cell = 22.5;
const top = Track.top;

const t = new Track(-3, 0);
t.style = "offroad";
// Sunset: a low sun, long shadows over the quarry
t.mood = "sunset";
// Par: a sixth over the scripted full-throttle runs (22 to 28 s), until the owner's own runs set it
t.par = 32;

// ---- Road (middle layer, takes part in the physics) ----

// Start: wall behind the car, checkered start area, start gate with the START sign (classic: they have no off-road look)
t.put("middle", "u1f", -4, -4).put("middle", "t1f", -4, -13).put("middle", "t1f", -4, -22);
t.road("a1", "a1", "xa1", "xa1", "xa1", "sb");
t.put("middle", "sg", 2, -4).put("middle", "st2", 2, -13);

// 1. The haul road: concrete (a pit with two barrels on its rim here, as in the docks, let the car push one of them
// ahead of it through the whole quarry)
const haul = t.x;
t.withStyle("classic", () => t.road("a1", "a1", "a1", "a1", "a1", "a1", "a1"));

// 2. Down into the quarry on earth, gently
const descent = t.x;
t.road("e0", "b0", "b0", "b0", "e1", "b1", "b1", "d1", "a1");

// 3. The floor of the quarry: a low washboard, far from the tunnel (the cars come down here at 55 to 60 units a second,
// and a high washboard threw them through the air, nose down, into the mouth of the tunnel)
const floor1 = t.x;
t.road("a1", "w1", "a1", "a1", "a1", "a1", "a1", "a1");

// 4. A tunnel through the rock (classic: the tunnel has no off-road look)
const tunnel = t.x, tunnelH = t.h;
t.road("tu1s", "tu2s", "tu2s", "tu3s");
["tu1v", "tu2v", "tu2v", "tu3v"].forEach((vault, i) => t.put("middle", vault, tunnel + i, tunnelH - 4));
t.withStyle("classic", () => t.road("a1", "a1"));

// 5. The far floor of the quarry: a low washboard, a short climb, then stairs up out of the quarry on concrete, cones
// before them (a car meeting them at full speed, 50 to 60 units a second, may turn over on them: brake; the throttle
// alone does not get the car up them, turning the axles does; three flights were too long, the cars slid back down),
// and at their top, where the car is nearly standing, a boulder (sheer faces: the axles get the car over it; met at 20
// units a second it threw a car onto its roof)
const floor2 = t.x;
t.road("a1", "a1", "w1", "a1", "a1", "d1f", "b1f", "b1f", "e1f", "a1");
const stairs = t.x;
t.withStyle("classic", () => t.road("s1f", "s1f"));
const boulder = t.x;
t.road("rk3");

// 6. The rim of the quarry: small rocks while the car is still slow, a flat run, a gentle kicker and the jump over a
// gap of two cells onto the concrete of the finish, four steps lower, with six flat cells to land on (off a curved kicker
// the cars turned nose down in the air; the car comes to the lip from the boulder at only 40 to 50 units a second, and
// the Crawler fell short of a gap of three cells)
const rim = t.x;
t.road("a1", "a1", "a1", "rk1", "a1", "a1", "a1", "a1", "a1", "a1", "a1", "a1", "a1");
const lip = t.x;
t.road("d0f");
const lipH = t.h;
t.gap(2);
t.h = lipH + 4;
const landing = t.x;
t.withStyle("classic", () => t.road("r2f", "a1", "a1", "a1", "a1", "a1", "a1"));

// Finish: checkered floor and the finish gate, then the wall at the end, which stops the car
const finish = t.x, finishH = t.h;
t.road("xa1", "xa1", "xa1");
t.put("middle", "g", finish + 1, t.h - 4, { finish: true }).put("middle", "st", finish + 1, t.h - 13);
t.withStyle("classic", () => t.road("a1", "a1"));
const wall = t.x;
t.road("u1");
for (let i = 1; i <= 4; i++) t.put("middle", "t1", wall, t.h - 4 - 9 * i);

// ---- Decorations (back and front layers, no physics) ----

t.checkered("back", -1, 1, 0).checkered("front", -1, 1, 0);
t.checkered("back", finish, finish + 2, finishH).checkered("front", finish, finish + 2, finishH);
for (const layer of ["back", "front"]) {
  t.put(layer, "u1", wall, finishH - 4);
  for (let i = 1; i <= 3; i++) t.put(layer, "t1", wall, finishH - 4 - 9 * i);
}
const floor = Math.max(...[...t.cells.values()].map(cell => Math.max(cell.left, cell.right))) + 9;
// The countryside, rocky hills behind the quarry
t.countryside(-3, wall, { floor, hills: ["bk1", "bk1f", null, "bk2", "bk1", null, "bk4", "bk1f", null] });

// ---- Scenery (props beside the track, no physics) ----

// Cones before the stairs and before the lip
for (const x of [stairs - 1, lip])
  for (const z of [-7.5, 7.5]) t.prop("cone", (x - 0.5) * Cell - 2.5, top(t.surface(x - 1)), z);
// Utility poles along the haul roads, guardrails along the front edge of the rim above the stairs
for (const x of [haul + 1, haul + 5, tunnel + 4, floor2 + 3, landing + 3]) {
  const surface = t.surface(x);
  if (surface !== undefined) t.prop("pole", x * Cell, top(surface), -11, 1);
}
for (let x = boulder; x < boulder + 4; x++) t.prop("guardrail", x * Cell, top(t.surface(x)), 10.5, 2);

const output = process.argv[2] || path.join(__dirname, "..", "..", "content", "Levels", "quarry.json");
t.save(output, 12);
console.log(`${output}: ${t.middle.length} road blocks, ${t.objects.length} objects, ${t.scenery.length} props; haul ${haul}, descent ${descent}, floor ${floor1}, tunnel ${tunnel}, floor ${floor2}, stairs ${stairs}, boulder ${boulder}, rim ${rim}, lip ${lip} (h ${lipH}), finish ${finish}`);
