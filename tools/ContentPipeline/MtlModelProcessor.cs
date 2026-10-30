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
    /// how metallic and how rough they are. The importer reads only what BasicEffect has (the colours and the
    /// opacity); this processor reads the PBR extension of the MTL file of the model, "Pm" (metallic, 0–1) and
    /// "Pr" (roughness, 0–1) of a material, and keeps them in the Tag of its mesh parts as a Vector2 (metallic,
    /// roughness). A material with neither gets no tag.
    /// </summary>
    [ContentProcessor(DisplayName = "Model - TW3D (metal from MTL)")]
    public class MtlModelProcessor : ModelProcessor
    {
        //Of the materials of the MTL file, by name; then of the materials the base processor made of them
        private Dictionary<string, Vector2> finishes;
        private readonly Dictionary<MaterialContent, Vector2> convertedFinishes = new Dictionary<MaterialContent, Vector2>();

        public override ModelContent Process(NodeContent input, ContentProcessorContext context)
        {
            finishes = ReadFinishes(input.Identity?.SourceFilename, context);
            convertedFinishes.Clear();

            ModelContent model = base.Process(input, context);
            foreach (ModelMeshContent mesh in model.Meshes)
            {
                foreach (ModelMeshPartContent part in mesh.MeshParts)
                {
                    if (part.Material != null && convertedFinishes.TryGetValue(part.Material, out Vector2 finish))
                        part.Tag = finish;
                }
            }
            return model;
        }

        protected override MaterialContent ConvertMaterial(MaterialContent material, ContentProcessorContext context)
        {
            MaterialContent converted = base.ConvertMaterial(material, context);
            if (material.Name != null && finishes.TryGetValue(material.Name, out Vector2 finish))
                convertedFinishes[converted] = finish;
            return converted;
        }

        /// <summary>Reads the metal and roughness of the materials from the MTL files of an OBJ model.</summary>
        private static Dictionary<string, Vector2> ReadFinishes(string modelFile, ContentProcessorContext context)
        {
            Dictionary<string, Vector2> finishes = new Dictionary<string, Vector2>();
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

        private static void ReadMaterials(string materialFile, Dictionary<string, Vector2> finishes)
        {
            string name = null;
            float? metallic = null, roughness = null;

            void Finish()
            {
                //Neither metallic nor rough, as the materials without them are drawn
                if (name != null && (metallic != null || roughness != null))
                    finishes[name] = new Vector2(metallic ?? 0f, roughness ?? 1f);
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
