using System;
using System.IO;
using System.Numerics;
using ImGuiNET;

namespace TW3D.Editor
{
    /// <summary>
    /// The frame of the editor's interface: the font and the style of ImGui, scaled with the window, and the dock space
    /// the windows dock to.
    /// </summary>
    sealed class InterfaceLayout
    {
        //The font of the interface (DejaVu Sans, approved by the FSF; its licence is content/Fonts/DejaVuSans-LICENSE.txt)
        private const string FontFile = "Fonts/DejaVuSans.ttf";

        //The size of the font in pixels on a 1920 × 1080 window, scaled with the window
        private const float FontSize = 17f;

        private readonly ImGuiRenderer imgui;
        private readonly string fontPath;
        //The style ImGui starts with, scaled anew from it whenever the size of the font changes
        private readonly ImGuiStyle defaultStyle;
        private int fontSize;

        /// <param name="contentRoot">The root folder of the content (ContentManager.RootDirectory), where the font is.</param>
        public unsafe InterfaceLayout(ImGuiRenderer imgui, string contentRoot)
        {
            this.imgui = imgui;
            fontPath = Path.Combine(AppContext.BaseDirectory, contentRoot, FontFile);
            defaultStyle = *ImGui.GetStyle().NativePtr;
        }

        /// <summary>The scale of the interface: the size of the font against the size it was designed for.</summary>
        public static float Scale => ImGui.GetFontSize() / FontSize;

        /// <summary>The font and the sizes of the interface follow the size of the window; called before the layout of a frame.</summary>
        /// <param name="uiScale">The scale of the window against 1920 × 1080.</param>
        public unsafe void Update(float uiScale)
        {
            int size = Math.Max(8, (int)Math.Round(FontSize * uiScale));
            if (size == fontSize)
                return;
            fontSize = size;
            imgui.LoadFont(fontPath, size);

            ImGuiStylePtr style = ImGui.GetStyle();
            *style.NativePtr = defaultStyle;
            ImGui.StyleColorsDark();
            style.WindowRounding = 4f;
            style.FrameRounding = 3f;
            style.ScaleAllSizes(size / FontSize);
        }

        /// <summary>
        /// A dock space over the whole work area between the menu bar and the status bar: the windows can be docked to its
        /// edges, and the scene shows through its middle, where the mouse is the scene's.
        /// </summary>
        public static void DrawDockSpace(ImGuiViewportPtr viewport, float statusHeight)
        {
            ImGui.SetNextWindowPos(viewport.WorkPos);
            ImGui.SetNextWindowSize(new Vector2(viewport.WorkSize.X, viewport.WorkSize.Y - statusHeight));
            ImGui.SetNextWindowViewport(viewport.ID);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse
                | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoNavFocus
                | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings;
            ImGui.Begin("Editor dock space", flags);
            ImGui.PopStyleVar(3);
            ImGui.DockSpace(ImGui.GetID("EditorDockSpace"), Vector2.Zero, ImGuiDockNodeFlags.PassthruCentralNode);
            ImGui.End();
        }
    }
}
