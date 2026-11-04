// Generates content/Levels/docks.json: the docks at night, a level that uses every kind of block, object and
// prop there is. A fast run needs more than the throttle: a sliding ramp (a trailer) to brake on, so that it slides
// into its lock and the car climbs onto the road above by turning the axles, barrels to push into the pits of the
// road, a ramp at the edge of a sunken yard that throws the car over the roofs of the containers standing in it (a
// slow car tips the ramp and drops onto them), a hump that flips a car that does not brake, stairs to walk up by
// turning the axles, and a long run down to the quay and the jump across the harbour to the pier, which only a car
// at full speed makes. The back layers, up to three cells behind the road, make the docks wide.
//
//   node tools/levelgen/docks.js [output.json]

"use strict";

const path = require("path");
const { Track, Cell } = require("./lib.js");

const top = Track.top;
const t = new Track(-3, 0);
// The docks at night: the headlights on, the street lamps along the quay
t.mood = "night";
// Par: a scripted run that brakes for the hump, climbs the trailer and the stairs by turning the axles and makes the
// jump takes 40 s; the owner's runs will set it right
t.par = 40;
// The jump across the harbour: cells of nothing between the lip and the pier, and how many steps the pier is
// below the end of the lip
const Gap = 5, Drop = 4;
// A barrel is this big (the radius of its circle in the catalog), and stands on the road at this depth below its
// surface
const Barrel = 3, BarrelY = -3.1;

// ---- Road (middle layer, takes part in the physics) ----

// Start: wall behind the car, checkered start area, start gate with the START sign
t.put("middle", "u1f", -4, -4).put("middle", "t1f", -4, -13).put("middle", "t1f", -4, -22);
t.road("a1", "a1", "xa1", "xa1", "xa1", "sb");
t.put("middle", "sg", 2, -4).put("middle", "st2", 2, -13);

// The sliding ramp and its lock, right after the start, with the distances of the lock level (the lock block in the
// tenth cell from the start): a loose trailer with a ramp at its back end, its front wall as high as the road beyond
// the lock block, 7.5 units up. The car gathers speed on the straight, runs up the ramp onto the bed of the trailer
// and brakes: the trailer slides on with the car on it until the jaws of the lock catch its front, and the car climbs
// onto the road above by turning the axles with the throttle held. A car that does not brake runs over the trailer and
// gets stuck in front of the lock; with a longer run-up it would fly over the lock, so the distance matters. (A lesson
// of the tutorial on braking could use it.) It comes before the barrels, which the car would push onto the trailer.
const trailer = t.x;
t.road("a1", "a1", "a1", "a1", "a1", "a1", "a1");
const lock = t.x;
t.obj("Mov", "mov1", (lock - 3.16) * Cell, top(t.h) - 3.74);
t.step(-3);
t.road("lo", "a1");

// A tunnel after the lock
const tunnel = t.x, tunnelH = t.h;
t.road("tu1s", "tu2s", "tu2s", "tu3s");
["tu1v", "tu2v", "tu2v", "tu3v"].forEach((vault, i) => t.put("middle", vault, tunnel + i, tunnelH - 4));

// The wide pit (as in the tunel level): two barrels fit in it. They stand before it as in that level, the first on
// the rim of the pit (6.6 units from the middle of the block), so that a touch tips it in, the second against the
// first: the car pushes them into the pit and drives over them, or flies over the pit at speed.
t.road("a1");
const trap = t.x;
t.road("ba2");
t.obj("Bar", "bar1", trap * Cell - 6.5, top(t.h) + BarrelY);
t.obj("Bar", "bar1", trap * Cell - 6.5 - 2 * Barrel, top(t.h) + BarrelY);

// The container yard (as in the lock level, the same distances): the road ends in a ledge over a yard sunk 12.5
// units below it, where three containers stand with stacks of two crates between them, as high as the containers,
// so that their roofs go on at the height of the road (1.6 above it), with gaps of some 10 units between them,
// which the car bridges. The ramp lies on the road at the ledge and overhangs it, its high end 3 units before the
// first container and 3 above its roof: a car at speed flies from it over the roofs, a slow one tips it and drops
// onto the roof. The floor of the yard goes on under the road at both ends.
t.road("a1", "a1");
const ledge = t.x - 1, yardH = t.h, edge = (ledge + 0.5) * Cell;
for (let x = ledge; x <= ledge + 6; x++) t.put("middle", "a1", x, yardH + 5);
t.x = ledge + 6;
t.road("a1", "a1", "a1");
const roof = top(yardH);
t.obj("Ramp", "rm", edge - 0.28 * Cell, roof - 5.74);
t.obj("Co", "co2", edge + 15.75, roof + 5.26);
t.obj("Box", "box", edge + 34.75, roof + 8.26).obj("Box", "box", edge + 34.75, roof + 1.26);
t.obj("Co", "co", edge + 54.75, roof + 5.26);
t.obj("Box", "box", edge + 75.75, roof + 8.26).obj("Box", "box", edge + 76.75, roof + 1.26);
t.obj("Co", "co2", edge + 96.75, roof + 5.26);

