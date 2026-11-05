"""Blender (background): builds the railing block "za" and exports it as content/Levels/mod/za.obj + za.mtl.

A modern glass railing in the colours of the track blocks and of the street lamp and the fence, a decoration of the
layers behind the road (no collision): two square dark grey posts on base plates with bolts under yellow caps, a flat
light grey top rail over them, and between the posts a see-through glass panel held by dark grey clamps in two light grey
rails, the lower one with a yellow stripe. Each piece is a span of its own, a little short of the edges of its cell
(the owner's choice, 2026-11-05, of three variants: continuous rails with a kickboard and a continuous mesh panel were the
others), so a row of them is a row of spans with small gaps, as the railing of 2014 was.

It stands at the near edge of its cell, facing the camera (-z of the model), its foot on the top of a road block (0.5),
as the railing of 2014 did, so that the placed railings need no change. Model units of the blocks (a cell is 9 units,
the car 2.4 high).

    blender -b --factory-startup --python tools/blender/railing.py [-- <output folder>]

Blender has z up and its OBJ export writes (x, y, z) as (x, z, -y): the railing is built with z up, along x, its front
towards +y.
"""
import os, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from props import ROOT, POLE, HEAD, ACCENT, material, box, cylinder, export

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = args[0] if args else os.path.join(ROOT, 'content', 'Levels', 'mod')

END = 4.1             # the middle of a post from the middle of the cell
Y = 5.16              # the line of the railing: the near edge of the cell (-z of the model, +y here)
FOOT = 0.5            # the top of a road block
TOP = 5.0             # the top of the top rail
POST = 0.2            # the side of a square post
PLATE, PLATE_SIDE = 0.05, 0.4

GLASS = material("glass", (0.70, 0.88, 0.97), roughness=0.05, alpha=0.3)

parts = []


def add(obj):
    parts.append(obj)
    return obj


def post(x):
    """A post at x on a base plate with four bolts, under a yellow cap."""
    add(box((x, Y, FOOT + PLATE / 2), (PLATE_SIDE, PLATE_SIDE, PLATE), POLE, bevel=0.012))
    for dx in (-0.13, 0.13):
        for dy in (-0.13, 0.13):
            add(cylinder(0.025, 0.025, FOOT + PLATE, FOOT + PLATE + 0.04, POLE, vertices=10, location=(x + dx, Y + dy)))
    add(box((x, Y, (FOOT + PLATE + TOP - 0.1) / 2), (POST, POST, TOP - 0.1 - FOOT - PLATE), POLE, bevel=0.02))
    add(box((x, Y, TOP - 0.01), (POST + 0.04, POST + 0.04, 0.18), ACCENT, bevel=0.015))


def rail(x0, x1, z, height, depth, mat):
    """A flat rail from x0 to x1 at the height z (its middle)."""
    add(box(((x0 + x1) / 2, Y, z), (x1 - x0, depth, height), mat, bevel=0.015))


# ---- the two posts and the top rail over them
for x in (-END, END):
    post(x)
add(box((0, Y, TOP + 0.02), (2 * END + 0.3, 0.34, 0.1), HEAD, bevel=0.02))

# ---- the glass panel between the posts, in a lower and an upper rail, held by clamps at its corners
left, right = -END + POST / 2, END - POST / 2
rail(left, right, FOOT + 0.55, 0.12, 0.12, HEAD)
rail(left, right, TOP - 0.3, 0.12, 0.12, HEAD)
add(box((0, Y, (FOOT + 0.61 + TOP - 0.36) / 2), (right - left - 0.04, 0.03, TOP - 0.36 - FOOT - 0.61), GLASS))
for x in (left + 0.06, right - 0.06):
    for z in (FOOT + 0.9, TOP - 0.65):
        add(box((x, Y, z), (0.1, 0.1, 0.16), POLE, bevel=0.01))
# A yellow stripe along the lower rail
rail(left, right, FOOT + 0.55, 0.05, 0.125, ACCENT)

export(parts, [], "za", OUT, {
    "za.obj": "# Block za: the modern glass railing built by tools/blender/railing.py",
    "za.mtl": "# Materials of za (tools/blender/railing.py), those of the street lamp and the fence and the see-through glass;\n"
              "# Pm and Pr: metal and roughness (tools/ContentPipeline)",
})
