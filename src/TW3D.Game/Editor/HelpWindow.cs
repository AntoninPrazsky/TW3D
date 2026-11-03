using System.Numerics;
using ImGuiNET;

namespace TW3D.Editor
{
    /// <summary>
    /// The help window of the editor (F1, Help > Controls): a tab with the controls, in tables of rows grown from what the
    /// tools and the screen do (the mouse by the tool, the keys by what they work on, the windows), and a tab with a short
    /// guide to building a level. It keeps nothing but the text; the screen only says whether it is shown.
    /// </summary>
    static class HelpWindow
    {
        //The width of the column of the keys, in heights of the font: the longest of them fits, and the tables line up
        private const float KeysColumnWidth = 15.5f;
        //The colour of the small headings inside a section
        private static readonly Vector4 TopicColor = new Vector4(0.55f, 0.8f, 1f, 1f);

        private static readonly (string Keys, string Action)[] CameraRows =
        {
            ("Middle button drag", "Pan along the track: the point under the cursor stays under it"),
            ("Wheel", "Zoom towards the cursor"),
            ("Alt + left drag", "Orbit around the point under the cursor")
        };

        private static readonly (string Keys, string Action)[] PlaceMouseRows =
        {
            ("Left button", "Place the chosen block in the cell under the cursor, in the active layer; dragging paints (a drag is one step of undo)"),
            ("Left Shift + left button", "Erase the block under the cursor; dragging erases"),
            ("Right button", "Pick: the block under the cursor becomes the chosen block, with its shift"),
            ("Ctrl + wheel", "Shift the block under the cursor up or down (the chosen shift follows)")
        };

        private static readonly (string Keys, string Action)[] ObjectMouseRows =
        {
            ("Left button", "Add the chosen object at the point under the cursor, or select the object there; dragging moves it"),
            ("Left Shift + left button", "Remove the object under the cursor"),
            ("Right button", "Pick: the object under the cursor becomes the chosen object")
        };

        private static readonly (string Keys, string Action)[] SceneryMouseRows =
        {
            ("Left button", "Add the chosen prop at the point under the cursor, on the plane of the chosen depth, or select the prop there; dragging moves it. The mouse places and drags in steps of 1.25 physics units, on which the flat road lies"),
            ("Left Shift + left button", "Remove the prop under the cursor"),
            ("Right button", "Pick: the prop under the cursor becomes the chosen prop, with its turn and its depth")
        };

        private static readonly (string Keys, string Action)[] SelectMouseRows =
        {
            ("Left button", "Select the cell under the cursor alone: its block and the props standing in it; Ctrl adds them to the selection or takes them out"),
            ("Left drag", "From a selected cell (or one selected alone first): moves the selection by whole cells, as one step of undo. From an empty cell, or with Ctrl: a rectangle selects the blocks of the active layer and the props in it (Ctrl adds them)")
        };

        private static readonly (string Keys, string Action)[] EverywhereRows =
        {
            ("Right button", "Picks in the Place, Object and Scenery tools; the Select tool does not use it"),
            ("Left Shift", "With the left button erases (Place tool) or removes (Object and Scenery tools); Right Shift only toggles the back layer"),
            ("Ctrl", "With the left button adds to the selection or takes out of it (Select tool); with the wheel shifts a block (Place tool, so it does not zoom)"),
            ("Alt", "With the left button orbits the camera instead of using the tool")
        };

        private static readonly (string Keys, string Action)[] ToolKeyRows =
        {
            ("B / O / V / P", "The Place tool (blocks) / the Object tool (dynamic objects) / the Select tool (many blocks) / the Scenery tool (props beside the track); View > Tool lists them and the status bar names the active one"),
            ("Tab", "The next layer: back, middle, front"),
            ("Right Shift", "Toggles the back layer as soon as it is pressed"),
            ("Right Ctrl", "Toggles the front layer when it is tapped alone (let go with nothing else done while it was held)"),
            ("R", "Road mode on / off, in any tool: road blocks join the road next to them; red bars mark gaps"),
            ("M", "The mood of the level, in any tool: day, sunset, night")
        };

        private static readonly (string Keys, string Action)[] PlaceKeyRows =
        {
            ("Arrows", "Move the cursor cell (the camera follows)"),
            ("Space / Delete", "Place the chosen block / erase the block at the cursor cell"),
            ("PageUp / PageDown", "Shift the block at the cursor cell up / down (the chosen shift follows)"),
            ("F", "The block at the cursor cell is the finish of the level, or stops being it (middle layer only)"),
            (", / .", "The previous / next block of the catalog becomes the chosen block (and Place the active tool)")
        };

        private static readonly (string Keys, string Action)[] ObjectKeyRows =
        {
            ("Space", "Add the chosen object at the top of the cursor cell (it follows the mouse): it drops onto the block there"),
            ("Delete", "Remove the selected object"),
            ("Arrows / Ctrl + arrows", "Move the selected object by 1 / 0.1 physics unit; without a selection the arrows (not Ctrl) move the cursor cell"),
            ("Esc", "Clear the selection (a second Esc leaves the editor)")
        };

