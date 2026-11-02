using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;
using TW3D.Shared;

namespace TW3D.Editor
{
    /// <summary>The answer to "Save changes?".</summary>
    enum SaveChangesAnswer
    {
        Save,
        DontSave,
        Cancel
    }

    /// <summary>
    /// The modal dialogs of the File menu, drawn with ImGui: Open (the levels of the <see cref="LevelFolder"/> in a table
    /// with their previews, and a path field for any level file), Save as (a name in the folder, checked, with "Replace?"
    /// for an existing file) and "Save changes?". The screen shows a dialog by <see cref="ShowOpen"/>,
    /// <see cref="ShowSaveAs"/> or <see cref="AskSaveChanges"/> (from its keys or from its menu) and hears how it ended
    /// through the callbacks; the popups themselves are opened at the root of ImGui's ID stack, in <see cref="Draw"/>,
    /// where they are begun (a popup opened inside a menu would never appear). A dialog reacts to its keys only from
    /// its second frame, so that the key that opened it (Esc for "Save changes?") does not also answer it.
    /// </summary>
    sealed class FileDialogs : IDisposable
    {
        private enum Dialog
        {
            None,
            Open,
            SaveAs,
            SaveChanges
        }

        //The titles of the popups; what follows ### is the ID, so that a title can change without making a new popup
        private const string OpenTitle = "Open level###OpenLevel";
        private const string SaveAsTitle = "Save level as###SaveLevelAs";
        private const string ReplaceTitle = "Replace?###ReplaceLevel";
        private const string SaveChangesTitle = "Save changes?###SaveChanges";
        private const int NameLength = 128;
        private const int PathLength = 1024;
        //The size of a preview in the Open dialog (the previews are 700 × 394) on a 1920 × 1080 window
        private const float PreviewWidth = 128f;
        private const float PreviewHeight = 72f;
        //The width of the Open dialog's table on a 1920 × 1080 window, and how much of the window's height it may take
        private const float OpenWidth = 820f;
        private const float OpenHeightShare = 0.55f;
        private static readonly Vector4 ErrorColor = new Vector4(1f, 0.4f, 0.35f, 1f);

        private readonly GraphicsDevice device;
        private readonly ImGuiRenderer imgui;
        private readonly LevelFolder folder;
        private readonly Action<string> open;
        private readonly Action<string> saveAs;
        private readonly Action saveAsCancelled;
        private readonly Action<SaveChangesAnswer> answer;

        private Dialog dialog;
        //The popup of the dialog is still to be opened (at the root, in Draw)
        private bool opening;

        //Open: the levels of the folder, the selected row, its previews (bound to ImGui while the dialog is open), the path field
        private List<LevelEntry> entries = new List<LevelEntry>();
        private int selected = -1;
        private bool scrollToSelected;
        private string selectFile;
        private readonly Dictionary<string, (Texture2D Texture, IntPtr Id)> previews = new Dictionary<string, (Texture2D, IntPtr)>();
        //Previews of a closed dialog, released at the start of the next frame: the frame that closed it may still draw them
        private readonly List<(Texture2D Texture, IntPtr Id)> released = new List<(Texture2D, IntPtr)>();
        private string path = "";
        private string pathError;
        //Why the folder itself cannot be listed; null when it can
        private string listError;

        //Save as: the name typed, why it is refused, the file asked about by "Replace?"
        private string name = "";
        private string nameError;
        private string replaceFile;
        //The name field gets the keyboard back in the next frame: Enter took it away, and a refused name or a cancelled
        //"Replace?" leaves the user to correct the name, possibly with the keyboard only
        private bool focusName;

        //Save changes: the name of the level
        private string levelName;

        /// <param name="open">Gets the file chosen in the Open dialog.</param>
        /// <param name="saveAs">Saves the level as the file chosen in Save as (confirmed when it existed); a failure is the screen's to tell.</param>
        /// <param name="saveAsCancelled">Hears that Save as was given up.</param>
        /// <param name="answer">Gets the answer to "Save changes?".</param>
        public FileDialogs(GraphicsDevice device, ImGuiRenderer imgui, LevelFolder folder, Action<string> open,
            Action<string> saveAs, Action saveAsCancelled, Action<SaveChangesAnswer> answer)
        {
            this.device = device;
            this.imgui = imgui;
            this.folder = folder;
            this.open = open;
            this.saveAs = saveAs;
            this.saveAsCancelled = saveAsCancelled;
            this.answer = answer;
        }

        /// <summary>A dialog is shown (or about to be): the scene gets no input.</summary>
        public bool IsOpen => dialog != Dialog.None;

