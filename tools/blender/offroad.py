"""Blender (background): builds road blocks of the off-road style (#57, a prototype) from the classic blocks: a dirt road
with two ruts where the wheels run, a crown between them, berms and shoulders beside them and grass on the verges, swept
along the profile of the classic block, and exports them as <block>o (.obj + .mtl).

The profile is the classic block cut at the plane z = 0 of the game (what tools/CollisionGen makes its collision of). The
bottom of the ruts lies on it: the wheels of every car run at |z| 1.42 to 2.17 (the wheel models are made there), the
physics of the game is flat, so the wheels never leave the ruts, and the collision of the classic block is where the
tyres touch. Everything else, the crown, the berms, the shoulders and the verges, is raised above it, straight up, so
that the cross-section is the same at both ends of every block and the ruts run on from block to block, also from a flat
road onto a slope. Only the edges of the profile that face up (the road) are raised; its sides and its bottom stay, the
faces of the block at z = +-4.5 are the classic profile in the colour of a cut bank of earth.

The stairs (s1) have ruts too, and the edges of their steps are rounded, as steps of earth wear (the noses inwards, the
feet filled with a little earth), the cross-section raised along the normal of the rounded profile there, so that the
crown and the verges follow it round. Their collision stays the sharp classic one. The blocks are the road of the classic set (ROAD_KIT, settled by #55): the pits, the
tunnels, the walls, the start and the finish and the decoration stay classic. A block whose road is not the same across
it (cut at other z) is left out, as only a prism along z can be swept so.

The materials name the surfaces of the game's shader (Effects/Surfaces.fxh): "earth…" is drawn as earth, the ruts and
the bank in darker browns of it, and "grass" as grass.

    blender -b --factory-startup --python tools/blender/offroad.py -- <output folder> <earth|grass> [<block>...]

The second argument is what the crown between the ruts is: bare earth, or a strip of grass as on a field path.

Blender has z up and its OBJ export writes (x, y, z) as (x, z, -y): the game's (x, y, z) is built at (x, -z, y).
"""
import bpy, bmesh, math, os, random, sys
from mathutils import Vector, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from props import ROOT, material, export
from outline import profile
import rocks

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = args[0] if args else os.path.join(ROOT, 'content', 'Levels', 'mod')
CROWN = args[1] if len(args) > 1 else "earth"
# The road of the classic set: the flat road, the slopes, the smooth slopes, the lips, the stairs, the steps, the crests, the bends and the road ends
ROAD_KIT = ["a1", "b1", "b2", "b3", "b4", "c1", "c2", "c3", "d1", "d2", "d3", "s1", "s2", "k1", "k2", "k3", "b0", "c0", "d0", "e0", "e1", "e2", "e3", "e12", "e23", "e34", "r1", "r2", "r3", "w1", "w2",
            "l1", "l2", "rk1", "rk2", "rk3"]
BLOCKS = [a for a in args[2:] if a not in ("o", "t", "u")] or ROAD_KIT
# What is made of each block: the off-road block (o) and the transitions (t, u), all of them unless named
KINDS = [a for a in args[2:] if a in ("o", "t", "u")] or ["o", "t", "u"]
CLASSIC = os.path.join(ROOT, 'content', 'Levels', 'mod')

EARTH = material("earth", (0.38, 0.30, 0.22), roughness=1.0)
RUT = material("earthrut", (0.30, 0.23, 0.17), roughness=1.0)
BANK = material("earthbank", (0.34, 0.25, 0.17), roughness=1.0)
GRASS = material("grass", (0.25, 0.33, 0.14), roughness=1.0)
# The grey of the road of the classic blocks, where a transition reaches the classic style
ROAD = material("road", (0.22744, 0.22744, 0.22744), roughness=1.0)
# A log lying across the road: bark, and the cut wood of its ends; rocks
BARK = material("bark", (0.20, 0.15, 0.10), roughness=1.0)
BARK2 = material("barkgrey", (0.29, 0.25, 0.20), roughness=1.0)
WOOD = material("wood", (0.62, 0.47, 0.30), roughness=1.0)
RING = material("woodring", (0.47, 0.33, 0.20), roughness=1.0)
ROCK = material("stone", (0.30, 0.28, 0.26), roughness=1.0)
ROCK2 = material("stonedark", (0.22, 0.21, 0.20), roughness=1.0)
# The materials of the dirt road (every one is written into the .mtl of every look), and those of what lies on it
MATERIALS = [EARTH, RUT, BANK, GRASS, ROAD]
ON_ROAD_MATERIALS = [BARK, BARK2, WOOD, RING, ROCK, ROCK2]
# Earth and grass mirror nothing and shine little (Ks): Blender would write 0.5
for m in MATERIALS + ON_ROAD_MATERIALS:
    m.node_tree.nodes["Principled BSDF"].inputs["Specular IOR Level"].default_value = 0.1

