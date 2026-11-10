"""Blender (background): the ground under the road and the backdrop behind it (#66), decoration without collision for
the layers in front of the road and behind it.

The ground, so that the road runs along the top of a bank and does not float in the air:
- <road>g, the ground under a road block (a1g under every flat one): concrete across the cell from the surface of the
  road down to the bottom of the cell, its top the top of the outline of the classic block (tools/blender/outline.py, the
  outline its collision is made of), a dark seam along it a little lower and a joint at each end of the cell; for the
  front layer and the back layers beside the road. It is the same across the cell, so it turns with its road block (b2g
  turned under b2f). Its off-road look <road>go: earth with grass on top, along the top of the grass verge of the
  off-road look of the road block (<road>o cut at the verge).
- <road>w, a retaining wall of concrete panels along the edge of the road instead, for the front layer only: a wall the
  height of the cell at the side towards the road, its face to the camera, a cap along its top and a band of hazard
  stripes under it. It shows its face one way round only, so the road block turned has its mirror image <road>wf.
- g2, g2o, g2w: a whole cell of each, to stack under them.

The backdrop, for the deeper back layers, standing on the ground at the height of a flat road, the same in every style:
bk1 a rock outcrop, bk2 a grassy hill with bushes, stones and a few conifers, bk3 an industrial hall (corrugated
cladding, a roller door, a door with a lamp, windows lit at night: "windowlit", a stack), bk4 a copse of trees. All but
the hall look right from behind, so they turn.

    blender -b --factory-startup --python tools/blender/ground.py -- [<output folder>] [<block>...]

The coordinates are those of the game (model units: a cell is 9; y up, -z towards the camera: the scene turns the
world round, so +z of a model in the front layer is towards the road, in a back layer away from it). Blender has z up and
its OBJ export writes (x, y, z) as (x, z, -y): the game's (x, y, z) is built at (x, -z, y).
"""
import bpy, bmesh, math, os, random, sys
from mathutils import Vector, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from props import ROOT, material, export, box, cylinder, tube, finish
import outline
import rocks

HALF = 4.5
ROAD = 0.5                  # the surface of a flat road in its cell
MOD = os.path.join(ROOT, 'content', 'Levels', 'mod')
# The road blocks that are not flat, each with ground of its own (the flat ones share a1g)
SLOPED = ["b0", "b1", "b2", "b3", "b4", "c0", "c1", "c2", "c3", "d0", "d1", "d2", "d3", "e0", "e1", "e2", "e3", "e12",
          "e23", "e34", "s1", "s2", "k1", "k2", "k3"]
VERGE = -4.0                # where the off-road look is cut for the top of its grass verge (offroad.py, SECTION)
FACE = HALF - 1.5           # the face of the retaining wall, 1.5 thick, towards the camera
FLAT = [(HALF, ROAD), (-HALF, ROAD)]
FULL = [(HALF, HALF), (-HALF, HALF)]

CONCRETE = material("concrete", (0.33, 0.35, 0.38), roughness=0.85)
CONCRETE_TOP = material("concretetop", (0.40, 0.42, 0.45), roughness=0.85)
SEAM = material("seam", (0.20, 0.21, 0.23), roughness=0.9)
EARTH = material("earthbank", (0.34, 0.25, 0.17), roughness=1.0)
# "meadow": grass all over (the "grass" of the off-road road is earth in the middle of its block, grass at its verge)
GRASS = material("meadow", (0.25, 0.33, 0.14), roughness=1.0)
STONE = material("stone", (0.35, 0.33, 0.30), roughness=0.9)
STONE2 = material("stonedark", (0.26, 0.25, 0.23), roughness=0.9)
STONE3 = material("stonelichen", (0.33, 0.35, 0.27), roughness=0.9)
FOLIAGE = material("foliage", (0.15, 0.27, 0.09), roughness=0.9)
FOLIAGE2 = material("foliagelight", (0.24, 0.34, 0.11), roughness=0.9)
NEEDLES = material("needles", (0.08, 0.19, 0.10), roughness=0.9)
BARK = material("bark", (0.24, 0.17, 0.11), roughness=1.0)
WALL = material("wall", (0.30, 0.34, 0.40), roughness=0.7)
RIB = material("wallrib", (0.36, 0.40, 0.46), roughness=0.6)
ROOF = material("roof", (0.17, 0.18, 0.20), metallic=0.4, roughness=0.5)
STEEL = material("steel", (0.20, 0.21, 0.23), metallic=0.6, roughness=0.45)
HAZARD = material("hazard", (1.0, 0.71333, 0.0), roughness=0.5)
BLACK = material("black", (0.04, 0.04, 0.045), roughness=0.6)
DOOR = material("door", (0.45, 0.47, 0.50), metallic=0.5, roughness=0.45)
SLAT = material("doorslat", (0.30, 0.32, 0.35), metallic=0.5, roughness=0.5)
SIGN = material("sign", (0.62, 0.12, 0.10), roughness=0.5)
WHITE = material("signwhite", (0.85, 0.85, 0.82), roughness=0.5)
WINDOW = material("windowlit", (0.50, 0.72, 0.88), metallic=0.3, roughness=0.12, emission=(0.5, 0.34, 0.15))
for m in (EARTH, GRASS, CONCRETE, CONCRETE_TOP, SEAM, STONE, STONE2, STONE3, FOLIAGE, FOLIAGE2, NEEDLES, BARK):
    m.node_tree.nodes["Principled BSDF"].inputs["Specular IOR Level"].default_value = 0.1


