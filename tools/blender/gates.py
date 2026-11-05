"""Blender (background): builds the start gate and the finish gate of the track with their signs and exports them as
content/Levels/mod/sg, g, st2 and st (.obj + .mtl).

A truss gantry as on the race tracks: two lattice legs and a lattice beam across the road, joined by cast aluminium
corners (flanges with bolts, an access hatch, a lifting eye, a knee brace under each), the legs on base plates with
anchor bolts and ribs, wrapped at the foot in a yellow protector, a cable conduit up one leg and along the beam. The start
gate (sg) carries the start lights on the front of its beam: a housing with five columns of two red lamps, each lamp a
dome lens in a bezel under a hood and an object of its own named SignalRed<column><a|b> (a = the upper one) in the
material "signalred", which the game lights column by column (SceneRenderer.StartLights). The finish gate (g) has a
photo-finish camera at the middle of its beam instead. The sign above a gate (st2 START, st chequered) is a black panel in
an aluminium bezel with corner caps, held between two lattice posts under a lattice beam with three flood lights; the
finish sign has two chequered flags on its corners. The gate and its sign are two blocks, as they always were (the sign
stands in the cell above the gate), so no level has to change.

The lettering is that of the Wings model of 2015 (tools/blender/start_letters.json, from art/wings/Levels/mod/st2.wings),
a little larger and with a raised edge; the chequered pattern is made here.

The models face +x (the car comes from there, the start lights are seen from behind it), span the road along y (the legs
stand at |y| = 7, beyond the lane the cars drive in) and are 8.67 high above the feet at z = -4.17, the top of the gate
cell is z = 4.5. Anything of the gate in the lane the cars drive in (|y| up to about 2.2) hangs high above it: the beam,
the lamps and the camera are above z = 2.4. The blocks are 9 units wide, and the gate stands 1.2 from the edge of its cell,
where the old gate did (x = -3.3 in the model).

    blender -b --factory-startup --python tools/blender/gates.py [-- <output folder>]

Blender has z up and its OBJ export writes (x, y, z) as (x, z, -y): the gates are built with z up, along x, across y.
"""
import bpy, bmesh, json, math, os, sys
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from props import ROOT, material, finish, box, export, scene

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = args[0] if args else os.path.join(ROOT, 'content', 'Levels', 'mod')

FEET = -4.17          # the underside of the base plates, as the old gate had its feet
TOP = 4.5             # the top of the gate cell: the sign stands above it, in the cell above
CELL = 9.0            # the height of a cell: the sign blocks are the gate's coordinates moved down by it
XC = -3.3             # where the gate stands in its cell
LEG = 7.0             # |y| of the legs
NODE = 1.4            # the corner castings
BEAM_Z = TOP - NODE / 2   # the middle of the beam, the castings' top is at the top of the cell
PEDESTAL = 1.7        # the height of the yellow pedestal of a leg
MOUNT = FEET + 0.34 + PEDESTAL + 0.14   # where the lattice of a leg starts: on the mounting plate
WIDTH = 1.1           # the section of the legs and the beam
POST = 5.7            # |y| of the posts of the sign
POST_TOP = TOP + 3.7  # the middle of the top beam of the sign
SIGN_Z = TOP + 1.85   # the middle of the panel

