"""Blender (background): builds track blocks in the classic look of 2016 (the blocks of content/Levels/mod made in Wings 3D:
a grey slab of the road with yellow hazard stripes along both edges), from the outline of the block seen from the side.

The outline is swept straight across the road, from z = -4.5 to 4.5. Its edges that face up are the road: the middle of
it (|z| < 3.6) is plain grey, the edges (3.6 to 4.5) carry the stripes of the classic blocks, parallelograms 0.9 long
slanted by 45° across the edge, yellow and grey by turns along x (a1 has them so, and the slopes as seen from above), so
that the stripes of a new block run on from those of a flat road beside it. A curved road is cut finer along x (`div`
cuts to a stripe) and its edges as finely across, so that the slanted borders of the stripes still run straight from
corner to corner of the little cells. The other edges (the risers of the steps, the bottom, the ends) are plain grey, as
are the faces at the sides. The materials are those of the classic blocks (road grey Kd 0.22744, hazard yellow), written
into the .mtl as the Wings 3D exports have them.

The blocks:
- #61, the steps for the axles: k1, k2, k3, a step down of 1, 2 and 3 shift steps (2.5, 5 and 7.5 physics units) in the
  middle of the cell, and s2, stairs of two steps of 2 shift steps (5 units), the drop of s1. Below a step the block is
  solid down to a sloping bottom, as s1 is.
- #62, smoother roads: e1, e2, e3, rounded crests, the flat road rounding over into a slope as steep as b1, b2 and b3 at
  its right edge (a parabola: it drops 1, 2 and 3 shift steps); turned, a climb rounding over into a flat top. The
  bends e12, e23, e34: the slope of b1, b2, b3 rounding over into the next steeper one, so that a crest longer than a
  cell is e1, e12, e23... with the curve of e1 all the way (the gentlest the grid of whole shift steps allows). b0, a
  straight slope of one shift step a cell (6.3°), gentler than b1, with its S-curve c0, its sag curve d0 and its crest
  e0 (a cubic: the grid makes them drop a whole shift step, so the middle of d0 and e0 is a little steeper than b0).
  A slab one shift step thick, as the slopes are.
Every block starts and ends with the slab of a road one shift step thick, so that it meets a1, the slopes and s1 flush.

    blender -b --factory-startup --python tools/blender/classic.py -- [<output folder>] [<block>...]

Without a folder the blocks go to content/Levels/mod. The coordinates are those of the game (model units: a cell is 9,
a shift step 1; +x is the left edge of a block placed unturned in a level, y up, +z towards the camera). Blender has z
up and its OBJ export writes (x, y, z) as (x, z, -y): the game's (x, y, z) is built at (x, -z, y).
"""
import bpy, bmesh, math, os, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from props import ROOT, material, export

ROAD = material("road", (0.22744, 0.22744, 0.22744))
HAZARD = material("hazard", (1.0, 0.71333, 0.0))
MATERIALS = [ROAD, HAZARD]
# The .mtl as the classic blocks have it (Blender would write its own highlights)
MTL = """newmtl road
Kd 0.22744 0.22744 0.22744
d 1.0
Ks 0.06094 0.06094 0.06094
Ke 0.0 0.0 0.0
Ns 0.3
illum 2

newmtl hazard
Kd 1.0 0.7133333333333334 0.0
d 1.0
Ks 0.59374 0.59374 0.59374
Ke 0.0 0.0 0.0
Ns 0.8533333333333334
illum 2
"""

HALF = 4.5      # half a cell
EDGE = 3.6      # where the stripes along the edge of the road begin (|z|)
STRIPE = 0.9    # the length of a stripe along x


def step(drop):
    """A flat road with a step down of `drop` shift steps in the middle of the cell (its right half lower)."""
    return [(HALF, 0.5), (0.0, 0.5), (0.0, 0.5 - drop), (-HALF, 0.5 - drop), (-HALF, -0.5 - drop), (0.0, -0.5 - drop),
            (HALF, -0.5)]


