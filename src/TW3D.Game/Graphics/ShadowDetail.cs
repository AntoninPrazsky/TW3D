using System;

namespace TW3D.Graphics
{
    /// <summary>The detail of the shadows (the Shadow detail row of the Options menu): the size of the shadow map.</summary>
    public enum ShadowDetail
    {
        /// <summary>A shadow map of 1024 texels, the one of a Raspberry Pi.</summary>
        Low,
        /// <summary>2048 texels: the shadow map of the game before the choice came (2026-11-04), the default on a phone.</summary>
        Medium,
        /// <summary>4096 texels, the default on a desktop.</summary>
        High
    }

    /// <summary>
    /// The sizes of the shadow maps of each <see cref="ShadowDetail"/>, the default one and the steps of the Options row.
    /// Pure arithmetic, tested without a window.
    /// </summary>
    public static class ShadowDetails
    {
        //The deep shadow map (the deeper back layers, far from the camera) is half the size of the shadow map, but no
        //smaller than it always was
        private const int MinDeepMapSize = 1024;

        /// <summary>
        /// The detail the shadows have until another is chosen: high on a desktop, medium on a phone (the shadow map it
        /// always had) and low on a Raspberry Pi, whose GPU has far too little for more (see <see cref="PiGraphics"/>).
        /// </summary>
        public static ShadowDetail Default { get; } =
#if ANDROID
            ShadowDetail.Medium;
#else
            PiGraphics.Active ? ShadowDetail.Low : ShadowDetail.High;
#endif

        /// <summary>The detail can be chosen: everywhere but on a Raspberry Pi, which keeps the low one.</summary>
        public static bool CanChoose => !PiGraphics.Active;

        /// <summary>A detail the game knows: an unknown one (a settings file edited by hand) is the default.</summary>
        public static ShadowDetail Valid(ShadowDetail detail)
        {
            return Enum.IsDefined(detail) ? detail : Default;
        }

        /// <summary>The size of the shadow map (texels on a side).</summary>
        public static int MapSize(ShadowDetail detail)
        {
            return 1024 << (int)Valid(detail);
        }

        /// <summary>The size of the deep shadow map (texels on a side), which lies beside the shadow map in one texture.</summary>
        public static int DeepMapSize(ShadowDetail detail)
        {
            return Math.Max(MinDeepMapSize, MapSize(detail) / 2);
        }

        /// <summary>The next detail up, round to the lowest (Enter on the row).</summary>
        public static ShadowDetail Next(ShadowDetail detail)
        {
            return detail >= ShadowDetail.High ? ShadowDetail.Low : Valid(detail) + 1;
        }

        /// <summary>The detail a step down (-1) or up (1) from another, no lower than low nor higher than high (left and right on the row).</summary>
        public static ShadowDetail Step(ShadowDetail detail, int step)
        {
            return (ShadowDetail)Math.Clamp((int)Valid(detail) + step, (int)ShadowDetail.Low, (int)ShadowDetail.High);
        }

        /// <summary>The name of a detail on the Options row.</summary>
        public static string Name(ShadowDetail detail)
        {
            return Valid(detail).ToString();
        }
    }
}
