using System.IO;
using System.Numerics;
using ImGuiNET;

namespace TW3D.Editor
{
    /// <summary>
    /// The status bar of the editor along the bottom of the window: the cursor cell, the layer, the tool with its block
    /// and the object chosen in the palette, the level's file with its folder as a tooltip, the warning about the level
    /// and what was done last. It keeps nothing; the screen gives it what it shows every frame.
    /// </summary>
    static class StatusBar
    {
        private static readonly Vector4 WarningColor = new Vector4(1f, 0.8f, 0.3f, 1f);

        /// <param name="height">The height of the bar, the space the dock space leaves for it.</param>
        /// <param name="chosenObject">The object chosen in the palette; null for none.</param>
        /// <param name="message">What the status bar says about the last thing done; empty for nothing.</param>
        public static void Draw(ImGuiViewportPtr viewport, float height, BlockTool tool, Layer layer, ChosenObject chosenObject,
            LevelSession session, string message)
        {
            ImGui.SetNextWindowPos(new Vector2(viewport.WorkPos.X, viewport.WorkPos.Y + viewport.WorkSize.Y - height));
            ImGui.SetNextWindowSize(new Vector2(viewport.WorkSize.X, height));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
            const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings
                | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoNavFocus;
            ImGui.Begin("Status bar", flags);
            ImGui.PopStyleVar(2);

            ImGui.Text($"Cell: {tool.Cursor.X}, {tool.Cursor.Y}");
            Separate();
            ImGui.Text("Layer: " + layer);
            Separate();
            ImGui.Text("Tool: Place");
            Separate();
            ImGui.Text($"Block: {tool.Block}, shift {tool.Shift}");
            if (chosenObject != null)
            {
                Separate();
                ImGui.Text($"Object: {chosenObject.Type} {chosenObject.Model}");
            }
            Separate();
            EditedLevel level = session.Level;
            ImGui.Text((level.File != null ? Path.GetFileName(level.File) : session.LevelName) + (level.IsDirty ? "*" : ""));
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip((level.File ?? "Not saved yet") + "\nLevels folder: " + session.Folder.FullPath);
            if (session.Warning != null)
            {
                Separate();
                ImGui.TextColored(WarningColor, session.Warning);
            }
            if (message.Length > 0)
            {
                Separate();
                ImGui.TextDisabled(message);
            }
            ImGui.End();
        }

        /// <summary>A gap between the items of the status bar.</summary>
        private static void Separate()
        {
            ImGui.SameLine(0f, ImGui.GetFontSize());
            ImGui.TextDisabled("|");
            ImGui.SameLine(0f, ImGui.GetFontSize());
        }
    }
}