# ---------------------------------------------------------------------------------------------------------------- helpers

def mesh_object(name, build, materials, weld=True):
    """An object of a bmesh made by build(bm, vert) (vert(x, y, z) in the game's coordinates), its faces in the
    materials (by index), welded unless it is made of separate closed pieces that may touch."""
    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    for m in materials:
        mesh.materials.append(m)
    bm = bmesh.new()
    build(bm, lambda x, y, z: bm.verts.new((x, -z, y)))
    if weld:
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    return obj


def gbox(centre, size, mat, bevel=None):
    """A box in the game's coordinates: its centre and its size along x, y and z."""
    return box((centre[0], -centre[2], centre[1]), (size[0], size[2], size[1]), mat, bevel)


def chain_of(model, cut=1e-4):
    """The top of the outline of a block's model cut at z = cut (see outline.top), its ends at the edges of the cell."""
    chain = outline.top(outline.profile(os.path.join(MOD, model + ".obj"), cut))
    chain[0], chain[-1] = (HALF, chain[0][1]), (-HALF, chain[-1][1])
    return [p for i, p in enumerate(chain) if i == 0 or abs(p[0] - chain[i - 1][0]) + abs(p[1] - chain[i - 1][1]) > 1e-6]


def mirrored(chain):
    """The chain of the block mirrored left to right."""
    return [(-x, y) for x, y in reversed(chain)]


def height_at(chain, x):
    """The height of the chain at x (the top of a riser there)."""
    best = None
    for p, q in zip(chain, chain[1:]):
        if min(p[0], q[0]) - 1e-9 <= x <= max(p[0], q[0]) + 1e-9:
            y = max(p[1], q[1]) if abs(p[0] - q[0]) < 1e-9 else p[1] + (q[1] - p[1]) * (x - p[0]) / (q[0] - p[0])
            best = y if best is None else max(best, y)
    return best


def split(chain, xs):
    """The chain with a point added where an edge that is not a riser crosses one of the xs."""
    points = [chain[0]]
    for p, q in zip(chain, chain[1:]):
        if abs(p[0] - q[0]) > 1e-9:
            for x in sorted((x for x in xs if q[0] + 1e-6 < x < p[0] - 1e-6), reverse=True):
                points.append((x, p[1] + (q[1] - p[1]) * (x - p[0]) / (q[0] - p[0])))
        points.append(q)
    return points


def solid(chain, z0, z1, top=1, side=0, bottom=-HALF):
    """A build (see mesh_object) of a prism from z0 to z1: the cell under the chain down to `bottom`, its top (the edges
    of the chain that are not risers) in the material `top`, everything else in `side`."""
    poly = chain + [(-HALF, bottom), (HALF, bottom)]

    def build(bm, vert):
        a = [vert(x, y, z0) for x, y in poly]
        b = [vert(x, y, z1) for x, y in poly]
        bm.faces.new(a).material_index = side
        bm.faces.new(b[::-1]).material_index = side
        for i in range(len(poly)):
            j = (i + 1) % len(poly)
            face = bm.faces.new((a[i], a[j], b[j], b[i]))
            face.material_index = top if j < len(chain) and abs(poly[i][0] - poly[j][0]) > 1e-6 else side
    return build


def decals(name, quads, z0, z1, mat):
    """Flat pieces on a face of a block: each quad (x, y) a thin closed slab from z0 to z1."""
    def build(bm, vert):
        for quad in quads:
            a = [vert(x, y, z0) for x, y in quad]
            b = [vert(x, y, z1) for x, y in quad]
            bm.faces.new(a)
            bm.faces.new(b[::-1])
            for i in range(len(quad)):
                j = (i + 1) % len(quad)
                bm.faces.new((a[i], a[j], b[j], b[i]))
    return mesh_object(name, build, [mat], weld=False)


