"""Builds the 3D logo of the main menu from its Wings 3D file (art/wings/Garage/logo.wings, August 2015: the lettering
"TripleWheel3D" beside the emblem of three wheels, as the 2D logo of the start has them; the plate it was rendered on is
left out) into content/Garage/logo.obj/.mtl: one unit high, the middle of its box at the origin, facing +z.

The lettering is white on its face, as the items of the menus are, with dark sides, as the text title it replaced had
its black shadow; the wheels of the emblem have dark tyres and silver rims.

    python tools/garagegen/logo.py

It prints the width and the depth of the logo (to its height) and where the middle of the lettering is (a part of the height, from
the top), which the main menu and the garage place it by (MainMenuScreen, GarageScene).
"""
import os
import numpy as np
from scenery import Material, Prop, ROOT, build
from wings import read, mesh

SRC = os.path.join(ROOT, 'art', 'wings', 'Garage')
OUT = os.path.join(ROOT, 'content', 'Garage')
LETTERING, PLATE = 'Curve.012', 'Cube20'


def logo(face, mats):
    if face['object'] == PLATE:
        return None
    if face['object'] == LETTERING:
        return 'letters' if face['normal'][2] > 0.7 else 'letter_sides'
    return 'rim' if face['material'] == 'default11' else 'tyre'


LOGO = Prop('logo', 'logo.wings', logo, lambda mats: {
    'letters': Material((0.95, 0.95, 0.95), (0.4, 0.4, 0.4), ke=(0.75, 0.75, 0.75), pr=0.35),
    'letter_sides': Material((0.06, 0.06, 0.06), (0.5, 0.5, 0.5), pr=0.3),
    'rim': Material((0.85, 0.85, 0.85), (0.6, 0.6, 0.6), pm=1, pr=0.3),
    'tyre': Material((0.05, 0.05, 0.05), (0.1, 0.1, 0.1), pr=0.85)}, height=1.0, middle=True)


if __name__ == '__main__':
    lo, hi = build(LOGO, SRC, OUT, 'The 3D logo of the main menu')
    objects, mats = read(os.path.join(SRC, LOGO.file))
    every = np.vstack([mesh(w)[0] for name, ws in objects.items() if name != PLATE for w in ws])
    lettering = np.vstack([mesh(w)[0] for w in objects[LETTERING]])
    top, bottom = every[:, 1].max(), every[:, 1].min()
    middle = (top - (lettering[:, 1].max() + lettering[:, 1].min()) / 2) / (top - bottom)
    print(f'width {hi[0] - lo[0]:.3f} and depth {hi[2] - lo[2]:.3f} (the height is 1), the middle of the lettering {middle:.3f} of the height from the top')
