using System;
using System.Globalization;
using System.Numerics;
using ImGuiNET;

namespace TW3D.Editor
{
    /// <summary>
    /// The Inspector window of the editor: what the active tool works on. With the Place tool the block in the cursor
    /// cell of the active layer (its name, cell, shift and whether it is the finish); with the Object tool the selected
    /// object, whose type and model it shows, its position as two fields (an edit is applied as one step of undo when it
    /// is finished, to the object it was begun on) and a button that removes it; while none is selected, the hovered one
    /// only to read; with the Select tool how many blocks are selected and the cells they span, with buttons that mirror
    /// and delete them.
    /// </summary>
    sealed class InspectorWindow
    {
        //The window first appears under the palette, at least this wide, and this high (pixels on a 1920 × 1080 window)
        private const float MinWidth = 300f;
        private const float Height = 210f;
        private const float Top = PaletteWindow.DefaultMargin * 2f + PaletteWindow.DefaultHeight;
        //The step of the buttons of the position fields, and with Ctrl, in physics units
        private const float PositionStep = 0.1f;
        private const float PositionFastStep = 1f;

        /// <summary>A position field: it shows the object until it is edited, then keeps the edit and the object it is for.</summary>
        private sealed class PositionEdit
        {
            public float Value;
            public bool Active;
            public PlacedObject Target;
        }

        private readonly PositionEdit editX = new PositionEdit(), editY = new PositionEdit();

        /// <summary>Lays the window out; called every frame while it is shown.</summary>
        /// <param name="open">False after the window was closed by its button.</param>
        /// <param name="tool">The active tool.</param>
        /// <param name="placeTool">The Place tool, whose cursor cell the Inspector shows the block of.</param>
        /// <param name="objectTool">The Object tool, whose selected (else hovered) object the Inspector shows.</param>
        /// <param name="selectTool">The Select tool, whose selected blocks the Inspector counts.</param>
        /// <param name="layer">The active layer, whose block the Place tool shows.</param>
        /// <param name="scale">The scale of the interface (1 on a 1920 × 1080 window).</param>
        /// <param name="paletteWidth">The width the palette first appears with: the Inspector appears under it as wide.</param>
        public void Draw(ref bool open, ImGuiViewportPtr viewport, float scale, float paletteWidth, EditorTool tool, BlockTool placeTool,
            ObjectTool objectTool, SelectTool selectTool, Layer layer)
        {
            float width = Math.Max(paletteWidth, MinWidth * scale);
            ImGui.SetNextWindowSize(new Vector2(width, Height * scale), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(viewport.WorkPos.X + viewport.WorkSize.X - width - PaletteWindow.DefaultMargin * scale, viewport.WorkPos.Y + Top * scale),
                ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Inspector", ref open))
            {
                if (tool == EditorTool.Object)
                    DrawObject(objectTool);
                else if (tool == EditorTool.Select)
                    DrawSelection(selectTool, layer);
                else
                    DrawBlock(placeTool, layer);
            }
            ImGui.End();
        }

        private static void DrawBlock(BlockTool placeTool, Layer layer)
        {
            Cell cell = placeTool.Cursor;
            PlacedBlock block = placeTool.Level.BlockAt(layer, cell);
            if (block == null)
            {
                ImGui.TextDisabled("Empty cell");
                ImGui.Text($"Cell: {cell.X}, {cell.Y} ({layer} layer)");
                ImGui.Text("Road gaps: " + placeTool.Level.RoadGaps().Count);
                return;
            }
            ImGui.Text("Block: " + block.Name);
            ImGui.Text($"Cell: {cell.X}, {cell.Y} ({layer} layer)");
            ImGui.Text($"Shift: {block.Shift}");
            ImGui.Text(block.Finish ? "Finish: yes" : "Finish: no");
            //The surface of a road block of the middle layer, in shift steps, and the gaps of the road of the whole level
            int? left = layer == Layer.Middle ? placeTool.Level.SurfaceLeft(block) : null;
            if (left.HasValue)
                ImGui.Text($"Surface: left {left.Value}, right {placeTool.Level.SurfaceRight(block).Value} (steps)");
            ImGui.Text("Road gaps: " + placeTool.Level.RoadGaps().Count);
        }

        private static void DrawSelection(SelectTool selectTool, Layer layer)
        {
            int count = selectTool.Selection.Count;
            if (!selectTool.Bounds(out Cell min, out Cell max))
                ImGui.TextDisabled("No blocks selected");
            else
            {
                ImGui.Text(SelectTool.Blocks(count) + " selected");
                ImGui.Text($"Cells: {min} to {max} ({layer} layer)");
            }
            ImGui.Text(selectTool.ClipboardCount == 0 ? "Clipboard: empty" : "Clipboard: " + SelectTool.Blocks(selectTool.ClipboardCount));
            ImGui.BeginDisabled(count == 0);
            if (ImGui.Button("Mirror"))
                selectTool.Mirror();
            ImGui.SameLine();
            if (ImGui.Button("Delete"))
                selectTool.Delete();
            ImGui.EndDisabled();
        }

        private void DrawObject(ObjectTool objectTool)
        {
            PlacedObject selected = objectTool.Selected;
            if (selected == null)
            {
                editX.Active = editY.Active = false;
                PlacedObject hovered = objectTool.Hovered;
                if (hovered == null)
                {
                    ImGui.TextDisabled("No object selected");
                    return;
                }
                //The mouse leaves the scene for the fields, so the hovered object is only shown: a click selects it
                ImGui.TextDisabled("Object under the mouse");
                ImGui.Separator();
                ImGui.Text("Type: " + hovered.Type);
                ImGui.Text("Model: " + hovered.Model);
                ImGui.Text("X: " + Number(hovered.X));
                ImGui.Text("Y: " + Number(hovered.Y));
                ImGui.TextDisabled("Click it to select it.");
                return;
            }
            ImGui.Text("Selected object");
            ImGui.Separator();
            ImGui.Text("Type: " + selected.Type);
            ImGui.Text("Model: " + selected.Model);
            //A click in the scene may finish an edit and select another object in the same frame: the edit is for the
            //object it was begun on
            if (PositionField("X", selected, selected.X, editX))
                MoveEdited(objectTool, editX.Target, editX.Value, null);
            if (PositionField("Y", selected, selected.Y, editY))
                MoveEdited(objectTool, editY.Target, null, editY.Value);
            if (ImGui.Button("Remove"))
                objectTool.Remove(selected);
        }

        /// <summary>A field of the position: it shows the value until it is edited, and keeps the edit until it is finished.</summary>
        /// <returns>The edit was finished (Enter, a click elsewhere, a step button released): the value is to be applied.</returns>
        private static bool PositionField(string label, PlacedObject obj, float value, PositionEdit edit)
        {
            if (!edit.Active)
            {
                edit.Value = value;
                edit.Target = obj;
            }
            ImGui.InputFloat(label, ref edit.Value, PositionStep, PositionFastStep, "%.2f");
            edit.Active = ImGui.IsItemActive();
            return ImGui.IsItemDeactivatedAfterEdit();
        }

        /// <summary>Moves the object an edit was begun on, if it is still in the level; the other coordinate stays as it is.</summary>
        private static void MoveEdited(ObjectTool objectTool, PlacedObject target, float? x, float? y)
        {
            if (target != null && objectTool.Contains(target))
                objectTool.MoveTo(target, x ?? target.X, y ?? target.Y);
        }

        private static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
