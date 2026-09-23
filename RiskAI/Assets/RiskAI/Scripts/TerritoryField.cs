using System.Collections.Generic;
using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// The single country partition of a map: which country and which member city own every
    /// point. The strategic atlas, camp inspection, border posts and minimap all read it.
    /// Countries grow over land from their member cities (and source camps) with a shortest-path
    /// competition, so a territory follows its own landmass instead of a straight Voronoi
    /// cut across bays and straits. Water is crossable only at a high cost, so an island joins
    /// the nearest country only when no country reaches it over land. On the Warcraft maps each
    /// Risk country is painted with one ground tile (a four-colour map); entering another
    /// country's tile is expensive, which reproduces the original borders. Detached pieces
    /// without a member city are merged into the neighbour that surrounds them and ragged
    /// one-cell spurs are smoothed away. Deterministic; computed once per configured map.
    /// </summary>
    public sealed class TerritoryField
    {
        const float WaterCost = 6f;
        // Authored maps: climbing a cliff (slope above CliffSlope, full cost CliffSlopeRange later)
        // costs like a wide strait, so borders follow precipices instead of eating the terrain below.
        const float CliffCost = 14f, CliffSlope = .55f, CliffSlopeRange = .45f;
        // Authored maps: a gentle deterministic cost relief (plus or minus Roughness) bends the
        // otherwise ruler-straight bisectors between two cities on open plains.
        const float Roughness = .35f, RoughnessScale = .07f;
        const float ForeignTileCost = 16f;
        const float ProceduralCell = .7f;
        const int SmoothingPasses = 2;

        public readonly int Width, Height;
        readonly float originX, originZ, cell;
        readonly bool sourceGrid;
        readonly bool[] land;
        readonly float[] heights, roughness;
        readonly short[] country;
        readonly short[] city;
        readonly ImportedMapData imported;
        readonly MapLayout.City[] towns;

        static TerritoryField cached;
        public static TerritoryField Current
        {
            get
            {
                if (cached == null || !ReferenceEquals(cached.towns, MapLayout.Towns) || !ReferenceEquals(cached.imported, MapLayout.Imported))
                    cached = new TerritoryField();
                return cached;
            }
        }
        /// <summary>Build time of the last computed field, reported with the territory_atlas phase.</summary>
        public static float LastBuildMilliseconds { get; private set; }

        TerritoryField()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            towns = MapLayout.Towns; imported = MapLayout.Imported;
            if (imported != null)
            {
                // Field cells are the source W3E vertices, so tiles need no resampling.
                sourceGrid = true; Width = imported.width; Height = imported.height;
                originX = imported.originX; originZ = imported.originZ; cell = imported.cellSize;
            }
            else
            {
                Vector2 min = MapLayout.PlayableMin, max = MapLayout.PlayableMax;
                cell = ProceduralCell; originX = min.x + cell * .5f; originZ = min.y + cell * .5f;
                Width = Mathf.CeilToInt((max.x - min.x) / cell); Height = Mathf.CeilToInt((max.y - min.y) / cell);
            }
            int count = Width * Height;
            land = new bool[count]; country = new short[count]; city = new short[count];
            // Relief only steers authored maps: on Warcraft maps the source country paint is the border.
            if (imported == null) { heights = new float[count]; roughness = new float[count]; }
            for (int z = 0; z < Height; z++) for (int x = 0; x < Width; x++)
            {
                var p = CellWorld(x, z);
                land[z * Width + x] = MapLayout.IsLand(p.x, p.y);
                if (heights == null) continue;
                heights[z * Width + x] = MapLayout.Height(p.x, p.y);
                float n = .65f * Mathf.PerlinNoise(p.x * RoughnessScale + 31.7f, p.y * RoughnessScale + 7.3f)
                    + .35f * Mathf.PerlinNoise(p.x * RoughnessScale * 2.3f + 3.1f, p.y * RoughnessScale * 2.3f + 57.9f);
                roughness[z * Width + x] = 1 + Roughness * (2 * n - 1);
            }
            int[] tiles = null, countryTile = null;
            if (imported != null) PrepareTiles(out tiles, out countryTile);
            var seeds = Seeds(tiles, countryTile);
            GrowCountries(seeds, tiles, countryTile);
            var anchored = AnchorCells(seeds);
            MergeDetachedPieces(anchored);
            for (int pass = 0; pass < SmoothingPasses; pass++) Smooth(anchored);
            MergeDetachedPieces(anchored);
            GrowCities(seeds);
            LastBuildMilliseconds = (float)watch.Elapsed.TotalMilliseconds;
        }

        // ------------------------------------------------------------------ queries

        /// <summary>Country owning a world XZ point, or -1 outside the map.</summary>
        public int CountryAt(float x, float z) { int i = Nearest(x, z); return i < 0 ? -1 : country[i]; }
        /// <summary>Member city (MapLayout.Towns index) owning a world XZ point, or -1.</summary>
        public int CityAt(float x, float z) { int i = Nearest(x, z); return i < 0 ? -1 : city[i]; }

        // Raw cells, for audits and tests.
        public bool IsLandCell(int x, int z) => land[z * Width + x];
        public int CellCountry(int x, int z) => country[z * Width + x];
        public int CellCity(int x, int z) => city[z * Width + x];
        public Vector2 CellCenter(int x, int z) => CellWorld(x, z);
        /// <summary>Source ground tile of a cell on imported maps, otherwise -1.</summary>
        public int CellTile(int x, int z) => imported != null ? ImportedMapData.GroundTileIndex(imported.tileSamples[z * Width + x]) : -1;
        public Vector2Int CellAt(Vector3 world) { int i = CellOf(world); return new Vector2Int(i % Width, i / Width); }

        /// <summary>
        /// Smooth lookup for rendering: blends the four surrounding cells and returns the
        /// winning country and, inside it, the city with the largest weight. Land cells vote
        /// ahead of water cells so coasts keep the country of the adjacent shore.
        /// </summary>
        public void Sample(float x, float z, out int sampledCountry, out int sampledCity) => Sample(x, z, out sampledCountry, out sampledCity, out _, out _);
        /// <param name="margin">Winning vote minus the runner-up, over the total vote (0 on a border, 1 inside).
        /// Near a border it grows by <see cref="MarginPerCell"/> per field cell, so it places the border inside a texel.</param>
        /// <param name="cityMargin">The same margin between the cities of the winning country.</param>
        public void Sample(float x, float z, out int sampledCountry, out int sampledCity, out float margin, out float cityMargin)
        {
            // Cubic B-spline vote over the surrounding 4x4 cells: borders become smooth curves
            // instead of following the cell staircase. Land cells vote ahead of water cells so
            // coasts keep the country of the adjacent shore.
            FieldPoint(x, z, out float fx, out float fz);
            int x1 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, Width - 2), z1 = Mathf.Clamp(Mathf.FloorToInt(fz), 0, Height - 2);
            float u = Mathf.Clamp01(fx - x1), v = Mathf.Clamp01(fz - z1);
            // Interior fast path: one country and one city over the whole 4x4 footprint.
            if (x1 >= 1 && z1 >= 1 && x1 + 2 < Width && z1 + 2 < Height)
            {
                int corner = (z1 - 1) * Width + x1 - 1; short c0 = country[corner], s0 = city[corner]; bool uniform = true;
                for (int j = 0; j < 4 && uniform; j++) { int row = corner + j * Width; for (int i = 0; i < 4; i++) if (country[row + i] != c0 || city[row + i] != s0) { uniform = false; break; } }
                if (uniform) { sampledCountry = c0; sampledCity = s0; margin = 1; cityMargin = 1; return; }
            }
            BSpline(u, wx); BSpline(v, wz);
            int labels = 0; bool anyLand = false;
            for (int j = 0; j < 4; j++) for (int i = 0; i < 4; i++)
            {
                int cx = Mathf.Clamp(x1 - 1 + i, 0, Width - 1), cz = Mathf.Clamp(z1 - 1 + j, 0, Height - 1);
                anyLand |= land[cz * Width + cx];
            }
            float total = 0;
            for (int j = 0; j < 4; j++) for (int i = 0; i < 4; i++)
            {
                int cx = Mathf.Clamp(x1 - 1 + i, 0, Width - 1), cz = Mathf.Clamp(z1 - 1 + j, 0, Height - 1), cell = cz * Width + cx;
                if (anyLand && !land[cell]) continue;
                float w = wx[i] * wz[j]; if (w <= 0) continue;
                total += w;
                int label = country[cell], k = 0;
                while (k < labels && voteLabel[k] != label) k++;
                if (k == labels) { voteLabel[k] = label; voteWeight[k] = 0; labels++; }
                voteWeight[k] += w;
            }
            int best = -1; float bestWeight = -1, runnerUp = 0;
            // Ties resolve to the lower country index so every platform draws the same border.
            for (int k = 0; k < labels; k++)
                if (voteWeight[k] > bestWeight || (voteWeight[k] == bestWeight && voteLabel[k] < voteLabel[best])) { best = k; bestWeight = voteWeight[k]; }
            if (best < 0) { sampledCountry = -1; sampledCity = -1; margin = 1; cityMargin = 1; return; }
            for (int k = 0; k < labels; k++) if (k != best && voteWeight[k] > runnerUp) runnerUp = voteWeight[k];
            sampledCountry = voteLabel[best];
            margin = total > 0 ? (bestWeight - runnerUp) / total : 1;
            // Second vote, among the winning country's cells, for its city.
            int cities = 0;
            for (int j = 0; j < 4; j++) for (int i = 0; i < 4; i++)
            {
                int cx = Mathf.Clamp(x1 - 1 + i, 0, Width - 1), cz = Mathf.Clamp(z1 - 1 + j, 0, Height - 1), cell = cz * Width + cx;
                if ((anyLand && !land[cell]) || country[cell] != sampledCountry) continue;
                float w = wx[i] * wz[j]; int label = city[cell], k = 0;
                while (k < cities && voteCity[k] != label) k++;
                if (k == cities) { voteCity[k] = label; voteCityWeight[k] = 0; cities++; }
                voteCityWeight[k] += w;
            }
            int bestCity = 0; float cityRunnerUp = 0;
            for (int k = 1; k < cities; k++)
                if (voteCityWeight[k] > voteCityWeight[bestCity] || (voteCityWeight[k] == voteCityWeight[bestCity] && voteCity[k] < voteCity[bestCity])) bestCity = k;
            for (int k = 0; k < cities; k++) if (k != bestCity && voteCityWeight[k] > cityRunnerUp) cityRunnerUp = voteCityWeight[k];
            sampledCity = cities > 0 ? voteCity[bestCity] : -1;
            cityMargin = bestWeight > 0 && cities > 0 ? (voteCityWeight[bestCity] - cityRunnerUp) / bestWeight : 1;
        }
        /// <summary>Slope of the vote margin across a straight border, per field cell (cubic B-spline of a step: 2 x 2/3).</summary>
        public const float MarginPerCell = 4f / 3f;
        readonly float[] wx = new float[4], wz = new float[4], voteWeight = new float[16], voteCityWeight = new float[16];
        readonly int[] voteLabel = new int[16], voteCity = new int[16];
        static void BSpline(float t, float[] w)
        {
            float t2 = t * t, t3 = t2 * t, s = 1 - t;
            w[0] = s * s * s / 6f; w[1] = (3 * t3 - 6 * t2 + 4) / 6f; w[2] = (-3 * t3 + 3 * t2 + 3 * t + 1) / 6f; w[3] = t3 / 6f;
        }
        /// <summary>
        /// On authored maps the exact land test (rivers, ponds, islands) is costly; away from any
        /// coast the surrounding 4x4 field cells already agree and answer it. Imported maps always
        /// use the exact source test.
        /// </summary>
        public bool TryUniformLand(float x, float z, out bool isLand)
        {
            isLand = false;
            if (sourceGrid) return false;
            FieldPoint(x, z, out float fx, out float fz);
            int x0 = Mathf.FloorToInt(fx) - 1, z0 = Mathf.FloorToInt(fz) - 1;
            if (x0 < 0 || z0 < 0 || x0 + 3 >= Width || z0 + 3 >= Height) return false;
            bool first = land[z0 * Width + x0];
            for (int dz = 0; dz < 4; dz++) for (int dx = 0; dx < 4; dx++) if (land[(z0 + dz) * Width + x0 + dx] != first) return false;
            isLand = first; return true;
        }
        /// <summary>World size of one field cell (the scale over which a border vote changes).</summary>
        public float CellSize => cell;

        Vector2 CellWorld(int x, int z) => sourceGrid ? imported.TerrainVertex(x, z) : new Vector2(originX + x * cell, originZ + z * cell);
        void FieldPoint(float x, float z, out float fx, out float fz)
        {
            if (sourceGrid) { var source = imported.SourcePositionAt(x, z); x = source.x; z = source.y; }
            fx = (x - originX) / cell; fz = (z - originZ) / cell;
        }
        int Nearest(float x, float z)
        {
            if (float.IsNaN(x) || float.IsNaN(z) || float.IsInfinity(x) || float.IsInfinity(z)) return -1;
            FieldPoint(x, z, out float fx, out float fz);
            int ix = Mathf.RoundToInt(fx), iz = Mathf.RoundToInt(fz);
            if (ix < 0 || iz < 0 || ix >= Width || iz >= Height) return -1;
            return iz * Width + ix;
        }
        int CellOf(Vector3 world)
        {
            FieldPoint(world.x, world.z, out float fx, out float fz);
            return Mathf.Clamp(Mathf.RoundToInt(fz), 0, Height - 1) * Width + Mathf.Clamp(Mathf.RoundToInt(fx), 0, Width - 1);
        }

        // ------------------------------------------------------------------ construction

        readonly struct Seed
        {
            public readonly int Cell, Country, City;
            public Seed(int cell, int country, int city) { Cell = cell; Country = country; City = city; }
        }

        List<Seed> Seeds(int[] tiles, int[] countryTile)
        {
            var seeds = new List<Seed>(towns.Length * 2 + MapLayout.Countries.Length);
            for (int i = 0; i < towns.Length; i++)
            {
                seeds.Add(new Seed(CellOf(towns[i].Position), towns[i].Country, i));
                if (imported == null || !towns[i].IsPort) continue;
                // A source port may stand far out in its amphibious circle; its shore landing
                // (where the harbour is built) is land of its country unless painted for another.
                int shore = CellOf(ImportedPortLayout.Resolve(towns[i].Position, towns[i].ClaimPoint).Shore);
                int paint = tiles[shore];
                bool foreign = paint != countryTile[towns[i].Country] && System.Array.IndexOf(countryTile, paint) >= 0;
                if (land[shore] && !foreign) seeds.Add(new Seed(shore, towns[i].Country, i));
            }
            // Imported camps are source-authored inside their country.
            if (imported != null)
                for (int c = 0; c < MapLayout.Countries.Length; c++)
                {
                    int cellIndex = CellOf(MapLayout.Countries[c].CampPoint);
                    if (land[cellIndex]) seeds.Add(new Seed(cellIndex, c, -1));
                }
            return seeds;
        }

        void PrepareTiles(out int[] tiles, out int[] countryTile)
        {
            int count = Width * Height;
            tiles = new int[count];
            for (int i = 0; i < count; i++) tiles[i] = ImportedMapData.GroundTileIndex(imported.tileSamples[i]);
            int countries = MapLayout.Countries.Length;
            var votes = new Dictionary<int, int>[countries];
            for (int i = 0; i < towns.Length; i++)
            {
                int c = towns[i].Country, cellIndex = CellOf(towns[i].Position);
                // Ports stand in amphibious source circles; their tile is not the country paint.
                if (towns[i].IsPort || !land[cellIndex]) continue;
                (votes[c] ??= new Dictionary<int, int>()).TryGetValue(tiles[cellIndex], out int n);
                votes[c][tiles[cellIndex]] = n + 1;
            }
            countryTile = new int[countries];
            for (int c = 0; c < countries; c++)
            {
                countryTile[c] = -1; int best = 0;
                if (votes[c] != null) foreach (var pair in votes[c]) if (pair.Value > best || (pair.Value == best && pair.Key < countryTile[c])) { best = pair.Value; countryTile[c] = pair.Key; }
            }
            // A country of only ports (small islands) takes the paint of the land next to its first port.
            for (int i = 0; i < towns.Length; i++)
            {
                int c = towns[i].Country; if (countryTile[c] >= 0) continue;
                int start = CellOf(towns[i].Position), sx = start % Width, sz = start / Width; float nearest = float.MaxValue;
                for (int dz = -6; dz <= 6; dz++) for (int dx = -6; dx <= 6; dx++)
                {
                    int x = sx + dx, z = sz + dz; if (x < 0 || z < 0 || x >= Width || z >= Height) continue;
                    int j = z * Width + x; float d = dx * dx + dz * dz;
                    if (land[j] && d < nearest) { nearest = d; countryTile[c] = tiles[j]; }
                }
            }
        }

        void GrowCountries(List<Seed> seeds, int[] tiles, int[] countryTile)
        {
            int count = Width * Height;
            var distance = new float[count];
            for (int i = 0; i < count; i++) { distance[i] = float.MaxValue; country[i] = -1; }
            HashSet<int> paints = null;
            if (tiles != null) { paints = new HashSet<int>(); foreach (int t in countryTile) if (t >= 0) paints.Add(t); }
            var heap = new MinHeap(count);
            foreach (var seed in seeds)
            {
                if (distance[seed.Cell] <= 0 && country[seed.Cell] <= seed.Country) continue;
                distance[seed.Cell] = 0; country[seed.Cell] = (short)seed.Country; heap.Push(0, seed.Cell);
            }
            while (heap.Count > 0)
            {
                heap.Pop(out float d, out int i);
                if (d > distance[i]) continue;
                int owner = country[i], x = i % Width, z = i / Width, paint = tiles != null ? countryTile[owner] : -1;
                for (int k = 0; k < Neighbours; k++)
                {
                    int nx = x + Dx[k], nz = z + Dz[k];
                    if (nx < 0 || nz < 0 || nx >= Width || nz >= Height) continue;
                    int j = nz * Width + nx;
                    float step = Step[k] * TerrainFactor(i, x, z, k);
                    if (land[j] && paints != null && paints.Contains(tiles[j]) && tiles[j] != paint) step *= ForeignTileCost;
                    float next = d + step;
                    if (next < distance[j] || (next == distance[j] && owner < country[j]))
                    { distance[j] = next; country[j] = (short)owner; heap.Push(next, j); }
                }
            }
        }

        /// <summary>
        /// Cost multiplier of one growth step: water (also for any cell a knight move passes),
        /// cliffs and the authored roughness. 16 neighbours keep the growth nearly isotropic, so
        /// open-plain borders are not locked to the axes and diagonals.
        /// </summary>
        float TerrainFactor(int from, int x, int z, int k)
        {
            int to = (z + Dz[k]) * Width + x + Dx[k];
            if (!land[to]) return WaterCost;
            if (k >= 8)
            {
                int sx = Dx[k] > 0 ? 1 : -1, sz = Dz[k] > 0 ? 1 : -1;
                int a, b;
                if (Dx[k] == 2 || Dx[k] == -2) { a = z * Width + x + sx; b = (z + Dz[k]) * Width + x + sx; }
                else { a = (z + sz) * Width + x; b = (z + sz) * Width + x + Dx[k]; }
                if (!land[a] || !land[b]) return WaterCost;
            }
            if (heights == null || !land[from]) return 1;
            return CliffFactor(from, to, Step[k]) * .5f * (roughness[from] + roughness[to]);
        }
        float CliffFactor(int from, int to, float step)
        {
            float slope = Mathf.Abs(heights[to] - heights[from]) / (step * cell);
            return 1 + CliffCost * Mathf.Clamp01((slope - CliffSlope) / CliffSlopeRange);
        }
        /// <summary>Whether a field step between two neighbouring cells climbs a cliff (authored maps).</summary>
        public bool IsCliffStep(int x0, int z0, int x1, int z1)
        {
            if (heights == null) return false;
            int a = z0 * Width + x0, b = z1 * Width + x1;
            float step = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0)) * cell;
            return land[a] && land[b] && Mathf.Abs(heights[b] - heights[a]) / step > CliffSlope + CliffSlopeRange * .5f;
        }

        // Land cells within four field cells of a country's own seed hold that country's pieces in place.
        bool[] AnchorCells(List<Seed> seeds)
        {
            var anchored = new bool[Width * Height];
            foreach (var seed in seeds)
            {
                int sx = seed.Cell % Width, sz = seed.Cell / Width;
                for (int dz = -4; dz <= 4; dz++) for (int dx = -4; dx <= 4; dx++)
                {
                    if (dx * dx + dz * dz > 16) continue;
                    int x = sx + dx, z = sz + dz; if (x < 0 || z < 0 || x >= Width || z >= Height) continue;
                    int j = z * Width + x;
                    if (land[j] && country[j] == seed.Country) anchored[j] = true;
                }
            }
            return anchored;
        }

        /// <summary>
        /// A land piece of a country that holds none of its cities is an artefact of the growth
        /// (a peninsula tip reached around a bay, a coastal strip cut off by a neighbour). It joins
        /// the neighbouring country it shares the longest border with. Isolated islets keep the
        /// country that reached them across the water.
        /// </summary>
        void MergeDetachedPieces(bool[] anchored)
        {
            int count = Width * Height;
            var piece = new int[count]; var stack = new Stack<int>(); var members = new List<int>(); var border = new Dictionary<int, int>();
            for (int round = 0; round < 4; round++)
            {
                for (int i = 0; i < count; i++) piece[i] = -1;
                bool changed = false; int next = 0;
                for (int s = 0; s < count; s++)
                {
                    if (!land[s] || piece[s] >= 0) continue;
                    int owner = country[s]; bool hasAnchor = false;
                    members.Clear(); border.Clear(); piece[s] = next; stack.Push(s);
                    while (stack.Count > 0)
                    {
                        int i = stack.Pop(), x = i % Width, z = i / Width; members.Add(i); hasAnchor |= anchored[i];
                        for (int k = 0; k < 8; k++)
                        {
                            int nx = x + Dx[k], nz = z + Dz[k];
                            if (nx < 0 || nz < 0 || nx >= Width || nz >= Height) continue;
                            int j = nz * Width + nx; if (!land[j]) continue;
                            if (country[j] == owner) { if (piece[j] < 0) { piece[j] = next; stack.Push(j); } }
                            else if (k < 4) { border.TryGetValue(country[j], out int n); border[country[j]] = n + 1; }
                        }
                    }
                    next++;
                    if (hasAnchor || border.Count == 0) continue;
                    int target = -1, longest = 0;
                    foreach (var pair in border) if (pair.Value > longest || (pair.Value == longest && pair.Key < target)) { longest = pair.Value; target = pair.Key; }
                    foreach (int i in members) country[i] = (short)target;
                    changed = true;
                }
                if (!changed) break;
            }
        }

        /// <summary>Majority filter on land: removes staircase spurs and one-cell slivers without moving seeds.</summary>
        void Smooth(bool[] anchored)
        {
            var result = (short[])country.Clone();
            var counts = new Dictionary<int, int>(9);
            for (int z = 1; z < Height - 1; z++) for (int x = 1; x < Width - 1; x++)
            {
                int i = z * Width + x; if (!land[i] || anchored[i]) continue;
                counts.Clear();
                for (int k = 0; k < 8; k++)
                {
                    int j = (z + Dz[k]) * Width + x + Dx[k]; if (!land[j]) continue;
                    counts.TryGetValue(country[j], out int n); counts[country[j]] = n + 1;
                }
                counts.TryGetValue(country[i], out int own);
                int best = -1, bestCount = 0;
                foreach (var pair in counts) if (pair.Value > bestCount || (pair.Value == bestCount && pair.Key < best)) { best = pair.Key; bestCount = pair.Value; }
                if (best != country[i] && bestCount >= 5 && bestCount > own) result[i] = (short)best;
            }
            System.Array.Copy(result, country, result.Length);
        }

        /// <summary>Splits each country among its own cities for live per-city ownership colours.</summary>
        void GrowCities(List<Seed> seeds)
        {
            int count = Width * Height;
            var distance = new float[count];
            for (int i = 0; i < count; i++) { distance[i] = float.MaxValue; city[i] = -1; }
            var heap = new MinHeap(count);
            foreach (var seed in seeds)
            {
                if (seed.City < 0 || distance[seed.Cell] == 0) continue;
                distance[seed.Cell] = 0; city[seed.Cell] = (short)seed.City; heap.Push(0, seed.Cell);
            }
            while (heap.Count > 0)
            {
                heap.Pop(out float d, out int i);
                if (d > distance[i]) continue;
                int owner = city[i], ownerCountry = towns[owner].Country, x = i % Width, z = i / Width;
                for (int k = 0; k < Neighbours; k++)
                {
                    int nx = x + Dx[k], nz = z + Dz[k];
                    if (nx < 0 || nz < 0 || nx >= Width || nz >= Height) continue;
                    int j = nz * Width + nx;
                    // A city spreads over its own country's land; it only crosses water or
                    // foreign land to reach detached pieces of that country.
                    float step = Step[k] * TerrainFactor(i, x, z, k) * (land[j] && country[j] != ownerCountry ? 40 : 1);
                    float next = d + step;
                    if (next < distance[j]) { distance[j] = next; city[j] = (short)owner; heap.Push(next, j); }
                }
            }
            // A cell whose nearest city belongs to another country keeps the country's first city.
            var first = new short[MapLayout.Countries.Length];
            for (int c = 0; c < first.Length; c++) first[c] = -1;
            for (int t = towns.Length - 1; t >= 0; t--) first[towns[t].Country] = (short)t;
            for (int i = 0; i < count; i++)
                if (country[i] >= 0 && (city[i] < 0 || towns[city[i]].Country != country[i])) city[i] = first[country[i]];
        }

        const int Neighbours = 16;
        static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1, 2, 1, -1, -2, -2, -1, 1, 2 };
        static readonly int[] Dz = { 0, 0, 1, -1, 1, -1, 1, -1, 1, 2, 2, 1, -1, -2, -2, -1 };
        static readonly float[] Step = { 1, 1, 1, 1, 1.41421356f, 1.41421356f, 1.41421356f, 1.41421356f,
            2.23606798f, 2.23606798f, 2.23606798f, 2.23606798f, 2.23606798f, 2.23606798f, 2.23606798f, 2.23606798f };

        sealed class MinHeap
        {
            float[] keys; int[] values; public int Count;
            public MinHeap(int capacity) { keys = new float[Mathf.Max(16, capacity)]; values = new int[keys.Length]; }
            public void Push(float key, int value)
            {
                if (Count == keys.Length) { System.Array.Resize(ref keys, Count * 2); System.Array.Resize(ref values, Count * 2); }
                int i = Count++;
                while (i > 0)
                {
                    int parent = (i - 1) >> 1;
                    if (keys[parent] < key || (keys[parent] == key && values[parent] <= value)) break;
                    keys[i] = keys[parent]; values[i] = values[parent]; i = parent;
                }
                keys[i] = key; values[i] = value;
            }
            public void Pop(out float key, out int value)
            {
                key = keys[0]; value = values[0];
                float lastKey = keys[--Count]; int lastValue = values[Count], i = 0;
                while (true)
                {
                    int child = 2 * i + 1; if (child >= Count) break;
                    if (child + 1 < Count && (keys[child + 1] < keys[child] || (keys[child + 1] == keys[child] && values[child + 1] < values[child]))) child++;
                    if (lastKey < keys[child] || (lastKey == keys[child] && lastValue <= values[child])) break;
                    keys[i] = keys[child]; values[i] = values[child]; i = child;
                }
                keys[i] = lastKey; values[i] = lastValue;
            }
        }
    }
}
