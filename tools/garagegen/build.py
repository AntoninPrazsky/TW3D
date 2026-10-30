"""Builds the 3D garage behind the menus from the Wings 3D files of 2015-2016 (art/wings/Garage) into content/Garage:
garage.obj/.mtl (the hall, its windows and furniture, the lift) and window.obj/.mtl (the landscape behind the windows,
drawn as a background). Coordinates stay those of Wings (y up, the axis of the lift along z, its middle at the origin).

    python tools/garagegen/build.py
"""
import os, sys
from collections import defaultdict
import numpy as np
import wings
from wings import vertex, edge

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
SRC = os.path.join(ROOT, 'art', 'wings', 'Garage')
OUT = os.path.join(ROOT, 'content', 'Garage')

FLOOR = -1.1522        # the floor of the original scene
TOP = 13.0             # the top of the walls (the hall has no ceiling: the camera never looks that high)
WEST, EAST = -13.67, 13.67      # the walls across the car's axis (the windows are in the west one)
NORTH, SOUTH = -24.0, 24.0      # the walls along it: longer than in 2015, so that cars stand in a row
NORTH_SHIFT = NORTH - (-13.67)  # the furniture at the north wall moves with it
BAND = (-8.33, 11.67, 1.68, 7.37)  # the opening of the windows in the west wall: z from, to, y from, to

# ---- Wings objects with their faces and smooth normals

def read(path):
    term = wings.load(path)
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

# ---- output

class Obj:
    def __init__(self):
        self.lines, self.vn, self.nv, self.nn = [], [], 0, 0
        self.materials = {}

    def material(self, name, kd, ks=(0.3, 0.3, 0.3), d=1.0, ke=(0, 0, 0), pm=None, pr=None, ns=0.3):
        self.materials[name] = (kd, ks, d, ke, pm, pr, ns)
        return name

    def object(self, name):
        self.lines.append('o ' + name)

    def face(self, points, normals, material):
        if material != getattr(self, 'current', None):
            self.lines.append('usemtl ' + material); self.current = material
        refs = []
        for p, n in zip(points, normals):
            self.lines.append('v %.5f %.5f %.5f' % tuple(p)); self.nv += 1
            self.lines.append('vn %.5f %.5f %.5f' % tuple(n)); self.nn += 1
            refs.append(f'{self.nv}//{self.nn}')
        self.lines.append('f ' + ' '.join(refs))

    def quad(self, a, b, c, d, material):
        """A flat quad, counter-clockwise seen from its front."""
        pts = [np.array(p, float) for p in (a, b, c, d)]
        n = np.cross(pts[1] - pts[0], pts[2] - pts[0]); n /= np.linalg.norm(n)
        self.face(pts, [n] * 4, material)

    def save(self, path):
        base = os.path.splitext(path)[0]
        open(path, 'w', newline='\n').write('# The garage of the menu, built from the Wings 3D files of 2015-2016 (see art/wings/README.md)\n'
                                           'mtllib ' + os.path.basename(base) + '.mtl\n' + '\n'.join(self.lines) + '\n')
        out = ['# Materials of the garage; Pm and Pr: metal and roughness (tools/ContentPipeline)', '']
        for name, (kd, ks, d, ke, pm, pr, ns) in self.materials.items():
            out += [f'newmtl {name}', 'Kd %.4f %.4f %.4f' % tuple(kd), 'd %.4f' % d, 'Ks %.4f %.4f %.4f' % tuple(ks),
                    'Ke %.4f %.4f %.4f' % tuple(ke), 'Ns %.4f' % ns]
            if pm is not None: out.append(f'Pm {pm:g}')
            if pr is not None: out.append(f'Pr {pr:g}')
            out += ['illum 2', '']
        open(base + '.mtl', 'w', newline='\n').write('\n'.join(out))

o = Obj()