def stairs(steps, rise):
    """Stairs of `steps` steps of `rise` shift steps across the cell, the last one down at its right edge (as s1: its
    left edge 4 steps above the base), the bottom sloping from one end of the flat slab to the other."""
    top = 0.5 + steps * rise
    tread = 2 * HALF / steps
    outline = [(HALF, top)]
    for i in range(steps):
        x = HALF - (i + 1) * tread
        y = top - i * rise
        outline += [(x, y)] if i == steps - 1 else [(x, y), (x, y - rise)]
    return outline + [(-HALF, -0.5), (HALF, top - 1.0)]


def road(height, div, left=0):
    """A slab of road one shift step thick under the surface height(u) (how far it is down from its left edge, in shift
    steps), u from 0 at the left edge (x = 4.5) to 1 at the right one, cut at every 1/div of a stripe. The left edge is
    `left` shift steps below the base (the first number of the block's profile)."""
    n = round(2 * HALF / STRIPE * div)
    top = [(HALF - 2 * HALF * i / n, 0.5 - left - height(i / n)) for i in range(n + 1)]
    return top + [(x, y - 1.0) for x, y in reversed(top)]


def crest(drop):
    """The flat road rounding over into a slope that drops 2 * drop shift steps a cell at the right edge."""
    return road(lambda u: drop * u * u, 2)


def curve(entry, leaving, drop, left=0):
    """A road from a slope dropping `entry` shift steps a cell at its left edge to one dropping `leaving` at its right
    edge, `drop` shift steps lower: the cubic that meets both slopes (a parabola when the drop is their mean)."""
    a, b = 3 * drop - 2 * entry - leaving, entry + leaving - 2 * drop
    return road(lambda u: entry * u + a * u * u + b * u * u * u, 2, left)


BLOCKS = {
    # name: (what it is, the outline, cuts to a stripe, flat shading, the issue)
    "k1": ("a step down of 2.5 units", step(1), 1, True, 61),
    "k2": ("a step down of 5 units", step(2), 1, True, 61),
    "k3": ("a step down of 7.5 units", step(3), 1, True, 61),
    "s2": ("stairs of two steps of 5 units", stairs(2, 2), 1, True, 61),
    "e1": ("a crest rounding over into the slope of b1", crest(1), 2, False, 62),
    "e2": ("a crest rounding over into the slope of b2", crest(2), 2, False, 62),
    "e3": ("a crest rounding over into the slope of b3", crest(3), 2, False, 62),
    "b0": ("a slope of one shift step a cell", road(lambda u: u, 1), 1, True, 62),
    "c0": ("an S-curve dropping one shift step", curve(0, 0, 1), 2, False, 62),
    "d0": ("the slope of b0 easing into the flat road", curve(1, 0, 1), 2, False, 62),
    "e0": ("the flat road rounding over into the slope of b0", curve(0, 1, 1), 2, False, 62),
    # The bends between the slopes: a crest longer than a cell is e1, then e12, e23... (the curve of e1 all the way)
    "e12": ("the slope of b1 rounding over into that of b2", curve(2, 4, 3, 1), 2, False, 62),
    "e23": ("the slope of b2 rounding over into that of b3", curve(4, 6, 5, -1), 2, False, 62),
    "e34": ("the slope of b3 rounding over into that of b4", curve(6, 8, 7, -3), 2, False, 62),
}

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
# The first argument is the output folder unless it names a block
OUT = args.pop(0) if args and args[0] not in BLOCKS else os.path.join(ROOT, 'content', 'Levels', 'mod')
NAMES = args or list(BLOCKS)


def faces_up(p, q):
    """An edge of the counter-clockwise outline is the road when it faces up (it runs leftwards, -x)."""
    return q[0] < p[0] - 1e-6 and abs(q[1] - p[1]) < (p[0] - q[0]) * 2


def grid(p, q, cell):
    """The points where a road edge from p to q is cut: its ends and the multiples of a cell from -4.5 between them."""
    xs = {round(p[0], 6), round(q[0], 6)}
    i = math.ceil((q[0] + HALF) / cell + 1e-6)
    while -HALF + i * cell < p[0] - 1e-6:
        xs.add(round(-HALF + i * cell, 6))
        i += 1
    xs = sorted(xs, reverse=True)
    for a, b in zip(xs, xs[1:]):
        if abs(a - b - cell) > 1e-6:
            raise ValueError("a cell of the road is not a whole part of a stripe long")
    return [(x, p[1] + (q[1] - p[1]) * (x - p[0]) / (q[0] - p[0])) for x in xs]