def band(chain, d0, d1):
    """A band d0 to d1 under the chain, as quads (x, y): one under each edge that is not a riser, and one down each riser,
    so that the band steps down with the road."""
    quads, w = [], d1 - d0
    for p, q in zip(chain, chain[1:]):
        if p[0] - q[0] > 1e-4:
            quads.append([(p[0], p[1] - d0), (q[0], q[1] - d0), (q[0], q[1] - d1), (p[0], p[1] - d1)])
        elif abs(p[0] - q[0]) <= 1e-4 and abs(p[1] - q[1]) > 1e-4:
            high, low = max(p[1], q[1]), min(p[1], q[1])
            x = p[0]
            quads.append([(x + w / 2, high - d0), (x - w / 2, high - d0), (x - w / 2, low - d1), (x + w / 2, low - d1)])
    return quads


def strip(chain, x0, x1, below=0.0):
    """A strip from x0 to x1 (x0 > x1) from `below` under the chain down to the bottom of the cell."""
    return [(x0, height_at(chain, x0) - below), (x1, height_at(chain, x1) - below), (x1, -HALF), (x0, -HALF)]


# ----------------------------------------------------------------------------------------------------------------- ground

def concrete(chain, seam=True):
    """Concrete across the cell under the chain: lighter on top, a dark seam along both faces 0.9 under the top (so that
    it stays in the cell under the lowest road, 4 steps under its base), a dark joint at each end of the cell."""
    parts = [mesh_object("concrete", solid(chain, -HALF, HALF), [CONCRETE, CONCRETE_TOP])]
    quads = band(chain, 0.9, 0.98) if seam else []
    quads += [strip(chain, HALF, HALF - 0.05, 0.03), strip(chain, -HALF + 0.05, -HALF, 0.03)]
    for z in (-HALF, HALF):
        parts.append(decals("seam", quads, z - 0.02, z + 0.02, SEAM))
    return parts


