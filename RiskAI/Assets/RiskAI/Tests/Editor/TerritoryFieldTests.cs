using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI.Tests
{
    /// <summary>The shared country partition behind the atlas, camp inspection, border posts and minimap.</summary>
    public sealed class TerritoryFieldTests
    {
        ScenarioMap previous;
        [SetUp] public void Remember() => previous = MapLayout.Scenario;
        [TearDown] public void Restore() => MapLayout.Configure(previous);

        [TestCase(ScenarioMap.Classic)]
        [TestCase(ScenarioMap.Riverlands)]
        [TestCase(ScenarioMap.Europe)]
        [TestCase(ScenarioMap.NewWorld)]
        public void EveryMemberCityLiesInsideItsOwnLandTerritory(ScenarioMap map)
        {
            MapLayout.Configure(map);
            var field = TerritoryField.Current;
            Assert.That(TerritoryField.Current, Is.SameAs(field), "The field is computed once per configured map.");
            for (int i = 0; i < MapLayout.Towns.Length; i++)
            {
                var town = MapLayout.Towns[i];
                // A source port may stand far out in its amphibious circle; its landing is the shore anchor.
                Vector3 p = town.IsPort && MapLayout.IsImported ? ImportedPortLayout.Resolve(town.Position, town.ClaimPoint).Shore : town.Position;
                if (MapLayout.IsLand(p.x, p.z))
                {
                    Assert.That(field.CountryAt(p.x, p.z), Is.EqualTo(town.Country), town.Id + " stands in its own country.");
                    field.Sample(p.x, p.z, out int rendered, out int renderedCity);
                    Assert.That(rendered, Is.EqualTo(town.Country), town.Id + " is drawn inside its own country.");
                    Assert.That(MapLayout.Towns[renderedCity].Country, Is.EqualTo(town.Country), town.Id + " ownership colour stays in its country.");
                    continue;
                }
                // Otherwise the shore beside the port is its country's.
                bool shore = false;
                for (float dz = -6; dz <= 6 && !shore; dz += .5f)
                for (float dx = -6; dx <= 6 && !shore; dx += .5f)
                    shore = dx * dx + dz * dz <= 36 && MapLayout.IsLand(p.x + dx, p.z + dz) && field.CountryAt(p.x + dx, p.z + dz) == town.Country;
                Assert.That(shore, Is.True, town.Id + " port holds the shore next to it.");
            }
        }

        [TestCase(ScenarioMap.Classic)]
        [TestCase(ScenarioMap.Riverlands)]
        [TestCase(ScenarioMap.Europe)]
        [TestCase(ScenarioMap.NewWorld)]
        public void EveryTerritoryPieceHoldsAMemberCityOrIsAnIsolatedIslet(ScenarioMap map)
        {
            MapLayout.Configure(map);
            var field = TerritoryField.Current;
            int w = field.Width, h = field.Height;
            var anchored = new bool[w * h];
            void Anchor(Vector3 world, int country)
            {
                var c = field.CellAt(world);
                for (int dz = -4; dz <= 4; dz++) for (int dx = -4; dx <= 4; dx++)
                {
                    int x = c.x + dx, z = c.y + dz;
                    if (x >= 0 && z >= 0 && x < w && z < h && field.IsLandCell(x, z) && field.CellCountry(x, z) == country) anchored[z * w + x] = true;
                }
            }
            foreach (var town in MapLayout.Towns) Anchor(town.Position, town.Country);
            if (MapLayout.IsImported) for (int c = 0; c < MapLayout.Countries.Length; c++) Anchor(MapLayout.Countries[c].CampPoint, c);
            var seen = new bool[w * h]; var stack = new Stack<int>();
            var landCells = new int[MapLayout.Countries.Length];
            for (int s = 0; s < w * h; s++)
            {
                int sx = s % w, sz = s / w;
                if (seen[s] || !field.IsLandCell(sx, sz)) continue;
                int country = field.CellCountry(sx, sz), size = 0; bool held = false, touchesForeignLand = false;
                Assert.That(country, Is.InRange(0, MapLayout.Countries.Length - 1), "Every land cell belongs to a country.");
                seen[s] = true; stack.Push(s);
                while (stack.Count > 0)
                {
                    int i = stack.Pop(), x = i % w, z = i / w; size++; held |= anchored[i];
                    for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, nz = z + dz;
                        if ((dx == 0 && dz == 0) || nx < 0 || nz < 0 || nx >= w || nz >= h || !field.IsLandCell(nx, nz)) continue;
                        int j = nz * w + nx;
                        if (field.CellCountry(nx, nz) != country) { touchesForeignLand |= dx == 0 || dz == 0; continue; }
                        if (!seen[j]) { seen[j] = true; stack.Push(j); }
                    }
                }
                landCells[country] += size;
                Assert.That(held || !touchesForeignLand, Is.True,
                    $"{MapLayout.Countries[country].Name}: a {size}-cell piece near cell {sx},{sz} holds none of its cities but borders another country.");
            }
            for (int c = 0; c < landCells.Length; c++) Assert.That(landCells[c], Is.GreaterThan(0), MapLayout.Countries[c].Name + " has land.");
        }

        [TestCase(ScenarioMap.Europe)]
        [TestCase(ScenarioMap.NewWorld)]
        public void ImportedTerritoriesFollowTheSourceCountryPaint(ScenarioMap map)
        {
            // Warcraft paints each Risk country with one ground tile; that paint is the original border.
            MapLayout.Configure(map);
            var field = TerritoryField.Current;
            var paint = new int[MapLayout.Countries.Length];
            for (int c = 0; c < paint.Length; c++)
            {
                var tiles = MapLayout.Towns.Where(t => t.Country == c && !t.IsPort).Select(t => { var cell = field.CellAt(t.Position); return field.CellTile(cell.x, cell.y); }).ToArray();
                paint[c] = tiles.Length == 0 ? -1 : tiles.GroupBy(t => t).OrderByDescending(g => g.Count()).First().Key;
                Assert.That(tiles.Distinct().Count(), Is.LessThanOrEqualTo(1), MapLayout.Countries[c].Name + " cities share one source paint.");
            }
            var painted = new HashSet<int>(paint.Where(t => t >= 0));
            int total = 0, agree = 0;
            for (int z = 0; z < field.Height; z++) for (int x = 0; x < field.Width; x++)
            {
                var p = field.CellCenter(x, z);
                if (!field.IsLandCell(x, z) || p.x < MapLayout.PlayableMin.x || p.x > MapLayout.PlayableMax.x || p.y < MapLayout.PlayableMin.y || p.y > MapLayout.PlayableMax.y) continue;
                int tile = field.CellTile(x, z), country = field.CellCountry(x, z);
                if (!painted.Contains(tile) || paint[country] < 0) continue;
                total++; if (paint[country] == tile) agree++;
            }
            Assert.That(agree / (float)total, Is.GreaterThan(.97f), $"{map}: {agree}/{total} painted land cells follow the source country paint.");
        }

        [TestCase(ScenarioMap.Europe)]
        [TestCase(ScenarioMap.NewWorld)]
        public void SourceCampsStandInsideTheirTerritoryAwayFromCities(ScenarioMap map)
        {
            // Source camp positions are kept; none is glued to a city (closest: Wales, 5.4 units).
            MapLayout.Configure(map);
            var field = TerritoryField.Current;
            for (int c = 0; c < MapLayout.Countries.Length; c++)
            {
                var camp = MapLayout.Countries[c].CampPoint;
                Assert.That(field.CountryAt(camp.x, camp.z), Is.EqualTo(c), MapLayout.Countries[c].Name + " camp lies inside its territory.");
                foreach (var town in MapLayout.Towns)
                    Assert.That(Vector2.Distance(new Vector2(camp.x, camp.z), new Vector2(town.Position.x, town.Position.z)), Is.GreaterThan(5f), MapLayout.Countries[c].Name + " camp / " + town.Id);
            }
        }

        [Test]
        public void ReconfiguringAMapRebuildsAnIdenticalField()
        {
            MapLayout.Configure(ScenarioMap.Classic);
            var first = TerritoryField.Current;
            MapLayout.Configure(ScenarioMap.Riverlands);
            Assert.That(TerritoryField.Current, Is.Not.SameAs(first));
            MapLayout.Configure(ScenarioMap.Classic);
            var second = TerritoryField.Current;
            Assert.That(second, Is.Not.SameAs(first));
            int differences = 0;
            for (int z = 0; z < first.Height; z++) for (int x = 0; x < first.Width; x++)
                if (second.CellCountry(x, z) != first.CellCountry(x, z) || second.CellCity(x, z) != first.CellCity(x, z)) differences++;
            Assert.That(differences, Is.Zero, "The partition is deterministic.");
        }
    }
}