def hazard(x, w, front):
    """Whether the point (x, |z| = w) of the edge strip of the road is a yellow stripe (as the stripes of a1 lie)."""
    s = x - (w - EDGE) + HALF if front else x + (w - EDGE) + HALF
    s %= 2 * STRIPE
    return s < STRIPE if front else s >= STRIPE


def build(name):
    title, outline, div, flat, issue = BLOCKS[name]
    n = len(outline)
    cell = STRIPE / div
    widths = [EDGE + (HALF - EDGE) * j / div for j in range(div + 1)]     # the rows across an edge strip (|z|)
    up = [faces_up(outline[i], outline[(i + 1) % n]) for i in range(n)]
    road = set()                         # the points of the outline at an end of the road
    for i in range(n):
        if up[i]:
            road |= {i, (i + 1) % n}

    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    for m in MATERIALS:
        mesh.materials.append(m)
    bm = bmesh.new()
    verts = {}

    def vert(x, y, z):
        key = (round(x, 5), round(y, 5), round(z, 5))
        if key not in verts:
            verts[key] = bm.verts.new((x, -z, y))
        return verts[key]

    def face(points, mat):
        f = bm.faces.new([vert(*p) for p in points])
        f.material_index = MATERIALS.index(mat)

    def striped(tri, front):
        cx, cw = sum(t[0] for t in tri) / 3, abs(sum(t[2] for t in tri) / 3)
        face(tri, HAZARD if hazard(cx, cw, front) else ROAD)

    rows = [-w for w in reversed(widths)] + widths
    sides = {HALF: [], -HALF: []}        # the outline at both sides, with every point the road is cut at
    for i in range(n):
        p, q = outline[i], outline[(i + 1) % n]
        if up[i]:
            cut = grid(p, q, cell)
            for (xa, ya), (xb, yb) in zip(cut, cut[1:]):        # xa > xb: the road runs leftwards
                # The middle of the road
                face([(xa, ya, -EDGE), (xb, yb, -EDGE), (xb, yb, EDGE), (xa, ya, EDGE)], ROAD)
                for w0, w1 in zip(widths, widths[1:]):
                    # The front edge (z > 0): each little cell split along the slant of the stripes, (xb, w0) to (xa, w1)
                    striped(((xb, yb, w0), (xb, yb, w1), (xa, ya, w1)), True)
                    striped(((xb, yb, w0), (xa, ya, w1), (xa, ya, w0)), True)
                for w0, w1 in zip(widths, widths[1:]):
                    # The back edge (z < 0): the slant mirrored, (xa, -w0) to (xb, -w1)
                    striped(((xa, ya, -w0), (xa, ya, -w1), (xb, yb, -w1)), False)
                    striped(((xa, ya, -w0), (xb, yb, -w1), (xb, yb, -w0)), False)
            for side in sides:
                sides[side] += [(x, y) for x, y in cut[:-1]]
        else:
            # A riser, the bottom or an end: one face across the block, through the rows of the road at its ends
            zs_p = rows if i in road else [-HALF, HALF]
            zs_q = rows if (i + 1) % n in road else [-HALF, HALF]
            face([(p[0], p[1], z) for z in zs_p] + [(q[0], q[1], z) for z in reversed(zs_q)], ROAD)
            for side in sides:
                sides[side].append(p)
    for z, points in sides.items():
        face([(x, y, z) for x, y in points], ROAD)

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    header = f"# Made by tools/blender/classic.py: {name}, {title}, in the classic look (#{issue})"
    # Flat shading, as the faces of the classic blocks have; a curved road smooth along the curve, sharp at its edges
    export([obj], [], name, OUT, {name + ".obj": header}, smooth_angle=1 if flat else 30)
    with open(os.path.join(OUT, name + ".mtl"), "w", encoding="utf-8", newline="\n") as f:
        f.write(header + "\n\n" + MTL)
    bpy.data.objects.remove(obj)


for name in NAMES:
    build(name)
