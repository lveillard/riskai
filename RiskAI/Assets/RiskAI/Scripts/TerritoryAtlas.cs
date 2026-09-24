using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// GPU copy of the shared <see cref="TerritoryField"/> used by country inspection and the
    /// strategic ownership view. R/G hold the owning site (city or independent port), B the
    /// country plus one (zero is water or ungrouped) and A the land mask.
    /// </summary>
    public sealed class TerritoryAtlas
    {
        public readonly struct Site
        {
            public readonly Vector2 Point;
            public readonly int Country;
            public readonly Settlement Town;
            public readonly Harbor Port;
            public Site(Vector3 point, int country, Settlement town, Harbor port = null)
            { Point = new Vector2(point.x, point.z); Country = country; Town = town; Port = port; }
            public int Owner => Port ? Port.Owner : Town ? Town.State.Owner : -1;
        }
        public readonly List<Site> Sites = new List<Site>();
        public readonly Texture2D Regions, Palette, Borders;
        /// <summary>Per country: the land point farthest from its borders and coast (label anchor), or NaN.</summary>
        public readonly Vector2[] LabelAnchors;
        /// <summary>Per country: world distance from the anchor to the nearest border or coast.</summary>
        public readonly float[] LabelRadii;
        /// <summary>Texels represented by a full byte in <see cref="Borders"/>.</summary>
        public const float BorderRange = 16;
        /// <summary>Muted fill of a country no player holds.</summary>
        public static readonly Color32 NeutralFill = new Color32(150, 142, 118, 255);
        public readonly Vector4 Bounds;
        public TerritoryField Field { get; }
        readonly Color32[] palette;
        const int Resolution = 1024;
        // An independent harbour (authored maps) shows its own owner on a small patch of shore.
        const float PortPatchRadius = 4.5f;

        public TerritoryAtlas(BattleSession session,GeneratedResourceOwner owner)
        {
            Field = TerritoryField.Current;
            // Site indices of towns equal MapLayout.Towns indices: the field's city labels map directly.
            foreach (var town in session.Towns)
                Sites.Add(new Site(town.ClaimPoint, town.State.Country, town));
            var patches = new List<int>();
            if (session.Naval) foreach (var port in session.Naval.Harbors)
            {
                int country = port.LinkedTown ? port.LinkedTown.State.Country : port.State.Country;
                // Both shore and berth belong to the port's group, even when its city is inland.
                if (!port.IsImportedPort) patches.Add(Sites.Count);
                Sites.Add(new Site(port.Landing, country, port.LinkedTown, port));
                Sites.Add(new Site(port.Berth, country, port.LinkedTown, port));
            }
            Vector2 min = MapLayout.PlayableMin, max = MapLayout.PlayableMax;
            Bounds = new Vector4(min.x, min.y, max.x - min.x, max.y - min.y);
            var pixels = new Color32[Resolution * Resolution];
            var margins = new float[pixels.Length]; var cityMargins = new float[pixels.Length];
            float patch = PortPatchRadius * PortPatchRadius;
            for (int z = 0; z < Resolution; z++) for (int x = 0; x < Resolution; x++)
            {
                int i = z * Resolution + x;
                float wx = min.x + (x + .5f) / Resolution * Bounds.z, wz = min.y + (z + .5f) / Resolution * Bounds.w;
                if (!Field.TryUniformLand(wx, wz, out bool land)) land = MapLayout.IsLand(wx, wz);
                if (Sites.Count == 0 || !land) { pixels[i] = new Color32(0, 0, 0, 0); continue; }
                Field.Sample(wx, wz, out int country, out int city, out margins[i], out cityMargins[i]);
                int site = city >= 0 && city < session.Towns.Count ? city : 0;
                var point = new Vector2(wx, wz);
                foreach (int port in patches) if ((Sites[port].Point - point).sqrMagnitude < patch) { site = port; break; }
                pixels[i] = new Color32((byte)(site & 255), (byte)(site >> 8), (byte)(country + 1), 255);
            }
            Regions = owner.Track(new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false, true)
                { name = "Territory IDs and coast", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp });
            Regions.SetPixels32(pixels); Regions.Apply(false, true);
            // Texels per unit of vote margin: the margin places a border inside its texel.
            float cellTexels = Field.CellSize / Bounds.z * Resolution / TerritoryField.MarginPerCell;
            var borders = EncodeBorders(pixels, margins, cityMargins, Resolution, Resolution, cellTexels, out var countryDistance);
            Borders = owner.Track(new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false, true)
                { name = "Territory border distances", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp });
            Borders.SetPixels32(borders); Borders.Apply(false, true);
            FindLabelAnchors(pixels, countryDistance, out LabelAnchors, out LabelRadii);
            palette = new Color32[Mathf.NextPowerOfTwo(Mathf.Max(2, Sites.Count))];
            Palette = owner.Track(new Texture2D(palette.Length, 1, TextureFormat.RGBA32, false, false)
                { name = "Live territory ownership", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp });
            RefreshOwners();
        }
        public void RefreshOwners()
        {
            bool changed = false;
            for (int i = 0; i < Sites.Count; i++)
            {
                int owner = Sites[i].Owner;
                // Alpha marks a neutral site so the shader can vary its muted fill per country.
                Color32 next = PlayerRules.IsPlayer(owner) ? (Color32)VisualFactory.TeamColor(owner) : NeutralFill;
                next.a = PlayerRules.IsPlayer(owner) ? (byte)255 : (byte)0;
                if (!palette[i].Equals(next)) { palette[i] = next; changed = true; }
            }
            if (changed) { Palette.SetPixels32(palette); Palette.Apply(false, false); }
        }
        /// <summary>
        /// Distances, in texels, to the nearest border between two countries (R) and between two
        /// sites of one country (G), scaled so a byte spans <see cref="BorderRange"/> texels and
        /// sampled bilinearly by the strategic shader for anti-aliased lines of constant screen width.
        /// Borders start at their sub-texel position from the vote margins (a port patch or a
        /// test fixture without margins starts half a texel away); water never
        /// makes a border. Shared with the shader regression test.
        /// </summary>
        public static Color32[] EncodeBorders(Color32[] regions, float[] margins, float[] siteMargins, int width, int height, float texelsPerMargin, out float[] countryDistance)
        {
            int count = width * height;
            countryDistance = new float[count]; var siteDistance = new float[count];
            for (int i = 0; i < count; i++) { countryDistance[i] = float.MaxValue; siteDistance[i] = float.MaxValue; }
            for (int z = 0; z < height; z++) for (int x = 0; x < width; x++)
            {
                int i = z * width + x; var p = regions[i]; if (p.a < 128) continue;
                bool countryEdge = false, siteEdge = false;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), nz = z + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (nx < 0 || nz < 0 || nx >= width || nz >= height) continue;
                    var q = regions[nz * width + nx]; if (q.a < 128) continue;
                    if (q.b != p.b) countryEdge = true;
                    else if (q.r != p.r || q.g != p.g) siteEdge = true;
                }
                if (countryEdge) countryDistance[i] = margins != null ? Mathf.Clamp(margins[i] * texelsPerMargin, 0, 1) : .5f;
                if (countryEdge) siteDistance[i] = countryDistance[i];
                else if (siteEdge) siteDistance[i] = siteMargins != null ? Mathf.Clamp(siteMargins[i] * texelsPerMargin, 0, 1) : .5f;
            }
            Chamfer(countryDistance, width, height); Chamfer(siteDistance, width, height);
            var result = new Color32[count];
            for (int i = 0; i < count; i++)
                result[i] = new Color32(Pack(countryDistance[i]), Pack(siteDistance[i]), 255, 255);
            return result;
        }
        static byte Pack(float texels) => (byte)Mathf.RoundToInt(Mathf.Clamp01(texels / BorderRange) * 255);
        // Two-pass 3x3 chamfer distance, written without calls: it runs over a million texels at startup.
        static void Chamfer(float[] d, int width, int height)
        {
            const float Diagonal = 1.41421356f;
            for (int z = 0; z < height; z++)
            {
                int row = z * width;
                for (int x = 0; x < width; x++)
                {
                    int i = row + x; float v = d[i], c;
                    if (x > 0 && (c = d[i - 1] + 1) < v) v = c;
                    if (z > 0)
                    {
                        if ((c = d[i - width] + 1) < v) v = c;
                        if (x > 0 && (c = d[i - width - 1] + Diagonal) < v) v = c;
                        if (x < width - 1 && (c = d[i - width + 1] + Diagonal) < v) v = c;
                    }
                    d[i] = v;
                }
            }
            for (int z = height - 1; z >= 0; z--)
            {
                int row = z * width;
                for (int x = width - 1; x >= 0; x--)
                {
                    int i = row + x; float v = d[i], c;
                    if (x < width - 1 && (c = d[i + 1] + 1) < v) v = c;
                    if (z < height - 1)
                    {
                        if ((c = d[i + width] + 1) < v) v = c;
                        if (x < width - 1 && (c = d[i + width + 1] + Diagonal) < v) v = c;
                        if (x > 0 && (c = d[i + width - 1] + Diagonal) < v) v = c;
                    }
                    d[i] = v;
                }
            }
        }

        // Label anchors need no texel precision: a 4x coarser grid keeps them cheap.
        void FindLabelAnchors(Color32[] pixels, float[] countryDistance, out Vector2[] anchors, out float[] radii)
        {
            const int Step = 4; int size = Resolution / Step, count = size * size, countries = MapLayout.Countries.Length;
            var room = new float[count]; var label = new int[count];
            for (int z = 0; z < size; z++) for (int x = 0; x < size; x++)
            {
                int source = (z * Step + Step / 2) * Resolution + x * Step + Step / 2, i = z * size + x;
                label[i] = pixels[source].a < 128 ? -1 : pixels[source].b - 1;
                room[i] = label[i] < 0 ? 0 : float.MaxValue;
            }
            Chamfer(room, size, size);
            anchors = new Vector2[countries]; radii = new float[countries];
            var best = new float[countries]; var at = new int[countries];
            for (int c = 0; c < countries; c++) { best[c] = -1; at[c] = -1; }
            for (int z = 0; z < size; z++) for (int x = 0; x < size; x++)
            {
                int i = z * size + x, c = label[i]; if (c < 0 || c >= countries) continue;
                float border = countryDistance[(z * Step + Step / 2) * Resolution + x * Step + Step / 2] / Step;
                float free = room[i] < border ? room[i] : border;
                if (free > best[c]) { best[c] = free; at[c] = i; }
            }
            float texel = Bounds.z / size;
            for (int c = 0; c < countries; c++)
            {
                if (at[c] < 0) { anchors[c] = new Vector2(float.NaN, float.NaN); continue; }
                int x = at[c] % size, z = at[c] / size;
                anchors[c] = new Vector2(Bounds.x + (x + .5f) * texel, Bounds.y + (z + .5f) / size * Bounds.w);
                radii[c] = best[c] * texel;
            }
        }
    }
}
