"""Builds the scenery (the static props beside the track) from their Wings 3D files (art/wings/Scenery) into
content/Scenery: an .obj + .mtl for each prop, in model units at the scale of the blocks (a cell is 9 units, the car is
8.6 units long), with the foot of the prop at the origin, y up and the long side of a fence or a guardrail along x (along
the road). Smooth normals by the hard edges of Wings, as the garage has them.

The street lamp and the metal fence were textured with photos of rust, which are not in the repository: the game
draws coloured polygons, so their faces get the colours the textures had there (sampled once and written down here)
by the rules below. The chain between the posts was modelled link by link (some 3400 faces); a tube along the curve it
hangs in takes its place, which looks the same from the camera of the game.

    python tools/garagegen/scenery.py
"""
import math
import os
import numpy as np
from wings import read, mesh, face_uvs

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
SRC = os.path.join(ROOT, 'art', 'wings', 'Scenery')
OUT = os.path.join(ROOT, 'content', 'Scenery')


class Material:
    def __init__(self, kd, ks=(0.2, 0.2, 0.2), d=1.0, ke=(0, 0, 0), pm=None, pr=None):
        self.kd, self.ks, self.d, self.ke, self.pm, self.pr = kd, ks, d, ke, pm, pr


def wings_material(gl, ks=(0.2, 0.2, 0.2), pm=None, pr=None):
    """The colour of a Wings material as it is (its diffuse colour, alpha and emission)."""
    dif = gl.get('diffuse', (0.8, 0.8, 0.8, 1.0))
    return Material(dif[:3], ks, dif[3], gl.get('emission', (0, 0, 0, 1))[:3], pm, pr)


class Prop:
    """A prop: its model, its Wings file, its size (the height, or the length along x), how its faces get their
    materials (None drops a face), the materials, and what is done to it: centred in x and z, turned half way round
    about y, more faces added (in the coordinates of the Wings file)."""
    def __init__(self, model, file, rule, materials, height=None, length=None, centre=False, half_turn=False, extra=None):
        self.model, self.file, self.rule, self.materials = model, file, rule, materials
        self.height, self.length, self.centre, self.half_turn, self.extra = height, length, centre, half_turn, extra


# ---- the materials of the faces

def cone(face, mats):
    # Orange plastic with white reflective bands
    return 'orange' if face['material'] == 'default2' else 'white'

def lamp(face, mats):
    if face['material'] == 'default2':
        return 'glass'
    # The texture: rust, brighter on the rim of the roof, and a grey plate in one corner (the door at the foot)
    if 12.9 < face['centre'][1] < 12.95:
        return 'rim'
    uvs = face['uvs']
    if uvs is not None and max(u for u, v in uvs) < 0.13 and max(v for u, v in uvs) < 0.26:
        return 'door'
    return 'rust'

def pole(face, mats):
    return {'default': 'metal', 'default2': 'brace', 'default3': 'insulator', 'default4': 'wood'}[face['material']]

def fence(face, mats):
    # The texture: rusty bars and posts, steel blue spikes on the posts
    return 'steel' if face['centre'][1] > 1.5 else 'rust'

def guardrail(face, mats):
    return {'chrome': 'steel', 'default2': 'red', 'default3': 'white'}[face['material']]

def stand(face, mats):
    return {'default2': 'wood', 'default3': 'brown', 'default4': 'white', 'default5': 'glass', 'default6': 'wall',
            'default7': 'dark', 'default9': 'grey', 'default10': 'roof'}[face['material']]

# The posts of the chain stand at these x; the links hang between them, thin in z, above this height
CHAIN_POSTS = (-2.1, 0.0, 2.1)
CHAIN_LINKS_ABOVE = 0.6

def chain(face, mats):
    P, centre = face['points'], face['centre']
    link = (face['material'] in ('default3', 'default5') and centre[1] > CHAIN_LINKS_ABOVE and np.abs(P[:, 2]).max() < 0.075
            and min(abs(centre[0] - post) for post in CHAIN_POSTS) > 0.12)
    if link:
        return None
    return {'default': 'white', 'default2': 'red', 'default3': 'white', 'default4': 'base', 'default5': 'red'}[face['material']]

