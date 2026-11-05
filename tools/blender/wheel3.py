"""Blender (background): builds the third wheel of the garage, the rock crawler wheel, as content/wheel3.obj + .mtl.

A tall, wide, soft-looking tyre with a deep chunky tread and a beadlock wheel under a hubcap, the default wheel of the
Crawler (car3) and a wheel for any car. The same size as the other wheels: the radius of the tread 0.705 model units
(the circle of the physics), the wheel within 1.42 <= |z| <= 2.18 at the side of the car (where the collision shapes
expect the wheels), its axle along z, the outer face towards -z, the inner one towards the holder.

The tread: big separate lugs from each shoulder to past the middle, slanting, the two sides offset by half a pitch
(the same both ways, so the near and the far wheels are alike), each one solid lofted across the tyre (no parts through
each other), highest in the middle of the tread and lower towards the shoulders, standing on the rounded carcass, their
edges a little bevelled, and stepped shoulder blocks on the sidewalls under their ends. The owner chose it (2026-11-05)
over a directional chevron tread, and the hubcap over an open beadlock rim. The face: a hubcap ("rim", its colour chosen in the
garage, #46; orange, as the springs of the Crawler, by default) over the whole rim, with three slanted dark ribs, the
hub with its nuts and the ring of the bolts of the beadlock round it.

    blender -b --factory-startup --python tools/blender/wheel3.py [-- <output folder>]

Blender has z up and its OBJ export writes (x, y, z) as (x, z, -y): the wheel is built lying (its axle along z, the
outer face up), then stood up with its axle along y and the outer face towards +y.
"""
import bpy, bmesh, math, os, sys
from mathutils import Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from props import ROOT, scene, material, finish, cylinder, box, export

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = args[0] if args else os.path.join(ROOT, 'content')

TYRE = material("tyre", (0.11, 0.11, 0.115), roughness=0.85)
RIM = material("rim", (1.0, 0.42, 0.0), metallic=0.3, roughness=0.4)
STEEL = material("steel", (0.62, 0.62, 0.62), metallic=1.0, roughness=0.3)
DARK = material("darkmetal", (0.16, 0.16, 0.17), metallic=0.8, roughness=0.4)

R_TREAD = 0.705       # the radius of the tread: the circle of the physics
R_CARCASS = 0.62      # the tyre under the lugs
SHOULDER = 0.09       # the radius of the rounded shoulders of the carcass
R_RIM = 0.42          # the rim and its hubcap: the sidewall is tall
HALF_W = 0.33         # half the width of the tyre
CENTRE = 1.77         # the middle of the wheel from the middle of the car (|z| of the OBJ): it spans 1.43 to 2.17
LUGS = 14             # pairs of lugs round the tyre

parts = []


def add(obj):
    parts.append(obj)
    return obj


def link(bm, name, mat, bevel=None):
    """An object of a bmesh, with a material and, if asked, bevelled edges."""
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    return add(finish(obj, mat, bevel, segments=1))


def carcass_radius(z):
    """The radius of the carcass at z across the tread: flat, then the rounded shoulder."""
    over = abs(z) - (HALF_W - SHOULDER)
    if over <= 0:
        return R_CARCASS
    return R_CARCASS - SHOULDER + math.sqrt(max(0.0, SHOULDER ** 2 - over ** 2))


def crown(z):
    """The top of a lug at z: highest in the middle of the tread, lower towards the shoulders."""
    return R_TREAD - 0.035 * (abs(z) / HALF_W) ** 3


def lug(theta, path, half_width):
    """A lug of the tread lofted along a path of (s, z) across the tread (s along the tread, wrapping round the tyre),
    half_width wide along s, from a little inside the carcass to the crown: one closed solid."""
    bm = bmesh.new()
    sections = []
    for s, z in path:
        bottom, top = carcass_radius(z) - 0.03, crown(z)
        section = []
        for ds, r in ((-half_width, bottom), (half_width, bottom), (half_width, top), (-half_width, top)):
            a = theta + (s + ds) / R_TREAD
            section.append(bm.verts.new((r * math.cos(a), r * math.sin(a), z)))
        sections.append(section)
    bm.faces.new(sections[0])
    bm.faces.new(list(reversed(sections[-1])))
    for a, b in zip(sections, sections[1:]):
        for i in range(4):
            j = (i + 1) % 4
            bm.faces.new((a[i], a[j], b[j], b[i]))
    return link(bm, "lug", TYRE, bevel=0.012)


def shoulder_block(theta, s, side, half_width):
    """A stepped block on the sidewall under the end of a lug, at s along the tread, on the side (-1 inner, 1 outer)."""
    bm = bmesh.new()
    verts = []
    for z in (side * (HALF_W - 0.02), side * (HALF_W + 0.018)):
        for ds, r in ((-half_width, 0.49), (half_width, 0.49), (half_width * 0.8, carcass_radius(HALF_W - 0.05) - 0.005),
                      (-half_width * 0.8, carcass_radius(HALF_W - 0.05) - 0.005)):
            a = theta + (s + ds) / R_TREAD
            verts.append(bm.verts.new((r * math.cos(a), r * math.sin(a), z)))
    bm.faces.new(verts[0:4])
    bm.faces.new(list(reversed(verts[4:8])))
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new((verts[i], verts[j], verts[4 + j], verts[4 + i]))
    return link(bm, "shoulder", TYRE, bevel=0.01)


