using Microsoft.Xna.Framework;

namespace TW3D.Graphics
{
    /// <summary>
    /// The mud a model of the car is drawn with (#60, see <see cref="SceneRenderer.Add"/> and Effects/Mud.fxh): how much of
    /// it there is, for the body of the car where its axles are, around which it gathers first, and for a wheel how much
    /// is on the tread of its tyre (which the road wipes first). A wheel or the holder of an axle gets it all over. The
    /// default is clean.
    /// </summary>
    public readonly struct Mud
    {
        /// <param name="amount">0 clean, 1 as muddy as it gets.</param>
        /// <param name="axles">The body of a car: the middles of its front and its rear axle in its model's own coordinates
        /// (front x, y, rear x, y); null for a wheel or a holder.</param>
        /// <param name="tread">A wheel: the mud on the tread of its tyre, 0 clean to 1; null as much as the rest of it.</param>
        /// <param name="wet">How wet the mud is, 0 dry (light, matt) to 1 fresh (dark, a little glossy).</param>
        public Mud(float amount, Vector4? axles = null, float? tread = null, float wet = 0f)
        {
            Amount = MathHelper.Clamp(amount, 0f, 1f);
            Axles = axles;
            Tread = MathHelper.Clamp(tread ?? amount, 0f, 1f);
            Wet = MathHelper.Clamp(wet, 0f, 1f);
        }

        /// <summary>0 clean, 1 as muddy as it gets.</summary>
        public float Amount { get; }

        /// <summary>The middles of the axles of the body in its model's own coordinates; null for a wheel or a holder.</summary>
        public Vector4? Axles { get; }

        /// <summary>The mud on the tread of the tyre of a wheel, 0 clean to 1.</summary>
        public float Tread { get; }

        /// <summary>How wet the mud is, 0 dry to 1 fresh.</summary>
        public float Wet { get; }

        public bool IsClean => Amount <= 0f && Tread <= 0f;
    }
}