# Blocks whose off-road look is the dirt road of a1 with something lying on it, no transitions (they would meet the
# classic road with a log or rocks on them): a log half sunk into the earth (its axis on the profile, so that it is the
# half cylinder of the classic block, a speed bump), and the rocks of tools/blender/rocks.py, the same as the classic one's
def log(radius, segments=64, rows=24, length=4.45, seed=7):
    """The part of a trunk above the slab of the road: a cylinder cut a little under the road (y = 0.5), so that it does
    not show under the block, and a little shorter than the cell, so that its ends do not lie in the faces at its sides.
    Its bark is furrowed along the trunk and patchy in two colours; the radius only ever shrinks from that of the classic
    block (whose half cylinder is the collision), so that a wheel never sinks into the bark. Its ends show the year rings
    inside a rim of bark, and two stubs of branches stand out of it beside the lines of the wheels."""
    rng = random.Random(seed)
    offset = Vector((rng.uniform(-50, 50), rng.uniform(-50, 50), rng.uniform(-50, 50)))
    sink = 0.2 / radius                      # how far round under the road the bark goes (radians)
    angles = [-sink + (math.pi + 2 * sink) * i / segments for i in range(segments + 1)]
    zs = [-length + 2 * length * j / rows for j in range(rows + 1)]

    def bark(a, z):
        """How far in the bark is at the angle round the trunk and z along it (0 to about 0.09 of the radius)."""
        wobble = noise.noise(Vector((math.cos(a), math.sin(a), z * 0.35)) * 1.5 + offset)
        furrow = (0.5 + 0.5 * math.sin(a * 23 + 2.5 * wobble)) ** 6
        return radius * (0.06 * furrow + 0.03 * (0.5 + 0.5 * noise.noise(Vector((a * 2.0, z * 0.6, 0.0)) + offset)))

    mesh = bpy.data.meshes.new("log")
    obj = bpy.data.objects.new("log", mesh)
    bpy.context.scene.collection.objects.link(obj)
    for m in (BARK, BARK2, WOOD, RING):
        mesh.materials.append(m)
    bm = bmesh.new()
    grid = [[bm.verts.new(((radius - bark(a, z)) * math.cos(a), -z, 0.5 + (radius - bark(a, z)) * math.sin(a)))
             for a in angles] for z in zs]
    for j in range(rows):
        for i in range(segments):
            f = bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
            patch = noise.noise(Vector((angles[i] * 1.3, zs[j] * 0.4, 5.0)) + offset)
            f.material_index = 1 if patch > 0.25 else 0
        # The cut under the road
        bm.faces.new((grid[j][0], grid[j + 1][0], grid[j + 1][-1], grid[j][-1])).material_index = 0
    # The ends: a rim of bark, rings of the wood by turns, the core; the cut under the axis as one triangle
    rings = [0.9, 0.74, 0.58, 0.42, 0.26, 0.12]
    for j, z in ((0, zs[0]), (rows, zs[-1])):
        axis = bm.verts.new((0.0, -z, 0.5))
        loops = [grid[j]] + [[bm.verts.new((v.co.x * k, v.co.y, 0.5 + (v.co.z - 0.5) * k)) for v in grid[j]] for k in rings]
        for n, (outer, inner) in enumerate(zip(loops, loops[1:])):
            for i in range(segments):
                f = bm.faces.new((outer[i], outer[i + 1], inner[i + 1], inner[i]))
                f.material_index = 0 if n == 0 else (3 if n % 2 else 2)
        for i in range(segments):
            bm.faces.new((loops[-1][i], loops[-1][i + 1], axis)).material_index = 2
        for outer, inner in zip(loops, loops[1:]):
            bm.faces.new((outer[0], inner[0], inner[-1], outer[-1])).material_index = 0
        bm.faces.new((loops[-1][0], axis, loops[-1][-1])).material_index = 0
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    parts = [obj]
    # Stubs of branches, out of the upper side of the trunk beyond the lines of the wheels (|z| above 2.2)
    for z, a in ((3.1, 1.15), (-2.7, 2.2)):
        direction = Vector((math.cos(a), -0.35 * (1 if z > 0 else -1), math.sin(a))).normalized()
        base = Vector((radius * 0.9 * math.cos(a), -z, 0.5 + radius * 0.9 * math.sin(a)))
        length_stub = 0.45 * radius
        bpy.ops.mesh.primitive_cone_add(vertices=10, radius1=0.13 * radius, radius2=0.09 * radius, depth=length_stub,
                                        location=base + direction * (length_stub / 2),
                                        rotation=direction.to_track_quat('Z', 'Y').to_euler())
        stub = bpy.context.active_object
        stub.data.materials.append(BARK)
        stub.data.materials.append(WOOD)
        for poly in stub.data.polygons:
            poly.material_index = 1 if poly.normal.z > 0.9 else 0
        parts.append(stub)
    return parts


