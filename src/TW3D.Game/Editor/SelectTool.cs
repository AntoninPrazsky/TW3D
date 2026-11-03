using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TW3D.Input;

namespace TW3D.Editor
{
    /// <summary>
    /// The Select tool of the editor: working with many blocks of the active layer at once. The selection is a list of
    /// blocks of that layer; a switch of the layer and another level clear it, and a block that leaves the level
    /// (erased, undone) drops out of it.
    /// <para>
    /// Mouse: the left button selects the block under the cursor alone (with Ctrl it adds the block or takes it out); a
    /// drag from an empty cell (or with Ctrl) draws a rectangle on the plane of the layer and selects the blocks whose
    /// cells it holds (with Ctrl adds them); a drag of a selected block moves the whole selection by whole cells, in one
    /// step of undo made when the button goes up (the cells it would move to are shown meanwhile, and nothing is erased
    /// on the way).
    /// </para>
    /// <para>
    /// Keys: Space toggles the block at the cursor cell, Ctrl+A selects the layer, the arrows move the selection by a
    /// cell (the cursor with it), or the cursor cell while nothing is selected (with Ctrl they move the cursor cell
    /// only), Delete erases, PageUp / PageDown shift, H mirrors (see <see cref="EditedLevel.MirrorBlocks"/>). No key
    /// works while the button drags. Esc drops a drag, then the selection.
    /// </para>
    /// <para>
    /// Clipboard: Ctrl+C / Ctrl+X copy and cut the selection into the clipboard of the tool, the cells relative to the
    /// leftmost of the copied blocks (the lowest of those if several), and Ctrl+V pastes it into the active layer with
    /// that block at the cursor cell; the pasted blocks are the selection. The clipboard survives a switch of the layer
    /// and another level; a paste into the middle layer skips the decorations and says so.
    /// </para>
    /// The cursor cell is that of the Place tool. What is done is reported to the screen for its status bar; every
    /// command is one step of undo.
    /// </summary>
    sealed class SelectTool : IEditorTool
    {
        /// <summary>What the left button is doing.</summary>
        private enum Drag
        {
            None,
            Move,
            Rectangle
        }

        /// <summary>A block in the clipboard: what it was and where, relative to the anchor block (see <see cref="Anchor"/>).</summary>
        private readonly record struct Copied(string Name, int Shift, int X, int Y);

        private readonly EditorCamera camera;
        private readonly BlockTool placeTool;
        private readonly Action<string> report;
        private readonly List<PlacedBlock> selected = new List<PlacedBlock>();
        private readonly List<Copied> clipboard = new List<Copied>();
        private EditedLevel level;
        //The active layer as the tool saw it last: the selection is of this layer
        private Layer layer = Layer.Middle;
        //The version of the document the selection was last checked against
        private int checkedVersion;
        //The drag of the left button: the layer and the cell it began at, and whether Ctrl was held (it adds)
        private Drag drag;
        private Cell dragFrom;
        private bool additive;

        /// <param name="camera">The camera, which a drag of the mouse leaves the mouse to.</param>
        /// <param name="placeTool">The Place tool, whose cursor cell the Select tool shares.</param>
        /// <param name="report">Gets a note of what was done, or the reason of a refusal, for the status bar.</param>
        public SelectTool(EditorCamera camera, BlockTool placeTool, Action<string> report)
        {
            this.camera = camera;
            this.placeTool = placeTool;
            this.report = report;
        }

        /// <summary>
        /// The document edited; a drag in the previous one ends and the selection is dropped when it changes (the
        /// clipboard stays).
        /// </summary>
        public EditedLevel Level
        {
            get => level;
            set
            {
                EndStroke();
                level = value;
                selected.Clear();
                checkedVersion = value.Version;
            }
        }

        /// <summary>The selected blocks of the active layer, without those that are no longer in the level.</summary>
        public IReadOnlyList<PlacedBlock> Selection
        {
            get
            {
                Forget();
                return selected;
            }
        }

        /// <summary>How many blocks the clipboard holds.</summary>
        public int ClipboardCount => clipboard.Count;

        /// <summary>The cells the selection spans; false when nothing is selected.</summary>
        public bool Bounds(out Cell min, out Cell max)
        {
            IReadOnlyList<PlacedBlock> blocks = Selection;
            min = max = default;
            if (blocks.Count == 0)
                return false;
            min = max = blocks[0].Cell;
            for (int i = 1; i < blocks.Count; i++)
            {
                Cell cell = blocks[i].Cell;
                min = new Cell(Math.Min(min.X, cell.X), Math.Min(min.Y, cell.Y));
                max = new Cell(Math.Max(max.X, cell.X), Math.Max(max.Y, cell.Y));
            }
            return true;
        }

