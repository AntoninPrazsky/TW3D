using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TW3D.Shared;

namespace TW3D.Editor
{
    /// <summary>A cell of the level grid: the position of a block in a layer.</summary>
    readonly record struct Cell(int X, int Y)
    {
        /// <summary>The cell a point in physics units falls in.</summary>
        public static Cell Of(float x, float y)
        {
            return new Cell((int)Math.Round(x / Units.GridCell), (int)Math.Round(y / Units.GridCell));
        }

        /// <summary>
        /// The cells a stroke of the mouse passes from this cell to another one, in order, this cell left out and the
        /// other one included: the mouse may cross several cells between two frames, and a stroke must not skip them.
        /// </summary>
        public IEnumerable<Cell> LineTo(Cell to)
        {
            int dx = to.X - X, dy = to.Y - Y;
            int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
            for (int i = 1; i <= steps; i++)
            {
                yield return new Cell(
                    X + (int)Math.Round(dx * (double)i / steps, MidpointRounding.AwayFromZero),
                    Y + (int)Math.Round(dy * (double)i / steps, MidpointRounding.AwayFromZero));
            }
        }

        /// <summary>The cell as the level files and the messages write it, e.g. "[3, -1]".</summary>
        public override string ToString() => $"[{X}, {Y}]";
    }

    /// <summary>
    /// A block in a layer of the edited level. The document alone changes it (through its commands, so that undo
    /// can put it back); a command that changes it keeps its place in the file and its identity.
    /// </summary>
    sealed class PlacedBlock
    {
        /// <param name="name">Name of the block type in the catalog.</param>
        public PlacedBlock(Cell cell, string name, int shift, bool finish)
        {
            Cell = cell;
            Name = name;
            Shift = shift;
            Finish = finish;
        }

        /// <summary>The cell of the layer the block is in.</summary>
        public Cell Cell { get; internal set; }

        /// <summary>Name of the block type in the catalog.</summary>
        public string Name { get; internal set; }

        /// <summary>Vertical shift in steps (see <see cref="Units.ShiftStep"/>).</summary>
        public int Shift { get; internal set; }

        /// <summary>The block is the finish of the level (a block of the middle layer only).</summary>
        public bool Finish { get; internal set; }
    }

    /// <summary>A dynamic object of the edited level. The document alone moves it, so that undo can move it back.</summary>
    sealed class PlacedObject
    {
        /// <param name="type">Name of the object type in the catalog.</param>
        /// <param name="model">One of the models of the object type.</param>
        public PlacedObject(string type, string model, float x, float y)
        {
            Type = type;
            Model = model;
            X = x;
            Y = y;
        }

        /// <summary>Name of the object type in the catalog.</summary>
        public string Type { get; }

        /// <summary>One of the models of the object type.</summary>
        public string Model { get; }

        /// <summary>Position in physics units.</summary>
        public float X { get; internal set; }
        public float Y { get; internal set; }
    }

    /// <summary>
    /// Something wrong with the edited level, and where it is, so that the screen can show the place: a block has a
    /// layer and a cell, an object a position, a problem of the whole level neither.
    /// </summary>
    sealed class LevelProblem
    {
        /// <summary>A problem of the whole level, without a place.</summary>
        public LevelProblem(string message)
        {
            Message = message;
        }

        /// <summary>A problem of the block of a layer at a cell.</summary>
        public LevelProblem(string message, Layer layer, Cell cell)
        {
            Message = message;
            Layer = layer;
            Cell = cell;
        }

        /// <summary>A problem of the object at a position (physics units).</summary>
        public LevelProblem(string message, float x, float y)
        {
            Message = message;
            X = x;
            Y = y;
        }

        /// <summary>What is wrong, as a sentence.</summary>
        public string Message { get; }

        /// <summary>The layer and the cell of the block the problem is about; null for other problems.</summary>
        public Layer? Layer { get; }
        public Cell? Cell { get; }

        /// <summary>The position (physics units) of the object the problem is about; null for other problems.</summary>
        public float? X { get; }
        public float? Y { get; }