ON_ROAD = {"l1": lambda: log(0.9), "l2": lambda: log(1.8), "rk1": lambda: rocks.cluster("rk1", [ROCK, ROCK2]),
           "rk2": lambda: rocks.cluster("rk2", [ROCK, ROCK2]), "rk3": lambda: rocks.cluster("rk3", [ROCK, ROCK2])}

# The cross-section across the road (|z|, model units) and how high it is above the profile: the crown, the ruts (the
# tyres are 1.42 to 2.17 wide, the bottom a little wider), the berm pushed out beside a rut, the shoulder, the grass
# verge and the rounded bank down to the face of the block at |z| = 4.5
SECTION = [
    (0.0, 0.20), (0.45, 0.195), (0.85, 0.18), (1.08, 0.16), (1.2, 0.12), (1.3, 0.05), (1.38, 0.0),
    (1.8, 0.0), (2.22, 0.0), (2.3, 0.07), (2.4, 0.17), (2.52, 0.22), (2.7, 0.2), (3.1, 0.17), (3.35, 0.19),
    (3.5, 0.26), (3.7, 0.3), (4.05, 0.31), (4.3, 0.27), (4.42, 0.16), (4.48, 0.05), (4.5, 0.0)]
# The blocks whose steps are rounded: the radius of the noses and of the feet of the steps
ROUNDED = {"s1": (0.45, 0.3), "s2": (0.6, 0.4), "k1": (0.45, 0.3), "k2": (0.6, 0.4), "k3": (0.7, 0.45)}


def zone(w):
    """The material of the part of the cross-section around |z| = w. The earth of the shoulder meets the grass of the verge
    at |z| = 3.35 (a point of SECTION), where the game blends them raggedly (SURFACE_VERGE in content/Effects/Surfaces.fxh,
    which must follow it), and the colours of EARTH and GRASS are those the game blends (SceneRenderer.OffroadEarthColour
    and OffroadGrassColour)."""
    if w >= 3.42:
        return GRASS
    if w < 1.25:
        return GRASS if CROWN == "grass" else EARTH
    if w < 2.36:
        return RUT
    return EARTH


def faces_up(p, q):
    """An edge of the counter-clockwise outline is a part of the road when its outward normal points up."""
    dx, dy = q[0] - p[0], q[1] - p[1]
    length = math.hypot(dx, dy)
    return length > 1e-6 and -dx / length > 0.35


def same(a, b):
    return len(a) == len(b) and all(abs(p[0] - q[0]) < 1e-3 and abs(p[1] - q[1]) < 1e-3 for p, q in zip(a, b))


