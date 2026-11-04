using System;
using ImGuiNET;
using TW3D.Graphics;

namespace TW3D.Editor
{
    /// <summary>
    /// The Edit and View menus of the editor's menu bar, which the screen only has to place: undo and redo, the
    /// clipboard and the selection commands of the <see cref="SelectTool"/> (enabled while that tool is active and the
    /// selection or the clipboard allows), the lighting, what is drawn (the layers, the objects, the scenery, the grid,
    /// the gap markers), the tool, the layer and the windows.
    /// </summary>
    static class EditorMenus
    {
        /// <summary>The Edit menu: undo and redo with the name of the step, and the commands of the Select tool.</summary>
        /// <param name="hidden">Why the active tool cannot edit (see <see cref="LevelView.HiddenNote"/>); null when it can.</param>
        /// <param name="report">Gets what is said to the user, for the status bar.</param>
        public static void DrawEdit(EditedLevel level, BlockTool placeTool, SelectTool selectTool, EditorTool activeTool, string hidden, Action<string> report)
        {
            if (!ImGui.BeginMenu("Edit"))
                return;
            if (ImGui.MenuItem(level.CanUndo ? "Undo " + level.UndoName : "Undo", "Ctrl+Z", false, level.CanUndo))
                placeTool.Undo();
            if (ImGui.MenuItem(level.CanRedo ? "Redo " + level.RedoName : "Redo", "Ctrl+Y", false, level.CanRedo))
                placeTool.Redo();
            ImGui.Separator();
            bool selecting = activeTool == EditorTool.Select;
            bool selected = selecting && selectTool.Any;
            if (ImGui.MenuItem("Cut", "Ctrl+X", false, selected))
                Edit(hidden, report, selectTool.Cut);
            if (ImGui.MenuItem("Copy", "Ctrl+C", false, selected))
                selectTool.Copy();
            if (ImGui.MenuItem("Paste", "Ctrl+V", false, selecting && selectTool.ClipboardCount > 0))
                Edit(hidden, report, selectTool.Paste);
            if (ImGui.MenuItem("Select all", "Ctrl+A", false, selecting))
                Edit(hidden, report, selectTool.SelectAll);
            if (ImGui.MenuItem("Mirror", "H", false, selected))
                Edit(hidden, report, selectTool.Mirror);
            if (ImGui.MenuItem("Move to the layer behind", "Ctrl+PageUp", false, selected))
                Edit(hidden, report, () => selectTool.MoveToLayer(-1));
            if (ImGui.MenuItem("Move to the layer in front", "Ctrl+PageDown", false, selected))
                Edit(hidden, report, () => selectTool.MoveToLayer(1));
            ImGui.EndMenu();
        }

        /// <summary>A command that changes the layer or the selection in it: not done in a hidden layer, which says why instead.</summary>
        private static void Edit(string hidden, Action<string> report, Action command)
        {
            if (hidden != null)
                report(hidden);
            else
                command();
        }

        /// <summary>The View menu: the lighting, what is drawn, the tool and the layer, framing the level, the windows.</summary>
        /// <param name="view">The level drawn, whose back layers, front layer, objects and scenery can be hidden.</param>
        /// <param name="markers">The markers drawn over it, whose grid and gap bars can be hidden.</param>
        /// <param name="setTool">Makes a tool the active one.</param>
        /// <param name="frameLevel">Points the camera at the whole level.</param>
        public static void DrawView(SceneRenderer renderer, LevelView view, EditorMarkers markers, EditorTool activeTool, Action<EditorTool> setTool,
            ref Layer layer, Action frameLevel, ref bool showPalette, ref bool showInspector, ref bool showLevel, ref bool showDemo)
        {
            if (!ImGui.BeginMenu("View"))
                return;
            if (ImGui.MenuItem("Lighting of 2016", "F9", renderer.Classic))
                renderer.Classic = !renderer.Classic;
            if (ImGui.MenuItem("Shadows", "F10", renderer.Shadows))
                renderer.Shadows = !renderer.Shadows;
            if (ImGui.MenuItem("Ambient occlusion", "Shift+F10", renderer.AmbientOcclusion))
                renderer.AmbientOcclusion = !renderer.AmbientOcclusion;
            ImGui.Separator();
            //A hidden layer is not drawn and cannot be edited (see HiddenLayerGuard)
            foreach (Layer shown in Layers.All)
                if (shown != Layer.Middle && ImGui.MenuItem(shown.Title() + " layer", null, view.Shows(shown)))
                    view.Show(shown, !view.Shows(shown));
            if (ImGui.MenuItem("Objects", null, view.ShowObjects))
                view.ShowObjects = !view.ShowObjects;
            if (ImGui.MenuItem("Scenery", null, view.ShowScenery))
                view.ShowScenery = !view.ShowScenery;
            if (ImGui.MenuItem("Grid", null, markers.ShowGrid))
                markers.ShowGrid = !markers.ShowGrid;
            if (ImGui.MenuItem("Gap markers", null, markers.ShowGaps))
                markers.ShowGaps = !markers.ShowGaps;
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
                foreach (Layer candidate in Layers.All)
                    if (ImGui.MenuItem(candidate.Title(), null, layer == candidate))
                        layer = candidate;
                ImGui.EndMenu();
            }
            if (ImGui.MenuItem("Frame the level", "Home"))
                frameLevel();
            ImGui.Separator();
            ImGui.MenuItem("Palette", null, ref showPalette);
            ImGui.MenuItem("Inspector", null, ref showInspector);
            ImGui.MenuItem("Level", null, ref showLevel);
            ImGui.MenuItem("ImGui demo window", null, ref showDemo);
            ImGui.EndMenu();
        }
    }
}