        /// <summary>The message.</summary>
        public override string ToString() => Message;
    }

    /// <summary>
    /// The level being edited, as a document: three layers of blocks, the dynamic objects, the mood, the order and the
    /// par, built from a <see cref="LevelData"/> and written back to one. Every change is a command that can be undone
    /// and redone; commands that can break the level refuse with a reason instead. The blocks keep the order of the
    /// file they were read in (new ones are appended, a block put back by undo goes to its old place), so that a level
    /// saved unchanged, or changed and undone, is written exactly as it was read.
    /// </summary>
    sealed class EditedLevel
    {
        //The block a new level starts with, at the start cell, where the car starts
        private const string StartBlock = "a1";
        //How many cells below its own an object may hang over before it is standing on nothing
        private const int ObjectDrop = 2;

        private readonly Catalog catalog;
        //The blocks of each layer, indexed by the Layer enum, in the order of the file
        private readonly List<PlacedBlock>[] layers = new List<PlacedBlock>[3];
        private readonly List<PlacedObject> objects = new List<PlacedObject>();
        private LevelMood mood;
        private int? order;
        private float? par;

        //The steps done so far: the first position of them are done, the rest are undone and can be redone
        private readonly List<Step> history = new List<Step>();
        private int position;
        //The position in the history the file holds; -1 when that state is not in the history any more
        private int savedPosition;
        //The step the open compounds collect, and how many of them are open (nested ones are flattened into the outermost)
        private Step compound;
        private int compoundDepth;

        /// <param name="catalog">The blocks and the objects the level can have: the commands keep the level to it.</param>
        /// <param name="data">The level to edit; its lists are copied.</param>
        /// <param name="file">The full path of the file the level was read from; null for a new level.</param>
        public EditedLevel(Catalog catalog, LevelData data, string file)
        {
            this.catalog = catalog;
            File = file;
            mood = data.Mood;
            order = data.Order;
            par = data.Par;
            layers[(int)Layer.Back] = data.Back.Select(Convert).ToList();
            layers[(int)Layer.Middle] = data.Middle.Select(Convert).ToList();
            layers[(int)Layer.Front] = data.Front.Select(Convert).ToList();
            objects.AddRange(data.Objects.Select(placement => new PlacedObject(placement.Type, placement.Model, placement.X, placement.Y)));
        }

        /// <summary>A new level, not in any file: the start block at the start cell, where the car starts.</summary>
        public static EditedLevel New(Catalog catalog)
        {
            LevelData data = new LevelData();
            data.Middle.Add(new BlockPlacement { Block = StartBlock, X = 0, Y = 0 });
            return new EditedLevel(catalog, data, null);
        }

        /// <summary>The catalog the level is kept to.</summary>
        public Catalog Catalog => catalog;

        /// <summary>The full path of the file of the level; null for a new level that was not saved yet.</summary>
        public string File { get; private set; }

        /// <summary>The light of the level.</summary>
        public LevelMood Mood => mood;

        /// <summary>The position of the level in the level select; null for a level without one.</summary>
        public int? Order => order;

        /// <summary>The time a good run takes, in seconds; null when the game is to estimate it.</summary>
        public float? Par => par;

        /// <summary>The dynamic objects, in the order of the file.</summary>
        public IReadOnlyList<PlacedObject> Objects => objects;

        /// <summary>The finish block of the level; null when it has none.</summary>
        public PlacedBlock Finish => layers[(int)Layer.Middle].FirstOrDefault(block => block.Finish);

        /// <summary>Grows by one on every change, undo and redo, so that a view knows when to redraw.</summary>
        public int Version { get; private set; }

        /// <summary>
        /// The level differs from the file: it was changed since it was loaded, created or last saved. The changes of
        /// an open compound count, although they are not a step of the history yet.
        /// </summary>
        public bool IsDirty => position != savedPosition || (compound != null && compound.Changes.Count > 0);

