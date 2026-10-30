namespace TW3D.Shared
{
    /// <summary>
    /// The three systems of coordinates of the game and how they relate:
    /// <list type="bullet">
    /// <item><description><b>Model units</b>: the coordinates of the 3D models (the .obj files). A track block is
    /// 9 units wide and high, the track lies in the plane z = 0.</description></item>
    /// <item><description><b>Physics units</b>: the coordinates of the physics simulation and of the level files,
    /// with y pointing down. A model unit is 2.5 physics units, so a block is 22.5 units: for the physics engine the
    /// world is huge (the car is about 20 metres long), which is why the game runs faster than real time.</description></item>
    /// <item><description><b>Scene units</b>: the coordinates the models are drawn in. A model is drawn at a quarter
    /// of its size, and a physics point (x, y) is drawn at (-x, -y) / 10: the physics world is turned around, so
    /// its y pointing down becomes the y of the scene pointing up.</description></item>
    /// </list>
    /// </summary>
    public static class Units
    {
        /// <summary>A model is drawn at this part of its size.</summary>
        public const float ModelToScene = 0.25f;

        /// <summary>
        /// A physics coordinate is drawn at this part of its value, turned around: the position (x, y) of a body
        /// is drawn at (-x, -y) × <see cref="PhysicsToScene"/>.
        /// </summary>
        public const float PhysicsToScene = 0.1f;

        /// <summary>
        /// One model unit in physics units (<see cref="ModelToScene"/> / <see cref="PhysicsToScene"/>): negative,
        /// as the physics world is turned around relative to the scene.
        /// </summary>
        public const float ModelToPhysics = -2.5f;

        /// <summary>A track block is this wide and high, in model units.</summary>
        public const float BlockModelSize = 9f;

        /// <summary>One cell of the level grid (the size of a block) in physics units: 9 model units.</summary>
        public const float GridCell = 22.5f;

        /// <summary>One step of the vertical shift of a block, in physics units: a ninth of a cell, one model unit.</summary>
        public const float ShiftStep = 2.5f;
    }
}
