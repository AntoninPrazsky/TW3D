using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TW3D.Input;
using TW3D.Shared;

namespace TW3D.Editor
{
    /// <summary>
    /// The Place tool of the editor: the cursor cell of the active layer and the editing of blocks there, with the mouse
    /// and with the keys of the old editor. The mouse sets the cursor to the cell under it; the left button paints the
    /// chosen block (with the chosen shift) into every cell the cursor passes while it is held, Left Shift + the left
    /// button erases the same way (a stroke is one step of undo, and it goes on wherever the cursor gets to), the right
    /// button picks the block under the cursor as the chosen one, Ctrl + the wheel shifts it. The arrows move the cursor (the
    /// camera follows), Space places, Delete erases, PageUp / PageDown shift, F toggles the finish, comma and period
    /// choose the previous / next block of the catalog; <see cref="Undo"/> and <see cref="Redo"/> (Ctrl+Z / Ctrl+Y, given
    /// by the screen) take a step of the document back and forth. What is done and what the
    /// document refuses is reported to the screen for its status bar. In road mode (R) a road block placed in the middle
    /// layer gets the cell and the shift that join it to the road next to it (<see cref="EditedLevel.RoadShift"/>).
    /// </summary>
    sealed class BlockTool : IEditorTool
    {
        //The block chosen at the start, the one a new level starts with
        private const string DefaultBlock = "a1";
        //Mouse wheel units of MonoGame per notch
        private const int WheelNotch = 120;

        /// <summary>What a drag with the left button does.</summary>
        private enum Stroke
        {
            None,
            Paint,
            Erase
        }

        private readonly EditorCamera camera;
        private readonly Action<string> report;
        private EditedLevel level;
        private Stroke stroke;
        //The step of undo the stroke is collected in, the layer it paints and the last cell it painted
        private IDisposable strokeStep;
        private Layer strokeLayer;
        private Cell strokeCell;
        //The block the stroke placed last, which the next one of the stroke joins in road mode; null before the first one
        private PlacedBlock strokePlaced;

        /// <param name="camera">The camera, panned along with the cursor moved by the arrows.</param>
        /// <param name="report">Gets a note of what was done, or the reason of a refusal, for the status bar.</param>
        public BlockTool(EditorCamera camera, Action<string> report)
        {
            this.camera = camera;
            this.report = report;
        }

        /// <summary>The document edited; a stroke in the previous one ends when it changes, and the cursor goes to the start.</summary>
        public EditedLevel Level
        {
            get => level;
            set
            {
                EndStroke();
                level = value;
                Cursor = new Cell(0, 0);
            }
        }

        /// <summary>The cursor cell of the active layer.</summary>
        public Cell Cursor { get; private set; }

        /// <summary>The name of the block placed by the left button and Space.</summary>
        public string Block { get; private set; } = DefaultBlock;

        /// <summary>The shift the placed blocks get; it follows the block picked or shifted.</summary>
        public int Shift { get; private set; }

        /// <summary>Road mode: the road blocks placed in the middle layer join the road next to them, whatever the chosen shift.</summary>
        public bool RoadMode { get; private set; }

        /// <summary>A stroke of the mouse (painting or erasing) is going on: the cursor follows the mouse wherever it is.</summary>
        public bool InStroke => stroke != Stroke.None;

        /// <summary>
        /// The keys of the old editor; called when the interface does not want the keyboard. With Ctrl held the keys are
        /// shortcuts of the screen's menus (Ctrl+Z, Ctrl+S...), which the screen handles even while a text field has the
        /// keyboard, so they are left alone here.
        /// </summary>
        /// <param name="layer">The active layer.</param>
        public void HandleKeys(InputState input, Layer layer)
        {
            if (input.IsKeyDown(Keys.LeftControl) || input.IsKeyDown(Keys.RightControl))
                return;

            Point step = ArrowStep(input);
            if (step != Point.Zero)
                MoveCursor(step);

            if (input.KeyPressed(Keys.Space))
                Place(layer, Cursor, true, null);
            if (input.KeyPressed(Keys.Delete))
                Erase(layer, Cursor, true);
            if (input.KeyPressed(Keys.PageUp))
                ShiftBlock(layer, -1);
            if (input.KeyPressed(Keys.PageDown))
                ShiftBlock(layer, 1);
            if (input.KeyPressed(Keys.F))
                ToggleFinish(layer);
            if (input.KeyPressed(Keys.OemComma))
                ChooseBlock(-1);
            if (input.KeyPressed(Keys.OemPeriod))
                ChooseBlock(1);
        }