def ring_of(r_in, r_out, z0, z1, mat, vertices=40):
    """A flat ring round the axle from the radius r_in to r_out, from the height z0 to z1."""
    bm = bmesh.new()
    loops = [[bm.verts.new((r * math.cos(2 * math.pi * k / vertices), r * math.sin(2 * math.pi * k / vertices), z))
              for k in range(vertices)] for r, z in ((r_in, z0), (r_out, z0), (r_out, z1), (r_in, z1))]
    for a, b in ((0, 1), (1, 2), (2, 3), (3, 0)):
        for k in range(vertices):
            j = (k + 1) % vertices
            bm.faces.new((loops[a][k], loops[a][j], loops[b][j], loops[b][k]))
    return link(bm, "ring", mat)


# ---- the tyre: the carcass with rounded shoulders, the lugs of the tread on it, the shoulder blocks
carcass = cylinder(R_CARCASS, R_CARCASS, -HALF_W, HALF_W, TYRE, vertices=56, bevel=SHOULDER)
carcass.modifiers["bevel"].segments = 3
add(carcass)
pitch = 2 * math.pi * R_TREAD / LUGS
for k in range(LUGS):
    theta = 2 * math.pi * k / LUGS
    # A lug from each shoulder to past the middle, slanting, the two sides offset by half a pitch
    for side, offset in ((-1, 0.0), (1, 0.5)):
        s = offset * pitch
        edge = side * (HALF_W - 0.015)
        lug(theta, [(s - 0.04, edge), (s - 0.02, side * 0.18), (s + 0.01, side * 0.04), (s + 0.02, -side * 0.03)], 0.075)
        shoulder_block(theta, s - 0.04, side, 0.07)

# ---- the outer face: the hubcap over the rim, three slanted dark ribs, the hub with nuts, the bolts of the beadlock
Z = HALF_W
add(cylinder(R_RIM, R_RIM, Z - 0.05, Z + 0.005, DARK, vertices=48))
add(cylinder(R_RIM + 0.04, R_RIM - 0.035, Z + 0.005, Z + 0.045, RIM, vertices=48))
for k in range(3):
    # A rib at each third of the turn, all slanting the same way from the radius: the blades of a turbine
    position = 2 * math.pi * k / 3
    turn = position + math.radians(55)
    rib = box(((R_RIM - 0.17) * math.cos(position), (R_RIM - 0.17) * math.sin(position), Z + 0.05), (0.26, 0.06, 0.03), DARK, bevel=0.01)
    centre = rib.matrix_world.to_translation()
    rib.matrix_world = Matrix.Translation(centre) @ Matrix.Rotation(turn, 4, 'Z') @ Matrix.Translation(-centre) @ rib.matrix_world
    add(rib)
for k in range(16):
    a = 2 * math.pi * (k + 0.5) / 16
    add(cylinder(0.016, 0.016, Z + 0.04, Z + 0.058, STEEL, vertices=6, location=((R_RIM + 0.003) * math.cos(a), (R_RIM + 0.003) * math.sin(a))))
add(cylinder(0.12, 0.1, Z + 0.045, Z + 0.06, DARK, vertices=24))
add(cylinder(0.06, 0.045, Z + 0.06, Z + 0.07, DARK, vertices=16))
for k in range(6):
    a = 2 * math.pi * k / 6
    add(cylinder(0.016, 0.016, Z + 0.058, Z + 0.068, STEEL, vertices=6, location=(0.085 * math.cos(a), 0.085 * math.sin(a))))

# ---- the inner face: the back of the rim, a dark dish and the hub the holder holds
ring_of(R_RIM - 0.07, R_RIM, -Z - 0.005, -Z + 0.05, RIM)
add(cylinder(R_RIM - 0.07, R_RIM - 0.07, -Z, -Z + 0.05, DARK, vertices=40))
add(cylinder(0.14, 0.14, -Z - 0.01, -Z + 0.06, DARK, vertices=24))

# ---- stand the wheel up: its axle along y, the outer face towards +y, at its place beside the car
stand = Matrix.Translation((0, CENTRE, 0)) @ Matrix.Rotation(math.radians(-90), 4, 'X')
for obj in parts:
    obj.matrix_world = stand @ obj.matrix_world

export(parts, [], "wheel3", OUT, {
    "wheel3.obj": "# The third wheel, the rock crawler wheel, built by tools/blender/wheel3.py",
    "wheel3.mtl": "# Materials of wheel3 (tools/blender/wheel3.py); Pm and Pr: metal and roughness (tools/ContentPipeline).\n"
                  "# The hubcap \"rim\" takes the colour chosen in the garage",
}, smooth_angle=40)