# ---- materials: aluminium truss and castings, bolts, grey plates, yellow protectors, black panels, the lamps
ALU = material("alu", (0.78, 0.78, 0.8), metallic=0.5, roughness=0.4)
CAST = material("cast", (0.66, 0.67, 0.7), metallic=0.4, roughness=0.5)
GRAPHITE = material("graphite", (0.14, 0.14, 0.16), metallic=0.5, roughness=0.5)
BOLT = material("bolt", (0.34, 0.34, 0.36), metallic=0.8, roughness=0.35)
PLATE = material("plate", (0.3, 0.3, 0.32), metallic=0.4, roughness=0.6)
PAD = material("pad", (1.0, 0.71, 0.0), roughness=0.6)
PAD_STRIPE = material("padstripe", (0.04, 0.04, 0.045), roughness=0.7)
CABLE = material("cable", (0.03, 0.03, 0.035), roughness=0.7)
PANEL = material("panel", (0.045, 0.045, 0.05), roughness=0.55)
LETTERS = material("letters", (0.97, 0.97, 0.97), roughness=0.4)
CHECK_WHITE = material("checkwhite", (0.93, 0.93, 0.93), roughness=0.5)
CHECK_BLACK = material("checkblack", (0.04, 0.04, 0.045), roughness=0.5)
POLE = material("pole", (0.6, 0.6, 0.62), metallic=0.8, roughness=0.35)
LENS = material("lens", (0.86, 0.88, 0.92), metallic=0.3, roughness=0.15)
# The red start lights: dark red when off, shining in their Ke when the game lights them at the start
SIGNALRED = material("signalred", (0.30666666666666664, 0.0, 0.0), roughness=0.3, emission=(1.0, 0.06, 0.04))
HOUSING = material("housing", (0.03, 0.03, 0.03), metallic=0.25, roughness=0.55)

parts, lamps = [], []


def add(obj):
    parts.append(obj)
    return obj


def cyl(a, b, r, mat, vertices=12, r2=None):
    """A cylinder (a cone with r2) between two points."""
    a, b = Vector(a), Vector(b)
    d = b - a
    if r2 is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=r, depth=d.length, location=(a + b) / 2)
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=r, radius2=r2, depth=d.length, location=(a + b) / 2)
    obj = bpy.context.active_object
    obj.rotation_euler = d.to_track_quat('Z', 'X' if abs(d.normalized().y) > 0.9 else 'Y').to_euler()
    return add(finish(obj, mat))


def blk(centre, size, mat, bevel=None, segments=1):
    return add(box(centre, size, mat, bevel, segments))


def mesh_object(bm, mat, name="part", recalc=True):
    """The bmesh as an object of the model (a closed one with its normals made to look outwards; the faces of an open one
    keep their winding)."""
    if recalc:
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    return add(finish(obj, mat))


def bolt(point, direction, r=0.06, height=0.06, mat=BOLT):
    """A hexagonal bolt head standing on a face."""
    p = Vector(point)
    cyl(p, p + Vector(direction).normalized() * height, r, mat, 6)


def plate(centre, axis, size, thick, mat, bevel=None):
    """A square plate of the given size, thick along the axis (0 x, 1 y, 2 z)."""
    dims = [size, size, size]
    dims[axis] = thick
    return blk(centre, tuple(dims), mat, bevel)


def flange(centre, normal, size=1.3, thick=0.1):
    """A flange plate on a face of a casting (its normal pointing away from the casting) with four bolts on the middles of
    its edges."""
    n = Vector(normal)
    bolt_at = size * 0.42
    axis = 0 if abs(n.x) > 0.5 else 1 if abs(n.y) > 0.5 else 2
    plate(centre, axis, size, thick, ALU, 0.015)
    other = [i for i in range(3) if i != axis]
    base = Vector(centre) + n * (thick / 2)
    for k in other:
        for sign in (-1, 1):
            off = Vector((0, 0, 0))
            off[k] = sign * bolt_at
            bolt(base + off, n, 0.07, 0.07)


