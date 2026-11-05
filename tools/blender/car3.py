"""Blender (background): builds the third car of the garage, the blue crawler, and exports it as content/car3.obj + .mtl.

A rock crawler on the chassis all cars share: an exoskeleton cage of blue tubes over two seats, a short blue hood over the
engine with the turbo standing out of it, a tube bumper with a winch, rock sliders along the sides, coil-over shocks
with orange springs at both axles, and a rack at the back with the fuel cell and a spare wheel standing on it across
the car. The engine with its turbo and cooler, the seat (twice), the steering wheel, the pedals, the gear lever and
the fuel cell are the parts of the Buggy of 2014, taken from content/car.obj where they stand in the Buggy and
recoloured; the spare is the Crawler's own wheel, the rock crawler wheel (content/wheel3.obj, tools/blender/wheel3.py),
with fewer triangles. About 13 000 triangles.

The model units are those of the cars: the front towards -x, y up in the OBJ file, the middle of the body at the
origin. The holders of the wheels (Car.cs) turn about pivots at x = -3.6 (front) and 3.0 (rear), 1.25 below the
middle; their plates sweep |z| 1.01 to 1.47 within about 1.05 of a pivot, the wheels |z| from 1.42 within about 1.6.
The body keeps out of those places and within |z| <= 1.4. The car collides with the outline of its whole model, so it
is no lower and no higher than the Buggy (1.21 from the middle).

    blender -b --factory-startup --python tools/blender/car3.py [-- <output folder>]

Blender has z up and its OBJ export writes (x, y, z) as (x, z, -y): the car is built with z up, along x, across y.
"""
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from props import ROOT, material, finish, box, tube, export, scene

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = args[0] if args else os.path.join(ROOT, 'content')

# ---- materials: the blue of the car with orange accents, dark and metal parts, and the colours of the Buggy's parts.
# The paint is "primary" (the hood and the body panels) and "secondary" (the cage), which the garage colours (#46)
BLUE = material("primary", (0.02, 0.24, 0.8), metallic=0.35, roughness=0.35)
CAGE = material("secondary", (0.02, 0.24, 0.8), metallic=0.35, roughness=0.35)
FRAME = material("frame", (0.14, 0.14, 0.15), metallic=0.6, roughness=0.45)
ORANGE = material("orange", (1.0, 0.42, 0.0), roughness=0.4)
BLACK = material("black", (0.04, 0.04, 0.045), roughness=0.7)
STEEL = material("steel", (0.4, 0.4, 0.4), metallic=1.0, roughness=0.35)
ALU = material("alu", (0.72694, 0.72694, 0.72694), metallic=1.0, roughness=0.25)
CHROME = material("chrome", (0.8, 0.8, 0.8), metallic=1.0, roughness=0.1)
DARKMETAL = material("darkmetal", (0.27, 0.27, 0.27), metallic=1.0, roughness=0.4)
ENGINE = material("engine", (0.2, 0.2, 0.2), roughness=0.6)
LIGHT = material("light", (0.82018, 0.82018, 0.82018), roughness=0.4)
SEAT = material("seat", (0.0, 0.2, 0.51333), roughness=0.6)
LENS = material("lens", (0.85, 0.87, 0.9), metallic=0.5, roughness=0.15)

# The materials of the Buggy's parts become these
BUGGY = {"default": ALU, "default10": ORANGE, "default19": CHROME, "default2": SEAT, "default20": BLACK, "primary": BLUE,
         "default5": LIGHT, "default6": DARKMETAL, "default7": ENGINE, "default8": STEEL, "default9": ALU}

# The axles pass a little above the pivots, so that nothing of the body is lower than the Buggy's (-1.21): the car
# collides with the outline of its whole model
REAR, FRONT, AXLE = 3.0, -3.6, -1.06
parts, lamps = [], []


def add(obj):
    parts.append(obj)
    return obj


def prism(points, y0, y1, mat, bevel=None, segments=1):
    """A solid from an outline in the x-z plane, from y0 to y1 across."""
    mesh = bpy.data.meshes.new("prism")
    bm = bmesh.new()
    a = [bm.verts.new((x, y0, z)) for x, z in points]
    b = [bm.verts.new((x, y1, z)) for x, z in points]
    bm.faces.new(a[::-1])
    bm.faces.new(b)
    for i in range(len(points)):
        j = (i + 1) % len(points)
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("prism", mesh)
    scene.collection.objects.link(obj)
    return add(finish(obj, mat, bevel, segments))


