using System.Numerics;
using ImGuiNET;

namespace TW3D.Editor
{
    /// <summary>The Controls window of the editor (F1): what the mouse and the keys do, in two tables.</summary>
    static class HelpWindow
    {
        private static readonly (string Keys, string Action)[] MouseRows =
        {
            ("Left button", "Place the chosen block in the cell under the cursor; dragging paints"),
            ("Left Shift + left button", "Erase the block under the cursor; dragging erases"),
            ("Right button", "Pick: the block under the cursor becomes the chosen block, with its shift"),
            ("Ctrl + wheel", "Shift the block under the cursor up or down (the chosen shift follows)"),
            ("Middle button drag", "Pan along the track"),
            ("Wheel", "Zoom towards the cursor"),
            ("Alt + left drag", "Orbit around the point under the cursor"),
            ("Palette", "Click a preview to choose the block; the Objects tab chooses the object to place (coming)")
        };

        private static readonly (string Keys, string Action)[] KeyRows =
        {
            ("Arrows", "Move the cursor cell (the camera follows)"),
            ("Space / Delete", "Place the chosen block / erase the block at the cursor cell"),
            ("PageUp / PageDown", "Shift the block at the cursor cell up / down (the chosen shift follows)"),
            ("F", "The block at the cursor cell is the finish of the level, or stops being it"),
            (", / .", "The previous / next block of the catalog becomes the chosen block"),
            ("Ctrl+Z / Ctrl+Y", "Undo / redo"),
            ("Ctrl+N", "A new level (asks to save unsaved changes)"),
            ("Ctrl+O", "Open a level of the levels folder or any level file"),
            ("Ctrl+S", "Save the level (a new level asks for a name)"),
            ("Ctrl+Shift+S", "Save the level under a new name in the levels folder"),
            ("Tab", "The next layer; Right Shift / Right Ctrl toggle the back / front layer"),
            ("M", "The mood of the level: day, sunset, night"),
            ("W / S / A / D, Q / E", "Fly the camera along the track, up and down"),
            ("Numeric keypad 4 / 6, 8 / 2", "Turn the camera"),
            ("Home", "Frame the whole level"),
            ("F9 / F10 / F11", "Lighting of 2016 / shadows / ambient occlusion"),
            ("F1", "This window"),
            ("Esc", "Leave the editor (asks to save unsaved changes)")
        };

        /// <param name="open">The window is shown; its close button clears it.</param>
        /// <param name="scale">The scale of the interface (the font size against the size it was designed for).</param>
        public static void Draw(ref bool open, float scale)
        {
            ImGui.SetNextWindowSize(new Vector2(540f, 560f) * scale, ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Controls", ref open))
            {
                ImGui.SeparatorText("Mouse");
                Rows(MouseRows);
                ImGui.SeparatorText("Keys");
                Rows(KeyRows);
                ImGui.Spacing();
                ImGui.TextWrapped("Selection, dynamic objects and the test drive come with the next issues of the editor (GitHub issue #20).");
            }
            ImGui.End();
        }

        private static void Rows((string Keys, string Action)[] rows)
        {
            if (!ImGui.BeginTable("help", 2, ImGuiTableFlags.SizingStretchProp))
                return;
            foreach ((string keys, string action) in rows)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(keys);
                ImGui.TableNextColumn();
                ImGui.TextWrapped(action);
            }
            ImGui.EndTable();
        }
    }
}