def ring(x0, x1, y, z, r_out, r_in, mat, arc=None, steps=12):
    """A tube along x from x0 to x1 with the given outer and inner radius round (y, z); arc = (a0, a1) degrees from +y
    towards +z makes it a part of one (a hood)."""
    bm = bmesh.new()
    full = arc is None
    a0, a1 = (0.0, 360.0) if full else arc
    n = steps if full else max(2, round(steps * (a1 - a0) / 360.0))
    count = n if full else n + 1

    def point(x, r, k):
        a = math.radians(a0 + (a1 - a0) * k / n)
        return bm.verts.new((x, y + r * math.cos(a), z + r * math.sin(a)))

    oa = [point(x0, r_out, k) for k in range(count)]
    ob = [point(x1, r_out, k) for k in range(count)]
    ia = [point(x0, r_in, k) for k in range(count)]
    ib = [point(x1, r_in, k) for k in range(count)]
    for k in range(count if full else count - 1):
        j = (k + 1) % count
        bm.faces.new((oa[k], oa[j], ob[j], ob[k]))
        bm.faces.new((ia[j], ia[k], ib[k], ib[j]))
        bm.faces.new((oa[j], oa[k], ia[k], ia[j]))
        bm.faces.new((ob[k], ob[j], ib[j], ib[k]))
    if not full:
        bm.faces.new((oa[0], ob[0], ib[0], ia[0]))
        bm.faces.new((ob[-1], oa[-1], ia[-1], ib[-1]))
    return mesh_object(bm, mat, "ring")


def gusset(base, direction, run, rise, thick, mat):
    """A triangular rib standing on a plate: along the horizontal direction by run, rising by rise, thick across."""
    base = Vector(base)
    d = Vector((direction[0], direction[1], 0)).normalized()
    side = Vector((-d.y, d.x, 0)) * (thick / 2)
    corners = [base, base + d * run, base + Vector((0, 0, rise))]
    bm = bmesh.new()
    a = [bm.verts.new(c + side) for c in corners]
    b = [bm.verts.new(c - side) for c in corners]
    bm.faces.new(a)
    bm.faces.new(b[::-1])
    for i in range(3):
        j = (i + 1) % 3
        bm.faces.new((a[i], a[j], b[j], b[i]))
    return mesh_object(bm, mat, "gusset")


def truss(a, b, width, segment=1.1, chord=0.11, brace=0.05, collars=True, mat=ALU):
    """A square lattice girder from a to b: four round chords, a diagonal in each face of every bay, a ring of cross members
    and, at every second node, a collar on each chord. The braces have four sides, which is enough from afar."""
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    ref = Vector((1, 0, 0)) if abs(d.x) < 0.9 else Vector((0, 1, 0))
    u = d.cross(ref).normalized() * (width / 2)
    v = d.cross(u).normalized() * (width / 2)
    corners = [u + v, u - v, -u - v, -u + v]
    for c in corners:
        cyl(a + c, b + c, chord, mat, 8)
    n = max(1, round((b - a).length / segment))
    for i in range(n):
        p0, p1 = a + (b - a) * (i / n), a + (b - a) * ((i + 1) / n)
        for k in range(4):
            c0, c1 = corners[k], corners[(k + 1) % 4]
            if (i + k) % 2 == 0:
                cyl(p0 + c0, p1 + c1, brace, mat, 4)
            else:
                cyl(p0 + c1, p1 + c0, brace, mat, 4)
    for i in range(n + 1):
        p = a + (b - a) * (i / n)
        for k in range(4):
            cyl(p + corners[k], p + corners[(k + 1) % 4], brace, mat, 4)
        if collars and i % 2 == 0:
            for c in corners:
                cyl(p + c - d * 0.06, p + c + d * 0.06, chord * 1.5, CAST, 8)


