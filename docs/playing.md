# Playing

The user interface is designed for desktop monitors at 1920×1080 and scaled
uniformly to the screen, so it fits any aspect ratio (16:9, 21:9, 4:3, …).
It keeps a margin of 48 px (at 1080p) from the screen edges; the level select
uses as many columns as fit the screen.

## Menus

Mouse (click, wheel and dragging in the level select), arrow keys and Enter, a
gamepad (D-pad, A) or touch. Esc (the gamepad Back button, the back gesture or
button of a phone) goes back; in the main menu it quits the game.

On a computer the main menu has a *Level editor* item in the top right corner, which opens
the [level editor](levels.md#level-editor) on the level edited last, or a new level (Esc
returns to the menu, asking first when there are unsaved changes). It saves the levels in
`%LOCALAPPDATA%\TW3D\Levels` (`~/.local/share/TW3D/Levels` on Linux), and its test drive
(F5) drives them. A phone and a Raspberry Pi have no editor.

## Garage

The cars to choose from, one at a time; dragging the car sideways (with a
finger or the mouse) or the arrows next to the name (or left and right) show the others.
The car shown is the one to drive. On the right its wheels are chosen, for the front and
the rear axle each (any wheels fit any car), and under each the colour of their rims; on the
left bars show how fast, heavy and gripping the car is with them, and under them the primary
and the secondary colour of its paint are chosen (up and down choose the row, left and right
or the arrows the wheels or the colour). The colours go round a palette of twelve, with
*Original* first, the colour the car was made in; a swatch shows each. The primary colour is
the body (the frame and the pods of the Sand Rail, the hood and the panels of the Crawler), the
secondary one the roof of the Buggy, the wing and the strips of the Sand Rail and the cage of the
Crawler; the colours are only for the looks. The cars
differ in their top speed and weight (the Sand Rail is faster and lighter than the Buggy,
the Crawler slower and heavier),
the wheels in their grip and weight (the off-road wheels grip better, the racing ones are
lighter; the rock crawler wheels, the Crawler's own, with a deep tread and an orange hubcap,
grip and weigh as the off-road ones). The grip depends on the surface too (#58): a bar for the
classic slab (*Grip classic*) and one for the earth of the off-road style (*Grip off-road*).
On the slab the off-road and the rock crawler wheels grip as they always did; on the earth
they grip better still, the rock crawler wheels best (the tall knobby tyre on loose ground),
while the racing slicks slide (about half the grip of the crawler there, not hopeless). The
level select marks the levels of the off-road style (*OFF-ROAD* at the top left of the
image), so that the wheels can be chosen for them; a transition between the two surfaces
grips as the mean of both. The numbers are provisional until test drives tune them (#57). On the earth the tyres throw it up (#59): clods and crumbs leave the
tread as it turns up from the ground and fly on the way the wheel turns, a rooster tail behind a
spinning wheel, pushed ahead of a wheel sliding braked, more the faster it slips, a few
from a wheel rolling fast, a spray where a wheel lands hard, with a brown dust instead of the
white smoke of the tyres, and the skid marks are fainter (a rut dug rather than rubber); fewer
pieces at a lower Surface detail. And the car gets muddy there (#60): first the tyres and the
wheels, then the lower part of the body around and behind the axles, then further up, the more
the longer it drives on the earth, the faster, and the more it slides, spins and lands hard. Off
the earth the wheels wash clean as they roll on (the treads in a few seconds, the rest of the
wheels in about twenty at full speed), and leave tracks of mud on the road and on the objects
until the treads are clean; the body keeps its mud for the rest of the run. Fresh mud is wet and
dark and dries lighter in about half a minute; the next earth makes it dark again. Every
run, and the garage, start with a clean car. The mud is not drawn at the low Surface detail.
The choices are saved with the options, the wheels and the colours for each car; a car whose
wheels were never chosen comes with its own.

## About

What the game is, who made it and the link to its source code on GitHub, which
opens in the web browser (clicked, or chosen by the keys and Enter).

## Options

Full screen or a window (on a desktop, also F11 anywhere in the game; the window opens
maximized and can be resized), the resolution of the 3D, antialiasing (the multisample antialiasing of the edges: on
by default, but on a Raspberry Pi; a switch comes with the next start of the game), the look of the game (new, or classic: as the 2016 version looked),
shadows, the detail of the shadows (low, medium or high: a shadow map of 1024, 2048 or 4096 texels; high by default on
a desktop, medium on a phone; a Raspberry Pi keeps low and does not offer the row), the detail of the surfaces of the
blocks (low: a plain colour with lighter and darker patches; medium: their stones, pebbles and relief too; high: the same
without the repeating of the fine pattern showing, and the relief of the mud on the car, which medium draws flat and low not at all; high on a desktop, medium on a phone, low on a Raspberry Pi, which does
not offer the row), ambient occlusion, the frame rate (frames per second in the top left
corner), the tutorial and the volume of the sound (in tenths; Enter turns it up, round to
off, left and right turn it down and up). A window was the cure when the game stuttered in
full screen with one graphics driver (an AMD Radeon with OpenGL, 2026). They are saved in
`TW3D/settings.json` in the user's local application data (on Windows `%LOCALAPPDATA%\TW3D`).

**Resolution** draws the 3D scene below the resolution of the screen, for a faster frame on
a weak graphics card, a large monitor or a phone: native, then 5/6, 2/3, 1/2 and 1/3 of the
height of the screen, none under 360 rows, each at the exact shape of the screen, so that the
picture scaled up to it is never stretched (on 1920 × 1080: 1600 × 900, 1280 × 720,
960 × 540 and 640 × 360; a 21:9 monitor gets 21:9 sizes only). Only the 3D gets coarser: the
menus, the time, the tutorial cards and the on-screen controls stay sharp. Enter steps down,
from the lowest round to native, and left and right step down and up. The row shows the size
drawn (`1280 × 720`, `1920 × 1080 (native)`); it is kept as a part of the height of the
screen, so a window of another size, or another monitor, gets the nearest size of its own. A
Raspberry Pi draws 720 rows at most until another resolution is chosen there. On a phone the
smaller 3D has no multisample antialiasing (MonoGame cannot resolve it there), so its edges
are rougher; on the vivo test phone the load of its GPU in the garage fell from about 62 % at
native to 42 % at 2/3 and 25 % at 1/3, at the same 60 frames per second. The level editor and
the previews of the levels (H while driving) stay at the full resolution.

## Sound

The sound effects are synthesized when the game starts, no recorded files: the menus (the
focus moving, an item chosen, going back), the countdown (a beep as each pair of start lights
comes on and the start an octave higher), the finish, a lost run (the chord broken downwards), the stars of the results one
after another (a major chord) and a new record, and the cars and wheels changing in the garage. They are pleasant and deep rather
than bright: struck bars (a marimba) and soft bongs in C major, low, with the octave below
for body, never a beep; nothing shrill above 5 kHz, but enough between 300 Hz and 4 kHz for
small speakers, which play little below. Mono. The notes are never nudged off their pitch,
so they stay in tune with each other; the noises and knocks play a little higher or lower
each time. `--sounds <folder>` writes them as WAV files to listen to (see
[Building](building.md)). The recipes are in `Audio/SoundBakes.cs`, the arithmetic in
`Audio/Synth.cs`.

The car sounds as it drives (`GamePlay/CarSounds.cs`): the engine, deep and unhurried (a slow
firing with a strong burble at half its rate), is two loops in step, idling and under the
throttle, crossfaded by the throttle and pitched by how fast the wheels turn (those on the
ground, which follow the car; all of them in the air, so wheels spinning in the air rev it),
following them. Each car has its own engine in the catalog: the Sand Rail, sportier, sounds
higher and revs up faster than the Buggy, the Crawler deeper and slower. The motor of the axles hums while they turn; the
lock of the axles clunks (softer as it opens); the brake grabs the wheels of a moving car.

The shock absorbers creak at each stroke of the springs of the axles (`GamePlay/SpringCreak.cs`,
from `Car.SpringSpeed`): metal sticking and slipping at two spots, the spring and its bush,
each ringing many close modes of its own metal. The slips follow the speed of the stroke: single
clicks run into a squeak of up to 400 a second and fall apart into clicks again; each slip comes
a little early or late and knocks harder or softer, the body of the car sounds them below and
the bush rubs between them. Eight takes, played as they are: never pitched (pitched by the
axle and the stroke, the creak sounded robotic in the game), and never two at once (the two
axles creaking together sang like a voice). A stroke is a spring moving one way faster than 2
physics units a second (a flat road, a car at rest or pushing against a step move them at
most about 1); it creaks once it has passed its fastest, the louder the faster it was, at its
loudest from 14 (humps move the springs 1 to 3.5, steps of the stairs and landings 4 to 22),
and that axle not again for 0.3 s. Even a soft creak plays at 0.6 of its volume, so that the
engine does not leave only its squeak to be heard; the owner then had the whole creak halved
(0.225), so even at its loudest it stays well under the engine under the throttle.

The tyres screech while they slip over what they stand on, the track or an object, faster
than 8 physics units a second (`Car.SlippingWheels`): braked wheels sliding (with the ABS only
short chirps), or wheels spinning at a launch or landing from a jump; louder and higher the
faster they slip. Where they
slip, they leave dark skid marks (`GameObjects/TyreMarks.cs`: under the wheels on both sides
of the car, darker the faster they slip, lying on what the wheel stood on and moving with it,
fading away after 20 seconds; on an object they lie on the surface of its model, over its rims,
and end where it ends, so they do not hang in the air past the ends of a barrel, which is shorter
than the car is wide), and slipping hard they smoke (`GameObjects/TyreSmoke.cs`: big
soft puffs rising and thinning out, their colour a greyed haze of the level's light). The car
has traction control: a wheel on the ground spinning faster than it rolls gets less torque, so
it grips instead of spinning (the motors never hold the wheels back). A wheel lifted off the
ground while the car stands on its others, like the front wheel of a triangle under the
throttle, does not spin up faster than it could on the ground either, so it does not screech
as it comes down again; only with the whole car in the air the wheels spin up freely. Only at a
launch the tyres may spin up for a moment, less the faster the car goes. A launch (the throttle
pressed with the car standing, not braked) is a burnout: the motors spin the wheels with all
their torque and the tyres grip little, so the wheels really spin, screeching and smoking,
while the car creeps; after about a second of spinning they grip again and the car shoots off.
Letting go of the throttle or braking ends it, and it does not come when the wheels do not
start spinning within two seconds (the axles turning instead, or the car against an obstacle),
so it does not get in the way of climbing. Braking ends the whole launch: let go of the brake
with the throttle held, the car pulls away gripping, without spinning its tyres, until the
throttle is let go or the car is going faster than the launch.

The brake (`Car.Brake`, the `Brake…` and `Abs…` constants) works against the turning of each
wheel with a torque that builds up over a moment (a sixth of a second of real time) and cuts
the drive of the wheels it brakes. It is strong enough to lock any wheel, but the car has ABS:
a wheel on the ground turning a fifth slower than it would roll is about to lock, so its brake
lets go until the tyre nearly rolls again, then grabs again a little softer than it locked and
builds up again. So the wheels lock only for a moment, several times a second, the tyres chirp
and leave dashed skid marks. A locked tyre grips only 0.8 of its grip, a rolling one fully, so
the ABS brakes better than locked wheels would; in tests on the flat the cars stopped 5–7 %
slower than with the brake before the ABS (2026-11), which held the wheels still and let the
tyres slide at their full grip. Below 6 physics units a second (about 5 km/h) the ABS is off: a
wheel the brake has stopped is then held still until the brake is let go, as before, so the
car stands on a slope and climbs stairs on braked wheels while the axles turn (the motor of a
held wheel turns the axle around it).

The axles, the triangles of three wheels, turn against the body (`Car`, the `Axle…`
constants). Let alone, they hold with a firm torque: the track turns them where it would lift
a wheel off it (a hump, a step, the start of a slope), but the motors of the wheels, small
bumps and wheels in the air do not, and they settle back on two wheels afterwards, so the car
does not stay standing on single wheels. ↑ ↓ turn them with a slow, strong motor, which stops
them when let go. Locked, they keep their angle in the world instead, and the body turns
freely around them.

The exhaust smokes too (`GameObjects/ExhaustSmoke.cs`): small bluish grey puffs blown out to
the back from the ends of the two pipes (`exhaust` in the catalog), thin idling, thicker under
the throttle and a burst when it is pressed; wispier than the smoke of the tyres, they thin
out quicker and hardly rise. Both kinds of smoke are clouds of `GameObjects/Smoke.cs`, each
with its own look.

The loops last a second and every part of them is a whole number of hertz, so they loop
without a seam; they pause with the game. Things hitting each other are heard too
(`GamePlay/ImpactSounds.cs`): from the impulse of every contact the physics solves, how much
it changed the speed of each body; the hardest hit of a frame for the tyres of the car, its
body, a metal object, a wooden one and a plastic one sounds, as loud as it was (an object lower
the heavier it is), each body not again for 0.15 s. Most of them are modal synthesis
(`Synth.AddContact` and `Synth.AddResonance`): a contact pushes for a moment, long and soft or
short and hard, and every mode of the body rings from it and dies away at its own rate; many
modes close together and out of tune with each other sound like a thing, a few far apart like
an instrument. The tyres landing or hitting something thud: a deep boom of the car on its
tyres, the rubber whumping, a moment later the frame knocking dully and a loose part
rattling, and the car bouncing once more on its tyres, all muffled above 1.2 kHz. The body hitting the track or an object crunches: its
heavy frame booms, many damped modes of the frame and the sheet metal crunch at once and the
frame scrapes over the road for a moment. A metal object clangs like a steel drum and bounces
once or twice more; a plastic barrel knocks, its wall dying away almost at once over a low
boom of its air. The owner chose each of these by ear among candidates (#49): the first
versions rang like a drum or a musical instrument, cracked like split wood or creaked like a
chair. They are baked in four takes, struck at other spots and tuned a little differently
(`SoundBakes.Variants`), one played at random, and a heavier car plays lower: the Crawler a
little deeper, the Sand Rail a little higher than the Buggy. The car's light wheels take every bump of the road, so
the tyres are heard from a change of 30 physics units a second; the heavy body changes its
speed less, so it is heard from 8, an object from 12: about once a second over humps and once
a step up stairs. What an object is made of is in the catalog (see
[Game content](content.md#catalog)).

## Driving

The keyboard, the on-screen controls (operated by the mouse or by
touch) and a gamepad all work at the same time. The on-screen controls are shown while
the mouse or touch is used, and fade out when the keyboard or a gamepad is. A level starts
with the keyboard on a computer (the controls hidden, whatever was used in the menus) and
with touch on a phone (the controls shown); a click or a touch in the level shows them.

| Key | On screen / gamepad | Action |
|---|---|---|
| → / ← | joystick right / left, left stick | Accelerate / reverse |
| ↑ / ↓ | joystick up / down, left stick | Rotate the axles clockwise / counterclockwise |
| Space | brake button, A | Brake all wheels |
| Left Ctrl | axle lock button, B | Lock the axle rotation |
| Esc, P | pause button (top right), Back, the back gesture of a phone | Pause: resume, restart, the level select or the main menu |

The car can also be grabbed with the mouse and dragged around. The game also pauses
when its window loses the focus, or when a phone switches to another app.

## Tutorial

While driving, cards at the top of the screen teach the controls one at a time: the key,
the gamepad button or stick, or the on-screen control (for the device used last), and
what to do with it. An action stays until it is done, then a word of praise joins it; not
done, it goes after about 20 seconds and comes back in the next run. A notice stays a few
seconds. A level shows its own lessons in every run, learnt or not. A lesson not learnt
yet follows the player into the later levels; one learnt is kept (in `TW3D/records.json`
next to the settings) and not shown there again. The cards never stop the game;
Options → Tutorial turns them off.

| Lesson | From level | Card (keyboard) |
|---|---|---|
| `drive` | 1 (training1: driving) | → *Hold to drive* |
| `par` | 1 | *Beat the par time for three stars* |
| `axles` | 2 (training2: stairs) | ↑ ↓ *Hold to turn the axles* |
| `lock` | 4 | Left Ctrl *Hold to lock the axles* |

The third training level (training3: long stairs) has no lesson of its own. The brake has
no lesson yet: no level needs it so far. A card can have two steps (the second one comes
when the first is done), which a lesson of braking while turning the axles would use.

A car stuck in front of an obstacle gets a hint: when the throttle has been held for 3
seconds while the car stood on its wheels and made no progress, ↑ ↓ *Turn the axles to
climb over* comes (only the time with the throttle held counts, and turning the axles
starts the count again). It does not come in the air or with the car turned far over (on
its roof the run is soon lost, see below). Where the axles were turned (with the hint or
without it) and the car still did not move on, they do not help (it is wedged between
blocks, hung on an edge with its wheels in the air, in front of a step too high): there
the next hint is Esc (the Back button of a gamepad, the pause button of the screen)
*Stuck? Pause and restart* instead, until the car has moved on half a car's length. The
hints are never saved: they go after 7 seconds and come again whenever they are due; the
restart hint goes at once when the pause menu opens.

The levels are counted in the order of the level select. The lessons and their state are
in `GamePlay/Tutorial.cs`, the card is drawn by `GamePlay/TutorialCard.cs`; the design
follows the tutorial of BS3D.

## A run of a level

A run starts with a countdown of three seconds, while the car is held still. The start
lights of the start gate count it down as in Formula 1: five pairs of red lamps come on pair
by pair, from the left seen from the car, one every 0.6 s, and when all five have shone for
a moment they go out: that is the start. A row of five red lights at the top of the screen, in the place of
the time, shows the same, with a beep for each pair and a higher tone at the start, and fades
away after it as the time comes. The start comes always 3 s after the first lights, with no random wait. Meanwhile the camera
looks at the gate and the car from behind the car, low and from farther away, and comes back
to its place behind the car as the car drives off, by the time it is under the lamps (a test
drive from the editor away from the start gate has no such view). The time at the top runs from the start until the car reaches the finish of the
level (its finish block, see the [level format](levels.md#level-format)); below it is the
par time of the level, the time a good run takes. At the left edge in the middle (behind
the car, which drives to the right, and above the joystick of the on-screen controls) a
speedometer shows the speed of the car in km/h, on a round dial with a needle as the cars of
the past had (drawn by the game, up to 160 km/h; the cars reach about 80 to 100 on their own).
The score is 1000 × par time / time:
finishing in the par time scores 1000 points, faster more and slower less (half the time,
twice the points). A run within the par time gets three stars, within one and a half of it
two, otherwise one. Past the finish the car brakes on its own. The results offer the main
menu, the level select, the level again and the next level in a row along the bottom of the
screen (left and right choose between them; Esc goes to the level select). The best time of each level is kept (in `TW3D/records.json` next
to the settings), and the level select shows it with its stars. A run in which the car was
dragged by hand does not count.

A car that falls off the level (below its lowest block, where there is nothing to land on)
for a second, while nobody holds it, loses the run (`GamePlay/Race.cs`): Game over, with a
falling chord, and results titled Game over with no stars and no time. So does a car lying
still on its roof for 4 seconds (turned more than about 120° from upright, its body on the
track or an object): one that slides, or that the player rocks with the axles or gets back
onto its wheels in time (braking and turning the axles), drives on, and a car that turns
over in the air counts only when it lands on its roof. The camera keeps following a car
lost on its roof, but not a car below the level: that one drops out of sight, and the
camera stays where it was lost.

## Development keys

While driving:

| Key | Action |
|---|---|
| F1–F8 | Physics simulation debug view (Aether.Physics2D DebugView), gamepad Start toggles most of it |
| Num 8 / 2 / 4 / 6 | Move the camera away from the car |
| Page Up / Page Down | Zoom of the debug view |
| Home | Reset the camera |
| F9 | The lighting of the 2016 version, for comparison |
| F10 | Shadows on / off |
| Shift+F10 | Ambient occlusion on / off |
| M | Switches the mood of the level (day, sunset, night) to try it; the level file stays as it is |
| F12 | Frames per second on / off (also in Options) |
| H | The track alone: the car, the time, the tutorial and the on-screen controls hidden (for the previews of the levels) |
| U | Turns the car onto its roof (to try out the Game over on the roof) |
| T | The road of the blocks as a test surface: asphalt, earth, grass, then as it is (the grey of the road of the classic blocks drawn so, #58) |
| B | The mud on the car in steps: a quarter more each time, from all of it clean again (#60) |