def wall(chain, top=True):
    """A retaining wall 1.5 thick at the side of the cell towards the road (+z in the front layer), from the chain down
    to the bottom of the cell, of concrete panels 1.8 wide; on top, a cap standing out of its face and a band of hazard
    stripes under it (squares by turns, 20 to a cell, so that they run on from cell to cell)."""
    parts = [mesh_object("wall", solid(chain, FACE, HALF), [CONCRETE, CONCRETE_TOP])]
    below = 0.3 if top else 0.0
    joints = [strip(chain, HALF, HALF - 0.04, below), strip(chain, -HALF + 0.04, -HALF, below)]
    joints += [strip(chain, x + 0.04, x - 0.04, below) for x in (-2.7, -0.9, 0.9, 2.7)]
    parts.append(decals("joints", joints, FACE - 0.02, FACE + 0.02, SEAM))
    if top:
        parts.append(decals("cap", band(chain, 0.0, 0.2), FACE - 0.1, FACE, CONCRETE_TOP))
        size = 0.45
        points = split(chain, [-HALF + size * k for k in range(1, 20)])
        stripes = [[(p[0], p[1] - 0.3), (q[0], q[1] - 0.3), (q[0], q[1] - 0.8), (p[0], p[1] - 0.8)]
                   for p, q in zip(points, points[1:])
                   if p[0] - q[0] > 1e-4 and int(((p[0] + q[0]) / 2 + HALF) // size) % 2 == 0]
        parts.append(decals("hazard", stripes, FACE - 0.03, FACE + 0.01, HAZARD))
    return parts


def subdivided(chain, step):
    """The chain with points added along its edges that are not risers, `step` apart at most."""
    points = [chain[0]]
    for p, q in zip(chain, chain[1:]):
        k = max(1, math.ceil(abs(p[0] - q[0]) / step - 1e-9))
        points += [(p[0] + (q[0] - p[0]) * i / k, p[1] + (q[1] - p[1]) * i / k) for i in range(1, k + 1)]
    return points


def bank(chain, seed, grass=True):
    """Earth across the cell under the chain, grass on top a little uneven (not at the ends of the cell, so that the
    banks of the cells beside meet it, turned or not); the steep parts and the risers of steps bare earth."""
    rng = random.Random(seed)
    offset = Vector((rng.uniform(-50, 50), rng.uniform(-50, 50), 0))
    points = subdivided(chain, 0.5)
    zs = [-HALF + 0.5 * j for j in range(19)]

    def lift(x, z):
        fade = min(1.0, (HALF - abs(x)) / 1.5)
        return 0.12 * fade * noise.noise(Vector((x * 0.35, z * 0.35, 0)) + offset) if grass else 0.0

    def build(bm, vert):
        grid = [[vert(x, y + lift(x, z), z) for z in zs] for x, y in points]
        n = len(points)
        for i in range(n - 1):
            (x0, y0), (x1, y1) = points[i], points[i + 1]
            steep = abs(y1 - y0) > 1.2 * abs(x1 - x0)
            for j in range(18):
                face = bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
                face.material_index = 1 if grass and not steep else 0
        low = {(x, z): vert(x, -HALF, z) for x in (HALF, -HALF) for z in (HALF, -HALF)}
        bm.faces.new([grid[i][0] for i in range(n)] + [low[(-HALF, -HALF)], low[(HALF, -HALF)]])
        bm.faces.new([grid[i][18] for i in range(n)] + [low[(-HALF, HALF)], low[(HALF, HALF)]])
        bm.faces.new([grid[0][j] for j in range(19)] + [low[(HALF, HALF)], low[(HALF, -HALF)]])
        bm.faces.new([grid[n - 1][j] for j in range(19)] + [low[(-HALF, HALF)], low[(-HALF, -HALF)]])
        bm.faces.new([low[(HALF, -HALF)], low[(-HALF, -HALF)], low[(-HALF, HALF)], low[(HALF, HALF)]])
    return [mesh_object("bank", build, [EARTH, GRASS])]


# --------------------------------------------------------------------------------------------------------------- backdrop

def lumpy(x, z, base, radius, height, seed, mat, subdivisions=2, flat=0.3, rough=0.22):
    """A lumpy ball sitting on the ground at y = base: a bush or a crown of leaves (radius across, height up)."""
    rng = random.Random(seed)
    offset = Vector((rng.uniform(-50, 50), rng.uniform(-50, 50), rng.uniform(-50, 50)))
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdivisions, radius=1.0)
    for v in bm.verts:
        d = v.co.normalized()
        bump = 1.0 + rough * noise.noise(d * 2.2 + offset) + 0.4 * rough * noise.noise(d * 5.5 + offset)
        up = max(d.z, -flat)
        v.co = Vector((x + d.x * radius * bump, -z + d.y * radius * bump, base + (up + flat) / (1 + flat) * height * bump))
    mesh = bpy.data.meshes.new(f"lump{seed}")
    obj = bpy.data.objects.new(f"lump{seed}", mesh)
    bpy.context.scene.collection.objects.link(obj)
    bm.to_mesh(mesh)
    bm.free()
    return finish(obj, mat)


def conifer(x, z, base, height, seed):
    """A spruce: a short trunk and tiers of cones of needles narrowing upwards, a little askew."""
    rng = random.Random(seed)
    parts = [cylinder(0.05 * height, 0.035 * height, base - 0.2, base + 0.3 * height, BARK, vertices=7,
                      location=(x, -z))]
    tiers = 4
    for k in range(tiers):
        bottom = base + height * (0.18 + 0.19 * k)
        top = bottom + height * (0.42 - 0.04 * k)
        r = height * (0.25 - 0.045 * k) * rng.uniform(0.9, 1.1)
        dx, dz = rng.uniform(-0.06, 0.06) * height, rng.uniform(-0.06, 0.06) * height
        cone = cylinder(r, 0.0, bottom, min(top, base + height), NEEDLES if k % 2 == 0 else FOLIAGE, vertices=12,
                        location=(x + dx * (k / tiers), -(z + dz * (k / tiers))))
        cone.rotation_euler.z = rng.uniform(0, math.pi)
        parts.append(cone)
    return parts


def broadleaf(x, z, base, height, seed):
    """A broadleaf tree: a trunk forking into a crown of a few lumps of leaves."""
    rng = random.Random(seed)
    parts = [cylinder(0.07 * height, 0.045 * height, base - 0.2, base + 0.55 * height, BARK, vertices=7,
                      location=(x, -z))]
    r = 0.3 * height
    for k in range(4):
        angle = k * math.pi / 2 + rng.uniform(-0.4, 0.4)
        cx = x + (0.0 if k == 0 else 0.45 * r * math.cos(angle))
        cz = z + (0.0 if k == 0 else 0.45 * r * math.sin(angle))
        cy = base + height * (0.62 if k == 0 else rng.uniform(0.45, 0.55))
        size = r * (1.0 if k == 0 else rng.uniform(0.65, 0.8))
        parts.append(lumpy(cx, cz, cy - size * 0.6, size, size * 1.4, seed * 10 + k, FOLIAGE if k % 2 else FOLIAGE2,
                           subdivisions=3, flat=1.0, rough=0.12))
    return parts


def outcrop():
    """A crag across the cell on the ground at road height, the highest about 6 units above it: big stones split by many
    planes (flat faces with sharp edges between rounded, lumpy parts) in three greys, smaller ones leaning on them, scree
    at their feet, tufts of grass and a small spruce in the gaps."""
    big = [(-1.0, 0.6, (2.6, 2.3), 5.6, 41, 7), (2.1, 1.5, (2.0, 1.8), 3.8, 42, 6), (-3.1, 2.2, (1.5, 1.6), 2.8, 43, 6),
           (0.9, -1.7, (1.5, 1.3), 2.3, 46, 5), (3.3, -1.3, (1.2, 1.2), 1.8, 45, 5), (-3.2, -1.7, (1.1, 1.0), 1.4, 47, 5),
           (-1.9, -2.4, (0.8, 0.7), 1.0, 49, 4), (2.2, -3.2, (0.7, 0.8), 0.8, 50, 4)]
    shades = [[STONE, STONE2], [STONE3, STONE2], [STONE2, STONE], [STONE, STONE3]]
    parts = [rocks.rock(x, z, r, h, seed, shades[i % 4], ROAD, subdivisions=3, breaks=breaks, sink=0.1, lumps=0.3)
             for i, (x, z, r, h, seed, breaks) in enumerate(big)]
    rng = random.Random(48)
    for i in range(16):
        # Scree: mostly in front of the crag (towards the camera, -z), a few round the back
        x = rng.uniform(-4.1, 4.1)
        z = rng.uniform(-4.2, -2.4) if i < 11 else rng.uniform(-2.0, 4.0)
        r = rng.uniform(0.18, 0.5)
        parts.append(rocks.rock(x, z, r, r * rng.uniform(0.5, 0.9), 100 + i, shades[i % 4], ROAD, subdivisions=2,
                                breaks=3, sink=0.15))
    for i in range(8):
        x, z = rng.uniform(-4.0, 4.0), rng.uniform(-4.0, -1.2)
        parts.append(lumpy(x, z, ROAD - 0.05, rng.uniform(0.3, 0.55), rng.uniform(0.25, 0.45), 200 + i,
                           FOLIAGE2 if i % 2 else FOLIAGE, subdivisions=1, rough=0.3))
    parts += conifer(0.9, 1.4, ROAD + 1.6, 3.2, 210)
    return parts


def mound(seed, humps, n=26):
    """A build of grassy ground rising from the ground at the edges of the cell to the humps (x, z, radius, height),
    bare earth in patches and where it is steep."""
    rng = random.Random(seed)
    offset = Vector((rng.uniform(-50, 50), rng.uniform(-50, 50), 0))
    patches = Vector((rng.uniform(-50, 50), rng.uniform(-50, 50), 0))

    def y(x, z):
        u, v = x / HALF, z / HALF
        edge = max(0.0, 1 - u * u) * max(0.0, 1 - v * v)
        rise = sum(h * math.exp(-((x - cx) ** 2 + (z - cz) ** 2) / (r * r)) for cx, cz, r, h in humps)
        return ROAD + edge ** 0.6 * rise * (1 + 0.14 * noise.fractal(Vector((x * 0.3, z * 0.3, 0)) + offset, 1.0, 2.0, 3))

    def build(bm, vert):
        coords = [-HALF + 9 * i / n for i in range(n + 1)]
        grid = [[vert(x, y(x, z), z) for z in coords] for x in coords]
        for i in range(n):
            for j in range(n):
                quad = (grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1])
                face = bm.faces.new(quad)
                cx, cz = (coords[i] + coords[i + 1]) / 2, (coords[j] + coords[j + 1]) / 2
                rise = max(abs(y(coords[i + 1], cz) - y(coords[i], cz)), abs(y(cx, coords[j + 1]) - y(cx, coords[j])))
                bare = noise.noise(Vector((cx * 0.5, cz * 0.5, 0)) + patches) > 0.6 or rise > 2.4 * 9 / n
                face.material_index = 0 if bare else 1
        edge = [(i, 0) for i in range(n)] + [(n, j) for j in range(n)] + [(i, n) for i in range(n, 0, -1)] + \
               [(0, j) for j in range(n, 0, -1)]
        low = {p: vert(coords[p[0]], ROAD - 0.6, coords[p[1]]) for p in edge}
        for k, p in enumerate(edge):
            q = edge[(k + 1) % len(edge)]
            bm.faces.new((grid[p[0]][p[1]], grid[q[0]][q[1]], low[q], low[p])).material_index = 0
        bm.faces.new([low[p] for p in edge]).material_index = 0
    return build, y


