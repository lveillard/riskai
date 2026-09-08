using UnityEngine;

namespace RiskAI
{
    public sealed partial class BattleHud
    {
        const float MinimapMarkerInterval = .1f;
        Texture2D minimapMarkers;
        MinimapMarkerRaster minimapRaster;
        Vector2 minimapMarkerLogicalSize;
        float nextMinimapMarkerRefresh;

        // Called only while the map is visible. Marker upload is bounded at 10 Hz;
        // the camera outline and the existing hit overlay still update every frame.
        void DrawMinimapMarkers(Rect rect)
        {
            RefreshMinimapMarkers(rect, Time.unscaledTime);
            Color previous = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(rect, minimapMarkers, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        void RefreshMinimapMarkers(Rect rect, float now)
        {
            int pixelWidth = Mathf.Clamp(Mathf.CeilToInt(rect.width * Scale), 1, 1024);
            int pixelHeight = Mathf.Clamp(Mathf.CeilToInt(rect.height * Scale), 1, 1024);
            if (!minimapMarkers || minimapMarkers.width != pixelWidth || minimapMarkers.height != pixelHeight || minimapMarkerLogicalSize != rect.size)
            {
                DisposeMinimapMarkers();
                minimapMarkers = new Texture2D(pixelWidth, pixelHeight, TextureFormat.RGBA32, false) {
                    name = "Minimap marker overlay", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
                };
                minimapRaster = new MinimapMarkerRaster(pixelWidth, pixelHeight, rect.size);
                minimapMarkerLogicalSize = rect.size;
                nextMinimapMarkerRefresh = float.NegativeInfinity;
            }
            if (now < nextMinimapMarkerRefresh) return;
            nextMinimapMarkerRefresh = now + MinimapMarkerInterval;
            minimapRaster.Clear();
            var local = new Rect(Vector2.zero, rect.size);
            float townSize = Mathf.Clamp(35f / Mathf.Sqrt(MapLayout.Towns.Length), 2, 8);
            foreach (var town in session.Towns)
            {
                var point = MapPoint(town.transform.position, local);
                minimapRaster.Fill(new Rect(point.x - townSize * .5f, point.y - townSize * .5f, townSize, townSize), VisualFactory.TeamColor(town.State.Owner));
                if (town.Defense.IsAlive)
                    minimapRaster.Outline(new Rect(point.x - townSize * .5f - 2, point.y - townSize * .5f - 2, townSize + 4, townSize + 4), new Color(.83f, .76f, .48f));
            }
            foreach (var unit in session.Units)
            {
                if (!unit || !unit.IsAlive) continue;
                var point = MapPoint(unit.transform.position, local);
                minimapRaster.Fill(new Rect(point.x - 1, point.y - 1, 2.5f, 2.5f), unit.Selected ? Color.white : VisualFactory.TeamColor(unit.Team));
            }
            if (session.Naval)
            {
                foreach (var harbor in session.Naval.Harbors)
                {
                    var point = MapPoint(harbor.Landing, local);
                    minimapRaster.Outline(new Rect(point.x - 3, point.y - 3, 6, 6), VisualFactory.TeamColor(harbor.Owner));
                }
                foreach (var ship in session.Naval.Ships)
                {
                    if (!ship || !ship.IsAlive) continue;
                    var point = MapPoint(ship.transform.position, local);
                    minimapRaster.Fill(new Rect(point.x - 2, point.y - 2, 4, 4), ship.Selected ? Color.white : VisualFactory.TeamColor(ship.Team));
                }
            }
            minimapMarkers.SetPixels32(minimapRaster.Pixels);
            minimapMarkers.Apply(false, false);
        }

        void DisposeMinimapMarkers()
        {
            if (minimapMarkers) Destroy(minimapMarkers);
            minimapMarkers = null;
            minimapRaster = null;
        }
    }
}
