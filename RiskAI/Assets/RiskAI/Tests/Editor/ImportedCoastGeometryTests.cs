using NUnit.Framework;
using UnityEngine;

namespace RiskAI.Tests
{
    public sealed class ImportedCoastGeometryTests
    {
        static ImportedMapData CornerMap()
        {
            const int size = 24;
            var data = new ImportedMapData {
                width = size, height = size, originX = -12, originZ = -16, cellSize = 2,
                heightSamples = new float[size * size], waterSamples = new float[size * size],
                landSamples = new int[size * size], tileSamples = new int[size * size],
                cities = new ImportedMapData.City[0],
                countries = new[] { new ImportedMapData.Country { name = "fixture" } }
            };
            for (int z = 0; z < size; z++) for (int x = 0; x < size; x++)
            {
                int k = z * size + x;
                data.landSamples[k] = x <= 11 && z <= 11 ? 0 : 1;
                data.heightSamples[k] = .3f * x - .2f * z + (x + z) % 3 * .25f;
                data.waterSamples[k] = .1f * x - .3f * z + .5f;
                data.tileSamples[k] = k;
            }
            return data;
        }

        static float Cross(Vector2 a, Vector2 b, Vector2 c)
        {
            var ab = b - a; var ac = c - a;
            return ab.x * ac.y - ab.y * ac.x;
        }

        [Test]
        public void RoundingIsBoundedPreservesSourceArraysAndKeepsEveryTriangleOriented()
        {
            var data = CornerMap();
            var heights = (float[])data.heightSamples.Clone();
            var waters = (float[])data.waterSamples.Clone();
            var flags = (int[])data.landSamples.Clone();
            var tiles = (int[])data.tileSamples.Clone();
            data.PrepareCoastGeometry();
            Assert.That(data.CoastGeometry.MovedVertexCount, Is.GreaterThan(0), "The fixture must exercise a rounded corner.");
            var vertices = new Vector2[data.width * data.height];
            for (int z = 0; z < data.height; z++) for (int x = 0; x < data.width; x++)
            {
                var vertex = data.TerrainVertex(x, z);
                vertices[z * data.width + x] = vertex;
                var source = new Vector2(data.originX + x * data.cellSize, data.originZ + z * data.cellSize);
                Assert.That(Vector2.Distance(vertex, source), Is.LessThanOrEqualTo(ImportedCoastGeometry.MaximumCellDisplacement * data.cellSize + .00001f));
                if (x == 0 || z == 0 || x == data.width - 1 || z == data.height - 1)
                    Assert.That(vertex, Is.EqualTo(source), "The outside boundary cannot move.");
                if (x == data.width - 1 || z == data.height - 1) continue;
                var b = data.TerrainVertex(x + 1, z);
                var c = data.TerrainVertex(x, z + 1);
                var d = data.TerrainVertex(x + 1, z + 1);
                Assert.That(Cross(vertex, b, c), Is.GreaterThan(0), "Lower source triangle inverted or collapsed.");
                Assert.That(Cross(d, c, b), Is.GreaterThan(0), "Upper source triangle inverted or collapsed.");
            }
            CollectionAssert.AreEqual(heights, data.heightSamples);
            CollectionAssert.AreEqual(waters, data.waterSamples);
            CollectionAssert.AreEqual(flags, data.landSamples);
            CollectionAssert.AreEqual(tiles, data.tileSamples);
            data.PrepareCoastGeometry();
            for (int z = 0; z < data.height; z++) for (int x = 0; x < data.width; x++)
                Assert.That(data.TerrainVertex(x, z), Is.EqualTo(vertices[z * data.width + x]), "Preparing again must not accumulate deformation.");
        }

