using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using TW3D.Shared;

namespace TW3D.Editor
{
    /// <summary>
    /// The Palette window of the editor: the blocks of the catalog as the turning previews of the
    /// <see cref="BlockPreviews"/>, in the groups of the catalog (<see cref="BlockType.Group"/>, as on the catalog page)
    /// as collapsing headers, found by a search box; a click makes one the chosen block of the <see cref="BlockTool"/>
    /// and the Place tool the active one. Decorations (blocks without a collision shape) are disabled while the middle
    /// layer is active, as the document refuses them there. A second tab shows the models of the dynamic objects by
    /// their type; a click makes one the chosen object of the <see cref="ObjectTool"/> and that tool the active one. A
    /// third tab shows the props of the scenery; a click makes one the chosen prop of the <see cref="SceneryTool"/> and
    /// that tool the active one.
    /// </summary>
    sealed class PaletteWindow
    {
        //A block of the catalog without a group goes to the last group. A block that turns has one preview: T turns the
        //chosen block
        private const string OtherGroup = "Other";
        private const string DecorationNote = "\nDecoration: back or front layers only";
        //The frame of the chosen preview: the colour of its button and of the border drawn around it
        private static readonly Vector4 ChosenButton = new Vector4(0.26f, 0.59f, 0.98f, 0.7f);
        private static readonly Vector4 ChosenBorder = new Vector4(1f, 0.85f, 0.2f, 1f);
        private const float BorderThickness = 2f;
        //The window first appears wide enough for this many previews side by side
        private const int DefaultColumns = 3;
        /// <summary>The height and the margin from the edge of the work area (pixels on a 1920 × 1080 window) the window first appears with.</summary>
        public const float DefaultHeight = 450f, DefaultMargin = 20f;
        private const uint SearchLength = 32;

        /// <summary>A preview shown in a group: the block, the model of an object type, or a prop of the scenery.</summary>
        private sealed class Entry
        {
            public int Preview;
            public BlockType Block;
            public string Type, Model;
            public SceneryType Prop;
            //What the tooltip says: the name of the block, "type: model" of an object; also the id of the button, unique
            //in its tab as it names the type of a model that two types share
            public string Label;
            //The tooltip of a block disabled in the middle layer
            public string DisabledLabel;
        }

        private sealed class Group
        {
            public string Name;
            public List<Entry> Entries = new List<Entry>();
        }

        private readonly BlockPreviews previews;
        private readonly Action<EditorTool> activate;
        private readonly List<Group> blockGroups = new List<Group>();
        private readonly List<Group> objectGroups = new List<Group>();
        private readonly List<Entry> sceneryEntries = new List<Entry>();
        private string search = "";
        //The border of the chosen preview in pixels, scaled with the interface
        private float borderThickness = BorderThickness;

        /// <param name="activate">Makes a tool the active one: the tool of what a click chose.</param>
        public PaletteWindow(BlockPreviews previews, Catalog catalog, Action<EditorTool> activate)
        {
            this.previews = previews;
            this.activate = activate;
            //The groups of the catalog in the order they first appear in it, each block in the order of the catalog
            Group other = new Group { Name = OtherGroup };
            foreach (BlockType type in catalog.Blocks)
            {
                Group group = other;
                if (!string.IsNullOrEmpty(type.Group))
                {
                    group = blockGroups.Find(g => g.Name == type.Group);
                    if (group == null)
                        blockGroups.Add(group = new Group { Name = type.Group });
                }
                group.Entries.Add(BlockEntry(type));
            }
            if (other.Entries.Count > 0)
                blockGroups.Add(other);

            foreach (ObjectType type in catalog.Objects)
            {
                Group group = new Group { Name = type.Type };
                foreach (string model in type.Models)
                    group.Entries.Add(new Entry { Preview = previews.ObjectModelIndex(model), Type = type.Type, Model = model, Label = type.Type + ": " + model });
                objectGroups.Add(group);
            }

            foreach (SceneryType prop in catalog.Scenery)
                sceneryEntries.Add(new Entry { Preview = previews.SceneryIndex(prop.Model), Prop = prop, Label = prop.Name });
        }