        /// <summary>
        /// The mouse: the cursor follows it over the scene, and during a stroke anywhere; the buttons paint, erase and
        /// pick, Ctrl + the wheel shifts. Called after the camera has had the mouse: a drag of the camera takes
        /// precedence, and Alt + the left button is the camera's, not a stroke.
        /// </summary>
        /// <param name="layer">The active layer.</param>
        /// <param name="free">The cursor is over the scene, not over the user interface.</param>
        public void HandleMouse(InputState input, Viewport viewport, Layer layer, bool free)
        {
            MouseState mouse = input.Mouse, previous = input.PreviousMouse;
            bool overScene = free && viewport.Bounds.Contains(mouse.Position);
            bool leftPressed = mouse.LeftButton == ButtonState.Pressed && previous.LeftButton == ButtonState.Released;
            bool rightPressed = mouse.RightButton == ButtonState.Pressed && previous.RightButton == ButtonState.Released;
            FollowMouse(input, viewport, layer, overScene, leftPressed || rightPressed);

            if (InStroke)
            {
                //The button may have gone up while the window had no focus (the screen gets no input then): a stroke
                //ends when the button was up at any time since the last call, and the press of now begins no new one
                if (mouse.LeftButton == ButtonState.Released || previous.LeftButton == ButtonState.Released)
                    EndStroke();
                else if (Cursor != strokeCell)
                {
                    foreach (Cell cell in strokeCell.LineTo(Cursor))
                        Apply(cell);
                    strokeCell = Cursor;
                }
                return;
            }

            if (!overScene || camera.IsDragging)
                return;
            bool alt = input.IsKeyDown(Keys.LeftAlt) || input.IsKeyDown(Keys.RightAlt);
            //Right Shift toggles the back layer the moment it is pressed, so only Left Shift makes a stroke erase
            bool erase = input.IsKeyDown(Keys.LeftShift);
            bool control = input.IsKeyDown(Keys.LeftControl) || input.IsKeyDown(Keys.RightControl);
            if (leftPressed && !alt)
                BeginStroke(layer, erase ? Stroke.Erase : Stroke.Paint);
            else if (rightPressed)
                Pick(layer);
            //The wheel away from the user moves the block up, as PageUp does
            if (control && input.WheelDelta != 0)
                ShiftBlock(layer, -input.WheelDelta / WheelNotch);
        }

        /// <summary>
        /// The arrows pressed as a step of one: Up on the screen is y - 1, as physics y grows downward. The Object and
        /// Select tools step with them too.
        /// </summary>
        public static Point ArrowStep(InputState input)
        {
            Point step = Point.Zero;
            if (input.KeyPressed(Keys.Right)) step.X++;
            if (input.KeyPressed(Keys.Left)) step.X--;
            if (input.KeyPressed(Keys.Up)) step.Y--;
            if (input.KeyPressed(Keys.Down)) step.Y++;
            return step;
        }

        /// <summary>
        /// The cursor goes to the cell of a layer under the mouse, which the Object and Select tools' cursor does too. The
        /// cursor the arrows moved stays until the mouse moves; a button, though, acts under the mouse even when only the camera
        /// moved the scene under it (the keys fly it, Home frames the level).
        /// </summary>
        /// <param name="overScene">The cursor is over the scene, not over the user interface.</param>
        /// <param name="pressed">A button went down in this frame.</param>
        public void FollowMouse(InputState input, Viewport viewport, Layer layer, bool overScene, bool pressed)
        {
            if (!(input.MouseMoved && (overScene || InStroke) || overScene && pressed))
                return;
            Cell? cell = CellUnder(input.Mouse, viewport, InStroke ? strokeLayer : layer);
            if (cell.HasValue)
                Cursor = cell.Value;
        }

        /// <summary>The cell of a layer under the mouse; null when the mouse points off the plane of the layer.</summary>
        private Cell? CellUnder(MouseState mouse, Viewport viewport, Layer layer)
        {
            camera.Update(viewport);
            Vector3? hit = EditorCamera.HitPlane(camera.PickRay(new Vector2(mouse.X, mouse.Y), viewport), LevelView.SceneDepth(layer));
            return hit.HasValue ? LevelView.CellAt(hit.Value) : null;
        }

        /// <summary>
        /// The arrows move the cursor by cells (the Object and Select tools' too, while nothing is selected); the camera
        /// moves with it, so that it stays in view.
        /// </summary>
        public void MoveCursor(Point step)
        {
            Cell from = Cursor;
            Cursor = new Cell(from.X + step.X, from.Y + step.Y);
            camera.Target += LevelView.ScenePosition(Layer.Middle, Cursor) - LevelView.ScenePosition(Layer.Middle, from);
        }

        private void BeginStroke(Layer layer, Stroke kind)
        {
            stroke = kind;
            strokeLayer = layer;
            strokeCell = Cursor;
            strokePlaced = null;
            strokeStep = level.BeginCompound(kind == Stroke.Paint ? "Paint" : "Erase");
            Apply(Cursor);
        }

        /// <summary>Paints or erases a cell, whichever the stroke does.</summary>
        private void Apply(Cell cell)
        {
            if (stroke == Stroke.Paint)
            {
                strokePlaced = Place(strokeLayer, cell, false, strokePlaced) ?? strokePlaced;
            }
            else
                Erase(strokeLayer, cell, false);
        }

        /// <summary>The tool holds nothing for Esc to drop: Esc leaves the editor.</summary>
        public bool Cancel() => false;

        /// <summary>Ends the stroke of the mouse, closing its step of undo; nothing when there is none.</summary>
        public void EndStroke()
        {
            if (!InStroke)
                return;
            stroke = Stroke.None;
            strokeStep.Dispose();
            strokeStep = null;
        }

