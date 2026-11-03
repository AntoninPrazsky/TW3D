using System;
using ImGuiNET;
using TW3D.Graphics;

namespace TW3D.Editor
{
    /// <summary>
    /// The Edit and View menus of the editor's menu bar, which the screen only has to place: undo and redo, the
    /// clipboard and the selection commands of the <see cref="SelectTool"/> (enabled while that tool is active and the
    /// selection or the clipboard allows), the lighting, the tool, the layer and the windows.
    /// </summary>
    static class EditorMenus
    {
        /// <summary>The Edit menu: undo and redo with the name of the step, and the commands of the Select tool.</summary>
        public static void DrawEdit(EditedLevel level, BlockTool placeTool, SelectTool selectTool, EditorTool activeTool)
        {
            if (!ImGui.BeginMenu("Edit"))
                return;
            if (ImGui.MenuItem(level.CanUndo ? "Undo " + level.UndoName : "Undo", "Ctrl+Z", false, level.CanUndo))
                placeTool.Undo();
            if (ImGui.MenuItem(level.CanRedo ? "Redo " + level.RedoName : "Redo", "Ctrl+Y", false, level.CanRedo))
                placeTool.Redo();
            ImGui.Separator();
            bool selecting = activeTool == EditorTool.Select;
            bool selected = selecting && selectTool.Selection.Count > 0;
            if (ImGui.MenuItem("Cut", "Ctrl+X", false, selected))
                selectTool.Cut();
            if (ImGui.MenuItem("Copy", "Ctrl+C", false, selected))
                selectTool.Copy();
            if (ImGui.MenuItem("Paste", "Ctrl+V", false, selecting && selectTool.ClipboardCount > 0))
                selectTool.Paste();
            if (ImGui.MenuItem("Select all", "Ctrl+A", false, selecting))
                selectTool.SelectAll();
            if (ImGui.MenuItem("Mirror", "H", false, selected))
                selectTool.Mirror();
            ImGui.EndMenu();
        }

        /// <summary>The View menu: the lighting, the tool and the layer, framing the level, the windows.</summary>
        /// <param name="setTool">Makes a tool the active one.</param>
        /// <param name="frameLevel">Points the camera at the whole level.</param>
        public static void DrawView(SceneRenderer renderer, EditorTool activeTool, Action<EditorTool> setTool, ref Layer layer, Action frameLevel,
            ref bool showPalette, ref bool showInspector, ref bool showDemo)
        {
            if (!ImGui.BeginMenu("View"))
                return;
            if (ImGui.MenuItem("Lighting of 2016", "F9", renderer.Classic))
                renderer.Classic = !renderer.Classic;
            if (ImGui.MenuItem("Shadows", "F10", renderer.Shadows))
                renderer.Shadows = !renderer.Shadows;
            if (ImGui.MenuItem("Ambient occlusion", "F11", renderer.AmbientOcclusion))
                renderer.AmbientOcclusion = !renderer.AmbientOcclusion;
            ImGui.Separator();
            if (ImGui.BeginMenu("Tool"))
            {
                for (int i = 0; i < EditorToolKeys.ByTool.Length; i++)
                    if (ImGui.MenuItem(((EditorTool)i).ToString(), EditorToolKeys.ByTool[i].ToString(), activeTool == (EditorTool)i))
                        setTool((EditorTool)i);
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Layer"))
            {
                foreach (Layer candidate in Enum.GetValues<Layer>())
                    if (ImGui.MenuItem(candidate.ToString(), null, layer == candidate))
                        layer = candidate;
                ImGui.EndMenu();
            }
            if (ImGui.MenuItem("Frame the level", "Home"))
                frameLevel();
            ImGui.Separator();
            ImGui.MenuItem("Palette", null, ref showPalette);
            ImGui.MenuItem("Inspector", null, ref showInspector);
            ImGui.MenuItem("ImGui demo window", null, ref showDemo);
            ImGui.EndMenu();
        }
    }
}
