using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;

namespace TW3D.ContentPipeline
{
    /// <summary>
    /// The model processor of MonoGame, keeping what the importer leaves out of the materials of an OBJ model:
    /// how metallic and how rough they are, and which of them are lamps. The importer reads only what
    /// BasicEffect has (the colours and the opacity); this processor reads the MTL file of the model: the PBR
    /// extension, "Pm" (metallic, 0–1) and "Pr" (roughness, 0–1) of a material, and the name of the material:
    /// the lenses of the headlights of a car are named "headlight…", of the rear lights "taillight…", of a street
    /// lamp "streetlight…", the red start lights of the start gate "signalred…", the lit windows of a building
    /// "windowlit…", and the parts that can be painted: the paint of a car "primary…" and "secondary…", the rims of a wheel
    /// "rim…". It keeps them in the Tag of the mesh parts of the material as a Vector4: metallic, roughness, lamp
    /// (0 = none, 1 = headlight, 2 = rear light, 3 = street lamp, 4 = start light, 5 = lit window) and paint (0 = none,
    /// 1 = primary, 2 = secondary, 3 = rim). A material with none of them gets no tag.
    /// </summary>
    [ContentProcessor(DisplayName = "Model - TW3D (metal and lamps from MTL)")]
    public class MtlModelProcessor : ModelProcessor
    {
        //Of the materials of the MTL file, by name; then of the materials the base processor made of them
        private Dictionary<string, Vector4> finishes;
        private readonly Dictionary<MaterialContent, Vector4> convertedFinishes = new Dictionary<MaterialContent, Vector4>();

        /// <summary>
        /// Mirrors the model front to back (x to -x; the faces stay turned outwards). A wheel so mirrored turns its
        /// tread the other way round: the wheels on the near side of a car are mirrored, so that they turn their tread
        /// the same way as the wheels on the far side, which are the model turned around (see GameObjects/Car.cs).
        /// </summary>
        [DisplayName("Mirrored")]
        [DefaultValue(false)]
        public bool Mirrored { get; set; }

        public override ModelContent Process(NodeContent input, ContentProcessorContext context)
        {
            finishes = ReadFinishes(input.Identity?.SourceFilename, context);
            convertedFinishes.Clear();
            if (Mirrored)
                MeshHelper.TransformScene(input, Matrix.CreateScale(-1f, 1f, 1f));

            ModelContent model = base.Process(input, context);
            foreach (ModelMeshContent mesh in model.Meshes)
            {
                foreach (ModelMeshPartContent part in mesh.MeshParts)
                {
                    if (part.Material != null && convertedFinishes.TryGetValue(part.Material, out Vector4 finish))
                        part.Tag = finish;
                }
            }
            return model;
        }

        protected override MaterialContent ConvertMaterial(MaterialContent material, ContentProcessorContext context)
        {
            MaterialContent converted = base.ConvertMaterial(material, context);
            if (material.Name != null && finishes.TryGetValue(material.Name, out Vector4 finish))
                convertedFinishes[converted] = finish;
            return converted;
        }

        /// <summary>Reads the metal, the roughness, the lamps and the paint of the materials from the MTL files of an OBJ model.</summary>
        private static Dictionary<string, Vector4> ReadFinishes(string modelFile, ContentProcessorContext context)
        {
            Dictionary<string, Vector4> finishes = new Dictionary<string, Vector4>();
            if (modelFile == null || !string.Equals(Path.GetExtension(modelFile), ".obj", StringComparison.OrdinalIgnoreCase))
                return finishes;

            foreach (string line in File.ReadLines(modelFile))
            {
                if (!line.StartsWith("mtllib ", StringComparison.Ordinal))
                    continue;
                string materialFile = Path.Combine(Path.GetDirectoryName(modelFile), line.Substring("mtllib ".Length).Trim());
                if (!File.Exists(materialFile))
                    continue;
                //The model is built again when its materials change
                context.AddDependency(materialFile);
                ReadMaterials(materialFile, finishes);
            }
            return finishes;
        }

        private static void ReadMaterials(string materialFile, Dictionary<string, Vector4> finishes)
        {
            string name = null;
            float? metallic = null, roughness = null;

            void Finish()
            {
                if (name == null)
                    return;
                float lamp = name.StartsWith("headlight", StringComparison.OrdinalIgnoreCase) ? 1f
                    : name.StartsWith("taillight", StringComparison.OrdinalIgnoreCase) ? 2f
                    : name.StartsWith("streetlight", StringComparison.OrdinalIgnoreCase) ? 3f
                    : name.StartsWith("signalred", StringComparison.OrdinalIgnoreCase) ? 4f
                    : name.StartsWith("windowlit", StringComparison.OrdinalIgnoreCase) ? 5f : 0f;
                float paint = name.StartsWith("primary", StringComparison.OrdinalIgnoreCase) ? 1f
                    : name.StartsWith("secondary", StringComparison.OrdinalIgnoreCase) ? 2f
                    : name.StartsWith("rim", StringComparison.OrdinalIgnoreCase) ? 3f : 0f;
                //Neither metallic nor rough, as the materials without them are drawn
                if (metallic != null || roughness != null || lamp != 0f || paint != 0f)
                    finishes[name] = new Vector4(metallic ?? 0f, roughness ?? 1f, lamp, paint);
            }

            foreach (string line in File.ReadLines(materialFile))
            {
                string[] parts = line.Trim().Split((char[])null, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    continue;
                switch (parts[0])
                {
                    case "newmtl":
                        Finish();
                        name = parts[1].Trim();
                        metallic = null;
                        roughness = null;
                        break;
                    case "Pm":
                        metallic = float.Parse(parts[1], CultureInfo.InvariantCulture);
                        break;
                    case "Pr":
                        roughness = float.Parse(parts[1], CultureInfo.InvariantCulture);
                        break;
                }
            }
            Finish();
        }
    }
}
