// Building blocks for level generators: a track builder that keeps the road continuous,
// and helpers for decorations in the back and front layers.
//
// Layers: "middle" (the road, with physics), "back", "back2" and "back3" (one, two and three cells
// behind the road) and "front" (one cell in front of it; the camera is too near for more).
//
// Units: heights are in shift steps (2.5 physics units), y grows downwards, one grid cell is
// 22.5 physics units = 9 shift steps. A block's "base" is 9 * y + shift, i.e. its vertical
// position in shift steps.

"use strict";

const fs = require("fs");
const path = require("path");

const Cell = 22.5;
const Step = 2.5;

// Height of the road surface at the left and right edge of each road block, relative to its base: the "profile"
// of the blocks in content/catalog.json (measured on the 2016 collision bitmaps, now docs/catalog/bl/cols; a flat a1
// has its surface at the base; s1/s1f, the stairs, start or end with a step at the block edge). The level editor reads
// the same profiles, so that it joins the blocks of a road as road() does.
const Profiles = loadProfiles();

function loadProfiles() {
  const catalog = JSON.parse(fs.readFileSync(path.join(__dirname, "..", "..", "content", "catalog.json"), "utf8"));
  const profiles = {};
  for (const block of catalog.blocks)
    if (block.profile)
      profiles[block.name] = block.profile;
  return profiles;
}

const FlatBlocks = new Set(["a1", "xa1", "sb"]);

// The layers a block can be put in, from the back to the front
const Layers = ["back3", "back2", "back", "middle", "front"];

class Track {
  /** Starts the road at grid column x with the surface at height h. */
  constructor(x, h) {
    this.x = x;
    this.h = h;
    this.middle = [];
    this.back = [];
    this.back2 = [];
    this.back3 = [];
    this.front = [];
    this.objects = [];
    // Props beside the track, without physics (written only when there are some)
    this.scenery = [];
    // The light of the level: "day" (default), "sunset" or "night"
    this.mood = "day";
    // The par time in seconds (the time a good run takes); null = estimated by the game
    this.par = null;
    // Road blocks by column: { block, base, left, right } (left/right = surface height at the edges)
    this.cells = new Map();
  }

  static place(block, x, base) {
    const y = Math.round(base / 9);
    return { block, x, y, shift: base - 9 * y };
  }

  /** Surface y in physics units of a flat block at height h. */
  static top(h) {
    return h * Step - 1.26;
  }

  /** Adds road blocks one after another, each starting at the height where the previous one ended. */
  road(...blocks) {
    for (const block of blocks) {
      const profile = Profiles[block];
      if (!profile)
        throw new Error("No surface profile for block " + block);
      const base = this.h - profile[0];
      this.middle.push(Track.place(block, this.x, base));
      this.cells.set(this.x, { block, base, left: base + profile[0], right: base + profile[1] });
      this.h = base + profile[1];
      this.x++;
    }
    return this;
  }

  /** Vertical step at the current position (negative = up). */
  step(dh) {
    this.h += dh;
    return this;
  }

  /**
   * A block placed directly by its base in a layer ("middle", "back", "back2", "back3" or "front"), outside the road
   * (walls, gates, decorations); extra fields (e.g. finish) are added to it.
   */
  put(layer, block, x, base, extra = {}) {
    Track.layer(layer);
    this[layer].push({ ...Track.place(block, x, base), ...extra });
    return this;
  }

  /** Removes the blocks of the given type in column x of a layer. */
  remove(layer, block, x) {
    Track.layer(layer);
    this[layer] = this[layer].filter(b => b.block !== block || b.x !== x);
    return this;
  }

  /** Checks the name of a layer. */
  static layer(name) {
    if (!Layers.includes(name))
      throw new Error(`No layer "${name}": the layers are ${Layers.join(", ")}`);
  }

  obj(type, model, x, y) {
    this.objects.push({ type, model, x: Math.round(x * 100) / 100, y: Math.round(y * 100) / 100 });
    return this;
  }

  /**
   * A prop of the scenery (a model of the catalog's "scenery" list): its foot at x, y in physics units, at the depth z
   * (negative behind the road, positive in front of it; the road reaches 11.25 to either side), turned by quarter turns.
   */
  prop(model, x, y, z, turn = 0) {
    const round = v => Math.round(v * 100) / 100;
    const prop = { model, x: round(x), y: round(y), z: round(z) };
    if (turn % 4 !== 0) prop.turn = ((turn % 4) + 4) % 4;
    this.scenery.push(prop);
    return this;
  }

