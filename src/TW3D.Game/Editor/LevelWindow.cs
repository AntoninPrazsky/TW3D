using System;
using System.Collections.Generic;
using ImGuiNET;
using TW3D.Shared;
using Vector2 = System.Numerics.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace TW3D.Editor
{
    /// <summary>
    /// The Level window of the editor: everything about the level that is not a block. Its name (the file; the full path
    /// as a tooltip), the mood (a combo, applied at once), the position in the level select and the par time (each a
    /// checkbox that gives the level the property or takes it away, and a field applied as one step of undo when it is
    /// finished, to the level it was begun on; the par shows the estimate of the game while it has none), the counts of
    /// the blocks, the objects and the props of the scenery and the cells they span, then the problems of the level and the gaps of the road,
    /// each a line that a click points the camera at. What it shows comes from the <see cref="LevelReport"/>, which is
    /// brought up to date only when the level changes.
    /// </summary>
    sealed class LevelWindow
    {
        //The window first appears under the Inspector, as wide, at least this high and no higher than this (pixels on a 1920 × 1080 window)
        private const float MinHeight = 200f, MaxHeight = 420f;
        //The step of the field of the par time, and with Ctrl, in seconds; the least a par time can be
        private const float ParStep = 0.1f, ParFastStep = 1f;
        private const float MinPar = 0.1f;
        //The position a level gets when it is put into the level select
        private const int FirstOrder = 1;
        //Going to a problem keeps the distance of the camera, but not a greater one than this, where a block is a readable size
        private const float MaxGoToDistance = 30f;
        private static readonly LevelMood[] Moods = Enum.GetValues<LevelMood>();

        /// <summary>A field being edited: it shows the level until it is edited, then keeps the edit and the level it is for.</summary>
        private sealed class FieldEdit<T>
        {
            public T Value;
            public bool Active;
            public EditedLevel Target;
        }

        private readonly FieldEdit<int> order = new FieldEdit<int>();
        private readonly FieldEdit<float> par = new FieldEdit<float>();
        //The level the folding of the list of gaps was last set for: a level opened gets the default, the choice of the user stays
        private EditedLevel gapsFoldedFor;

        /// <summary>Lays the window out; called every frame while it is shown.</summary>
        /// <param name="open">False after the window was closed by its button.</param>
        /// <param name="report">What the level looks like; brought up to date by the caller.</param>
        /// <param name="camera">The camera, pointed at a problem that is clicked.</param>
        /// <param name="scale">The scale of the interface (1 on a 1920 × 1080 window).</param>
        /// <param name="paletteWidth">The width the palette first appears with: this window appears under the Inspector as wide.</param>
        /// <param name="statusHeight">The height of the status bar, which the window first appears above.</param>
        public void Draw(ref bool open, ImGuiViewportPtr viewport, LevelReport report, EditorCamera camera, float scale, float paletteWidth,
            float statusHeight)
        {
            InterfaceLayout.PlaceBelowInspector(viewport, scale, paletteWidth, statusHeight, MinHeight, MaxHeight);
            if (ImGui.Begin("Level", ref open))
            {
                EditedLevel level = report.Level;
                DrawProperties(level, report);
                ImGui.Separator();
                ImGui.Text(report.BlocksText);
                ImGui.Text(report.ObjectsText);
                ImGui.Text(report.SceneryText);
                ImGui.Text(report.CellsText);
                ImGui.Spacing();
                DrawProblems(report, camera);
                DrawGaps(level, report, camera);
            }
            ImGui.End();
        }

        private void DrawProperties(EditedLevel level, LevelReport report)
        {
            ImGui.Text("Name: " + report.Name);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(level.File ?? "Not saved yet");

            //The mood is applied at once; M cycles it too
            if (ImGui.BeginCombo("Mood", level.Mood.ToString()))
            {
                foreach (LevelMood mood in Moods)
                {
                    if (ImGui.Selectable(mood.ToString(), mood == level.Mood))
                        level.SetMood(mood);
                }
                ImGui.EndCombo();
            }

            //The style of the track: every block with a look in it is drawn so, unless it has a style of its own
            if (ImGui.BeginCombo("Style", StyleNames.Name(level.Style ?? Catalog.ClassicStyle)))
            {
                foreach (string style in StyleNames.LevelStyles(level.Catalog))
                {
                    if (ImGui.Selectable(StyleNames.Name(style ?? Catalog.ClassicStyle), style == level.Style))
                        level.SetStyle(style);
                }
                ImGui.EndCombo();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The style of the track: the blocks that have a look in it are drawn so,\nthe others classic. A block can have a style of its own (Edit > Style).");

            bool ordered = level.Order.HasValue;
            if (ImGui.Checkbox("In the level select", ref ordered))
                level.SetOrder(ordered ? FirstOrder : null);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The position of the level in the level select.\nLevels without one come after the others, sorted by name.");
            if (ordered)
            {
                ImGui.Indent();
                if (!order.Active)
                {
                    order.Value = level.Order.Value;
                    order.Target = level;
                }
                ImGui.InputInt("Position", ref order.Value);
                order.Active = ImGui.IsItemActive();
                if (ImGui.IsItemDeactivatedAfterEdit())
                    order.Target.SetOrder(order.Value);
                ImGui.Unindent();
            }

            bool hasPar = level.Par.HasValue;
            //A level given a par time starts from what the game would take
            if (ImGui.Checkbox("Par time", ref hasPar))
                level.SetPar(hasPar ? Math.Max(MinPar, MathF.Round(report.Estimate * 10f) / 10f) : null);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The time a good run takes: finishing within it earns three stars.\nWithout one the game estimates it from the distance to the finish.");
            if (hasPar)
            {
                ImGui.Indent();
                if (!par.Active)
                {
                    par.Value = level.Par.Value;
                    par.Target = level;
                }
                ImGui.InputFloat("seconds", ref par.Value, ParStep, ParFastStep, "%.1f");
                par.Active = ImGui.IsItemActive();
                if (ImGui.IsItemDeactivatedAfterEdit())
                    par.Target.SetPar(Math.Max(MinPar, par.Value));
                ImGui.Unindent();
            }
            else
                ImGui.TextDisabled(report.EstimateText);
        }

        private static void DrawProblems(LevelReport report, EditorCamera camera)
        {
            if (!ImGui.CollapsingHeader("Problems", ImGuiTreeNodeFlags.DefaultOpen))
                return;
            if (report.Problems.Count == 0)
                ImGui.TextDisabled("No problems");
            else
                DrawList("problems", report.Problems, camera);
        }

        private void DrawGaps(EditedLevel level, LevelReport report, EditorCamera camera)
        {
            //Many gaps are folded away at first; the label changes with their number, its id after the ### does not
            if (gapsFoldedFor != level)
            {
                gapsFoldedFor = level;
                ImGui.SetNextItemOpen(report.Gaps.Count <= LevelReport.FoldedGaps);
            }
            if (!ImGui.CollapsingHeader(report.GapsHeader))
                return;
            if (report.Gaps.Count == 0)
                ImGui.TextDisabled("No gaps");
            else
                DrawList("gaps", report.Gaps, camera);
        }

        /// <summary>
        /// The problems as lines of wrapped text: those with a place can be clicked, and the camera goes to it; those of the
        /// whole level are plain text, as a click could do nothing.
        /// </summary>
        /// <param name="id">Names the list, as the lines of two lists would have the same ids by their numbers and one click could end the other's.</param>
        private static void DrawList(string id, List<LevelProblem> problems, EditorCamera camera)
        {
            float wrap = ImGui.GetContentRegionAvail().X;
            ImGui.PushID(id);
            for (int i = 0; i < problems.Count; i++)
            {
                LevelProblem problem = problems[i];
                if (!HasPlace(problem))
                {
                    ImGui.TextWrapped(problem.Message);
                    continue;
                }
                ImGui.PushID(i);
                //A selectable as high as the wrapped text, which is drawn over it
                Vector2 start = ImGui.GetCursorScreenPos();
                float height = ImGui.CalcTextSize(problem.Message, false, wrap).Y;
                bool clicked = ImGui.Selectable("##problem", false, ImGuiSelectableFlags.None, new Vector2(0f, height));
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Click to go there");
                ImGui.SetCursorScreenPos(start);
                ImGui.TextWrapped(problem.Message);
                ImGui.PopID();
                if (clicked)
                    GoTo(camera, problem);
            }
            ImGui.PopID();
        }

        private static bool HasPlace(LevelProblem problem)
        {
            return problem.Layer.HasValue && problem.Cell.HasValue || problem.X.HasValue && problem.Y.HasValue;
        }

        /// <summary>Points the camera at the place of a problem (a problem of the whole level has none and leaves it alone).</summary>
        private static void GoTo(EditorCamera camera, LevelProblem problem)
        {
            Vector3 position;
            if (problem.Layer.HasValue && problem.Cell.HasValue)
                position = LevelView.ScenePosition(problem.Layer.Value, problem.Cell.Value);
            else if (problem.X.HasValue && problem.Y.HasValue)
                position = LevelView.ScenePosition(Layer.Middle, new Microsoft.Xna.Framework.Vector2(problem.X.Value, problem.Y.Value));
            else
                return;
            camera.Target = position;
            camera.Distance = Math.Min(camera.Distance, MaxGoToDistance);
        }
    }
}
