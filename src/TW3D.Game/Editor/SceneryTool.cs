using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TW3D.GameObjects;
using TW3D.Input;
using TW3D.Shared;

namespace TW3D.Editor
{
    /// <summary>
    /// The Scenery tool of the editor: the props beside the track (traffic cones, street lamps...), which have no
    /// physics. A new prop goes to the chosen depth with the chosen turn; the cursor cell of the Place tool and its grid
    /// are on the plane of that depth. The prop whose bounding sphere the ray from the mouse hits first is the hovered
    /// one. The left button adds the chosen prop at the point of the plane under the mouse, or selects the prop under the
    /// mouse and drags it on the plane of its own depth (the whole drag one step of undo); Left Shift + the left button
    /// removes the prop under the mouse, the right button picks its model, turn and depth as the chosen ones. The mouse
    /// places and drags in steps of <see cref="Step"/>, on which the surfaces of the flat road blocks lie, so that a prop
    /// stands on the road and the pieces of a fence join. Space adds the chosen prop in the middle of the cursor cell, on
    /// the road there; Delete removes the selected prop, the arrows move it by a step (Ctrl + the arrows by a tenth) or
    /// move the cursor cell while nothing is selected; T turns the selected prop (else the chosen turn) by a quarter;
    /// PageUp and PageDown move it (else the chosen depth) away from the camera and towards it. Esc drops the selection.
    /// What is done and what the document refuses is reported to the screen for its status bar.
    /// </summary>
    sealed class SceneryTool : IEditorTool
    {
        /// <summary>
        /// The step the mouse and the arrows place and move props by, and PageUp and PageDown change the depth by
        /// (physics units): half a shift step. The surfaces of the flat road blocks lie on it (1.25 above the height of
        /// their profile), and a cell is 18 of it: two pieces of the fence, half a cell long, fill a cell.
        /// </summary>
        public const float Step = Units.ShiftStep / 2f;
        //Ctrl + the arrows move by a tenth; the files keep two decimals, which the Inspector keeps
        private const float FineStep = 0.1f;
        private const int Decimals = 2;
        //The depth new props are placed at until another one is chosen: on the road, behind where the car drives
        private const float DefaultDepth = -10f;
        //Two props closer than this (the files keep two decimals) are in one place
        private const float SamePlace = 0.005f;

        private readonly EditorCamera camera;
        private readonly BlockTool placeTool;
        private readonly Action<string> report;
        //The bounding spheres of the models of the scenery, in the units of the models
        private readonly Dictionary<string, BoundingSphere> modelBounds = new Dictionary<string, BoundingSphere>();
        private EditedLevel level;
        private PlacedScenery selected, hovered;
        //The version of the document the selected and the hovered prop were last checked against
        private int checkedVersion;
        //The prop being dragged, the step of undo of the drag and the offset of the grab (physics units)
        private PlacedScenery dragged;
        private IDisposable dragStep;
        private Vector2 grabOffset;

        /// <param name="camera">The camera, for the ray from the mouse.</param>
        /// <param name="placeTool">The Place tool: Space adds at its cursor cell, the arrows move that while nothing is selected.</param>
        /// <param name="report">Gets a note of what was done, or the reason of a refusal, for the status bar.</param>
        public SceneryTool(EditorCamera camera, BlockTool placeTool, LevelAssets assets, Action<string> report)
        {
            this.camera = camera;
            this.placeTool = placeTool;
            this.report = report;
            foreach (SceneryType prop in assets.Catalog.Scenery)
                modelBounds[prop.Model] = ModelBounds.Of(assets.SceneryModel(prop.Model));
            //Until one is chosen, the first prop of the catalog is the chosen one
            if (assets.Catalog.Scenery.Count > 0)
                Chosen = assets.Catalog.Scenery[0];
        }

        /// <summary>The document edited; a drag in the previous one ends and the selection is dropped when it changes.</summary>
        public EditedLevel Level
        {
            get => level;
            set
            {
                EndStroke();
                level = value;
                selected = null;
                hovered = null;
                checkedVersion = value.Version;
            }
        }

        /// <summary>The prop chosen in the palette or picked by the right button, the one the tool adds; null when the catalog has none.</summary>
        public SceneryType Chosen { get; private set; }

        /// <summary>The depth new props are placed at (physics units: negative behind the road, positive in front of it).</summary>
        public float Depth { get; private set; } = DefaultDepth;

        /// <summary>The quarter turns new props are placed with.</summary>
        public int Turn { get; private set; }

        /// <summary>The selected prop: the one added, clicked or dragged last; null for none, or when it is no longer in the level.</summary>
        public PlacedScenery Selected
        {
            get
            {
                Forget();
                return selected;
            }
        }