def casting(centre, size, eye=True, hatch=1):
    """A corner casting of aluminium with the faces a lattice joins (down and towards the middle of the gate) flanged and
    bolted, an access hatch on the outer face, plates on the front and the back and a lifting eye on the top. hatch is the
    side the outer face looks to (+1 or -1 along y), 0 for a casting without one."""
    cx, cy, cz = centre
    h = size / 2
    blk(centre, (size, size, size), CAST, 0.1, 2)
    flange((cx, cy, cz - h - 0.05), (0, 0, -1), size * 0.93)
    if hatch:
        flange((cx, cy - hatch * (h + 0.05), cz), (0, -hatch, 0), size * 0.93)
        disc_centre = Vector((cx, cy + hatch * (h + 0.07), cz))     # the face of the hatch, 0.07 off the casting
        cyl(Vector((cx, cy + hatch * (h - 0.02), cz)), disc_centre, size * 0.3, ALU, 14)
        for k in range(6):
            a = math.tau * k / 6
            bolt(disc_centre + Vector((math.cos(a), 0, math.sin(a))) * size * 0.22, (0, hatch, 0), 0.04, 0.05)
    for sign in (-1, 1):
        face = Vector((sign * (h + 0.02), cy, cz))
        plate(face, 0, size * 0.62, 0.05, GRAPHITE, 0.01)
        for a in (-1, 1):
            for b in (-1, 1):
                bolt(face + Vector((0, a * size * 0.22, b * size * 0.22)), (sign, 0, 0), 0.045, 0.05)
    if eye:
        cyl((cx, cy, cz + h), (cx, cy, cz + h + 0.12), size * 0.16, BOLT, 10)
        bpy.ops.mesh.primitive_torus_add(major_radius=size * 0.16, minor_radius=size * 0.035, major_segments=12, minor_segments=6,
                                         location=(cx, cy, cz + h + 0.12 + size * 0.16), rotation=(0, math.pi / 2, 0))
        add(finish(bpy.context.active_object, BOLT))


def lamp(name, y, z):
    """A start lamp: a dome lens, an object of its own (see the notes at the top)."""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=6, radius=0.36, location=(1.0, y, z))
    obj = bpy.context.active_object
    obj.scale = (0.3, 1.0, 1.0)
    finish(obj, SIGNALRED)
    obj.name = name
    obj.data.name = name
    lamps.append(obj)
    return obj


def reset():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for mesh in [m for m in bpy.data.meshes if m.users == 0]:
        bpy.data.meshes.remove(mesh)
    parts.clear()
    lamps.clear()


def foot(s):
    """The foot of a leg: a base plate with anchor bolts, a yellow pedestal with black straps on it, and on top of that a
    steel mounting plate with a sleeve and a rib at each chord of the leg, which stands on it."""
    y = s * LEG
    blk((0, y, FEET + 0.17), (2.7, 2.7, 0.34), PLATE, 0.05, 2)
    for dx, dy in ((1.1, 0), (-1.1, 0), (0, 1.1), (0, -1.1)):
        p = Vector((dx, y + dy, FEET + 0.34))
        cyl(p, p + Vector((0, 0, 0.32)), 0.07, BOLT, 8)                            # the stud
        cyl(p + Vector((0, 0, 0.08)), p + Vector((0, 0, 0.26)), 0.16, BOLT, 6)      # the nut
        cyl(p, p + Vector((0, 0, 0.05)), 0.24, BOLT, 10)                           # the washer
    z0 = FEET + 0.34
    blk((0, y, z0 + PEDESTAL / 2), (1.75, 1.75, PEDESTAL), PAD, 0.16, 3)
    for z in (z0 + 0.35, z0 + 0.85, z0 + 1.35):
        blk((0, y, z), (1.79, 1.79, 0.1), PAD_STRIPE, 0.04, 2)
    top = z0 + PEDESTAL
    blk((0, y, top + 0.07), (1.4, 1.4, 0.14), CAST, 0.02, 2)
    for dx, dy in ((0.62, 0), (-0.62, 0), (0, 0.62), (0, -0.62)):
        bolt((dx, y + dy, top + 0.14), (0, 0, 1), 0.07, 0.07)
    for a in (-1, 1):
        for b in (-1, 1):
            p = Vector((a * WIDTH / 2, y + b * WIDTH / 2, top + 0.14))
            cyl(p, p + Vector((0, 0, 0.32)), 0.19, CAST, 8)                         # the sleeve the chord comes out of
            gusset(Vector((a * 0.64, y + b * 0.64, top + 0.14)), (a, b), 0.24, 0.55, 0.06, CAST)