// The narrow pit: one barrel fits in it, and stands on its rim (3.6 units from the middle of the block)
const pit = t.x;
t.road("ba1", "a1", "a1");
t.obj("Bar", "bar2", pit * Cell - 4, top(yardH) + BarrelY);

// A hump 10 units high: a car that does not brake for it flies off it onto its roof. Two flat cells after it are
// where a car that flies a little lands.
const hump = t.x;
t.road("c2f", "c2");
t.road("a1", "a1");

// The climb: a gentle slope to a terrace, then stairs, which the throttle alone does not get the car up:
// turning the axles walks it up
const climb = t.x;
t.road("d1f", "b1f", "b1f");
const terrace = t.x;
t.road("a1");
const stairs = t.x;
t.road("s1f", "s1f", "s1f", "s1f");

// The top: a flat run to gather speed again
const summit = t.x, summitH = t.h;
t.road("a1", "a1", "a1");

// The run down to the quay: a long gentle descent, where the car reaches its full speed
const descent = t.x;
t.road("b1", "b1", "b1", "b1", "d1");
const quay = t.x, quayH = t.h;
t.road("a1", "a1");

// The jump across the harbour: a curved lip, then nothing; the pier is lower than the lip. A car that falls
// short drops into the harbour, below the level, and the run is lost.
const lip = t.x;
t.road("d1f");
const lipH = t.h;
t.x += Gap;
t.h = lipH + Drop;
// The pier: a tapered tip (xa7 has no profile, so it is placed by hand), then checkered floor
const pier = t.x, pierH = t.h;
t.put("middle", "xa7", t.x++, t.h);
t.road("xa1", "xa1");

// Finish: the checkered floor goes on, with the finish gate, then trestle barriers, a pile of crates and
// containers in front of the wall at the end, which stop the car
const finish = t.x, finishH = t.h, ground = top(t.h);
t.road("xa1", "xa1", "xa1");
t.put("middle", "g", finish + 1, t.h - 4, { finish: true }).put("middle", "st", finish + 1, t.h - 13);
t.road("a1", "a1", "a1", "a1");
const wall = t.x;
t.road("u1");
for (let i = 1; i <= 4; i++) t.put("middle", "t1", wall, t.h - 4 - 9 * i);
t.obj("Tb", "tb1", (finish + 3.2) * Cell, ground - 2.74);
t.obj("Tb", "tb1", (finish + 3.5) * Cell, ground - 2.74);
const crate = 6.5, pile = (finish + 4) * Cell;
t.obj("Box", "box", pile - crate / 2, ground - 3.36).obj("Box", "box", pile + crate / 2, ground - 3.36);
t.obj("Box", "box", pile, ground - 3.36 - crate - 0.1);
const wallX = (wall - 0.5) * Cell;
t.obj("Co", "co2", wallX - 26, ground - 7.0);
t.obj("Co", "co", wallX - 28, ground - 21.0);

// ---- Decorations (back and front layers, no physics) ----

// Kerbs along the flat parts of the road, running across the pits (the kerb is at the edges of the road, the pit
// in its middle, as in the lock level). No end pieces in the tunnel, over the yard, at the lock block and at the
// tip of the pier.
const runs = [];
for (const run of t.flatRuns()) {
  const last = runs[runs.length - 1];
  let pits = last && last.h === run.h;
  for (let x = last ? last.to + 1 : 0; pits && x < run.from; x++)
    pits = ["ba1", "ba2"].includes(t.cells.get(x)?.block);
  if (pits) last.to = run.to;
  else runs.push({ ...run });
}
for (const run of runs) t.kerbs(run.from, run.to, run.h);
t.remove("back", "kea1d", -4);
for (const x of [tunnel, tunnel + 3, ledge + 1, ledge + 5, lock, pier]) {
  for (const piece of ["kea1c", "kea1f"]) t.remove("front", piece, x);
  for (const piece of ["kea1d", "kea1e"]) t.remove("back", piece, x);
}

// Start: checkered area on both sides, barriers beside it; behind it, the checkered area goes on a cell further and
// narrows on, its slanted edges in a line with those in front of it
t.checkered("back", -1, 1, 0).checkered("front", -1, 1, 0);
t.barrier(-3, -2, 0);
t.checkered("back2", 0, 0, 0);

