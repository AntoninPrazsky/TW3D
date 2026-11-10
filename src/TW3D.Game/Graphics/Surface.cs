namespace TW3D.Graphics
{
    /// <summary>
    /// The surfaces the shader draws per pixel on a part of a model (Effects/Surfaces.fxh): its colour varies and its
    /// relief catches the light, from the noise of <see cref="SurfaceNoise"/>. A part is a surface when the name of its
    /// material in the MTL file begins with the name of the surface ("asphalt…", "earth…", "grass…", "meadow…", see
    /// tools/ContentPipeline); its colour (Kd) is the base colour of the surface. Every other part keeps the plain colour of
    /// its material.
    /// </summary>
    public enum Surface
    {
        None,
        Asphalt,
        Earth,
        /// <summary>The grass of the verge of the off-road road: grass towards the edges of its block, earth in the middle.</summary>
        Grass,
        /// <summary>Grass all over (the ground and the hills of the backdrop, #66), the look of <see cref="Grass"/> without a verge.</summary>
        Meadow
    }
}
