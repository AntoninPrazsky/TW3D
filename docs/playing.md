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
shadows, ambient occlusion and the frame rate (frames per second in the top left
corner). With some graphics drivers the game stutters in full screen (measured on an
AMD Radeon with OpenGL), but not in a window. They are saved in `TW3D/settings.json` in the
user's local application data (on Windows `%LOCALAPPDATA%\TW3D`).

## Driving

The keyboard, the on-screen controls (operated by the mouse or by
touch) and a gamepad all work at the same time. The on-screen controls are shown while
the mouse or touch is used, and fade out when the keyboard or a gamepad is (a phone
starts with them shown, a computer with them hidden).

| Key | On screen / gamepad | Action |
|---|---|---|
| → / ← | joystick right / left, left stick | Accelerate / reverse |
| ↑ / ↓ | joystick up / down, left stick | Rotate the axles clockwise / counterclockwise |
| Space | brake button, A | Brake all wheels |
| Left Ctrl | axle lock button, B | Lock the axle rotation |
| Esc, P | pause button (top right), Back, the back gesture of a phone | Pause: resume, restart, the level select or the main menu |

The car can also be grabbed with the mouse and dragged around. The game also pauses
when its window loses the focus, or when a phone switches to another app.

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
