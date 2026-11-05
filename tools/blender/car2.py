"""Blender (background): builds the second car of the garage, the red Sand Rail, and exports it as content/car2.obj + .mtl.

A single-seater racer on the chassis all cars share: a red tube frame with a low, pointed nose between the front wheels,
the blown engine with its red stacks standing open in front of the cockpit, a teal bubble canopy under three arches, a
swept tail with twin fins carrying the wing, side pipes along the sills to the tail, and a battery box behind the seat.
The engine with its stacks, the radiator, the seat, the steering wheel with the pedals and the battery box are the parts
of the Sand Rail of 2014/15 (tools/blender/car2_parts.obj, cut from the old content/car2.obj; the rest was modelled
again). About 11 000 triangles.

The model units are those of the cars: the front towards -x, y up in the OBJ file, the middle of the body at the origin.
The holders of the wheels (Car.cs) turn about pivots at x = -3.6 (front) and 3.0 (rear), 1.25 below the middle; their
plates sweep |z| 1.01 to 1.47 within about 1.05 of a pivot, the wheels |z| from 1.42 within about 1.6. The body keeps out
of those places and within |z| <= 1.4. The car collides with the outline of its whole model, so it is no lower and no
higher than the Buggy (1.21 from the middle).

    blender -b --factory-startup --python tools/blender/car2.py [-- <output folder>]

Blender has z up and its OBJ export writes (x, y, z) as (x, z, -y): the car is built with z up, along x, across y.
"""
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from props import ROOT, material, finish, box, tube, export, scene

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = args[0] if args else os.path.join(ROOT, 'content')
HERE = os.path.dirname(os.path.abspath(__file__))

# ---- materials: the red of the frame and panels (primary) and the accents (secondary), the dark parts, the metal and the
# colours of the parts of the old Sand Rail
RED = material("primary", (0.9, 0.03, 0.03), metallic=0.35, roughness=0.3)
ACCENT = material("secondary", (0.9, 0.9, 0.92), metallic=0.3, roughness=0.35)
GLASS = material("glass", (0.0, 0.85, 0.8), metallic=0.2, roughness=0.1, alpha=0.32)
FRAME = material("frame", (0.14, 0.14, 0.15), metallic=0.6, roughness=0.45)
BLACK = material("black", (0.04, 0.04, 0.045), roughness=0.7)
STEEL = material("steel", (0.55, 0.55, 0.57), metallic=1.0, roughness=0.3)
CHROME = material("chrome", (0.8, 0.8, 0.8), metallic=1.0, roughness=0.12)
OLD = {"default4": material("default4", (0.2, 0.2, 0.2), roughness=0.6),
       "default5": material("default5", (0.4, 0.4, 0.4), metallic=1.0, roughness=0.35),
       "default6": material("default6", (0.72694, 0.72694, 0.72694), metallic=1.0, roughness=0.15),
       "default7": material("default7", (0.90676, 0.0, 0.0), roughness=0.3)}

REAR, FRONT = 3.0, -3.6
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


def loft(stations, mat, bevel=None, segments=2):
    """A solid through cross sections (x, zb, zt, hb, ht, hm): the station, the bottom and the top, the half width at the
    bottom, at the top and at the belt (at 45 % of the height)."""
    bm = bmesh.new()
    rings = []
    for x, zb, zt, hb, ht, hm in stations:
        zm = zb + (zt - zb) * 0.45
        rings.append([bm.verts.new(p) for p in ((x, -hb, zb), (x, hb, zb), (x, hm, zm), (x, ht, zt), (x, -ht, zt), (x, -hm, zm))])
    for r0, r1 in zip(rings, rings[1:]):
        for i in range(6):
            j = (i + 1) % 6
            bm.faces.new((r0[i], r0[j], r1[j], r1[i]))
    bm.faces.new(rings[0][::-1])
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new("loft")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("loft", mesh)
    scene.collection.objects.link(obj)
    return add(finish(obj, mat, bevel, segments))