        private static readonly (string Keys, string Action)[] SceneryKeyRows =
        {
            ("Space", "Add the chosen prop in the middle of the cursor cell, standing on the road there"),
            ("Delete", "Remove the selected prop"),
            ("Arrows / Ctrl + arrows", "Move the selected prop by 1.25 / 0.1 physics unit; without a selection the arrows (not Ctrl) move the cursor cell"),
            ("T", "Turn the selected prop by a quarter; without a selection, the props to be added"),
            ("PageUp / PageDown", "Move the selected prop away from the camera / towards it; without a selection, the depth the props are added at (the grid shows its plane)"),
            ("Esc", "Clear the selection (a second Esc leaves the editor)")
        };

        private static readonly (string Keys, string Action)[] SelectKeyRows =
        {
            ("Ctrl+A / Esc", "Select the whole active layer with its props / clear the selection (a second Esc leaves the editor)"),
            ("Space", "Add the block and the props at the cursor cell to the selection, or take them out"),
            ("Arrows / Ctrl + arrows", "Move the selection (and the cursor) by a cell; without a selection, or with Ctrl, only the cursor cell"),
            ("PageUp / PageDown", "Shift the selected blocks up / down"),
            ("H", "Mirror the selection left to right, swapping the blocks for their f variants (b2 and b2f); the props turn as their mirror images"),
            ("Delete", "Erase the selected blocks and props"),
            ("Ctrl+C / Ctrl+X / Ctrl+V", "Copy / cut the selection; paste it with its leftmost (lowest) cell at the cursor cell (the pasted blocks and props are selected; decorations are skipped in the middle layer)")
        };

        private static readonly (string Keys, string Action)[] FileKeyRows =
        {
            ("Ctrl+N", "A new level (asks to save unsaved changes)"),
            ("Ctrl+O", "Open a level of the levels folder or any level file"),
            ("Ctrl+S", "Save the level (a new level asks for a name)"),
            ("Ctrl+Shift+S", "Save the level under a new name in the levels folder"),
            ("Ctrl+Z / Ctrl+Y", "Undo / redo (Ctrl+Shift+Z redoes too)"),
            ("Esc", "Leave the editor (asks to save unsaved changes)")
        };

        private static readonly (string Keys, string Action)[] DriveKeyRows =
        {
            ("F5", "Test drive: the level in the game from the start, with the car of the garage; Esc comes back"),
            ("Shift+F5", "Test drive from the cursor cell")
        };

        private static readonly (string Keys, string Action)[] ViewKeyRows =
        {
            ("W / S", "Fly the camera forward / back into the scene, towards or away from the layers"),
            ("A / D", "Fly the camera left / right along the track"),
            ("Q / E", "Fly the camera up / down"),
            ("Numeric keypad 4 / 6, 8 / 2", "Turn the camera"),
            ("Home", "Frame the whole level"),
            ("F9 / F10 / F11", "Lighting of 2016 / shadows / ambient occlusion"),
            ("F1", "This window")
        };

        private static readonly (string Keys, string Action)[] WindowRows =
        {
            ("Palette", "Every block of the catalog as a turning 3D preview, in groups, with a search box: click a block to choose it (Place tool). The Objects and Scenery tabs do the same for the dynamic objects (Object tool) and the props (Scenery tool). View > Palette"),
            ("Inspector", "What the active tool works on: the block at the cursor cell, the selected blocks and props, the selected object with its position to type, or the selected prop with its position, depth and turn (without one, the depth and the turn of the props to be added). View > Inspector"),
            ("Level", "The mood, the position in the level select and the par time to edit, the counts, the problems of the level and the gaps of the road: click one to go there. View > Level"),
            ("Controls", "This window: Help > Controls or F1"),
            ("View menu", "Also hides the back and front layers (which cannot be edited while hidden), the objects, the scenery, the grid and the gap markers")
        };