def conduit(s):
    """A cable conduit: a junction box at the foot of a leg, up its back outer chord and along the back of the beam to the
    middle, held by clips."""
    y = s * (LEG + 0.55)
    blk((-0.92, y, MOUNT + 0.5), (0.4, 0.55, 0.6), GRAPHITE, 0.03)
    x, top = -0.83, BEAM_Z - WIDTH / 2
    r = 0.07
    cyl((x, y, MOUNT + 0.75), (x, y, top), r, CABLE, 8)
    cyl((x, y, top), (x, 0.0, top), r, CABLE, 8)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=5, radius=r * 1.15, location=(x, y, top))
    add(finish(bpy.context.active_object, CABLE))
    for z in (-0.5, 1.5, 2.5):                  # between the collars of the chord (at its nodes), not in their planes
        blk((x + 0.14, y, z), (0.3, 0.18, 0.1), GRAPHITE, 0.01)
    for yy in (s * 5.2, s * 3.0, s * 0.8):
        blk((x + 0.14, yy, top), (0.3, 0.12, 0.2), GRAPHITE, 0.01)


def knee(s):
    """The braces under a corner of the gantry, in the front and the back plane: from the inner chord of the leg up to the
    lower chord of the beam."""
    for x in (-WIDTH / 2, WIDTH / 2):
        a = Vector((x, s * (LEG - WIDTH / 2), BEAM_Z - NODE / 2 - 1.5))
        b = Vector((x, s * (LEG - NODE / 2 - 1.2), BEAM_Z - WIDTH / 2))
        cyl(a, b, 0.09, ALU, 8)
        for p in (a, b):
            cyl(p - (b - a).normalized() * 0.08, p + (b - a).normalized() * 0.08, 0.15, CAST, 8)


def gate(start):
    """The gate: two legs with their feet, the beam, the four corner castings with their braces, the conduit and, on the
    front of the beam, the light housing of the start gate or the photo-finish camera of the finish gate."""
    low = BEAM_Z - NODE / 2
    for s in (-1, 1):
        truss((0, s * LEG, MOUNT), (0, s * LEG, low - 0.1), WIDTH)
        foot(s)
        casting((0, s * LEG, BEAM_Z), NODE, True, s)
        knee(s)
    truss((0, -(LEG - NODE / 2 - 0.1), BEAM_Z), (0, LEG - NODE / 2 - 0.1, BEAM_Z), WIDTH, 1.1)
    conduit(1)
    if start:
        # The housing on the front of the beam in front of the front chords, held by straps, and the lamps in the columns
        # 1 to 5 from the left as the driver sees them, each in a bezel under a hood
        blk((0.75, 0, 3.4), (0.4, 9.8, 2.0), HOUSING, 0.1, 2)
        for y in (-4.5, -2.7, -0.9, 0.9, 2.7, 4.5):
            blk((0.97, y, 3.4), (0.06, 0.32, 2.0), ALU, 0.01)
            for z in (2.6, 4.2):
                bolt((1.0, y, z), (1, 0, 0), 0.06, 0.05)
        for column in range(5):
            y = -3.6 + column * 1.8
            for row, z in (("a", 3.95), ("b", 2.95)):
                lamp(f"SignalRed{column + 1}{row}", y, z)
                ring(0.95, 1.07, y, z, 0.45, 0.37, GRAPHITE, None, 12)
                ring(1.0, 1.4, y, z, 0.5, 0.45, GRAPHITE, (-5, 185), 12)
    else:
        # The photo-finish camera under the middle of the beam: a housing on an arm with a lens barrel looking at the cars
        blk((0.3, 0, 2.85), (0.9, 0.7, 0.55), GRAPHITE, 0.08, 2)
        cyl((0.75, 0, 2.85), (1.2, 0, 2.85), 0.2, HOUSING, 12)
        cyl((1.2, 0, 2.85), (1.24, 0, 2.85), 0.16, LENS, 12)
        ring(1.15, 1.35, 0, 2.85, 0.27, 0.22, GRAPHITE, None, 12)
        blk((0.3, 0, 3.17), (0.5, 0.5, 0.12), ALU, 0.01)


