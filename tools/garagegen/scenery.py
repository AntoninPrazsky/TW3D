"""Builds the scenery (the static props beside the track) from their Wings 3D files (art/wings/Scenery) into
content/Scenery: an .obj + .mtl for each prop, in model units at the scale of the blocks (a cell is 9 units, the car is
8.6 units long), with the foot of the prop at the origin, y up and the long side of a fence along x (along the road).
Smooth normals by the hard edges of Wings, as the garage has them.

The street lamp and the metal fence were textured with photos of rust, which are not in the repository: the game
draws coloured polygons, so their faces get the colours the textures had there (sampled once and written down here)
by the rules below.

    python tools/garagegen/scenery.py
"""
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


# ---- the props: the file, the size, and the material of each face

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

PROPS = [
    # model, Wings file, height in model units, how the faces get their materials, the materials
    ('cone', 'cone.wings', 1.6, cone, lambda mats: {
        'orange': wings_material(mats['default2'], ks=(0.3, 0.3, 0.3), pr=0.5),
        'white': wings_material(mats['default3'], ks=(0.3, 0.3, 0.3), pr=0.5)}),
    ("lamp", "lamp.wings", 8.0, lamp, lambda mats: {
        'rust': Material((0.42, 0.21, 0.07), (0.1, 0.1, 0.1)),
        'rim': Material((0.58, 0.27, 0.15), (0.1, 0.1, 0.1)),
        'door': Material((0.33, 0.32, 0.30), (0.3, 0.3, 0.3), pm=1, pr=0.5),
        'glass': wings_material(mats['default2'], ks=(0.6, 0.6, 0.6), pr=0.1)}),
    ('pole', 'pole.wings', 20.0, pole, lambda mats: {
        'wood': wings_material(mats['default4'], ks=(0.1, 0.1, 0.1)),
        'metal': wings_material(mats['default'], ks=(0.4, 0.4, 0.4), pm=1, pr=0.45),
        'brace': wings_material(mats['default2'], ks=(0.3, 0.3, 0.3)),
        'insulator': wings_material(mats['default3'], ks=(0.4, 0.4, 0.4), pr=0.3)}),
    # Half a cell long: two pieces in each cell, end to end, make a fence along the road
    ('fence', 'fence.wings', None, fence, lambda mats: {
        'rust': Material((0.54, 0.25, 0.16), (0.1, 0.1, 0.1)),
        'steel': Material((0.45, 0.47, 0.48), (0.4, 0.4, 0.4), pm=1, pr=0.4)}),
]
FENCE_LENGTH = 4.5


def build(model, file, height, rule, materials):
    objects, mats = read(os.path.join(SRC, file))
    palette = materials(mats)
    parts = []
    for name, ws in objects.items():
        for winged in ws:
            pts, corners = mesh(winged)
            uvs = face_uvs(winged)
            # mesh() and face_uvs() leave out the same faces (those without edges), in the same order
            for (loop, material, normals), f in zip(corners, sorted(uvs)):
                face = {'material': material, 'centre': pts[loop].mean(0), 'uvs': uvs[f]}
                parts.append((pts[loop], normals, rule(face, mats)))
    every = np.vstack([p for p, _, _ in parts])
    lo, hi = every.min(0), every.max(0)
    scale = FENCE_LENGTH / (hi[0] - lo[0]) if height is None else height / (hi[1] - lo[1])
    # The foot at the origin: the bottom at y = 0, the middle of the base in x and z where the model has it
    offset = np.array([0.0, -lo[1], 0.0])

    positions, normals_out, lines = {}, {}, []
    def index(table, value, prefix):
        key = tuple(round(c, 5) for c in value)
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
            refs = [f'{index(positions, (p + offset) * scale, "v")}//{index(normals_out, n, "vn")}' for p, n in zip(P, N)]
            lines.append('f ' + ' '.join(refs))
            faces += 1
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, model + '.obj'), 'w', newline='\n') as obj:
        obj.write(f'# Scenery: {model}, built from art/wings/Scenery/{file} by tools/garagegen/scenery.py\n'
                  f'mtllib {model}.mtl\no {model}\n' + '\n'.join(lines) + '\n')
    out = [f'# Materials of {model}; Pm and Pr: metal and roughness (tools/ContentPipeline)', '']
    for name, m in palette.items():
        out += [f'newmtl {name}', 'Kd %.4f %.4f %.4f' % tuple(m.kd), 'd %.4f' % m.d, 'Ks %.4f %.4f %.4f' % tuple(m.ks),
                'Ke %.4f %.4f %.4f' % tuple(m.ke), 'Ns 0.3000']
        if m.pm is not None: out.append(f'Pm {m.pm:g}')
        if m.pr is not None: out.append(f'Pr {m.pr:g}')
        out += ['illum 2', '']
    with open(os.path.join(OUT, model + '.mtl'), 'w', newline='\n') as mtl:
        mtl.write('\n'.join(out))
    size = (hi - lo) * scale
    print(f'{model}: {faces} faces, scale {scale:.4f}, size x {size[0]:.2f} y {size[1]:.2f} z {size[2]:.2f}')


for prop in PROPS:
    build(*prop)