def fillet(points, radius=0.22, steps=3):
    """The polyline through the points with its corners rounded (a quadratic curve from radius before each corner to
    radius after it)."""
    pts = [Vector(p) for p in points]
    out = [pts[0]]
    for i in range(1, len(pts) - 1):
        a, b, c = pts[i - 1], pts[i], pts[i + 1]
        r = min(radius, (b - a).length / 2, (c - b).length / 2)
        p0 = b + (a - b).normalized() * r
        p2 = b + (c - b).normalized() * r
        for k in range(steps + 1):
            t = k / steps
            out.append((1 - t) ** 2 * p0 + 2 * (1 - t) * t * b + t ** 2 * p2)
    out.append(pts[-1])
    return [tuple(p) for p in out]


def pipe(points, r, mat, radius=0.22, resolution=1):
    """A tube along the points with rounded bends: eight sides by default."""
    return add(tube(fillet(points, radius), r, mat, resolution=resolution))


def mirrored(points):
    return [(x, -y, z) for x, y, z in points]


def both(points, r, mat, radius=0.22):
    pipe(points, r, mat, radius)
    pipe(mirrored(points), r, mat, radius)


def cyl(a, b, r, mat, vertices=12, bevel=None):
    """A cylinder between two points."""
    a, b = Vector(a), Vector(b)
    d = b - a
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=r, depth=d.length, location=(a + b) / 2)
    obj = bpy.context.active_object
    obj.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()
    return add(finish(obj, mat, bevel))


def helix(a, b, r, turns, wire, mat):
    """A coil spring from a to b."""
    a, b = Vector(a), Vector(b)
    axis = (b - a).normalized()
    u = axis.orthogonal().normalized()
    v = axis.cross(u)
    n = int(turns * 8)
    pts = [tuple(a + (b - a) * (k / n) + (u * math.cos(2 * math.pi * turns * k / n) + v * math.sin(2 * math.pi * turns * k / n)) * r)
           for k in range(n + 1)]
    return add(tube(pts, wire, mat, resolution=0))


def blk(centre, size, mat, bevel=0.015, segments=1):
    return add(box(centre, size, mat, bevel, segments))


# ---- the Buggy's parts: content/car.obj split into its loose parts; those wholly inside a region (in Blender
# coordinates: x, y across, z up) are taken, moved by the offset and scaled across by the factor
def buggy_parts():
    before = set(bpy.data.objects)
    bpy.ops.wm.obj_import(filepath=os.path.join(ROOT, 'content', 'car.obj'))
    imported = [o for o in bpy.data.objects if o not in before]
    for o in imported:
        bpy.ops.object.select_all(action='DESELECT')
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    body = next(o for o in imported if o.name.startswith("body"))
    # The Buggy's lamps are not taken: the crawler has its own, with the same names and materials
    for o in imported:
        if o is not body:
            bpy.data.objects.remove(o)
    for name in ("headlight", "lamphousing", "taillight"):
        bpy.data.materials.remove(bpy.data.materials[name])
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active = body
    body.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.separate(type='LOOSE')
    bpy.ops.object.mode_set(mode='OBJECT')
    loose = [o for o in bpy.data.objects if o not in before and o.type == 'MESH' and o.name.startswith("body")]
    for o in loose:
        bpy.ops.object.select_all(action='DESELECT')
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
        bpy.ops.object.material_slot_remove_unused()
        for slot in o.material_slots:
            slot.material = BUGGY.get(slot.material.name.split(".")[0], slot.material)
    return loose


def bounds(o):
    cs = [Vector(c) for c in o.bound_box]
    return Vector((min(c.x for c in cs), min(c.y for c in cs), min(c.z for c in cs))), \
        Vector((max(c.x for c in cs), max(c.y for c in cs), max(c.z for c in cs)))


loose = buggy_parts()
taken = set()


