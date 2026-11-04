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
    /// The Select tool of the editor: working with many blocks of the active layer at once, and with the props of the
    /// scenery that stand in the layer (see <see cref="PlacedScenery.Layer"/>; a prop is in the cell its foot is in). The
    /// selection is a list of blocks and a list of props of that layer; a switch of the layer and another level clear it,
    /// and a block or a prop that leaves the level or the layer (erased, undone, moved by the Scenery tool) drops out of
    /// it, as the props do while the View menu hides the scenery.
    /// <para>
    /// Mouse: the left button selects the cell under the cursor alone, its block and its props (with Ctrl it adds them or
    /// takes them out); a drag from an empty cell (or with Ctrl) draws a rectangle on the plane of the layer and selects
    /// the blocks and the props in its cells (with Ctrl adds them); a drag of a selected cell moves the whole selection by
    /// whole cells, in one step of undo made when the button goes up (the cells it would move to are shown meanwhile, and
    /// nothing is erased on the way).
    /// </para>
    /// <para>
    /// Keys: Space toggles the cell at the cursor, Ctrl+A selects the layer, the arrows move the selection by a cell (the
    /// cursor with it), or the cursor cell while nothing is selected (with Ctrl they move the cursor cell only), Delete
    /// erases, PageUp / PageDown shift, H mirrors (see <see cref="EditedLevel.MirrorBlocks(Layer, IReadOnlyList{PlacedBlock}, int)"/>
    /// and <see cref="EditedLevel.MirrorScenery"/>), Ctrl+PageUp / Ctrl+PageDown move the selection into the layer behind
    /// or in front, which becomes the active one (see <see cref="EditedLevel.MoveToLayer"/>). No key works while the
    /// button drags. Esc drops a drag, then the selection.
    /// </para>
    /// <para>
    /// Clipboard: Ctrl+C / Ctrl+X copy and cut the selection into the clipboard of the tool, the cells relative to the
    /// leftmost of the copied cells (the lowest of those if several), and Ctrl+V pastes it into the active layer with
    /// that cell at the cursor cell; the pasted blocks and props are the selection. The props keep their depth relative
    /// to the layer. The clipboard survives a switch of the layer and another level; a paste into the middle layer skips
    /// the decorations and says so.
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

        /// <summary>A block in the clipboard: what it was and where, relative to the anchor cell (see <see cref="Anchor"/>).</summary>
        private readonly record struct Copied(string Name, int Shift, int X, int Y);

        /// <summary>
        /// A prop in the clipboard: its model, where its foot was relative to the middle of the anchor cell (physics units),
        /// its depth relative to the layer and its turn.
        /// </summary>
        private readonly record struct CopiedProp(string Model, float X, float Y, float Depth, int Turn);

        //The files keep two decimals; a prop moved by cells keeps them
        private const int Decimals = 2;

        private readonly EditorCamera camera;
        private readonly BlockTool placeTool;
        private readonly Func<PlacedScenery, BoundingSphere?> propBounds;
        private readonly Action<string> report;
        //Makes another layer the active one, which the selection was moved into
        private readonly Action<Layer> activate;
        //Why a layer cannot be edited (it is hidden); null when it can
        private readonly Func<Layer, string> hiddenNote;
        private readonly List<PlacedBlock> selected = new List<PlacedBlock>();
        private readonly List<PlacedScenery> selectedProps = new List<PlacedScenery>();
        private readonly List<Copied> clipboard = new List<Copied>();
        private readonly List<CopiedProp> propClipboard = new List<CopiedProp>();
        //The bounding spheres of the selected props, filled for every draw
        private readonly List<BoundingSphere> propSpheres = new List<BoundingSphere>();
        private EditedLevel level;
        //The active layer as the tool saw it last: the selection is of this layer
        private Layer layer = Layer.Middle;
        private bool scenery = true;
        //The version of the document the selection was last checked against
        private int checkedVersion;
        //The drag of the left button: the layer and the cell it began at, and whether Ctrl was held (it adds)
        private Drag drag;
        private Cell dragFrom;
        private bool additive;

        /// <param name="camera">The camera, which a drag of the mouse leaves the mouse to.</param>
        /// <param name="placeTool">The Place tool, whose cursor cell the Select tool shares.</param>
        /// <param name="propBounds">The bounding sphere of a prop in the scene, for its mark; null for one that is not drawn.</param>
        /// <param name="report">Gets a note of what was done, or the reason of a refusal, for the status bar.</param>
        /// <param name="activate">Makes a layer the active one: the selection was moved into it.</param>
        /// <param name="hiddenNote">Why a layer cannot be edited, as the status bar says it (the View menu hides it); null when it can.</param>
        public SelectTool(EditorCamera camera, BlockTool placeTool, Func<PlacedScenery, BoundingSphere?> propBounds, Action<string> report,
            Action<Layer> activate, Func<Layer, string> hiddenNote)
        {
            this.hiddenNote = hiddenNote;
            this.camera = camera;
            this.placeTool = placeTool;
            this.propBounds = propBounds;
            this.report = report;
            this.activate = activate;
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
                selectedProps.Clear();
                checkedVersion = value.Version;
            }
        }

        /// <summary>
        /// The props of the scenery are selected with the blocks: false while the View menu hides them, which drops them
        /// from the selection (what is not seen is not moved or erased).
        /// </summary>
        public bool Scenery
        {
            get => scenery;
            set
            {
                if (value == scenery)
                    return;
                scenery = value;
                if (!value)
                    selectedProps.Clear();
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

        /// <summary>The selected props of the scenery of the active layer, without those that are no longer in the level or in the layer.</summary>
        public IReadOnlyList<PlacedScenery> SelectedProps
        {
            get
            {
                Forget();
                return selectedProps;
            }
        }

        /// <summary>Something is selected: a block or a prop.</summary>
        public bool Any => Selection.Count + SelectedProps.Count > 0;

        /// <summary>How many blocks and props the clipboard holds.</summary>
        public int ClipboardCount => clipboard.Count + propClipboard.Count;

        /// <summary>What the clipboard holds as a phrase ("3 blocks and 2 props"); empty for nothing.</summary>
        public string ClipboardText => Items(clipboard.Count, propClipboard.Count);

        /// <summary>The cells the selection spans (a prop by the cell of its foot); false when nothing is selected.</summary>
        public bool Bounds(out Cell min, out Cell max)
        {
            min = max = default;
            bool any = false;
            foreach (Cell cell in SelectedCells())
            {
                min = any ? new Cell(Math.Min(min.X, cell.X), Math.Min(min.Y, cell.Y)) : cell;
                max = any ? new Cell(Math.Max(max.X, cell.X), Math.Max(max.Y, cell.Y)) : cell;
                any = true;
            }
            return any;
        }

        /// <summary>The active layer is this one: the selection of another layer is dropped, and so is a drag in it.</summary>
        public void Sync(Layer active)
        {
            if (active == layer)
                return;
            EndStroke();
            selected.Clear();
            selectedProps.Clear();
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
                //Away from the camera and towards it, as PageUp and PageDown move a prop of the Scenery tool
                if (input.KeyPressed(Keys.PageUp))
                    MoveToLayer(-1);
                if (input.KeyPressed(Keys.PageDown))
                    MoveToLayer(1);
                return;
            }
            Point step = BlockTool.ArrowStep(input);
            if (step != Point.Zero)
            {
                if (Any)
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
            if (!Any)
                return false;
            ClearSelection();
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

        /// <summary>Selects every block of the active layer and the props standing in it (Ctrl+A, Edit > Select all).</summary>
        public void SelectAll()
        {
            ClearSelection();
            selected.AddRange(level.Blocks(layer));
            selectedProps.AddRange(PropsOfLayer());
            ReportSelected();
        }

        /// <summary>Puts the selected blocks and props into the clipboard, relative to the anchor cell (Ctrl+C).</summary>
        public void Copy()
        {
            if (!Any)
            {
                report("Nothing is selected.");
                return;
            }
            Cell origin = Anchor();
            clipboard.Clear();
            foreach (PlacedBlock block in selected)
                clipboard.Add(new Copied(block.Name, block.Shift, block.Cell.X - origin.X, block.Cell.Y - origin.Y));
            propClipboard.Clear();
            foreach (PlacedScenery prop in selectedProps)
                propClipboard.Add(new CopiedProp(prop.Model, prop.X - origin.X * Units.GridCell, prop.Y - origin.Y * Units.GridCell,
                    prop.Z - LevelView.Depth(layer), prop.Turn));
            report($"Copied {Items(selected.Count, selectedProps.Count)}.");
        }

        /// <summary>
        /// Moves the selected blocks and props into the layer a number of layers further to the front (negative: to the
        /// back), as one step of undo; that layer becomes the active one and they stay selected (Ctrl+PageUp and
        /// Ctrl+PageDown, Edit > Move to the layer behind / in front). Nothing beyond the first and the last layer.
        /// </summary>
        public void MoveToLayer(int layers)
        {
            if (!Any)
            {
                report("Nothing is selected.");
                return;
            }
            Layer to = Layers.OfCells(layer.Cells() + layers);
            if (to == layer)
            {
                report(layers < 0 ? "There is no layer behind this one." : "There is no layer in front of this one.");
                return;
            }
            //What is moved into a hidden layer could not be seen there
            string hidden = hiddenNote(to);
            if (hidden != null)
            {
                report(hidden);
                return;
            }
            string items = Items(selected.Count, selectedProps.Count);
            string refused = level.MoveToLayer(layer, selected, selectedProps, to);
            if (refused != null)
            {
                report(refused);
                return;
            }
            //The selection goes with them (a prop whose depth falls beyond the last layer drops out of it)
            layer = to;
            activate(to);
            report($"Moved {items} to the {to.Name()} layer.");
        }

        /// <summary>Copies the selection and erases it, in one step of undo (Ctrl+X).</summary>
        public void Cut()
        {
            if (!Any)
            {
                report("Nothing is selected.");
                return;
            }
            string items = Items(selected.Count, selectedProps.Count);
            Copy();
            Erase("Cut");
            report($"Cut {items}.");
        }

        /// <summary>
        /// Puts the blocks and the props of the clipboard into the active layer, the anchor cell at the cursor cell, over
        /// what is there, in one step of undo; they become the selection. Decorations are skipped in the middle layer,
        /// which the message counts. When that leaves nothing to paste, the selection stays as it was (Ctrl+V).
        /// </summary>
        public void Paste()
        {
            if (ClipboardCount == 0)
            {
                report("The clipboard is empty.");
                return;
            }
            Cell origin = placeTool.Cursor;
            List<PlacedBlock> pasted = new List<PlacedBlock>(clipboard.Count);
            List<PlacedScenery> pastedProps = new List<PlacedScenery>(propClipboard.Count);
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
                //What is not seen is not pasted: the props wait in the clipboard until the scenery is shown
                foreach (CopiedProp copied in scenery ? propClipboard : new List<CopiedProp>())
                {
                    if (level.AddScenery(copied.Model, Round(origin.X * Units.GridCell + copied.X), Round(origin.Y * Units.GridCell + copied.Y),
                        Round(LevelView.Depth(layer) + copied.Depth), copied.Turn) == null)
                        pastedProps.Add(level.Scenery[level.Scenery.Count - 1]);
                }
            }
            string note = skipped == 0 ? "" : $"{skipped} {(skipped == 1 ? "decoration" : "decorations")} skipped in the {layer.ToString().ToLowerInvariant()} layer";
            if (!scenery && propClipboard.Count > 0)
                note += (note.Length > 0 ? ", " : "") + Items(0, propClipboard.Count) + " skipped: the scenery is hidden";
            //No step was recorded when everything was refused or was there already: nothing changed, the selection stays
            if (level.Version == version)
            {
                report(note.Length == 0 ? "Nothing pasted: the blocks are there already." : "Nothing pasted; " + note + ".");
                return;
            }
            ClearSelection();
            selected.AddRange(pasted);
            selectedProps.AddRange(pastedProps);
            report($"Pasted {Items(pasted.Count, pastedProps.Count)}" + (note.Length == 0 ? "." : "; " + note + "."));
        }

        /// <summary>Erases the selected blocks and removes the selected props, in one step of undo (Delete, the Inspector).</summary>
        public void Delete()
        {
            if (!Any)
            {
                report("Nothing is selected.");
                return;
            }
            string items = Items(selected.Count, selectedProps.Count);
            Erase("Delete");
            report($"Deleted {items}.");
        }

        /// <summary>
        /// Flips the selection left to right around the middle of its cells, swapping the blocks for their mirrored
        /// variants and turning the props as their mirror images, in one step of undo (H, the Inspector).
        /// </summary>
        public void Mirror()
        {
            if (!Bounds(out Cell min, out Cell max))
            {
                report("Nothing is selected.");
                return;
            }
            int sum = min.X + max.X;
            int version = level.Version;
            using (level.BeginCompound("Mirror"))
            {
                level.MirrorBlocks(layer, selected, sum);
                level.MirrorScenery(selectedProps, sum);
            }
            if (level.Version != version)
                report($"Mirrored {Items(selected.Count, selectedProps.Count)}.");
            else
                report("Nothing to mirror: the selection stays as it is.");
        }

        /// <summary>Draws the selection, the cells a drag would move it to, or the rectangle being dragged.</summary>
        public void Draw(EditorMarkers markers, Matrix view, Matrix projection)
        {
            markers.DrawBlocks(view, projection, layer, Selection, 0, 0, false);
            propSpheres.Clear();
            foreach (PlacedScenery prop in SelectedProps)
            {
                BoundingSphere? sphere = propBounds(prop);
                if (sphere.HasValue)
                    propSpheres.Add(sphere.Value);
            }
            markers.DrawProps(view, projection, propSpheres, 0, 0, false);
            if (drag == Drag.Move)
            {
                Cell to = placeTool.Cursor;
                if (to != dragFrom)
                {
                    markers.DrawBlocks(view, projection, layer, selected, to.X - dragFrom.X, to.Y - dragFrom.Y, true);
                    markers.DrawProps(view, projection, propSpheres, to.X - dragFrom.X, to.Y - dragFrom.Y, true);
                }
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

            //A cell that is not selected is selected alone and dragged; one that is, is dragged with the others. An empty
            //cell begins a rectangle, and the selection stays until it is finished (Esc keeps it)
            if (!IsSelected(cell))
            {
                if (!HasAnything(cell))
                    return;
                SelectCell(cell);
                ReportSelected();
            }
            drag = Drag.Move;
        }

        /// <summary>
        /// The button went up after a drag of the selection: it moves by the cells the cursor moved, or a click on it
        /// selects its cell alone.
        /// </summary>
        private void FinishMove()
        {
            Cell to = placeTool.Cursor;
            int dx = to.X - dragFrom.X, dy = to.Y - dragFrom.Y;
            if (dx != 0 || dy != 0)
            {
                Forget();
                ReportMoved(Move(dx, dy));
            }
            else if (selected.Count + selectedProps.Count > 1)
            {
                SelectCell(dragFrom);
                ReportSelected();
            }
        }

        /// <summary>
        /// The button went up after a drag from an empty cell (or with Ctrl): the blocks and the props in the rectangle are
        /// selected, a click without a drag selects the cell alone.
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
                SelectCell(to);
                ReportSelected();
                return;
            }
            int minX = Math.Min(dragFrom.X, to.X), maxX = Math.Max(dragFrom.X, to.X);
            int minY = Math.Min(dragFrom.Y, to.Y), maxY = Math.Max(dragFrom.Y, to.Y);
            bool Inside(Cell cell) => cell.X >= minX && cell.X <= maxX && cell.Y >= minY && cell.Y <= maxY;
            if (!additive)
                ClearSelection();
            foreach (PlacedBlock block in level.Blocks(layer))
                if (Inside(block.Cell) && (!additive || !selected.Contains(block)))
                    selected.Add(block);
            foreach (PlacedScenery prop in PropsOfLayer())
                if (Inside(prop.Cell) && (!additive || !selectedProps.Contains(prop)))
                    selectedProps.Add(prop);
            ReportSelected();
        }

        /// <summary>Selects a cell alone: its block (the one placed last, when it holds two) and its props; nothing for an empty cell.</summary>
        private void SelectCell(Cell cell)
        {
            ClearSelection();
            PlacedBlock block = level.BlockAt(layer, cell);
            if (block != null)
                selected.Add(block);
            foreach (PlacedScenery prop in PropsOfLayer())
                if (prop.Cell == cell)
                    selectedProps.Add(prop);
        }

        /// <summary>The cell holds a block of the layer or a prop standing in it.</summary>
        private bool HasAnything(Cell cell)
        {
            if (level.BlockAt(layer, cell) != null)
                return true;
            foreach (PlacedScenery prop in PropsOfLayer())
                if (prop.Cell == cell)
                    return true;
            return false;
        }

        /// <summary>A block or a prop of the cell is selected.</summary>
        private bool IsSelected(Cell cell)
        {
            return selected.Exists(block => block.Cell == cell) || selectedProps.Exists(prop => prop.Cell == cell);
        }

        /// <summary>Adds the block and the props of a cell to the selection, or takes them out when any of them is in.</summary>
        /// <param name="complain">Say so when the cell is empty (the key does, a click does not).</param>
        private void Toggle(Cell cell, bool complain)
        {
            Forget();
            int removed = selected.RemoveAll(block => block.Cell == cell) + selectedProps.RemoveAll(prop => prop.Cell == cell);
            if (removed == 0)
            {
                PlacedBlock block = level.BlockAt(layer, cell);
                List<PlacedScenery> props = new List<PlacedScenery>();
                foreach (PlacedScenery prop in PropsOfLayer())
                    if (prop.Cell == cell)
                        props.Add(prop);
                if (block == null && props.Count == 0)
                {
                    if (complain)
                        report($"There is nothing at {cell}.");
                    return;
                }
                if (block != null)
                    selected.Add(block);
                selectedProps.AddRange(props);
            }
            ReportSelected();
        }

        /// <summary>The arrows: the selection and the cursor with it move by a cell, one step of undo.</summary>
        private void MoveSelection(Point step)
        {
            if (ReportMoved(Move(step.X, step.Y)))
                placeTool.MoveCursor(step);
        }

        /// <summary>Moves the selected blocks and props by whole cells as one step of undo; false when nothing changed.</summary>
        private bool Move(int dx, int dy)
        {
            int version = level.Version;
            using (level.BeginCompound("Move"))
            {
                level.MoveBlocks(layer, selected, dx, dy);
                foreach (PlacedScenery prop in selectedProps)
                    level.MoveScenery(prop, Round(prop.X + dx * Units.GridCell), Round(prop.Y + dy * Units.GridCell), prop.Z);
            }
            return level.Version != version;
        }

        /// <summary>Tells that the selection moved, or that it did not (the document recorded no step); the same.</summary>
        private bool ReportMoved(bool moved)
        {
            report(moved ? $"Moved {Items(selected.Count, selectedProps.Count)}." : "Nothing moved.");
            return moved;
        }

        /// <summary>
        /// The cell the clipboard is relative to: the leftmost of the selected cells, the lowest of those if several
        /// (the one a climb or a descent starts on, which is on the row of the cursor when it is pasted).
        /// </summary>
        private Cell Anchor()
        {
            Cell anchor = default;
            bool first = true;
            foreach (Cell cell in SelectedCells())
            {
                if (first || cell.X < anchor.X || cell.X == anchor.X && cell.Y > anchor.Y)
                    anchor = cell;
                first = false;
            }
            return anchor;
        }

        private void ShiftSelection(int steps)
        {
            if (!Any)
            {
                report("Nothing is selected.");
                return;
            }
            using (level.BeginCompound("Shift"))
            {
                foreach (PlacedBlock block in selected)
                    level.Shift(layer, block, steps);
                foreach (PlacedScenery prop in selectedProps)
                    level.MoveScenery(prop, prop.X, Round(prop.Y + steps * Units.ShiftStep), prop.Z);
            }
            report($"Shifted {Items(selected.Count, selectedProps.Count)} by {steps}.");
        }

        /// <summary>Erases the selected blocks and removes the selected props as one step of undo, and drops the selection.</summary>
        private void Erase(string step)
        {
            using (level.BeginCompound(step))
            {
                foreach (PlacedBlock block in selected)
                    level.Erase(layer, block);
                foreach (PlacedScenery prop in selectedProps)
                    level.RemoveScenery(prop);
            }
            ClearSelection();
        }

        private void ClearSelection()
        {
            selected.Clear();
            selectedProps.Clear();
        }

        private void ReportSelected()
        {
            report(selected.Count + selectedProps.Count == 0 ? "Nothing is selected." : $"Selected {Items(selected.Count, selectedProps.Count)}.");
        }

        /// <summary>The cells of the selected blocks and of the feet of the selected props.</summary>
        private IEnumerable<Cell> SelectedCells()
        {
            foreach (PlacedBlock block in Selection)
                yield return block.Cell;
            foreach (PlacedScenery prop in SelectedProps)
                yield return prop.Cell;
        }

        /// <summary>The props of the scenery that stand in the active layer; none while the scenery is hidden.</summary>
        private IEnumerable<PlacedScenery> PropsOfLayer()
        {
            if (!scenery)
                yield break;
            foreach (PlacedScenery prop in level.Scenery)
                if (prop.Layer == layer)
                    yield return prop;
        }

        /// <summary>A count of blocks as a phrase: "1 block", "3 blocks".</summary>
        public static string Blocks(int count) => count == 1 ? "1 block" : count + " blocks";

        /// <summary>A count of blocks and props as a phrase: "3 blocks", "1 prop", "3 blocks and 2 props"; empty for none.</summary>
        public static string Items(int blocks, int props)
        {
            string propsText = props == 1 ? "1 prop" : props + " props";
            if (props == 0)
                return blocks == 0 ? "" : Blocks(blocks);
            return blocks == 0 ? propsText : Blocks(blocks) + " and " + propsText;
        }

        /// <summary>A coordinate rounded to two decimals, as the files keep it (a float does not hold a sum exactly).</summary>
        private static float Round(float value) => MathF.Round(value, Decimals);

        /// <summary>The blocks that are no longer in the layer and the props that are no longer in the level or in the layer (erased, undone, moved) leave the selection.</summary>
        private void Forget()
        {
            if (level == null || level.Version == checkedVersion)
                return;
            checkedVersion = level.Version;
            if (selected.Count > 0)
            {
                HashSet<PlacedBlock> present = new HashSet<PlacedBlock>(level.Blocks(layer));
                selected.RemoveAll(block => !present.Contains(block));
            }
            if (selectedProps.Count > 0)
            {
                HashSet<PlacedScenery> props = new HashSet<PlacedScenery>(level.Scenery);
                selectedProps.RemoveAll(prop => !props.Contains(prop) || prop.Layer != layer);
            }
        }
    }
}
