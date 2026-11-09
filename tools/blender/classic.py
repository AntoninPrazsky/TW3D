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
- #64, the road ends: r1, r2, r3, the flat road from the left edge of the cell ending 2.7, 4.5 and 6.3 model units into
  it (lengths on the grid of the stripes), with a band of hazard stripes across the end; turned, the road starting
  after a gap. Their profile is that of a flat road: the edge without road has the height a road would go on at.
- #63, the bridge: br1, a deck of road 0.4 thick on a steel truss under each edge (a bridge, or a low ceiling hung over
  the road); br2, the head of a lattice steel pillar, a cell under a deck, whose cap beam carries both trusses; br3, a
  piece of the column to stack under the head. Parts that are not the road come as extras (steel, see MTL_STEEL).
- #65, the rough road: w1, w2, washboards of five ridges a cell; l1, l2, a log across the road, here a speed bump (half
  a cylinder striped along the road); rk1, rk2, rk3, small stones, big ones and a boulder with sheer faces that needs the
  axles turned (the stones of tools/blender/rocks.py, the same in the off-road look).
Every block starts with the slab of a road one shift step thick, and but for a road end ends with one, so that it meets
a1, the slopes and s1 flush.

    blender -b --factory-startup --python tools/blender/classic.py -- [<output folder>] [<block>...]

Without a folder the blocks go to content/Levels/mod. The coordinates are those of the game (model units: a cell is 9,
a shift step 1; +x is the left edge of a block placed unturned in a level, y up, -z towards the camera: the scene
turns the world round). Blender has z
up and its OBJ export writes (x, y, z) as (x, z, -y): the game's (x, y, z) is built at (x, -z, y).
"""
import bpy, bmesh, math, os, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from props import ROOT, material, export, box, polyline
import rocks

ROAD = material("road", (0.22744, 0.22744, 0.22744))
HAZARD = material("hazard", (1.0, 0.71333, 0.0))
MATERIALS = [ROAD, HAZARD]
# The steel of the girders of a deck: the dark grey metal of the street lamp's pole
STEEL = material("steel", (0.22744, 0.22744, 0.22744), metallic=0.6, roughness=0.45)
# The stone of the rocks lying on the road
STONE = material("stone", (0.42, 0.41, 0.39), roughness=0.9)
STONE2 = material("stonedark", (0.31, 0.30, 0.29), roughness=0.9)
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
MTL_STEEL = """
newmtl steel
Kd 0.227440 0.227440 0.227440
Ks 0.500000 0.500000 0.500000
Ke 0.000000 0.000000 0.000000
Ni 1.500000
d 1.000000
illum 3
Pr 0.450000
Pm 0.600000
"""
MTL_STONE = """
newmtl stone
Kd 0.420000 0.410000 0.390000
Ks 0.100000 0.100000 0.100000
Ke 0.000000 0.000000 0.000000
d 1.000000
illum 2
Pr 0.900000
Pm 0.000000
"""
MTL_STONE2 = """
newmtl stonedark
Kd 0.310000 0.300000 0.290000
Ks 0.100000 0.100000 0.100000
Ke 0.000000 0.000000 0.000000
d 1.000000
illum 2
Pr 0.900000
Pm 0.000000
"""
# The materials of parts that are not the road, written into the .mtl of a block that uses them
MTL_PARTS = {"steel": MTL_STEEL, "stone": MTL_STONE, "stonedark": MTL_STONE2}

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


def road_end(length):
    """The flat road from the left edge of the cell, ending `length` model units into it (a ledge at a gap)."""
    return [(HALF, 0.5), (HALF - length, 0.5), (HALF - length, -0.5), (HALF, -0.5)]


def deck(thickness):
    """A thin deck of road across the whole cell, its top where a flat road's is."""
    return [(HALF, 0.5), (-HALF, 0.5), (-HALF, 0.5 - thickness), (HALF, 0.5 - thickness)]


# The parts under the edges of a deck stand at |z| above 2.2, outside the bands tools/CollisionGen cuts the shapes of
# the body and the wheels from, so the deck alone collides. Blender's (x, y, z) is the game's (x, -z, y).
TRUSS_Z = 3.8       # the trusses under the edges of the deck (|z|)
TRUSS_DEPTH = 1.3   # from the deck's bottom to the axis of the bottom chord
DECK = 0.4          # the thickness of the deck
COLUMN_X, COLUMN_Z = 0.45, 2.0   # half the size of a pillar along the road and across it (through both bands)


def truss(bottom):
    """A steel truss under each edge of a deck: chords along the cell, diagonals in a W (panels of an eighth of it)."""
    parts = []
    low = bottom - TRUSS_DEPTH
    for side in (1, -1):
        z = -TRUSS_Z * side
        parts.append(polyline([(-HALF, z, bottom - 0.06), (HALF, z, bottom - 0.06)], 0.07, STEEL))
        parts.append(polyline([(-HALF, z, low), (HALF, z, low)], 0.07, STEEL))
        zigzag = [(-HALF + 1.125 * i, z, low if i % 2 else bottom - 0.06) for i in range(9)]
        parts.append(polyline(zigzag, 0.05, STEEL))
    return parts


