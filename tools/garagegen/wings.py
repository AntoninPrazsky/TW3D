"""Reading Wings 3D files (.wings: an Erlang external term, usually compressed): the objects with their winged edges,
faces and vertices, and the materials."""
import struct
import zlib
from collections import defaultdict

import numpy as np


class Atom(str):
    pass


def parse_term(d, i):
    """The Erlang term at d[i:] and the index after it."""
    t = d[i]; i += 1
    if t == 97: return d[i], i + 1                                   # small integer
    if t == 98: return struct.unpack_from('>i', d, i)[0], i + 4      # integer
    if t == 70: return struct.unpack_from('>d', d, i)[0], i + 8      # float
    if t == 99: return float(d[i:i + 31].split(b'\0')[0]), i + 31    # old float (text)
    if t in (100, 118):                                              # atom
        n = struct.unpack_from('>H', d, i)[0]; return Atom(d[i + 2:i + 2 + n].decode('utf8')), i + 2 + n
    if t in (115, 119):                                              # small atom
        n = d[i]; return Atom(d[i + 1:i + 1 + n].decode('utf8')), i + 1 + n
    if t in (104, 105):                                              # tuple
        if t == 104: n = d[i]; i += 1
        else: n = struct.unpack_from('>I', d, i)[0]; i += 4
        items = []
        for _ in range(n):
            v, i = parse_term(d, i); items.append(v)
        return tuple(items), i
    if t == 106: return [], i                                        # empty list
    if t == 107:                                                     # string (list of bytes)
        n = struct.unpack_from('>H', d, i)[0]; return list(d[i + 2:i + 2 + n]), i + 2 + n
    if t == 108:                                                     # list
        n = struct.unpack_from('>I', d, i)[0]; i += 4
        items = []
        for _ in range(n):
            v, i = parse_term(d, i); items.append(v)
        _, i = parse_term(d, i)  # tail
        return items, i
    if t == 109:                                                     # binary
        n = struct.unpack_from('>I', d, i)[0]; return bytes(d[i + 4:i + 4 + n]), i + 4 + n
    if t == 77:                                                      # bit binary
        n = struct.unpack_from('>I', d, i)[0]; return bytes(d[i + 5:i + 5 + n]), i + 5 + n
    if t in (110, 111):                                              # big integer
        if t == 110: n = d[i]; i += 1
        else: n = struct.unpack_from('>I', d, i)[0]; i += 4
        sign = d[i]; i += 1
        v = int.from_bytes(d[i:i + n], 'little'); return (-v if sign else v), i + n
    if t == 116:                                                     # map
        n = struct.unpack_from('>I', d, i)[0]; i += 4
        m = {}
        for _ in range(n):
            k, i = parse_term(d, i); v, i = parse_term(d, i); m[k] = v
        return m, i
    raise ValueError(f'tag {t} at {i - 1}')


def load(path):
    """The term of a .wings file: ('wings', version, (shapes, materials, properties))."""
    d = open(path, 'rb').read()
    if not d.startswith(b'#!WINGS-1.0'):
        raise ValueError('not a wings file')
    i = 15 + 4
    assert d[i] == 131
    i += 1
    if d[i] == 80:
        size = struct.unpack_from('>I', d, i + 1)[0]
        body = zlib.decompress(d[i + 5:])
        assert len(body) == size
        term, _ = parse_term(body, 0)
    else:
        term, _ = parse_term(d, i)
    return term


def vertex(v):
    """The position of a vertex record."""
    if isinstance(v, bytes):
        return struct.unpack('>3d', v[:24])
    for item in v:
        if isinstance(item, bytes):
            return struct.unpack('>3d', item[:24])
        if isinstance(item, tuple) and item[0] == 'position':
            p = item[1]
            return struct.unpack('>3d', p[:24]) if isinstance(p, bytes) else p
    raise ValueError('vertex without a position')


def edge(e):
    """The winged edge of an edge record: (vs, ve, lf, rf, ltpr, ltsu, rtpr, rtsu)."""
    for item in e:
        if isinstance(item, tuple) and item[0] == 'edge':
            return item[1:]
        if isinstance(item, bytes) and len(item) >= 32:
            return struct.unpack('>8i', item[:32])
    raise ValueError('edge without a record')


