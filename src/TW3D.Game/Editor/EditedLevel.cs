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
        /// <param name="turned">The block is turned round (see <see cref="BlockPlacement.Turned"/>).</param>
        /// <param name="style">The style of the block (see <see cref="BlockPlacement.Style"/>); null for the level's.</param>
        public PlacedBlock(Cell cell, string name, int shift, bool finish, bool turned = false, string style = null)
        {
            Cell = cell;
            Name = name;
            Shift = shift;
            Finish = finish;
            Turned = turned;
            Style = style;
        }

        /// <summary>The cell of the layer the block is in.</summary>
        public Cell Cell { get; internal set; }

        /// <summary>Name of the block type in the catalog.</summary>
        public string Name { get; internal set; }

        /// <summary>Vertical shift in steps (see <see cref="Units.ShiftStep"/>).</summary>
        public int Shift { get; internal set; }

        /// <summary>The block is the finish of the level (a block of the middle layer only).</summary>
        public bool Finish { get; internal set; }

        /// <summary>
        /// The block is turned round by 180° about the vertical axis: its road runs the other way (see
        /// <see cref="BlockPlacement.Turned"/>). A file may turn a block that cannot be turned, which is a problem.
        /// </summary>
        public bool Turned { get; internal set; }

        /// <summary>
        /// The style of the block, a style or a transition between two (see <see cref="BlockPlacement.Style"/>); null for
        /// the style of the level.
        /// </summary>
        public string Style { get; internal set; }
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
    /// A prop of the scenery of the edited level. The document alone moves and turns it, so that undo can put it back.
    /// </summary>
    sealed class PlacedScenery
    {
        /// <param name="model">The model of the prop in the catalog.</param>
        /// <param name="turn">Quarter turns about the vertical axis.</param>
        public PlacedScenery(string model, float x, float y, float z, int turn)
        {
            Model = model;
            X = x;
            Y = y;
            Z = z;
            Turn = turn;
        }

        /// <summary>The model of the prop in the catalog.</summary>
        public string Model { get; }

        /// <summary>Where its foot stands, in physics units.</summary>
        public float X { get; internal set; }
        public float Y { get; internal set; }

        /// <summary>The depth in physics units: negative behind the road, positive in front of it.</summary>
        public float Z { get; internal set; }

        /// <summary>Quarter turns about the vertical axis, 0 to 3 (a file may hold another number, which is a problem).</summary>
        public int Turn { get; internal set; }

        /// <summary>
        /// The layer the prop belongs to, by its depth: the one whose cells it stands in (the road of the middle layer
        /// reaches half a cell to either side, each of the other layers the next cell), or the nearest one beyond them.
        /// The Select tool works on the props of a layer with its blocks.
        /// </summary>
        public Layer Layer => Layers.OfCells(LayerCells(Z));

        /// <summary>How many cells in front of the road the layer of a depth is (physics units; the middle of a cell lies on a layer, its edges half way between two).</summary>
        public static int LayerCells(float z)
        {
            float half = Units.GridCell / 2f;
            if (z > half)
                return (int)MathF.Ceiling((z - half) / Units.GridCell);
            return z < -half ? -(int)MathF.Ceiling((-z - half) / Units.GridCell) : 0;
        }

        /// <summary>The cell of the layer its foot is in.</summary>
        public Cell Cell => Cell.Of(X, Y);
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

        /// <summary>
        /// The problem is a gap in the road (see <see cref="EditedLevel.RoadGaps"/>), which may be left on purpose: the
        /// editor lists it apart from the problems that stop the level from being played.
        /// </summary>
        public bool IsRoadGap { get; init; }

        /// <summary>The message.</summary>
        public override string ToString() => Message;
    }

    /// <summary>
    /// The level being edited, as a document: five layers of blocks, the dynamic objects, the props of the scenery, the
    /// mood, the order and the par, built from a <see cref="LevelData"/> and written back to one. Every change is a
    /// command that can be undone and redone; commands that can break the level refuse with a reason instead. The blocks
    /// keep the order of the file they were read in (new ones are appended, a block put back by undo goes to its old
    /// place), and so do the objects and the props, so that a level saved unchanged, or changed and undone, is written
    /// exactly as it was read.
    /// </summary>
    sealed class EditedLevel
    {
        //The block a new level starts with, at the start cell, where the car starts
        private const string StartBlock = "a1";
        //How many cells below its own an object may hang over before it is standing on nothing
        private const int ObjectDrop = 2;
        //How many shift steps a cell is high: the size of a cell over the shift step
        private const int StepsPerCell = 9;

        private readonly Catalog catalog;
        //The layers but the middle one in the order their problems are listed: the back layers from the road, the front ones
        private static readonly Layer[] OuterLayers = { Layer.Back, Layer.Back2, Layer.Back3, Layer.Front, Layer.Front2, Layer.Front3 };

        //The blocks of each layer, indexed by the Layer enum, in the order of the file
        private readonly List<PlacedBlock>[] layers = new List<PlacedBlock>[Layers.Count];
        private readonly List<PlacedObject> objects = new List<PlacedObject>();
        private readonly List<PlacedScenery> scenery = new List<PlacedScenery>();
        private LevelMood mood;
        private string style;
        private int? order;
        private bool debug;
        private float? par;

        //The steps done so far: the first position of them are done, the rest are undone and can be redone
        private readonly List<Step> history = new List<Step>();
        private int position;
        //The looks of the blocks of the middle layer, and the version of the level they were found for
        private Dictionary<PlacedBlock, string> middleLooks;
        private int middleLooksVersion = -1;
        //The road gaps found, and the version of the level they were found for
        private List<RoadGap> roadGaps;
        private int roadGapsVersion = -1;
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
            style = data.Style;
            order = data.Order;
            debug = data.Debug;
            par = data.Par;
            foreach (Layer layer in Layers.All)
                layers[(int)layer] = new List<PlacedBlock>();
            //The back layers are one list in the file, by the depth of the blocks (one out of range goes to the nearest)
            foreach (BlockPlacement placement in data.Back)
                layers[(int)Layers.OfBackDepth(placement.Depth)].Add(Convert(placement));
            layers[(int)Layer.Middle].AddRange(data.Middle.Select(Convert));
            //So are the front layers
            foreach (BlockPlacement placement in data.Front)
                layers[(int)Layers.OfFrontDepth(placement.Depth)].Add(Convert(placement));
            objects.AddRange(data.Objects.Select(placement => new PlacedObject(placement.Type, placement.Model, placement.X, placement.Y)));
            if (data.Scenery != null)
                scenery.AddRange(data.Scenery.Select(placement => new PlacedScenery(placement.Model, placement.X, placement.Y, placement.Z, placement.Turn)));
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

        /// <summary>The style of the track (see <see cref="LevelData.Style"/>); null for the classic one.</summary>
        public string Style => style;

        /// <summary>
        /// The style a block is drawn in: in the middle layer as the game draws the road (see <see cref="RoadLooks"/>: the
        /// transitions where its styles meet), elsewhere its own style or the level's. Found again when the level changes.
        /// </summary>
        public string LookOf(Layer layer, PlacedBlock block)
        {
            if (layer != Layer.Middle)
                return block.Style ?? style;
            if (middleLooks == null || middleLooksVersion != Version)
            {
                List<PlacedBlock> blocks = layers[(int)Layer.Middle];
                string[] looks = RoadLooks.Of(blocks.Select(placed => new RoadBlock(placed.Name, placed.Cell.X, placed.Cell.Y, placed.Shift, placed.Turned, placed.Style)).ToList(),
                    style, catalog);
                middleLooks = new Dictionary<PlacedBlock, string>();
                for (int i = 0; i < blocks.Count; i++)
                    middleLooks[blocks[i]] = looks[i];
                middleLooksVersion = Version;
            }
            return middleLooks.TryGetValue(block, out string look) ? look : block.Style ?? style;
        }

        /// <summary>The position of the level in the level select; null for a level without one.</summary>
        public int? Order => order;

        /// <summary>A level for development and testing, under DEBUG LEVELS in the level select (see <see cref="LevelData.Debug"/>).</summary>
        public bool Debug => debug;

        /// <summary>The time a good run takes, in seconds; null when the game is to estimate it.</summary>
        public float? Par => par;

        /// <summary>The dynamic objects, in the order of the file.</summary>
        public IReadOnlyList<PlacedObject> Objects => objects;

        /// <summary>The props of the scenery, in the order of the file.</summary>
        public IReadOnlyList<PlacedScenery> Scenery => scenery;

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

        /// <summary>The vertical position of a block in shift steps, its base: 9 * y + shift.</summary>
        public static int Base(PlacedBlock block) => StepsPerCell * block.Cell.Y + block.Shift;

        /// <summary>
        /// The height of the road surface at the left edge of a block in shift steps (they grow downward): its base
        /// plus the first number of its profile as placed (<see cref="BlockType.PlacedProfile"/>: a turned block has the
        /// edges of the catalog's profile swapped); null for a block without a profile.
        /// </summary>
        public int? SurfaceLeft(PlacedBlock block)
        {
            int[] profile = ProfileOf(block.Name, block.Turned);
            return profile == null ? null : Base(block) + profile[0];
        }

        /// <summary>The height of the road surface at the right edge of a block in shift steps; null for a block without a profile.</summary>
        public int? SurfaceRight(PlacedBlock block)
        {
            int[] profile = ProfileOf(block.Name, block.Turned);
            return profile == null ? null : Base(block) + profile[1];
        }

        /// <summary>
        /// Where the road of the middle layer breaks: for every road block (one with a profile; the last one when a cell
        /// holds two) the road block in the next column to the right whose left edge is the nearest in height to its right
        /// edge (the only road block of the column, mostly), when the surfaces differ there. A column with no road block
        /// to the right is the end of the road, not a gap. The list is kept until the level changes: do not change it.
        /// </summary>
        public List<RoadGap> RoadGaps()
        {
            if (roadGaps != null && roadGapsVersion == Version)
                return roadGaps;

            List<RoadGap> found = new List<RoadGap>();
            Dictionary<int, List<PlacedBlock>> columns = RoadColumns();
            foreach (PlacedBlock a in layers[(int)Layer.Middle])
            {
                //Only a block that counts as a road block (the last of its cell) has a place in the columns
                if (!columns.TryGetValue(a.Cell.X, out List<PlacedBlock> own) || !own.Contains(a))
                    continue;
                if (!columns.TryGetValue(a.Cell.X + 1, out List<PlacedBlock> next))
                    continue;
                int height = SurfaceRight(a).Value;
                PlacedBlock b = Nearest(next, true, height);
                int other = SurfaceLeft(b).Value;
                if (other != height)
                    found.Add(new RoadGap(a, b, height, other));
            }
            roadGaps = found;
            roadGapsVersion = Version;
            return found;
        }

        /// <summary>
        /// Where and at what shift a road block is to be placed so that it joins the road, as road() of the level
        /// generator does: in the column of the cell, with its left edge at the height of the right edge of the road
        /// block in the column to the left (when painting leftwards, its right edge at the left edge of the one to the
        /// right; where that column has no road block the other side is tried). With several road blocks in the column
        /// of the neighbour, the one nearest in height to the block as it would stand at the cell with the chosen shift
        /// is the neighbour, unless the block placed just before is one of them. With no neighbour at all, and for a
        /// block without a profile, the cell and the shift are the given ones.
        /// </summary>
        /// <param name="cell">The cell the block is placed at: only its column is kept when it joins a neighbour.</param>
        /// <param name="block">The name of the block in the catalog.</param>
        /// <param name="turned">The block is placed turned: its profile as placed has the edges swapped.</param>
        /// <param name="chosenShift">The shift chosen by hand.</param>
        /// <param name="leftwards">The road is painted from the right to the left: the neighbour on the right is joined first.</param>
        /// <param name="placedCell">The cell the block goes to.</param>
        /// <returns>The shift the block goes with.</returns>
        public int RoadShift(Cell cell, string block, bool turned, int chosenShift, bool leftwards, out Cell placedCell)
        {
            return RoadShift(cell, block, turned, chosenShift, leftwards, null, out placedCell);
        }

        /// <summary>
        /// <see cref="RoadShift(Cell, string, bool, int, bool, out Cell)"/> for a stroke: the block that the stroke placed
        /// before this one is the neighbour when it stands in the column next to the block, whatever else that column
        /// holds (the mouse height has nothing to do with where the road went).
        /// </summary>
        /// <param name="joined">The road block placed just before, or null.</param>
        public int RoadShift(Cell cell, string block, bool turned, int chosenShift, bool leftwards, PlacedBlock joined, out Cell placedCell)
        {
            placedCell = cell;
            int[] profile = ProfileOf(block, turned);
            if (profile == null)
                return chosenShift;

            Dictionary<int, List<PlacedBlock>> columns = RoadColumns();
            int own = StepsPerCell * cell.Y + chosenShift;
            for (int pass = 0; pass < 2; pass++)
            {
                //The neighbour in the direction of the painting first
                bool right = leftwards == (pass == 0);
                if (!columns.TryGetValue(cell.X + (right ? 1 : -1), out List<PlacedBlock> column))
                    continue;
                //The edge of the block next to the neighbour, and the edge of the neighbour next to the block
                int edge = profile[right ? 1 : 0];
                PlacedBlock neighbour = joined != null && column.Contains(joined) ? joined : Nearest(column, right, own + edge);
                int height = right ? SurfaceLeft(neighbour).Value : SurfaceRight(neighbour).Value;
                int placedBase = height - edge;
                //The cell nearest to the base, as the generator rounds it; a base is never half way between two cells
                int y = (int)Math.Floor((placedBase + StepsPerCell / 2) / (double)StepsPerCell);
                placedCell = new Cell(cell.X, y);
                return placedBase - StepsPerCell * y;
            }
            return chosenShift;
        }

        /// <summary>
        /// The road block that a road block placed by <see cref="RoadShift(Cell, string, bool, int, bool, out Cell)"/> is to
        /// replace, so that it does not stand on top of the old road when it goes to another cell than the one pointed
        /// at: the road block at the pointed cell, else the one in the column that lies within a cell of the new base.
        /// A road block in the placed cell itself is replaced by the placing; null for none.
        /// </summary>
        /// <param name="pointed">The cell the cursor was at.</param>
        /// <param name="placed">The cell the block goes to.</param>
        /// <param name="shift">The shift it goes with.</param>
        public PlacedBlock RoadBlockReplaced(Cell pointed, Cell placed, int shift)
        {
            if (!RoadColumns().TryGetValue(placed.X, out List<PlacedBlock> column))
                return null;
            PlacedBlock at = BlockAt(Layer.Middle, pointed);
            if (pointed != placed && pointed.X == placed.X && at != null && column.Contains(at))
                return at;
            int newBase = StepsPerCell * placed.Y + shift;
            PlacedBlock nearest = null;
            int distance = StepsPerCell;
            foreach (PlacedBlock block in column)
            {
                if (block.Cell != placed && Math.Abs(Base(block) - newBase) < distance)
                {
                    nearest = block;
                    distance = Math.Abs(Base(block) - newBase);
                }
            }
            return nearest;
        }

        /// <summary>The level as data to save or to play, everything in the order of the file.</summary>
        public LevelData ToData()
        {
            LevelData data = new LevelData
            {
                Format = LevelData.CurrentFormat,
                Order = order,
                Debug = debug,
                Mood = mood,
                Par = par,
                Style = style
            };
            //The back layers from the nearest to the deepest, each block with its depth
            for (Layer layer = Layer.Back; layer >= Layer.Back3; layer--)
            {
                int depth = layer.BackDepth();
                data.Back.AddRange(layers[(int)layer].Select(block => Convert(block, depth)));
            }
            data.Middle.AddRange(layers[(int)Layer.Middle].Select(block => Convert(block, 1)));
            //The front layers from the nearest to the farthest, each block with its depth
            for (Layer layer = Layer.Front; layer <= Layer.Front3; layer++)
            {
                int depth = layer.Cells();
                data.Front.AddRange(layers[(int)layer].Select(block => Convert(block, depth)));
            }
            data.Objects.AddRange(objects.Select(obj => new ObjectPlacement { Type = obj.Type, Model = obj.Model, X = obj.X, Y = obj.Y }));
            data.Scenery.AddRange(scenery.Select(prop => new SceneryPlacement { Model = prop.Model, X = prop.X, Y = prop.Y, Z = prop.Z, Turn = prop.Turn }));
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
        /// finish flag). Refused, with the reason, for a block the catalog does not know, for decoration (a block
        /// without a collision shape) in the middle layer and for a turned block that cannot be turned; null when done.
        /// </summary>
        /// <param name="turned">The block is placed turned round (see <see cref="BlockType.Turns"/>).</param>
        /// <param name="style">The style of the block (a pasted one keeps its own); null for the style of the level.</param>
        public string Place(Layer layer, Cell cell, string block, int shift, bool turned = false, string style = null)
        {
            BlockType type = catalog.FindBlock(block);
            if (type == null)
                return $"Block {block} is not in the catalog.";
            if (layer == Layer.Middle && !type.Collision)
                return $"Block {block} is decoration without a collision shape; it can only be in the back or front layers.";
            if (turned && !type.Turns)
                return $"Block {block} cannot be turned.";

            List<PlacedBlock> blocks = layers[(int)layer];
            PlacedBlock existing = BlockAt(layer, cell);
            string name = "Place " + block + (turned ? " turned" : "");
            if (existing == null)
            {
                PlacedBlock placed = new PlacedBlock(cell, block, shift, false, turned, style);
                int index = blocks.Count;
                Record(name, () => blocks.Insert(index, placed), () => blocks.RemoveAt(index));
            }
            else if (existing.Name != block || existing.Shift != shift || existing.Turned != turned || existing.Style != style)
            {
                string oldName = existing.Name;
                int oldShift = existing.Shift;
                bool oldTurned = existing.Turned;
                string oldStyle = existing.Style;
                Record(name,
                    () => Set(existing, block, shift, turned, style),
                    () => Set(existing, oldName, oldShift, oldTurned, oldStyle));
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
        /// Moves blocks of a layer by whole cells as one step: every block keeps its identity, its place in the file, its
        /// shift and its finish flag. A block of the layer in a cell they move to, that is not among them, is erased first
        /// (in the same step, so that undo puts it back in its place). Nothing is recorded for no blocks and for no move;
        /// a block that is not in the layer is left out. True when a step was recorded.
        /// </summary>
        public bool MoveBlocks(Layer layer, IReadOnlyList<PlacedBlock> blocks, int dx, int dy)
        {
            if (dx == 0 && dy == 0)
                return false;
            List<PlacedBlock> moved = InLayer(layer, blocks);
            return Relocate("Move", layer, moved,
                moved.Select(block => new Cell(block.Cell.X + dx, block.Cell.Y + dy)).ToList(),
                moved.Select(block => (block.Name, block.Turned)).ToList());
        }

        /// <summary>
        /// Flips blocks of a layer left to right around the middle column of them, as one step: a block at x goes to
        /// minX + maxX - x (minX and maxX are those of the blocks), and a block that turns (<see cref="BlockType.Turns"/>)
        /// is turned round, so that a climb becomes a descent; another one gets its mirrored variant where the catalog has
        /// one (<see cref="MirrorName"/>: bl2 and bl2f), and stays as it is otherwise. Identity, place in the file, shift
        /// and finish flag are kept; a block of the layer in a cell they move to, that is not among them, is erased first.
        /// Nothing is recorded for no blocks and for blocks that stay as they are (a single block in the middle column
        /// that neither turns nor has a mirrored variant). True when a step was recorded.
        /// </summary>
        public bool MirrorBlocks(Layer layer, IReadOnlyList<PlacedBlock> blocks)
        {
            List<PlacedBlock> mirrored = InLayer(layer, blocks);
            if (mirrored.Count == 0)
                return false;
            return MirrorBlocks(layer, mirrored, mirrored.Min(block => block.Cell.X) + mirrored.Max(block => block.Cell.X));
        }

        /// <summary>
        /// <see cref="MirrorBlocks(Layer, IReadOnlyList{PlacedBlock})"/> around a given axis half way between two columns
        /// (the Select tool mirrors the blocks and the props of the scenery around one): a block at x goes to sum - x.
        /// </summary>
        /// <param name="sum">The sum of the columns of the leftmost and the rightmost cell.</param>
        public bool MirrorBlocks(Layer layer, IReadOnlyList<PlacedBlock> blocks, int sum)
        {
            List<PlacedBlock> mirrored = InLayer(layer, blocks);
            if (mirrored.Count == 0)
                return false;
            return Relocate("Mirror", layer, mirrored,
                mirrored.Select(block => new Cell(sum - block.Cell.X, block.Cell.Y)).ToList(),
                mirrored.Select(Mirrored).ToList());
        }

        /// <summary>What a block becomes in a mirror image: turned round if it turns, else its mirrored variant, if any.</summary>
        private (string Name, bool Turned) Mirrored(PlacedBlock block)
        {
            if (catalog.FindBlock(block.Name)?.Turns == true)
                return (block.Name, !block.Turned);
            return (MirrorName(catalog, block.Name), block.Turned);
        }

        /// <summary>
        /// Turns blocks of a layer round in their own cells, as one step: each block that turns (<see cref="BlockType.Turns"/>)
        /// is turned, and a turned one turns back. Identity, cell, place in the file, shift and finish flag are kept; a
        /// block that cannot turn and a block that is not in the layer are left out. Returns how many blocks were turned;
        /// nothing is recorded for none.
        /// </summary>
        public int TurnBlocks(Layer layer, IReadOnlyList<PlacedBlock> blocks)
        {
            List<PlacedBlock> turned = InLayer(layer, blocks).Where(block => catalog.FindBlock(block.Name)?.Turns == true).ToList();
            if (turned.Count == 0)
                return 0;
            Record(turned.Count == 1 ? "Turn " + turned[0].Name : "Turn",
                () => Toggle(turned),
                () => Toggle(turned));
            return turned.Count;
        }

        /// <summary>
        /// Moves blocks of a layer into another layer as one step, with props of the scenery, which go as far in depth
        /// (a cell for every layer, so that they stay with the blocks). Every block keeps its identity, its cell and its
        /// shift, and goes after the blocks of the other layer; a block of the other layer in a cell they come to is erased
        /// first (in the same step, so that undo puts it back in its place). Refused, with the reason, for decoration
        /// going into the middle layer and for the finish leaving it; null when done, also when there was nothing to move
        /// (no step is recorded then). A block that is not in the layer and a prop that is not in the level are left out.
        /// </summary>
        public string MoveToLayer(Layer from, IReadOnlyList<PlacedBlock> blocks, IReadOnlyList<PlacedScenery> props, Layer to)
        {
            if (from == to)
                return null;
            List<PlacedBlock> moved = InLayer(from, blocks);
            List<PlacedScenery> movedProps = props.Where(prop => scenery.Contains(prop)).Distinct().ToList();
            if (to == Layer.Middle)
            {
                PlacedBlock decoration = moved.FirstOrDefault(block => catalog.FindBlock(block.Name)?.Collision != true);
                if (decoration != null)
                    return $"Block {decoration.Name} at {decoration.Cell} is decoration without a collision shape; it cannot be in the middle layer.";
            }
            if (moved.Any(block => block.Finish))
                return "The finish has to stay in the middle layer.";
            if (moved.Count == 0 && movedProps.Count == 0)
                return null;

            string name = "Move to the " + to.Name() + " layer";
            List<PlacedBlock> source = layers[(int)from], target = layers[(int)to];
            HashSet<Cell> entered = new HashSet<Cell>(moved.Select(block => block.Cell));
            List<PlacedBlock> displaced = target.Where(block => entered.Contains(block.Cell)).ToList();
            //Where the blocks were in the file, from the first: put back in this order, each goes to its place again
            List<(int Index, PlacedBlock Block)> places = moved.Select(block => (source.IndexOf(block), block)).OrderBy(place => place.Item1).ToList();
            float dz = (to.Cells() - from.Cells()) * Units.GridCell;
            using (BeginCompound(name))
            {
                foreach (PlacedBlock block in displaced)
                    Erase(to, block);
                if (moved.Count > 0)
                    Record(name,
                        () =>
                        {
                            foreach (PlacedBlock block in moved)
                                source.Remove(block);
                            target.AddRange(moved);
                        },
                        () =>
                        {
                            target.RemoveRange(target.Count - moved.Count, moved.Count);
                            foreach ((int index, PlacedBlock block) in places)
                                source.Insert(index, block);
                        });
                //Two decimals, as the files keep them
                foreach (PlacedScenery prop in movedProps)
                    MoveScenery(prop, prop.X, prop.Y, MathF.Round(prop.Z + dz, 2));
            }
            return null;
        }

        /// <summary>
        /// The block a mirrored block becomes, for a block that does not turn: the name without its "f" where that is a
        /// block of the catalog (bl2f gives bl2), the name with an "f" where that is one (bl2 gives bl2f); a block without
        /// a mirrored variant keeps its name (kea1f: there is no kea1). Mirroring twice gives the block back.
        /// </summary>
        public static string MirrorName(Catalog catalog, string name)
        {
            if (name.EndsWith("f", StringComparison.Ordinal) && catalog.FindBlock(name.Substring(0, name.Length - 1)) != null)
                return name.Substring(0, name.Length - 1);
            return catalog.FindBlock(name + "f") != null ? name + "f" : name;
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

        /// <summary>
        /// Sets the style of the track (see <see cref="LevelData.Style"/>; null for the classic one); nothing is recorded
        /// when it does not change.
        /// </summary>
        public void SetStyle(string value)
        {
            string previous = style;
            if (value != previous)
                Record("Style", () => style = value, () => style = previous);
        }

        /// <summary>
        /// Sets the style of blocks of a layer as one step (null: the style of the level), of each that has a look in it
        /// (see <see cref="BlockType.HasLook"/>); returns how many could take it. Nothing is recorded when none changes.
        /// </summary>
        public int StyleBlocks(Layer layer, IReadOnlyList<PlacedBlock> blocks, string value)
        {
            List<PlacedBlock> styled = InLayer(layer, blocks).Where(block => catalog.FindBlock(block.Name)?.HasLook(value) == true).ToList();
            List<PlacedBlock> changed = styled.Where(block => block.Style != value).ToList();
            if (changed.Count > 0)
            {
                List<string> previous = changed.Select(block => block.Style).ToList();
                Record("Style " + StyleNames.Name(value).ToLowerInvariant(),
                    () => changed.ForEach(block => block.Style = value),
                    () =>
                    {
                        for (int i = 0; i < changed.Count; i++)
                            changed[i].Style = previous[i];
                    });
            }
            return styled.Count;
        }

        /// <summary>Sets the position of the level in the level select; nothing is recorded when it does not change.</summary>
        public void SetOrder(int? value)
        {
            int? previous = order;
            if (value != previous)
                Record("Order", () => order = value, () => order = previous);
        }

        /// <summary>Makes the level one for development and testing, or a level of the game; nothing is recorded when it does not change.</summary>
        public void SetDebug(bool value)
        {
            bool previous = debug;
            if (value != previous)
                Record("Debug level", () => debug = value, () => debug = previous);
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
        /// Adds a prop of the scenery after the others, its foot at a position and depth in physics units; the turn is
        /// taken in quarter turns of a whole turn (5 is 1, -1 is 3). Refused, with the reason, for a model the catalog does
        /// not have; null when done.
        /// </summary>
        public string AddScenery(string model, float x, float y, float z, int turn)
        {
            SceneryType type = catalog.FindScenery(model);
            if (type == null)
                return $"The catalog has no scenery {model}.";

            PlacedScenery added = new PlacedScenery(model, x, y, z, WholeTurn(turn));
            int index = scenery.Count;
            Record("Add " + type.Name, () => scenery.Insert(index, added), () => scenery.RemoveAt(index));
            return null;
        }

        /// <summary>Moves a prop of the scenery to a position and depth in physics units; nothing is recorded when it is there already.</summary>
        public void MoveScenery(PlacedScenery prop, float x, float y, float z)
        {
            float oldX = prop.X, oldY = prop.Y, oldZ = prop.Z;
            if (x == oldX && y == oldY && z == oldZ)
                return;
            Record("Move " + SceneryName(prop),
                () => Move(prop, x, y, z),
                () => Move(prop, oldX, oldY, oldZ));
        }

        /// <summary>
        /// Turns a prop of the scenery to a number of quarter turns, taken of a whole turn; nothing is recorded when it is
        /// turned so already.
        /// </summary>
        public void TurnScenery(PlacedScenery prop, int turn)
        {
            int oldTurn = prop.Turn, newTurn = WholeTurn(turn);
            if (newTurn == oldTurn)
                return;
            Record("Turn " + SceneryName(prop), () => prop.Turn = newTurn, () => prop.Turn = oldTurn);
        }

        /// <summary>Removes a prop of the scenery; undo puts it back in its place.</summary>
        public void RemoveScenery(PlacedScenery prop)
        {
            int index = scenery.IndexOf(prop);
            if (index < 0)
                return;
            Record("Remove " + SceneryName(prop), () => scenery.RemoveAt(index), () => scenery.Insert(index, prop));
        }

        /// <summary>
        /// Flips props of the scenery left to right as one step, around the middle of two columns of cells, the way
        /// <see cref="MirrorBlocks(Layer, IReadOnlyList{PlacedBlock}, int)"/> flips the blocks with them: a prop at x goes to
        /// sum × a cell - x, and its turn t becomes 2 - t, so that a prop that looks the same from the front and from
        /// behind (all of them) stands as its mirror image would. Nothing is recorded for no props, and for props that
        /// stay as they are; a prop that is not in the level is left out. True when a step was recorded.
        /// </summary>
        /// <param name="sum">The sum of the columns of the leftmost and the rightmost cell: the axis is half way between them.</param>
        public bool MirrorScenery(IReadOnlyList<PlacedScenery> props, int sum)
        {
            List<PlacedScenery> mirrored = props.Where(prop => scenery.Contains(prop)).Distinct().ToList();
            List<(float X, int Turn)> before = mirrored.Select(prop => (prop.X, prop.Turn)).ToList();
            //Two decimals, as the files keep them (a float does not hold the difference exactly)
            List<(float X, int Turn)> after = mirrored.Select(prop => (MathF.Round(sum * Units.GridCell - prop.X, 2), WholeTurn(2 - prop.Turn))).ToList();
            if (before.SequenceEqual(after))
                return false;
            Record("Mirror", () => Mirrored(mirrored, after), () => Mirrored(mirrored, before));
            return true;
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
            if (style != null && !catalog.IsStyle(style))
                problems.Add(new LevelProblem($"The level has the unknown style \"{style}\"."));
            FindBlockProblems(Layer.Middle, problems);
            foreach (Layer layer in OuterLayers)
                FindBlockProblems(layer, problems);
            foreach (Layer layer in OuterLayers)
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
            foreach (PlacedScenery prop in scenery)
            {
                if (catalog.FindScenery(prop.Model) == null)
                    problems.Add(new LevelProblem($"Unknown scenery \"{prop.Model}\" at {Position(prop)}.", prop.X, prop.Y));
                if (prop.Turn < 0 || prop.Turn > 3)
                    problems.Add(new LevelProblem($"Scenery \"{prop.Model}\" at {Position(prop)} has turn {prop.Turn}; a turn is 0 to 3 quarter turns.", prop.X, prop.Y));
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
            //Last: a gap may be left on purpose, so it must not hide the problems that stop the level from being played
            foreach (RoadGap gap in RoadGaps())
            {
                problems.Add(new LevelProblem(
                    $"The road has a gap between {gap.A.Cell} and {gap.B.Cell}: the surfaces differ by {gap.Steps} step{(gap.Steps == 1 ? "" : "s")}.",
                    Layer.Middle, gap.A.Cell) { IsRoadGap = true });
            }
            return problems;
        }

        /// <summary>
        /// The blocks of a layer the catalog does not know, decoration in the middle layer and turned blocks that cannot
        /// turn, as <see cref="LevelData.Validate"/> words them.
        /// </summary>
        private void FindBlockProblems(Layer layer, List<LevelProblem> problems)
        {
            string layerName = layer.Name();
            foreach (PlacedBlock block in layers[(int)layer])
            {
                BlockType type = catalog.FindBlock(block.Name);
                if (type == null)
                    problems.Add(new LevelProblem($"Unknown block \"{block.Name}\" at {block.Cell} in the {layerName} layer.", layer, block.Cell));
                else if (layer == Layer.Middle && !type.Collision)
                    problems.Add(new LevelProblem($"Block \"{block.Name}\" at {block.Cell} has no collision shape, so it cannot be in the {layerName} layer.", layer, block.Cell));
                if (type != null && block.Turned && !type.Turns)
                    problems.Add(new LevelProblem($"Block \"{block.Name}\" at {block.Cell} in the {layerName} layer is turned, but it cannot be turned.", layer, block.Cell));
                if (block.Style != null && !catalog.IsStyleOrTransition(block.Style))
                    problems.Add(new LevelProblem($"Block \"{block.Name}\" at {block.Cell} in the {layerName} layer has the unknown style \"{block.Style}\".", layer, block.Cell));
                else if (type != null && block.Style != null && !type.HasLook(block.Style))
                    problems.Add(new LevelProblem($"Block \"{block.Name}\" at {block.Cell} in the {layerName} layer has the style \"{block.Style}\", but no look in it.", layer, block.Cell));
            }
        }

        /// <summary>The profile of a block of the catalog as placed, [left, right] (see <see cref="BlockType.PlacedProfile"/>); null for a block without one.</summary>
        private int[] ProfileOf(string block, bool turned)
        {
            return catalog.FindBlock(block)?.PlacedProfile(turned);
        }

        /// <summary>
        /// The road blocks of the middle layer by their column, in the order of the file; of two in one cell (a road
        /// block under another) only the one placed last.
        /// </summary>
        private Dictionary<int, List<PlacedBlock>> RoadColumns()
        {
            Dictionary<Cell, PlacedBlock> last = new Dictionary<Cell, PlacedBlock>();
            foreach (PlacedBlock block in layers[(int)Layer.Middle])
                if (ProfileOf(block.Name, false) != null)
                    last[block.Cell] = block;
            Dictionary<int, List<PlacedBlock>> columns = new Dictionary<int, List<PlacedBlock>>();
            foreach (PlacedBlock block in layers[(int)Layer.Middle])
            {
                if (!last.TryGetValue(block.Cell, out PlacedBlock winner) || winner != block)
                    continue;
                if (!columns.TryGetValue(block.Cell.X, out List<PlacedBlock> column))
                    columns[block.Cell.X] = column = new List<PlacedBlock>();
                column.Add(block);
            }
            return columns;
        }

        /// <summary>The block of a column whose left (or right) edge is the nearest in height to a height; the first of those equally near.</summary>
        private PlacedBlock Nearest(List<PlacedBlock> column, bool leftEdge, int height)
        {
            PlacedBlock nearest = null;
            int distance = int.MaxValue;
            foreach (PlacedBlock block in column)
            {
                int edge = leftEdge ? SurfaceLeft(block).Value : SurfaceRight(block).Value;
                if (Math.Abs(edge - height) < distance)
                {
                    nearest = block;
                    distance = Math.Abs(edge - height);
                }
            }
            return nearest;
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

        /// <summary>The position and the depth of a prop as the messages write them, with a decimal point whatever the culture.</summary>
        private static string Position(PlacedScenery prop)
        {
            return string.Format(CultureInfo.InvariantCulture, "[{0}, {1}, {2}]", prop.X, prop.Y, prop.Z);
        }

        /// <summary>The name of a prop in the catalog (its model, for one the catalog does not have), for the names of the steps.</summary>
        private string SceneryName(PlacedScenery prop) => catalog.FindScenery(prop.Model)?.Name ?? prop.Model;

        /// <summary>A number of quarter turns as 0 to 3: what is left of the whole turns.</summary>
        public static int WholeTurn(int turn) => (turn % 4 + 4) % 4;

        /// <summary>The blocks that are in the layer, once each, in the given order.</summary>
        private List<PlacedBlock> InLayer(Layer layer, IReadOnlyList<PlacedBlock> blocks)
        {
            HashSet<PlacedBlock> present = new HashSet<PlacedBlock>(layers[(int)layer]);
            HashSet<PlacedBlock> seen = new HashSet<PlacedBlock>();
            List<PlacedBlock> result = new List<PlacedBlock>();
            foreach (PlacedBlock block in blocks)
                if (present.Contains(block) && seen.Add(block))
                    result.Add(block);
            return result;
        }

        /// <summary>
        /// Puts blocks into new cells, under new names and turns, as one step. The blocks of the layer that are in the
        /// cells the blocks leave for, and not among them, are erased first; a block that stays in its cell displaces
        /// nobody (the other block of a cell with two stays with it). True when a step was recorded: none is when nothing
        /// would change.
        /// </summary>
        private bool Relocate(string name, Layer layer, List<PlacedBlock> blocks, List<Cell> cells, List<(string Name, bool Turned)> looks)
        {
            HashSet<Cell> entered = new HashSet<Cell>();
            bool changes = false;
            for (int i = 0; i < blocks.Count; i++)
            {
                if (cells[i] != blocks[i].Cell)
                    entered.Add(cells[i]);
                changes |= cells[i] != blocks[i].Cell || looks[i] != (blocks[i].Name, blocks[i].Turned);
            }
            if (!changes)
                return false;

            HashSet<PlacedBlock> own = new HashSet<PlacedBlock>(blocks);
            List<PlacedBlock> displaced = layers[(int)layer].Where(block => entered.Contains(block.Cell) && !own.Contains(block)).ToList();
            List<Cell> oldCells = blocks.Select(block => block.Cell).ToList();
            List<(string Name, bool Turned)> oldLooks = blocks.Select(block => (block.Name, block.Turned)).ToList();
            using (BeginCompound(name))
            {
                foreach (PlacedBlock block in displaced)
                    Erase(layer, block);
                Record(name, () => Relocated(blocks, cells, looks), () => Relocated(blocks, oldCells, oldLooks));
            }
            return true;
        }

        private static void Relocated(List<PlacedBlock> blocks, List<Cell> cells, List<(string Name, bool Turned)> looks)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                blocks[i].Cell = cells[i];
                blocks[i].Name = looks[i].Name;
                blocks[i].Turned = looks[i].Turned;
            }
        }

        private static void Toggle(List<PlacedBlock> blocks)
        {
            foreach (PlacedBlock block in blocks)
                block.Turned = !block.Turned;
        }

        private int IndexAt(Layer layer, Cell cell)
        {
            return layers[(int)layer].FindLastIndex(block => block.Cell == cell);
        }

        private static void Set(PlacedBlock block, string name, int shift, bool turned, string style)
        {
            block.Name = name;
            block.Shift = shift;
            block.Turned = turned;
            block.Style = style;
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

        private static void Move(PlacedScenery prop, float x, float y, float z)
        {
            prop.X = x;
            prop.Y = y;
            prop.Z = z;
        }

        private static void Mirrored(List<PlacedScenery> props, List<(float X, int Turn)> places)
        {
            for (int i = 0; i < props.Count; i++)
            {
                props[i].X = places[i].X;
                props[i].Turn = places[i].Turn;
            }
        }

        private static PlacedBlock Convert(BlockPlacement placement)
        {
            return new PlacedBlock(new Cell(placement.X, placement.Y), placement.Block, placement.Shift, placement.Finish, placement.Turned, placement.Style);
        }

        /// <param name="depth">How many cells behind the road the block stands (1 for the blocks that are not in a back layer).</param>
        private static BlockPlacement Convert(PlacedBlock block, int depth)
        {
            return new BlockPlacement
            {
                Block = block.Name, X = block.Cell.X, Y = block.Cell.Y, Shift = block.Shift, Turned = block.Turned, Finish = block.Finish,
                Style = block.Style, Depth = depth
            };
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