        /// <summary>The prop under the mouse; null for none.</summary>
        public PlacedScenery Hovered
        {
            get
            {
                Forget();
                return hovered;
            }
        }

        /// <summary>A prop of the catalog becomes the chosen one (the palette chooses so); one the catalog does not have is refused.</summary>
        public void Choose(string model)
        {
            SceneryType prop = level.Catalog.FindScenery(model);
            if (prop == null)
            {
                report($"The catalog has no scenery {model}.");
                return;
            }
            Chosen = prop;
            report($"Chosen scenery: {prop.Name}.");
        }

        /// <summary>The depth new props are placed at (the Inspector sets it), rounded to two decimals as the files keep it.</summary>
        public void SetDepth(float depth)
        {
            Depth = MathF.Round(depth, Decimals);
            report($"New props go to the depth {Number(Depth)}.");
        }

        /// <summary>The quarter turns new props are placed with (the Inspector sets them), of a whole turn.</summary>
        public void SetTurn(int turn)
        {
            Turn = EditedLevel.WholeTurn(turn);
            report($"New props are turned by {Turns(Turn)}.");
        }

        /// <summary>
        /// The keys: Space adds, Delete removes the selected prop, the arrows move it (by a tenth with Ctrl) or, while
        /// nothing is selected, the cursor cell; T turns, PageUp and PageDown change the depth of the selected prop, or
        /// the chosen ones. The other shortcuts with Ctrl are the screen's.
        /// </summary>
        public void HandleKeys(InputState input, Layer layer)
        {
            bool control = input.IsKeyDown(Keys.LeftControl) || input.IsKeyDown(Keys.RightControl);
            //While a prop is dragged, a step or a prop added would be recorded in the step of the drag
            if (dragged != null)
                return;
            Point step = BlockTool.ArrowStep(input);
            if (step != Point.Zero)
            {
                if (Selected != null)
                    Move(selected, step.ToVector2() * (control ? FineStep : Step));
                else if (!control)
                    placeTool.MoveCursor(step);
            }
            if (control)
                return;

            if (input.KeyPressed(Keys.Space))
                Add(OnTheRoad(placeTool.Cursor), Depth);
            if (input.KeyPressed(Keys.Delete))
            {
                if (Selected != null)
                    Remove(selected);
                else
                    report("No prop is selected.");
            }
            if (input.KeyPressed(Keys.T))
            {
                if (Selected != null)
                    TurnTo(selected, selected.Turn + 1);
                else
                    SetTurn(Turn + 1);
            }
            //PageUp goes away from the camera, into the scene: to the back
            int deeper = (input.KeyPressed(Keys.PageDown) ? 1 : 0) - (input.KeyPressed(Keys.PageUp) ? 1 : 0);
            if (deeper != 0)
            {
                if (Selected != null)
                    MoveTo(selected, selected.X, selected.Y, selected.Z + deeper * Step);
                else
                    SetDepth(Depth + deeper * Step);
            }
        }

        /// <summary>
        /// The mouse: the hovered prop follows it over the scene, and so does the cursor cell of the Place tool (which
        /// Space adds at), on the plane of the chosen depth; the left button adds, selects and drags, Left Shift + the
        /// left button removes, the right button picks. Alt + the left button is the camera's.
        /// </summary>
        public void HandleMouse(InputState input, Viewport viewport, Layer layer, bool free)
        {
            MouseState mouse = input.Mouse, previous = input.PreviousMouse;
            camera.Update(viewport);
            Ray ray = camera.PickRay(new Vector2(mouse.X, mouse.Y), viewport);

            if (dragged != null)
            {
                //The button may have gone up while the window had no focus (the screen gets no input then): the drag
                //ends when the button was up at any time since the last call, and the press of now begins nothing
                if (mouse.LeftButton == ButtonState.Released || previous.LeftButton == ButtonState.Released || !Contains(dragged))
                {
                    EndStroke();
                    return;
                }
                //Only a move of the mouse moves the prop: a click that selects it leaves it where it was, also off the steps
                Vector2? point = PointUnder(ray, dragged.Z);
                if (input.MouseMoved && point.HasValue)
                {
                    Vector2 to = Round(point.Value + grabOffset);
                    level.MoveScenery(dragged, to.X, to.Y, dragged.Z);
                }
                hovered = dragged;
                return;
            }

            bool overScene = free && viewport.Bounds.Contains(mouse.Position) && !camera.IsDragging;
            hovered = overScene ? PropUnder(ray) : null;
            bool leftPressed = mouse.LeftButton == ButtonState.Pressed && previous.LeftButton == ButtonState.Released;
            bool rightPressed = mouse.RightButton == ButtonState.Pressed && previous.RightButton == ButtonState.Released;
            placeTool.FollowMouse(input, viewport, LevelView.SceneDepth(Depth), overScene, leftPressed || rightPressed);
            if (!overScene)
                return;

            bool alt = input.IsKeyDown(Keys.LeftAlt) || input.IsKeyDown(Keys.RightAlt);
            //Right Shift toggles the back layer the moment it is pressed, so only Left Shift makes a click remove
            bool remove = input.IsKeyDown(Keys.LeftShift);
            if (leftPressed && !alt)
            {
                if (hovered == null)
                {
                    Vector2? point = PointUnder(ray, Depth);
                    if (point.HasValue && !remove)
                        Add(Round(point.Value), Depth);
                }
                else if (remove)
                    Remove(hovered);
                else
                    BeginDrag(hovered, PointUnder(ray, hovered.Z));
            }
            else if (rightPressed && hovered != null)
                Pick(hovered);
        }