def read(path):
    term = load(path)
    shapes, materials, props = term[2]
    mats = {}
    for m in materials:
        gl = dict(next((x[1] for x in m[1] if x[0] == 'opengl'), []))
        mats[str(m[0])] = gl
    objects = {}
    for sh in shapes:
        name = sh[1] if not isinstance(sh[1], list) else bytes(sh[1]).decode('utf8', 'replace')
        objects.setdefault(name, []).append(sh[2])
    return objects, mats

def mesh(winged):
    """Positions, faces (vertex loops counter-clockwise from outside, material) and a normal for each face corner."""
    es = [edge(e) for e in winged[1]]
    fs = winged[2]
    pts = np.array([vertex(v) for v in winged[3]])
    hard = set(winged[4]) if len(winged) > 4 and isinstance(winged[4], list) else set()
    first = {}
    for i, (vs, ve, lf, rf, *_) in enumerate(es):
        first.setdefault(lf, i); first.setdefault(rf, i)
    faces = []
    for f in range(len(fs)):
        if f not in first:
            faces.append(None); continue
        start = e = first[f]; loop = []
        for _ in range(2000):
            vs, ve, lf, rf, ltpr, ltsu, rtpr, rtsu = es[e]
            if lf == f: loop.append(vs); e = ltsu
            else: loop.append(ve); e = rtsu
            if e == start: break
        loop.reverse()   # Wings' order is clockwise seen from outside
        material = 'default'
        for item in fs[f]:
            if isinstance(item, tuple) and item[0] == 'material': material = str(item[1])
        faces.append((loop, material))
    # face normals (area weighted)
    fn = {}
    for f, face in enumerate(faces):
        if face is None: continue
        P = pts[face[0]]; n = np.zeros(3)
        for i in range(len(P)): n += np.cross(P[i], P[(i + 1) % len(P)])
        fn[f] = n
    # smoothing groups around each vertex: faces joined by its soft edges
    parent = {}
    def find(k):
        while parent.setdefault(k, k) != k:
            parent[k] = parent[parent[k]]; k = parent[k]
        return k
    for i, (vs, ve, lf, rf, *_) in enumerate(es):
        if i in hard: continue
        for v in (vs, ve):
            a, b = find((v, lf)), find((v, rf))
            if a != b: parent[a] = b
    sums = defaultdict(lambda: np.zeros(3))
    for f, face in enumerate(faces):
        if face is None: continue
        for v in face[0]: sums[find((v, f))] += fn[f]
    corners = []
    for f, face in enumerate(faces):
        if face is None: continue
        normals = []
        for v in face[0]:
            n = sums[find((v, f))]
            if np.linalg.norm(n) < 1e-12 or np.dot(n, fn[f]) <= 0: n = fn[f]
            normals.append(n / (np.linalg.norm(n) + 1e-20))
        corners.append((face[0], face[1], normals))
    return pts, corners


def face_uvs(winged):
    """The UV coordinates of the corners of each face, in the order of the loops of mesh(), by the index of the face;
    None for a face without them. Wings keeps them in the edges: for its left face the UV of the start vertex
    (uv_lt), for its right face the UV of the end vertex (uv_rt)."""
    records = winged[1]
    es = [edge(e) for e in records]
    uvs = []
    for e in records:
        found = {}
        for item in e:
            if isinstance(item, tuple) and item[0] in ('uv_lt', 'uv_rt') and isinstance(item[1], bytes) and len(item[1]) >= 16:
                found[item[0]] = struct.unpack('>2d', item[1][:16])
        uvs.append(found)
    first = {}
    for i, (vs, ve, lf, rf, *_) in enumerate(es):
        first.setdefault(lf, i); first.setdefault(rf, i)
    result = {}
    for f in range(len(winged[2])):
        if f not in first:
            continue
        start = e = first[f]; loop = []
        for _ in range(2000):
            vs, ve, lf, rf, ltpr, ltsu, rtpr, rtsu = es[e]
            if lf == f: loop.append(uvs[e].get('uv_lt')); e = ltsu
            else: loop.append(uvs[e].get('uv_rt')); e = rtsu
            if e == start: break
        loop.reverse()
        result[f] = None if any(uv is None for uv in loop) else loop
    return result
