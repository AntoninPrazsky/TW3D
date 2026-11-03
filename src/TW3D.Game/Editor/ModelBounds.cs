using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TW3D.Editor
{
    /// <summary>
    /// The bounding sphere of a whole model: the previews of the palette fit it in their cells, the Object tool hits the
    /// dynamic objects by it.
    /// </summary>
    static class ModelBounds
    {
        /// <summary>The bounding spheres of the meshes of a model, moved by their bone transforms, merged; in the units of the model.</summary>
        /// <param name="boneTransforms">The absolute bone transforms of the model (<see cref="Model.CopyAbsoluteBoneTransformsTo"/>).</param>
        public static BoundingSphere Of(Model model, Matrix[] boneTransforms)
        {
            BoundingSphere merged = new BoundingSphere();
            bool first = true;
            foreach (ModelMesh mesh in model.Meshes)
            {
                BoundingSphere bounds = mesh.BoundingSphere.Transform(boneTransforms[mesh.ParentBone.Index]);
                merged = first ? bounds : BoundingSphere.CreateMerged(merged, bounds);
                first = false;
            }
            return merged;
        }

        /// <summary>The merged bounding sphere of a model whose bone transforms are not needed otherwise.</summary>
        public static BoundingSphere Of(Model model)
        {
            Matrix[] boneTransforms = new Matrix[model.Bones.Count];
            model.CopyAbsoluteBoneTransformsTo(boneTransforms);
            return Of(model, boneTransforms);
        }
    }
}