        [Test]
        public void HeightWaterLandAndInverseMatchRenderedTrianglesIncludingCrossedSourceCells()
        {
            var data = CornerMap();
            data.PrepareCoastGeometry();
            var weights = new[] { new Vector2(.2f, .3f), new Vector2(.98f, .01f), new Vector2(.01f, .98f), new Vector2(.01f, .01f) };
            int crossedCells = 0;
            for (int z = 0; z < data.height - 1; z++) for (int x = 0; x < data.width - 1; x++)
            {
                int k = z * data.width + x;
                bool waterDisabled = data.landSamples[k] + data.landSamples[k + 1] + data.landSamples[k + data.width] + data.landSamples[k + data.width + 1] == 4;
                for (int triangle = 0; triangle < 2; triangle++)
                {
                    int a = triangle == 0 ? k : k + data.width + 1;
                    int b = triangle == 0 ? k + 1 : k + data.width;
                    int c = triangle == 0 ? k + data.width : k + 1;
                    foreach (var weight in weights)
                    {
                        float wa = 1 - weight.x - weight.y;
                        var point = Vertex(data, a) * wa + Vertex(data, b) * weight.x + Vertex(data, c) * weight.y;
                        float height = data.heightSamples[a] * wa + data.heightSamples[b] * weight.x + data.heightSamples[c] * weight.y;
                        float water = data.waterSamples[a] * wa + data.waterSamples[b] * weight.x + data.waterSamples[c] * weight.y;
                        Assert.That(data.HeightAt(point.x, point.y), Is.EqualTo(height).Within(.0002f));
                        Assert.That(data.WaterAt(point.x, point.y), Is.EqualTo(water).Within(.0002f));
                        // Stay away from the deliberate .02m equality boundary, where
                        // either floating-point rounding direction is legitimate.
                        if (waterDisabled || Mathf.Abs(height - water - .02f) > .0002f)
                            Assert.That(data.IsLand(point.x, point.y), Is.EqualTo(waterDisabled || height >= water + .02f));
                        var source = Source(data, a) * wa + Source(data, b) * weight.x + Source(data, c) * weight.y;
                        Assert.That(Vector2.Distance(data.SourcePositionAt(point.x, point.y), source), Is.LessThan(.0002f));
                        if (Mathf.FloorToInt((point.x - data.originX) / data.cellSize) != x ||
                            Mathf.FloorToInt((point.y - data.originZ) / data.cellSize) != z) crossedCells++;
                    }
                }
            }
            Assert.That(crossedCells, Is.GreaterThan(0), "A same-cell-only interpolation test would miss the coastline inversion lookup.");
        }

        static Vector2 Vertex(ImportedMapData data, int index) => data.TerrainVertex(index % data.width, index / data.width);
        static Vector2 Source(ImportedMapData data, int index) => new Vector2(data.originX + index % data.width * data.cellSize, data.originZ + index / data.width * data.cellSize);

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void CityAndPortBothProtectTheirModelAndClaimAnchors(bool port, bool claimAnchor)
        {
            var data = CornerMap();
            data.PrepareCoastGeometry();
            int ix = -1, iz = -1;
            for (int z = 0; z < data.height && ix < 0; z++) for (int x = 0; x < data.width; x++)
                if (data.CoastGeometry.Offset(x, z).sqrMagnitude > .0001f) { ix = x; iz = z; break; }
            Assert.That(ix, Is.GreaterThanOrEqualTo(0));
            var point = Source(data, iz * data.width + ix);
            float radius = port ? ImportedCoastGeometry.PortProtectionRadius : ImportedCoastGeometry.CityProtectionRadius;
            var anchor = point + Vector2.right * (radius - .5f);
            var city = new ImportedMapData.City { port = port, x = -1000, z = -1000, claimX = -1000, claimZ = -1000 };
            if (claimAnchor) { city.claimX = anchor.x; city.claimZ = anchor.y; }
            else { city.x = anchor.x; city.z = anchor.y; }
            data.cities = new[] { city };
            data.PrepareCoastGeometry();
            Assert.That(data.TerrainVertex(ix, iz), Is.EqualTo(point));
            if (claimAnchor) city.claimX += 1; else city.x += 1;
            data.PrepareCoastGeometry();
            Assert.That(data.CoastGeometry.Offset(ix, iz).sqrMagnitude, Is.GreaterThan(0), "Outside the protected radius, rounding remains enabled.");
        }

        [Test]
        public void SmallDryIslandSurvivesInsideSourceWaterWithoutChangingItsFlags()
        {
            var data = CornerMap();
            for (int k = 0; k < data.landSamples.Length; k++)
            { data.landSamples[k] = 0; data.heightSamples[k] = -2; data.waterSamples[k] = 0; }
            for (int z = 11; z <= 12; z++) for (int x = 11; x <= 12; x++)
            { int k = z * data.width + x; data.landSamples[k] = 1; data.heightSamples[k] = 2; }
            var flags = (int[])data.landSamples.Clone();
            data.PrepareCoastGeometry();
            var middle = (data.TerrainVertex(11, 11) + data.TerrainVertex(12, 12)) * .5f;
            Assert.That(data.IsLand(middle.x, middle.y), Is.True);
            Assert.That(data.HeightAt(middle.x, middle.y), Is.EqualTo(2).Within(.0001f));
            var water = Source(data, 8 * data.width + 8);
            Assert.That(data.IsLand(water.x, water.y), Is.False);
            CollectionAssert.AreEqual(flags, data.landSamples);
            Assert.That(Cross(data.TerrainVertex(11, 11), data.TerrainVertex(12, 11), data.TerrainVertex(11, 12)), Is.GreaterThan(0));
        }
    }
}