def hill():
    """A grassy hill across the cell, two humps up to about 6 above the ground, bare earth in patches, stones poking out
    of it, bushes and a few spruces."""
    build, y = mound(51, [(-1.0, 0.8, 3.2, 4.8), (2.2, 1.6, 2.6, 3.0), (0.3, -1.6, 2.4, 1.4)])
    parts = [mesh_object("hill", build, [EARTH, GRASS])]
    for x, z, r, h, seed in [(1.0, -2.6, 0.45, 0.35, 61), (-3.0, -1.2, 0.35, 0.3, 62), (2.6, 0.2, 0.5, 0.4, 63),
                             (-0.6, -0.4, 0.4, 0.3, 64)]:
        parts.append(rocks.rock(x, z, r, h, seed, [STONE, STONE2], y(x, z) - 0.15, subdivisions=2, breaks=3,
                                sink=0.0))
    for x, z, r, h, seed in [(-3.4, -2.6, 0.7, 0.6, 71), (2.9, -2.4, 0.6, 0.5, 72), (-0.4, -3.2, 0.5, 0.4, 73),
                             (3.6, 1.0, 0.8, 0.7, 74), (-2.4, 2.8, 0.9, 0.8, 75), (0.9, 3.0, 0.7, 0.6, 76)]:
        parts.append(lumpy(x, z, y(x, z) - 0.1, r, h, seed, FOLIAGE if seed % 2 else FOLIAGE2))
    for x, z, height, seed in [(-1.6, 2.2, 5.0, 81), (-0.2, 2.9, 3.8, 82), (2.8, 2.6, 4.4, 83)]:
        parts += conifer(x, z, y(x, z) - 0.1, height, seed)
    return parts


