"""Blender (background): builds the judges' stand of the scenery and exports it as content/Scenery/stand.obj + stand.mtl.

A modern race control building in the colours of the track blocks and of the street lamp and the fence: a light grey
ground floor on a dark grey base with a glazed entrance in the middle, a dark grey terrace slab over it with yellow edges,
out over a porch on four columns, with a glass balustrade towards the road; on the terrace a control room whose glazed
front leans out like that of an airfield tower, under a dark grey canopy with a yellow line, the air conditioning and a
radio mast on the roof; an open steel stair with yellow nosings up the side to the terrace (at -x of the model, on the
right as seen from the road). The windows are "windowlit": light blue glass by day, lit warm from within at night
(their Ke, Graphics/SceneRenderer); the balustrade is see-through "glass".

The terrace faces the camera (-z of the model), as the props face the road from behind it. The footprint is 25.7 along
x and 12.9 along z, as the stand of 2018 had, the roof is at about 10 and the mast reaches 12; the foot is at the middle
of the footprint, at the origin. Model units at the scale of the blocks (a cell is 9 units, the car 2.4 high).

    blender -b --factory-startup --python tools/blender/stand.py [-- <output folder>]

Blender has z up and its OBJ export writes (x, y, z) as (x, z, -y): the stand is built with z up and its front towards +y.
"""
import bpy, bmesh, math, os, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from props import ROOT, POLE, HEAD, ACCENT, scene, material, finish, box, cylinder, tube, export

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = args[0] if args else os.path.join(ROOT, 'content', 'Scenery')

# The windows: opaque light blue glass, smooth, so that it mirrors the sky by day, lit warm from within at night
WINDOW = material("windowlit", (0.50, 0.72, 0.88), metallic=0.3, roughness=0.12, emission=(0.5, 0.34, 0.15))
GLASS = material("glass", (0.70, 0.88, 0.97), roughness=0.05, alpha=0.3)

PLINTH = 0.3                       # the top of the base
FLOOR1 = 4.9                       # the underside and the top of the terrace slab
TERRACE = 5.35
ROOF = 9.5                         # the underside of the roof
MIDDLE = 1.9                       # the middle of the building along x (the stair is beside it, at -x)

parts = []


def block(x0, x1, y0, y1, z0, z1, mat, bevel=None):
    """A box between the coordinates given, one of the parts."""
    obj = box(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), (x1 - x0, y1 - y0, z1 - z0), mat, bevel)
    parts.append(obj)
    return obj


def plate(x0, x1, p0, p1, thickness, mat, offset=0.0):
    """A plate from x0 to x1 between the points p0 and p1 of the y-z plane, its middle moved by offset along its normal
    (out of the front of the stand when it leans back, out of the top when it lies flat), one of the parts."""
    (y0, z0), (y1, z1) = p0, p1
    length = math.hypot(y1 - y0, z1 - z0)
    ny, nz = (z1 - z0) / length, -(y1 - y0) / length
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0))
    obj = bpy.context.active_object
    obj.scale = (x1 - x0, length, thickness)
    bpy.ops.object.transform_apply(scale=True)
    obj.rotation_euler = (math.atan2(z1 - z0, y1 - y0), 0, 0)
    obj.location = ((x0 + x1) / 2, (y0 + y1) / 2 + ny * offset, (z0 + z1) / 2 + nz * offset)
    bpy.ops.object.transform_apply(location=True, rotation=True)
    parts.append(finish(obj, mat))
    return obj


def prism(profile, x0, x1, mat, bevel=None):
    """A solid from x0 to x1 whose section in the y-z plane is the polygon of the points, one of the parts."""
    mesh = bpy.data.meshes.new("prism")
    bm = bmesh.new()
    left = [bm.verts.new((x0, y, z)) for y, z in profile]
    right = [bm.verts.new((x1, y, z)) for y, z in profile]
    bm.faces.new(list(reversed(left)))
    bm.faces.new(right)
    for i in range(len(profile)):
        j = (i + 1) % len(profile)
        bm.faces.new((left[i], left[j], right[j], right[i]))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("prism", mesh)
    scene.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    parts.append(finish(obj, mat, bevel))
    return obj


def windows(axis, wall, out, a0, a1, z0, z1, count, frame=0.12, depth=0.13):
    """A row of count windows on a wall: along x on the wall at y = wall (axis "x") or along y on the wall at x = wall
    (axis "y"), facing out (+1 or -1), from a0 to a1 and from height z0 to z1, in a dark grey frame with mullions between
    the panes."""
    def piece(b0, b1, c0, c1, z_0, z_1, mat):
        # b along the wall, c out of it
        if axis == "x":
            return block(b0, b1, *sorted((wall + out * c0, wall + out * c1)), z_0, z_1, mat)
        return block(*sorted((wall + out * c0, wall + out * c1)), b0, b1, z_0, z_1, mat)

    piece(a0 - frame, a1 + frame, -0.02, depth, z0 - frame, z0, POLE)          # the sill
    piece(a0 - frame, a1 + frame, -0.02, depth, z1, z1 + frame, POLE)          # the head
    width = (a1 - a0) / count
    for i in range(count + 1):
        b = a0 + i * width
        w = frame if i in (0, count) else frame * 0.6
        lo, hi = (b - w, b) if i == 0 else (b, b + w) if i == count else (b - w / 2, b + w / 2)
        piece(lo, hi, -0.02, depth, z0, z1, POLE)                               # the jambs and the mullions
    for i in range(count):
        piece(a0 + i * width, a0 + (i + 1) * width, -0.02, 0.04, z0, z1, WINDOW)