        /// <summary>There is a step to take back; false while a compound is open.</summary>
        public bool CanUndo => position > 0 && compoundDepth == 0;

        /// <summary>There is an undone step to do again; false while a compound is open.</summary>
        public bool CanRedo => position < history.Count && compoundDepth == 0;

        /// <summary>The name of the step Undo would take back (e.g. "Place a1", "Erase"); null when there is none.</summary>
        public string UndoName => CanUndo ? history[position - 1].Name : null;

        /// <summary>The name of the step Redo would do again; null when there is none.</summary>
        public string RedoName => CanRedo ? history[position].Name : null;

        /// <summary>The blocks of a layer, in the order of the file.</summary>
        public IReadOnlyList<PlacedBlock> Blocks(Layer layer) => layers[(int)layer];

        /// <summary>
        /// The block of a layer at a cell; null when the cell is empty. A file may hold two blocks in one cell (a base
        /// and a gate on it): then it is the later one, the one placed last.
        /// </summary>
        public PlacedBlock BlockAt(Layer layer, Cell cell)
        {
            int index = IndexAt(layer, cell);
            return index >= 0 ? layers[(int)layer][index] : null;
        }

        /// <summary>The level as data to save or to play, everything in the order of the file.</summary>
        public LevelData ToData()
        {
            LevelData data = new LevelData
            {
                Format = LevelData.CurrentFormat,
                Order = order,
                Mood = mood,
                Par = par
            };
            data.Back.AddRange(layers[(int)Layer.Back].Select(Convert));
            data.Middle.AddRange(layers[(int)Layer.Middle].Select(Convert));
            data.Front.AddRange(layers[(int)Layer.Front].Select(Convert));
            data.Objects.AddRange(objects.Select(obj => new ObjectPlacement { Type = obj.Type, Model = obj.Model, X = obj.X, Y = obj.Y }));
            return data;
        }

        /// <summary>The level was written to the file: it is clean until the next change.</summary>
        public void MarkSaved()
        {
            savedPosition = position;
        }

        /// <summary>The level was written to another file (save as): that is its file now, and it is clean.</summary>
        public void SavedAs(string file)
        {
            File = file;
            MarkSaved();
        }

        /// <summary>
        /// Places a block in a cell of a layer, or replaces the block there (which keeps its place in the file and its
        /// finish flag). Refused, with the reason, for a block the catalog does not know and for decoration (a block
        /// without a collision shape) in the middle layer; null when done.
        /// </summary>
        public string Place(Layer layer, Cell cell, string block, int shift)
        {
            BlockType type = catalog.FindBlock(block);
            if (type == null)
                return $"Block {block} is not in the catalog.";
            if (layer == Layer.Middle && !type.Collision)
                return $"Block {block} is decoration without a collision shape; it can only be in the back or front layer.";

            List<PlacedBlock> blocks = layers[(int)layer];
            PlacedBlock existing = BlockAt(layer, cell);
            if (existing == null)
            {
                PlacedBlock placed = new PlacedBlock(cell, block, shift, false);
                int index = blocks.Count;
                Record("Place " + block, () => blocks.Insert(index, placed), () => blocks.RemoveAt(index));
            }
            else if (existing.Name != block || existing.Shift != shift)
            {
                string oldName = existing.Name;
                int oldShift = existing.Shift;
                Record("Place " + block,
                    () => Set(existing, block, shift),
                    () => Set(existing, oldName, oldShift));
            }
            return null;
        }

        /// <summary>
        /// Erases the block of a layer at a cell (the one placed last, when the cell holds two); false when the cell is
        /// empty (nothing to undo then).
        /// </summary>
        public bool Erase(Layer layer, Cell cell)
        {
            PlacedBlock block = BlockAt(layer, cell);
            return block != null && Erase(layer, block);
        }

        /// <summary>Erases a block of a layer; false when the block is not in the layer (nothing to undo then).</summary>
        public bool Erase(Layer layer, PlacedBlock block)
        {
            List<PlacedBlock> blocks = layers[(int)layer];
            int index = blocks.IndexOf(block);
            if (index < 0)
                return false;
            Record("Erase", () => blocks.RemoveAt(index), () => blocks.Insert(index, block));
            return true;
        }