        /// <summary>Switches road mode on or off.</summary>
        public void ToggleRoad()
        {
            RoadMode = !RoadMode;
            report(RoadMode ? "Road mode on: road blocks join the road." : "Road mode off.");
        }

        /// <param name="note">Report what was done, or that there was nothing to do, not only a refusal (the keys do, a stroke does not).</param>
        /// <param name="joined">The block the stroke placed before: in road mode the new one joins it, on its right when
        /// the stroke paints to the left; null for a key and for the first block of a stroke.</param>
        /// <returns>The block that stands at the cell now; null when the document refused to place it.</returns>
        private PlacedBlock Place(Layer layer, Cell cell, bool note, PlacedBlock joined)
        {
            //In road mode the block of the middle layer goes where it joins the road; the chosen shift stays as it is
            int shift = Shift;
            bool road = RoadMode && layer == Layer.Middle && level.Catalog.FindBlock(Block)?.IsRoad == true;
            //The old road block that the new one would otherwise stand on goes, in the same step of undo
            using (level.BeginCompound("Place " + Block))
            {
                if (road)
                {
                    Cell pointed = cell;
                    bool leftwards = joined != null && cell.X < joined.Cell.X;
                    shift = level.RoadShift(pointed, Block, Shift, leftwards, joined, out cell);
                    PlacedBlock stale = level.RoadBlockReplaced(pointed, cell, shift);
                    if (stale != null)
                        level.Erase(Layer.Middle, stale);
                }

                //The document records nothing when the same block with the same shift is there already
                int version = level.Version;
                string reason = level.Place(layer, cell, Block, shift);
                if (reason != null)
                    report(reason);
                else if (note)
                {
                    if (level.Version == version)
                        report($"{Block} with shift {shift} is already at {cell}.");
                    else
                        report(road ? $"Placed {Block} at {cell}, shift {shift}." : $"Placed {Block} at {cell}.");
                }
                return reason == null ? level.BlockAt(layer, cell) : null;
            }
        }

        /// <param name="note">Report what was done, or that there was nothing to erase (the keys do, a stroke does not).</param>
        private void Erase(Layer layer, Cell cell, bool note)
        {
            bool erased = level.Erase(layer, cell);
            if (note)
                report(erased ? $"Erased {cell}." : $"There is no block at {cell}.");
        }

        /// <summary>Shifts the block under the cursor by steps (negative: up); the chosen shift follows it.</summary>
        private void ShiftBlock(Layer layer, int steps)
        {
            PlacedBlock block = level.BlockAt(layer, Cursor);
            if (block == null)
            {
                report($"There is no block at {Cursor}.");
                return;
            }
            level.Shift(layer, block, steps);
            Shift = block.Shift;
            report($"Shift of {block.Name} at {Cursor} is {Shift}.");
        }

        private void ToggleFinish(Layer layer)
        {
            string reason = level.SetFinish(layer, Cursor);
            if (reason != null)
                report(reason);
            else
            {
                PlacedBlock block = level.BlockAt(layer, Cursor);
                report(block.Finish ? $"{block.Name} at {Cursor} is the finish." : $"{block.Name} at {Cursor} is no longer the finish; the level has none.");
            }
        }

        /// <summary>The block under the cursor, with its shift, becomes the chosen one; nothing on an empty cell.</summary>
        private void Pick(Layer layer)
        {
            PlacedBlock block = level.BlockAt(layer, Cursor);
            if (block == null)
                return;
            Block = block.Name;
            Shift = block.Shift;
            report($"Picked {Block}, shift {Shift}.");
        }

        /// <summary>The next (1) or the previous (-1) block of the catalog becomes the chosen one, around its ends.</summary>
        private void ChooseBlock(int direction)
        {
            List<BlockType> blocks = level.Catalog.Blocks;
            if (blocks.Count == 0)
                return;
            int index = blocks.FindIndex(type => type.Name == Block);
            index = ((index + direction) % blocks.Count + blocks.Count) % blocks.Count;
            Choose(blocks[index]);
        }

        /// <summary>A block of the catalog, by its name, becomes the chosen one (the palette chooses so); a name the catalog does not know is refused.</summary>
        public void Choose(string block)
        {
            BlockType type = level.Catalog.FindBlock(block);
            if (type == null)
                report($"The catalog has no block {block}.");
            else
                Choose(type);
        }

        private void Choose(BlockType type)
        {
            Block = type.Name;
            report(type.Collision ? $"Chosen block: {Block}." : $"Chosen block: {Block} (decoration, back or front layer only).");
        }

        /// <summary>Takes back the last step of the document and says which.</summary>
        public void Undo()
        {
            if (!level.CanUndo)
                return;
            report("Undone: " + level.UndoName + ".");
            level.Undo();
        }

        /// <summary>Does the last undone step of the document again and says which.</summary>
        public void Redo()
        {
            if (!level.CanRedo)
                return;
            report("Redone: " + level.RedoName + ".");
            level.Redo();
        }
    }
}