def copse():
    """A copse on a low mound across the cell: spruces of a few heights, a broadleaf tree and bushes at their feet."""
    build, y = mound(91, [(0.0, 0.5, 3.4, 0.9)])
    parts = [mesh_object("copse", build, [EARTH, GRASS])]
    for x, z, height, seed in [(-2.4, 0.8, 7.5, 92), (1.4, 1.9, 9.0, 93), (3.3, -0.6, 5.5, 94), (-0.4, 2.8, 6.2, 97)]:
        parts += conifer(x, z, y(x, z) - 0.1, height, seed)
    parts += broadleaf(-0.3, -1.3, y(-0.3, -1.3) - 0.1, 6.5, 95)
    for x, z, r, h, seed in [(-3.6, -2.6, 0.8, 0.7, 101), (1.7, -2.7, 0.7, 0.6, 102), (3.7, 2.8, 0.9, 0.8, 103),
                             (-1.9, -3.3, 0.5, 0.4, 104)]:
        parts.append(lumpy(x, z, y(x, z) - 0.1, r, h, seed, FOLIAGE if seed % 2 else FOLIAGE2))
    return parts


def hall():
    """An industrial hall standing on the ground: a concrete plinth with hazard stripes, walls of corrugated cladding
    between steel corner posts, a low gable roof with a gutter, downpipes and ventilators, on its front (towards the
    road and the camera, -z) a roller door in a frame with a lamp over it, a sign, a door under a canopy with a lamp, a
    row of windows, more on its ends, the windows and the lamps lit at night; a stack beside it."""
    w, d, h = 4.1, 3.4, 6.0               # half its length (x), half its depth (z), the height of its walls
    front = -d
    parts = [gbox((0, ROAD + h / 2, 0), (2 * w, h, 2 * d), WALL),
             gbox((0, ROAD + 0.35, 0), (2 * w + 0.12, 0.7, 2 * d + 0.12), CONCRETE)]
    # The cladding: ribs on the front and the ends above the plinth
    k = 0
    while -w + 0.18 + 0.34 * k < w - 0.1:
        parts.append(gbox((-w + 0.18 + 0.34 * k, ROAD + 0.7 + (h - 0.7) / 2, front - 0.03), (0.1, h - 0.7, 0.06), RIB))
        k += 1
    for side in (-1, 1):
        k = 0
        while -d + 0.18 + 0.34 * k < d - 0.1:
            parts.append(gbox((side * (w + 0.03), ROAD + 0.7 + (h - 0.7) / 2, -d + 0.18 + 0.34 * k), (0.06, h - 0.7, 0.1), RIB))
            k += 1
        # Steel posts at the corners
        for z in (-d, d):
            parts.append(gbox((side * w, ROAD + h / 2, z), (0.3, h, 0.3), STEEL))
    # The roof: two slopes meeting along the ridge over the middle, overhanging a little
    ridge = 1.2

    def roof(bm, vert):
        a = [vert(-w - 0.25, ROAD + h, z) for z in (-d - 0.3, 0.0, d + 0.3)]
        b = [vert(w + 0.25, ROAD + h, z) for z in (-d - 0.3, 0.0, d + 0.3)]
        a[1].co.z = ROAD + h + ridge
        b[1].co.z = ROAD + h + ridge
        for k in (0, 1):
            bm.faces.new((a[k], b[k], b[k + 1], a[k + 1]))
        under = [vert(x, ROAD + h - 0.15, z) for x, z in ((-w - 0.25, -d - 0.3), (w + 0.25, -d - 0.3), (w + 0.25, d + 0.3),
                                                         (-w - 0.25, d + 0.3))]
        bm.faces.new(under)
        for e in ((a[0], b[0], under[1], under[0]), (b[2], a[2], under[3], under[2]), (a[0], under[0], under[3], a[2], a[1]),
                  (b[0], b[1], b[2], under[2], under[1])):
            bm.faces.new(e)
    parts.append(mesh_object("roof", roof, [ROOF]))
    parts.append(gbox((0, ROAD + h + ridge + 0.05, 0), (2 * w + 0.5, 0.14, 0.4), STEEL))           # the ridge cap
    for side in (-1, 1):
        def gable(bm, vert, side=side):
            # A triangular prism a little thick, so that it is closed and its faces turn out
            tris = [[vert(x, ROAD + h, -d), vert(x, ROAD + h, d), vert(x, ROAD + h + ridge * 0.95, 0.0)]
                    for x in (side * w - 0.05, side * w)]
            bm.faces.new(tris[0])
            bm.faces.new(tris[1][::-1])
            for k in range(3):
                bm.faces.new((tris[0][k], tris[0][(k + 1) % 3], tris[1][(k + 1) % 3], tris[1][k]))
        parts.append(mesh_object("gable", gable, [WALL]))
        # A downpipe from the gutter at each front corner
        parts.append(gbox((side * (w - 0.2), ROAD + h / 2, front - 0.3), (0.14, h, 0.14), STEEL))
    parts.append(gbox((0, ROAD + h - 0.1, front - 0.38), (2 * w + 0.5, 0.2, 0.22), STEEL))         # the gutter
    # Ventilators on the front slope of the roof
    for x in (-2.6, 0.2, 2.8):
        z = -1.7
        base = ROAD + h + ridge * (1 - abs(z) / (d + 0.3))
        parts.append(gbox((x, base + 0.2, z), (0.55, 0.8, 0.55), DOOR))
        parts.append(gbox((x, base + 0.65, z), (0.8, 0.1, 0.8), STEEL))
    # Hazard stripes at the foot of the front, on the plinth: squares by turns
    size = 0.6
    door_x, door_w, door_h, side_door = 1.6, 2.6, 4.0, -1.2
    for i in range(int(2 * w / size)):
        x = -w + (i + 0.5) * size
        if i % 2 == 0 and abs(x - door_x) > door_w / 2 + 0.4 and abs(x - side_door) > 0.9:
            parts.append(gbox((x, ROAD + size / 2 + 0.05, front - 0.08), (size, size, 0.04), HAZARD))
    # The roller door: slats, a frame, a lamp over it, a sign above, bollards in front of its sides
    parts.append(gbox((door_x, ROAD + door_h / 2 + 0.05, front - 0.07), (door_w, door_h, 0.06), DOOR))
    for k in range(1, 12):
        parts.append(gbox((door_x, ROAD + 0.05 + k * door_h / 12, front - 0.1), (door_w - 0.04, 0.06, 0.03), SLAT))
    for side in (-1, 1):
        parts.append(gbox((door_x + side * (door_w / 2 + 0.1), ROAD + (door_h + 0.3) / 2, front - 0.1), (0.2, door_h + 0.3, 0.14),
                          STEEL))
        parts.append(gbox((door_x + side * (door_w / 2 + 0.35), ROAD + 0.5, front - 0.6), (0.24, 1.0, 0.24), HAZARD))
        for y in (0.3, 0.7):
            parts.append(gbox((door_x + side * (door_w / 2 + 0.35), ROAD + y, front - 0.6), (0.26, 0.12, 0.26), BLACK))
    parts.append(gbox((door_x, ROAD + door_h + 0.2, front - 0.1), (door_w + 0.4, 0.3, 0.14), STEEL))
    parts.append(gbox((door_x, ROAD + door_h + 0.55, front - 0.3), (0.12, 0.12, 0.45), STEEL))
    parts.append(gbox((door_x, ROAD + door_h + 0.5, front - 0.5), (0.6, 0.1, 0.3), WINDOW))
    parts.append(gbox((door_x, ROAD + 5.35, front - 0.1), (2.4, 0.65, 0.06), SIGN))
    parts.append(gbox((door_x, ROAD + 5.35, front - 0.14), (1.9, 0.12, 0.03), WHITE))
    # A door under a canopy with a lamp, beside the roller door
    parts.append(gbox((side_door, ROAD + 1.15, front - 0.1), (0.95, 2.1, 0.06), SLAT))
    parts.append(gbox((side_door, ROAD + 1.15, front - 0.08), (1.15, 2.25, 0.04), STEEL))
    parts.append(gbox((side_door, ROAD + 2.5, front - 0.4), (1.4, 0.08, 0.8), STEEL))
    parts.append(gbox((side_door, ROAD + 2.4, front - 0.55), (0.35, 0.08, 0.2), WINDOW))
    # Windows in steel frames with a mullion and a sill: on the front, and three on each end
    def window(x, y, z, normal):
        nx, nz = normal                   # the way the wall faces, out of the hall

        def at(depth, dy=0.0):
            return (x + nx * depth, y + dy, z + nz * depth)

        def size(along, up, thick):
            return (thick, up, along) if nx else (along, up, thick)
        return [gbox(at(0.08), size(0.74, 1.04, 0.04), STEEL),
                gbox(at(0.1), size(0.62, 0.92, 0.04), WINDOW),
                gbox(at(0.13), size(0.04, 0.92, 0.03), STEEL),
                gbox(at(0.12, -0.53), size(0.82, 0.07, 0.16), CONCRETE_TOP)]
    for x in (-1.6, -2.35, -3.1, 3.45):
        parts += window(x, ROAD + 4.6, front, (0, -1))
    for side in (-1, 1):
        for z in (-1.6, 0.0, 1.6):
            parts += window(side * w, ROAD + 4.6, z, (side, 0))
    # The stack, at the right end (-x) behind the front
    x, z = -w - 0.75, 1.0
    parts.append(tube([(x, -z, ROAD), (x, -z, ROAD + h + 3.4)], 0.32, STEEL, resolution=3, name="stack"))
    for y in (ROAD + h + 2.2, ROAD + h + 2.9):
        parts.append(tube([(x, -z, y), (x, -z, y + 0.3)], 0.34, SIGN if y < ROAD + h + 2.5 else WHITE, resolution=3,
                          name="band"))
    parts.append(gbox((x + 0.45, ROAD + 1.8, z), (0.5, 0.2, 0.2), STEEL))                           # its pipe into the wall
    return parts