        /// <summary>
        /// Adds steps to the shift of the block of a layer at a cell (the one placed last, when the cell holds two);
        /// false when the cell is empty.
        /// </summary>
        public bool Shift(Layer layer, Cell cell, int steps)
        {
            PlacedBlock block = BlockAt(layer, cell);
            return block != null && Shift(layer, block, steps);
        }

        /// <summary>Adds steps to the shift of a block of a layer; false when the block is not in the layer.</summary>
        public bool Shift(Layer layer, PlacedBlock block, int steps)
        {
            if (!layers[(int)layer].Contains(block))
                return false;
            if (steps != 0)
                Record("Shift", () => block.Shift += steps, () => block.Shift -= steps);
            return true;
        }

        /// <summary>
        /// Makes the block at a cell the only finish of the level; the only finish itself stops being one (the level
        /// has none then). Refused, with the reason, outside the middle layer and in an empty cell; null when done.
        /// </summary>
        public string SetFinish(Layer layer, Cell cell)
        {
            if (layer != Layer.Middle)
                return "Only a block of the middle layer can be the finish.";
            PlacedBlock block = BlockAt(layer, cell);
            if (block == null)
                return $"There is no block of the middle layer at {cell}.";

            //A loaded file may hold more finishes than one: all of them lose the flag, so that the block is the only one
            List<PlacedBlock> others = layers[(int)Layer.Middle].Where(other => other.Finish && other != block).ToList();
            bool wasFinish = block.Finish;
            //The finish toggles off only when it is the only one; one of several becomes the only one instead
            bool becomesFinish = !wasFinish || others.Count > 0;
            Record("Finish",
                () => SetFinish(block, becomesFinish, others, false),
                () => SetFinish(block, wasFinish, others, true));
            return null;
        }

        /// <summary>Sets the light of the level; nothing is recorded when it does not change.</summary>
        public void SetMood(LevelMood value)
        {
            LevelMood previous = mood;
            if (value != previous)
                Record("Mood", () => mood = value, () => mood = previous);
        }

        /// <summary>Sets the position of the level in the level select; nothing is recorded when it does not change.</summary>
        public void SetOrder(int? value)
        {
            int? previous = order;
            if (value != previous)
                Record("Order", () => order = value, () => order = previous);
        }

        /// <summary>Sets the par time of the level; nothing is recorded when it does not change.</summary>
        public void SetPar(float? value)
        {
            float? previous = par;
            if (value != previous)
                Record("Par", () => par = value, () => par = previous);
        }

        /// <summary>
        /// Adds a dynamic object after the others. Refused, with the reason, for an object type the catalog does not
        /// know and for a model the type does not have; null when done.
        /// </summary>
        public string AddObject(string type, string model, float x, float y)
        {
            ObjectType objectType = catalog.FindObject(type);
            if (objectType == null)
                return $"Object type {type} is not in the catalog.";
            if (!objectType.Models.Contains(model))
                return $"Object type {type} has no model {model}.";

            PlacedObject added = new PlacedObject(type, model, x, y);
            int index = objects.Count;
            Record("Add " + type, () => objects.Insert(index, added), () => objects.RemoveAt(index));
            return null;
        }

        /// <summary>Moves a dynamic object of the level to a position in physics units; nothing is recorded when it is there already.</summary>
        public void MoveObject(PlacedObject obj, float x, float y)
        {
            float oldX = obj.X;
            float oldY = obj.Y;
            if (x == oldX && y == oldY)
                return;
            Record("Move " + obj.Type,
                () => Move(obj, x, y),
                () => Move(obj, oldX, oldY));
        }

        /// <summary>Removes a dynamic object of the level; undo puts it back in its place.</summary>
        public void RemoveObject(PlacedObject obj)
        {
            int index = objects.IndexOf(obj);
            if (index < 0)
                return;
            Record("Remove " + obj.Type, () => objects.RemoveAt(index), () => objects.Insert(index, obj));
        }

