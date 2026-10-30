# Wings 3D originals

The vehicles, the track blocks, the dynamic objects and the block selector of the level
editor were modelled in [Wings 3D](https://www.wings3d.com/) in 2014–2015. These are the
original `.wings` files, kept for reference; the game does not use them. The models the
game is built from are the `.obj` + `.mtl` files in `content/` (see "Game content" in the
main README), converted from the DirectX `.x` files exported from these files. Blender
cannot open `.wings` files; Wings 3D can, and exports OBJ as well.

The folders follow `content/`: `Levels/mod/a1.wings` is the original of
`content/Levels/mod/a1.obj`. The files of the blocks, the dynamic objects and the selector
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

`Env/sky.wings` has one face more than the exported sky: a face without a material.

`Levels/mod/bl1s.wings` was saved 8 minutes after its export, so it may differ a little
from the model in the game.

`Levels/mod/tu1.wings`, `tu2.wings` and `tu3.wings` are the whole tunnels; the game uses
them split into the floor (`tu1s` …) and the ceiling (`tu1v` …), so they are not in
`content/`.