def take(lo, hi, offset=(0, 0, 0), scale_y=1.0, copy=False):
    """The Buggy's loose parts within the box lo..hi, moved by the offset (and scaled across about their middle)."""
    found = []
    for o in loose:
        a, b = bounds(o)
        if all(lo[i] - 1e-4 <= a[i] and b[i] <= hi[i] + 1e-4 for i in range(3)):
            found.append(o)
    if not found:
        raise RuntimeError(f"no part of the Buggy within {lo} {hi}")
    centre_y = (min(bounds(o)[0].y for o in found) + max(bounds(o)[1].y for o in found)) / 2
    result = []
    for o in found:
        if copy:
            o = o.copy()
            o.data = o.data.copy()
            scene.collection.objects.link(o)
        else:
            taken.add(o)
        for v in o.data.vertices:
            v.co.y = centre_y + (v.co.y - centre_y) * scale_y
            v.co += Vector(offset)
        result.append(add(o))
    return result


# The engine with its turbo, carburettor, cooler and fan, where they are in the Buggy (under the hood)
take((-4.05, -0.6, -0.85), (-1.95, 0.6, 0.3))
# Two seats side by side, a little narrower than the Buggy's one
take((-0.4, -0.47, -0.88), (0.81, 0.47, 0.66), offset=(0.05, -0.49, -0.02), scale_y=0.86, copy=True)
take((-0.4, -0.47, -0.88), (0.81, 0.47, 0.66), offset=(0.05, 0.49, -0.02), scale_y=0.86)
# The steering wheel and the pedals in front of the driver's seat (the far one, -y), the gear lever between the seats
take((-1.34, -0.33, -0.47), (-0.27, 0.33, 0.47), offset=(0.0, -0.49, 0.0))
take((-1.3, -0.23, -0.77), (-0.99, 0.23, -0.49), offset=(0.0, -0.49, 0.0))
take((-0.41, 0.42, -0.92), (-0.16, 0.68, -0.29), offset=(0.0, -0.55, 0.05))
# The fuel cell, on the rack behind the seats
take((1.99, -0.56, -0.61), (3.25, 0.56, -0.17), offset=(-0.35, 0.0, -0.1))
for o in loose:
    if o not in taken:
        bpy.data.objects.remove(o)

# ---- the frame: two rails, cross members, a skid plate under the engine and the floor
for s in (-1, 1):
    blk((-0.15, s * 0.72, -0.89), (8.0, 0.13, 0.16), FRAME, bevel=0.02)
for x in (-3.95, -2.0, 1.5, 3.75):
    blk((x, 0, -0.89), (0.12, 1.44, 0.12), FRAME)
prism([(-4.15, -0.86), (-3.75, -1.02), (-1.9, -1.02), (-1.75, -0.95), (-1.75, -0.92), (-4.15, -0.78)], -0.68, 0.68, STEEL, bevel=0.015)
blk((-0.15, 0, -0.95), (3.3, 2.0, 0.05), FRAME, bevel=0.01)

# ---- the axles with their differentials, the coil-overs from the axles up to the frame, orange springs
for x in (REAR, FRONT):
    cyl((x, -1.0, AXLE), (x, 1.0, AXLE), 0.1, STEEL)
    # The differential: a rounded drum on the axle, its cover and the pinion towards the middle of the car
    cyl((x, 0.18 - 0.14, AXLE + 0.06), (x, 0.18 + 0.14, AXLE + 0.06), 0.2, ALU)
    cyl((x, 0.18 - 0.17, AXLE + 0.06), (x, 0.18 + 0.21, AXLE + 0.06), 0.13, DARKMETAL)
    cyl((x + (0.3 if x < 0 else -0.3), 0.18, AXLE + 0.06), (x, 0.18, AXLE + 0.06), 0.07, DARKMETAL)
    for s in (-1, 1):
        # The coil-over leans a little towards the middle of the car
        lean = 0.32 if x < 0 else -0.32
        foot, top = Vector((x + 0.05 * lean, s * 0.82, AXLE + 0.08)), Vector((x + lean, s * 0.6, -0.12))
        d = (top - foot).normalized()
        cyl(foot, foot + d * 0.62, 0.07, DARKMETAL, vertices=10)
        cyl(foot + d * 0.6, top, 0.035, CHROME, vertices=8)
        helix(foot + d * 0.32, top - d * 0.1, 0.1, 4.0, 0.022, ORANGE)
        cyl(top - d * 0.1, top + d * 0.05, 0.12, DARKMETAL, vertices=10)
        cyl(foot + d * 0.25, foot + d * 0.32, 0.12, DARKMETAL, vertices=10)
        # Link arms from the axle to the frame
        cyl((x, s * 0.55, AXLE), (x + (1.2 if x < 0 else -1.2), s * 0.7, -0.92), 0.045, STEEL, vertices=6)

