using Microsoft.Xna.Framework;

namespace TW3D.Graphics
{
    /// <summary>
    /// The mud a model of the car is drawn with (#60, see <see cref="SceneRenderer.Add"/> and Effects/Mud.fxh): how much of
    /// it there is, and for the body of the car where its axles are, around which it gathers first. A wheel or the holder of
    /// an axle gets it all over. The default is clean.
    /// </summary>
    public readonly struct Mud
    {
        /// <param name="amount">0 clean, 1 as muddy as it gets.</param>
        /// <param name="axles">The body of a car: the middles of its front and its rear axle in its model's own coordinates
        /// (front x, y, rear x, y); null for a wheel or a holder.</param>
        public Mud(float amount, Vector4? axles = null)
        {
            Amount = MathHelper.Clamp(amount, 0f, 1f);
            Axles = axles;
        }

        /// <summary>0 clean, 1 as muddy as it gets.</summary>
        public float Amount { get; }

        /// <summary>The middles of the axles of the body in its model's own coordinates; null for a wheel or a holder.</summary>
        public Vector4? Axles { get; }

        public bool IsClean => Amount <= 0f;
    }
}