def door(axis, wall, out, a0, a1, z0, z1):
    """A glazed double door from z0 up to z1, in a dark grey frame."""
    windows(axis, wall, out, a0, a1, z0 + 0.12, z1, 2)


def balustrade(points, z, height=1.05):
    """A glass balustrade along the corners (x, y) of a path at the height z: dark grey posts, see-through glass panels
    between them and a light grey handrail on top."""
    for (x0, y0), (x1, y1) in zip(points, points[1:]):
        length = math.hypot(x1 - x0, y1 - y0)
        count = max(1, math.ceil(length / 2.2))
        along_x = abs(y1 - y0) < 1e-6
        for i in range(count + 1):
            t = i / count
            x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            block(x - 0.04, x + 0.04, y - 0.04, y + 0.04, z, z + height, POLE)
        for i in range(count):
            t0, t1 = i / count, (i + 1) / count
            if along_x:
                xa, xb = sorted((x0 + (x1 - x0) * t0, x0 + (x1 - x0) * t1))
                block(xa + 0.06, xb - 0.06, y0 - 0.015, y0 + 0.015, z + 0.1, z + height - 0.1, GLASS)
            else:
                ya, yb = sorted((y0 + (y1 - y0) * t0, y0 + (y1 - y0) * t1))
                block(x0 - 0.015, x0 + 0.015, ya + 0.06, yb - 0.06, z + 0.1, z + height - 0.1, GLASS)
        if along_x:
            xa, xb = sorted((x0, x1))
            block(xa - 0.06, xb + 0.06, y0 - 0.06, y0 + 0.06, z + height, z + height + 0.08, HEAD)
        else:
            ya, yb = sorted((y0, y1))
            block(x0 - 0.06, x0 + 0.06, ya - 0.06, yb + 0.06, z + height, z + height + 0.08, HEAD)


def stair(x0, x1, front, back):
    """An open steel stair from the base at y = front up to the terrace at y = back, between x0 and x1: dark grey treads
    with yellow nosings between two stringers, a handrail with posts on both sides."""
    steps = 17
    rise = (TERRACE - PLINTH) / steps
    run = (front - back) / steps
    for i in range(steps - 1):
        top = PLINTH + (i + 1) * rise
        edge = front - i * run
        block(x0 + 0.16, x1 - 0.16, edge - run - 0.05, edge, top - 0.07, top, POLE)
        block(x0 + 0.2, x1 - 0.2, edge - 0.09, edge, top, top + 0.012, ACCENT)
    for x in (x0, x1 - 0.16):
        plate(x, x + 0.16, (front + 0.25, PLINTH), (back, TERRACE), 0.4, POLE, offset=-0.12)
    for x in (x0 + 0.08, x1 - 0.08):
        parts.append(tube([(x, front, PLINTH + 1.0), (x, back, TERRACE + 1.0), (x, back - 0.6, TERRACE + 1.0)], 0.045, HEAD,
                          resolution=2, name="handrail"))
        for i in range(0, steps, 4):
            y = front - (i + 0.5) * run
            z = PLINTH + (i + 1) * rise
            block(x - 0.035, x + 0.035, y - 0.035, y + 0.035, z, z + 1.0 - 0.02, POLE)


def mast(x, y, z):
    """A radio mast on the roof: a base, a tapered pole, two cross arms with antennas and a yellow tip at 12."""
    block(x - 0.3, x + 0.3, y - 0.3, y + 0.3, z, z + 0.12, POLE)
    parts.append(cylinder(0.09, 0.05, z + 0.12, 11.9, POLE, vertices=12, location=(x, y)))
    for h, w in ((10.9, 0.65), (11.45, 0.45)):
        block(x - w, x + w, y - 0.03, y + 0.03, h - 0.03, h + 0.03, POLE)
        for s in (-1, 1):
            parts.append(cylinder(0.018, 0.012, h, h + 0.45, POLE, vertices=8, location=(x + s * w, y)))
    parts.append(cylinder(0.06, 0.06, 11.9, 12.0, ACCENT, vertices=12, location=(x, y)))