def lattice(bottom, top, panels):
    """A steel lattice column in the middle of the road from y = bottom to top: four corner posts, a ring at the bottom
    of each panel and a diagonal across each face of it, with a thin web across the road in its middle (edge on to the
    camera). The posts stand in the band of the wheels and the web in that of the body, so that all of the car collides
    with the column (tools/CollisionGen)."""
    parts = [box((0, 0, (bottom + top) / 2), (0.06, 2 * COLUMN_Z, top - bottom), STEEL)]
    corners = [(-COLUMN_X, -COLUMN_Z), (COLUMN_X, -COLUMN_Z), (COLUMN_X, COLUMN_Z), (-COLUMN_X, COLUMN_Z)]
    for x, z in corners:
        parts.append(polyline([(x, -z, bottom), (x, -z, top)], 0.06, STEEL))
    height = (top - bottom) / panels
    for i in range(panels):
        y0, y1 = bottom + i * height, bottom + (i + 1) * height
        parts.append(polyline([(x, -z, y0) for x, z in corners + corners[:1]], 0.05, STEEL))
        for (xa, za), (xb, zb) in zip(corners, corners[1:] + corners[:1]):
            a, b = ((xa, za), (xb, zb)) if i % 2 else ((xb, zb), (xa, za))
            parts.append(polyline([(a[0], -a[1], y0), (b[0], -b[1], y1)], 0.04, STEEL))
    return parts


def pillar_head():
    """The top of a bridge pillar, placed a cell under a deck (9 shift steps lower): the lattice column from the bottom
    of its cell up to a cap beam that carries both trusses of the deck, whose bottom chords lie 9 - 0.1 - 1.3 above."""
    chord = 9 + 0.5 - DECK - TRUSS_DEPTH - 0.07       # the underside of the bottom chords, in the pillar's coordinates
    cap = 0.4
    return lattice(-HALF, chord - cap, 13) + [box((0, 0, chord - cap / 2), (1.2, 2 * TRUSS_Z + 0.4, cap), STEEL)]


def hump(radius, segments=24):
    """A flat road with half a cylinder of the radius lying across it in the middle of the cell (a radius on the grid of
    the stripes, so that the flat road on both sides of it is cut as a1 is)."""
    arc = [(radius * math.cos(math.pi * i / segments), 0.5 + radius * math.sin(math.pi * i / segments))
           for i in range(segments + 1)]
    return [(HALF, 0.5)] + arc + [(-HALF, 0.5), (-HALF, -0.5), (HALF, -0.5)]


