"""Blender (background): builds the street lamp of the scenery and exports it as content/Scenery/lamp.obj + lamp.mtl.

A modern LED street light in the colours of the track blocks: a tapered round pole of the dark grey of the sides of the
road on a base with a flange and bolts, a yellow band (the yellow of the hazard stripes) and a service door; an arm
bending smoothly out over the road; a flat head of light grey with cooling fins on top and yellow strips along its sides,
tilted up a little. The lens under the head is an object of its own ("lens", material "streetlight"): the game lights it
at night and lets it light the scene below (Graphics/SceneRenderer). The head leans towards the camera (-z of the
model), as the props face the road from behind it. Model units at the scale of the blocks (a cell is 9 units, the car
2.4 high), the foot at the origin, y up.

    blender -b --factory-startup --python tools/blender/streetlamp.py [-- <output folder>]

Blender has z up and its OBJ export writes (x, y, z) as (x, z, -y): the lamp is built with z up and the head towards +y.
"""
import bpy, bmesh, math, os, sys
from mathutils import Matrix, Vector

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = args[0] if args else os.path.join(ROOT, 'content', 'Scenery')

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


# ---- materials: the colours of the blocks (sRGB as the MTL files keep them), metal and roughness

def material(name, colour, metallic=0.0, roughness=0.5, emission=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*colour, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 1.0
    return m


POLE = material("pole", (0.22744, 0.22744, 0.22744), metallic=0.6, roughness=0.45)
HEAD = material("head", (0.82018, 0.82018, 0.82018), roughness=0.4)
ACCENT = material("accent", (1.0, 0.71333, 0.0), roughness=0.5)
LENS = material("streetlight", (0.9, 0.9, 0.86), roughness=0.2, emission=(1.0, 0.86, 0.64))


def finish(obj, mat, bevel=None, segments=2):
    """Gives the object its material and, if asked, rounded edges (a bevel modifier, applied at the end)."""
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    if bevel:
        mod = obj.modifiers.new("bevel", 'BEVEL')
        mod.width = bevel
        mod.segments = segments
        mod.limit_method = 'ANGLE'
    return obj


def cylinder(r1, r2, z0, z1, mat, vertices=32, bevel=None, location=(0, 0)):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=r1, radius2=r2, depth=z1 - z0,
                                    location=(location[0], location[1], (z0 + z1) / 2))
    return finish(bpy.context.active_object, mat, bevel)


def box(centre, size, mat, bevel=None, segments=2):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=centre)
    obj = bpy.context.active_object
    obj.scale = size
    bpy.ops.object.transform_apply(scale=True)
    return finish(obj, mat, bevel, segments)


def pole_radius(z):
    """The radius of the tapered pole at a height."""
    return 0.19 - (0.19 - 0.115) * (z - 0.63) / (7.7 - 0.63)


parts = []

# ---- the base: a flange with four bolts and a sleeve over the foot of the pole
parts.append(cylinder(0.40, 0.40, 0.0, 0.08, POLE, bevel=0.015))
parts.append(cylinder(0.27, 0.205, 0.08, 0.63, POLE, bevel=0.01))
for k in range(4):
    a = math.pi / 4 + k * math.pi / 2
    parts.append(cylinder(0.032, 0.032, 0.08, 0.13, POLE, vertices=12, location=(0.33 * math.cos(a), 0.33 * math.sin(a))))

# ---- the pole, tapered, with a yellow band and a service door facing the road
parts.append(cylinder(pole_radius(0.63), pole_radius(7.7), 0.63, 7.7, POLE))
parts.append(cylinder(pole_radius(0.95) + 0.012, pole_radius(1.40) + 0.012, 0.95, 1.40, ACCENT))
parts.append(box((0, pole_radius(2.0) - 0.004, 2.0), (0.14, 0.03, 0.7), POLE, bevel=0.008))

# ---- the arm: up from the top of the pole, a smooth bend, then out over the road rising a little
curve = bpy.data.curves.new("arm", 'CURVE')
curve.dimensions = '3D'
curve.bevel_depth = 0.075
curve.bevel_resolution = 4
curve.use_fill_caps = True
spline = curve.splines.new('POLY')
points = [(0, 0, 7.55), (0, 0, 7.95)]
bend = 0.6
for i in range(1, 13):
    t = math.pi / 2 * i / 12
    points.append((0, bend - bend * math.cos(t), 7.95 + bend * math.sin(t)))
points.append((0, 2.45, 8.62))
spline.points.add(len(points) - 1)
for p, co in zip(spline.points, points):
    p.co = (*co, 1.0)
arm = bpy.data.objects.new("arm", curve)
scene.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
arm.select_set(True)
bpy.ops.object.convert(target='MESH')
arm = bpy.context.active_object
parts.append(finish(arm, POLE))