        /// <summary>The active layer is this one: the selection of another layer is dropped, and so is a drag in it.</summary>
        public void Sync(Layer active)
        {
            if (active == layer)
                return;
            EndStroke();
            selected.Clear();
            layer = active;
        }

        /// <summary>
        /// The keys; called when the interface does not want the keyboard. The shortcuts with Ctrl are here too (they must
        /// not reach a text field of a window, which Ctrl+C and Ctrl+V are for); the other Ctrl shortcuts are the screen's.
        /// </summary>
        public void HandleKeys(InputState input, Layer active)
        {
            Sync(active);
            //What a key did while the mouse drags would be in the step of the drag or lost with it
            if (drag != Drag.None)
                return;
            if (input.IsKeyDown(Keys.LeftControl) || input.IsKeyDown(Keys.RightControl))
            {
                //Ctrl + the arrows move the cursor cell and leave the selection, to add blocks to it with Space
                Point cursorStep = BlockTool.ArrowStep(input);
                if (cursorStep != Point.Zero)
                    placeTool.MoveCursor(cursorStep);
                if (input.KeyPressed(Keys.A))
                    SelectAll();
                if (input.KeyPressed(Keys.C))
                    Copy();
                if (input.KeyPressed(Keys.X))
                    Cut();
                if (input.KeyPressed(Keys.V))
                    Paste();
                return;
            }
            Point step = BlockTool.ArrowStep(input);
            if (step != Point.Zero)
            {
                if (Selection.Count > 0)
                    MoveSelection(step);
                else
                    placeTool.MoveCursor(step);
            }
            if (input.KeyPressed(Keys.Space))
                Toggle(placeTool.Cursor, true);
            if (input.KeyPressed(Keys.Delete))
                Delete();
            if (input.KeyPressed(Keys.PageUp))
                ShiftSelection(-1);
            if (input.KeyPressed(Keys.PageDown))
                ShiftSelection(1);
            if (input.KeyPressed(Keys.H))
                Mirror();
        }

        /// <summary>
        /// The mouse: the cursor cell follows it over the scene, and during a drag anywhere; the left button selects and
        /// drags, the right one does nothing. Called after the camera has had the mouse: a drag of the camera takes
        /// precedence, and Alt + the left button is the camera's.
        /// </summary>
        public void HandleMouse(InputState input, Viewport viewport, Layer active, bool free)
        {
            Sync(active);
            MouseState mouse = input.Mouse, previous = input.PreviousMouse;
            bool overScene = free && viewport.Bounds.Contains(mouse.Position);
            bool leftPressed = mouse.LeftButton == ButtonState.Pressed && previous.LeftButton == ButtonState.Released;
            placeTool.FollowMouse(input, viewport, active, overScene || drag != Drag.None, leftPressed);

            if (drag != Drag.None)
            {
                //The button may have gone up while the window had no focus (the screen gets no input then): the drag
                //ends when the button was up at any time since the last call, and the press of now begins no new one
                if (mouse.LeftButton == ButtonState.Released || previous.LeftButton == ButtonState.Released)
                    EndStroke();
                return;
            }
            bool alt = input.IsKeyDown(Keys.LeftAlt) || input.IsKeyDown(Keys.RightAlt);
            if (!leftPressed || !overScene || camera.IsDragging || alt)
                return;
            BeginDrag(input.IsKeyDown(Keys.LeftControl) || input.IsKeyDown(Keys.RightControl));
        }

        /// <summary>Esc drops a drag, else the selection; false when there was neither, and Esc leaves the editor.</summary>
        public bool Cancel()
        {
            if (drag != Drag.None)
            {
                drag = Drag.None;
                report("Drag cancelled.");
                return true;
            }
            if (Selection.Count == 0)
                return false;
            selected.Clear();
            report("Selection cleared.");
            return true;
        }

        /// <summary>Ends the drag of the left button, doing what it came to as one step of undo; nothing when there is none.</summary>
        public void EndStroke()
        {
            Drag finished = drag;
            drag = Drag.None;
            if (finished == Drag.Move)
                FinishMove();
            else if (finished == Drag.Rectangle)
                FinishRectangle();
        }

        /// <summary>Selects every block of the active layer (Ctrl+A, Edit > Select all).</summary>
        public void SelectAll()
        {
            selected.Clear();
            selected.AddRange(level.Blocks(layer));
            ReportSelected();
        }

        /// <summary>Puts the selected blocks into the clipboard, their cells relative to the anchor block (Ctrl+C).</summary>
        public void Copy()
        {
            if (Selection.Count == 0)
            {
                report("Nothing is selected.");
                return;
            }
            Cell origin = Anchor();
            clipboard.Clear();
            foreach (PlacedBlock block in selected)
                clipboard.Add(new Copied(block.Name, block.Shift, block.Cell.X - origin.X, block.Cell.Y - origin.Y));
            report($"Copied {Blocks(selected.Count)}.");
        }

