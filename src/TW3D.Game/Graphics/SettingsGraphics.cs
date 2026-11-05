namespace TW3D.Graphics
{
    /// <summary>What the settings of the game set up in a renderer (kept apart from GameSettings, which has no MonoGame in it).</summary>
    public static class SettingsGraphics
    {
        /// <summary>Sets up a renderer by the settings.</summary>
        public static void Apply(this GameSettings settings, SceneRenderer renderer)
        {
            renderer.Classic = settings.Classic;
            renderer.Shadows = settings.Shadows;
            renderer.ShadowDetail = ShadowDetails.CanChoose ? ShadowDetails.Valid(settings.ShadowDetail) : ShadowDetails.Default;
            renderer.AmbientOcclusion = settings.AmbientOcclusion;
            renderer.Resolution = settings.Resolution;
        }
    }
}
