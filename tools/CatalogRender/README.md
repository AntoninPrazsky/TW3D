# Catalog images

Renders images of track blocks for the block catalog (`docs/catalog/bl`) the way the
2016 images look: the block with the selector of the level editor, in the editor's
lighting, on a gray background, 481 × 477 pixels.

```sh
# writes docs/catalog/bl/<block>.png
dotnet run --project tools/CatalogRender -- render kea1e c3f
dotnet run --project tools/CatalogRender -- render --out some/folder kea1e

# compares renders of all blocks in content/catalog.json with their catalog images
dotnet run --project tools/CatalogRender -- check
```

A new block also needs a row in `docs/catalog/catalog.html`.

## The camera

The 2016 images were taken in the level editor with a camera moved by hand, and its
position was not saved. It was found again by fitting renders of the selector (the green
corners) to the catalog images: the edges of the selector are 0.14 px apart on average.
Renders of the blocks then differ from their 2016 images by about 8–16 (RMS of the colour
channels, 0–255), as those were drawn by XNA. `check` reports an image differing much
more as showing another block; that is how the images of `kea1e` (showed `kea1d`) and
`c3f` (showed `c2f`) were found and replaced by renders.

The image of `lo` was taken from another position, as the block reaches out of its cell,
so `check` does not compare it.

Dynamic objects (`docs/catalog/obj`) were taken with other cameras and are not supported.