        /// <summary>Copies the selected blocks and erases them, in one step of undo (Ctrl+X).</summary>
        public void Cut()
        {
            if (Selection.Count == 0)
            {
                report("Nothing is selected.");
                return;
            }
            int count = selected.Count;
            Copy();
            Erase("Cut");
            report($"Cut {Blocks(count)}.");
        }

        /// <summary>
        /// Puts the blocks of the clipboard into the active layer, the anchor block at the cursor cell, over what is
        /// there, in one step of undo; they become the selection. Decorations are skipped in the middle layer, which
        /// the message counts. When that leaves nothing to paste, the selection stays as it was (Ctrl+V).
        /// </summary>
        public void Paste()
        {
            if (clipboard.Count == 0)
            {
                report("The clipboard is empty.");
                return;
            }
            Cell origin = placeTool.Cursor;
            List<PlacedBlock> pasted = new List<PlacedBlock>(clipboard.Count);
            int skipped = 0;
            int version = level.Version;
            using (level.BeginCompound("Paste"))
            {
                foreach (Copied copied in clipboard)
                {
                    Cell cell = new Cell(origin.X + copied.X, origin.Y + copied.Y);
                    if (level.Place(layer, cell, copied.Name, copied.Shift) != null)
                    {
                        skipped++;
                        continue;
                    }
                    PlacedBlock block = level.BlockAt(layer, cell);
                    if (!pasted.Contains(block))
                        pasted.Add(block);
                }
            }
            string note = skipped == 0 ? "" : $"{skipped} {(skipped == 1 ? "decoration" : "decorations")} skipped in the {layer.ToString().ToLowerInvariant()} layer";
            //No step was recorded when everything was refused or was there already: nothing changed, the selection stays
            if (level.Version == version)
            {
                report(skipped == 0 ? "Nothing pasted: the blocks are there already." : "Nothing pasted; " + note + ".");
                return;
            }
            selected.Clear();
            selected.AddRange(pasted);
            report($"Pasted {Blocks(pasted.Count)}" + (skipped == 0 ? "." : "; " + note + "."));
        }

        /// <summary>Erases the selected blocks, in one step of undo (Delete, the Inspector).</summary>
        public void Delete()
        {
            int count = Selection.Count;
            if (count == 0)
            {
                report("Nothing is selected.");
                return;
            }
            Erase("Delete");
            report($"Deleted {Blocks(count)}.");
        }

        /// <summary>
        /// Flips the selection left to right, swapping the blocks for their mirrored variants, in one step of undo (H,
        /// the Inspector).
        /// </summary>
        public void Mirror()
        {
            if (Selection.Count == 0)
            {
                report("Nothing is selected.");
                return;
            }
            if (level.MirrorBlocks(layer, selected))
                report($"Mirrored {Blocks(selected.Count)}.");
            else
                report("Nothing to mirror: the blocks stay as they are.");
        }

        /// <summary>Draws the selection, the cells a drag would move it to, or the rectangle being dragged.</summary>
        public void Draw(EditorMarkers markers, Matrix view, Matrix projection)
        {
            markers.DrawBlocks(view, projection, layer, Selection, 0, 0, false);
            if (drag == Drag.Move)
            {
                Cell to = placeTool.Cursor;
                if (to != dragFrom)
                    markers.DrawBlocks(view, projection, layer, selected, to.X - dragFrom.X, to.Y - dragFrom.Y, true);
            }
            else if (drag == Drag.Rectangle)
                markers.DrawRectangle(view, projection, layer, dragFrom, placeTool.Cursor);
        }

        /// <summary>The press of the left button at the cursor cell: a drag of the selection, or of a rectangle.</summary>
        private void BeginDrag(bool control)
        {
            Cell cell = placeTool.Cursor;
            dragFrom = cell;
            additive = control;
            drag = Drag.Rectangle;
            if (control)
                return;

            //A block that is not selected is selected alone and dragged; one that is, is dragged with the others
            if (!selected.Exists(block => block.Cell == cell))
            {
                PlacedBlock block = level.BlockAt(layer, cell);
                if (block == null)
                    return;
                selected.Clear();
                selected.Add(block);
                ReportSelected();
            }
            drag = Drag.Move;
        }

