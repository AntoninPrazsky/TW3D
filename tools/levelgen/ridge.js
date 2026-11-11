// Generates content/Levels/ridge.json: a mountain ridge on a clear day, an earth track up its side and a concrete road
// along its top by turns. From the start a meadow track at the foot of the ridge, two climbs on earth with a terrace of
// washboards between them, the concrete road of the ridge, a viaduct over a saddle on tall pillars, steps down off the
// ridge, an earth track with a dip, and a jump onto the concrete of the finish.
//
//   node tools/levelgen/ridge.js [output.json]

"use strict";

const path = require("path");
const { Track } = require("./lib.js");

const Cell = 22.5;
const top = Track.top;

const t = new Track(-3, 0);
t.style = "offroad";
t.mood = "day";
// Par: a fifth over the scripted full-throttle runs (25 to 28 s), until the owner's own runs set it
t.par = 34;

// ---- Road (middle layer, takes part in the physics) ----

// Start: wall behind the car, checkered start area, start gate with the START sign (classic: they have no off-road look)
t.put("middle", "u1f", -4, -4).put("middle", "t1f", -4, -13).put("middle", "t1f", -4, -22);
t.road("a1", "a1", "xa1", "xa1", "xa1", "sb");
t.put("middle", "sg", 2, -4).put("middle", "st2", 2, -13);

// 1. The meadow at the foot of the ridge: a washboard, then the first climb, 12.5° (the cars get up it from a stop)
const foot = t.x;
t.road("a1", "a1", "a1", "w1", "a1", "a1");
const climb1 = t.x;
t.road("d1f", "b1f", "b1f", "b1f", "b1f");

// 2. A terrace on the side of the ridge: a high washboard just after the top of the climb, where the car is slow, and a
// low one (a log here, met at 43 units a second, threw the Sand Rail onto its roof), then the second climb
const terrace = t.x;
t.road("a1", "w2", "a1", "a1", "a1", "w1", "a1", "a1");
const climb2 = t.x;
t.road("d1f", "b1f", "b1f", "b1f", "b1f");

// 3. The top of the ridge: concrete (a barrel on the rim of a pit here, met at 35 units a second, threw a car onto its
// roof)
const ridge = t.x;
t.withStyle("classic", () => t.road("a1", "a1", "a1", "a1", "a1", "a1", "a1", "a1", "a1"));

// 4. The viaduct over a saddle of the ridge, its deck flush with the road, on pillars down to the saddle far below
const viaduct = t.x, viaductH = t.h;
const ViaductCells = 7;
for (let i = 0; i < ViaductCells; i++) t.put("middle", "br1", viaduct + i, viaductH);
t.gap(ViaductCells);

// 5. On along the ridge, then steps down off it, three cells apart (down a step the car only drops; up one at speed
// it turned over), and a gentle run down to the earth
const ridge2 = t.x;
t.withStyle("classic", () => t.road(
  "a1", "a1", "a1", "a1", "k2", "a1", "a1", "a1", "k2", "a1", "a1", "a1", "k2", "a1", "a1",
  "e0", "b0", "b0", "d0", "a1"));

// 6. The earth track along the far side, met at full speed: a low washboard (a log or a high one threw the cars onto
// their roofs), a gentle dip, then a flat run, the gentle kicker and the jump onto the concrete of the finish, four steps
// lower, with six flat cells to land on
const track = t.x;
t.road("a1", "a1", "w1", "a1", "a1", "a1", "a1", "e0", "b0", "d0", "a1", "a1", "d0f", "b0f", "e0f", "a1", "a1", "a1", "a1",
  "a1", "a1");
const lip = t.x;
t.road("d0f");
const lipH = t.h;
t.gap(3);
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

// The saddle under the viaduct: meadow five cells under the deck, the pillars standing on it
const saddleH = viaductH + 45;
const floor = Math.max(saddleH, ...[...t.cells.values()].map(cell => Math.max(cell.left, cell.right))) + 9;
for (let x = viaduct; x < viaduct + ViaductCells; x++)
  for (const layer of ["back", "front", "back2", "front2", "front3"]) {
    t.put(layer, "a1g", x, saddleH, { style: "offroad" });
    for (let base = saddleH + 9; base < floor + 5; base += 9) t.put(layer, "g2", x, base, { style: "offroad" });
  }
for (const x of [viaduct + 1, viaduct + 3, viaduct + 5]) {
  t.put("middle", "br2", x, viaductH + 9);
  for (let base = viaductH + 18; base < saddleH; base += 9) t.put("middle", "br3", x, base);
}

// The countryside: ground, meadows, retaining walls under the concrete; rocks, hills and copses behind the ridge (no
// halls up here)
const hills = ["bk1", "bk2", null, "bk1f", "bk4", null, "bk2f", "bk1", null];
t.countryside(-3, wall, { floor, hills });
for (let x = viaduct; x < viaduct + ViaductCells; x++)
  t.backdrop("back3", x, x, saddleH, [hills[x % hills.length]], { ground: "a1g", style: "offroad" });

// ---- Scenery (props beside the track, no physics) ----

// Guardrails along both edges of the viaduct, facing the road
for (let x = viaduct; x < viaduct + ViaductCells; x++) {
  t.prop("guardrail", x * Cell, top(viaductH), 10.5, 2);
  t.prop("guardrail", x * Cell, top(viaductH), -10.5, 0);
}
// Cones before the lip, utility poles along the concrete
for (const x of [lip])
  for (const z of [-7.5, 7.5]) t.prop("cone", (x - 0.5) * Cell - 2.5, top(t.surface(x - 1)), z);
for (const x of [ridge + 1, ridge + 6, ridge2 + 2, ridge2 + 6, ridge2 + 10, ridge2 + 14, landing + 3]) {
  const surface = t.surface(x);
  if (surface !== undefined) t.prop("pole", x * Cell, top(surface), -11, 1);
}
// Fences by the earth tracks
for (const x of [foot + 1, terrace + 1, terrace + 6, track + 4, track + 13]) {
  const surface = t.surface(x);
  if (surface !== undefined) for (const half of [0.25, 0.75]) t.prop("fence", (x + half) * Cell, top(surface), -14);
}

const output = process.argv[2] || path.join(__dirname, "..", "..", "content", "Levels", "ridge.json");
t.save(output, 13);
console.log(`${output}: ${t.middle.length} road blocks, ${t.objects.length} objects, ${t.scenery.length} props; foot ${foot}, climb ${climb1}, terrace ${terrace}, climb ${climb2}, ridge ${ridge} (h ${t.cells.get(ridge).left}), viaduct ${viaduct} (h ${viaductH}), ridge ${ridge2}, track ${track}, lip ${lip} (h ${lipH}), finish ${finish}`);
