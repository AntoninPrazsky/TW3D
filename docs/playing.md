# Playing

The user interface is designed for desktop monitors at 1920×1080 and scaled
uniformly to the screen, so it fits any aspect ratio (16:9, 21:9, 4:3, …).
It keeps a margin of 48 px (at 1080p) from the screen edges; the level select
uses as many columns as fit the screen.

## Menus

Mouse (click, wheel and dragging in the level select), arrow keys and Enter, a
gamepad (D-pad, A) or touch. Esc (the gamepad Back button, the back gesture or
button of a phone) goes back; in the main menu it quits the game.

A Debug build of the game has a *Level editor* item in the main menu, which opens the
[level editor](levels.md#level-editor) on the level edited last, or a new level (Esc returns
to the menu, asking first when there are unsaved changes); a Release
build opens the editor only with the `--editor` argument.

## Garage

The cars to choose from, one at a time; dragging the car sideways (with a
finger or the mouse) or the arrows next to the name (or left and right) show the others.
The car shown is the one to drive. On the right its wheels are chosen, for the front and
the rear axle each (any wheels fit any car; up and down choose the row, left and right the
wheels); on the left bars show how fast, heavy and gripping the car is with them. The cars
differ in their top speed and weight (the Sand Rail is faster and lighter than the Buggy),
the wheels in their grip and weight (the off-road wheels grip better, the racing ones are
lighter). The choices are saved with the options, the wheels for each car.

## About

What the game is, who made it and the link to its source code on GitHub, which
opens in the web browser (clicked, or chosen by the keys and Enter).

## Options

Full screen or a window (on a desktop, also F11 anywhere in the game; the window opens
maximized and can be resized), the resolution of the 3D, antialiasing (the multisample antialiasing of the edges: on
by default, but on a Raspberry Pi; a switch comes with the next start of the game), the look of the game (new, or classic: as the 2016 version looked),
shadows, ambient occlusion, the frame rate (frames per second in the top left
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
focus moving, an item chosen, going back), the countdown (3, 2, 1 and the start an octave
higher), the finish, a lost run (the chord broken downwards), the stars of the results one
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
higher and revs up faster than the Buggy. The motor of the axles hums while they turn; the
lock of the axles clunks (softer as it opens); the brake grabs the wheels of a moving car.

The tyres screech while they slip over what they stand on, the track or an object, faster
than 8 physics units a second (`Car.SlippingWheels`): braked wheels sliding, or wheels
spinning at a launch or landing from a jump; louder and higher the faster they slip. Where they
slip, they leave dark skid marks (`GameObjects/SkidMarks.cs`: under the wheels on both sides
of the car, darker the faster they slip, lying on what the wheel stood on and moving with it,
fading away after 20 seconds), and slipping hard they smoke (`GameObjects/TyreSmoke.cs`: big
soft puffs rising and thinning out, their colour a greyed haze of the level's light). The car
has traction control: a wheel on the ground spinning faster than it rolls gets less torque, so
it grips instead of spinning (the motors never hold the wheels back, and wheels in the air
spin up freely); only at a launch the tyres may spin up for a moment, less the faster the car
goes. A launch (the throttle pressed with the car standing, not braked) is a burnout: the
motors spin the wheels with all their torque and the tyres grip little, so the wheels really
spin, screeching and smoking, while the car creeps; after about a second of spinning they grip
again and the car shoots off. Letting go of the throttle or braking ends it, and it does not
come when the wheels do not start spinning within two seconds (the axles turning instead, or
the car against an obstacle), so it does not get in the way of climbing.

The exhaust smokes too (`GameObjects/ExhaustSmoke.cs`): small bluish grey puffs blown out to
the back from the ends of the two pipes (`exhaust` in the catalog), thin idling, thicker under
the throttle and a burst when it is pressed; wispier than the smoke of the tyres, they thin
out quicker and hardly rise. Both kinds of smoke are clouds of `GameObjects/Smoke.cs`, each
with its own look.

The loops last a second and every part of them is a whole number of hertz, so they loop
without a seam; they pause with the game. Things hitting each other are heard too
(`GamePlay/ImpactSounds.cs`): from the impulse of every contact the physics solves, how much
it changed the speed of each body; the hardest hit of a frame for the car, a metal object, a
wooden one and a plastic one (the barrels: a dull bomp) sounds, as loud as it was (an object lower the heavier it is), each body not again
for 0.15 s. The car's light wheels take every bump of the road, so the car is heard from a
change of 30 physics units a second, an object from 12: about once a second over humps and
once a step up stairs. What an object is made of is in the catalog (see
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
seconds while the car made no progress, ↑ ↓ *Turn the axles to climb over* comes (only the
time with the throttle held counts, and turning the axles starts the count again). The
hint is never saved: it goes after 7 seconds and comes again whenever it is due.

The levels are counted in the order of the level select. The lessons and their state are
in `GamePlay/Tutorial.cs`, the card is drawn by `GamePlay/TutorialCard.cs`; the design
follows the tutorial of BS3D.

## A run of a level

A run starts with a countdown (3, 2, 1, Start), while the car is held
still. The signal lamps of the start gate count it down as in Formula 1: the red lamps come
on column by column, from the left seen from the car, all five for the last moment, and at
the start they go out and the green row lights until the run ends. Meanwhile the camera
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
two, otherwise one. Past the finish the car brakes on its own. The results offer the level
select, the level again and the next level in a row along the bottom of the screen (left and
right choose between them). The best time of each level is kept (in `TW3D/records.json` next
to the settings), and the level select shows it with its stars. A run in which the car was
dragged by hand does not count.

A car that falls off the level (below its lowest block, where there is nothing to land on)
for a second, while nobody holds it, loses the run (`GamePlay/Race.cs`): Game over, with a
falling chord, and results titled Game over with no stars and no time. The camera does not
follow the car below the level, so it drops out of sight, and stays where it was lost.

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
