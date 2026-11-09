"""Rocks lying across the road of a block (#65), shared by tools/blender/classic.py (the classic look) and
tools/blender/offroad.py (the off-road look), so that both looks have the very same rocks: the collision shape is made of
the classic model and serves every look.

A rock is an icosphere pushed in and out by fractal noise, broken off by a few planes (the flat faces where a stone split),
flattened a little and sunk into the road so that it sits on it. The export smooths it, so that its rounded parts show no
facets while the edges of the planes stay sharp; each rock is of one of two stone colours (a colour for each face would
draw the triangles). A seed gives the same rock every run, so the models are the same whoever builds them. Blender's
(x, y, z) is the game's (x, -z, y).
"""
import bpy, bmesh, math, random
from mathutils import Vector, noise

# The rocks of a block: (x, z, the radius, the height as a part of the radius, the seed), the game's model units. The
# wheels run at |z| 1.42 to 2.17 and the body up to 1.49: rocks there are what the car hits.
CLUSTERS = {
    # Small rocks a car rolls over, spread along the cell
    "rk1": [(-2.9, 2.6, 0.55, 0.7, 11), (-1.4, 1.7, 0.5, 0.8, 12), (0.2, -0.3, 0.6, 0.65, 13), (1.6, -1.9, 0.55, 0.75, 14),
            (3.0, -3.1, 0.45, 0.7, 15), (-0.6, 3.6, 0.4, 0.6, 16), (2.4, 0.9, 0.42, 0.7, 17)],
    # Big rocks across the middle of the cell
    "rk2": [(-0.4, 1.8, 1.45, 0.95, 21), (0.6, -0.8, 1.3, 1.0, 22), (-1.6, -2.9, 0.9, 0.9, 23), (1.9, 3.3, 0.8, 0.85, 24),
            (2.2, 1.0, 0.7, 0.8, 25)],
}
# A boulder across the whole road with sheer faces at the front and the back, and a few stones beside it: what needs the
# axles turned (rk3): (x, z, (the radius along the road, across it), the height, the seed), the first one the boulder
BOULDERS = {
    "rk3": [(0.0, 0.0, (2.0, 3.4), 2.0, 31), (-2.6, 3.6, (0.9, 0.9), 0.8, 32), (2.7, -3.6, (0.8, 0.8), 0.75, 33)],
}


def rough(rng, subdivisions, breaks, boxy=False):
    """A unit stone: an icosphere pushed out and in by fractal noise, broken off by `breaks` planes. A boxy one is pushed
    out towards a cube first (sheer sides, a flat top) and broken off by upright planes only, so that its sides stay sheer."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdivisions, radius=1.0)
    offset = Vector((rng.uniform(-50, 50), rng.uniform(-50, 50), rng.uniform(-50, 50)))
    for v in bm.verts:
        d = v.co.normalized()
        if boxy:
            d = Vector([math.copysign(abs(c) ** 0.3, c) for c in d])
        if boxy:
            bump = 1.0 + 0.1 * noise.fractal(d * 1.6 + offset, 1.0, 2.0, 4) + 0.06 * noise.noise(d * 7.0 + offset)
        else:
            # No finer detail than the mesh can carry, or the smoothing shows the triangles
            bump = 1.0 + 0.22 * noise.fractal(d * 1.6 + offset, 1.0, 2.0, 3) + 0.03 * noise.noise(d * 3.5 + offset)
        v.co = d * bump
    for _ in range(breaks):
        normal = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1),
                         rng.uniform(-0.1, 0.1) if boxy else rng.uniform(-0.2, 1))).normalized()
        cut = rng.uniform(0.62, 0.85)
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        result = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=normal * cut, plane_no=normal, clear_outer=True)
        edges = [e for e in result["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
        if edges:
            bmesh.ops.holes_fill(bm, edges=edges, sides=0)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    return bm


def rock(x, z, radius, height, seed, materials, road=0.5, subdivisions=None, breaks=3, sink=0.15, boxy=False):
    """One rock standing on the road (y = road) at (x, z); radius a number, or (along the road, across it)."""
    rng = random.Random(seed)
    rx, rz = radius if isinstance(radius, tuple) else (radius, radius)
    size = max(rx, rz)
    bm = rough(rng, subdivisions or (3 if size >= 0.5 else 2), breaks, boxy)
    stretch = (rng.uniform(0.85, 1.15), rng.uniform(0.85, 1.15))
    for v in bm.verts:
        gx, gz, gy = v.co.x * stretch[0], -v.co.y * stretch[1], v.co.z
        # Flat underneath, where it lies on the road (sunk a little into it)
        gy = max(gy, -0.4) * (height / size if gy > 0 else 0.5)
        v.co = Vector((x + gx * rx, -(z + gz * rz), road + (gy + sink) * size))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(f"rock{seed}")
    obj = bpy.data.objects.new(f"rock{seed}", mesh)
    bpy.context.scene.collection.objects.link(obj)
    for m in materials:
        mesh.materials.append(m)
    colour = 0 if rng.random() < 0.65 else len(materials) - 1
    for f in bm.faces:
        f.material_index = colour
    bm.to_mesh(mesh)
    bm.free()
    return obj


def cluster(name, materials, road=0.5):
    """The rocks of a block as Blender objects, in the stone materials given (the first the most of them)."""
    if name in BOULDERS:
        return [rock(x, z, r, h, seed, materials, road, breaks=3 if i == 0 else 4, sink=0.05, boxy=i == 0)
                for i, (x, z, r, h, seed) in enumerate(BOULDERS[name])]
    return [rock(x, z, r, r * h, seed, materials, road) for x, z, r, h, seed in CLUSTERS[name]]
