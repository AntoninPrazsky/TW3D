using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using TW3D.Shared;

namespace TW3D.Editor
{
    /// <summary>
    /// The Palette window of the editor: the blocks of the catalog as the turning previews of the
    /// <see cref="BlockPreviews"/>, in the groups (the families of the blocks) of the catalog page docs/catalog/catalog.html
    /// as collapsing headers, found by a search box; a click makes one the chosen block of the <see cref="BlockTool"/>.
    /// Decorations (blocks without a collision shape) are disabled while the middle layer is active, as the document
    /// refuses them there. A second tab shows the models of the dynamic objects by their type; a click chooses the
    /// object to place (<see cref="Object"/>, for the Object tool).
    /// </summary>
    sealed class PaletteWindow
    {
        //The families of the blocks in the order of the catalog page, each with its blocks in the order of the page;
        //a block of the catalog that is in none of them goes to the last group. The page has no tu3s, which is a
        //tunnel like the others
        private static readonly (string Group, string[] Blocks)[] Families =
        {
            ("A", new[] { "a1" }),
            ("B", new[] { "b1", "b2", "b3", "b4" }),
            ("Bf", new[] { "b1f", "b2f", "b3f", "b4f" }),
            ("C", new[] { "c1", "c2", "c3" }),
            ("Cf", new[] { "c1f", "c2f", "c3f" }),
            ("D", new[] { "d1", "d2", "d3" }),
            ("Df", new[] { "d1f", "d2f", "d3f" }),
            ("S", new[] { "s1", "s1f" }),
            ("BA", new[] { "ba1", "ba2" }),
            ("U", new[] { "u1", "u2" }),
            ("T", new[] { "t1", "t2" }),
            ("TU", new[] { "tu1s", "tu2s", "tu3s", "tu1v", "tu3v", "tu2v" }),
            ("XA", new[] { "xa1", "xa2", "xa3", "xa4", "xa5", "xa6", "xa7" }),
            ("KEA", new[] { "kea1a", "kea1b", "kea1c", "kea1d", "kea1e", "kea1f" }),
            ("SB", new[] { "sb", "g", "sg", "st", "st2" }),
            ("BL", new[] { "bl1", "bl2", "bl2f", "bl1s", "bl2s", "bl2sf", "za" }),
            ("LO", new[] { "lo" })
        };
        private const string OtherGroup = "Other";
        private const string DecorationNote = " (decoration: back or front layer only)";
        //The frame of the chosen preview: the colour of its button and of the border drawn around it
        private static readonly Vector4 ChosenButton = new Vector4(0.26f, 0.59f, 0.98f, 0.7f);
        private static readonly Vector4 ChosenBorder = new Vector4(1f, 0.85f, 0.2f, 1f);
        private const float BorderThickness = 2f;
        //The window first appears wide enough for this many previews side by side
        private const int DefaultColumns = 3;
        private const uint SearchLength = 32;

        /// <summary>A preview shown in a group: the block, or the model of an object type.</summary>
        private sealed class Entry
        {
            public int Preview;
            public BlockType Block;
            public string Type, Model;
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
        private readonly List<Group> blockGroups = new List<Group>();
        private readonly List<Group> objectGroups = new List<Group>();
        private string search = "";
        //The border of the chosen preview in pixels, scaled with the interface
        private float borderThickness = BorderThickness;

        public PaletteWindow(BlockPreviews previews, Catalog catalog)
        {
            this.previews = previews;
            HashSet<string> grouped = new HashSet<string>();
            foreach ((string name, string[] blocks) in Families)
            {
                Group group = new Group { Name = name };
                foreach (string block in blocks)
                {
                    BlockType type = catalog.FindBlock(block);
                    if (type == null)
                        continue;
                    grouped.Add(block);
                    group.Entries.Add(BlockEntry(type));
                }
                if (group.Entries.Count > 0)
                    blockGroups.Add(group);
            }
            Group other = new Group { Name = OtherGroup };
            foreach (BlockType type in catalog.Blocks)
                if (!grouped.Contains(type.Name))
                    other.Entries.Add(BlockEntry(type));
            if (other.Entries.Count > 0)
                blockGroups.Add(other);

            foreach (ObjectType type in catalog.Objects)
            {
                Group group = new Group { Name = type.Type };
                foreach (string model in type.Models)
                    group.Entries.Add(new Entry { Preview = previews.ObjectModelIndex(model), Type = type.Type, Model = model, Label = type.Type + ": " + model });
                objectGroups.Add(group);
            }
        }

        private Entry BlockEntry(BlockType type)
        {
            return new Entry { Preview = previews.BlockIndex(type.Name), Block = type, Label = type.Name, DisabledLabel = type.Name + DecorationNote };
        }

        /// <summary>
        /// The previews are shown, so the atlas is to be drawn for the next frame; false while the window is collapsed
        /// or docked away, when ImGui lays nothing out.
        /// </summary>
        public bool NeedsPreviews { get; private set; } = true;

        /// <summary>The dynamic object chosen in the Objects tab; null until one is chosen.</summary>
        public ChosenObject Object { get; private set; }

        /// <summary>Lays the window out; called every frame while it is shown.</summary>
        /// <param name="open">False after the window was closed by its button.</param>
        /// <param name="tool">The Place tool, whose chosen block a click sets.</param>
        /// <param name="layer">The active layer: decorations are disabled in the middle one.</param>
        /// <param name="cellSize">The size of a preview in pixels.</param>
        /// <param name="scale">The scale of the interface (1 on a 1920 × 1080 window).</param>
        public void Draw(ref bool open, ImGuiViewportPtr viewport, BlockTool tool, Layer layer, int cellSize, float scale)
        {
            ImGuiStylePtr style = ImGui.GetStyle();
            float buttonWidth = cellSize + style.FramePadding.X * 2f;
            float width = DefaultColumns * buttonWidth + (DefaultColumns - 1) * style.ItemSpacing.X + style.WindowPadding.X * 2f + style.ScrollbarSize;
            borderThickness = BorderThickness * scale;
            ImGui.SetNextWindowSize(new Vector2(width, 600f * scale), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(viewport.WorkPos.X + viewport.WorkSize.X - width - 20f * scale, viewport.WorkPos.Y + 20f * scale), ImGuiCond.FirstUseEver);

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
                    DrawObjects(cellSize, buttonWidth, ref hovered);
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
            ImGui.End();
            previews.Hovered = hovered;
        }

        private void DrawBlocks(BlockTool tool, Layer layer, int cellSize, float buttonWidth, ref int hovered)
        {
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
                        tool.Choose(entry.Block.Name);
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

        private void DrawObjects(int cellSize, float buttonWidth, ref int hovered)
        {
            foreach (Group group in objectGroups)
            {
                if (!ImGui.CollapsingHeader(group.Name, ImGuiTreeNodeFlags.DefaultOpen))
                    continue;
                float right = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
                bool first = true;
                foreach (Entry entry in group.Entries)
                {
                    Wrap(ref first, buttonWidth, right);
                    bool chosen = Object != null && Object.Type == entry.Type && Object.Model == entry.Model;
                    if (Button(entry, cellSize, chosen))
                        Object = new ChosenObject(entry.Type, entry.Model);
                    if (ImGui.IsItemHovered())
                    {
                        hovered = entry.Preview;
                        ImGui.SetTooltip(entry.Label);
                    }
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
