using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TW3D.Input;

namespace TW3D.Editor
{
    /// <summary>The tools of the editor; the active one gets the keys and the mouse of the scene.</summary>
    enum EditorTool
    {
        /// <summary>Blocks in the cells of the active layer (<see cref="BlockTool"/>).</summary>
        Place,

        /// <summary>The dynamic objects of the middle plane (<see cref="ObjectTool"/>).</summary>
        Object,

        /// <summary>
        /// Many blocks of the active layer at once, with the props of the scenery standing in the layer: selecting, moving,
        /// copying, mirroring (<see cref="SelectTool"/>).
        /// </summary>
        Select,

        /// <summary>The props of the scenery beside the track, at any depth (<see cref="SceneryTool"/>).</summary>
        Scenery
    }

    /// <summary>The keys that choose the tools.</summary>
    static class EditorToolKeys
    {
        /// <summary>The key of each tool by the <see cref="EditorTool"/> enum; the View > Tool menu shows them, the screen reads them.</summary>
        public static readonly Keys[] ByTool = { Keys.B, Keys.O, Keys.V, Keys.P };
    }

    /// <summary>What the screen asks of the active tool: the keys and the mouse of the scene, Esc, and the end of a drag.</summary>
    interface IEditorTool
    {
        /// <summary>The keys; called when the interface does not want the keyboard.</summary>
        /// <param name="layer">The active layer.</param>
        void HandleKeys(InputState input, Layer layer);

        /// <summary>The mouse; called after the camera has had it, so that a drag of the camera takes precedence.</summary>
        /// <param name="layer">The active layer.</param>
        /// <param name="free">The cursor is over the scene, not over the user interface.</param>
        void HandleMouse(InputState input, Viewport viewport, Layer layer, bool free);

        /// <summary>Esc: drops what the tool holds (a selection); false when it held nothing, and Esc leaves the editor.</summary>
        bool Cancel();

        /// <summary>Ends a drag of the mouse, closing its step of undo; nothing when there is none.</summary>
        void EndStroke();
    }
}
