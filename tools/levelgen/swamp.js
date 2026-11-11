// Generates content/Levels/swamp.json: a night in a swamp, where the dirt track runs between lit concrete islands, the
// off-road and the classic style by turns. From the start the mud with a low log, washboards and a dip, the first island:
// stairs up (the axles walk the car up them) and a sliding ramp to brake on, which slides into its lock, then the mud
// again, a jump onto the second island and steps down it, the mud with a small kicker, and concrete to the finish.
//
//   node tools/levelgen/swamp.js [output.json]

"use strict";

const path = require("path");
const { Track } = require("./lib.js");

const Cell = 22.5;
const top = Track.top;

const t = new Track(-3, 0);
t.style = "offroad";
// At night: the headlights on, the street lamps of the islands lit
t.mood = "night";
// Par: about the scripted runs that brake before the stairs and on the trailer (41 to 65 s), until the owner's own runs
// set it
t.par = 55;

// ---- Road (middle layer, takes part in the physics) ----

// Start: wall behind the car, checkered start area, start gate with the START sign (classic: they have no off-road look)
t.put("middle", "u1f", -4, -4).put("middle", "t1f", -4, -13).put("middle", "t1f", -4, -22);
t.road("a1", "a1", "xa1", "xa1", "xa1", "sb");
t.put("middle", "sg", 2, -4).put("middle", "st2", 2, -13);

// 1. The swamp: a low log close to the start, where the car is still slow (at 43 units a second one threw the Sand Rail
// onto its roof), a washboard, a gentle dip with a low washboard at its bottom (a high one there, met at 50 units a
// second, threw the Crawler into the air nose down). (A steeper climb out of it, to slow the car before the stairs,
// threw it through the air onto them instead.)
const swamp = t.x;
t.road("a1", "l1", "a1", "a1", "a1", "w1", "a1", "a1", "a1", "a1", "a1");
const dip = t.x;
t.road("e0", "b0", "b0", "d0", "a1", "w1", "a1", "d0f", "b0f", "b0f", "e0f", "a1", "a1");

// 2. The first island: cones, then stairs up (met at full speed, some 50 units a second, they now and then threw a car
// onto its roof: brake; the throttle alone does not get the car up them, turning the axles does), then from
// the top, where the car is nearly standing, the run of the brake lesson of training3: the sliding ramp three cells
// before the lock block, the lock 7.5 units up, and as long a run-up to the ramp as from the start of training3 (the
// trailer has to slide some 36 units: with a run-up of four cells from the stairs it slid too little; with eight to ten
// most runs got through, braked or not, ten the most)
const island1 = t.x;
t.withStyle("classic", () => t.road("a1", "a1", "a1", "s1f", "s1f", "a1"));
const trailer = t.x;
t.withStyle("classic", () => t.road("a1", "a1", "a1", "a1", "a1", "a1", "a1", "a1", "a1", "a1"));
const lock = t.x;
t.obj("Mov", "mov1", (lock - 3) * Cell, top(t.h) - 3.74);
t.step(-3);
t.withStyle("classic", () => t.road("lo", "a1", "a1"));

// 3. The mud after the island: a low log and a high washboard, met while the car is still slow from climbing off the
// trailer, a flat run and a gentle kicker: the jump onto the second island, four steps lower, with six flat cells to
// land on (off a curved kicker the cars turned nose down in the air, see valley.js)
const mud2 = t.x;
t.road("l1", "a1", "a1", "a1", "w2", "a1", "a1", "a1", "a1", "a1", "a1");
const lip = t.x;
t.road("d0f");
const lipH = t.h;
t.gap(3);
t.h = lipH + 4;

// 4. The second island: the landing, then steps down
const island2 = t.x;
t.withStyle("classic", () => t.road("r2f", "a1", "a1", "a1", "a1", "a1", "a1", "k1", "a1", "a1", "k2", "a1", "a1", "a1"));

// 5. The swamp again, met at full speed: a low washboard (a high one threw the cars into the air, nose down, and a log
// onto their roofs), a small kicker with six flat cells after it
const swamp2 = t.x;
t.road("a1", "a1", "a1", "w1", "a1", "a1", "a1", "a1", "a1", "a1", "a1", "d0f");
t.step(2);
t.road("a1", "a1", "a1", "a1", "a1", "a1");

// 6. Concrete to the finish
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

t.checkered("back", -1, 1, 0).checkered("front", -1, 1, 0);
t.checkered("back", finish, finish + 2, finishH).checkered("front", finish, finish + 2, finishH);
for (const layer of ["back", "front"]) {
  t.put(layer, "u1", wall, finishH - 4);
  for (let i = 1; i <= 3; i++) t.put(layer, "t1", wall, finishH - 4 - 9 * i);
}
const floor = Math.max(...[...t.cells.values()].map(cell => Math.max(cell.left, cell.right))) + 9;
t.countryside(-3, wall, { floor, hills: ["bk4", "bk4f", null, "bk2", "bk4", null, "bk1f", "bk4", null] });

// ---- Scenery (props beside the track, no physics) ----

// Street lamps on the back edge of the islands and at the start and the finish, in the middle of flat cells: the light
// of the night
for (const x of [-1, island1 + 1, trailer + 1, trailer + 5, trailer + 9, lock + 2, island2 + 2, island2 + 6, island2 + 12, finishRoad + 1, finish + 1])
  t.prop("lamp", x * Cell, top(t.surface(x)), -10);
// Guardrails along the back edge of the run to the lock (props: a railing block there would lie in the ground of the
// countryside), and cones before the stairs and the lip
for (let x = trailer; x < lock; x++) t.prop("guardrail", x * Cell, top(t.surface(x)), -10.5, 0);
for (const x of [island1 + 2, lip])
  for (const z of [-7.5, 7.5]) t.prop("cone", (x - 0.5) * Cell - 2.5, top(t.surface(x - 1)), z);
// Fences by the swamp
for (const x of [swamp + 1, swamp + 9, dip + 5, mud2 + 1, swamp2 + 2, swamp2 + 9]) {
  const surface = t.surface(x);
  if (surface !== undefined) for (const half of [0.25, 0.75]) t.prop("fence", (x + half) * Cell, top(surface), -14);
}

const output = process.argv[2] || path.join(__dirname, "..", "..", "content", "Levels", "swamp.json");
t.save(output, 11);
console.log(`${output}: ${t.middle.length} road blocks, ${t.objects.length} objects, ${t.scenery.length} props; swamp ${swamp}, dip ${dip}, island ${island1}, trailer ${trailer}, lock ${lock}, mud ${mud2}, lip ${lip} (h ${lipH}), island ${island2}, swamp ${swamp2}, finish ${finish}`);