def rounded(loop, nose, foot, segments=6):
    """The outline with the corners of its steps rounded (where the road meets a riser, inside the block's cell): a nose
    (a convex corner) cut inwards, a foot (a concave one) filled, each by a curve tangent to both edges at the radius
    from the corner."""
    n = len(loop)
    points = []
    for i in range(n):
        a, p, b = loop[i - 1], loop[i], loop[(i + 1) % n]
        up_in, up_out = faces_up(a, p), faces_up(p, b)
        riser = b if up_in else a
        steep = abs(riser[0] - p[0]) < abs(riser[1] - p[1])
        if up_in == up_out or not steep or abs(p[0]) > 4.49:
            points.append(p)
            continue
        d1 = ((p[0] - a[0]), (p[1] - a[1]))
        d2 = ((b[0] - p[0]), (b[1] - p[1]))
        l1, l2 = math.hypot(*d1), math.hypot(*d2)
        convex = d1[0] * d2[1] - d1[1] * d2[0] > 0
        t = min(nose if convex else foot, 0.45 * l1, 0.45 * l2)
        t1 = (p[0] - d1[0] / l1 * t, p[1] - d1[1] / l1 * t)
        t2 = (p[0] + d2[0] / l2 * t, p[1] + d2[1] / l2 * t)
        for k in range(segments + 1):
            u = k / segments
            points.append(((1 - u) ** 2 * t1[0] + 2 * (1 - u) * u * p[0] + u * u * t2[0],
                           (1 - u) ** 2 * t1[1] + 2 * (1 - u) * u * p[1] + u * u * t2[1]))
    return points


def subdivided(loop, step):
    """The outline with a point added wherever an edge that is not vertical crosses x = -4.5 + step * i, so that every edge
    spans one interval between such x (and the x of the points of the outline) at most."""
    xs = sorted({round(p[0], 6) for p in loop} | {round(-4.5 + step * i, 6) for i in range(int(round(9 / step)) + 1)})
    points = []
    for p, q in zip(loop, loop[1:] + loop[:1]):
        points.append(p)
        if abs(q[0] - p[0]) < 1e-6:
            continue
        inside = [x for x in xs if min(p[0], q[0]) + 1e-6 < x < max(p[0], q[0]) - 1e-6]
        for x in (inside if q[0] > p[0] else reversed(inside)):
            t = (x - p[0]) / (q[0] - p[0])
            points.append((x, p[1] + (q[1] - p[1]) * t))
    return points, xs


def smoothstep(a, b, x):
    t = min(max((x - a) / (b - a), 0.0), 1.0)
    return t * t * (3 - 2 * t)


def ends_at(z, material):
    """How far along a transition (0 at its off-road end, 1 at its classic end) a part of the cross-section of a material
    reaches, uneven across the road: the grass ends first, the ruts last."""
    if material == GRASS:
        return 0.42 + 0.07 * math.sin(2.9 * z + 0.6) + 0.04 * math.sin(7.1 * z + 1.9)
    if material == RUT:
        return 0.68 + 0.04 * math.sin(5.0 * z + 0.3)
    return 0.56 + 0.06 * math.sin(3.3 * z + 2.1) + 0.03 * math.sin(8.3 * z)


