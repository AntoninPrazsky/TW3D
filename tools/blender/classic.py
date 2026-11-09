"""Blender (background): builds track blocks in the classic look of 2016 (the blocks of content/Levels/mod made in Wings 3D:
a grey slab of the road with yellow hazard stripes along both edges), from the outline of the block seen from the side.

The outline is swept straight across the road, from z = -4.5 to 4.5. Its edges that face up are the road: the middle of
it (|z| < 3.6) is plain grey, the edges (3.6 to 4.5) carry the stripes of the classic blocks, parallelograms 0.9 long
slanted by 45° across the edge, yellow and grey by turns along x (a1 has them so, and the slopes as seen from above), so
that the stripes of a new block run on from those of a flat road beside it. The other edges (the risers of the steps,
the bottom, the ends) are plain grey, as are the faces at the sides. The materials are those of the classic blocks
(road grey Kd 0.22744, hazard yellow), written into the .mtl as the Wings 3D exports have them.

The blocks (#61, the steps for the axles): k1, k2, k3, a step down of 1, 2 and 3 shift steps (2.5, 5 and 7.5 physics
units) in the middle of the cell, and s2, stairs of two steps of 2 shift steps (5 units), the drop of s1. Every block
starts and ends with the slab of a flat road, one shift step thick, so that it meets a1 and s1 flush; below a step the
block is solid down to a sloping bottom, as s1 is.

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


BLOCKS = {
    "k1": ("a step down of 2.5 units", step(1)),
    "k2": ("a step down of 5 units", step(2)),
    "k3": ("a step down of 7.5 units", step(3)),
    "s2": ("stairs of two steps of 5 units", stairs(2, 2)),
}

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
# The first argument is the output folder unless it names a block
OUT = args.pop(0) if args and args[0] not in BLOCKS else os.path.join(ROOT, 'content', 'Levels', 'mod')
NAMES = args or list(BLOCKS)


def faces_up(p, q):
    """An edge of the counter-clockwise outline is the road when it faces up (it runs leftwards, -x)."""
    return q[0] < p[0] - 1e-6 and abs(q[1] - p[1]) < (p[0] - q[0]) * 2


def grid(p, q):
    """The x where a road edge from p to q is cut: its ends and the multiples of a stripe from -4.5 between them."""
    xs = {p[0], q[0]}
    i = math.ceil((q[0] + HALF) / STRIPE - 1e-6)
    while -HALF + i * STRIPE < p[0] - 1e-6:
        xs.add(-HALF + i * STRIPE)
        i += 1
    xs = sorted(xs, reverse=True)
    for a, b in zip(xs, xs[1:]):
        if abs(a - b - STRIPE) > 1e-6 and not (abs(a - p[0]) < 1e-6 or abs(b - q[0]) < 1e-6):
            raise ValueError("a stripe cell inside the road is not 0.9 long")
    return [(x, p[1] + (q[1] - p[1]) * (x - p[0]) / (q[0] - p[0])) for x in xs]


def hazard(x, w, front):
    """Whether the point (x, |z| = w) of the edge strip of the road is a yellow stripe (as the stripes of a1 lie)."""
    s = x - (w - EDGE) + HALF if front else x + (w - EDGE) + HALF
    s %= 2 * STRIPE
    return s < STRIPE if front else s >= STRIPE


def build(name):
    title, outline = BLOCKS[name]
    n = len(outline)
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

    rows = [-HALF, -EDGE, EDGE, HALF]
    sides = {HALF: [], -HALF: []}        # the outline at both sides, with every point the road is cut at
    for i in range(n):
        p, q = outline[i], outline[(i + 1) % n]
        if up[i]:
            cut = grid(p, q)
            for (xa, ya), (xb, yb) in zip(cut, cut[1:]):        # xa > xb: the road runs leftwards
                # The middle of the road
                face([(xa, ya, -EDGE), (xb, yb, -EDGE), (xb, yb, EDGE), (xa, ya, EDGE)], ROAD)
                # The front edge (z > 0): the cell split along the slant of the stripes, from (xb, 3.6) to (xa, 4.5)
                for tri in (((xb, yb, EDGE), (xb, yb, HALF), (xa, ya, HALF)), ((xb, yb, EDGE), (xa, ya, HALF), (xa, ya, EDGE))):
                    cx, cw = sum(t[0] for t in tri) / 3, sum(t[2] for t in tri) / 3
                    face(tri, HAZARD if hazard(cx, cw, True) else ROAD)
                # The back edge (z < 0): the slant mirrored, from (xa, -3.6) to (xb, -4.5)
                for tri in (((xa, ya, -EDGE), (xa, ya, -HALF), (xb, yb, -HALF)), ((xa, ya, -EDGE), (xb, yb, -HALF), (xb, yb, -EDGE))):
                    cx, cw = sum(t[0] for t in tri) / 3, -sum(t[2] for t in tri) / 3
                    face(tri, HAZARD if hazard(cx, cw, False) else ROAD)
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
    header = f"# Made by tools/blender/classic.py: {name}, {title}, in the classic look (#61)"
    # Flat shading, as the faces of the classic blocks have
    export([obj], [], name, OUT, {name + ".obj": header}, smooth_angle=1)
    with open(os.path.join(OUT, name + ".mtl"), "w", encoding="utf-8", newline="\n") as f:
        f.write(header + "\n\n" + MTL)
    bpy.data.objects.remove(obj)


for name in NAMES:
    build(name)