def fillet(points, radius=0.22, steps=3):
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
    """A tube along the points with rounded bends."""
    return add(tube(fillet(points, radius), r, mat, resolution=resolution))


def mirrored(points):
    return [(x, -y, z) for x, y, z in points]


def both(points, r, mat, radius=0.22):
    pipe(points, r, mat, radius)
    pipe(mirrored(points), r, mat, radius)


def cyl(a, b, r, mat, vertices=12, r2=None, bevel=None):
    a, b = Vector(a), Vector(b)
    d = b - a
    if r2 is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=r, depth=d.length, location=(a + b) / 2)
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=r, radius2=r2, depth=d.length, location=(a + b) / 2)
    obj = bpy.context.active_object
    obj.rotation_euler = d.to_track_quat('Z', 'X' if abs(d.normalized().y) > 0.9 else 'Y').to_euler()
    return add(finish(obj, mat, bevel))


def blk(centre, size, mat, bevel=0.015, segments=1):
    return add(box(centre, size, mat, bevel, segments))


def old_parts():
    """The parts of the Sand Rail of 2014/15, by their names, in their old places, recoloured."""
    before = set(bpy.data.objects)
    bpy.ops.wm.obj_import(filepath=os.path.join(HERE, 'car2_parts.obj'))
    found = {}
    for o in [o for o in bpy.data.objects if o not in before]:
        bpy.ops.object.select_all(action='DESELECT')
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        for slot in o.material_slots:
            slot.material = OLD.get(slot.material.name.split(".")[0], slot.material)
        found[o.name.split(".")[0]] = o
        add(o)
    return found


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


def rod(a, b, r=0.05):
    """A straight red tube between two points."""
    return pipe([a, b], r, RED, 0.1, False)


# ---- the underframe: two rails, cross members, the axles with their differentials and the floor of the cockpit
def underframe():
    for s in (-1, 1):
        blk((-0.03, s * 0.62, -0.92), (7.5, 0.14, 0.14), RED, 0.02)
    for x in (-2.5, -1.7, -0.2, 1.2, 2.2, 3.3):
        blk((x, 0, -0.92), (0.12, 1.38, 0.12), FRAME, 0.01)
    for x in (REAR, FRONT):
        cyl((x, -1.0, -1.06), (x, 1.0, -1.06), 0.09, STEEL, 10)
        blk((x, 0.0, -1.06), (0.5, 0.45, 0.28), FRAME, 0.05, 2)
        # A coil-over from the axle up to the frame at each side: a white spring on a dark damper
        for s in (-1, 1):
            foot, top = Vector((x + 0.05, s * 0.8, -1.0)), Vector((x + (0.25 if x > 0 else 0.3), s * 0.66, -0.3))
            d = (top - foot).normalized()
            cyl(foot, foot + d * 0.5, 0.06, FRAME, 10)
            cyl(foot + d * 0.45, top, 0.03, CHROME, 8)
            helix(foot + d * 0.3, top - d * 0.08, 0.09, 4.0, 0.02, ACCENT)
            cyl(top - d * 0.1, top, 0.1, FRAME, 10)