        private Entry BlockEntry(BlockType type)
        {
            //"b2: Slope, 4 steps a cell (24°)": the tooltip, and what the search looks in
            string label = string.IsNullOrEmpty(type.Title) ? type.Name : type.Name + ": " + type.Title;
            return new Entry { Preview = previews.BlockIndex(type.Name), Block = type, Label = label, DisabledLabel = label + DecorationNote };
        }

        /// <summary>
        /// The previews are shown, so the atlas is to be drawn for the next frame; false while the window is collapsed
        /// or docked away, when ImGui lays nothing out.
        /// </summary>
        public bool NeedsPreviews { get; private set; } = true;

        /// <summary>The width the window first appears with (pixels), for the Inspector to appear under it as wide.</summary>
        public float DefaultWidth { get; private set; }

        /// <summary>Lays the window out; called every frame while it is shown.</summary>
        /// <param name="open">False after the window was closed by its button.</param>
        /// <param name="tool">The Place tool, whose chosen block a click sets.</param>
        /// <param name="objectTool">The Object tool, whose chosen object a click sets.</param>
        /// <param name="sceneryTool">The Scenery tool, whose chosen prop a click sets.</param>
        /// <param name="layer">The active layer: decorations are disabled in the middle one.</param>
        /// <param name="cellSize">The size of a preview in pixels.</param>
        /// <param name="scale">The scale of the interface (1 on a 1920 × 1080 window).</param>
        public void Draw(ref bool open, ImGuiViewportPtr viewport, BlockTool tool, ObjectTool objectTool, SceneryTool sceneryTool, Layer layer,
            int cellSize, float scale)
        {
            ImGuiStylePtr style = ImGui.GetStyle();
            float buttonWidth = cellSize + style.FramePadding.X * 2f;
            float width = DefaultColumns * buttonWidth + (DefaultColumns - 1) * style.ItemSpacing.X + style.WindowPadding.X * 2f + style.ScrollbarSize;
            borderThickness = BorderThickness * scale;
            DefaultWidth = width;
            ImGui.SetNextWindowSize(new Vector2(width, DefaultHeight * scale), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(viewport.WorkPos.X + viewport.WorkSize.X - width - DefaultMargin * scale, viewport.WorkPos.Y + DefaultMargin * scale), ImGuiCond.FirstUseEver);

            int hovered = -1;
            NeedsPreviews = ImGui.Begin("Palette", ref open);
            if (NeedsPreviews && ImGui.BeginTabBar("PaletteTabs"))
            {
                if (ImGui.BeginTabItem("Blocks"))
                {
                    DrawBlocks(tool, layer, cellSize, buttonWidth, ref hovered);
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Objects"))
                {
                    DrawObjects(objectTool, cellSize, buttonWidth, ref hovered);
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Scenery"))
                {
                    DrawScenery(sceneryTool, cellSize, buttonWidth, ref hovered);
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
            ImGui.End();
            previews.Hovered = hovered;
        }

        private void DrawBlocks(BlockTool tool, Layer layer, int cellSize, float buttonWidth, ref int hovered)
        {
            //The style switch: the style the placed blocks get (the level's when none), which the previews show
            if (tool.Level != null)
            {
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.BeginCombo("##style", "Style: " + StyleNames.Name(tool.Style)))
                {
                    foreach (string style in StyleNames.BlockStyles(tool.Level.Catalog))
                        if (ImGui.Selectable(StyleNames.Name(style), style == tool.Style))
                            tool.Style = style;
                    ImGui.EndCombo();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("The style the placed blocks get: the level's (none of their own), a style, or a\ntransition from one style to another, left to right as the block is placed unturned.\nA block without a look in it takes the level's.");
                previews.Style = tool.Style ?? tool.Level.Style;
            }
            ImGui.SetNextItemWidth(-1f);
            ImGui.InputTextWithHint("##search", "Search", ref search, SearchLength);
            bool middle = layer == Layer.Middle;
            foreach (Group group in blockGroups)
            {
                if (!AnyMatch(group))
                    continue;
                if (!ImGui.CollapsingHeader(group.Name, ImGuiTreeNodeFlags.DefaultOpen))
                    continue;
                float right = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
                bool first = true;
                foreach (Entry entry in group.Entries)
                {
                    if (!Matches(entry))
                        continue;
                    Wrap(ref first, buttonWidth, right);
                    bool disabled = middle && !entry.Block.Collision;
                    if (disabled)
                        ImGui.BeginDisabled();
                    if (Button(entry, cellSize, entry.Block.Name == tool.Block))
                    {
                        tool.Choose(entry.Block.Name);
                        activate(EditorTool.Place);
                    }
                    if (disabled)
                        ImGui.EndDisabled();
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    {
                        hovered = entry.Preview;
                        ImGui.SetTooltip(disabled ? entry.DisabledLabel : entry.Label);
                    }
                }
            }
        }

        private void DrawObjects(ObjectTool objectTool, int cellSize, float buttonWidth, ref int hovered)
        {
            ChosenObject chosenObject = objectTool.Chosen;
            foreach (Group group in objectGroups)
            {
                if (!ImGui.CollapsingHeader(group.Name, ImGuiTreeNodeFlags.DefaultOpen))
                    continue;
                float right = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
                bool first = true;
                foreach (Entry entry in group.Entries)
                {
                    Wrap(ref first, buttonWidth, right);
                    bool chosen = chosenObject != null && chosenObject.Type == entry.Type && chosenObject.Model == entry.Model;
                    if (Button(entry, cellSize, chosen))
                    {
                        objectTool.Choose(entry.Type, entry.Model);
                        activate(EditorTool.Object);
                    }
                    if (ImGui.IsItemHovered())
                    {
                        hovered = entry.Preview;
                        ImGui.SetTooltip(entry.Label);
                    }
                }
            }
        }

        private void DrawScenery(SceneryTool sceneryTool, int cellSize, float buttonWidth, ref int hovered)
        {
            ImGui.TextWrapped("Props beside the track, without physics.");
            float right = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
            bool first = true;
            foreach (Entry entry in sceneryEntries)
            {
                Wrap(ref first, buttonWidth, right);
                if (Button(entry, cellSize, sceneryTool.Chosen == entry.Prop))
                {
                    sceneryTool.Choose(entry.Prop.Model);
                    activate(EditorTool.Scenery);
                }
                if (ImGui.IsItemHovered())
                {
                    hovered = entry.Preview;
                    ImGui.SetTooltip(entry.Label);
                }
            }
        }

        /// <summary>The next button goes beside the last one while it fits in the width of the window, else under it.</summary>
        private static void Wrap(ref bool first, float buttonWidth, float right)
        {
            if (!first && ImGui.GetItemRectMax().X + ImGui.GetStyle().ItemSpacing.X + buttonWidth < right)
                ImGui.SameLine();
            first = false;
        }

        /// <summary>An image button with the cell of the preview; the chosen one has a highlighted button and a border.</summary>
        private bool Button(Entry entry, int cellSize, bool chosen)
        {
            if (chosen)
                ImGui.PushStyleColor(ImGuiCol.Button, ChosenButton);
            (Vector2 min, Vector2 max) = previews.CellUv(entry.Preview);
            bool clicked = ImGui.ImageButton(entry.Label, previews.TextureId, new Vector2(cellSize, cellSize), min, max);
            if (chosen)
            {
                ImGui.PopStyleColor();
                ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.GetColorU32(ChosenBorder),
                    ImGui.GetStyle().FrameRounding, ImDrawFlags.None, borderThickness);
            }
            return clicked;
        }

        private bool AnyMatch(Group group)
        {
            if (search.Length == 0)
                return true;
            foreach (Entry entry in group.Entries)
                if (Matches(entry))
                    return true;
            return false;
        }

        private bool Matches(Entry entry)
        {
            return search.Length == 0 || entry.Label.Contains(search, StringComparison.OrdinalIgnoreCase);
        }
    }
}
