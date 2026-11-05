"""Blender (background): builds the start gate and the finish gate of the track with their signs and exports them as
content/Levels/mod/sg, g, st2 and st (.obj + .mtl).

A truss gantry as on the race tracks: two lattice legs on base plates, a lattice beam across the road, black corner
nodes. The start gate (sg) carries the start lights on the front of its beam: five columns of two red lamps, each lamp an
object of its own named SignalRed<column><a|b> (a = the upper one) in the material "signalred", which the game lights
column by column (SceneRenderer.StartLights). The sign above it (st2) is a black panel in a lattice frame with the
lettering START, the finish sign (st) the same with a chequered panel and two chequered flags. The gate and its sign are
two blocks, as they always were (the sign stands in the cell above the gate), so no level has to change.

The lettering is that of the Wings model of 2015 (tools/blender/start_letters.json, from art/wings/Levels/mod/st2.wings),
a little larger and with a raised edge; the chequered pattern is made here.

The models face +x (the car comes from there, the start lights are seen from behind it), span the road along y (the legs
stand at |y| = 7, beyond the lane the cars drive in) and are 8.67 high above the feet at z = -4.17, the top of the gate
cell is z = 4.5. Anything of the gate in the lane the cars drive in (|y| up to about 2.2) hangs high above it: the beam
and the lamps are above z = 2.4. The blocks are 9 units wide, and the gate stands 1.2 from the edge of its cell, where the
old gate did (x = -3.3 in the model).

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
NODE = 1.5            # the corner nodes
BEAM_Z = TOP - NODE / 2   # the middle of the beam, the nodes' top is at the top of the cell

# ---- materials: aluminium truss, black nodes and panels, grey plates, the lamps and the colours of the lettering
ALU = material("alu", (0.78, 0.78, 0.8), metallic=0.5, roughness=0.4)
NODE_BLACK = material("node", (0.05, 0.05, 0.055), metallic=0.3, roughness=0.55)
PLATE = material("plate", (0.3, 0.3, 0.32), metallic=0.4, roughness=0.6)
PANEL = material("panel", (0.045, 0.045, 0.05), roughness=0.55)
LETTERS = material("letters", (0.97, 0.97, 0.97), roughness=0.4)
CHECK_WHITE = material("checkwhite", (0.93, 0.93, 0.93), roughness=0.5)
CHECK_BLACK = material("checkblack", (0.04, 0.04, 0.045), roughness=0.5)
POLE = material("pole", (0.6, 0.6, 0.62), metallic=0.8, roughness=0.35)
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


def blk(centre, size, mat, bevel=None):
    return add(box(centre, size, mat, bevel, 1))


def truss(a, b, width, segment=1.5, chord=0.1, brace=0.05, mat=ALU):
    """A square lattice girder from a to b: four chords, a diagonal in each face of every bay and a ring of cross
    members at the ends and at every second node. The tubes have four sides, which is enough from afar."""
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    ref = Vector((1, 0, 0)) if abs(d.x) < 0.9 else Vector((0, 1, 0))
    u = d.cross(ref).normalized() * (width / 2)
    v = d.cross(u).normalized() * (width / 2)
    corners = [u + v, u - v, -u - v, -u + v]
    for c in corners:
        cyl(a + c, b + c, chord, mat, 4)
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
        if i % 2 and i != n:
            continue
        p = a + (b - a) * (i / n)
        for k in range(4):
            cyl(p + corners[k], p + corners[(k + 1) % 4], brace, mat, 4)


def quad_grid(centre_y, centre_z, width, height, columns, rows, x, two_sided=False):
    """A chequered pattern of flat squares facing +x at x (and -x too if asked), one object for each colour."""
    for colour, mat in ((0, CHECK_WHITE), (1, CHECK_BLACK)):
        mesh = bpy.data.meshes.new("check")
        bm = bmesh.new()
        w, h = width / columns, height / rows
        for i in range(columns):
            for j in range(rows):
                if (i + j) % 2 != colour:
                    continue
                y0, z0 = centre_y - width / 2 + i * w, centre_z - height / 2 + j * h
                ring = [(x, y0, z0), (x, y0 + w, z0), (x, y0 + w, z0 + h), (x, y0, z0 + h)]     # looks towards +x
                bm.faces.new([bm.verts.new(p) for p in ring])
                if two_sided:
                    bm.faces.new([bm.verts.new(p) for p in ring[::-1]])
        bm.to_mesh(mesh)
        bm.free()
        obj = bpy.data.objects.new("check", mesh)
        scene.collection.objects.link(obj)
        add(finish(obj, mat))


def lettering(centre_y, centre_z, scale, x, depth):
    """The lettering START of the Wings sign, raised by depth from the panel at x."""
    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "start_letters.json"), encoding="utf-8") as f:
        data = json.load(f)
    mesh = bpy.data.meshes.new("letters")
    bm = bmesh.new()
    verts = [bm.verts.new((x, centre_y + a * scale, centre_z + u * scale)) for a, u in data["vertices"]]
    for face in data["faces"]:
        bm.faces.new([verts[i] for i in face])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if sum(f.normal.x for f in bm.faces) < 0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
    region = bmesh.ops.extrude_face_region(bm, geom=list(bm.faces))
    bmesh.ops.translate(bm, vec=(depth, 0, 0), verts=[g for g in region["geom"] if isinstance(g, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("letters", mesh)
    scene.collection.objects.link(obj)
    return add(finish(obj, LETTERS))


def lamp(name, y, z):
    """A start lamp: a disc on the light panel, an object of its own (see the notes at the top)."""
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=0.4, depth=0.1, location=(1.0, y, z))
    obj = bpy.context.active_object
    obj.rotation_euler = (0, math.pi / 2, 0)
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


def gate(start):
    """The gate: two legs, the beam, the corner nodes and base plates; the start gate has the light panel."""
    for s in (-1, 1):
        truss((0, s * LEG, FEET + 0.3), (0, s * LEG, BEAM_Z), 1.1)
        blk((0, s * LEG, FEET + 0.15), (2.4, 2.4, 0.3), PLATE)
        blk((0, s * LEG, BEAM_Z), (NODE, NODE, NODE), NODE_BLACK)
    truss((0, -LEG, BEAM_Z), (0, LEG, BEAM_Z), 1.1)
    if start:
        # The light panel on the front of the beam, the lamps in the columns 1 to 5 from the left as the driver sees them
        blk((0.75, 0, 3.4), (0.4, 9.8, 2.0), HOUSING)
        for column in range(5):
            y = -3.6 + column * 1.8
            lamp(f"SignalRed{column + 1}a", y, 3.9)
            lamp(f"SignalRed{column + 1}b", y, 2.9)


def sign(start):
    """The sign above the gate (in the coordinates of the gate; the block moves it down into its own cell)."""
    top = TOP + 3.7
    for s in (-1, 1):
        truss((0, s * 5.7, TOP), (0, s * 5.7, top), 0.7, 1.4, 0.07, 0.04)
        blk((0, s * 5.7, top), (0.9, 0.9, 0.9), NODE_BLACK)
    truss((0, -5.7, top), (0, 5.7, top), 0.7, 1.4, 0.07, 0.04)
    centre = TOP + 1.85
    blk((0.05, 0, centre), (0.3, 10.2, 3.0), PANEL)
    if start:
        lettering(0, centre, 1.6, 0.2, 0.12)
    else:
        quad_grid(0, centre, 9.8, 2.6, 24, 6, 0.23)
        # Two chequered flags on the nodes of the gate, leaning inwards, the cloth out
        for s in (-1, 1):
            base = Vector((0, s * LEG, TOP))
            tip = base + Vector((0, -s * 0.6, 2.6))
            cyl(base, tip, 0.05, POLE, 6)
            quad_grid(tip.y + s * 0.75, tip.z - 0.65, 1.2, 0.9, 4, 3, 0.0, two_sided=True)


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