def build(block, kind):
    """kind "o": the off-road block; "t": a transition from the off-road style at its -x end (the right edge of an unturned
    block in a level) to the classic one at its +x end, "u": the same the other way round. Along a transition the
    cross-section sinks to the profile, the grass, the earth and the ruts end unevenly and the classic grey of the road
    takes their place, so that it meets the classic block next to it flush."""
    if block in ON_ROAD and kind != "o":
        return
    path = os.path.join(CLASSIC, ("a1" if block in ON_ROAD else block) + ".obj")
    loop = profile(path)
    if not all(same(loop, profile(path, cut)) for cut in (-3.0, -1.8, 1.8, 3.0)):
        print(f"{block}: not the same across the road, left out")
        return
    name = block + kind
    transition = kind != "o"
    if block in ROUNDED:
        loop = rounded(loop, *ROUNDED[block])
    xs = None
    if transition:
        loop, xs = subdivided(loop, 0.25)
    n = len(loop)
    up = [faces_up(loop[i], loop[(i + 1) % n]) for i in range(n)]
    # Where a point of the outline is raised to: straight up when an edge of the road meets it (so that the ends of the
    # blocks meet); round the rounded steps, along the normal of the outline, less where it turns down a riser
    lift = []
    for i in range(n):
        p, a, b = loop[i], loop[i - 1], loop[(i + 1) % n]
        if block not in ROUNDED or abs(p[0]) > 4.49:
            lift.append((0.0, 1.0) if up[i] or up[i - 1] else (0.0, 0.0))
            continue
        normal = [0.0, 0.0]
        for q, r in ((a, p), (p, b)):
            length = math.hypot(r[0] - q[0], r[1] - q[1])
            normal[0] += (r[1] - q[1]) / length
            normal[1] += -(r[0] - q[0]) / length
        length = math.hypot(*normal)
        normal = (normal[0] / length, normal[1] / length)
        weight = smoothstep(0.3, 0.8, normal[1])
        lift.append((normal[0] * weight, normal[1] * weight))
    sections = [(-w, h) for w, h in reversed(SECTION[1:])] + SECTION

    def along(x):
        """How far a point is from the off-road end of a transition (0) to its classic end (1)."""
        return (x + 4.5) / 9 if kind == "t" else (4.5 - x) / 9

    def scale(x):
        """How much of the height of the cross-section a point of a transition keeps."""
        return 1.0 - smoothstep(0.12, 0.62, along(x)) if transition else 1.0

    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    for m in MATERIALS:
        mesh.materials.append(m)
    bm = bmesh.new()
    grid = [[bm.verts.new((loop[i][0] + lift[i][0] * h * scale(loop[i][0]), -z, loop[i][1] + lift[i][1] * h * scale(loop[i][0])))
             for z, h in sections] for i in range(n)]
    for i in range(n):
        j = (i + 1) % n
        u = along((loop[i][0] + loop[j][0]) / 2)
        if not up[i] and abs(lift[i][0]) + abs(lift[i][1]) + abs(lift[j][0]) + abs(lift[j][1]) < 1e-9:
            # An edge that is not raised at either end (the bottom, a side): one face across the block
            face = bm.faces.new((grid[i][0], grid[j][0], grid[j][-1], grid[i][-1]))
            face.material_index = MATERIALS.index(BANK if not transition or u < 0.6 else ROAD)
            continue
        for k in range(len(sections) - 1):
            face = bm.faces.new((grid[i][k], grid[j][k], grid[j][k + 1], grid[i][k + 1]))
            z = (sections[k][0] + sections[k + 1][0]) / 2
            material = zone(abs(z)) if up[i] else BANK
            if transition and u > (ends_at(z, material) if up[i] else 0.6):
                material = ROAD
            face.material_index = MATERIALS.index(material)
    for k in (0, len(sections) - 1):
        if not transition:
            face = bm.faces.new([grid[i][k] for i in range(n)])
            face.material_index = MATERIALS.index(BANK)
            continue
        # The face at the side of a transition in vertical strips, each between an edge of the outline below and one
        # above, so that its colour can change along it
        for left, right in zip(xs, xs[1:]):
            spans = [i for i in range(n) if min(loop[i][0], loop[(i + 1) % n][0]) <= left + 1e-6
                     and max(loop[i][0], loop[(i + 1) % n][0]) >= right - 1e-6 and abs(loop[i][0] - loop[(i + 1) % n][0]) > 1e-6]
            spans.sort(key=lambda i: loop[i][1] + loop[(i + 1) % n][1])
            for lower, upper in zip(spans[0::2], spans[1::2]):
                a, b = (lower, (lower + 1) % n) if loop[lower][0] < loop[(lower + 1) % n][0] else ((lower + 1) % n, lower)
                c, d = (upper, (upper + 1) % n) if loop[upper][0] > loop[(upper + 1) % n][0] else ((upper + 1) % n, upper)
                verts = []
                for v in (grid[a][k], grid[b][k], grid[c][k], grid[d][k]):
                    if v not in verts:
                        verts.append(v)
                if len(verts) < 3:
                    continue
                face = bm.faces.new(verts)
                face.material_index = MATERIALS.index(BANK if along((left + right) / 2) < 0.6 else ROAD)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    what = "the off-road style of the block" if kind == "o" else "a transition from the off-road style to the classic one"
    header = f"# Made by tools/blender/offroad.py from {block}.obj: {what} (a prototype, #57)"
    extras = ON_ROAD[block]() if block in ON_ROAD else []
    export([obj] + extras, [], name, OUT, {name + ".obj": header, name + ".mtl": header}, smooth_angle=50)
    bpy.data.objects.remove(obj)


for block in BLOCKS:
    for kind in KINDS:
        build(block, kind)
