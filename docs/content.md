# Game content

## 3D models

The 3D models are in `content/` as `.obj` + `.mtl` files (Wavefront OBJ, a text
format that Blender and most 3D programs open and save). The MonoGame Content
Pipeline builds them into `.xnb` files whenever the game or
CatalogRender is built: `content/Content.mgcb` lists them, and the projects run it
through the `MonoGame.Content.Builder.Task` package. The models have no textures,
they are made of coloured polygons only (the colour is the diffuse colour `Kd` of
the material). There are no textures of the scene at all: the sky is computed by the
shader (see [the sky](architecture.md#the-sky)).

The materials of metal parts also have the metallic `Pm` (0–1) and the roughness `Pr`
(0–1, 0 = a mirror) of the PBR extension of MTL. The importer of MonoGame leaves them
out (it reads only what XNA's `BasicEffect` has), so the models are processed by
`MtlModelProcessor` of `tools/ContentPipeline`, which reads them from the `.mtl` file
and keeps them with the model. It also marks the lamps, by the names of their
materials: the lenses of the headlights of the cars are named `headlight…`, of the rear
lights `taillight…`, of the street lamp `streetlight…`, the start lights of the start gate
`sg` `signalred…`. Each lamp is an object of its own in the `.obj` file (`HeadlightLeft` …,
`lens`, `SignalRed1a` …), the game finds its middle as the middle of the object; the start
lights one above the other (`SignalRed1a` and `SignalRed1b`) are a pair, lit together.
The parts the player paints in the garage are marked the same way: the two paints of a car
`primary…` and `secondary…`, the rims of a wheel `rim…`. The game draws them in the colours
chosen (`Graphics/Paint`), or in their own colour (`Kd`) for *Original*; they keep their
metal and roughness. A new car or new wheels name their paint and rims so to be coloured. The
windows of a building that are lit at night are `windowlit…` (the judges' stand): they shine
evenly, without a glow, and light nothing, so they need no objects of their own.

A part can also be a **surface**, whose detail the shader draws per pixel (see
[surfaces](architecture.md#surfaces)): a material named `asphalt…`, `earth…` or `grass…` (the names of
`Graphics/Surface`). Its colour (`Kd`) is the base colour of the surface, which the shader varies
by the noise of the surfaces and lights with a relief of its own; the shape stays that of the
model. The looks of the off-road style are earth (`earth`, `earthrut`, `earthbank`, see
[the catalog](#catalog)); the racing style (#56) will bring asphalt. The development key T
draws the grey road of the classic blocks as the test surfaces, and `tools/CatalogRender`
renders a block so (`render --surface asphalt`).

The processor turns the normal of a vertex that faces away from the faces it belongs to (most of
the faces of its geometry decide which way they are wound): a few old models had faces drawn from
outside with their normals pointing into the model, lit as their backs are, which the matt mud
on the car showed as lighter patches.

The models of the dynamic objects are built with the parameter `Surface` of the processor:
it keeps their triangles in the model (its `Tag`), as the game cannot read the vertices back
from the graphics card on Android. The skid marks of the tyres lie on that surface
(`Graphics/ModelSurface`): where a mark grows, rays across the tyre find the model under it, so
a mark follows the rims of a barrel and ends where the barrel ends. A new object model gets it
by being listed with the others in `content/Content.mgcb`.

The models were modelled in Wings 3D in 2014–2015 and exported to DirectX `.x`
files, which XNA 4.0 compiled for the 2016 version. In 2026 those exports were
converted to OBJ: the same polygons, normals and material colours, in the
right-handed coordinates of the game (DirectX `.x` is left-handed, the XNA importer
mirrored the z axis). Builds of the converted models were checked against the
2016 `.xnb` files: the same surfaces and colours, they look the same. The
original Wings 3D files are in [`art/wings/`](../art/wings/README.md).

The gates are modern since 2026: the start gate `sg` and the finish gate `g`, each with the
sign above it in the cell above (`st2` with the lettering START, `st` with a chequered panel),
built by `tools/blender/gates.py` (Blender 4.2, `blender -b --factory-startup --python
tools/blender/gates.py`; run it after changing it and commit its output). It is a truss gantry as
on the race tracks: two round-chord lattice legs and a lattice beam across the road, joined at
the top of the legs by cast aluminium corners (flanges with bolts where the lattice joins, an
access hatch on the outside, plates on the front and the back, a lifting eye on top) with a knee
brace under each. A leg stands on a yellow pedestal with black straps on a base plate with anchor
bolts; the lattice is bolted to a steel mounting plate on the pedestal, each chord in a sleeve with
a rib. A cable conduit runs up one leg and along the beam. The start gate
has a black light housing on the front of its beam held by straps, and in it the start lights;
the finish gate has a photo-finish camera under the middle of its beam. 12 400 triangles for the
start gate, 9 400 for the finish gate, 6 600 and 6 800 for the signs (these are not meant to be
as light as the old ones: they are four blocks in a level). The gate stands 1.2 from the edge of
its cell, as the old one did, with its legs at |z| = 7 beyond the lane of the cars, and the front
(the start lights, the sign) towards +x, where the car comes from.

The start lights are five columns of two red lamps, each lamp a dome lens in a bezel under a
hood and an object of its own named `SignalRed<column><a|b>` in the material `signalred`, which
the game lights column by column (above). The lamps and everything else of a gate in the lane
hang above y = 2.4 (the beam, the lamps, the camera), so nothing of it is in the way of a car. The
sign is a black panel in an aluminium bezel with a cap on each corner, held between two lattice
posts under a lattice beam with three flood lights. The lettering START is that of the Wings
model of 2015 (`tools/blender/start_letters.json`, taken from `art/wings/Levels/mod/st2.wings`),
a little larger and with a raised edge; the chequered pattern of the finish sign and the two
waving chequered flags on its corners are made by the script. The gate and its sign are two
blocks, as they always were, so no level changed.

One block is modern since 2026: the railing `za`, a decoration of the layers behind the
road (no collision), built by `tools/blender/railing.py` in the colours of the street lamp
and the fence (below), about 1800 triangles. It is a glass railing: two square dark grey
posts on base plates with bolts under yellow caps, a light grey top rail over them, and a
see-through glass panel held by clamps in two light grey rails, the lower one with a yellow
stripe. Each piece is a span of its own, a little short of the edges of its cell, so a row
of them is a row of spans with small gaps (the owner's choice); it stands at the near edge
of its cell, its foot on the top of a road block, 4.6 high, as the railing of 2014–2015 (a
bent grey tube) did.

## The garage

The garage behind the menus (`content/Garage/`) is 3D since 2026; in 2016 it was a
picture rendered from the garage scene of 2015 (`art/wings/Garage/garage.wings`). The
hall is new, generated by `tools/garagegen/build.py` (Python 3 with NumPy): the floor, the
walls, the garage door and the lines of the parking bay. From the Wings 3D files it takes
the windows, the lift, the furniture and the tools, and the poles outside, with smooth
normals by the hard edges of Wings and metal materials where there was metal. The
windows look out at the sky of a day, which the game draws behind the hall. Run the script after
changing the `.wings` files or the script and commit its output.

The car chosen in the garage stands on the lift facing the camera, the other cars in a
row along its axis in the order of the catalog (the next ones behind it). The menus look
into the garage as the picture of 2016 did; the garage screen moves the camera to the car,
and to the wheels of the axle being chosen.

The main menu shows the 3D logo of the game (`Garage/logo.obj`) in the top left corner,
instead of the title in text: the lettering "TripleWheel3D" of the 3D logo modelled in
August 2015. `tools/garagegen/logo.py` builds it from `art/wings/Garage/logo.wings`, leaving
out the emblem of three wheels beside the lettering and the plate it was rendered on: one
unit high, its middle at the origin, the face of the letters white with dark sides. The
garage draws it lit by
its light, but placed against the frame rather than in the hall (where the main menu lays
it out, so that it keeps its place whatever the camera and the shape of the window) and
over everything else, turned a little towards the middle of the frame and swaying; it slides up out of the frame when another screen
covers the main menu, as the titles of the other menus do.

## The cars

The Buggy (`car`, 2014) was modelled in Wings 3D ([`art/wings/`](../art/wings/README.md)). The
Sand Rail (`car2`, a single-seater racer of 2014, remodelled in 2026) is red, built by
`tools/blender/car2.py` (Blender 4.2, `blender -b --factory-startup --python
tools/blender/car2.py`), 10 006 triangles: a red tube frame with open ends, two faceted sidepods in
the manner of a formula car either side of an open cockpit (a wide inlet in front, the widest part
ahead of the middle, an undercut below it and a taper to the tail; vents on the flank and a line of
the secondary colour), a low rounded windscreen wrapped round the front of the cockpit on the dash
in a frame of the red tubes, a red
roll hoop behind the seat with two stays to the hoop of the tail frame and a tonneau between
them; in the cockpit the seat on the floor, the steering wheel on a column that goes into the dash
and the pedals held by a plate on it; the blown engine with its red stacks in front of a finned
radiator in a frame open at the front, a splitter, coil-overs at both axles with white springs,
two pipes along the sills to the tail, a battery box in the open tail frame under a louvred deck,
and two swept fins carrying the wing. (It had a glass canopy with white arches over the cockpit
for a while, which did not belong to the frame.) The red frame, the pods and the fins are its
`primary` paint, the wing and the strips its `secondary` one (#46). The engine with its stacks,
the seat, the steering wheel with the pedals and the battery box are the parts of the car of
2014/15, which the script takes from `tools/blender/car2_parts.obj` (cut from the old
`content/car2.obj`) and moves into the new cockpit; the rest is new. Its lamps are objects of
their own: slim headlights either side of the radiator, taillights on the endplates of the wing. The third car, the Crawler (`car3`, 2026), is a blue
rock crawler built by `tools/blender/car3.py` (Blender 4.2, `blender -b --factory-startup
--python tools/blender/car3.py`), 13 106 triangles, a little more than the Sand Rail: an
exoskeleton cage of blue tubes over two seats, a short blue hood with the turbo standing out
of it, a tube bumper with a winch, rock sliders along the sides, coil-overs with orange
springs at both axles, and a rack at the back with the fuel cell and a spare wheel standing
across the car under an orange strap. Its hood and body panels are its `primary` paint, the
cage its `secondary` one (#46). Its engine with the turbo and the cooler, the seats, the
steering wheel, the pedals, the gear lever and the fuel cell are the Buggy's parts, which the
script takes from `content/car.obj` and recolours; the spare is its own wheel, the rock
crawler wheel, with fewer triangles and its hubcap orange. Its lamps are objects of their own:
round headlights beside the grille, taillights on the rear bumper.

The Crawler comes with the third wheel of the garage, the rock crawler wheel (`wheel3`, #54),
built by `tools/blender/wheel3.py`, 6112 triangles: a tall, wide tyre with a deep chunky
tread of big lugs from each shoulder to past the middle, slanting and offset from side to
side (the same both ways), each lug one solid piece, highest in the middle of the tread and
set on the rounded shoulder, with stepped shoulder blocks on the sidewalls; the wheel under an
orange hubcap (`rim`, so its colour is chosen in the garage) with three slanted dark ribs, the
hub and the sixteen bolts of a beadlock round it. It is the size of the other wheels (the radius
0.705 of the tread, within 1.42 ≤ |z| ≤ 2.18 at the side of the car), so it fits any car; its
friction and mass are those of the off-road wheel (1.2 and 10), the most the garage's bars
show, as the owner chose to keep the bars of the other wheels as they are.

The paint of the Buggy is its body (`primary`) and its roof panel (`secondary`); of the Sand
Rail its red frame (`primary`) and the arches of the frame over the canopy (`secondary`); the
rims of the off-road and the racing wheel are `rim`. The materials were renamed in 2026 (#46) from the names Wings
3D gave them (`default3`, `default2`, `default11`, `default6`), and the roof and the arches
split off as their own material; the faces and the shapes are the same.

All cars share the chassis (`GameObjects/Car.cs`): the holders of the wheels turn about pivots
3.6 model units in front of the middle of the body and 3.0 behind it, 1.25 below it, so a
body keeps clear of the holders' plates and the wheels as they turn. A car collides with the
outline of its whole model (see the [collision shapes](#collision-shapes)), so the Crawler is
no lower and no higher than the Buggy (1.21 from the middle; its axles pass a little above
the pivots) and fits where the Buggy fits. Run the script after changing it or `car.obj` and
commit its output.

## Scenery

The scenery (`content/Scenery/`) are static props that stand beside the track, placed in the
level editor (the Scenery tool) and drawn with the layers, without physics: the car drives
through them. The traffic cone (`cone`) is a prop the owner released on OpenGameArt in
2016; the street lamp (`lamp`), the metal fence (`fence`), the judges' stand (`stand`), a race
control building for the start or the finish, and the utility pole (`pole`) are modern ones
of 2026 (below); the other three the owner made for the game in March 2018: the guardrail
(`guardrail`), the thick guardrail with red and white stripes (`guardrail2`) and the posts with a
chain (`chain`).

`tools/garagegen/scenery.py` (Python 3 with NumPy) builds them from their Wings 3D files in
`art/wings/Scenery/`, with smooth normals by the hard edges of Wings, at the scale of the
blocks: the cone is 1.6 model units high (the car is 2.4); a guardrail is a cell long (9), so that the pieces in a row along
a straight road join, and a piece of the fence and of the chain half a cell (4.5), two pieces
to a cell. The foot of every prop is at the origin of its model, and the long side of the
fence, the chain and the guardrails runs along x, along the road; the rails of the guardrails
face the camera (-z), so that behind the road they face the road, as the modern props below do.

The street lamp is a modern LED street light in the colours of the blocks, built by
`tools/blender/streetlamp.py` (Blender 4.2, `blender -b --factory-startup --python
tools/blender/streetlamp.py`), about 3200 triangles: a tapered dark grey pole on a base with
a flange and bolts, a yellow band and a service door, an arm bending out over the road and a
light grey head with cooling fins and yellow strips, tilted up a little; its foot at the
origin, 8.7 high, the head 3.5 towards the camera (-z), so that a lamp behind the road leans
over it. The lens under the head is an object of its own with the material `streetlight…`:
at night it shines in its emissive colour with a glow, and the four street lamps nearest to
the camera light the scene below them as spot lights (`SceneRenderer`); by day they are off.

The metal fence is a modern welded mesh fence in the colours of the blocks and of the lamp,
built the same way by `tools/blender/fence.py`, about 3600 triangles: panels of dark grey wire
with three V-shaped bends along them, held by clamps to square dark grey posts, each post on a
base plate with bolts and under a yellow cap; 3.5 high. A piece has half a post at each end,
cut along the end of the piece: two pieces of a row make one whole post where they meet, and a
row ends with half a post at either end. It looks the same from both sides, so it can stand
behind the road, in front of it and across it.

The judges' stand is a modern race control building in the same colours, built by
`tools/blender/stand.py`, about 4500 triangles: a light grey ground floor on a dark grey base
with a glazed entrance in the middle of its front, a dark grey terrace slab over it with yellow
edges, out over a porch on four columns, with a glass balustrade (the see-through material
`glass`); on the terrace a control room whose glazed front leans out like that of an airfield
tower, under a dark grey canopy with a yellow line, the air conditioning and a radio mast on
the roof; an open steel stair with yellow nosings up the side (on the right as seen from the
road). It keeps the footprint of the stand of 2018 (25.7 × 12.9) and its height (12 with the
mast), its foot in the middle of the footprint and the terrace towards the camera (-z). Its
windows are `windowlit`: light blue glass by day, lit warm from within at night.

The utility pole is a modern power pole built by `tools/blender/pole.py`, about 3900
triangles: a slim tapered dark grey steel pole on a base plate with bolts and a sleeve, a
yellow band low on it and step bolts up its sides, a light grey crossarm with yellow ends on
two braces at the top with four light blue insulators standing on it, and halfway up a light
grey pole transformer with dark grey fins, two bushings and a yellow plate, which shows what
the pole is also from the end of its crossarm. It is 20 high (over two cells), the crossarm 9
long along x, its foot at the origin, as the pole of 2016 was; the poles of the docks stand
behind the road turned across it.

The Blender scripts (also that of the Crawler) share the materials (`pole`, `head` and `accent`: the dark grey, the light grey and the
yellow of the blocks), the parts and the export in `tools/blender/props.py`; run a script after
changing it and commit its output.

The chain was modelled link by link (some 3400 faces); `scenery.py` puts a tube of red and
white pieces along the curve it hangs in instead, which looks the same from the camera of the
game and leaves the posts with a chain 835 faces. Run `scenery.py` after changing the `.wings`
files or the script and commit its output.

## Images and fonts

The content pipeline builds the images from PNG files:

- the images of the user interface (`Common/`: the logo of the start, the pointers, the
  arrows and the on-screen controls). They were precompiled `.xnb` files of XNA 4.0 whose
  sources were lost; in 2026 they were loaded and written as PNG, their colours no longer
  premultiplied by alpha (the pipeline premultiplies them again, which gives the same
  pictures to a rounding). Three images nothing drew any more were left out. The logo of
  the start was white on an opaque near-black background, which showed as a dark rectangle
  while the logo faded into the main menu; its background is transparent since 2026, the
  alpha of every pixel taken from its brightness and its colour the white of the logo, so
  that on black it looks as it did.
- the previews of the levels in the level select (`Levels/levimgs/*.png`, 700 × 394):
  screenshots of the track alone, taken in a Debug build with H, which hides the car and
  everything over the level while driving. The previews of 2016 were taken again in 2026,
  of the same places, when the sky changed.

The content pipeline builds the fonts from TrueType files:

- the font of the menus, the titles and the display of a run (`Fonts/menuFont<size>.spritefont`):
  **Triple Wheel Display** (`Fonts/TripleWheelDisplay.ttf`, SIL Open Font License 1.1, see
  [its licence](../content/Fonts/TripleWheelDisplay-OFL.txt)), a version of
  [Orbitron](https://github.com/theleagueof/orbitron), which the font of 2016 (a precompiled
  `.xnb` of capitals and ASCII only, whose font is not known) was close to.
  `tools/fontgen/menufont.py` (Python 3 with fontTools) makes it from
  `art/fonts/Orbitron[wght].ttf`: the ExtraBold weight and the Czech letters Orbitron lacks
  (Ě Č Ř Ů Ť Ď Ň and ě č ř ů ť ď ň, built from its own letters and its caron and ring; ť and
  ď with the caron as an apostrophe); the licence reserves the name Orbitron, so the modified
  font has another. The menus, their items and the titles (and the big Finish and Game over) are written
  in capitals, as in 2016; the other texts (the tutorial, the results, About) in small letters
  too. The script writes the `.spritefont` files too, in four sizes (20, 40, 80, 160): the
  game draws a text from the smallest size at least as large as the text is on the screen
  (`Screens/GameFont.cs`), so that large texts and high resolutions are sharp, and measures
  the layouts in the size 40, whose capitals are 38 pixels high as before. The sizes up to 80
  have every character the font has among ASCII, the Latin letters with accents, the dashes,
  the quotes and the euro; the size 160 only the digits, the capitals and a little
  punctuation, so that its texture stays within 2048 pixels on phones. A character the font
  lacks (such as `^` or `©`) is drawn as `?`.
- the glyphs of keys and buttons on the tutorial cards: `Fonts/promptFont.spritefont`, from
  [PromptFont](../content/Fonts/PromptFont-OFL.txt) (`Fonts/PromptFont.ttf`, SIL Open Font
  License 1.1), only the glyphs it lists.
- the frame rate counter: `Fonts/frameRateCounterFont.spritefont`, from Triple Wheel Display
  too (it was a precompiled `.xnb` of Arial until 2026), only the digits and the letters it
  writes.
- the font of the debug view of Aether.Physics2D, which the development keys of a Debug build
  show (see [Playing](playing.md)): `Fonts/diagnosticsFont.spritefont`, from
  [DejaVu Sans](../content/Fonts/DejaVuSans-LICENSE.txt) (`Fonts/DejaVuSans.ttf`, the font of
  the level editor), built as `DiagnosticsFont`, the name the debug view loads it by (it was a
  precompiled `.xnb` of Arial until 2026).

Nothing of the content is precompiled any more: every image, model, effect and font is built
from its source in the repository.

## Editing models

In Blender: *File → Import → Wavefront (.obj)*, edit, then *File → Export →
Wavefront (.obj)* over the original file. The default settings work: the import
converts the axes (OBJ has Y up, Blender Z up) and the export converts them back.
Keep the export of normals and materials on and *Triangulated Mesh* off (the
content pipeline triangulates the polygons itself); turn *PBR Extensions* on, so that
the metallic and the roughness of the materials are kept. The next build of the game
builds the changed model and generates its collision shape again (commit
`content/collision.json` with the model).

`tools/blender/decimate.py` simplifies one material of a model (the tyre of a wheel) to a
number of triangles in Blender, without its window, and leaves the other materials as they
are; the Racing wheel (`wheel2.obj`) was made lighter by it:

```sh
blender -b --factory-startup --python tools/blender/decimate.py -- content/wheel2.obj out.obj default2 7000
```

Its `.obj` replaces the model's (with the `mtllib` line of the model's `.mtl`, which stays
as it is).

## Catalog

`content/catalog.json` lists everything the levels are built from, and the cars, for both
the game and the editor:

- **blocks**: the name (model `Levels/mod/<name>`), what the block is in a few words
  (`title`, shown by the Palette and the Inspector of the editor and on the catalog page),
  its `group` (the Palette and the catalog page show the blocks in these groups, in the
  order they first appear in the catalog, so the blocks of a group stand together) and
  whether the block has a collision shape (`collision`). Only blocks with a collision shape can be in the
  middle (physics) layer; the others are decoration for the back layers (up to three cells
  behind the road) and the front layer.
  A road block also has a `profile`, `[left, right]`: the height of the road surface
  at the left and the right edge of the block relative to its base, in shift steps
  (2.5 physics units; heights grow downward, as physics y does). For example `b2`, a
  descent, is `[0, 4]`: it starts at its base and ends 4 steps below it. The editor joins
  the blocks of a road with it (road mode) and marks the gaps, and the level generator
  `tools/levelgen` builds its roads from it. A block without a profile (the decoration,
  the gates and walls) is not part of the road.
  A block with `"turns": true` can be turned round by 180° about the vertical axis in a
  level (`"turned": true`, see [the level format](levels.md#level-format)): the game draws
  its model turned, mirrors its collision shape left to right and swaps its profile, so `b2`
  turned is a climb, `[4, 0]`. The slopes, the crests and bends, the road ends, the stairs `s1` and
  `s2`, the steps `k1` to `k3` and the walls `t1` and `u1` turn;
  so one model serves both directions, where the 2016 version had a mirrored model of each
  (`b2f`, `t2`...; their Wings 3D originals are still in `art/wings`). A tunnel or a
  decoration turned would show its back, so it does not turn; the barriers `bl2f` and
  `bl2sf` are mirror images of `bl2` and `bl2s`, not their turns, and stay blocks of their
  own. A new block that should run both ways needs one model with `turns`.
- **styles** and the **looks** of the blocks (#58): the styles of the track besides the
  classic one, `offroad` so far, and for a block the model of its look in each style, and in
  each transition between two styles, `first-second` (the first at the left edge of the block
  placed unturned): `"looks": { "offroad": "a1o", "classic-offroad": "a1t", "offroad-classic":
  "a1u" }`. A look is not a block of its own: it shares the collision shape, the profile and
  the turning of its block, only its model differs, so tools/CollisionGen makes no shape of it
  and the physics of a level is the same in every style. A block without a look in a style is
  drawn classic there (the start, the finish, the tunnels, the pits). The levels choose the
  style (see [the level format](levels.md#level-format)).
- **objects**: dynamic object types with their shape (`rectangle`, `circle`, or
  `model`: the collision shape of the model), size, friction, mass, restitution, the
  models they can use (`Env/<model>`) and what they are made of (`material`: `metal`, the
  default, `wood` or `plastic`), which decides how they sound when they hit something.
- **scenery**: the props beside the track: the name shown in the editor and the model
  (`Scenery/<model>`), by which the levels name the prop. They have no physics, so no
  collision shape.
- **cars**: the cars of the garage: the name, the model of the body, the wheels it comes
  with, the mass of the body, the top speed (how fast the motors turn the wheels at
  most, radians per second) and its engine: how much higher it sounds (`enginePitch`,
  octaves, 0 by default) and how fast it follows the wheels (`engineResponse`, 1 / seconds,
  3 by default): a sporty car sounds higher and revs up faster; and where its exhaust blows
  out (`exhaust`: the end of the pipe in the coordinates of the model, x to the back of the
  car and y up, one pipe on each side, at z and -z). All cars have the same chassis (the
  holders of the wheels, `holder`), so any wheels fit any car.
- **wheels**: the wheels of the garage: the name, the model, the friction of the tyre (its
  grip on the track, whose friction is 1) and the mass of one wheel of the physics (a pair of
  wheels, one on each side of the car), and its `grip` on the surface of each style, a factor
  of the friction of the contact (1 for a style it does not name, and on the classic slab).
  A style has the friction of its surface (`"styles": [ { "name": "offroad", "title":
  "Off-road", "friction": 0.9 } ]`; the classic slab's is 1). The physics gives a block the
  friction of the surface of its look (a transition the mean of both) and mixes it with the
  friction of the tyre as Aether.Physics2D does (the square root of their product), and the car
  scales the contact by the grip of the tyre on that surface (`Car.Grip`); the bars Grip of the
  garage show the same (`Catalog.ContactGrip`). On the earth (0.9): the off-road wheel ×1.1,
  the rock crawler ×1.2, the racing wheel ×0.75, so about 1.14, 1.25 and 0.62 against 1.10,
  1.10 and 0.87 on the slab (provisional, #57 tunes them by test drives).

The masses are relative to each other, not kilograms. The Buggy is about 3.8 m long and 1.9 m
wide (a model unit is about 0.44 m), so its body of 200 stands for some 550 kg of a real buggy
without its wheels. Its wheels are 0.62 m across and 0.32 m wide, light alloy rims with 25-inch
tyres: about 14 kg each off-road (knobby tyres) and 10 kg racing, so a pair of them weighs
10 and 7 in the catalog.

A new block, object, prop, car or wheel only needs its model in `content/` (and a `/build`
line in `content/Content.mgcb`) and an entry in the catalog. A wheel is built twice: as it is,
for the far side of a car, where it is turned around, and mirrored front to back (the
`Mirrored` parameter of the model processor) for the near side, so that the wheels on both
sides turn their tread the same way. The arrows of a directional tread (the off-road wheel's)
point to +x on top of the wheel in its model, so that they point forward, the way the wheel
turns, on both sides of the car.

The blocks added to the classic set since 2016 (#55) are made by `tools/blender/classic.py`
in the classic look: the outline of the block seen from the side swept straight across the road,
the road grey with the yellow hazard stripes along both edges as `a1` has them (parallelograms
0.9 long, slanted by 45°, in the same places along x, so that they run on from a flat road), the
risers, the bottom and the sides plain grey, and the materials of the classic blocks. So far
these are the steps of #61, approved by the owner on 2026-11-09: `k1`, `k2` and `k3`, a flat
road with a step down of 2.5, 5 and 7.5 units in the middle of the cell (turned, a step up),
and `s2`, stairs of two steps of 5 units with the drop of `s1`, each solid below its step down to
a sloping bottom, as `s1` is. Then the smoother roads of #62, approved on 2026-11-09: `b0`, a
slope of one shift step a cell (6.3°), with its S-curve `c0` and its sag curve `d0`; the crests
`e0` to `e3`, the flat road rounding over into the slope of `b0` to `b3` (`e1` to `e3` are
parabolas: they leave the flat road level and meet their slope at its angle, dropping 1, 2 and
3 shift steps); and the bends `e12`, `e23` and `e34`, a slope rounding over into the next
steeper one (`b1` into `b2`...), so that a crest longer than a cell is `e1`, `e12`, `e23`...
with the curve of `e1` all the way. A curved road is cut finer along x (0.45) and across its
edges, so that the stripes stay straight. And the road ends of #64, approved on 2026-11-09: `r1`,
`r2` and `r3`, the flat road from the left edge of the cell ending 2.7, 4.5 (half the cell) and
6.3 model units into it, with a band of hazard stripes across the end (turned, the road starting
after a gap), for gaps of any length and short ledges, in the place of the checkered `xa6` and
`xa7`; their profile is that of a flat road, the edge without road at the height a road would go
on at. Every block starts with the slab of a road one shift step thick, and but for a road end
ends with one, so that it meets the classic blocks flush. And the bridge of #63, approved on
2026-11-09: `br1`, a deck of road 0.4 thick on two steel trusses under its edges, for a bridge (its
top where a flat road's is, so placed at the shift of the road it joins it) or a low ceiling hung
over the road (placed by hand: it has no profile, so it does not count as road); `br2`, the head
of a lattice steel pillar, placed a cell (9 shift steps) under a deck, whose cap beam carries both
trusses; and `br3`, a piece of the column to stack under the head. The trusses stand outside the
bands tools/CollisionGen cuts the shapes from, so the deck alone collides; the pillar is as wide
as both bands (its posts in the band of the wheels, a thin web across the road in that of the
body), so all of a car hits it:

    blender -b --factory-startup --python tools/blender/classic.py

The looks of the off-road style (#57, approved by the owner on 2026-11-06) are made by
`tools/blender/offroad.py` from the classic road blocks (`a1`, the slopes `b1` to `b4`, `c1`
to `c3`, the lips `d1` to `d3`, the gentle `b0`, `c0` and `d0`, the crests `e0` to `e3`, the bends
`e12` to `e34`, the road ends `r1` to `r3`, the stairs `s1` and `s2` and the steps `k1` to `k3`): a cross-section of a dirt road swept
along the profile of the classic block, its cut at z = 0. The two ruts lie where the wheels of
every car run (|z| 1.42 to 2.17 model units, the wheel models are made there; the physics is
flat, so the wheels never leave them), their bottom on the profile, which is the collision;
the crown between them, the berms beside them, the shoulders and the grass verges are raised
straight up, so the ruts run on from block to block. The steps of the stairs are rounded, as
steps of earth wear (the higher steps a little more). The transitions sink the cross-section to the profile along the block
and end the grass, the earth and the ruts unevenly into the grey of the classic road. The
earth is drawn by the surface `earth` of the shader, the grass by the surface `grass` (short
blades, darker gaps between the clumps, drier yellower patches):

    blender -b --factory-startup --python tools/blender/offroad.py -- content/Levels/mod earth

Pictures of the blocks and objects are in `docs/catalog/`: open
[`catalog.html`](catalog/catalog.html) in a browser.

## Collision shapes

The physics shapes of the blocks, the dynamic objects and the cars are generated
from their models by [`tools/CollisionGen`](../tools/CollisionGen/README.md) into
`content/collision.json`: convex polygons, whatever of a model a car can touch
as it drives in the plane z = 0. The build of the game runs it whenever a model
or the catalog changes. The cars are up to 4.35 model units wide across the plane:
their bodies touch what is within |z| ≤ 1.49 of a block, their wheels at the sides
what is within 1.42 ≤ |z| ≤ 2.18, so the arch of a tunnel is lower for the wheels
than for the body, and the pillars of gates at the sides of the road are left out.

Until 2026 the shapes were traced from collision bitmaps (screenshots of the models
from Wings 3D, some edited by hand) every time a level loaded; they are kept as
images in `docs/catalog/bl/cols/`.