BLOCKS = {
    "a1g": ("the ground under a flat road, concrete", lambda: concrete(FLAT)),
    "a1go": ("the ground under a flat road, earth and grass", lambda: bank(chain_of("a1o", VERGE), 60)),
    "a1w": ("a retaining wall under a flat road", lambda: wall(FLAT)),
    "g2": ("a cell of ground under the ground of a road, concrete", lambda: concrete(FULL, seam=False)),
    "g2o": ("a cell of ground under the ground of a road, earth", lambda: bank(FULL, 62, grass=False)),
    "g2w": ("a cell of a retaining wall under the wall of a road", lambda: wall(FULL, top=False)),
    "bk1": ("a rock outcrop", outcrop),
    "bk2": ("a grassy hill", hill),
    "bk3": ("an industrial hall", hall),
    "bk4": ("a copse of trees", copse),
}
for i, road in enumerate(SLOPED):
    BLOCKS[road + "g"] = (f"the ground under {road}, concrete", lambda road=road: concrete(chain_of(road)))
    BLOCKS[road + "go"] = (f"the ground under {road}, earth and grass",
                           lambda road=road, i=i: bank(chain_of(road + "o", VERGE), 63 + i))
    BLOCKS[road + "w"] = (f"a retaining wall under {road}", lambda road=road: wall(chain_of(road)))
    BLOCKS[road + "wf"] = (f"a retaining wall under {road} turned", lambda road=road: wall(mirrored(chain_of(road))))

if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    OUT = args.pop(0) if args and args[0] not in BLOCKS else MOD
    for name in args or list(BLOCKS):
        title, make = BLOCKS[name]
        parts = make()
        header = f"# Made by tools/blender/ground.py: {name}, {title} (#66)"
        export(parts, [], name, OUT, {name + ".obj": header, name + ".mtl": header}, smooth_angle=35)
        for obj in list(bpy.data.objects):
            bpy.data.objects.remove(obj)
