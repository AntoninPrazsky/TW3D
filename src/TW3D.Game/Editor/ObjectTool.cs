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
    /// The Object tool of the editor: the dynamic objects (boxes, bars, ramps...), which are always on the plane of the
    /// middle layer. The object whose bounding sphere the ray from the mouse hits first is the hovered one. The left
    /// button adds the chosen object at the point under the mouse (rounded to a tenth of a physics unit) and selects it,
    /// or selects the object under the mouse and drags it (the whole drag one step of undo); Left Shift + the left button
    /// removes the object under the mouse, the right button picks its type and model as the chosen object. Space adds the
    /// chosen object at the top of the cursor cell of the Place tool (it drops onto the block there), Delete removes the
    /// selected object, the arrows move it by a physics unit (Ctrl + the arrows by a tenth), or move the cursor cell
    /// while nothing is selected; Esc drops the selection. What is done and what the document refuses is reported to the
    /// screen for its status bar.
    /// </summary>
    sealed class ObjectTool : IEditorTool
    {
        //How far the arrows move the selected object (physics units), and with Ctrl
        private const float Step = 1f;
        private const float FineStep = 0.1f;
        //The mouse places and drags objects in tenths of a physics unit; the files keep two decimals, which the keys keep
        private const float MouseSteps = 10f;
        private const int Decimals = 2;
        //Two objects closer than this (the files keep two decimals) are in one place
        private const float SamePlace = 0.005f;

        private readonly EditorCamera camera;
        private readonly BlockTool placeTool;
        private readonly Action<string> report;
        //The bounding spheres of the models of the dynamic objects, in the units of the models
        private readonly Dictionary<string, BoundingSphere> modelBounds = new Dictionary<string, BoundingSphere>();
        private EditedLevel level;
        private PlacedObject selected, hovered;
        //The version of the document the selected and the hovered object were last checked against
        private int checkedVersion;
        //The object being dragged, the step of undo of the drag and the offset of the grab (physics units)
        private PlacedObject dragged;
        private IDisposable dragStep;
        private Vector2 grabOffset;

        /// <param name="camera">The camera, for the ray from the mouse.</param>
        /// <param name="placeTool">The Place tool: Space adds at its cursor cell, the arrows move that while nothing is selected.</param>
        /// <param name="report">Gets a note of what was done, or the reason of a refusal, for the status bar.</param>
        public ObjectTool(EditorCamera camera, BlockTool placeTool, LevelAssets assets, Action<string> report)
        {
            this.camera = camera;
            this.placeTool = placeTool;
            this.report = report;
            foreach (string model in assets.Catalog.ObjectModels)
                modelBounds[model] = ModelBounds.Of(assets.ObjectModel(model));
            //Until one is chosen, the first model of the first object type of the catalog is the chosen object
            foreach (ObjectType type in assets.Catalog.Objects)
            {
                if (type.Models.Count > 0)
                {
                    Chosen = new ChosenObject(type.Type, type.Models[0]);
                    break;
                }
            }
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

        /// <summary>The object chosen in the palette or picked by the right button, the one the tool adds; the first of the catalog at the start, null when the catalog has none.</summary>
        public ChosenObject Chosen { get; private set; }

        /// <summary>The selected object: the one added, clicked or dragged last; null for none, or when it is no longer in the level.</summary>
        public PlacedObject Selected
        {
            get
            {
                Forget();
                return selected;
            }
        }

        /// <summary>The object under the mouse; null for none.</summary>
        public PlacedObject Hovered
        {
            get
            {
                Forget();
                return hovered;
            }
        }

        /// <summary>An object of the catalog, by its type and model, becomes the chosen one (the palette chooses so); one the catalog does not know is refused.</summary>
        public void Choose(string type, string model)
        {
            ObjectType objectType = level.Catalog.FindObject(type);
            if (objectType == null || !objectType.Models.Contains(model))
            {
                report($"The catalog has no object {type} {model}.");
                return;
            }
            Chosen = new ChosenObject(type, model);
            report($"Chosen object: {type} {model}.");
        }

        /// <summary>
        /// The keys: Space adds, Delete removes the selected object, the arrows move it (by a tenth with Ctrl) or, while
        /// nothing is selected, the cursor cell. The other shortcuts with Ctrl are the screen's.
        /// </summary>
        public void HandleKeys(InputState input, Layer layer)
        {
            bool control = input.IsKeyDown(Keys.LeftControl) || input.IsKeyDown(Keys.RightControl);
            //While the object is dragged, a step or an object added would be recorded in the step of the drag
            bool dragging = dragged != null;
            Point step = BlockTool.ArrowStep(input);
            if (step != Point.Zero && !dragging)
            {
                if (Selected != null)
                    Move(selected, step.ToVector2() * (control ? FineStep : Step));
                else if (!control)
                    placeTool.MoveCursor(step);
            }
            if (control)
                return;

            if (input.KeyPressed(Keys.Space) && !dragging)
            {
                //The top of the cursor cell: the object drops onto the block there
                Cell cell = placeTool.Cursor;
                Add(new Vector2(cell.X * Units.GridCell, cell.Y * Units.GridCell - Units.GridCell / 2f));
            }
            if (input.KeyPressed(Keys.Delete))
            {
                if (Selected != null)
                    Remove(selected);
                else
                    report("No object is selected.");
            }
        }

        /// <summary>
        /// The mouse: the hovered object follows it over the scene, and so does the cursor cell of the Place tool (which
        /// Space adds at), on the middle layer; the left button adds, selects and drags, Left Shift + the left button
        /// removes, the right button picks. Alt + the left button is the camera's.
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
                //Only a move of the mouse moves the object: a click that selects it leaves its position as it was, also
                //one with two decimals, which the rounding to a tenth would change
                Vector2? point = PointUnder(ray);
                if (input.MouseMoved && point.HasValue)
                {
                    Vector2 to = Round(point.Value + grabOffset);
                    level.MoveObject(dragged, to.X, to.Y);
                }
                hovered = dragged;
                return;
            }

            bool overScene = free && viewport.Bounds.Contains(mouse.Position) && !camera.IsDragging;
            hovered = overScene ? ObjectUnder(ray) : null;
            bool leftPressed = mouse.LeftButton == ButtonState.Pressed && previous.LeftButton == ButtonState.Released;
            bool rightPressed = mouse.RightButton == ButtonState.Pressed && previous.RightButton == ButtonState.Released;
            //The objects are on the middle plane, so is the cell Space adds above
            placeTool.FollowMouse(input, viewport, Layer.Middle, overScene, leftPressed || rightPressed);
            if (!overScene)
                return;

            bool alt = input.IsKeyDown(Keys.LeftAlt) || input.IsKeyDown(Keys.RightAlt);
            //Right Shift toggles the back layer the moment it is pressed, so only Left Shift makes a click remove
            bool remove = input.IsKeyDown(Keys.LeftShift);
            if (leftPressed && !alt)
            {
                Vector2? point = PointUnder(ray);
                if (hovered == null)
                {
                    if (point.HasValue && !remove)
                        Add(Round(point.Value));
                }
                else if (remove)
                    Remove(hovered);
                else
                    BeginDrag(hovered, point);
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

        /// <summary>The bounding sphere of an object in the scene; null for a model the catalog does not have (it is not drawn).</summary>
        public BoundingSphere? Bounds(PlacedObject obj)
        {
            if (obj == null || !modelBounds.TryGetValue(obj.Model, out BoundingSphere bounds))
                return null;
            Vector3 position = LevelView.ScenePosition(Layer.Middle, new Vector2(obj.X, obj.Y));
            return new BoundingSphere(position + bounds.Center * Units.ModelToScene, bounds.Radius * Units.ModelToScene);
        }

        /// <summary>The object is in the level (not removed, not undone away).</summary>
        public bool Contains(PlacedObject obj)
        {
            IReadOnlyList<PlacedObject> objects = level.Objects;
            for (int i = 0; i < objects.Count; i++)
                if (objects[i] == obj)
                    return true;
            return false;
        }

        /// <summary>Moves an object to a position (physics units) as one step of undo; the Inspector moves so.</summary>
        public void MoveTo(PlacedObject obj, float x, float y)
        {
            level.MoveObject(obj, MathF.Round(x, Decimals), MathF.Round(y, Decimals));
            report($"Moved {obj.Type} to {Position(obj.X, obj.Y)}.");
        }

        /// <summary>Removes an object (Delete, Left Shift + the left button, the Inspector); it is no longer selected.</summary>
        public void Remove(PlacedObject obj)
        {
            EndStroke();
            level.RemoveObject(obj);
            if (obj == selected)
                selected = null;
            if (obj == hovered)
                hovered = null;
            report($"Removed {obj.Type} {obj.Model} at {Position(obj.X, obj.Y)}.");
        }

        /// <summary>Adds the chosen object at a position where no object is yet; the added object is selected.</summary>
        private void Add(Vector2 position)
        {
            ChosenObject chosen = Chosen;
            if (chosen == null)
            {
                report("The catalog has no dynamic objects.");
                return;
            }
            //Two objects in one place look as one and fly apart in the physics, as the old editor knew
            foreach (PlacedObject other in level.Objects)
            {
                if (MathF.Abs(other.X - position.X) < SamePlace && MathF.Abs(other.Y - position.Y) < SamePlace)
                {
                    report($"Another object is already at {Position(position.X, position.Y)}.");
                    return;
                }
            }
            string reason = level.AddObject(chosen.Type, chosen.Model, position.X, position.Y);
            if (reason != null)
            {
                report(reason);
                return;
            }
            IReadOnlyList<PlacedObject> objects = level.Objects;
            selected = objects[objects.Count - 1];
            report($"Added {chosen.Type} {chosen.Model} at {Position(position.X, position.Y)}.");
        }

        /// <summary>The arrows move the selected object by a step, each press one step of undo.</summary>
        private void Move(PlacedObject obj, Vector2 step)
        {
            MoveTo(obj, obj.X + step.X, obj.Y + step.Y);
        }

        /// <summary>Selects an object and drags it from now on, keeping where it was grabbed; the moves of the drag are one step of undo.</summary>
        /// <param name="point">The point under the mouse on the middle plane; null when the ray misses the plane (selected only then).</param>
        private void BeginDrag(PlacedObject obj, Vector2? point)
        {
            selected = obj;
            report($"Selected {obj.Type} {obj.Model} at {Position(obj.X, obj.Y)}.");
            if (!point.HasValue)
                return;
            dragged = obj;
            grabOffset = new Vector2(obj.X, obj.Y) - point.Value;
            dragStep = level.BeginCompound("Move " + obj.Type);
        }

        /// <summary>The type and the model of an object become the chosen object.</summary>
        private void Pick(PlacedObject obj)
        {
            Chosen = new ChosenObject(obj.Type, obj.Model);
            report($"Picked {obj.Type} {obj.Model}.");
        }

        /// <summary>The object whose bounding sphere the ray hits nearest to the camera; null for none.</summary>
        private PlacedObject ObjectUnder(Ray ray)
        {
            PlacedObject nearest = null;
            float nearestDistance = float.MaxValue;
            IReadOnlyList<PlacedObject> objects = level.Objects;
            for (int i = 0; i < objects.Count; i++)
            {
                BoundingSphere? bounds = Bounds(objects[i]);
                if (!bounds.HasValue)
                    continue;
                float? distance = ray.Intersects(bounds.Value);
                if (distance.HasValue && distance.Value < nearestDistance)
                {
                    nearest = objects[i];
                    nearestDistance = distance.Value;
                }
            }
            return nearest;
        }

        /// <summary>The point of the middle plane under the mouse in physics units; null when the ray runs away from the plane.</summary>
        private static Vector2? PointUnder(Ray ray)
        {
            Vector3? hit = EditorCamera.HitPlane(ray, LevelView.SceneDepth(Layer.Middle));
            //A physics point (x, y) is drawn at (-x, -y) scaled, see Units
            return hit.HasValue ? new Vector2(-hit.Value.X, -hit.Value.Y) / Units.PhysicsToScene : null;
        }

        /// <summary>The selected and the hovered object are dropped once they are no longer in the level (undone, removed).</summary>
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

        /// <summary>A point rounded to a tenth of a physics unit, as the mouse places and drags.</summary>
        private static Vector2 Round(Vector2 point)
        {
            //Divided, not multiplied by a tenth, which a float does not hold exactly
            return new Vector2(MathF.Round(point.X * MouseSteps) / MouseSteps, MathF.Round(point.Y * MouseSteps) / MouseSteps);
        }

        /// <summary>A position as the messages write it, with a decimal point whatever the culture.</summary>
        private static string Position(float x, float y)
        {
            return string.Format(CultureInfo.InvariantCulture, "[{0:0.##}, {1:0.##}]", x, y);
        }
    }
}