BLOCKS = {
    # name: (what it is, the outline, cuts to a stripe, flat shading, the issue[, the x of a hazard band across the road])
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
    # The road ends at a gap, with a hazard band across the end (the owner chose it over a plain end)
    "r1": ("the road ending 2.7 units into the cell", road_end(2.7), 1, True, 64, {"band": HALF - 2.7}),
    "r2": ("the road ending in the middle of the cell", road_end(4.5), 1, True, 64, {"band": 0.0}),
    "r3": ("the road ending 6.3 units into the cell", road_end(6.3), 1, True, 64, {"band": HALF - 6.3}),
    # The thin deck (#63): a bridge, or a low ceiling hung over the road; the owner chose the trusses of three sketches
    "br1": ("a thin deck on steel trusses", deck(DECK), 1, True, 63, {"extras": lambda: truss(0.5 - DECK)}),
    # Its pillar: the head a cell under a deck, and pieces of the column to stack under it
    "br2": ("the head of a bridge pillar", None, 1, True, 63, {"extras": pillar_head}),
    "br3": ("a piece of a bridge pillar", None, 1, True, 63, {"extras": lambda: lattice(-HALF, HALF, 10)}),
    # The rough road (#65): a washboard of ridges, a log across the road (in the classic look a speed bump striped along
    # the road), rocks
    "w1": ("a washboard of low ridges", road(lambda u: -0.25 * (1 - math.cos(2 * math.pi * 5 * u)) / 2, 4), 4, False, 65),
    "w2": ("a washboard of high ridges", road(lambda u: -0.5 * (1 - math.cos(2 * math.pi * 5 * u)) / 2, 4), 4, False, 65),
    "l1": ("a low log across the road", hump(0.9), 1, False, 65, {"arc": 0.9}),
    "l2": ("a high log across the road", hump(1.8), 1, False, 65, {"arc": 1.8}),
    "rk1": ("small rocks on the road", deck(1.0), 1, False, 65, {"extras": lambda: rocks.cluster("rk1", [STONE, STONE2])}),
    "rk2": ("big rocks on the road", deck(1.0), 1, False, 65, {"extras": lambda: rocks.cluster("rk2", [STONE, STONE2])}),
    "rk3": ("a boulder across the road, steep: the axles get a car over it", deck(1.0), 1, False, 65,
            {"extras": lambda: rocks.cluster("rk3", [STONE, STONE2])}),
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
    title, outline, div, flat, issue, *options = BLOCKS[name]
    options = options[0] if options else {}
    if outline is None:
        parts = options["extras"]()
        finish_block(name, title, issue, parts[0], parts[1:], flat)
        return
    band = round(options["band"], 6) if "band" in options else None
    # Where the road ends at a band, the last stripe across the road is cut across it too, every 0.9 (z from -3.6 to 3.6)
    across = [round(-EDGE + STRIPE * k, 6) for k in range(round(2 * EDGE / STRIPE) + 1)]
    full = set() if band is None else {band, round(band + STRIPE, 6)}
    arc = options.get("arc")
    if arc is not None:
        # A log: its half cylinder is cut across the road as a band is, and the road meets it so
        full |= {round(arc, 6), round(-arc, 6)}

    def middle(x):
        """The z the middle of the road is cut at along x, ascending."""
        return across if round(x, 6) in full else [-EDGE, EDGE]
    n = len(outline)
    cell = STRIPE / div
    widths = [EDGE + (HALF - EDGE) * j / div for j in range(div + 1)]     # the rows across an edge strip (|z|)
    def on_arc(i):
        """The edge is a part of the half cylinder of a log (both its ends on it)."""
        return arc is not None and all(abs(pt[0]) <= arc + 1e-6 and pt[1] >= 0.5 - 1e-6
                                       for pt in (outline[i], outline[(i + 1) % n]))
    up = [faces_up(outline[i], outline[(i + 1) % n]) and not on_arc(i) for i in range(n)]
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

    def rows_at(x):
        """The z a riser meets the road at: the rows of the edge strips, and the cuts across a band."""
        return [-w for w in reversed(widths)] + middle(x)[1:-1] + widths
    sides = {HALF: [], -HALF: []}        # the outline at both sides, with every point the road is cut at
    for i in range(n):
        p, q = outline[i], outline[(i + 1) % n]
        if up[i]:
            cut = grid(p, q, cell)
            for (xa, ya), (xb, yb) in zip(cut, cut[1:]):        # xa > xb: the road runs leftwards
                if band is not None and abs(xb - band) < 1e-6:
                    # The band across the end of the road: squares split along a slant of 45°, yellow and grey by turns
                    for z0, z1 in zip(across, across[1:]):
                        for tri in (((xb, yb, z0), (xa, ya, z0), (xa, ya, z1)), ((xb, yb, z0), (xa, ya, z1), (xb, yb, z1))):
                            cx, cz = sum(t[0] for t in tri) / 3, sum(t[2] for t in tri) / 3
                            face(tri, HAZARD if (cx - cz) % (2 * STRIPE) < STRIPE else ROAD)
                else:
                    # The middle of the road (with the cuts of a band on the side next to it)
                    face([(xa, ya, -EDGE)] + [(xb, yb, z) for z in middle(xb)] + [(xa, ya, z) for z in reversed(middle(xa)[1:])],
                         ROAD)
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
        elif on_arc(i):
            # The half cylinder of a log: stripes along the road, yellow and grey by turns across it (yellow at the
            # edges and in the middle)
            cuts = rows_at(arc)
            for z0, z1 in zip(cuts, cuts[1:]):
                yellow = int(abs(z0 + z1) / 2 / STRIPE) % 2 == 0
                face([(p[0], p[1], z0), (q[0], q[1], z0), (q[0], q[1], z1), (p[0], p[1], z1)], HAZARD if yellow else ROAD)
            for side in sides:
                sides[side].append(p)
        else:
            # A riser, the bottom or an end: one face across the block, through the rows of the road at its ends
            zs_p = rows_at(p[0]) if i in road else [-HALF, HALF]
            zs_q = rows_at(q[0]) if (i + 1) % n in road else [-HALF, HALF]
            face([(p[0], p[1], z) for z in zs_p] + [(q[0], q[1], z) for z in reversed(zs_q)], ROAD)
            for side in sides:
                sides[side].append(p)
    for z, points in sides.items():
        face([(x, y, z) for x, y in points], ROAD)

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    finish_block(name, title, issue, obj, options["extras"]() if "extras" in options else [], flat)


def finish_block(name, title, issue, obj, extras, flat):
    """Joins the parts, exports the block and writes its .mtl with the materials of the classic blocks it uses."""
    header = f"# Made by tools/blender/classic.py: {name}, {title}, in the classic look (#{issue})"
    # Flat shading, as the faces of the classic blocks have; a curved road (and stones) smooth, sharp at their edges
    export([obj] + extras, [], name, OUT, {name + ".obj": header}, smooth_angle=1 if flat else 30)
    used = {line.split()[1] for line in open(os.path.join(OUT, name + ".obj"), encoding="utf-8") if line.startswith("usemtl ")}
    with open(os.path.join(OUT, name + ".mtl"), "w", encoding="utf-8", newline="\n") as f:
        f.write(header + "\n\n" + MTL + "".join(text for part, text in MTL_PARTS.items() if part in used))
    bpy.data.objects.remove(obj)


for name in NAMES:
    build(name)