        /// <summary>
        /// Every command until the returned object is disposed is one step of undo, with the name: a painted stroke,
        /// a moved selection. Compounds nest, flattened into the outermost one; a compound with no command records
        /// nothing, and a command refused inside one does not abort it.
        /// </summary>
        public IDisposable BeginCompound(string name)
        {
            if (compoundDepth == 0)
                compound = new Step(name);
            compoundDepth++;
            return new CompoundScope(this);
        }

        private void EndCompound()
        {
            compoundDepth--;
            if (compoundDepth > 0)
                return;
            Step step = compound;
            compound = null;
            if (step.Changes.Count > 0)
                Push(step);
        }

        /// <summary>Takes back the last step; nothing when there is none (or a compound is open).</summary>
        public void Undo()
        {
            if (!CanUndo)
                return;
            position--;
            Step step = history[position];
            for (int i = step.Changes.Count - 1; i >= 0; i--)
                step.Changes[i].Revert();
            Version++;
        }

        /// <summary>Does the last undone step again; nothing when there is none (or a compound is open).</summary>
        public void Redo()
        {
            if (!CanRedo)
                return;
            Step step = history[position];
            foreach (Change change in step.Changes)
                change.Apply();
            position++;
            Version++;
        }

        /// <summary>
        /// Everything wrong with the level: what <see cref="LevelData.Validate"/> finds, in its words (checked here,
        /// so that each problem of a block or an object has its place), a missing finish, no block of the middle layer
        /// at the start cell (the car would fall), and an object with no block of the middle layer in its cell or the
        /// two below it (it would fall too). Empty for a level fit to play.
        /// </summary>
        public List<LevelProblem> Problems()
        {
            List<LevelProblem> problems = new List<LevelProblem>();
            FindBlockProblems(Layer.Middle, problems);
            FindBlockProblems(Layer.Back, problems);
            FindBlockProblems(Layer.Front, problems);
            foreach (Layer layer in new[] { Layer.Back, Layer.Front })
                foreach (PlacedBlock block in layers[(int)layer].Where(block => block.Finish))
                    problems.Add(new LevelProblem($"Block \"{block.Name}\" at {block.Cell} is a finish, but only a block of the middle layer can be.", layer, block.Cell));
            if (layers[(int)Layer.Middle].Count(block => block.Finish) > 1)
                problems.Add(new LevelProblem("The level has more than one finish."));
            if (par <= 0f)
                problems.Add(new LevelProblem("The par time of the level is not positive."));
            foreach (PlacedObject obj in objects)
            {
                ObjectType type = catalog.FindObject(obj.Type);
                if (type == null)
                    problems.Add(new LevelProblem($"Unknown object type \"{obj.Type}\" at {Position(obj)}.", obj.X, obj.Y));
                else if (!type.Models.Contains(obj.Model))
                    problems.Add(new LevelProblem($"Object type \"{obj.Type}\" has no model \"{obj.Model}\" (at {Position(obj)}).", obj.X, obj.Y));
            }

            if (Finish == null)
                problems.Add(new LevelProblem("The level has no finish."));
            Cell start = new Cell(0, 0);
            if (BlockAt(Layer.Middle, start) == null)
                problems.Add(new LevelProblem($"There is no block of the middle layer at the start cell {start}, where the car starts.", Layer.Middle, start));
            foreach (PlacedObject obj in objects)
            {
                if (!HasGroundBelow(obj))
                    problems.Add(new LevelProblem($"Object {obj.Type} ({obj.Model}) at {Position(obj)} has nothing below it.", obj.X, obj.Y));
            }
            return problems;
        }

