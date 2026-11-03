using Microsoft.Xna.Framework;
using TW3D.Shared;

namespace TW3D.GameObjects
{
    /// <summary>
    /// Where a prop of the scenery (see <see cref="SceneryPlacement"/>) is drawn: its model turned by its quarter turns
    /// about the vertical axis, at the scale of the models, its foot at its position and depth. The game and the editor
    /// draw the scenery by it.
    /// </summary>
    static class SceneryProp
    {
        /// <summary>The world matrix of a prop.</summary>
        /// <param name="x">The position of the foot in physics units, as that of a dynamic object.</param>
        /// <param name="y">Physics y: it grows downward.</param>
        /// <param name="z">The depth in physics units: negative behind the road, positive in front of it.</param>
        /// <param name="turn">Quarter turns about the vertical axis.</param>
        public static Matrix World(float x, float y, float z, int turn)
        {
            //A physics point is drawn turned around, as the layers are (see Units.PhysicsToScene)
            return Matrix.CreateRotationY(turn * SceneryPlacement.QuarterTurn) * Matrix.CreateScale(Units.ModelToScene)
                * Matrix.CreateTranslation(-new Vector3(x, y, z) * Units.PhysicsToScene);
        }
    }
}
