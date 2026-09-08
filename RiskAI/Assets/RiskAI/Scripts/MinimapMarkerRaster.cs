using System;
using UnityEngine;

namespace RiskAI
{
    /// <summary>Reusable transparent marker layer in minimap logical coordinates.
    /// Fractional edge coverage retains small markers when the UI scale changes.</summary>
    public sealed class MinimapMarkerRaster
    {
        public int Width { get; }
        public int Height { get; }
        public Color32[] Pixels { get; }
        readonly float scaleX, scaleY;

        public MinimapMarkerRaster(int width, int height, Vector2 logicalSize)
        {
            Width = width; Height = height;
            Pixels = new Color32[width * height];
            scaleX = width / logicalSize.x; scaleY = height / logicalSize.y;
        }

        public void Clear() => Array.Clear(Pixels, 0, Pixels.Length);

        public void Fill(Rect rect, Color color)
        {
            float left = rect.xMin * scaleX, right = rect.xMax * scaleX;
            float top = rect.yMin * scaleY, bottom = rect.yMax * scaleY;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(left)), x1 = Mathf.Min(Width, Mathf.CeilToInt(right));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(top)), y1 = Mathf.Min(Height, Mathf.CeilToInt(bottom));
            Color32 opaque = color;
            for (int y = y0; y < y1; y++)
            {
                float vertical = Mathf.Min(y + 1, bottom) - Mathf.Max(y, top);
                for (int x = x0; x < x1; x++)
                {
                    float coverage = vertical * (Mathf.Min(x + 1, right) - Mathf.Max(x, left));
                    int index = (Height - 1 - y) * Width + x; // GUI top-down, texture bottom-up.
                    float alpha = color.a * coverage;
                    if (alpha >= .99999f) { Pixels[index] = opaque; continue; }
                    if (alpha <= 0) continue;
                    Color previous = Pixels[index];
                    float remaining = previous.a * (1 - alpha), combined = alpha + remaining;
                    Pixels[index] = new Color((color.r * alpha + previous.r * remaining) / combined,
                        (color.g * alpha + previous.g * remaining) / combined,
                        (color.b * alpha + previous.b * remaining) / combined, combined);
                }
            }
        }

        public void Outline(Rect rect, Color color)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, 1), color);
            Fill(new Rect(rect.x, rect.yMax, rect.width, 1), color);
            Fill(new Rect(rect.x, rect.y, 1, rect.height), color);
            Fill(new Rect(rect.xMax, rect.y, 1, rect.height), color);
        }
    }
}
