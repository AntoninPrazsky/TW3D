using System;
using System.Collections.Generic;

namespace TW3D
{
    /// <summary>
    /// The colours chosen for a car in the garage, by the keys of the palette (<see cref="PaintColours"/>): its primary and
    /// secondary paint and the rims of the wheels of its front and rear axle. Null, or a key the palette does not have,
    /// is the original colour (the colour of the material in the model).
    /// </summary>
    public sealed class CarColours
    {
        public string Primary { get; set; }

        public string Secondary { get; set; }

        public string FrontRims { get; set; }

        public string RearRims { get; set; }

        public CarColours Clone() => (CarColours)MemberwiseClone();
    }

    /// <summary>A colour of the palette of the garage: its key in the settings, its name and its colour (sRGB, 0–1).</summary>
    public sealed class PaintColour
    {
        public PaintColour(string key, string name, float r, float g, float b)
        {
            Key = key;
            Name = name;
            R = r;
            G = g;
            B = b;
        }

        /// <summary>The key in the settings; null for <see cref="PaintColours.Original"/>.</summary>
        public string Key { get; }

        public string Name { get; }

        public float R { get; }

        public float G { get; }

        public float B { get; }

        /// <summary>The colour of the material in the model, which has no colour of its own here.</summary>
        public bool IsOriginal => Key == null;
    }

    /// <summary>
    /// The palette of the garage: <see cref="Original"/> first (the car as it was made), then twelve colours that read well
    /// against the sky and the track. The same for the paint and the rims; as the colours of the models, the colours are
    /// those picked on the screen (sRGB), and the material keeps its own metal and roughness (shiny paint, chrome rims).
    /// </summary>
    public static class PaintColours
    {
        public static readonly PaintColour Original = new PaintColour(null, "Original", 0f, 0f, 0f);

        public static readonly IReadOnlyList<PaintColour> All = new[]
        {
            Original,
            new PaintColour("red", "Red", 0.80f, 0.04f, 0.04f),
            new PaintColour("orange", "Orange", 1.00f, 0.40f, 0.00f),
            new PaintColour("yellow", "Yellow", 1.00f, 0.78f, 0.00f),
            new PaintColour("lime", "Lime", 0.55f, 0.85f, 0.05f),
            new PaintColour("green", "Green", 0.04f, 0.50f, 0.16f),
            new PaintColour("teal", "Teal", 0.00f, 0.62f, 0.64f),
            new PaintColour("blue", "Blue", 0.02f, 0.24f, 0.80f),
            new PaintColour("purple", "Purple", 0.40f, 0.10f, 0.70f),
            new PaintColour("pink", "Pink", 1.00f, 0.36f, 0.66f),
            new PaintColour("white", "White", 0.92f, 0.92f, 0.92f),
            new PaintColour("silver", "Silver", 0.62f, 0.64f, 0.66f),
            new PaintColour("black", "Black", 0.05f, 0.05f, 0.055f)
        };

        /// <summary>The colour of a key (in any case); <see cref="Original"/> for null or a key the palette does not have.</summary>
        public static PaintColour Find(string key)
        {
            if (key != null)
                foreach (PaintColour colour in All)
                    if (string.Equals(colour.Key, key, StringComparison.OrdinalIgnoreCase))
                        return colour;
            return Original;
        }

        /// <summary>The key of the colour after (direction 1) or before (-1) a colour in the palette, round the ends.</summary>
        public static string Step(string key, int direction)
        {
            int index = 0;
            for (int i = 0; i < All.Count; i++)
                if (All[i] == Find(key))
                    index = i;
            return All[((index + direction) % All.Count + All.Count) % All.Count].Key;
        }
    }
}
