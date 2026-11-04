"""Blender (background): decimates one material of an OBJ model (the tyre of a wheel) to a number of triangles, keeps
the other materials as they are, smooths the normals by angle and exports the OBJ with the same material names.

blender -b --factory-startup --python tools/blender/decimate.py -- <in.obj> <out.obj> <material> <triangles> [angle]"""
import bpy, bmesh, math, sys

args = sys.argv[sys.argv.index("--") + 1:]
src, out, tyre, target = args[0], args[1], args[2], int(args[3])
angle = float(args[4]) if len(args) > 4 else 30.0

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.obj_import(filepath=src)
obj = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
bpy.context.view_layer.objects.active = obj
obj.select_set(True)

def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)

# The tyre apart: separate by material
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.separate(type='MATERIAL')
bpy.ops.object.mode_set(mode='OBJECT')
parts = [o for o in bpy.context.scene.objects if o.type == 'MESH']
for o in parts:
    names = [m.name for m in o.data.materials if m]
    used = {o.data.materials[p.material_index].name for p in o.data.polygons}
    print("PART", o.name, used, tris(o))
    if tyre in used:
        bpy.ops.object.select_all(action='DESELECT')
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        # Merge the doubles of the hard edges first, so that the collapse sees one surface
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.remove_doubles(threshold=0.0001)
        bpy.ops.mesh.quads_convert_to_tris()
        bpy.ops.object.mode_set(mode='OBJECT')
        mod = o.modifiers.new("d", 'DECIMATE')
        mod.decimate_type = 'COLLAPSE'
        mod.ratio = min(1.0, target / max(1, tris(o)))
        mod.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=mod.name)
        if o.data.has_custom_normals:
            bpy.ops.mesh.customdata_custom_splitnormals_clear()
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(angle))
        print("TYRE", tris(o))

bpy.ops.object.select_all(action='SELECT')
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
joined = bpy.context.view_layer.objects.active
joined.name = "Cylinder1"
print("TOTAL", tris(joined))
bpy.ops.wm.obj_export(filepath=out, export_selected_objects=False, export_normals=True, export_uv=False,
                      export_materials=True, export_pbr_extensions=True, export_triangulated_mesh=False,
                      apply_modifiers=True)