        /// <summary>The blocks of a layer the catalog does not know, and decoration in the middle layer, as <see cref="LevelData.Validate"/> words them.</summary>
        private void FindBlockProblems(Layer layer, List<LevelProblem> problems)
        {
            string layerName = layer.ToString().ToLowerInvariant();
            foreach (PlacedBlock block in layers[(int)layer])
            {
                BlockType type = catalog.FindBlock(block.Name);
                if (type == null)
                    problems.Add(new LevelProblem($"Unknown block \"{block.Name}\" at {block.Cell} in the {layerName} layer.", layer, block.Cell));
                else if (layer == Layer.Middle && !type.Collision)
                    problems.Add(new LevelProblem($"Block \"{block.Name}\" at {block.Cell} has no collision shape, so it cannot be in the {layerName} layer.", layer, block.Cell));
            }
        }

        /// <summary>A block of the middle layer is in the cell of the object or within a few cells below it (y grows downward).</summary>
        private bool HasGroundBelow(PlacedObject obj)
        {
            Cell cell = Cell.Of(obj.X, obj.Y);
            for (int below = 0; below <= ObjectDrop; below++)
                if (BlockAt(Layer.Middle, new Cell(cell.X, cell.Y + below)) != null)
                    return true;
            return false;
        }

        /// <summary>The position of an object as the messages write it, with a decimal point whatever the culture.</summary>
        private static string Position(PlacedObject obj)
        {
            return string.Format(CultureInfo.InvariantCulture, "[{0}, {1}]", obj.X, obj.Y);
        }

        private int IndexAt(Layer layer, Cell cell)
        {
            return layers[(int)layer].FindLastIndex(block => block.Cell == cell);
        }

        private static void Set(PlacedBlock block, string name, int shift)
        {
            block.Name = name;
            block.Shift = shift;
        }

        private static void SetFinish(PlacedBlock block, bool finish, List<PlacedBlock> others, bool othersFinish)
        {
            block.Finish = finish;
            foreach (PlacedBlock other in others)
                other.Finish = othersFinish;
        }

        private static void Move(PlacedObject obj, float x, float y)
        {
            obj.X = x;
            obj.Y = y;
        }

        private static PlacedBlock Convert(BlockPlacement placement)
        {
            return new PlacedBlock(new Cell(placement.X, placement.Y), placement.Block, placement.Shift, placement.Finish);
        }

        private static BlockPlacement Convert(PlacedBlock block)
        {
            return new BlockPlacement { Block = block.Name, X = block.Cell.X, Y = block.Cell.Y, Shift = block.Shift, Finish = block.Finish };
        }

        /// <summary>Does a change and records it: as a step of its own, or as a part of the open compound.</summary>
        private void Record(string name, Action apply, Action revert)
        {
            apply();
            Version++;
            Change change = new Change(apply, revert);
            if (compound != null)
            {
                compound.Changes.Add(change);
                return;
            }
            Step step = new Step(name);
            step.Changes.Add(change);
            Push(step);
        }

        /// <summary>Adds a done step to the history; the steps undone before it cannot be redone any more.</summary>
        private void Push(Step step)
        {
            //The saved state is lost with the undone steps when it was among them
            if (savedPosition > position)
                savedPosition = -1;
            history.RemoveRange(position, history.Count - position);
            history.Add(step);
            position++;
        }

        /// <summary>One change of the level and how to take it back.</summary>
        private sealed class Change
        {
            public Change(Action apply, Action revert)
            {
                Apply = apply;
                Revert = revert;
            }

            public Action Apply { get; }
            public Action Revert { get; }
        }

        /// <summary>One step of undo: a command, or the commands of a compound.</summary>
        private sealed class Step
        {
            public Step(string name)
            {
                Name = name;
            }

            public string Name { get; }
            public List<Change> Changes { get; } = new List<Change>();
        }

        /// <summary>Ends the compound when disposed, once.</summary>
        private sealed class CompoundScope : IDisposable
        {
            private EditedLevel level;

            public CompoundScope(EditedLevel level)
            {
                this.level = level;
            }

            public void Dispose()
            {
                if (level == null)
                    return;
                level.EndCompound();
                level = null;
            }
        }
    }
}
