using System;

namespace TW3D.Graphics
{
    /// <summary>
    /// The detail of the surfaces of the blocks (the Surface detail row of the Options menu, see <see cref="Surface"/>):
    /// how much of their noise the shader reads and how much it computes from it.
    /// </summary>
    public enum SurfaceDetail
    {
        /// <summary>A plain colour with patches of a little lighter and darker, no relief: one lookup of the noise. The one of a Raspberry Pi.</summary>
        Low,
        /// <summary>The whole surface (its stones, pebbles and relief): two lookups. The default on a phone.</summary>
        Medium,
        /// <summary>As Medium, with a third lookup that hides how the fine pattern repeats. The default on a desktop.</summary>
        High
    }

    /// <summary>The default <see cref="SurfaceDetail"/> and the steps of the Options row. Pure arithmetic, tested without a window.</summary>
    public static class SurfaceDetails
    {
        /// <summary>
        /// The detail the surfaces have until another is chosen: high on a desktop, medium on a phone and low on a
        /// Raspberry Pi, whose GPU has far too little for more (see <see cref="PiGraphics"/>).
        /// </summary>
        public static SurfaceDetail Default { get; } =
#if ANDROID
            SurfaceDetail.Medium;
#else
            PiGraphics.Active ? SurfaceDetail.Low : SurfaceDetail.High;
#endif

        /// <summary>The detail can be chosen: everywhere but on a Raspberry Pi, which keeps the low one.</summary>
        public static bool CanChoose => !PiGraphics.Active;

        /// <summary>A detail the game knows: an unknown one (a settings file edited by hand) is the default.</summary>
        public static SurfaceDetail Valid(SurfaceDetail detail)
        {
            return Enum.IsDefined(detail) ? detail : Default;
        }

        /// <summary>The next detail up, round to the lowest (Enter on the row).</summary>
        public static SurfaceDetail Next(SurfaceDetail detail)
        {
            return detail >= SurfaceDetail.High ? SurfaceDetail.Low : Valid(detail) + 1;
        }

        /// <summary>The detail a step down (-1) or up (1) from another, no lower than low nor higher than high (left and right on the row).</summary>
        public static SurfaceDetail Step(SurfaceDetail detail, int step)
        {
            return (SurfaceDetail)Math.Clamp((int)Valid(detail) + step, (int)SurfaceDetail.Low, (int)SurfaceDetail.High);
        }

        /// <summary>The name of a detail on the Options row.</summary>
        public static string Name(SurfaceDetail detail)
        {
            return Valid(detail).ToString();
        }
    }
}
