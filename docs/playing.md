# Playing

The user interface is designed for desktop monitors at 1920×1080 and scaled
uniformly to the screen, so it fits any aspect ratio (16:9, 21:9, 4:3, …).
It keeps a margin of 48 px (at 1080p) from the screen edges; the level select
uses as many columns as fit the screen.

## Menus

Mouse (click, wheel and dragging in the level select), arrow keys and Enter, a
gamepad (D-pad, A) or touch. Esc (the gamepad Back button, the back gesture or
button of a phone) goes back; in the main menu it quits the game.

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

Full screen or a window (on a desktop; the window opens maximized and
can be resized), the look of the game (new, or classic: as the 2016 version looked),
shadows, ambient occlusion, the frame rate (frames per second in the top left
corner), the tutorial and the volume of the sound (in tenths; Enter turns it up, round to
off, left and right turn it down and up). A window was the cure when the game stuttered in
full screen with one graphics driver (an AMD Radeon with OpenGL, 2026). They are saved in
`TW3D/settings.json` in the user's local application data (on Windows `%LOCALAPPDATA%\TW3D`).

## Sound

The sound effects are synthesized when the game starts, no recorded files: the menus (the
focus moving, an item chosen, going back), the countdown (3, 2, 1 and the start an octave
higher), the finish, the stars of the results one after another (a major chord) and a new
record, and the cars and wheels changing in the garage. They are pleasant and deep rather
than bright: struck bars (a marimba) and soft bongs in C major, low, with the octave below
for body, never a beep; nothing shrill above 5 kHz, but enough between 300 Hz and 4 kHz for
small speakers, which play little below. Mono. The notes are never nudged off their pitch,
so they stay in tune with each other; the noises and knocks play a little higher or lower
each time. `--sounds <folder>` writes them as WAV files to listen to (see
[Building](building.md)). The recipes are in `Audio/SoundBakes.cs`, the arithmetic in
`Audio/Synth.cs`.

The car sounds as it drives (`GamePlay/CarSounds.cs`): the engine is two loops in step, idling
and under the throttle, crossfaded by the throttle and pitched by how fast the wheels turn
(so wheels spinning in the air rev it); the motor of the axles hums while they turn; the
lock of the axles clunks (softer as it opens); the brake grabs the wheels of a moving car.
The loops last a second and every part of them is a whole number of hertz, so they loop
without a seam; they pause with the game. Things hitting each other are heard too
(`GamePlay/ImpactSounds.cs`): from the impulse of every contact the physics solves, how much
it changed the speed of each body; the hardest hit of a frame for the car, a metal object and a
wooden one sounds, as loud as it was (an object lower the heavier it is), each body not again
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
still. The time at the top runs from the start until the car reaches the finish of the
level (its finish block, see the [level format](levels.md#level-format)); below it is the
par time of the level, the time a good run takes. The score is 1000 × par time / time:
finishing in the par time scores 1000 points, faster more and slower less (half the time,
twice the points). A run within the par time gets three stars, within one and a half of it
two, otherwise one. The results offer the level select, the level again and the next level
in a row along the bottom of the screen (left and right choose between them). The best
time of each level is kept (in `TW3D/records.json` next to the settings), and the level
select shows it with its stars. A run in which the car was dragged by hand does not count.

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
| F11 | Ambient occlusion on / off |
| M | Switches the mood of the level (day, sunset, night) to try it; the level file stays as it is |
| F12 | Frames per second on / off (also in Options) |
| H | The track alone: the car, the time, the tutorial and the on-screen controls hidden (for the previews of the levels) |
