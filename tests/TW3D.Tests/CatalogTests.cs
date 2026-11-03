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
                .Concat(catalog.Scenery.Select(prop => prop.ModelAsset))
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

        /// <summary>The levels name a prop of the scenery by its model, so no two props share one.</summary>
        [Fact]
        public void EveryPropOfTheSceneryHasItsOwnModelAndAName()
        {
            Assert.NotEmpty(catalog.Scenery);
            Assert.Equal(catalog.Scenery.Count, catalog.Scenery.Select(prop => prop.Model).Distinct().Count());
            Assert.All(catalog.Scenery, prop => Assert.False(string.IsNullOrWhiteSpace(prop.Name)));
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

        /// <summary>The end of a car's exhaust pipe is a point of its model (in its bounds), so the smoke comes out of the car.</summary>
        [Fact]
        public void EveryCarsExhaustIsInItsModel()
        {
            Assert.All(catalog.Cars, car =>
            {
                if (car.Exhaust == null)
                    return;
                Assert.Equal(3, car.Exhaust.Length);
                float[] min = { float.MaxValue, float.MaxValue, float.MaxValue }, max = { float.MinValue, float.MinValue, float.MinValue };
                foreach (string line in File.ReadLines(Path.Combine(Repository.Content, car.Model + ".obj")).Where(line => line.StartsWith("v ")))
                {
                    float[] vertex = line.Split(' ', System.StringSplitOptions.RemoveEmptyEntries).Skip(1).Take(3)
                        .Select(number => float.Parse(number, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    for (int axis = 0; axis < 3; axis++)
                    {
                        min[axis] = System.Math.Min(min[axis], vertex[axis]);
                        max[axis] = System.Math.Max(max[axis], vertex[axis]);
                    }
                }
                for (int axis = 0; axis < 3; axis++)
                    Assert.InRange(car.Exhaust[axis], min[axis], max[axis]);
            });
        }
    }
}
