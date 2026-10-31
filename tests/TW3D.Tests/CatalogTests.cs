using System.Collections.Generic;
using System.IO;
using System.Linq;
using TW3D.Shared;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>
    /// The catalog (content/catalog.json) against the content: every model it names exists and is built by the
    /// content pipeline, and every model the physics uses has its collision shape (content/collision.json).
    /// </summary>
    public class CatalogTests
    {
        private readonly Catalog catalog = Repository.LoadCatalog();

        /// <summary>Every model the catalog names, by its content name (e.g. "Levels/mod/a1").</summary>
        private IEnumerable<string> Models()
        {
            return catalog.Blocks.Select(block => block.ModelAsset)
                .Concat(catalog.ObjectModels.Select(ObjectType.ModelAsset))
                .Concat(catalog.Cars.Select(car => car.Model))
                .Concat(catalog.Wheels.Select(wheel => wheel.Model))
                .Append("holder");
        }

        [Fact]
        public void EveryModelExistsAndIsBuilt()
        {
            HashSet<string> built = Repository.BuiltContent();
            List<string> missing = Models()
                .Where(model => !File.Exists(Path.Combine(Repository.Content, model + ".obj")) || !built.Contains(model + ".obj"))
                .ToList();
            Assert.Empty(missing);
        }

        [Fact]
        public void EveryModelThePhysicsUsesHasACollisionShape()
        {
            CollisionShapes shapes = Repository.LoadCollisionShapes();
            IEnumerable<string> physical = catalog.Blocks.Where(block => block.Collision).Select(block => block.ModelAsset)
                .Concat(catalog.Objects.Where(type => type.Shape == ObjectShape.Model).SelectMany(type => type.Models).Select(ObjectType.ModelAsset))
                .Concat(catalog.Cars.Select(car => car.Model))
                .Append("holder");
            Assert.DoesNotContain(physical, model => shapes.Find(model) == null);
        }

        [Fact]
        public void EveryCarComesWithWheelsOfTheCatalog()
        {
            Assert.All(catalog.Cars, car => Assert.Contains(catalog.Wheels, wheel => wheel.Model == car.Wheel));
        }

        /// <summary>
        /// The engine sweeps from 0.4 octaves below to 0.6 above its baked pitch (GamePlay/CarSounds.cs), shifted by the
        /// car's own; MonoGame pitches a sound an octave either way at most, so a car's own stays between -0.6 and 0.4.
        /// </summary>
        [Fact]
        public void EveryCarsEngineCanBePitchedAndFollowsTheWheels()
        {
            Assert.All(catalog.Cars, car =>
            {
                Assert.InRange(car.EnginePitch, -0.6f, 0.4f);
                Assert.True(car.EngineResponse > 0f, car.Name);
            });
        }
    }
}
