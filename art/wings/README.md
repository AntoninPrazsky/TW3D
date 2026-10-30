# Wings 3D originals

The track blocks, the dynamic objects and the block selector of the level editor were
modelled in [Wings 3D](https://www.wings3d.com/) in 2014–2015. These are the original
`.wings` files, kept for reference; the game does not use them. The models the game is
built from are the `.obj` + `.mtl` files in `content/` (see "Game content" in the main
README), converted from the DirectX `.x` files exported from these files. Blender cannot
open `.wings` files; Wings 3D can, and exports OBJ as well.

The folders follow `content/`: `Levels/mod/a1.wings` is the original of
`content/Levels/mod/a1.obj`. Every file here lay next to its `.x` export, and that export
was the one the game used. Two files had other names:

| File | Original name |
|---|---|
| `Env/mov1.wings` | `movable1conc.wings` |
| `Env/tb1.wings` | `roadblock.wings` |

`Levels/mod/bl1s.wings` was saved 8 minutes after its export, so it may differ a little
from the model in the game.

`Levels/mod/tu1.wings`, `tu2.wings` and `tu3.wings` are the whole tunnels; the game uses
them split into the floor (`tu1s` …) and the ceiling (`tu1v` …), so they are not in
`content/`.

Not here: the vehicles (`car`, `car2`, `holder`, `wheel`, `wheel2`) and the sky. There are
several versions of their Wings 3D files, and it is not certain which of them were
exported for the game.