        /// <summary>Esc drops the selection; false when nothing was selected.</summary>
        public bool Cancel()
        {
            if (Selected == null)
                return false;
            EndStroke();
            selected = null;
            report("Selection cleared.");
            return true;
        }

        /// <summary>Ends the drag of the mouse, closing its step of undo; nothing when there is none.</summary>
        public void EndStroke()
        {
            if (dragged == null)
                return;
            dragged = null;
            dragStep.Dispose();
            dragStep = null;
        }

        /// <summary>The bounding sphere of a prop in the scene; null for a model the catalog does not have (it is not drawn).</summary>
        public BoundingSphere? Bounds(PlacedScenery prop)
        {
            if (prop == null || !modelBounds.TryGetValue(prop.Model, out BoundingSphere bounds))
                return null;
            return bounds.Transform(SceneryProp.World(prop.X, prop.Y, prop.Z, prop.Turn));
        }

        /// <summary>The prop is in the level (not removed, not undone away).</summary>
        public bool Contains(PlacedScenery prop)
        {
            IReadOnlyList<PlacedScenery> props = level.Scenery;
            for (int i = 0; i < props.Count; i++)
                if (props[i] == prop)
                    return true;
            return false;
        }

        /// <summary>Moves a prop to a position and depth (physics units) as one step of undo; the Inspector moves so.</summary>
        public void MoveTo(PlacedScenery prop, float x, float y, float z)
        {
            level.MoveScenery(prop, MathF.Round(x, Decimals), MathF.Round(y, Decimals), MathF.Round(z, Decimals));
            report($"Moved {Name(prop)} to {Position(prop.X, prop.Y)}, depth {Number(prop.Z)}.");
        }

        /// <summary>Turns a prop to a number of quarter turns as one step of undo (T, the Inspector).</summary>
        public void TurnTo(PlacedScenery prop, int turn)
        {
            level.TurnScenery(prop, turn);
            report($"Turned {Name(prop)} by {Turns(prop.Turn)}.");
        }

        /// <summary>Removes a prop (Delete, Left Shift + the left button, the Inspector); it is no longer selected.</summary>
        public void Remove(PlacedScenery prop)
        {
            EndStroke();
            level.RemoveScenery(prop);
            if (prop == selected)
                selected = null;
            if (prop == hovered)
                hovered = null;
            report($"Removed {Name(prop)} at {Position(prop.X, prop.Y)}.");
        }

        /// <summary>The name of a prop in the catalog, or its model for one the catalog does not have.</summary>
        public string Name(PlacedScenery prop) => level.Catalog.FindScenery(prop.Model)?.Name ?? prop.Model;

        /// <summary>A number of quarter turns as a phrase: "no turn", "1 quarter", "2 quarters".</summary>
        public static string Turns(int turn) => turn == 0 ? "no turn" : turn == 1 ? "1 quarter" : turn + " quarters";

        /// <summary>
        /// Where Space adds a prop at a cell: in its middle, on the road block of the middle layer there (in the middle of
        /// its surface), else where a flat block standing in the cell would have its surface.
        /// </summary>
        private Vector2 OnTheRoad(Cell cell)
        {
            float x = cell.X * Units.GridCell;
            PlacedBlock block = level.BlockAt(Layer.Middle, cell);
            int? left = block != null ? level.SurfaceLeft(block) : null;
            //The top of a road block is a half step above the height of its profile (heights grow downward)
            float height = left.HasValue ? (left.Value + level.SurfaceRight(block).Value) / 2f * Units.ShiftStep : cell.Y * Units.GridCell;
            return new Vector2(x, height - Step);
        }

