using System;

namespace TW3D.Graphics
{
    /// <summary>The detail of the shadows (the Shadow detail row of the Options menu): the size of the shadow map.</summary>
    public enum ShadowDetail
    {
        /// <summary>A shadow map of 1024 texels, the default on a Raspberry Pi.</summary>
        Low,
        /// <summary>2048 texels: the shadow map of the game before the choice came (2026-11-04), the default on a phone.</summary>
        Medium,
        /// <summary>4096 texels, the default on a desktop. Not on a Raspberry Pi (see <see cref="ShadowDetails.Highest"/>).</summary>
        High
    }

    /// <summary>
    /// The sizes of the shadow maps of each <see cref="ShadowDetail"/>, the default one, the highest one and the steps of
    /// the Options row. Pure arithmetic, tested without a window.
    /// </summary>
    public static class ShadowDetails
    {
        //The deep shadow map (the deeper back layers, far from the camera) is half the size of the shadow map, but no
        //smaller than it always was
        private const int MinDeepMapSize = 1024;

        /// <summary>
        /// The detail the shadows have until another is chosen: high on a desktop, medium on a phone (the shadow map it
        /// always had) and low on a Raspberry Pi, whose GPU has little to spare (see <see cref="PiGraphics"/>).
        /// </summary>
        public static ShadowDetail Default { get; } =
#if ANDROID
            ShadowDetail.Medium;
#else
            PiGraphics.Active ? ShadowDetail.Low : ShadowDetail.High;
#endif

        /// <summary>
        /// The highest detail the GPU can hold: high, but medium on a Raspberry Pi, whose GPU has no texture wider than
        /// 4096 texels (GL_MAX_TEXTURE_SIZE of V3D), and the shadow maps of high lie in one 6144 × 4096.
        /// </summary>
        public static ShadowDetail Highest { get; } = PiGraphics.Active ? ShadowDetail.Medium : ShadowDetail.High;

        /// <summary>
        /// A detail the game knows and the GPU can hold: an unknown one (a settings file edited by hand) is the default, one
        /// above <see cref="Highest"/> (a settings file brought from a desktop to a Raspberry Pi) the highest.
        /// </summary>
        public static ShadowDetail Valid(ShadowDetail detail)
        {
            return !Enum.IsDefined(detail) ? Default : detail > Highest ? Highest : detail;
        }

        /// <summary>The size of the shadow map (texels on a side), of any detail the game knows (the renderer asks for a valid one).</summary>
        public static int MapSize(ShadowDetail detail)
        {
            return 1024 << (int)(Enum.IsDefined(detail) ? detail : Default);
        }

        /// <summary>The size of the deep shadow map (texels on a side), which lies beside the shadow map in one texture.</summary>
        public static int DeepMapSize(ShadowDetail detail)
        {
            return Math.Max(MinDeepMapSize, MapSize(detail) / 2);
        }

        /// <summary>The next detail up, round to the lowest after the highest (Enter on the row).</summary>
        public static ShadowDetail Next(ShadowDetail detail)
        {
            return detail >= Highest ? ShadowDetail.Low : Valid(detail) + 1;
        }

        /// <summary>The detail a step down (-1) or up (1) from another, no lower than low nor higher than the highest (left and right on the row).</summary>
        public static ShadowDetail Step(ShadowDetail detail, int step)
        {
            return (ShadowDetail)Math.Clamp((int)Valid(detail) + step, (int)ShadowDetail.Low, (int)Highest);
        }

        /// <summary>The name of a detail on the Options row.</summary>
        public static string Name(ShadowDetail detail)
        {
            return Valid(detail).ToString();
        }
    }
}
