using System.Numerics;
using ImGuiNET;

namespace TW3D.Editor
{
    /// <summary>The Controls window of the editor (F1): what the mouse and the keys do, in tables by the tool and for all of them.</summary>
    static class HelpWindow
    {
        private static readonly (string Keys, string Action)[] PlaceRows =
        {
            ("Left button", "Place the chosen block in the cell under the cursor; dragging paints"),
            ("Left Shift + left button", "Erase the block under the cursor; dragging erases"),
            ("Right button", "Pick: the block under the cursor becomes the chosen block, with its shift"),
            ("Ctrl + wheel", "Shift the block under the cursor up or down (the chosen shift follows)"),
            ("Arrows", "Move the cursor cell (the camera follows)"),
            ("Space / Delete", "Place the chosen block / erase the block at the cursor cell"),
            ("PageUp / PageDown", "Shift the block at the cursor cell up / down (the chosen shift follows)"),
            ("F", "The block at the cursor cell is the finish of the level, or stops being it"),
            ("R", "Road mode on / off: road blocks join the road next to them; red bars mark gaps"),
            (", / .", "The previous / next block of the catalog becomes the chosen block (and Place the active tool)")
        };

        private static readonly (string Keys, string Action)[] SelectRows =
        {
            ("Left button", "Select the block under the cursor alone; Ctrl adds it to the selection or takes it out"),
            ("Left drag", "From an empty cell: a rectangle selects the blocks of the active layer in it (Ctrl adds them). From a selected block: moves the selection by whole cells, as one step"),
            ("Ctrl+A / Esc", "Select the whole active layer / clear the selection (a second Esc leaves the editor)"),
            ("Space", "Add the block at the cursor cell to the selection, or take it out"),
            ("Arrows / Ctrl + arrows", "Move the selection (and the cursor) by a cell; without a selection, or with Ctrl, only the cursor cell"),
            ("PageUp / PageDown", "Shift the selected blocks up / down"),
            ("H", "Mirror the selection left to right, swapping the blocks for their f variants (b2 and b2f)"),
            ("Delete", "Erase the selected blocks"),
            ("Ctrl+C / Ctrl+X / Ctrl+V", "Copy / cut the selection; paste it with its leftmost (lowest) block at the cursor cell (the pasted blocks are selected; decorations are skipped in the middle layer)")
        };

        private static readonly (string Keys, string Action)[] ObjectRows =
        {
            ("Left button", "Add the chosen object at the point under the cursor, or select the object there; dragging moves it"),
            ("Left Shift + left button", "Remove the object under the cursor"),
            ("Right button", "Pick: the object under the cursor becomes the chosen object"),
            ("Space", "Add the chosen object at the top of the cursor cell (it follows the mouse): it drops onto the block there"),
            ("Delete", "Remove the selected object"),
            ("Arrows / Ctrl + arrows", "Move the selected object by 1 / 0.1 physics unit; without a selection the cursor cell"),
            ("Esc", "Clear the selection (a second Esc leaves the editor)")
        };

        private static readonly (string Keys, string Action)[] WindowRows =
        {
            ("Palette", "Click a block to choose it (Place), an object of the Objects tab to choose it (Object)"),
            ("Inspector", "The block at the cursor cell, the selected blocks, or the selected object with its position to type"),
            ("Level", "The mood, the position in the level select and the par time to edit, the counts, the problems of the level and the gaps of the road: click one to go there"),
            ("Controls", "This window (F1)"),
            ("View menu", "Shows or hides each window, the back and front layers (which cannot be edited while hidden), the objects, the grid and the gap markers")
        };

        private static readonly (string Keys, string Action)[] CommonRows =
        {
            ("B / V / O", "The Place tool (blocks) / the Select tool (many blocks) / the Object tool (dynamic objects); View > Tool lists them"),
            ("Middle button drag", "Pan along the track"),
            ("Wheel", "Zoom towards the cursor"),
            ("Alt + left drag", "Orbit around the point under the cursor"),
            ("Ctrl+Z / Ctrl+Y", "Undo / redo"),
            ("Ctrl+N", "A new level (asks to save unsaved changes)"),
            ("Ctrl+O", "Open a level of the levels folder or any level file"),
            ("Ctrl+S", "Save the level (a new level asks for a name)"),
            ("Ctrl+Shift+S", "Save the level under a new name in the levels folder"),
            ("F5", "Test drive: the level in the game from the start, with the car of the garage; Esc comes back"),
            ("Shift+F5", "Test drive from the cursor cell"),
            ("Tab", "The next layer; Right Shift / Right Ctrl (pressed alone) toggle the back / front layer"),
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
            ImGui.SetNextWindowSize(new Vector2(560f, 620f) * scale, ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Controls", ref open))
            {
                ImGui.SeparatorText("Place tool (B): blocks");
                Rows(PlaceRows);
                ImGui.SeparatorText("Select tool (V): many blocks of the layer");
                Rows(SelectRows);
                ImGui.SeparatorText("Object tool (O): dynamic objects");
                Rows(ObjectRows);
                ImGui.SeparatorText("Windows");
                Rows(WindowRows);
                ImGui.SeparatorText("All tools");
                Rows(CommonRows);
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
