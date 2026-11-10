"""The side outline of a block of the road: its model (OBJ, in the game's coordinates) cut at a plane z = const, which is
what tools/CollisionGen makes its collision of at z = 0. tools/blender/offroad.py sweeps the off-road road along the
outline of the classic block, tools/blender/ground.py lays the ground under the road along its top."""
HALF = 4.5


def read_obj(path):
    """The vertices and the faces (lists of vertex indices) of an OBJ file, in the game's coordinates."""
    vertices, faces = [], []
    with open(path, encoding="utf-8") as f:
        for line in f:
            parts = line.split()
            if not parts:
                continue
            if parts[0] == "v":
                vertices.append(tuple(float(p) for p in parts[1:4]))
            elif parts[0] == "f":
                faces.append([int(p.split("/")[0]) - 1 for p in parts[1:]])
    return vertices, faces


def profile(path, cut=1e-4):
    """The outline of the block cut at z = cut (a hair off 0, as no vertex lies there), counter-clockwise in (x, y)."""
    vertices, faces = read_obj(path)
    segments = []
    for face in faces:
        points = []
        for a, b in zip(face, face[1:] + face[:1]):
            # The two faces of an edge go along it the other way round: cut it from its lower vertex in both, so that
            # they get the very same point (else the two may round to different keys and the outline falls apart)
            pa, pb = vertices[min(a, b)], vertices[max(a, b)]
            if (pa[2] - cut) * (pb[2] - cut) < 0:
                t = (cut - pa[2]) / (pb[2] - pa[2])
                points.append((pa[0] + (pb[0] - pa[0]) * t, pa[1] + (pb[1] - pa[1]) * t))
        if len(points) == 2:
            segments.append(points)
    # Chained into the longest closed loop
    def key(p):
        return (round(p[0], 4), round(p[1], 4))
    loops, unused = [], segments[:]
    while unused:
        a, b = unused.pop()
        loop = [a, b]
        while True:
            for i, (c, d) in enumerate(unused):
                if key(c) == key(loop[-1]):
                    loop.append(d)
                elif key(d) == key(loop[-1]):
                    loop.append(c)
                else:
                    continue
                unused.pop(i)
                break
            else:
                break
        if key(loop[0]) == key(loop[-1]):
            loop.pop()
        loops.append(loop)
    loop = max(loops, key=len)
    # Without points on a straight line, counter-clockwise
    def straight(p, q, r):
        return abs((q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0])) < 1e-6
    loop = [q for p, q, r in zip(loop[-1:] + loop[:-1], loop, loop[1:] + loop[:1]) if not straight(p, q, r)]
    area = sum(p[0] * q[1] - q[0] * p[1] for p, q in zip(loop, loop[1:] + loop[:1]))
    return loop if area > 0 else loop[::-1]


def top(loop):
    """The top of a counter-clockwise outline across its cell: its points from x = +4.5 (the left of the level) to
    x = -4.5, the road and the risers of its steps. x never grows along it: where the outline turns back under itself
    (under the rounded nose of a step of the off-road road), its points are moved back to the x where it turned, so that
    the chain is the top of the block seen from the side."""
    n = len(loop)
    start = max((i for i in range(n) if loop[i][0] > HALF - 1e-3), key=lambda i: loop[i][1])
    end = max((i for i in range(n) if loop[i][0] < -HALF + 1e-3), key=lambda i: loop[i][1])
    chain = [loop[start]]
    i = start
    while i != end:
        i = (i + 1) % n
        chain.append(loop[i])
    least = HALF
    for k, (x, y) in enumerate(chain):
        least = min(least, x)
        chain[k] = (round(least, 6), round(y, 6))
    return chain
