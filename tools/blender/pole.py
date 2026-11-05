"""Blender (background): builds the utility pole of the scenery and exports it as content/Scenery/pole.obj + pole.mtl.

A modern power pole in the colours of the track blocks and of the street lamp and the fence: a slim tapered steel pole
of the dark grey of the sides of the road on a base plate with bolts and a sleeve, a yellow warning band low on it and
step bolts up its sides; at the top a light grey crossarm along x, held by two braces, with light blue insulators (the
light blue of the glass of the track) and a cap on the top of the pole; halfway up a pole transformer, light grey with
dark grey fins, which shows the pole is one also when it is seen from the end of its crossarm. The pole stands behind the
road and is turned (turn) across it in the levels, so it is seen from the front and from the end of the crossarm alike.

20 model units high (over two cells; the car is 2.4 high), the crossarm about 9 long, the foot at the origin, as the
pole of 2016 was, so that the placed poles need no change.

    blender -b --factory-startup --python tools/blender/pole.py [-- <output folder>]

The owner chose this one of three variants (2026-11-05); the others were the pole without the transformer, and one with
two crossarms and insulators hanging under them.

Blender has z up and its OBJ export writes (x, y, z) as (x, z, -y): the pole is built with z up, the crossarm along x and
the front (the camera, when the pole is not turned) towards +y.
"""
import bpy, math, os, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from props import ROOT, POLE, HEAD, ACCENT, material, finish, box, cylinder, export

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = args[0] if args else os.path.join(ROOT, 'content', 'Scenery')

# Glazed porcelain of the insulators, in the light blue of the glass of the track
INSULATOR = material("insulator", (0.45, 0.72, 0.88), roughness=0.2)

TOP = 19.6           # the top of the pole, under its cap
BASE = 0.1           # the top of the base plate
ARM = 18.6           # the middle of the crossarm
HALF_ARM = 4.5       # half its length

parts = []


def add(obj):
    parts.append(obj)
    return obj


def radius(z):
    """The radius of the tapered pole at a height."""
    return 0.3 - (0.3 - 0.16) * (z - BASE) / (TOP - BASE)


def bar(p0, p1, width, depth, mat):
    """A straight bar of a rectangular section from p0 to p1 (x, z) in the plane of the crossarm, depth along y."""
    (x0, z0), (x1, z1) = p0, p1
    length = math.hypot(x1 - x0, z1 - z0)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0))
    obj = bpy.context.active_object
    obj.scale = (length, depth, width)
    obj.rotation_euler = (0, -math.atan2(z1 - z0, x1 - x0), 0)
    obj.location = ((x0 + x1) / 2, 0, (z0 + z1) / 2)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return add(finish(obj, mat))


def post_insulator(x, z):
    """An insulator standing on the crossarm at x, its foot at the height z: a dark grey pin, a stack of light blue
    sheds on a core and a dark grey clamp on top."""
    add(cylinder(0.05, 0.05, z, z + 0.12, POLE, vertices=10, location=(x, 0)))
    add(cylinder(0.07, 0.07, z + 0.12, z + 0.95, INSULATOR, vertices=16, location=(x, 0)))
    for k in range(5):
        h = z + 0.2 + k * 0.15
        add(cylinder(0.17, 0.09, h, h + 0.07, INSULATOR, vertices=16, location=(x, 0)))
    add(cylinder(0.09, 0.07, z + 0.95, z + 1.08, POLE, vertices=12, location=(x, 0)))


