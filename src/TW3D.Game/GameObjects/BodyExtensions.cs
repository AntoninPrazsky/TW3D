using nkast.Aether.Physics2D.Dynamics;

namespace TW3D.GameObjects
{
    /// <summary>
    /// Sets fixture properties on all fixtures of a body at once.
    /// Farseer Physics had these as Body properties, Aether.Physics2D only has them on Fixture.
    /// </summary>
    static class BodyExtensions
    {
        public static void SetFriction(this Body body, float friction)
        {
            foreach (Fixture fixture in body.FixtureList)
                fixture.Friction = friction;
        }

        public static void SetRestitution(this Body body, float restitution)
        {
            foreach (Fixture fixture in body.FixtureList)
                fixture.Restitution = restitution;
        }

        public static void SetCollisionCategories(this Body body, Category categories)
        {
            foreach (Fixture fixture in body.FixtureList)
                fixture.CollisionCategories = categories;
        }

        public static void SetCollidesWith(this Body body, Category categories)
        {
            foreach (Fixture fixture in body.FixtureList)
                fixture.CollidesWith = categories;
        }
    }
}