        private static readonly (string Heading, string Text)[] Guide =
        {
            ("Start", "The car starts at the cell [0, 0], and a new level has its start block there. Build the track to the right of it. The status bar shows the cell under the cursor, the active layer and tool and the chosen block."),
            ("Choose blocks", "Choose a block in the Palette. The middle layer is the road the car drives on and the blocks that collide; decorations go to the back layer (behind the road) and to the front layer (in front of it): Tab, Right Shift and Right Ctrl switch the layer, and the Palette greys out the decorations while the middle layer is active."),
            ("Paint a road", "Switch road mode on (R, or Level > Road mode) and paint the road with the left button: every road block joins the one next to it at the right height. Where two road blocks do not meet, a red bar marks the gap (the Level window lists the gaps and a click goes to one). A gap may be left on purpose. Blocks can be shifted up and down with PageUp, PageDown and Ctrl + the wheel, and a mistake is taken back with Ctrl+Z."),
            ("Put the finish", "Put the cursor on a block of the middle layer and press F: that block is the finish, where the run ends. A level without a finish has no end, and the Level window says so."),
            ("Add objects", "Boxes, bars, ramps and the other dynamic objects are added with the Object tool (O): choose one in the Objects tab of the Palette and click where it is to be, or press Space to drop it onto the block at the cursor cell. Drag an object to move it."),
            ("Add scenery", "Traffic cones, street lamps, utility poles, fences, guardrails, posts with a chain and a judges' stand stand beside the track without physics: the car drives through them. Choose one in the Scenery tab of the Palette (the Scenery tool, P), set the depth it goes to (PageUp and PageDown, or the Inspector: negative behind the road, positive in front of it; the road reaches 11.25 to either side, the back and front layers the next 22.5) and click where it is to stand, or press Space to stand it on the road at the cursor cell. T turns it. A guardrail is a cell long: Space at every cell of a straight road makes a continuous one; two pieces of the fence or of the chain fill a cell. The Select tool moves, copies and mirrors the props with the blocks of the layer they stand in."),
            ("Test drive", "Press F5 to drive the level at once, from the start (Shift+F5 from the cursor cell). Nothing is saved and no record is kept. Esc brings the editor back as it was, so test often."),
            ("Save", "Press Ctrl+S. A new level asks for a name; the file goes into the levels folder (content/Levels of the repository), where the game finds it: the level select shows it at once. The status bar counts the problems and the road gaps of the level when it is saved."),
            ("Set the level", "In the Level window set the mood (day, sunset or night), the position in the level select (the order) and the par time, the time a good run takes. A level without an order comes after the others in the level select, and the game estimates a par time when the level has none."),
            ("The thesis of 2016", "The Czech thesis of 2016 (docs/bachelor-thesis in the repository) describes the old editor, not this one: the controls of this editor are in the Controls tab.")
        };

        /// <param name="open">The window is shown; its close button clears it.</param>
        /// <param name="scale">The scale of the interface (the font size against the size it was designed for).</param>
        public static void Draw(ref bool open, float scale)
        {
            ImGui.SetNextWindowSize(new Vector2(760f, 780f) * scale, ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Controls", ref open))
            {
                //Opened by F1 over the other windows, not behind one that was shown later
                if (ImGui.IsWindowAppearing())
                    ImGui.SetWindowFocus();
                if (ImGui.BeginTabBar("help tabs"))
                {
                    if (ImGui.BeginTabItem("Controls"))
                    {
                        DrawControls();
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("How to build a level"))
                    {
                        DrawGuide();
                        ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }
            }
            ImGui.End();
        }

        /// <summary>The Controls tab: the mouse, the keys and the windows.</summary>
        private static void DrawControls()
        {
            ImGui.SeparatorText("Mouse");
            Topic("Camera");
            Rows("camera", CameraRows);
            Topic("Place tool (B): blocks");
            Rows("place mouse", PlaceMouseRows);
            Topic("Object tool (O): dynamic objects");
            Rows("object mouse", ObjectMouseRows);
            Topic("Select tool (V): many blocks of the layer");
            Rows("select mouse", SelectMouseRows);
            Topic("Scenery tool (P): props beside the track");
            Rows("scenery mouse", SceneryMouseRows);
            Topic("Everywhere");
            Rows("everywhere", EverywhereRows);

            ImGui.SeparatorText("Keys");
            Topic("Tools and layers");
            Rows("tool keys", ToolKeyRows);
            Topic("Place tool");
            Rows("place keys", PlaceKeyRows);
            Topic("Object tool");
            Rows("object keys", ObjectKeyRows);
            Topic("Select tool");
            Rows("select keys", SelectKeyRows);
            Topic("Scenery tool");
            Rows("scenery keys", SceneryKeyRows);
            Topic("Files and undo");
            Rows("file keys", FileKeyRows);
            Topic("Test drive");
            Rows("drive keys", DriveKeyRows);
            Topic("View");
            Rows("view keys", ViewKeyRows);

            ImGui.SeparatorText("Windows");
            Rows("windows", WindowRows);
        }

        /// <summary>The How to build a level tab: a short guide in paragraphs.</summary>
        private static void DrawGuide()
        {
            foreach ((string heading, string text) in Guide)
            {
                ImGui.SeparatorText(heading);
                ImGui.TextWrapped(text);
            }
        }

        /// <summary>A small heading inside a section.</summary>
        private static void Topic(string text)
        {
            ImGui.TextColored(TopicColor, text);
        }

        private static void Rows(string id, (string Keys, string Action)[] rows)
        {
            if (!ImGui.BeginTable(id, 2, ImGuiTableFlags.SizingStretchProp))
                return;
            ImGui.TableSetupColumn("Keys", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * KeysColumnWidth);
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthStretch);
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
