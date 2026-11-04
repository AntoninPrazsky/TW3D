"""Blender (background): builds the metal fence of the scenery and exports it as content/Scenery/fence.obj + fence.mtl.

A modern welded mesh fence in the colours of the track blocks and of the street lamp: panels of dark grey wire with
three V-shaped bends along them, held by clamps to square posts of the same dark grey, each post on a base plate with
bolts and under a yellow cap. A piece is half a cell long (4.5 units along x, from -2.25 to 2.25, its foot at the
origin) and has half a post at each end, cut along the end of the piece: where two pieces of a row meet, their halves
make one whole post, and a row ends with half a post at either end. The fence is about as high as the old one (3.5
units; the car is 2.4) and looks the same from both of its sides, as it stands behind, in front of and across the road.

    blender -b --factory-startup --python tools/blender/fence.py [-- <output folder>]

Blender has z up and its OBJ export writes (x, y, z) as (x, z, -y): the fence is built with z up, along x.
"""
import os, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from props import ROOT, POLE, ACCENT, box, cylinder, polyline, export

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = args[0] if args else os.path.join(ROOT, 'content', 'Scenery')

HALF = 2.25                        # half the length of a piece
TOP = 3.4                          # the top of a post, under its cap
POST = 0.2                         # the side of a square post
PLATE, PLATE_SIDE = 0.06, 0.42     # the base plate of a post
PANEL = HALF - POST / 2 - 0.035    # the panel reaches from -PANEL to PANEL, a little off the posts

parts = []


def half_box(end, depth, size, z0, z1, mat, bevel):
    """The half of a box centred on the end of the piece (x = end, -HALF or HALF) that lies inside the piece: size wide
    along x when whole, depth along y, from height z0 to z1. Its edges are rounded but for those of the face on the end of
    the piece, so that the halves of two pieces make one box without a seam."""
    inward = -1 if end > 0 else 1
    obj = box((end + inward * size / 4, 0, (z0 + z1) / 2), (size / 2, depth, z1 - z0), mat)
    weights = obj.data.attributes.new("bevel_weight_edge", 'FLOAT', 'EDGE')
    for edge in obj.data.edges:
        on_end = all(abs((obj.matrix_world @ obj.data.vertices[v].co).x - end) < 1e-5 for v in edge.vertices)
        weights.data[edge.index].value = 0.0 if on_end else 1.0
    mod = obj.modifiers.new("bevel", 'BEVEL')
    mod.width = bevel
    mod.segments = 2
    mod.limit_method = 'WEIGHT'
    return obj


# ---- half a post at each end: a base plate with two of its four bolts, the post and its yellow cap
for end in (-HALF, HALF):
    inward = -1 if end > 0 else 1
    parts.append(half_box(end, PLATE_SIDE, PLATE_SIDE, 0.0, PLATE, POLE, 0.012))
    for dy in (-0.14, 0.14):
        parts.append(cylinder(0.026, 0.026, PLATE, PLATE + 0.04, POLE, vertices=10, location=(end + inward * 0.14, dy)))
    parts.append(half_box(end, POST, POST, PLATE, TOP, POLE, 0.02))
    parts.append(half_box(end, POST + 0.04, POST + 0.04, TOP - 0.005, TOP + 0.065, ACCENT, 0.015))
    parts.append(half_box(end, POST - 0.02, POST - 0.02, TOP + 0.06, TOP + 0.1, ACCENT, 0.012))

# ---- the welded mesh panel: vertical wires bent out into a V at three heights, horizontal wires on both sides of them
BOTTOM, MESH_TOP = 0.2, TOP - 0.1
BENDS = (0.75, 1.75, 2.75)   # the heights of the bends
DEPTH, SPAN = 0.075, 0.16    # how far a bend stands out (towards +y), and half its height
WIRE = 0.02                  # the radius of a wire
VERTICAL_WIRES = 22


def wire_y(z):
    """How far out of the plane of the panel a vertical wire is at a height."""
    for bend in BENDS:
        if abs(z - bend) < SPAN:
            return DEPTH * (1 - abs(z - bend) / SPAN)
    return 0.0


profile = [(wire_y(z), z) for z in sorted({BOTTOM, MESH_TOP} | {b + d for b in BENDS for d in (-SPAN, 0.0, SPAN)})]
for i in range(VERTICAL_WIRES):
    x = -PANEL + 2 * PANEL * i / (VERTICAL_WIRES - 1)
    parts.append(polyline([(x, y, z) for y, z in profile], WIRE, POLE))
# At the bottom and the top, at the bends and halfway between them
for z in (BOTTOM + 0.02, BENDS[0], (BENDS[0] + BENDS[1]) / 2, BENDS[1], (BENDS[1] + BENDS[2]) / 2, BENDS[2], MESH_TOP - 0.02):
    y = wire_y(z)
    for side in (-1, 1):
        parts.append(polyline([(-PANEL, y + side * 2 * WIRE, z), (PANEL, y + side * 2 * WIRE, z)], WIRE, POLE))

# ---- the clamps that hold the panel to the posts, at both of its ends
for z in (0.5, 1.5, 2.5, MESH_TOP - 0.15):
    for x in (-PANEL - 0.005, PANEL + 0.005):
        parts.append(box((x, 0, z), (0.08, 0.13, 0.1), POLE, bevel=0.01))

export(parts, [], "fence", OUT, {
    "fence.obj": "# Scenery: fence, the modern welded mesh fence built by tools/blender/fence.py",
    "fence.mtl": "# Materials of fence (tools/blender/fence.py), those of the street lamp; Pm and Pr: metal and roughness\n"
                 "# (tools/ContentPipeline)",
})
