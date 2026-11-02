namespace TW3D.Editor
{
    /// <summary>The dynamic object chosen in the palette, the one the Object tool will place: its type and the model of the type.</summary>
    sealed class ChosenObject
    {
        public ChosenObject(string type, string model)
        {
            Type = type;
            Model = model;
        }

        /// <summary>The type of the object in the catalog (e.g. "Bar").</summary>
        public string Type { get; }

        /// <summary>One of the models of the type (e.g. "bar1").</summary>
        public string Model { get; }
    }
}
