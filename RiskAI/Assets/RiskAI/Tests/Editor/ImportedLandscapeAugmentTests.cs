using NUnit.Framework;
using UnityEngine;

namespace RiskAI.Tests
{
    public sealed class ImportedLandscapeAugmentTests
    {
        [Test]
        public void EuropeReliefPreservesAnchorClearingsWaterAndGentleGrades()
        {
            var data = Map("Europe");
            var original = (float[])data.heightSamples.Clone();

            Assert.That(ImportedLandscapeAugment.Apply(data), Is.True);
            Assert.That(data.HeightAt(-115, -96), Is.GreaterThan(4.9f));
            Assert.That(data.HeightAt(-100, -83), Is.EqualTo(0).Within(.0001f));
            Assert.That(data.HeightAt(-77, -87), Is.EqualTo(0).Within(.0001f));
            Assert.That(data.HeightAt(-115, -20), Is.EqualTo(0).Within(.0001f));
            Assert.That(MaximumCardinalRise(data), Is.LessThan(Mathf.Tan(35 * Mathf.Deg2Rad) * data.cellSize));

            for (int index = 0; index < data.heightSamples.Length; index++)
                if (data.landSamples[index] == 0) Assert.That(data.heightSamples[index], Is.EqualTo(original[index]));
            Assert.That(ImportedLandscapeAugment.Apply(data), Is.False, "A repeated call must not compound runtime relief.");
        }

        [Test]
        public void NewWorldUsesTheSpecifiedSourceCoordinateOffset()
        {
            var data = Map("NewWorld");
            Assert.That(ImportedLandscapeAugment.Apply(data), Is.True);
            Assert.That(data.HeightAt(48.84f, -96), Is.GreaterThan(4.9f));
            Assert.That(data.HeightAt(-115, -96), Is.EqualTo(0).Within(.0001f));
        }

        static ImportedMapData Map(string id)
        {
            const int width = 360, height = 220;
            var data = new ImportedMapData {
                mapId = id, width = width, height = height, originX = -240, originZ = -180, cellSize = 1,
                heightSamples = new float[width * height], waterSamples = new float[width * height],
                landSamples = new int[width * height], tileSamples = new int[width * height],
                cities = new[] { new ImportedMapData.City { id = "city", x = -100, z = -83, claimX = -100, claimZ = -83 } },
                countries = new[] { new ImportedMapData.Country { name = "camp", x = -77, z = -87, count = 1 } }
            };
            for (int index = 0; index < data.landSamples.Length; index++) { data.landSamples[index] = 1; data.waterSamples[index] = -.24f; }
            // A source-water patch must stay byte-for-byte at its original elevation.
            for (int z = 4; z < 12; z++) for (int x = 4; x < 12; x++) data.landSamples[z * width + x] = 0;
            return data;
        }

        static float MaximumCardinalRise(ImportedMapData data)
        {
            float maximum = 0;
            for (int z = 0; z < data.height; z++) for (int x = 0; x < data.width; x++)
            {
                int index = z * data.width + x;
                if (x + 1 < data.width) maximum = Mathf.Max(maximum, Mathf.Abs(data.heightSamples[index + 1] - data.heightSamples[index]));
                if (z + 1 < data.height) maximum = Mathf.Max(maximum, Mathf.Abs(data.heightSamples[index + data.width] - data.heightSamples[index]));
            }
            return maximum;
        }
    }
}