def chequered(centre_y, centre_z, width, height, columns, rows, x, wave=0.0, two_sided=False, side=1):
    """A chequered pattern of squares facing +x at x (and -x too if asked), one object for each colour. With a wave the
    squares are cut into quarters and the cloth rises and falls across, more and more away from its pole at the start
    (the pattern runs along y by side)."""
    sub = 2 if wave else 1
    for colour, mat in ((0, CHECK_WHITE), (1, CHECK_BLACK)):
        bm = bmesh.new()
        grid = {}

        def vert(i, j, back):
            key = (i, j, back)
            if key not in grid:
                t = i / (columns * sub)
                dx = wave * t * math.sin(t * 2.4 * math.pi + 0.4 * j / sub) if wave else 0.0
                grid[key] = bm.verts.new((x + dx + (0.02 if back else 0.0), centre_y + side * (-width / 2 + width * t),
                                          centre_z - height / 2 + height * j / (rows * sub)))
            return grid[key]

        for i in range(columns * sub):
            for j in range(rows * sub):
                if (i // sub + j // sub) % 2 != colour:
                    continue
                quad = [vert(i, j, False), vert(i + 1, j, False), vert(i + 1, j + 1, False), vert(i, j + 1, False)]
                bm.faces.new(quad if side > 0 else quad[::-1])
                if two_sided:
                    back = [vert(i, j, True), vert(i + 1, j, True), vert(i + 1, j + 1, True), vert(i, j + 1, True)]
                    bm.faces.new(back[::-1] if side > 0 else back)
        mesh_object(bm, mat, "check", False)


def lettering(centre_y, centre_z, scale, x, depth):
    """The lettering START of the Wings sign, raised by depth from the panel at x."""
    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "start_letters.json"), encoding="utf-8") as f:
        data = json.load(f)
    bm = bmesh.new()
    verts = [bm.verts.new((x, centre_y + a * scale, centre_z + u * scale)) for a, u in data["vertices"]]
    for face in data["faces"]:
        bm.faces.new([verts[i] for i in face])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if sum(f.normal.x for f in bm.faces) < 0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
    region = bmesh.ops.extrude_face_region(bm, geom=list(bm.faces))
    bmesh.ops.translate(bm, vec=(depth, 0, 0), verts=[g for g in region["geom"] if isinstance(g, bmesh.types.BMVert)])
    return mesh_object(bm, LETTERS, "letters")


def flood(y):
    """A flood light on the top beam of the sign, on an arm, looking down at the panel."""
    p0 = Vector((0.85, y, POST_TOP + 0.3))
    d = Vector((-0.6, 0, -1.65)).normalized()
    cyl(Vector((0.3, y, POST_TOP + 0.35)), p0, 0.05, ALU, 6)
    cyl(p0, p0 + d * 0.6, 0.21, GRAPHITE, 10, r2=0.17)
    cyl(p0 + d * 0.6, p0 + d * 0.64, 0.16, LENS, 10)
    cyl(p0 + d * 0.05, p0 + d * 0.15, 0.24, ALU, 10)