# ---- the nose: an open frame of red tubes that tapers to a point between the front wheels, the radiator in a frame, a
# splitter with canards, the cowl across the front of the cockpit
def nose():
    for s in (-1, 1):
        pipe([(-3.4, s * 0.62, -0.92), (-3.9, s * 0.35, -0.9), (-4.32, s * 0.07, -0.84)], 0.075, RED, 0.25, False)
        pipe([(-1.95, s * 0.88, 0.08), (-2.8, s * 0.76, -0.2), (-3.6, s * 0.52, -0.5), (-4.28, s * 0.08, -0.72)], 0.07, RED, 0.35, False)
        for x, zt, y in ((-2.1, 0.0, 0.88), (-2.9, -0.22, 0.75), (-3.55, -0.5, 0.52)):
            rod((x, s * y, -0.92), (x, s * y, zt))
        rod((-2.1, s * 0.88, -0.9), (-2.9, s * 0.75, -0.24), 0.045)
        rod((-2.9, s * 0.75, -0.9), (-3.55, s * 0.52, -0.52), 0.045)
        rod((-3.55, s * 0.52, -0.9), (-4.15, s * 0.1, -0.74), 0.045)
        prism([(-4.05, -1.01), (-3.45, -1.01), (-3.4, -0.78)], s * 0.93, s * 0.97, ACCENT, 0.01)
    # the frame round the radiator, a grille over it
    pipe([(-4.24, -0.46, -0.72), (-4.24, 0.46, -0.72), (-4.24, 0.46, -0.2), (-4.24, -0.46, -0.2), (-4.24, -0.46, -0.72)], 0.04, RED, 0.06, False)
    blk((-4.26, 0, -0.46), (0.04, 0.84, 0.5), BLACK, 0.005)
    for k in range(5):
        blk((-4.29, 0, -0.68 + k * 0.1), (0.03, 0.8, 0.025), STEEL, 0.003)
    blk((-4.0, 0, -1.04), (0.7, 1.6, 0.05), BLACK, 0.015)


# ---- the cabin: two wide pods with sloping shoulders either side of a narrow cockpit, a dash across its front with the
# steering column and the pedal box on it, and a low glass canopy over the cockpit under arches that follow its surface
POD = [(0.6, -1.0), (1.3, -1.0), (1.3, -0.35), (0.8, 0.1), (0.6, 0.1)]                   # the section of a pod: (y, z)
POD_STATIONS = [(-1.95, 0.6, 0.3), (-1.55, 0.92, 0.22), (-0.5, 1.0, 0.2), (0.9, 1.0, 0.2), (1.55, 0.95, 0.22), (2.05, 0.66, 0.36)]


def pods():
    for s in (-1, 1):
        bm = bmesh.new()
        rings = []
        for x, width, drop in POD_STATIONS:
            ring = []
            for y, z in POD:
                y2 = 0.6 + (y - 0.6) * width
                z2 = z - drop * max(0.0, (z + 0.35) / 0.45)
                ring.append(bm.verts.new((x, s * y2, z2)))
            rings.append(ring)
        n = len(POD)
        for r0, r1 in zip(rings, rings[1:]):
            for i in range(n):
                j = (i + 1) % n
                bm.faces.new((r0[i], r0[j], r1[j], r1[i]))
        bm.faces.new(rings[0][::-1])
        bm.faces.new(rings[-1])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        mesh = bpy.data.meshes.new("pod")
        bm.to_mesh(mesh)
        bm.free()
        obj = bpy.data.objects.new("pod", mesh)
        scene.collection.objects.link(obj)
        add(finish(obj, RED, None))
        # the intake on the front of the flank, the vents behind the shoulder, the strip of the secondary colour along the sill
        blk((-1.45, s * 1.02, -0.62), (0.5, 0.05, 0.34), BLACK, 0.01)
        for k in range(3):
            blk((-0.5 + 0.28 * k, s * 1.31, -0.28), (0.12, 0.04, 0.34), BLACK, 0.005)
        blk((0.1, s * 1.31, -0.9), (3.4, 0.04, 0.07), ACCENT, 0.005)
    # the dash between the pods, its face towards the cockpit at x = -1.45
    loft([(-1.95, -0.5, 0.0, 0.6, 0.6, 0.6), (-1.3, -0.5, 0.12, 0.6, 0.57, 0.6)], RED, 0.02, 1)


# The driver's parts of the old car are moved forward by this much (the seat, the steering wheel, the pedals): the seat
# stands on the floor of the cockpit, the steering column and the pedals are held by the dash
DRIVER = -0.45


