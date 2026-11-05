"""What the Blender scripts of the props and the cars share (tools/blender/streetlamp.py, fence.py, car3.py): the empty
scene, the materials in the colours of the track blocks, simple parts and the export to an .obj + .mtl.

Imported at the start of a script run by Blender in the background; importing it empties the scene.
"""
import bpy, math, os

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


# ---- materials: the colours of the blocks (sRGB as the MTL files keep them), metal and roughness

def material(name, colour, metallic=0.0, roughness=0.5, emission=None, alpha=None):
    """A material; alpha below 1 makes it see-through (glass, "d" in the MTL file)."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*colour, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 1.0
    if alpha is not None:
        bsdf.inputs["Alpha"].default_value = alpha
    return m


# The dark grey of the sides of the road (metal), the light grey of the blocks, the yellow of the hazard stripes
POLE = material("pole", (0.22744, 0.22744, 0.22744), metallic=0.6, roughness=0.45)
HEAD = material("head", (0.82018, 0.82018, 0.82018), roughness=0.4)
ACCENT = material("accent", (1.0, 0.71333, 0.0), roughness=0.5)


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
    """An upright cylinder or cone from height z0 to z1."""
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=r1, radius2=r2, depth=z1 - z0,
                                    location=(location[0], location[1], (z0 + z1) / 2))
    return finish(bpy.context.active_object, mat, bevel)


def box(centre, size, mat, bevel=None, segments=2):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=centre)
    obj = bpy.context.active_object
    obj.scale = size
    bpy.ops.object.transform_apply(scale=True)
    return finish(obj, mat, bevel, segments)


def tube(points, radius, mat, resolution=4, name="tube"):
    """A tube along straight segments through the points, its ends closed; resolution 0 gives it four sides."""
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.bevel_depth = radius
    curve.bevel_resolution = resolution
    curve.use_fill_caps = True
    spline = curve.splines.new('POLY')
    spline.points.add(len(points) - 1)
    for p, co in zip(spline.points, points):
        p.co = (*co, 1.0)
    obj = bpy.data.objects.new(name, curve)
    scene.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target='MESH')
    return finish(bpy.context.active_object, mat)


def polyline(points, radius, mat):
    """A thin wire of four sides along the points."""
    return tube(points, radius, mat, resolution=0, name="wire")


def canonical_normals(path):
    """Writes the normals of each object of an OBJ file once each, as written in the file, in the order its faces first
    use them. The exporter writes the same normals in an order (and with repeated values) that can change from run to run
    for a large model; in this order the file is the same whoever builds it and however often."""
    with open(path, encoding="utf-8") as f:
        lines = f.read().split("\n")
    out, block = [], []
    written = [0, 0]   # the normals in the file before the block, as read and as written

    def flush():
        normals = [line for line in block if line.startswith("vn ")]
        unique, faces = {}, []
        for line in block:
            if line.startswith("f "):
                corners = []
                for corner in line.split()[1:]:
                    v, t, n = corner.split("/")
                    text = normals[int(n) - written[0] - 1]
                    if text not in unique:
                        unique[text] = len(unique) + written[1] + 1
                    corners.append(f"{v}/{t}/{unique[text]}")
                faces.append("f " + " ".join(corners))
        first = True
        for line in block:
            if line.startswith("vn "):
                if first:
                    out.extend(unique)
                    first = False
            elif line.startswith("f "):
                out.append(faces.pop(0))
            else:
                out.append(line)
        written[0] += len(normals)
        written[1] += len(unique)
        block.clear()

    for line in lines:
        if line.startswith("o "):
            flush()
        block.append(line)
    flush()
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(out))


def export(parts, separate, name, out, headers, smooth_angle=35):
    """Applies the modifiers, joins the parts into one object of the name (the separate objects stay objects of their
    own), smooths them by angle and exports them all as <name>.obj + .mtl into the folder out. The headers (by file name)
    replace the comment lines of Blender, which name its version, so that the files are the same whoever builds them."""
    for obj in parts + separate:
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
    joined = bpy.context.active_object
    joined.name = name
    joined.data.name = name
    for obj in [joined] + separate:
        bpy.ops.object.select_all(action='DESELECT')
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(smooth_angle))
        for mod in list(obj.modifiers):
            bpy.ops.object.modifier_apply(modifier=mod.name)

    triangles = sum(len(p.vertices) - 2 for o in [joined] + separate for p in o.data.polygons)
    os.makedirs(out, exist_ok=True)
    path = os.path.join(out, name + ".obj")
    bpy.ops.wm.obj_export(filepath=path, export_materials=True, export_pbr_extensions=True, export_normals=True,
                          export_uv=False, export_triangulated_mesh=True, export_selected_objects=False,
                          apply_modifiers=True)
    canonical_normals(path)
    for file_name, header in headers.items():
        file = os.path.join(out, file_name)
        with open(file, encoding="utf-8") as f:
            lines = [line for line in f.read().split("\n") if not line.startswith("# Blender") and line != "# www.blender.org"]
        with open(file, "w", encoding="utf-8", newline="\n") as f:
            f.write(header + "\n" + "\n".join(lines))
    print(f"{name.upper()} {triangles} triangles -> {path}")
    return triangles