        /// <summary>
        /// The button went up after a drag of the selection: it moves by the cells the cursor moved, or a click on it
        /// selects it alone.
        /// </summary>
        private void FinishMove()
        {
            Cell to = placeTool.Cursor;
            int dx = to.X - dragFrom.X, dy = to.Y - dragFrom.Y;
            if (dx != 0 || dy != 0)
            {
                Forget();
                ReportMoved(level.MoveBlocks(layer, selected, dx, dy));
            }
            else if (selected.Count > 1)
            {
                PlacedBlock block = level.BlockAt(layer, dragFrom);
                selected.Clear();
                if (block != null)
                    selected.Add(block);
                ReportSelected();
            }
        }

        /// <summary>
        /// The button went up after a drag from an empty cell (or with Ctrl): the blocks in the rectangle are selected,
        /// a click without a drag selects the block there alone.
        /// </summary>
        private void FinishRectangle()
        {
            Cell to = placeTool.Cursor;
            if (to == dragFrom)
            {
                if (additive)
                {
                    Toggle(to, false);
                    return;
                }
                PlacedBlock block = level.BlockAt(layer, to);
                selected.Clear();
                if (block != null)
                    selected.Add(block);
                ReportSelected();
                return;
            }
            int minX = Math.Min(dragFrom.X, to.X), maxX = Math.Max(dragFrom.X, to.X);
            int minY = Math.Min(dragFrom.Y, to.Y), maxY = Math.Max(dragFrom.Y, to.Y);
            if (!additive)
                selected.Clear();
            foreach (PlacedBlock block in level.Blocks(layer))
            {
                Cell cell = block.Cell;
                if (cell.X >= minX && cell.X <= maxX && cell.Y >= minY && cell.Y <= maxY && (!additive || !selected.Contains(block)))
                    selected.Add(block);
            }
            ReportSelected();
        }

        /// <summary>Adds the block at a cell to the selection, or takes it out when it is in.</summary>
        /// <param name="complain">Say so when the cell is empty (the key does, a click does not).</param>
        private void Toggle(Cell cell, bool complain)
        {
            Forget();
            if (selected.RemoveAll(block => block.Cell == cell) == 0)
            {
                PlacedBlock block = level.BlockAt(layer, cell);
                if (block == null)
                {
                    if (complain)
                        report($"There is no block at {cell}.");
                    return;
                }
                selected.Add(block);
            }
            ReportSelected();
        }

        /// <summary>The arrows: the selection and the cursor with it move by a cell, one step of undo.</summary>
        private void MoveSelection(Point step)
        {
            if (ReportMoved(level.MoveBlocks(layer, selected, step.X, step.Y)))
                placeTool.MoveCursor(step);
        }

        /// <summary>Tells that the selection moved, or that it did not (the document recorded no step); the same.</summary>
        private bool ReportMoved(bool moved)
        {
            report(moved ? $"Moved {Blocks(selected.Count)}." : "Nothing moved.");
            return moved;
        }

        /// <summary>
        /// The block the clipboard is relative to: the leftmost of the selected blocks, the lowest of those if several
        /// (the one a climb or a descent starts on, which is on the row of the cursor when it is pasted).
        /// </summary>
        private Cell Anchor()
        {
            Cell anchor = selected[0].Cell;
            foreach (PlacedBlock block in selected)
                if (block.Cell.X < anchor.X || block.Cell.X == anchor.X && block.Cell.Y > anchor.Y)
                    anchor = block.Cell;
            return anchor;
        }

        private void ShiftSelection(int steps)
        {
            if (Selection.Count == 0)
            {
                report("Nothing is selected.");
                return;
            }
            using (level.BeginCompound("Shift"))
            {
                foreach (PlacedBlock block in selected)
                    level.Shift(layer, block, steps);
            }
            report($"Shifted {Blocks(selected.Count)} by {steps}.");
        }

        /// <summary>Erases the selected blocks as one step of undo and drops the selection.</summary>
        private void Erase(string step)
        {
            using (level.BeginCompound(step))
            {
                foreach (PlacedBlock block in selected)
                    level.Erase(layer, block);
            }
            selected.Clear();
        }

        private void ReportSelected()
        {
            report(selected.Count == 0 ? "Nothing is selected." : $"Selected {Blocks(selected.Count)}.");
        }

        /// <summary>A count of blocks as a phrase: "1 block", "3 blocks".</summary>
        public static string Blocks(int count) => count == 1 ? "1 block" : count + " blocks";

        /// <summary>The blocks that are no longer in the layer (erased, undone) leave the selection.</summary>
        private void Forget()
        {
            if (level == null || level.Version == checkedVersion)
                return;
            checkedVersion = level.Version;
            if (selected.Count == 0)
                return;
            HashSet<PlacedBlock> present = new HashSet<PlacedBlock>(level.Blocks(layer));
            selected.RemoveAll(block => !present.Contains(block));
        }
    }
}