        /// <summary>Adds the chosen prop at a position and depth where no prop is yet; the added prop is selected.</summary>
        private void Add(Vector2 position, float depth)
        {
            SceneryType chosen = Chosen;
            if (chosen == null)
            {
                report("The catalog has no scenery.");
                return;
            }
            foreach (PlacedScenery other in level.Scenery)
            {
                if (MathF.Abs(other.X - position.X) < SamePlace && MathF.Abs(other.Y - position.Y) < SamePlace && MathF.Abs(other.Z - depth) < SamePlace)
                {
                    report($"Another prop is already at {Position(position.X, position.Y)}, depth {Number(depth)}.");
                    return;
                }
            }
            string reason = level.AddScenery(chosen.Model, position.X, position.Y, depth, Turn);
            if (reason != null)
            {
                report(reason);
                return;
            }
            IReadOnlyList<PlacedScenery> props = level.Scenery;
            selected = props[props.Count - 1];
            report($"Added {chosen.Name} at {Position(position.X, position.Y)}, depth {Number(depth)}.");
        }

        /// <summary>The arrows move the selected prop by a step, each press one step of undo.</summary>
        private void Move(PlacedScenery prop, Vector2 step)
        {
            MoveTo(prop, prop.X + step.X, prop.Y + step.Y, prop.Z);
        }

        /// <summary>Selects a prop and drags it from now on, keeping where it was grabbed; the moves of the drag are one step of undo.</summary>
        /// <param name="point">The point under the mouse on the plane of the prop; null when the ray misses the plane (selected only then).</param>
        private void BeginDrag(PlacedScenery prop, Vector2? point)
        {
            selected = prop;
            report($"Selected {Name(prop)} at {Position(prop.X, prop.Y)}, depth {Number(prop.Z)}.");
            if (!point.HasValue)
                return;
            dragged = prop;
            grabOffset = new Vector2(prop.X, prop.Y) - point.Value;
            dragStep = level.BeginCompound("Move " + Name(prop));
        }

        /// <summary>The model, the turn and the depth of a prop become the chosen ones.</summary>
        private void Pick(PlacedScenery prop)
        {
            SceneryType type = level.Catalog.FindScenery(prop.Model);
            if (type == null)
            {
                report($"The catalog has no scenery {prop.Model}.");
                return;
            }
            Chosen = type;
            Turn = EditedLevel.WholeTurn(prop.Turn);
            Depth = prop.Z;
            report($"Picked {type.Name}, {Turns(Turn)}, depth {Number(Depth)}.");
        }

        /// <summary>The prop whose bounding sphere the ray hits nearest to the camera; null for none.</summary>
        private PlacedScenery PropUnder(Ray ray)
        {
            PlacedScenery nearest = null;
            float nearestDistance = float.MaxValue;
            IReadOnlyList<PlacedScenery> props = level.Scenery;
            for (int i = 0; i < props.Count; i++)
            {
                BoundingSphere? bounds = Bounds(props[i]);
                if (!bounds.HasValue)
                    continue;
                float? distance = ray.Intersects(bounds.Value);
                if (distance.HasValue && distance.Value < nearestDistance)
                {
                    nearest = props[i];
                    nearestDistance = distance.Value;
                }
            }
            return nearest;
        }

        /// <summary>The point under the mouse on the plane of a depth, in physics units; null when the ray runs away from the plane.</summary>
        private static Vector2? PointUnder(Ray ray, float depth)
        {
            Vector3? hit = EditorCamera.HitPlane(ray, LevelView.SceneDepth(depth));
            //A physics point (x, y) is drawn at (-x, -y) scaled, see Units
            return hit.HasValue ? new Vector2(-hit.Value.X, -hit.Value.Y) / Units.PhysicsToScene : null;
        }

        /// <summary>The selected and the hovered prop are dropped once they are no longer in the level (undone, removed).</summary>
        private void Forget()
        {
            if (level == null || level.Version == checkedVersion)
                return;
            checkedVersion = level.Version;
            if (selected != null && !Contains(selected))
                selected = null;
            if (hovered != null && !Contains(hovered))
                hovered = null;
        }

        /// <summary>A point rounded to the step the mouse places and drags by.</summary>
        private static Vector2 Round(Vector2 point)
        {
            return new Vector2(MathF.Round(point.X / Step) * Step, MathF.Round(point.Y / Step) * Step);
        }

        /// <summary>A position as the messages write it, with a decimal point whatever the culture.</summary>
        private static string Position(float x, float y)
        {
            return string.Format(CultureInfo.InvariantCulture, "[{0:0.##}, {1:0.##}]", x, y);
        }

        private static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
