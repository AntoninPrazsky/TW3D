using System.Collections.Generic;
using System.Linq;
using TW3D.Shared;

namespace TW3D.Editor
{
    /// <summary>
    /// The styles of the track as the editor offers them (#58, see <see cref="Catalog.Styles"/>): their names in the
    /// windows and the menus, and the choices for the level and for a block.
    /// </summary>
    static class StyleNames
    {
        /// <summary>The name of a style in the editor: "Off-road", "Classic", a transition "Off-road to classic"; "The level's" for none.</summary>
        public static string Name(string style)
        {
            if (style == null)
                return "The level's";
            int dash = style.IndexOf('-');
            if (dash > 0)
                return Name(style.Substring(0, dash)) + " to " + Name(style.Substring(dash + 1)).ToLowerInvariant();
            return style switch
            {
                Catalog.ClassicStyle => "Classic",
                "offroad" => "Off-road",
                _ => style.Length > 0 ? char.ToUpperInvariant(style[0]) + style.Substring(1) : style
            };
        }

        /// <summary>The styles a level can have: the classic one (null in the file) and those of the catalog.</summary>
        public static IEnumerable<string> LevelStyles(Catalog catalog)
        {
            return new string[] { null }.Concat(catalog.Styles);
        }

        /// <summary>
        /// The styles a block can have: the level's (none of its own), the classic one, those of the catalog, and the
        /// transitions between each two of them (left to right as the block is placed unturned).
        /// </summary>
        public static IEnumerable<string> BlockStyles(Catalog catalog)
        {
            List<string> styles = new List<string> { Catalog.ClassicStyle };
            styles.AddRange(catalog.Styles);
            yield return null;
            foreach (string style in styles)
                yield return style;
            foreach (string left in styles)
                foreach (string right in styles)
                    if (left != right)
                        yield return left + "-" + right;
        }
    }
}
