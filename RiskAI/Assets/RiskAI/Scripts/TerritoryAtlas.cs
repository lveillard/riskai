using System.Collections.Generic;
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
        public readonly Texture2D Regions, Palette;
        public readonly Vector4 Bounds;
        public TerritoryField Field { get; }
        readonly Color32[] palette;
        const int Resolution = 1024;
        // An independent harbour (authored maps) shows its own owner on a small patch of shore.
        const float PortPatchRadius = 4.5f;

        public TerritoryAtlas(BattleSession session)
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
            float patch = PortPatchRadius * PortPatchRadius;
            for (int z = 0; z < Resolution; z++) for (int x = 0; x < Resolution; x++)
            {
                int i = z * Resolution + x;
                float wx = min.x + (x + .5f) / Resolution * Bounds.z, wz = min.y + (z + .5f) / Resolution * Bounds.w;
                if (!Field.TryUniformLand(wx, wz, out bool land)) land = MapLayout.IsLand(wx, wz);
                if (Sites.Count == 0 || !land) { pixels[i] = new Color32(0, 0, 0, 0); continue; }
                Field.Sample(wx, wz, out int country, out int city);
                int site = city >= 0 && city < session.Towns.Count ? city : 0;
                var point = new Vector2(wx, wz);
                foreach (int port in patches) if ((Sites[port].Point - point).sqrMagnitude < patch) { site = port; break; }
                pixels[i] = new Color32((byte)(site & 255), (byte)(site >> 8), (byte)(country + 1), 255);
            }
            Regions = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false, true)
                { name = "Territory IDs and coast", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            Regions.SetPixels32(pixels); Regions.Apply(false, true);
            palette = new Color32[Mathf.NextPowerOfTwo(Mathf.Max(2, Sites.Count))];
            Palette = new Texture2D(palette.Length, 1, TextureFormat.RGBA32, false, false)
                { name = "Live territory ownership", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            RefreshOwners();
        }
        public void RefreshOwners()
        {
            bool changed = false;
            for (int i = 0; i < Sites.Count; i++)
            {
                Color32 next = VisualFactory.TeamColor(Sites[i].Owner);
                if (!palette[i].Equals(next)) { palette[i] = next; changed = true; }
            }
            if (changed) { Palette.SetPixels32(palette); Palette.Apply(false, false); }
        }
        public void Dispose() { Object.Destroy(Regions); Object.Destroy(Palette); }
    }
}
