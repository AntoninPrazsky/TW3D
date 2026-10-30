using System;
using System.Collections.Generic;
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
    /// how metallic and how rough they are, and which of them are the lamps of a car. The importer reads only what
    /// BasicEffect has (the colours and the opacity); this processor reads the MTL file of the model: the PBR
    /// extension, "Pm" (metallic, 0–1) and "Pr" (roughness, 0–1) of a material, and the name of the material:
    /// the lenses of the headlights are named "headlight…", of the rear lights "taillight…". It keeps them in the
    /// Tag of the mesh parts of the material as a Vector3: metallic, roughness, lamp (0 = none, 1 = headlight,
    /// 2 = rear light). A material with none of them gets no tag.
    /// </summary>
    [ContentProcessor(DisplayName = "Model - TW3D (metal and lamps from MTL)")]
    public class MtlModelProcessor : ModelProcessor
    {
        //Of the materials of the MTL file, by name; then of the materials the base processor made of them
        private Dictionary<string, Vector3> finishes;
        private readonly Dictionary<MaterialContent, Vector3> convertedFinishes = new Dictionary<MaterialContent, Vector3>();

        public override ModelContent Process(NodeContent input, ContentProcessorContext context)
        {
            finishes = ReadFinishes(input.Identity?.SourceFilename, context);
            convertedFinishes.Clear();

            ModelContent model = base.Process(input, context);
            foreach (ModelMeshContent mesh in model.Meshes)
            {
                foreach (ModelMeshPartContent part in mesh.MeshParts)
                {
                    if (part.Material != null && convertedFinishes.TryGetValue(part.Material, out Vector3 finish))
                        part.Tag = finish;
                }
            }
            return model;
        }

        protected override MaterialContent ConvertMaterial(MaterialContent material, ContentProcessorContext context)
        {
            MaterialContent converted = base.ConvertMaterial(material, context);
            if (material.Name != null && finishes.TryGetValue(material.Name, out Vector3 finish))
                convertedFinishes[converted] = finish;
            return converted;
        }

        /// <summary>Reads the metal, the roughness and the lamps of the materials from the MTL files of an OBJ model.</summary>
        private static Dictionary<string, Vector3> ReadFinishes(string modelFile, ContentProcessorContext context)
        {
            Dictionary<string, Vector3> finishes = new Dictionary<string, Vector3>();
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

        private static void ReadMaterials(string materialFile, Dictionary<string, Vector3> finishes)
        {
            string name = null;
            float? metallic = null, roughness = null;

            void Finish()
            {
                if (name == null)
                    return;
                float lamp = name.StartsWith("headlight", StringComparison.OrdinalIgnoreCase) ? 1f
                    : name.StartsWith("taillight", StringComparison.OrdinalIgnoreCase) ? 2f : 0f;
                //Neither metallic nor rough, as the materials without them are drawn
                if (metallic != null || roughness != null || lamp != 0f)
                    finishes[name] = new Vector3(metallic ?? 0f, roughness ?? 1f, lamp);
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