# ---- the hood: blue, with an opening for the turbo and the carburettor, louvred sides and a grille at the front
HOOD = [(-4.08, -0.62), (-4.08, -0.02), (-3.95, 0.07), (-2.05, 0.16), (-2.05, -0.62)]
# The top in three pieces around the opening (x -2.95 .. -2.15, |y| < 0.36), the sides closed below
prism([(-4.08, -0.02), (-3.95, 0.07), (-2.95, 0.115), (-2.95, 0.055), (-4.02, 0.0)], -0.9, 0.9, BLUE, bevel=0.025)
for s in (-1, 1):
    prism([(-2.95, 0.055), (-2.95, 0.115), (-2.05, 0.16), (-2.05, 0.1)], s * 0.36, s * 0.9, BLUE, bevel=0.015)
    # The side of the hood, with three dark louvres
    prism(HOOD, s * 0.9, s * 0.95, BLUE, bevel=0.015)
    for k in range(3):
        x = -3.45 + k * 0.35
        prism([(x, -0.32), (x + 0.22, -0.32), (x + 0.22, -0.06), (x, -0.06)], s * 0.95, s * 0.965, BLACK)
prism([(-2.2, 0.1), (-2.2, 0.16), (-2.05, 0.16), (-2.05, 0.1)], -0.36, 0.36, BLUE, bevel=0.01)
# The grille: a dark frame with vertical bars in front of the cooler
blk((-4.1, 0, -0.32), (0.05, 1.5, 0.08), FRAME)
blk((-4.1, 0, -0.06), (0.05, 1.5, 0.08), FRAME)
for k in range(9):
    blk((-4.1, -0.6 + k * 0.15, -0.19), (0.04, 0.04, 0.22), FRAME, bevel=None)
blk((-4.07, 0, -0.5), (0.05, 1.8, 0.24), BLUE, bevel=0.015)

# ---- the front bumper: a tube with a winch in the middle and orange shackles, the headlights beside the grille
pipe([(-4.0, -1.22, -0.12), (-4.38, -1.15, -0.38), (-4.38, 1.15, -0.38), (-4.0, 1.22, -0.12)], 0.075, FRAME, radius=0.2)
for s in (-1, 1):
    cyl((-4.38, s * 0.65, -0.38), (-4.0, s * 0.65, -0.6), 0.05, FRAME, vertices=10)
blk((-4.42, 0, -0.62), (0.16, 1.1, 0.07), FRAME)
cyl((-4.42, -0.3, -0.5), (-4.42, 0.3, -0.5), 0.1, STEEL, vertices=14)
cyl((-4.42, -0.36, -0.5), (-4.42, -0.3, -0.5), 0.14, FRAME, vertices=14)
cyl((-4.42, 0.3, -0.5), (-4.42, 0.36, -0.5), 0.14, FRAME, vertices=14)
cyl((-4.52, 0, -0.5), (-4.58, 0, -0.5), 0.05, ORANGE, vertices=10)
for s in (-1, 1):
    bpy.ops.mesh.primitive_torus_add(major_radius=0.07, minor_radius=0.022, location=(-4.47, s * 0.9, -0.66), rotation=(0, 0, 0),
                                     major_segments=10, minor_segments=4)
    add(finish(bpy.context.active_object, ORANGE))