def sign(start):
    """The sign above the gate (in the coordinates of the gate; the block moves it down into its own cell): two lattice posts
    on flanges, a lattice beam on top between two castings, the black panel in an aluminium bezel with corner caps, held by
    clamps and rails, three flood lights; the finish sign has two chequered flags."""
    h = 0.95
    for s in (-1, 1):
        casting((0, s * POST, POST_TOP), h, eye=start, hatch=s)
        plate((0, s * POST, TOP + 0.05), 2, 0.95, 0.1, ALU, 0.015)
        for a in (-1, 1):
            for b in (-1, 1):
                bolt((a * 0.38, s * POST + b * 0.38, TOP + 0.1), (0, 0, 1), 0.05, 0.05)
        truss((0, s * POST, TOP + 0.1), (0, s * POST, POST_TOP - h / 2 - 0.1), 0.7, 1.3, 0.07, 0.04)
    truss((0, -(POST - h / 2 - 0.1), POST_TOP), (0, POST - h / 2 - 0.1, POST_TOP), 0.7, 1.3, 0.07, 0.04)
    # The panel in its bezel with a cap on each corner, and rails and clamps behind it
    blk((0.05, 0, SIGN_Z), (0.3, 10.2, 3.0), PANEL, 0.03)
    for z in (SIGN_Z + 1.4, SIGN_Z - 1.4):
        blk((0.24, 0, z), (0.14, 10.2, 0.22), ALU, 0.02)
    for y in (-4.99, 4.99):
        blk((0.24, y, SIGN_Z), (0.14, 0.22, 3.0), ALU, 0.02)
    for y in (-5.0, 5.0):
        for z in (SIGN_Z - 1.4, SIGN_Z + 1.4):
            blk((0.3, y, z), (0.26, 0.42, 0.42), GRAPHITE, 0.05, 2)
            bolt((0.43, y, z), (1, 0, 0), 0.06, 0.05)
    for z in (-1.0, 0.0, 1.0):
        cyl((-0.2, -5.0, SIGN_Z + z), (-0.2, 5.0, SIGN_Z + z), 0.06, ALU, 8)
    for s in (-1, 1):
        for z in (-1.0, 1.0):
            blk((-0.1, s * 5.2, SIGN_Z + z), (0.5, 0.45, 0.3), GRAPHITE, 0.03)
            bolt((0.16, s * 5.2, SIGN_Z + z), (1, 0, 0), 0.05, 0.04)
    for y in (-3.0, 0.0, 3.0):
        flood(y)
    if start:
        lettering(0, SIGN_Z, 1.6, 0.2, 0.12)
    else:
        chequered(0, SIGN_Z, 9.6, 2.5, 24, 6, 0.23)
        # Two chequered flags on the corners of the sign: a socket, a pole leaning outwards with a ball on its end, the
        # cloth waving out from it
        for s in (-1, 1):
            base = Vector((0, s * POST, POST_TOP + h / 2))
            cyl(base, base + Vector((0, 0, 0.3)), 0.14, BOLT, 8)
            tip = base + Vector((0, s * 0.8, 2.7))
            cyl(base + Vector((0, 0, 0.1)), tip, 0.05, POLE, 8)
            bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=5, radius=0.1, location=tip)
            add(finish(bpy.context.active_object, POLE))
            chequered(tip.y + s * 0.8, tip.z - 0.7, 1.6, 1.1, 4, 3, 0.0, wave=0.14, two_sided=True, side=s)


def build(name, builder, start, dz, headers):
    reset()
    builder(start)
    for obj in parts + lamps:
        obj.location += Vector((XC, 0, dz))
    return export(parts, lamps, name, OUT, headers)


NOTE = "Pm and Pr: metal and roughness (tools/ContentPipeline)"
build("sg", gate, True, 0.0, {
    "sg.obj": "# The start gate, built by tools/blender/gates.py (the Wings model of 2015 is art/wings/Levels/mod/sg.wings)",
    "sg.mtl": f"# Materials of the start gate (tools/blender/gates.py); {NOTE}.\n"
              "# The red lamps (signalred, the objects SignalRed1a to 5b) shine in their Ke when the game lights them at the start\n"
              "# (SceneRenderer.StartLights), column by column from the left as the driver sees them"})
build("g", gate, False, 0.0, {
    "g.obj": "# The finish gate, built by tools/blender/gates.py (the Wings model of 2015 is art/wings/Levels/mod/g.wings)",
    "g.mtl": f"# Materials of the finish gate (tools/blender/gates.py); {NOTE}"})
build("st2", sign, True, -CELL, {
    "st2.obj": "# The sign START above the start gate, built by tools/blender/gates.py (the Wings model of 2015 is art/wings/Levels/mod/st2.wings)",
    "st2.mtl": f"# Materials of the sign START (tools/blender/gates.py); {NOTE}"})
build("st", sign, False, -CELL, {
    "st.obj": "# The chequered sign above the finish gate, built by tools/blender/gates.py (the Wings model of 2015 is art/wings/Levels/mod/st.wings)",
    "st.mtl": f"# Materials of the chequered sign (tools/blender/gates.py); {NOTE}"})