def driver(found):
    for name, o in found.items():
        if name in ("seat", "steering") or name.startswith("pedal"):
            o.location.x += DRIVER
    # the column goes on into the dash through a collar, the pedals are held by a plate on it
    cyl((-1.17, 0.0, -0.24), (-1.29, 0.0, -0.33), 0.045, STEEL, 8)
    cyl((-1.27, 0.0, -0.31), (-1.21, 0.0, -0.27), 0.085, FRAME, 10)
    blk((-1.24, 0.0, -0.3), (0.06, 0.55, 0.4), FRAME, 0.01)
    # the floor under the seat and the footwell: the seat (bottom at -0.51) stands on it
    blk((-0.1, 0, -0.535), (3.9, 1.2, 0.05), FRAME, 0.01)
    loft([(0.3, -0.5, 0.6, 0.55, 0.28, 0.5), (1.0, -0.5, 0.5, 0.58, 0.3, 0.52), (1.75, -0.5, 0.22, 0.52, 0.3, 0.48)], RED, 0.02, 1)
    for s in (-1, 1):
        blk((-0.25, s * 0.45, -0.49), (0.8, 0.06, 0.04), FRAME, 0.005)       # the rails the seat is bolted to
        blk((0.0, s * 0.62, -0.3), (0.5, 0.03, 0.4), BLACK, 0.005)           # the inner sides of the cockpit by the seat


CANOPY = [(-1.9, 0.02, 0.26, 0.78, 0.74, 0.78), (-0.85, -0.1, 1.0, 0.8, 0.44, 0.78), (0.6, -0.1, 1.07, 0.8, 0.4, 0.78),
          (1.95, 0.0, 0.46, 0.64, 0.4, 0.62)]


def canopy_section(x, grow=0.02):
    """The outline of the canopy at x (from the left foot over the top to the right foot), a little outside the glass."""
    for a, b in zip(CANOPY, CANOPY[1:]):
        if a[0] <= x <= b[0]:
            t = (x - a[0]) / (b[0] - a[0])
            zb, zt, hb, ht, hm = (a[i] + (b[i] - a[i]) * t for i in range(1, 6))
            zm = zb + (zt - zb) * 0.45
            g = grow
            return [(x, -hb - g, zb), (x, -hm - g, zm), (x, -ht - g, zt + g), (x, ht + g, zt + g), (x, hm + g, zm), (x, hb + g, zb)]
    raise ValueError(x)


def canopy():
    loft(CANOPY, GLASS, None, 1)
    for x in (-1.8, -0.85, 0.6, 1.93):
        pipe(canopy_section(x), 0.045, ACCENT, 0.2)
    for s in (-1, 1):
        # the roof edge and the belt, the tubes that hold the glass
        pipe([canopy_section(x)[3 if s > 0 else 2] for x in (-1.89, -0.85, 0.6, 1.93)], 0.045, ACCENT, 0.5)
        pipe([canopy_section(x)[5 if s > 0 else 0] for x in (-1.89, 1.93)], 0.045, ACCENT, 0.1)