# ---- the cage of blue tubes: the side hoops over the seats, the roof, the front tubes along the hood, the rear tubes
# down to the bumper, door bars
R = 0.075
both([(-2.05, 0.95, -0.82), (-2.05, 0.97, 0.18), (-1.1, 1.12, 1.07), (0.95, 1.12, 1.1), (1.55, 1.12, 0.45), (1.55, 1.12, -0.82)], R, CAGE, 0.3)
both([(1.55, 1.12, 0.45), (3.0, 1.0, 0.2), (3.88, 0.95, -0.1), (3.88, 0.95, -0.82)], R, CAGE, 0.25)
both([(-2.05, 0.97, 0.18), (-3.0, 0.93, 0.25), (-4.0, 0.9, -0.05), (-4.05, 0.98, -0.6)], 0.06, CAGE, 0.25)
both([(-2.05, 0.96, -0.3), (-0.6, 1.15, -0.3), (1.55, 1.14, -0.3)], 0.06, CAGE, 0.3)
# The window brace: from the door bar up to the roof tube, both its ends buried in the tubes it joins (y and z are the
# centre lines of the door bar at x -1.4 and of the roof tube at x 0.2)
both([(-1.4, 1.045, -0.3), (0.2, 1.12, 1.089)], 0.05, CAGE)
both([(1.55, 1.1, 0.4), (3.85, 0.93, -0.78)], 0.05, CAGE)
for x, z in ((-1.1, 1.07), (0.95, 1.1), (1.55, 0.45), (-2.05, 0.18)):
    cyl((x, -1.12 if x > -2 else -0.97, z), (x, 1.12 if x > -2 else 0.97, z), R, CAGE)
cyl((3.88, -0.95, -0.12), (3.88, 0.95, -0.12), R, CAGE)
pipe([(-1.1, -1.12, 1.07), (0.95, 1.12, 1.1)], 0.05, CAGE)
pipe([(1.55, -1.1, 0.45), (0.95, 1.1, 1.1)], 0.05, CAGE)
# The roof: a dark plate with ribs, and a light bar on the front roof tube
blk((-0.05, 0, 1.16), (1.9, 2.1, 0.03), FRAME, bevel=0.01)
for k in range(4):
    blk((-0.75 + k * 0.48, 0, 1.185), (0.06, 2.0, 0.02), FRAME, bevel=None)
blk((-1.2, 0, 1.14), (0.14, 1.5, 0.11), BLACK, bevel=0.02)
for k in range(6):
    blk((-1.275, -0.6 + k * 0.24, 1.14), (0.01, 0.2, 0.07), LENS, bevel=None)
# A dashboard behind the cowl, and on the driver's side a console that holds the steering column, the pedals hanging
# under it
prism([(-2.0, -0.62), (-2.0, 0.16), (-1.7, 0.1), (-1.6, -0.15), (-1.75, -0.62)], -0.92, 0.92, FRAME, bevel=0.02)
prism([(-1.7, -0.52), (-1.62, -0.08), (-1.36, -0.06), (-1.2, -0.2), (-1.2, -0.52)], -0.75, -0.23, FRAME, bevel=0.02)

# ---- rock sliders along the sides between the axles
for s in (-1, 1):
    pipe([(-1.85, s * 0.75, -0.82), (-1.7, s * 1.28, -0.8), (1.15, s * 1.28, -0.8), (1.3, s * 0.75, -0.82)], 0.07, FRAME, radius=0.18)
    for x in (-0.9, 0.2):
        cyl((x, s * 0.75, -0.85), (x, s * 1.24, -0.82), 0.04, FRAME, vertices=6)

# ---- the rear: the rack with the spare wheel standing on it across the car under an orange strap, the bumper and the
# taillights
for s in (-1, 1):
    pipe([(1.9, s * 0.62, -0.62), (2.2, s * 0.62, -0.18), (3.75, s * 0.62, -0.18), (3.85, s * 0.62, -0.75)], 0.045, FRAME, radius=0.1)
for x in (2.2, 3.65):
    cyl((x, -0.62, -0.18), (x, 0.62, -0.18), 0.04, FRAME, vertices=10)

SPARE = Vector((2.85, 0, 0.44))


