# Catalog images

Renders images of track blocks for the block catalog (`docs/catalog/bl`) the way the
2016 images look: the block with the selector of the level editor (`Levels/selector`,
which the editor of the game draws at its cursor), in the lighting of 2016, on a gray
background, 481 × 477 pixels.

```sh
# writes docs/catalog/bl/<block>.png
dotnet run --project tools/CatalogRender -- render kea1e c3
dotnet run --project tools/CatalogRender -- render --out some/folder kea1e

# compares renders of all blocks in content/catalog.json with their catalog images
dotnet run --project tools/CatalogRender -- check

# the blocks drawn by the renderer of the game, their road as a test surface (asphalt, earth)
dotnet run --project tools/CatalogRender -- render --out some/folder --surface asphalt a1 b2

# the looks of the blocks in the styles of the track (docs/catalog/bl/looks/a1o.png, a1t.png, a1u.png ...)
dotnet run --project tools/CatalogRender -- render --out docs/catalog/bl/looks --looks a1 b1
```

With `--looks` the images are of the looks of the blocks in the styles of the track (the
catalog's `looks`, see `docs/content.md`), each named by the model of its look and drawn by the
renderer of the game as with `--surface`; the catalog page shows them with their blocks.

With `--surface` the image is not one of the catalog: the block is drawn by the renderer of the
game (`SceneRenderer`, the light of a day, without the shadows and the ambient occlusion,
whose render targets would take the place of the image's), the grey of its road as the
surface (`SceneRenderer.TestSurface`, see the surfaces in `docs/architecture.md`), with the
selector over it as usual.

A new block also needs a row in `docs/catalog/catalog.html` (and the row *Turns* when it has
`"turns": true`: a turned block has no image of its own).

## The camera

The 2016 images were taken in the level editor of 2016 with a camera moved by hand, and its
position was not saved. It was found again by fitting renders of the selector (the green
corners) to the catalog images: the edges of the selector are 0.14 px apart on average.
Renders of the blocks then differ from their 2016 images by about 8–16 (RMS of the colour
channels, 0–255), as those were drawn by XNA. `check` reports an image differing much
more as showing another block; that is how the images of `kea1e` (showed `kea1d`) and
`c3f` (showed `c2f`; the block is now `c3` turned and has no image of its own) were found and
replaced by renders.

The image of `lo` was taken from another position, as the block reaches out of its cell,
so `check` does not compare it.

Dynamic objects (`docs/catalog/obj`) were taken with other cameras and are not supported.
