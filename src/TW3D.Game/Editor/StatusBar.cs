using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using ImGuiNET;

namespace TW3D.Editor
{
    /// <summary>
    /// The status bar of the editor along the bottom of the window: the cursor cell, the layer, the active tool, the
    /// chosen block and the chosen object (and the chosen prop with its depth and turn while the Scenery tool is active),
    /// the level's file with its folder as a tooltip, how many problems and gaps
    /// of the road the level has, the warning about the level and what was done last. It keeps nothing; the screen gives it what it shows every frame.
    /// </summary>
    static class StatusBar
    {
        private static readonly Vector4 WarningColor = new Vector4(1f, 0.8f, 0.3f, 1f);
        private static readonly Vector4 RoadColor = new Vector4(0.45f, 0.85f, 1f, 1f);
        private static readonly Vector4 GapColor = new Vector4(1f, 0.4f, 0.4f, 1f);
        //The names of the tools by the EditorTool enum, made once instead of in every frame
        private static readonly string[] ToolNames = Enum.GetNames<EditorTool>();

        /// <param name="height">The height of the bar, the space the dock space leaves for it.</param>
        /// <param name="activeTool">The tool that gets the mouse and the keys.</param>
        /// <param name="chosenObject">The object chosen in the palette or picked; null for none.</param>
        /// <param name="sceneryTool">The Scenery tool, whose chosen prop is shown while it is active.</param>
        /// <param name="report">What the level looks like; its problems and gaps are counted here.</param>
        /// <param name="message">What the status bar says about the last thing done; empty for nothing.</param>
        public static void Draw(ImGuiViewportPtr viewport, float height, BlockTool tool, EditorTool activeTool, Layer layer, ChosenObject chosenObject,
            SceneryTool sceneryTool, LevelSession session, LevelReport report, string message)
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
            ImGui.Text("Tool: " + ToolNames[(int)activeTool]);
            Separate();
            ImGui.Text($"Block: {tool.Block}, shift {tool.Shift}");
            if (tool.RoadMode)
            {
                ImGui.SameLine();
                ImGui.TextColored(RoadColor, "Road");
            }
            if (chosenObject != null)
            {
                Separate();
                ImGui.Text($"Object: {chosenObject.Type} {chosenObject.Model}");
            }
            if (activeTool == EditorTool.Scenery && sceneryTool.Chosen != null)
            {
                Separate();
                ImGui.Text($"Scenery: {sceneryTool.Chosen.Name}, depth {sceneryTool.Depth.ToString("0.##", CultureInfo.InvariantCulture)}, {SceneryTool.Turns(sceneryTool.Turn)}");
            }
            Separate();
            EditedLevel level = session.Level;
            ImGui.Text((level.File != null ? Path.GetFileName(level.File) : session.LevelName) + (level.IsDirty ? "*" : ""));
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip((level.File ?? "Not saved yet") + "\nLevels folder: " + session.Folder.FullPath);
            if (report.ProblemsText != null)
            {
                Separate();
                ImGui.TextColored(WarningColor, report.ProblemsText);
            }
            if (report.GapsText != null)
            {
                Separate();
                ImGui.TextColored(GapColor, report.GapsText);
            }
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