def spare(centre):
    """The Crawler's own wheel (the rock crawler wheel), its axle across the car as on the car, so that the camera sees
    its face; its hubcap orange as the Crawler's are."""
    before = set(bpy.data.objects)
    bpy.ops.wm.obj_import(filepath=os.path.join(ROOT, 'content', 'wheel3.obj'))
    for o in [o for o in bpy.data.objects if o not in before]:
        bpy.ops.object.select_all(action='DESELECT')
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        # The wheel lies at OBJ z -2.17 .. -1.43 (Blender y 1.43 .. 2.17): its middle to the centre
        for v in o.data.vertices:
            v.co.y -= 1.77
        o.data.transform(Matrix.Translation(centre))
        for slot in o.material_slots:
            slot.material = {"rim": ORANGE, "steel": STEEL, "darkmetal": DARKMETAL}.get(slot.material.name.split('.')[0], slot.material)
        # Fewer triangles than the wheel: a spare is seen from far
        mod = o.modifiers.new("decimate", 'DECIMATE')
        mod.ratio = 0.33
        add(o)


spare(SPARE)
# The strap over the tread, from the rack in front of the wheel to the rack behind it
pipe([(SPARE.x + 0.725 * math.cos(math.radians(a)), 0, SPARE.z + 0.725 * math.sin(math.radians(a))) for a in range(212, -33, -16)],
     0.035, ORANGE, radius=0.05)
cyl((3.92, -0.98, -0.42), (3.92, 0.98, -0.42), 0.075, FRAME)
for s in (-1, 1):
    bpy.ops.mesh.primitive_torus_add(major_radius=0.07, minor_radius=0.022, location=(4.0, s * 0.45, -0.55), rotation=(0, math.pi / 2, 0),
                                     major_segments=10, minor_segments=4)
    add(finish(bpy.context.active_object, ORANGE))
    # The rear corners: small blue panels behind the axle
    prism([(3.3, -0.8), (3.95, -0.8), (3.95, -0.28), (3.55, -0.18), (3.3, -0.3)], s * 0.97, s * 0.99, BLUE, bevel=0.01)

# ---- the exhausts: from the engine along the frame to the back, one each side
for s in (-1, 1):
    pipe([(-2.6, s * 0.42, -0.45), (-2.3, s * 0.55, -0.8), (-1.8, s * 0.55, -1.02), (3.4, s * 0.55, -1.02), (3.95, s * 0.52, -0.8)], 0.05, STEEL, radius=0.25)
    cyl((3.8, s * 0.52, -0.8), (4.02, s * 0.52, -0.8), 0.07, CHROME, vertices=8)

# ---- the lamps, each an object of its own: round LED headlights beside the grille, taillights on the rear bumper
HOUSING = material("lamphousing", (0.8, 0.8, 0.8), metallic=1.0, roughness=0.08)
HEADLIGHT = material("headlight", (0.9, 0.9, 0.85), metallic=1.0, roughness=0.12, emission=(1.0, 0.94, 0.8))
TAILLIGHT = material("taillight", (0.55, 0.02, 0.02), roughness=0.1, emission=(1.0, 0.05, 0.03))


def lamp(name, pieces):
    """Joins the pieces (made by the functions above, which add them to the parts) into one lamp object."""
    for o in pieces:
        parts.remove(o)
    bpy.ops.object.select_all(action='DESELECT')
    for o in pieces:
        o.select_set(True)
    bpy.context.view_layer.objects.active = pieces[0]
    for o in pieces:
        bpy.context.view_layer.objects.active = o
        for mod in list(o.modifiers):
            bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.context.view_layer.objects.active = pieces[0]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = name
    obj.data.name = name
    lamps.append(obj)


for side, s in (("Left", -1), ("Right", 1)):
    y = s * 0.82
    lamp("Headlight" + side, [cyl((-4.02, y, -0.2), (-4.16, y, -0.2), 0.14, HOUSING, vertices=12),
                              cyl((-4.16, y, -0.2), (-4.175, y, -0.2), 0.115, HEADLIGHT, vertices=12)])
    y = s * 0.75
    lamp("Taillight" + side, [blk((3.97, y, -0.3), (0.06, 0.22, 0.14), HOUSING, bevel=0.01),
                              blk((4.005, y, -0.3), (0.02, 0.18, 0.1), TAILLIGHT, bevel=None)])

export(parts, lamps, "car3", OUT, {
    "car3.obj": "# The third car, the blue crawler, built by tools/blender/car3.py",
    "car3.mtl": "# Materials of car3 (tools/blender/car3.py); Pm and Pr: metal and roughness (tools/ContentPipeline).\n"
                "# The lenses \"headlight\" and \"taillight\" are the lamps of the car (one object each)",
})