# ---- the furniture: (object, from which file, moved with the north wall, finish (metallic, roughness) of its materials)
garage, garage_mats = read(SRC + '/garage.wings')
later, later_mats = read(SRC + '/garage_no_car.wings')
FURNITURE = [
    ('lift', 'garage', False, (0, 0.45)),
    ('cabinet', 'garage', True, (0, 0.4)),
    ('cabinetWheels', 'later', False, (1, 0.35)),
    ('cone1', 'garage', True, None), ('cone2', 'garage', True, None), ('cone3', 'garage', True, None),
    ('oil', 'garage', False, None), ('oil_copy19', 'garage', False, None),
    ('oil_copy30', 'garage', True, None), ('oil_copy31', 'garage', True, None), ('oil_copy32', 'garage', True, None),
    ('wheelsKey', 'garage', False, (1, 0.15)), ('Tube1', 'garage', True, (1, 0.15)),
    ('key', 'garage', False, (1, 0.2)), ('key_copy16', 'garage', False, (1, 0.2)), ('key_copy17', 'garage', False, (1, 0.2)),
    ('key_copy33', 'garage', True, (1, 0.2)), ('key_copy34', 'garage', True, (1, 0.2)), ('key_copy35', 'garage', True, (1, 0.2)),
    ('key_copy36', 'garage', True, (1, 0.2)), ('key_copy37', 'garage', True, (1, 0.2)),
    ('Cube1', 'garage', False, None),          # the blue box and the barrier under the windows
    ('Cube1_copy29', 'garage', True, None),
    ('Cylinder1', 'garage', True, (1, 0.08)),  # the rim on the wall
    ('sloupy', 'garage', False, None),         # the poles behind the windows
]
for name, source, north, finish in FURNITURE:
    objects, mats = (garage, garage_mats) if source == 'garage' else (later, later_mats)
    for k, winged in enumerate(objects[name]):
        pts, corners = mesh(winged)
        if north:
            pts = pts + np.array([0.0, 0.0, NORTH_SHIFT])
        o.object(name if k == 0 else f'{name}_{k}')
        for loop, material, normals in corners:
            gl = mats.get(material, {})
            mname = f'{name}_{material}'.replace(' ', '_')
            if mname not in o.materials:
                dif = gl.get('diffuse', (0.8, 0.8, 0.8, 1.0)); spec = gl.get('specular', (0.2, 0.2, 0.2, 1.0))
                pm, pr = finish if finish else (None, None)
                o.material(mname, dif[:3], spec[:3], dif[3], gl.get('emission', (0, 0, 0, 1))[:3], pm, pr)
            o.face(pts[loop], normals, mname)

# ---- the windows: the frames, the glass, the sills and the reveals of the old west wall
walls = garage['garageWalls'][0]
pts, corners = mesh(walls)
o.object('windows')
z0, z1, y0, y1 = BAND
for loop, material, normals in corners:
    P = pts[loop]
    lo, hi = P.min(0), P.max(0)
    if lo[0] >= -14.5 and hi[0] <= -11.2 and lo[2] >= z0 - 0.05 and hi[2] <= z1 + 0.05 and lo[1] >= y0 - 0.6 and hi[1] <= y1 + 0.6:
        gl = garage_mats.get(material, {})
        mname = 'window_' + material
        if mname not in o.materials:
            dif = gl.get('diffuse', (0.8, 0.8, 0.8, 1.0))
            o.material(mname, dif[:3], (0.6, 0.6, 0.6) if material == 'glass' else (0.3, 0.3, 0.3), dif[3],
                       pr=0.1 if material == 'glass' else None)
        o.face(P, normals, mname)

# ---- the hall
wall = o.material('hall_wall', (0.70, 0.68, 0.63), (0.1, 0.1, 0.1))
dado = o.material('hall_dado', (0.30, 0.33, 0.37), (0.2, 0.2, 0.2))
stripe = o.material('hall_stripe', (0.95, 0.75, 0.05), (0.3, 0.3, 0.3))
floor_a = o.material('hall_floor_light', (0.60, 0.60, 0.60), (0.4, 0.4, 0.4), pr=0.55)
floor_b = o.material('hall_floor_dark', (0.33, 0.34, 0.36), (0.4, 0.4, 0.4), pr=0.55)
line = o.material('hall_line', (0.95, 0.78, 0.05), (0.3, 0.3, 0.3))
door = o.material('hall_door', (0.82, 0.83, 0.84), (0.5, 0.5, 0.5), pr=0.5)
groove = o.material('hall_door_groove', (0.35, 0.36, 0.38))
frame = o.material('hall_door_frame', (0.20, 0.21, 0.23), (0.3, 0.3, 0.3))
DADO, STRIPE = 2.6, 0.18
o.object('hall')

# floor: checkered tiles of 4 units, centred on the lift, cut at the walls
T = 4.0
for i in range(-4, 5):
    for j in range(-7, 7):
        xa, xb = max(WEST, i * T - T / 2), min(EAST, i * T + T / 2)
        za, zb = max(NORTH, j * T), min(SOUTH, j * T + T)
        if xa >= xb or za >= zb: continue
        o.quad((xa, FLOOR, za), (xa, FLOOR, zb), (xb, FLOOR, zb), (xb, FLOOR, za), floor_a if (i + j) % 2 == 0 else floor_b)

def wall_x(x, za, zb, ya, yb, material, facing):
    """A rectangle of a wall across x, facing +x (1) or -x (-1)."""
    if facing > 0: o.quad((x, ya, zb), (x, ya, za), (x, yb, za), (x, yb, zb), material)
    else: o.quad((x, ya, za), (x, ya, zb), (x, yb, zb), (x, yb, za), material)

def wall_z(z, xa, xb, ya, yb, material, facing):
    if facing > 0: o.quad((xa, ya, z), (xb, ya, z), (xb, yb, z), (xa, yb, z), material)
    else: o.quad((xb, ya, z), (xa, ya, z), (xa, yb, z), (xb, yb, z), material)

def bands(put):
    """A wall in its bands: the dark dado, the yellow stripe, the light wall above; put(ya, yb, material)."""
    put(FLOOR, FLOOR + DADO, dado)
    put(FLOOR + DADO, FLOOR + DADO + STRIPE, stripe)
    put(FLOOR + DADO + STRIPE, TOP, wall)