        /// <summary>Shows the Open dialog.</summary>
        /// <param name="current">The file of the level being edited, selected in the list; null for none.</param>
        public void ShowOpen(string current)
        {
            Show(Dialog.Open);
            selectFile = current;
            path = "";
            pathError = null;
        }

        /// <summary>Shows the Save as dialog.</summary>
        /// <param name="proposed">The name offered (the level's own name); empty for none.</param>
        public void ShowSaveAs(string proposed)
        {
            Show(Dialog.SaveAs);
            name = proposed ?? "";
            nameError = null;
            replaceFile = null;
            focusName = false;
        }

        /// <summary>Asks "Save changes to the level?"; the answer comes to the callback.</summary>
        public void AskSaveChanges(string level)
        {
            Show(Dialog.SaveChanges);
            levelName = level;
        }

        /// <summary>A dialog replaces the one shown (its popup is replaced when the new one opens at the same level).</summary>
        private void Show(Dialog next)
        {
            Dialog previous = dialog;
            dialog = Dialog.None;
            Ended(previous);
            if (previous == Dialog.SaveAs)
                saveAsCancelled();
            dialog = next;
            opening = true;
        }

        /// <summary>Lays out the dialog shown, if any; called every frame between the frames of ImGui, at the root of its ID stack.</summary>
        /// <param name="scale">The scale of the interface (1 on a 1920 × 1080 window).</param>
        public void Draw(float scale)
        {
            ReleasePreviews();
            if (dialog == Dialog.None)
                return;

            string title = dialog == Dialog.Open ? OpenTitle : dialog == Dialog.SaveAs ? SaveAsTitle : SaveChangesTitle;
            if (opening)
            {
                opening = false;
                if (dialog == Dialog.Open)
                    LoadEntries();
                ImGui.OpenPopup(title);
            }

            ImGuiViewportPtr viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            if (!ImGui.BeginPopupModal(title, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
            {
                //Closed by ImGui itself: as if cancelled
                Cancel();
                return;
            }
            //The key that opened the dialog is still pressed in its first frame
            bool keys = !ImGui.IsWindowAppearing();
            switch (dialog)
            {
                case Dialog.Open:
                    DrawOpen(scale, keys);
                    break;
                case Dialog.SaveAs:
                    DrawSaveAs(keys);
                    break;
                case Dialog.SaveChanges:
                    DrawSaveChanges(keys);
                    break;
            }
            ImGui.EndPopup();
        }

        /// <summary>The dialog shown ends: its popup closes (call between its begin and end), and its state goes.</summary>
        private void Close()
        {
            ImGui.CloseCurrentPopup();
            Dialog closed = dialog;
            dialog = Dialog.None;
            Ended(closed);
        }

        /// <summary>The dialog shown is given up (Esc, Cancel).</summary>
        private void Cancel()
        {
            Dialog cancelled = dialog;
            Close();
            if (cancelled == Dialog.SaveAs)
                saveAsCancelled();
            else if (cancelled == Dialog.SaveChanges)
                answer(SaveChangesAnswer.Cancel);
        }

        private void Ended(Dialog closed)
        {
            if (closed != Dialog.Open)
                return;
            foreach ((Texture2D texture, IntPtr id) in previews.Values)
                released.Add((texture, id));
            previews.Clear();
            entries = new List<LevelEntry>();
        }

        private void ReleasePreviews()
        {
            foreach ((Texture2D texture, IntPtr id) in released)
            {
                imgui.UnbindTexture(id);
                texture.Dispose();
            }
            released.Clear();
        }

        /// <summary>Reads the levels of the folder and loads their previews for the Open dialog.</summary>
        private void LoadEntries()
        {
            entries = folder.List(out listError);
            selected = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                if (selectFile != null && LevelFolder.SamePath(entries[i].File, selectFile))
                    selected = i;
                string preview = folder.Preview(entries[i].Name);
                if (preview == null)
                    continue;
                try
                {
                    Texture2D texture = Texture2D.FromFile(device, preview);
                    previews[entries[i].Name] = (texture, imgui.BindTexture(texture));
                }
                //A preview that cannot be read is only a missing picture; the image loader throws more than IO errors
                catch (Exception e)
                {
                    Debug.WriteLine("Preview " + preview + " not loaded: " + e.Message);
                }
            }
            scrollToSelected = selected >= 0;
        }

        private void DrawOpen(float scale, bool keys)
        {
            ImGui.TextDisabled("Folder:");
            ImGui.SameLine();
            ImGui.Text(folder.FullPath);
            //The dialog is at least this wide; the table and the path field fill it, however long the folder is
            ImGui.Dummy(new Vector2(OpenWidth * scale, 0f));

            bool typing = ImGui.GetIO().WantTextInput;
            if (keys && !typing && entries.Count > 0)
            {
                if (ImGui.IsKeyPressed(ImGuiKey.DownArrow))
                    Select(Math.Min(entries.Count - 1, selected + 1), 1);
                if (ImGui.IsKeyPressed(ImGuiKey.UpArrow))
                    Select(Math.Max(0, selected - 1), -1);
            }

            string chosen = null;
            Vector2 previewSize = new Vector2(PreviewWidth, PreviewHeight) * scale;
            //Rows as high as the previews, when there are any
            float rowHeight = previews.Count > 0 ? Math.Max(previewSize.Y, ImGui.GetTextLineHeight()) : ImGui.GetTextLineHeight();
            float tableHeight = Math.Min(ImGui.GetMainViewport().WorkSize.Y * OpenHeightShare,
                ImGui.GetFrameHeightWithSpacing() + Math.Max(1, entries.Count) * (rowHeight + ImGui.GetStyle().CellPadding.Y * 2f));
            const ImGuiTableFlags flags = ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH
                | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.SizingFixedFit;
            if (ImGui.BeginTable("levels", 5, flags, new Vector2(0f, tableHeight)))
            {
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Preview", ImGuiTableColumnFlags.WidthFixed, previewSize.X);
                ImGui.TableSetupColumn("Order");
                ImGui.TableSetupColumn("Mood");
                ImGui.TableSetupColumn("Blocks");
                ImGui.TableHeadersRow();
                for (int i = 0; i < entries.Count; i++)
                {
                    LevelEntry entry = entries[i];
                    ImGui.TableNextRow(ImGuiTableRowFlags.None, rowHeight);
                    ImGui.TableNextColumn();
                    ImGuiSelectableFlags rowFlags = ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowDoubleClick
                        | (entry.Readable ? ImGuiSelectableFlags.None : ImGuiSelectableFlags.Disabled);
                    if (ImGui.Selectable(entry.Name + "##" + i, i == selected, rowFlags, new Vector2(0f, rowHeight)))
                    {
                        selected = i;
                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                            chosen = entry.File;
                    }
                    if (i == selected && scrollToSelected)
                    {
                        ImGui.SetScrollHereY();
                        scrollToSelected = false;
                    }
                    ImGui.TableNextColumn();
                    if (previews.TryGetValue(entry.Name, out (Texture2D Texture, IntPtr Id) preview))
                        ImGui.Image(preview.Id, previewSize);
                    ImGui.TableNextColumn();
                    if (entry.Readable)
                    {
                        ImGui.Text(entry.Order.HasValue ? entry.Order.Value.ToString() : "-");
                        ImGui.TableNextColumn();
                        ImGui.Text(entry.Mood.ToString());
                        ImGui.TableNextColumn();
                        ImGui.Text(entry.Blocks.ToString());
                    }
                    else
                    {
                        //In the Order cell, the first of what the file would say; Mood and Blocks stay empty
                        ImGui.TextColored(ErrorColor, "unreadable");
                        ImGui.SetItemTooltip(entry.Error);
                    }
                }
                ImGui.EndTable();
            }
            if (listError != null)
                ImGui.TextColored(ErrorColor, "The folder cannot be read: " + listError);
            else if (entries.Count == 0)
                ImGui.TextDisabled("The folder has no levels.");

            ImGui.Spacing();
            ImGui.TextDisabled("Any level file (also an old .lev):");
            ImGui.SetNextItemWidth(-(ImGui.CalcTextSize("Open file").X + ImGui.GetStyle().FramePadding.X * 2f + ImGui.GetStyle().ItemSpacing.X));
            bool pathEntered = ImGui.InputTextWithHint("##path", "A path, or a name of the folder", ref path, PathLength, ImGuiInputTextFlags.EnterReturnsTrue);
            ImGui.SameLine();
            if ((ImGui.Button("Open file") || pathEntered) && path.Trim().Length > 0)
            {
                string file = folder.Resolve(path.Trim().Trim('"'));
                if (File.Exists(file))
                    chosen = file;
                else
                    pathError = "There is no file " + file + ".";
            }
            if (pathError != null)
                ImGui.TextColored(ErrorColor, pathError);

            ImGui.Separator();
            bool canOpen = selected >= 0 && selected < entries.Count && entries[selected].Readable;
            ImGui.BeginDisabled(!canOpen);
            if (ImGui.Button("Open") || canOpen && keys && !typing && !pathEntered && EnterPressed())
                chosen = entries[selected].File;
            ImGui.EndDisabled();
            ImGui.SameLine();
            if (ImGui.Button("Cancel") || keys && ImGui.IsKeyPressed(ImGuiKey.Escape, false))
            {
                Cancel();
                return;
            }

            if (chosen != null)
            {
                Close();
                open(chosen);
            }
        }

        /// <summary>Moves the selection of the Open dialog by the keys, over the files that cannot be read.</summary>
        private void Select(int index, int direction)
        {
            while (index >= 0 && index < entries.Count && !entries[index].Readable)
                index += direction;
            if (index < 0 || index >= entries.Count)
                return;
            selected = index;
            scrollToSelected = true;
        }

        private void DrawSaveAs(bool keys)
        {
            bool replacing = replaceFile != null;
            ImGui.TextDisabled("Folder:");
            ImGui.SameLine();
            ImGui.Text(folder.FullPath);
            ImGui.Text("Name:");
            ImGui.SameLine();
            if (ImGui.IsWindowAppearing() || focusName && !replacing)
            {
                ImGui.SetKeyboardFocusHere();
                focusName = false;
            }
            ImGui.SetNextItemWidth(ImGui.GetFontSize() * 18f);
            bool submit = ImGui.InputText("##name", ref name, NameLength, ImGuiInputTextFlags.EnterReturnsTrue);
            bool fieldActive = ImGui.IsItemActive() || ImGui.IsItemDeactivated();
            ImGui.SameLine();
            ImGui.TextDisabled(LevelFile.Extension);
            if (nameError != null)
                ImGui.TextColored(ErrorColor, nameError);

            ImGui.Separator();
            submit |= ImGui.Button("Save");
            submit |= keys && !replacing && !fieldActive && EnterPressed();
            ImGui.SameLine();
            if (ImGui.Button("Cancel") || keys && !replacing && ImGui.IsKeyPressed(ImGuiKey.Escape, false))
            {
                Cancel();
                return;
            }

            if (submit && !replacing)
            {
                nameError = LevelFolder.CheckName(name);
                if (nameError != null)
                    focusName = true;
                else
                {
                    string file = folder.FileOf(LevelFolder.NormalName(name));
                    if (File.Exists(file))
                    {
                        replaceFile = file;
                        ImGui.OpenPopup(ReplaceTitle);
                    }
                    else
                    {
                        Save(file);
                        return;
                    }
                }
            }

            //"Replace?" is a popup of Save as: opened and begun inside it, so that both are in the same ID stack
            if (replaceFile != null)
                DrawReplace();
        }

        private void DrawReplace()
        {
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            if (!ImGui.BeginPopupModal(ReplaceTitle, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
            {
                replaceFile = null;
                return;
            }
            bool keys = !ImGui.IsWindowAppearing();
            ImGui.Text("Replace " + Path.GetFileName(replaceFile) + "?");
            ImGui.TextDisabled("The file exists in the folder; it will be overwritten.");
            ImGui.Separator();
            bool replace = ImGui.Button("Replace") || keys && EnterPressed();
            ImGui.SameLine();
            bool cancel = ImGui.Button("Cancel") || keys && ImGui.IsKeyPressed(ImGuiKey.Escape, false);
            if (replace || cancel)
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();

            if (replace)
            {
                string file = replaceFile;
                replaceFile = null;
                Save(file);
            }
            else if (cancel)
            {
                replaceFile = null;
                focusName = true;
            }
        }

        /// <summary>Save as ends with a file: the dialog closes and the level is saved (a failure is the screen's to tell).</summary>
        private void Save(string file)
        {
            Close();
            saveAs(file);
        }

        private void DrawSaveChanges(bool keys)
        {
            ImGui.Text("Save changes to " + levelName + "?");
            ImGui.TextDisabled("Enter: Save    D: Don't save    Esc: Cancel");
            ImGui.Separator();
            SaveChangesAnswer? chosen = null;
            if (ImGui.Button("Save") || keys && EnterPressed())
                chosen = SaveChangesAnswer.Save;
            ImGui.SameLine();
            if (ImGui.Button("Don't save") || keys && ImGui.IsKeyPressed(ImGuiKey.D, false))
                chosen = SaveChangesAnswer.DontSave;
            ImGui.SameLine();
            if (ImGui.Button("Cancel") || keys && ImGui.IsKeyPressed(ImGuiKey.Escape, false))
                chosen = SaveChangesAnswer.Cancel;
            if (!chosen.HasValue)
                return;
            Close();
            answer(chosen.Value);
        }

        private static bool EnterPressed()
        {
            return ImGui.IsKeyPressed(ImGuiKey.Enter, false) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter, false);
        }

        /// <summary>Releases the previews of an Open dialog still shown.</summary>
        public void Dispose()
        {
            Ended(dialog);
            dialog = Dialog.None;
            ReleasePreviews();
        }
    }
}
