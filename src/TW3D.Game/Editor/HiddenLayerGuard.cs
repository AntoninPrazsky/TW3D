using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TW3D.Input;

namespace TW3D.Editor
{
    /// <summary>
    /// Stands between the screen and the active tool while the layer the tool works on (or the objects, for the Object
    /// tool, or the scenery, for the Scenery tool) is hidden by the View menu: a hidden layer cannot be edited, as what is placed or selected there could not be
    /// seen. The cursor still moves and the block is still chosen, so that the user can go on looking around; a key or a
    /// click that would edit says why it does not instead.
    /// </summary>
    sealed class HiddenLayerGuard
    {
        private readonly BlockTool placeTool;
        private readonly Action<string> report;

        /// <param name="placeTool">The Place tool, whose cursor the other tools use too.</param>
        /// <param name="report">Gets what is said to the user, for the status bar.</param>
        public HiddenLayerGuard(BlockTool placeTool, Action<string> report)
        {
            this.placeTool = placeTool;
            this.report = report;
        }

        /// <summary>The keys of the scene for a hidden layer.</summary>
        /// <param name="hidden">Why the tool cannot edit (see <see cref="LevelView.HiddenNote"/>); null when it can.</param>
        /// <returns>The keys were handled here, the tool is not to get them.</returns>
        public bool HandleKeys(InputState input, EditorTool tool, string hidden)
        {
            if (hidden == null)
                return false;
            bool control = input.IsKeyDown(Keys.LeftControl) || input.IsKeyDown(Keys.RightControl);
            if (!control)
            {
                Point step = BlockTool.ArrowStep(input);
                if (step != Point.Zero)
                    placeTool.MoveCursor(step);
                if (input.KeyPressed(Keys.OemComma))
                    placeTool.ChooseBlock(-1);
                if (input.KeyPressed(Keys.OemPeriod))
                    placeTool.ChooseBlock(1);
            }
            if (Edits(input, tool, control))
                report(hidden);
            return true;
        }

        /// <summary>
        /// The mouse for a hidden layer: the cursor follows it, a press of a button says why nothing is done. The tool
        /// still gets the mouse as if it were off the scene, which ends a drag it had and clears what it hovers.
        /// </summary>
        /// <param name="free">The cursor is over the scene, not over the user interface.</param>
        /// <param name="cursorDepth">The depth of the plane the cursor cell is on (physics units), as the tool has it.</param>
        /// <returns>The mouse was handled here, the tool is not to get it.</returns>
        public bool HandleMouse(InputState input, IEditorTool activeTool, EditorTool tool, Viewport viewport, Layer layer, bool free, string hidden,
            float cursorDepth)
        {
            if (hidden == null)
                return false;
            activeTool.HandleMouse(input, viewport, layer, false);
            bool overScene = free && viewport.Bounds.Contains(input.Mouse.Position);
            bool pressed = input.Mouse.LeftButton == ButtonState.Pressed && input.PreviousMouse.LeftButton == ButtonState.Released
                || input.Mouse.RightButton == ButtonState.Pressed && input.PreviousMouse.RightButton == ButtonState.Released;
            placeTool.FollowMouse(input, viewport, LevelView.SceneDepth(cursorDepth), overScene, pressed);
            //Ctrl + the wheel shifts the block under the cursor, which the Place tool would do
            bool shifting = tool == EditorTool.Place && input.WheelDelta != 0
                && (input.IsKeyDown(Keys.LeftControl) || input.IsKeyDown(Keys.RightControl));
            if (overScene && (pressed || shifting))
                report(hidden);
            return true;
        }

        /// <summary>A key that would change the level, or the selection, with this tool.</summary>
        private static bool Edits(InputState input, EditorTool tool, bool control)
        {
            if (control)
                return tool == EditorTool.Select && (input.KeyPressed(Keys.A) || input.KeyPressed(Keys.C) || input.KeyPressed(Keys.X) || input.KeyPressed(Keys.V));
            if (input.KeyPressed(Keys.Space) || input.KeyPressed(Keys.Delete))
                return true;
            if (tool == EditorTool.Object)
                return false;
            if (tool == EditorTool.Scenery)
                return input.KeyPressed(Keys.T) || input.KeyPressed(Keys.PageUp) || input.KeyPressed(Keys.PageDown);
            return input.KeyPressed(Keys.PageUp) || input.KeyPressed(Keys.PageDown)
                || tool == EditorTool.Place && input.KeyPressed(Keys.F) || tool == EditorTool.Select && input.KeyPressed(Keys.H);
        }
    }
}