def crossarm(z, half, insulators):
    """A light grey crossarm at the height z (its middle), along x, clamped to the pole, with two braces under it and
    insulators at the x given."""
    add(box((0, 0, z), (2 * half, 0.24, 0.3), HEAD, bevel=0.03))
    r = radius(z)
    add(box((0, 0, z), (0.16, 2 * r + 0.14, 0.62), POLE, bevel=0.02))
    for s in (-1, 1):
        bar((s * (r + 0.02), z - 1.7), (s * half * 0.5, z - 0.13), 0.09, 0.08, POLE)
        add(box((s * half * 0.5, 0, z - 0.19), (0.14, 0.18, 0.08), POLE, bevel=0.01))
        add(cylinder(0.035, 0.035, z - 1.75, z - 1.6, POLE, vertices=8, location=(s * (r + 0.02), 0)))
        # The ends of the crossarm, capped in yellow
        add(box((s * (half + 0.02), 0, z), (0.04, 0.25, 0.31), ACCENT))
    for x in insulators:
        post_insulator(x, z + 0.15)


def transformer(z):
    """A pole transformer at the height z (its middle) on the front of the pole: a light grey tank with dark grey fins
    and lid on two brackets, two light blue bushings on top and a yellow warning plate."""
    y = radius(z) + 0.62
    add(cylinder(0.5, 0.5, z - 0.8, z + 0.7, HEAD, vertices=24, location=(0, y)))
    add(cylinder(0.53, 0.53, z + 0.7, z + 0.82, POLE, vertices=24, location=(0, y)))
    add(cylinder(0.3, 0.42, z - 0.95, z - 0.8, POLE, vertices=24, location=(0, y)))
    for k in range(8):
        a = math.pi * (0.15 + 0.7 * k / 7) + math.pi
        fx, fy = math.cos(a) * 0.55, math.sin(a) * 0.55
        add(box((fx, y + fy, z - 0.05), (0.04, 0.04, 1.2), POLE))
    for h in (z - 0.5, z + 0.45):
        add(box((0, (radius(z) + y - 0.4) / 2, h), (0.22, y - 0.4 - radius(z) + 0.1, 0.1), POLE, bevel=0.01))
    for x in (-0.22, 0.22):
        add(cylinder(0.06, 0.06, z + 0.82, z + 1.2, INSULATOR, vertices=12, location=(x, y)))
        for k in range(2):
            add(cylinder(0.11, 0.07, z + 0.88 + k * 0.14, z + 0.94 + k * 0.14, INSULATOR, vertices=12, location=(x, y)))
    add(box((0, y + 0.5, z + 0.1), (0.3, 0.02, 0.3), ACCENT))


# ---- the base: a plate with four bolts and a sleeve over the foot of the pole
add(box((0, 0, BASE / 2), (0.95, 0.95, BASE), POLE, bevel=0.02))
for sx in (-1, 1):
    for sy in (-1, 1):
        add(cylinder(0.04, 0.04, BASE, BASE + 0.07, POLE, vertices=10, location=(sx * 0.36, sy * 0.36)))
add(cylinder(radius(BASE) + 0.07, radius(0.7) + 0.05, BASE, 0.7, POLE, vertices=24))

# ---- the pole, tapered, a yellow warning band low on it and step bolts up its sides from above the reach of a hand
add(cylinder(radius(BASE), radius(TOP), BASE, TOP, POLE, vertices=24))
add(cylinder(radius(1.1) + 0.012, radius(1.9) + 0.012, 1.1, 1.9, ACCENT, vertices=24))
for k in range(12):
    h = 4.0 + k * 0.95
    s = 1 if k % 2 == 0 else -1
    r = radius(h)
    add(box((s * (r + 0.12), 0, h), (0.26, 0.05, 0.05), POLE))
# The cap on the top
add(cylinder(radius(TOP) + 0.03, 0.06, TOP, TOP + 0.4, POLE, vertices=24))

# ---- the crossarm with its insulators, and the transformer
crossarm(ARM, HALF_ARM, (-3.9, -1.5, 1.5, 3.9))
transformer(13.0)

export(parts, [], "pole", OUT, {
    "pole.obj": "# Scenery: pole, the modern utility pole built by tools/blender/pole.py",
    "pole.mtl": "# Materials of pole (tools/blender/pole.py), those of the street lamp and the fence and the light blue of the\n"
                "# insulators; Pm and Pr: metal and roughness (tools/ContentPipeline)",
})