  /** Road surface height at a fraction f of column x (0 = left edge, 1 = right edge), linear between the edges. */
  surface(x, f = 0.5) {
    const cell = this.cells.get(x);
    return cell && cell.left + (cell.right - cell.left) * f;
  }

  /** Runs of consecutive flat road cells with the same height: [{ from, to, h }]. */
  flatRuns() {
    const runs = [];
    const columns = [...this.cells.keys()].sort((a, b) => a - b);
    for (const x of columns) {
      const cell = this.cells.get(x);
      if (!FlatBlocks.has(cell.block))
        continue;
      const last = runs[runs.length - 1];
      if (last && last.to === x - 1 && last.h === cell.left)
        last.to = x;
      else
        runs.push({ from: x, to: x, h: cell.left });
    }
    return runs;
  }

  /**
   * Kerbs along a flat run: a checkered strip at the front edge of the road, a dark rail at the back edge.
   * The end pieces stick into the neighbouring columns.
   */
  kerbs(from, to, h, { front = true, back = true } = {}) {
    if (front) {
      this.put("front", "kea1c", from - 1, h).put("front", "kea1f", to + 1, h);
      for (let x = from; x <= to; x++) this.put("front", "kea1a", x, h);
    }
    if (back) {
      this.put("back", "kea1d", from - 1, h).put("back", "kea1e", to + 1, h);
      for (let x = from; x <= to; x++) this.put("back", "kea1b", x, h);
    }
    return this;
  }

  /** A concrete barrier standing on the back edge of a flat run, with sloped ends. */
  barrier(from, to, h, striped = false) {
    for (let x = from; x <= to; x++) {
      const block = x === from ? (striped ? "bl2sf" : "bl2") : x === to ? (striped ? "bl2s" : "bl2f") : (striped ? "bl1s" : "bl1");
      this.put("back", block, x, h);
    }
    return this;
  }

  /** A railing on the back edge of a flat run. */
  railing(from, to, h) {
    for (let x = from; x <= to; x++) this.put("back", "za", x, h);
    return this;
  }

  /**
   * A checkered area next to the road (a back layer or "front") over columns from..to,
   * with triangles at both ends that taper away from the road.
   */
  checkered(layer, from, to, h) {
    const [left, right] = layer.startsWith("back") ? ["xa4", "xa2"] : ["xa3", "xa5"];
    this.put(layer, left, from - 1, h).put(layer, right, to + 1, h);
    for (let x = from; x <= to; x++) this.put(layer, "xa1", x, h);
    return this;
  }

  /** Copies of the road blocks in columns from..to into a layer, moved by dh (negative = up). */
  copyRoad(layer, from, to, dh = 0) {
    for (let x = from; x <= to; x++) {
      const cell = this.cells.get(x);
      if (cell) this.put(layer, cell.block, x, cell.base + dh);
    }
    return this;
  }

  /**
   * Stairs beside a climb of b2f blocks (they rise the same 4 steps per column),
   * like a footpath next to the road.
   */
  stairs(layer, from, to) {
    for (let x = from; x <= to; x++) {
      const cell = this.cells.get(x);
      if (cell.block === "b2f") this.put(layer, "s1f", x, cell.left);
      else if (cell.block === "b2") this.put(layer, "s1", x, cell.right);
    }
    return this;
  }

  /** Copies of the road (or stairs beside it) on both sides: in the back and in the front layer. */
  bothSides(method, ...args) {
    return this[method]("back", ...args)[method]("front", ...args);
  }

  /**
   * The level as JSON in the format of LevelFile.ToJson (one block or object per line). The back layers are one list,
   * from the nearest to the deepest, the deeper ones with their depth, as the level editor writes them.
   */
  json(order) {
    const back = this.back.concat(this.back2.map(b => ({ ...b, depth: 2 })), this.back3.map(b => ({ ...b, depth: 3 })));
    const list = (name, items, last) => `  "${name}": [`
      + (items.length ? "\n" + items.map(i => "    " + JSON.stringify(i)).join(",\n") + "\n  ]" : "]")
      + (last ? "\n" : ",\n");
    return "{\n  \"format\": 1,\n" + (order != null ? `  "order": ${order},\n` : "")
      + (this.mood !== "day" ? `  "mood": "${this.mood}",\n` : "")
      + (this.par != null ? `  "par": ${this.par},\n` : "")
      + list("middle", this.middle) + list("back", back) + list("front", this.front)
      + list("objects", this.objects, this.scenery.length === 0)
      + (this.scenery.length ? list("scenery", this.scenery, true) : "") + "}\n";
  }

  save(path, order) {
    fs.writeFileSync(path, this.json(order));
  }
}

module.exports = { Track, Profiles, Cell, Step };
