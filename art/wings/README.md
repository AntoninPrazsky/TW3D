# Wings 3D originals

The vehicles, the track blocks, the dynamic objects and the block selector of the level
editor were modelled in [Wings 3D](https://www.wings3d.com/) in 2014–2015. These are the
original `.wings` files, kept for reference; the game does not use them. The models the
game is built from are the `.obj` + `.mtl` files in `content/` (see
[Game content](../../docs/content.md)), converted from the DirectX `.x` files exported from these files. Blender
cannot open `.wings` files; Wings 3D can, and exports OBJ as well.

The folders follow `content/`: `Levels/mod/a1.wings` is the original of
`content/Levels/mod/a1.obj`. The blocks of the other direction (`b1f.wings` to `d3f.wings`,
`s1f.wings`, `t2.wings` and `u2.wings`) are their blocks turned round by 180°; since 2026-11
the game turns the block instead (`"turned"` in a level), so they are not in `content/`. The files of the blocks, the dynamic objects and the selector
lay next to their `.x` exports, which were the ones the game used. The vehicles were
modelled in several versions, often in files with other names, so their files (and the
sky's) were found by comparing them with the exports: each contains just the exported
object, with the same vertex positions. Where the files with other names come from (in
the old project folder `TripleWheel/`):

| File | Original file |
|---|---|
| `car.wings` | `tests/body.wings` |
| `car2.wings` | `NPG/vehicles/2/body.wings` |
| `holder.wings` | `tests/wheelHolderRightPositionNoWheels.wings` |
| `wheel.wings` | `tests/wheelRightPosition.wings` |
| `wheel2.wings` | `NPG/racingWheelRightScaleAndPos.wings` |
| `Env/mov1.wings` | `NPG/environment/movables/movable1conc.wings` |
| `Env/sky.wings` | `NPG/environment/skydome/sky.wings` |
| `Env/tb1.wings` | `NPG/environment/trackblock/roadblock.wings` |

`Env/sky.wings` has one face more than the exported sky: a face without a material. It is the dome
the game drew its sky on until 2026, with a picture; the sky is computed by the shader now, so the
dome is no longer in `content/`.

`Levels/mod/bl1s.wings` was saved 8 minutes after its export, so it may differ a little
from the model in the game.

`content/wheel2.obj` was changed in 2026: its rim is smaller, with a thicker tyre, and it
has the hub of `wheel.obj`, joining it to the holder; and its tyre was simplified from
17 220 triangles to 7 000 (`tools/blender/decimate.py`, the whole wheel 8 424 instead of
18 644), keeping the pattern of the tread. `wheel2.wings` is the wheel as it was.
`content/car.obj` and `car2.obj` got their lamps in 2026 (the objects `Headlight…` and
`Taillight…`), which `car.wings` and `car2.wings` do not have. The third car, the Crawler
(`content/car3.obj`, 2026), has no Wings file: `tools/blender/car3.py` builds it in Blender and
takes the Buggy's engine, seats, steering wheel, pedals, gear lever and fuel cell from
`content/car.obj`. The start gate
`content/Levels/mod/sg.obj` got start lights like those of Formula 1 instead of its traffic
lights: five black modules with a pair of red lamps each (the objects `SignalRed1a`, `1b` …
`5b`); `sg.wings` still has the traffic lights of 2015. The railing block
`content/Levels/mod/za.obj` was replaced in 2026 by a modern glass railing built by
`tools/blender/railing.py`; its `Levels/mod/za.wings` (a bent grey tube) is in the history of git.

`Garage/garage.wings` (September 2015, `NPG/garage/garage.wings`) is the garage scene the
picture behind the menus of 2016 was rendered from (in Kerkythea), `Garage/garage_no_car.wings`
(June 2016) its later version without the car, the lift and the poles, from which the
tool cart is taken. The game builds its 3D garage from them by `tools/garagegen/build.py` (see [the
garage](../../docs/content.md#the-garage)). Their room was made for one camera (some of its walls are
missing, some face outwards), so the script builds a new hall and takes only the windows,
the lift, the furniture and the tools from these files. The photos behind the windows
(`windowBgs/windBg.jpg`, a stock photo) are not in the repository; the game shows its
own sky there.

`Garage/logo.wings` (August 2015, `TripleWheel/logo/3Dlogo.wings`) is the 3D logo: the lettering
(`logotext3D.wings` there is the lettering alone) and the emblem of three wheels, on a plate it was
rendered on. `tools/garagegen/logo.py` builds the 3D logo of the main menu from it (`content/Garage/logo.obj`):
the lettering alone.

`Levels/mod/tu1.wings`, `tu2.wings` and `tu3.wings` are the whole tunnels; the game uses
them split into the floor (`tu1s` …) and the ceiling (`tu1v` …), so they are not in
`content/`.

`Scenery/` holds the props beside the track (see [the scenery](../../docs/content.md#scenery)):
one of the four the owner released on OpenGameArt in 2016 (`3D modely/3DModelsForOGA (2016)/` in the
owner's model archive, with OBJ and 3DS exports and renders), and three of the four made for the game in
March 2018 (`TripleWheel3D/2018 lokální verze (TW3D2018LOCAL)/3-2018/`). `tools/garagegen/scenery.py`
builds `content/Scenery/` from them. The others were replaced in 2026 by modern props built in Blender,
and their Wings files are in the history of git: the street lamp of 2016
(`StreetLamp/StreetLamp.wings`, textured by `lamp.png`) by `tools/blender/streetlamp.py`, the metal fence
of 2016 (`MetalFence/fence.wings`, textured by photos of rust in `fence.png`) by `tools/blender/fence.py`, the
utility pole of 2016 (`UtilityPole/UtilityPole.wings`) by `tools/blender/pole.py`, and the judges' stand of 2018
(`judges_stand_final.wings`) by `tools/blender/stand.py`.

| File | Original file |
|---|---|
| `Scenery/cone.wings` | `TrafficCone/TrafficCone.wings` |
| `Scenery/guardrail.wings` | `TripleWheel3D/2018 lokální verze (TW3D2018LOCAL)/3-2018/barrier.wings` |
| `Scenery/guardrail2.wings` | `…/3-2018/barrier_thick.wings` |
| `Scenery/chain.wings` | `…/3-2018/chains.wings` |