# ---- the head, from the end of the arm out over the road, tilted up a little about its back end
HEAD_BACK, HEAD_FRONT, HEAD_Z, HEAD_HALF_WIDTH, HEAD_HALF_HEIGHT = 2.38, 3.95, 8.62, 0.31, 0.085
head_parts = []
head = box((0, (HEAD_BACK + HEAD_FRONT) / 2, HEAD_Z), (2 * HEAD_HALF_WIDTH, HEAD_FRONT - HEAD_BACK, 2 * HEAD_HALF_HEIGHT), HEAD,
           bevel=0.05, segments=4)
# Narrower towards its tip
for v in head.data.vertices:
    t = (v.co.y - HEAD_BACK) / (HEAD_FRONT - HEAD_BACK)
    v.co.x *= 1.0 - 0.18 * max(0.0, t - 0.3) / 0.7
head_parts.append(head)
# Cooling fins along the top, of the dark grey of the pole
for k in range(7):
    x = -0.21 + k * 0.07
    head_parts.append(box((x, (HEAD_BACK + HEAD_FRONT) / 2 - 0.05, HEAD_Z + HEAD_HALF_HEIGHT + 0.025), (0.022, 1.05, 0.05), POLE,
                          bevel=0.006))
# Yellow strips along both sides
for side in (-1, 1):
    head_parts.append(box((side * (HEAD_HALF_WIDTH - 0.02), (HEAD_BACK + HEAD_FRONT) / 2 - 0.08, HEAD_Z), (0.05, 1.1, 0.07), ACCENT,
                          bevel=0.01))
# The collar where the arm enters the head
bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=0.105, depth=0.2, location=(0, HEAD_BACK + 0.02, HEAD_Z),
                                    rotation=(math.pi / 2, 0, 0))
head_parts.append(finish(bpy.context.active_object, POLE, bevel=0.015))

# The lens: the bottom of the head inside a rim, a rounded rectangle facing down
bpy.ops.mesh.primitive_plane_add(size=1.0, location=(0, (HEAD_BACK + HEAD_FRONT) / 2 + 0.02, HEAD_Z - HEAD_HALF_HEIGHT - 0.004))
lens = bpy.context.active_object
lens.scale = (0.46, 1.3, 1.0)
bpy.ops.object.transform_apply(scale=True)
bm = bmesh.new()
bm.from_mesh(lens.data)
bmesh.ops.bevel(bm, geom=list(bm.verts), offset=0.08, segments=4, affect='VERTICES')
bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
bm.to_mesh(lens.data)
bm.free()
lens.name = "lens"
lens.data.name = "lens"
finish(lens, LENS)
head_parts.append(lens)

tilt = Matrix.Translation((0, HEAD_BACK, HEAD_Z)) @ Matrix.Rotation(math.radians(4), 4, 'X') @ Matrix.Translation((0, -HEAD_BACK, -HEAD_Z))
for obj in head_parts:
    obj.matrix_world = tilt @ obj.matrix_world
parts.extend(p for p in head_parts if p is not lens)

# ---- apply the modifiers, join everything but the lens into "lamp", smooth by angle
for obj in parts + [lens]:
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    for mod in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

bpy.ops.object.select_all(action='DESELECT')
for obj in parts:
    obj.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
lamp = bpy.context.active_object
lamp.name = "lamp"
lamp.data.name = "lamp"
for obj in (lamp, lens):
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))
    for mod in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=mod.name)

triangles = sum(len(p.vertices) - 2 for o in (lamp, lens) for p in o.data.polygons)
os.makedirs(OUT, exist_ok=True)
path = os.path.join(OUT, "lamp.obj")
bpy.ops.wm.obj_export(filepath=path, export_materials=True, export_pbr_extensions=True, export_normals=True, export_uv=False,
                      export_triangulated_mesh=True, export_selected_objects=False, apply_modifiers=True)

# The headers name this script instead of the version of Blender, so that the files are the same whoever builds them
HEADERS = {
    "lamp.obj": "# Scenery: lamp, the modern street lamp built by tools/blender/streetlamp.py",
    "lamp.mtl": "# Materials of lamp (tools/blender/streetlamp.py); Pm and Pr: metal and roughness (tools/ContentPipeline).\n"
                "# The lens \"streetlight\" shines in its Ke at night only and lights the scene below it (SceneRenderer)",
}
for name, header in HEADERS.items():
    file = os.path.join(OUT, name)
    with open(file, encoding="utf-8") as f:
        lines = [line for line in f.read().split("\n") if not line.startswith("# Blender") and line != "# www.blender.org"]
    with open(file, "w", encoding="utf-8", newline="\n") as f:
        f.write(header + "\n" + "\n".join(lines))
print(f"STREETLAMP {triangles} triangles -> {path}")
