"""Builds the 3D logo of the main menu from its Wings 3D file (art/wings/Garage/logo.wings, August 2015) into
content/Garage/logo.obj/.mtl: only the lettering "TripleWheel3D" of it (the emblem of three wheels beside it and the plate
it was rendered on are left out), one unit high, the middle of its box at the origin, facing +z.

The lettering is white on its face, as the items of the menus are, with dark sides, as the text title it replaced had
its black shadow.

    python tools/garagegen/logo.py

It prints the width and the depth of the logo (to its height), which the main menu and the garage place it by
(MainMenuScreen, GarageScene).
"""
import os
from scenery import Material, Prop, ROOT, build

SRC = os.path.join(ROOT, 'art', 'wings', 'Garage')
OUT = os.path.join(ROOT, 'content', 'Garage')
LETTERING = 'Curve.012'


def logo(face, mats):
    if face['object'] != LETTERING:
        return None
    return 'letters' if face['normal'][2] > 0.7 else 'letter_sides'


LOGO = Prop('logo', 'logo.wings', logo, lambda mats: {
    'letters': Material((0.95, 0.95, 0.95), (0.4, 0.4, 0.4), ke=(0.75, 0.75, 0.75), pr=0.35),
    'letter_sides': Material((0.06, 0.06, 0.06), (0.5, 0.5, 0.5), pr=0.3)}, height=1.0, middle=True)


if __name__ == '__main__':
    lo, hi = build(LOGO, SRC, OUT, 'The 3D logo of the main menu')
    print(f'width {hi[0] - lo[0]:.3f} and depth {hi[2] - lo[2]:.3f} (the height is 1)')