# ---- the base and the ground floor: a light grey box on a dark grey skirting, the glazed entrance in the middle of its
# front between two rows of windows, windows all round
block(-12.6, 12.6, -6.2, 6.2, 0.0, PLINTH, POLE, bevel=0.04)
block(-8.4, 12.2, -5.9, 3.4, PLINTH, FLOOR1, HEAD, bevel=0.05)
block(-8.45, 12.25, -5.95, 3.45, PLINTH, PLINTH + 0.45, POLE)
door("x", 3.4, 1, MIDDLE - 0.85, MIDDLE + 0.85, PLINTH, 3.5)
windows("x", 3.4, 1, MIDDLE - 9.6, MIDDLE - 1.4, 1.5, 4.1, 3)
windows("x", 3.4, 1, MIDDLE + 1.4, MIDDLE + 9.6, 1.5, 4.1, 3)
windows("x", -5.9, -1, -7.2, 11.0, 1.5, 4.1, 6)
windows("y", 12.2, 1, -4.8, 2.2, 1.5, 4.1, 3)
windows("y", -8.4, -1, -4.8, 2.2, 1.5, 4.1, 3)

# ---- the terrace: a dark grey slab over the ground floor, out over a porch on four columns, yellow along its edges
block(-8.8, 12.6, -6.3, 6.2, FLOOR1, TERRACE, POLE, bevel=0.04)
block(-8.8, 12.64, 6.2, 6.24, FLOOR1 + 0.12, FLOOR1 + 0.3, ACCENT)
block(12.6, 12.64, -6.3, 6.2, FLOOR1 + 0.12, FLOOR1 + 0.3, ACCENT)
for x in (-8.3, MIDDLE - 5.1, MIDDLE + 5.1, 12.1):
    block(x - 0.18, x + 0.18, 5.45, 5.81, PLINTH, FLOOR1, POLE, bevel=0.02)
balustrade([(-8.75, -3.7), (-8.75, 6.15), (12.55, 6.15), (12.55, -6.25), (-12.55, -6.25), (-12.55, -3.75)], TERRACE)

# ---- the stair up the side (-x), from the front to a landing at the back, which joins the terrace
stair(-12.5, -9.3, 5.9, -3.7)
block(-12.6, -8.8, -6.3, -3.7, FLOOR1, TERRACE, POLE, bevel=0.03)
for y in (-6.0, -4.0):
    block(-12.45, -12.1, y - 0.17, y + 0.17, PLINTH, FLOOR1, POLE)

# ---- the control room over the middle: light grey, its front glazed and leaning out under the roof, a door to the
# terrace on the side of the stair
LEFT, RIGHT = MIDDLE - 7.9, MIDDLE + 7.9
bottom, top = (2.5, 6.0), (3.15, ROOF)
prism([(-5.6, TERRACE), (2.5, TERRACE), bottom, top, (-5.6, ROOF)], LEFT, RIGHT, HEAD, bevel=0.03)
block(LEFT - 0.05, RIGHT + 0.05, 2.35, 2.75, 5.85, 6.05, POLE)
panes = 6
width = (RIGHT - LEFT - 0.5) / panes
for i in range(panes + 1):
    x = LEFT + 0.25 + i * width
    plate(x - 0.07, x + 0.07, bottom, top, 0.16, POLE, offset=0.06)
for i in range(panes):
    plate(LEFT + 0.25 + i * width + 0.07, LEFT + 0.25 + (i + 1) * width - 0.07, bottom, top, 0.04, WINDOW, offset=0.01)
windows("y", RIGHT, 1, -4.6, 1.6, 6.3, 8.9, 3)
windows("y", LEFT, -1, -2.4, 1.6, 6.3, 8.9, 2)
door("y", LEFT, -1, -4.9, -3.3, TERRACE, 7.9)
windows("x", -5.6, -1, MIDDLE - 6.1, MIDDLE + 6.1, 6.3, 8.9, 4)

# ---- the roof: a dark grey canopy out over the glass with a yellow line along its front, the air conditioning (light
# grey with dark grey louvres) and the mast
block(LEFT - 0.8, RIGHT + 0.8, -6.1, 4.1, ROOF, ROOF + 0.45, POLE, bevel=0.04)
block(LEFT - 0.8, RIGHT + 0.8, 4.1, 4.13, ROOF + 0.17, ROOF + 0.28, ACCENT)
block(LEFT - 0.5, RIGHT + 0.5, -5.8, 3.8, ROOF + 0.45, ROOF + 0.5, HEAD)
block(3.0, 8.0, -4.6, -1.4, ROOF + 0.5, ROOF + 1.25, HEAD, bevel=0.03)
for i in range(7):
    h = ROOF + 0.62 + i * 0.08
    block(3.25, 7.75, -1.4, -1.37, h, h + 0.035, POLE)
mast(-4.2, -3.2, ROOF + 0.5)

export(parts, [], "stand", OUT, {
    "stand.obj": "# Scenery: stand, the modern judges' stand built by tools/blender/stand.py",
    "stand.mtl": "# Materials of stand (tools/blender/stand.py), those of the street lamp and the fence; Pm and Pr: metal and\n"
                 "# roughness (tools/ContentPipeline). The windows \"windowlit\" shine in their Ke at night only (SceneRenderer)",
})