def chain_tubes():
    """The chains between the posts as tubes along a parabola through their ends and their lowest point (fitted to
    the links of the Wings file), in red and white pieces, as the links alternate."""
    ends_y, bottom_y, radius, pieces, sides = 1.217, 0.71, 0.045, 14, 5
    faces = []
    for a, b in zip(CHAIN_POSTS, CHAIN_POSTS[1:]):
        x0, x1 = a + 0.17, b - 0.17
        middle, half = (x0 + x1) / 2, (x1 - x0) / 2
        curve = lambda x: bottom_y + (ends_y - bottom_y) * ((x - middle) / half) ** 2
        for i in range(pieces):
            xa, xb = x0 + (x1 - x0) * i / pieces, x0 + (x1 - x0) * (i + 1) / pieces
            pa, pb = np.array([xa, curve(xa), 0.0]), np.array([xb, curve(xb), 0.0])
            along = (pb - pa) / np.linalg.norm(pb - pa)
            across = np.cross(along, [0.0, 0.0, 1.0])
            ring = [across * math.cos(k * 2 * math.pi / sides) + np.array([0.0, 0.0, 1.0]) * math.sin(k * 2 * math.pi / sides)
                    for k in range(sides)]
            material = 'red' if i % 2 == 0 else 'white'
            for k in range(sides):
                n0, n1 = ring[k], ring[(k + 1) % sides]
                quad = [pa + n0 * radius, pb + n0 * radius, pb + n1 * radius, pa + n1 * radius]
                # Counter-clockwise from outside, as mesh() gives the faces
                normal = np.cross(quad[1] - quad[0], quad[2] - quad[0])
                if np.dot(normal, n0 + n1) < 0:
                    quad.reverse()
                faces.append((np.array(quad), [n0, n0, n1, n1] if np.dot(normal, n0 + n1) >= 0 else [n1, n1, n0, n0], material))
    return faces


PROPS = [
    Prop('cone', 'cone.wings', cone, lambda mats: {
        'orange': wings_material(mats['default2'], ks=(0.3, 0.3, 0.3), pr=0.5),
        'white': wings_material(mats['default3'], ks=(0.3, 0.3, 0.3), pr=0.5)}, height=1.6),
    Prop('lamp', 'lamp.wings', lamp, lambda mats: {
        'rust': Material((0.42, 0.21, 0.07), (0.1, 0.1, 0.1)),
        'rim': Material((0.58, 0.27, 0.15), (0.1, 0.1, 0.1)),
        'door': Material((0.33, 0.32, 0.30), (0.3, 0.3, 0.3), pm=1, pr=0.5),
        'glass': wings_material(mats['default2'], ks=(0.6, 0.6, 0.6), pr=0.1)}, height=8.0),
    Prop('pole', 'pole.wings', pole, lambda mats: {
        'wood': wings_material(mats['default4'], ks=(0.1, 0.1, 0.1)),
        'metal': wings_material(mats['default'], ks=(0.4, 0.4, 0.4), pm=1, pr=0.45),
        'brace': wings_material(mats['default2'], ks=(0.3, 0.3, 0.3)),
        'insulator': wings_material(mats['default3'], ks=(0.4, 0.4, 0.4), pr=0.3)}, height=20.0),
    # Half a cell long: two pieces in each cell, end to end, make a fence along the road
    Prop('fence', 'fence.wings', fence, lambda mats: {
        'rust': Material((0.54, 0.25, 0.16), (0.1, 0.1, 0.1)),
        'steel': Material((0.45, 0.47, 0.48), (0.4, 0.4, 0.4), pm=1, pr=0.4)}, length=4.5),
    # The guardrails of 2018 are a cell long already, modelled on the near edge of a road block: centred, and turned so
    # that the rail faces the camera (and the road, when they stand behind it) and the posts are behind it
    Prop('guardrail', 'guardrail.wings', guardrail, lambda mats: {
        'steel': wings_material(mats['chrome'], ks=(0.6, 0.6, 0.6), pm=1, pr=0.35)}, length=9.0, centre=True, half_turn=True),
    Prop('guardrail2', 'guardrail2.wings', guardrail, lambda mats: {
        'steel': wings_material(mats['chrome'], ks=(0.6, 0.6, 0.6), pm=1, pr=0.35),
        'red': wings_material(mats['default2'], ks=(0.3, 0.3, 0.3)),
        'white': wings_material(mats['default3'], ks=(0.3, 0.3, 0.3))}, length=9.0, centre=True, half_turn=True),
    # The judges' stand: a two-storey building, its terrace towards the camera
    Prop('stand', 'stand.wings', stand, lambda mats: {
        'wall': wings_material(mats['default6'], ks=(0.1, 0.1, 0.1)),
        'white': wings_material(mats['default4'], ks=(0.2, 0.2, 0.2)),
        'grey': wings_material(mats['default9'], ks=(0.4, 0.4, 0.4), pm=1, pr=0.4),
        'brown': wings_material(mats['default3'], ks=(0.1, 0.1, 0.1)),
        'wood': wings_material(mats['default2'], ks=(0.1, 0.1, 0.1)),
        'dark': wings_material(mats['default7'], ks=(0.2, 0.2, 0.2)),
        'roof': wings_material(mats['default10'], ks=(0.1, 0.1, 0.1)),
        'glass': wings_material(mats['default5'], ks=(0.6, 0.6, 0.6), pr=0.1)}, height=12.0, half_turn=True),
    # Three posts with a chain between them, half a cell long as the fence
    Prop('chain', 'chain.wings', chain, lambda mats: {
        'base': wings_material(mats['default4'], ks=(0.2, 0.2, 0.2)),
        'red': wings_material(mats['default5'], ks=(0.3, 0.3, 0.3), pr=0.5),
        'white': wings_material(mats['default3'], ks=(0.3, 0.3, 0.3), pr=0.5)}, length=4.5, extra=chain_tubes),
]