# west wall with the opening of the windows (the reveals belong to the windows)
def west(ya, yb, material):
    if yb <= y0 or ya >= y1:
        wall_x(WEST, NORTH, SOUTH, ya, yb, material, 1); return
    if ya < y0: wall_x(WEST, NORTH, SOUTH, ya, y0, material, 1)
    if yb > y1: wall_x(WEST, NORTH, SOUTH, y1, yb, material, 1)
    lo, hi = max(ya, y0), min(yb, y1)
    wall_x(WEST, NORTH, z0, lo, hi, material, 1)
    wall_x(WEST, z1, SOUTH, lo, hi, material, 1)
bands(west)
bands(lambda ya, yb, m: wall_x(EAST, NORTH, SOUTH, ya, yb, m, -1))
bands(lambda ya, yb, m: wall_z(NORTH, WEST, EAST, ya, yb, m, 1))

# south wall with a sectional garage door
DOOR = (-7.0, 7.0, 9.5)   # x from, to, the top of its opening
dx0, dx1, dtop = DOOR
def south(ya, yb, material):
    wall_z(SOUTH, WEST, dx0, ya, yb, material, -1)
    wall_z(SOUTH, dx1, EAST, ya, yb, material, -1)
    if yb > dtop: wall_z(SOUTH, dx0, dx1, max(ya, dtop), yb, material, -1)
bands(south)
inset, sections = 0.25, 6
height = dtop - FLOOR
for k in range(sections):
    ya = FLOOR + k * height / sections; yb = ya + height / sections
    wall_z(SOUTH - inset, dx0, dx1, ya + 0.06, yb - 0.06, door, -1)
    wall_z(SOUTH - inset + 0.02, dx0, dx1, yb - 0.06, yb + 0.06 if k < sections - 1 else yb, groove, -1)
wall_z(SOUTH - inset + 0.02, dx0, dx1, FLOOR, FLOOR + 0.06, groove, -1)
# the frame of the door: its sides and lintel, standing out of the wall
fw, fd = 0.45, 0.3
for xa, xb in ((dx0 - fw, dx0), (dx1, dx1 + fw)):
    wall_z(SOUTH - fd, xa, xb, FLOOR, dtop + fw, frame, -1)
    wall_x(xa, SOUTH - fd, SOUTH, FLOOR, dtop + fw, frame, -1)
    wall_x(xb, SOUTH - fd, SOUTH, FLOOR, dtop + fw, frame, 1)
wall_z(SOUTH - fd, dx0, dx1, dtop, dtop + fw, frame, -1)
o.quad((dx0, dtop, SOUTH - fd), (dx0, dtop, SOUTH), (dx1, dtop, SOUTH), (dx1, dtop, SOUTH - fd), frame)
o.quad((dx0 - fw, dtop + fw, SOUTH), (dx0 - fw, dtop + fw, SOUTH - fd), (dx1 + fw, dtop + fw, SOUTH - fd), (dx1 + fw, dtop + fw, SOUTH), frame)
# the reveal of the door between its frame and the door
for x, facing in ((dx0, 1), (dx1, -1)):
    wall_x(x, SOUTH - inset, SOUTH, FLOOR, dtop, frame, facing)

# yellow lines around the bay of the lift
LY = FLOOR + 0.01
bay = (-4.2, 4.2, -6.5, 6.5)
bx0, bx1, bz0, bz1 = bay
w = 0.22
for (xa, xb, za, zb) in ((bx0, bx0 + w, bz0, bz1), (bx1 - w, bx1, bz0, bz1), (bx0, bx1, bz0, bz0 + w), (bx0, bx1, bz1 - w, bz1)):
    o.quad((xa, LY, za), (xa, LY, zb), (xb, LY, zb), (xb, LY, za), line)

os.makedirs(OUT, exist_ok=True)
o.save(os.path.join(OUT, 'garage.obj'))

# ---- the landscape behind the windows: the sky of the game on the plane of 2015 (its photos were stock photos),
# facing the hall, not mirrored seen from there
x, ya, yb, za, zb = -44.63, -10.65, 63.14, -36.9, 36.9
lines = ['# The landscape behind the windows of the garage (drawn as the background): the picture of the sky of the levels',
         'mtllib window.mtl', 'o window',
         f'v {x} {ya} {zb}', f'v {x} {ya} {za}', f'v {x} {yb} {za}', f'v {x} {yb} {zb}',
         'vt 0 0', 'vt 1 0', 'vt 1 1', 'vt 0 1', 'vn 1 0 0', 'usemtl window', 'f 1/1/1 2/2/1 3/3/1 4/4/1']
open(OUT + '/window.obj', 'w', newline='\n').write('\n'.join(lines) + '\n')
open(OUT + '/window.mtl', 'w', newline='\n').write('newmtl window\nKd 1 1 1\nd 1\nKs 0 0 0\nKe 0 0 0\nNs 0\nmap_Kd ../Env/sky.png\nillum 1\n')
print('faces', sum(1 for l in o.lines if l.startswith('f ')), 'materials', len(o.materials))
