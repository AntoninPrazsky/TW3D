// Building blocks for level generators: a track builder that keeps the road continuous,
// and helpers for decorations in the back and front layers.
//
// Units: heights are in shift steps (2.5 physics units), y grows downwards, one grid cell is
// 22.5 physics units = 9 shift steps. A block's "base" is 9 * y + shift, i.e. its vertical
// position in shift steps.

"use strict";

const fs = require("fs");

const Cell = 22.5;
const Step = 2.5;

// Height of the road surface at the left and right edge of each road block, relative to its base.
// Measured on the collision bitmaps (content/Levels/bit); a flat a1 has its surface at the base.
// s1/s1f (stairs) start or end with a step at the block edge.
const Profiles = {
  a1: [0, 0], xa1: [0, 0], lo: [0, 0], ba1: [0, 0], ba2: [0, 0],
  b1: [2, 4], b1f: [4, 2], b2: [0, 4], b2f: [4, 0], b3: [-2, 4], b3f: [4, -2], b4: [-4, 4], b4f: [4, -4],
  c1: [2, 4], c1f: [4, 2], c2: [0, 4], c2f: [4, 0], c3: [-2, 4], c3f: [4, -2],
  d1: [2, 4], d1f: [4, 2], d2: [0, 4], d2f: [4, 0], d3: [-2, 4], d3f: [4, -2],
  sb: [4, 4], tu1s: [4, 4], tu2s: [4, 4], tu3s: [4, 4], u1: [4, 4], u2: [4, 4],
  s1: [-4, 0], s1f: [0, -4]
};

const FlatBlocks = new Set(["a1", "xa1", "sb"]);

class Track {
  /** Starts the road at grid column x with the surface at height h. */
  constructor(x, h) {
    this.x = x;
    this.h = h;
    this.middle = [];
    this.back = [];
    this.front = [];
    this.objects = [];
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

  /** A block placed directly by its base, outside the road (walls, gates, decorations). */
  put(layer, block, x, base) {
    this[layer].push(Track.place(block, x, base));
    return this;
  }

  obj(type, model, x, y) {
    this.objects.push({ type, model, x: Math.round(x * 100) / 100, y: Math.round(y * 100) / 100 });
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
   * A checkered area next to the road (layer "back" or "front") over columns from..to,
   * with triangles at both ends that taper away from the road.
   */
  checkered(layer, from, to, h) {
    const [left, right] = layer === "back" ? ["xa4", "xa2"] : ["xa3", "xa5"];
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
   * Stairs in the back layer beside a climb of b2f blocks (they rise the same 4 steps per column),
   * like a footpath next to the road.
   */
  stairs(from, to) {
    for (let x = from; x <= to; x++) {
      const cell = this.cells.get(x);
      if (cell.block === "b2f") this.put("back", "s1f", x, cell.left);
      else if (cell.block === "b2") this.put("back", "s1", x, cell.right);
    }
    return this;
  }

  /** A pillar under the road in the front layer: thin wall plates reaching `cells` grid cells down. */
  pillar(x, cells = 3) {
    // The plate of t1 is right of the column centre; its top goes just under the road surface
    const surface = this.surface(x, 0.63) * Step - 1.26;
    const base = Math.round((surface + 13.2) / Step);
    for (let i = 0; i < cells; i++) this.put("front", "t1", x, base + 9 * i);
    return this;
  }

  /** The level as JSON in the format of LevelFile.ToJson (one block or object per line). */
  json(order) {
    const list = (name, items, last) => `  "${name}": [`
      + (items.length ? "\n" + items.map(i => "    " + JSON.stringify(i)).join(",\n") + "\n  ]" : "]")
      + (last ? "\n" : ",\n");
    return "{\n  \"format\": 1,\n" + (order != null ? `  "order": ${order},\n` : "")
      + list("middle", this.middle) + list("back", this.back) + list("front", this.front) + list("objects", this.objects, true) + "}\n";
  }

  save(path, order) {
    fs.writeFileSync(path, this.json(order));
  }
}

module.exports = { Track, Profiles, Cell, Step };