def build(prop):
    objects, mats = read(os.path.join(SRC, prop.file))
    palette = prop.materials(mats)
    parts = []
    for name, ws in objects.items():
        for winged in ws:
            pts, corners = mesh(winged)
            uvs = face_uvs(winged)
            # mesh() and face_uvs() leave out the same faces (those without edges), in the same order
            for (loop, material, normals), f in zip(corners, sorted(uvs)):
                face = {'material': material, 'points': pts[loop], 'centre': pts[loop].mean(0), 'uvs': uvs[f]}
                kind = prop.rule(face, mats)
                if kind is not None:
                    parts.append((pts[loop], normals, kind))
    if prop.extra is not None:
        parts += prop.extra()
    every = np.vstack([p for p, _, _ in parts])
    lo, hi = every.min(0), every.max(0)
    scale = prop.length / (hi[0] - lo[0]) if prop.height is None else prop.height / (hi[1] - lo[1])
    # The foot at the origin: the bottom at y = 0, the middle of the base in x and z where the model has it, or the
    # middle of its box
    offset = np.array([-(lo[0] + hi[0]) / 2 if prop.centre else 0.0, -lo[1], -(lo[2] + hi[2]) / 2 if prop.centre else 0.0])
    # Half way round about y: x and z change their signs
    turn = np.array([-1.0, 1.0, -1.0]) if prop.half_turn else np.ones(3)

    positions, normals_out, lines = {}, {}, []
    def index(table, value, prefix):
        key = tuple(round(c, 5) + 0.0 for c in value)
        if key not in table:
            table[key] = len(table) + 1
            lines.append(prefix + ' %.5f %.5f %.5f' % key)
        return table[key]
    current = None
    faces = 0
    for name in palette:
        for P, N, mat in parts:
            if mat != name:
                continue
            if mat != current:
                lines.append('usemtl ' + mat); current = mat
            refs = [f'{index(positions, (p + offset) * scale * turn, "v")}//{index(normals_out, np.asarray(n) * turn, "vn")}' for p, n in zip(P, N)]
            lines.append('f ' + ' '.join(refs))
            faces += 1
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, prop.model + '.obj'), 'w', newline='\n') as obj:
        obj.write(f'# Scenery: {prop.model}, built from art/wings/Scenery/{prop.file} by tools/garagegen/scenery.py\n'
                  f'mtllib {prop.model}.mtl\no {prop.model}\n' + '\n'.join(lines) + '\n')
    out = [f'# Materials of {prop.model}; Pm and Pr: metal and roughness (tools/ContentPipeline)', '']
    for name, m in palette.items():
        out += [f'newmtl {name}', 'Kd %.4f %.4f %.4f' % tuple(m.kd), 'd %.4f' % m.d, 'Ks %.4f %.4f %.4f' % tuple(m.ks),
                'Ke %.4f %.4f %.4f' % tuple(m.ke), 'Ns 0.3000']
        if m.pm is not None: out.append(f'Pm {m.pm:g}')
        if m.pr is not None: out.append(f'Pr {m.pr:g}')
        out += ['illum 2', '']
    with open(os.path.join(OUT, prop.model + '.mtl'), 'w', newline='\n') as mtl:
        mtl.write('\n'.join(out))
    size = (hi - lo) * scale
    print(f'{prop.model}: {faces} faces, scale {scale:.4f}, size x {size[0]:.2f} y {size[1]:.2f} z {size[2]:.2f}')


for prop in PROPS:
    build(prop)