// The yard: the road before and after it and its floor go on at both sides and two cells behind, so that it is a
// wide yard and not a hole in the road
for (const layer of ["back2", "back", "front"]) {
  for (let x = trap + 1; x <= ledge; x++) t.put(layer, "a1", x, yardH);
  for (let x = ledge; x <= ledge + 6; x++) t.put(layer, "a1", x, yardH + 5);
  for (let x = ledge + 6; x <= ledge + 8; x++) t.put(layer, "a1", x, yardH);
}
t.railing(ledge + 6, ledge + 8, yardH);

// The pit and the hump: a wider road, two cells behind too
t.bothSides("copyRoad", pit, hump + 3);
t.copyRoad("back2", pit, hump + 3);

// The trailer run: a railing on the back edge
t.railing(trailer, lock - 1, t.surface(trailer));

// Climb and stairs: a wide slope with a barrier on the terrace, wide stairs, two cells behind too
t.bothSides("copyRoad", climb, stairs + 3);
t.copyRoad("back2", climb, stairs + 3);
t.barrier(terrace, terrace, t.surface(terrace));

// The top and the descent: a railing along the top, a wide slope, two cells behind too
t.railing(summit, summit + 2, summitH);
t.copyRoad("back2", summit, summit + 2);
t.bothSides("copyRoad", descent, descent + 4);
t.copyRoad("back2", descent, descent + 4);

// The quay and the lip: wide, with a striped barrier on the back edge of the quay
t.bothSides("copyRoad", quay, lip);
t.copyRoad("back2", quay, lip);
t.barrier(quay, quay + 1, quayH, true);

// The pier and the finish: checkered areas on both sides, a floor two and three cells behind for the judges' stand,
// the end wall continues on all sides
t.checkered("back", pier + 2, finish + 2, finishH).checkered("front", pier + 2, finish + 2, finishH);
for (let x = pier + 1; x < wall; x++) t.put("back2", "a1", x, finishH);
for (let x = finish - 1; x <= finish + 4; x++) t.put("back3", "a1", x, finishH);
for (const layer of ["back3", "back2", "back", "front"]) {
  t.put(layer, "u1", wall, finishH - 4);
  for (let i = 1; i <= 3; i++) t.put(layer, "t1", wall, finishH - 4 - 9 * i);
}

// ---- Scenery (props beside the track, no physics) ----

// Street lamps on the back edge of the road, in the middle of flat cells
const lampX = [-2, trap - 1, ledge - 1, ledge + 7, pit + 2, hump + 3, trailer + 1, lock + 1, terrace, summit + 1, quay + 1, finish - 1, finish + 2, finish + 5];
for (const x of lampX) t.prop("lamp", x * Cell, top(t.surface(x)), -10, 1);
// Utility poles on the back edge of the road, in the middle of flat cells, their cross-arms across the road
for (const x of [3, ledge + 8, hump + 2, lock, summit, finish + 4]) t.prop("pole", x * Cell, top(t.surface(x)), -11, 1);
// A fence behind the start area, on the checkered area of the back layer
for (let x = -2; x <= 1; x++)
  for (const half of [0.25, 0.75]) t.prop("fence", (x + half) * Cell, top(0), -14);
// Traffic cones at the ends of the flat cells before the ramp, the hump and the lip warn of them
for (const x of [ledge, hump, lip])
  for (const z of [-7.5, 7.5]) t.prop("cone", (x - 0.5) * Cell - 2.5, top(t.surface(x - 1)), z);
// Guardrails in front of the road along the flat top, and the thick one along the quay, before the harbour, turned
// to face the road. A guardrail is a cell long, so they stand on the flat cells only, where they join in a line.
for (let x = summit; x <= summit + 2; x++) t.prop("guardrail", x * Cell, top(summitH), 10.5, 2);
for (let x = quay; x <= quay + 1; x++) t.prop("guardrail2", x * Cell, top(quayH), 10.5, 2);
// Posts with a chain along the finish, and the judges' stand two cells behind the road, clear of the finish gate
// (the gate reaches 19 units behind the road, the stand 16 units to either side of its middle)
for (let x = finish; x <= finish + 2; x++)
  for (const half of [0.25, 0.75]) t.prop("chain", (x + half) * Cell, top(finishH), 10.5);
t.prop("stand", (finish + 1.5) * Cell, top(finishH), -45);

const output = process.argv[2] || path.join(__dirname, "..", "..", "content", "Levels", "docks.json");
t.save(output, 8);
console.log(`${output}: ${t.middle.length} road blocks, ${t.back.length + t.back2.length + t.back3.length + t.front.length} decorations, ${t.objects.length} objects, ${t.scenery.length} props; tunnel at column ${tunnel}, trap at ${trap}, ledge at ${ledge}, pit at ${pit}, hump at ${hump}, trailer at ${trailer}, lock at ${lock}, stairs at ${stairs}, descent at ${descent}, lip at ${lip}, pier at ${pier}, finish at ${finish}`);
