namespace TW3D.Graphics
{
    /// <summary>
    /// The surfaces the shader draws per pixel on a part of a model (Effects/Surfaces.fxh): its colour varies and its
    /// relief catches the light, from the noise of <see cref="SurfaceNoise"/>. A part is a surface when the name of its
    /// material in the MTL file begins with the name of the surface ("asphalt…", "earth…", "grass…", see tools/ContentPipeline);
    /// its colour (Kd) is the base colour of the surface. Every other part keeps the plain colour of its material.
    /// </summary>
    public enum Surface
    {
        None,
        Asphalt,
        Earth,
        Grass
    }
}
