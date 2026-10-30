# Collision shapes

Generates the physics shapes of the models into `content/collision.json`: the blocks
with `"collision": true` and the objects with the shape `model` in
`content/catalog.json`, and the cars of the catalog (their bodies and the holder of the
wheels).
The build of the game (`src/TW3D.Game`) runs it whenever a model, the catalog or the
generator changes; the file is in the repository, so commit it with the models.

```sh
# writes content/collision.json
dotnet run --project tools/CollisionGen

# also pictures of the shapes (SVG, one per model): the outline and its convex polygons
dotnet run --project tools/CollisionGen -- --svg some/folder
```

## How

The physics world is the plane z = 0 of the models, and the cars are up to 4.35 model
units wide across it. A shape is whatever of a model a car can touch while moving in
that plane: everything within a band of z, seen along the z axis.

- The bands come from the models of the cars, all of them together: their bodies (with
  the holders of the wheels) are within |z| ≤ 1.49, the wheels at their sides within
  1.42 ≤ |z| ≤ 2.18.
- A block gets one shape for each band. They are the same for most blocks (straight
  along z); where they differ, the body and the wheels collide with their own shape
  (`body` and `wheels` in the file, collision categories in the game): the arch of a
  tunnel is lower for the wheels than for the body, the plate `xa6` reaches further
  below the wheels than below the body.
- A dynamic object gets the shape within the whole width of the cars, a car the
  shape of its whole models.
- The part of a model within a band is the faces clipped to the band and the cuts
  through the model at the edges of the band (a block straight along z has no face
  in the band that would not be flattened to a line), joined by
  [Clipper2](https://github.com/AngusJohnson/Clipper2). Holes are filled (nothing
  moving in the plane can get into them).
- Neighbouring blocks touch exactly, as their models do. The 2016 collision
  bitmaps left gaps of 0.036 physics units between them (a rounding in the tracing),
  which the wheels rolled over more easily: on a straight road of blocks the car now
  accelerates about 15 % slower than in 2016.
- The outlines are split into convex polygons of at most 8 vertices by the same
  algorithm (Bayazit, Aether.Physics2D) as the game used for the outlines traced
  from the collision bitmaps.

The coordinates are those of the models; the game turns them into physics units
(`ModelCollision`: one model unit is -2.5 physics units).

## Compared with the 2016 bitmaps

Until 2026 the game traced its shapes from collision bitmaps, screenshots of the
models from Wings 3D (now `docs/catalog/bl/cols/`). The generated shapes cover the
same area as the bitmaps (94–100 % of the area in common) for the road blocks, the
car and the dynamic objects. They differ where the bitmaps were edited by hand or
taken from another view:

- the tunnel roofs `tu1v`–`tu3v`: the bitmaps had only a thin line under the roof;
  the body of the car meets the arch at the same height, the wheels lower;
- the gates `sg`, `g`, `st`, `st2`: the bitmaps had parts beyond the road
  (lamps, the pillars' tops), the shapes only what is above the road;
- `tu3s`: the bitmap was the mirror image of the model (below the road, no effect);
- `tb1`: the bitmap filled the space between the legs.

In a scripted drive (holding the right arrow) through the four levels, the car climbs
the same ramps as with the bitmaps, only slower (see the gaps above); the runs soon
diverge, as runs of a physics simulation do.