# ---- the tail: an open frame behind the roll hoop with the battery box in it, a louvred deck, a diffuser, two swept fins
# with the wing between them
def tail():
    pipe([(1.95, -0.88, -0.15), (1.95, -0.5, 0.52), (1.95, 0.5, 0.52), (1.95, 0.88, -0.15)], 0.08, RED, 0.35)
    for s in (-1, 1):
        pipe([(1.95, s * 0.5, 0.52), (3.0, s * 0.62, 0.56), (3.95, s * 0.7, 0.9)], 0.07, RED, 0.4, False)
        pipe([(2.35, s * 0.62, -0.88), (3.3, s * 0.6, -0.7), (4.15, s * 0.46, -0.45)], 0.07, RED, 0.3, False)
        for x, zl, zt, y in ((2.6, -0.86, 0.55, 0.62), (3.25, -0.72, 0.66, 0.66)):
            rod((x, s * y, zl), (x, s * y, zt))
        rod((1.95, s * 0.5, 0.5), (2.6, s * 0.62, -0.84), 0.045)
        rod((2.6, s * 0.62, 0.52), (3.25, s * 0.6, -0.7), 0.045)
        rod((3.25, s * 0.66, 0.66), (4.12, s * 0.46, -0.45), 0.045)
        prism([(2.4, 0.5), (2.9, 0.62), (3.9, 0.98), (4.28, 1.03), (4.28, 0.7), (3.95, 0.45), (3.0, 0.5)], s * 0.69, s * 0.75, RED, 0.02, 2)
        blk((3.98, s * 1.0, 0.92), (0.66, 0.05, 0.5), RED, 0.02)                    # the endplates of the wing
    loft([(2.1, 0.28, 0.35, 0.62, 0.6, 0.62), (3.0, 0.22, 0.29, 0.66, 0.62, 0.66), (3.9, 0.36, 0.43, 0.52, 0.46, 0.52)], RED, 0.02, 1)
    for k in range(5):
        blk((2.35 + 0.34 * k, 0, 0.43), (0.12, 1.0, 0.03), BLACK, 0.005)
    loft([(3.0, -1.0, -0.85, 0.5, 0.52, 0.52), (3.7, -0.95, -0.7, 0.56, 0.58, 0.58), (4.25, -0.72, -0.5, 0.6, 0.62, 0.62)], BLACK, 0.02, 1)
    blk((3.97, 0, 1.0), (0.66, 2.0, 0.045), ACCENT, 0.02)
    blk((4.12, 0, 1.08), (0.32, 2.0, 0.04), ACCENT, 0.02)


# ---- the exhausts: along the sills to the tail
def exhausts():
    for s in (-1, 1):
        pipe([(-3.2, s * 0.45, -0.5), (-2.7, s * 0.82, -0.72), (-2.0, s * 0.96, -0.82), (1.9, s * 0.98, -0.82), (2.4, s * 0.8, -0.55), (4.1, s * 0.5, -0.38)],
             0.065, STEEL, 0.4, False)
        cyl((0.3, s * 0.98, -0.82), (1.5, s * 0.98, -0.82), 0.14, CHROME, 14)
        cyl((4.1, s * 0.5, -0.38), (4.2, s * 0.5, -0.38), 0.09, CHROME, 12)


def lamp(name, pieces):
    """Joins the pieces (made by the functions above, which add them to the parts) into one lamp object."""
    for o in pieces:
        parts.remove(o)
    bpy.ops.object.select_all(action='DESELECT')
    for o in pieces:
        o.select_set(True)
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


def lights():
    housing = material("lamphousing", (0.8, 0.8, 0.8), metallic=1.0, roughness=0.08)
    headlight = material("headlight", (0.12, 0.12, 0.14), metallic=1.0, roughness=0.08, emission=(0.85, 0.92, 1.0))
    taillight = material("taillight", (0.35, 0.02, 0.03), roughness=0.08, emission=(1.0, 0.03, 0.04))
    for side, s in (("Left", -1), ("Right", 1)):
        lamp("Headlight" + side, [blk((-4.3, s * 0.52, -0.46), (0.06, 0.12, 0.34), housing, 0.01),
                                  blk((-4.335, s * 0.52, -0.46), (0.02, 0.09, 0.3), headlight, None)])
        lamp("Taillight" + side, [blk((4.3, s * 1.0, 0.9), (0.04, 0.08, 0.42), housing, 0.01),
                                  blk((4.325, s * 1.0, 0.9), (0.02, 0.05, 0.38), taillight, None)])


def build():
    found = old_parts()
    # The battery box of the old car, further back, in the tail frame
    found["tank"].location.x += 1.3
    underframe()
    nose()
    pods()
    driver(found)
    canopy()
    tail()
    exhausts()
    lights()


build()

export(parts, lamps, "car2", OUT, {
    "car2.obj": "# The second car, the red Sand Rail, built by tools/blender/car2.py",
    "car2.mtl": "# Materials of car2 (tools/blender/car2.py); Pm and Pr: metal and roughness (tools/ContentPipeline).\n"
                "# The lenses \"headlight\" and \"taillight\" are the lamps of the car (one object each)",
})
