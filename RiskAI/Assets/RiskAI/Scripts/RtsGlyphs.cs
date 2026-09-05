using System.Collections.Generic;
using UnityEngine;

namespace RiskAI
{
    // Small original command illustrations, rendered once and reused by the HUD.
    public static class RtsGlyphs
    {
        static readonly Dictionary<int, Texture2D> icons = new Dictionary<int, Texture2D>();
        public static Texture2D Get(int kind)
        {
            if (icons.TryGetValue(kind, out var existing) && existing) return existing;
            var pixels = new Color[64 * 64];
            Color ink = kind == 2 ? new Color(1, .55f, .36f) : new Color(.92f, .83f, .59f);
            void Line(float ax, float ay, float bx, float by, float thickness = 4)
            {
                var a = new Vector2(ax, ay); var b = new Vector2(bx, by); var v = b - a;
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                {
                    var p = new Vector2(x, y); float t = Mathf.Clamp01(Vector2.Dot(p - a, v) / Mathf.Max(.001f, v.sqrMagnitude));
                    float alpha = Mathf.Clamp01(thickness / 2 + .75f - Vector2.Distance(p, a + v * t));
                    if (alpha > pixels[y * 64 + x].a) pixels[y * 64 + x] = new Color(ink.r, ink.g, ink.b, alpha);
                }
            }
            void Arrow(float ax, float ay, float bx, float by)
            {
                Line(ax, ay, bx, by, 5); var d = new Vector2(ax - bx, ay - by).normalized;
                var n = new Vector2(-d.y, d.x);
                Line(bx, by, bx + d.x * 13 + n.x * 10, by + d.y * 13 + n.y * 10, 5);
                Line(bx, by, bx + d.x * 13 - n.x * 10, by + d.y * 13 - n.y * 10, 5);
            }
            void Box(float x, float y, float w, float h)
            { Line(x,y,x+w,y); Line(x+w,y,x+w,y+h); Line(x+w,y+h,x,y+h); Line(x,y+h,x,y); }
            switch (kind)
            {
                case 0: Arrow(10,14,51,48); break;
                case 1: Box(15,15,34,34); Line(23,31,41,31,7); break;
                case 2: Arrow(13,13,49,49); Arrow(51,13,15,49); Line(12,23,23,12); Line(41,12,52,23); break;
                case 3: Line(13,51,51,51); Line(13,51,15,29); Line(15,29,32,10); Line(32,10,49,29); Line(49,29,51,51); Line(32,20,32,43); break;
                case 4: Arrow(12,43,51,43); Arrow(51,20,12,20); break;
                case 5: Box(8,11,13,17); Box(25,21,14,20); Box(43,11,13,17); Line(14,36,14,41,8); Line(32,49,32,54,9); Line(49,36,49,41,8); break;
                case 6: Box(24,24,16,16); Line(8,20,8,8); Line(8,8,20,8); Line(44,8,56,8); Line(56,8,56,20); Line(56,44,56,56); Line(56,56,44,56); Line(20,56,8,56); Line(8,56,8,44); break;
                case 7: Arrow(53,32,12,32); Line(44,13,54,13); Line(54,13,54,51); Line(54,51,44,51); break;
                default: Box(14,12,36,34); Box(25,12,14,18); Line(12,47,52,47); Line(15,47,15,55,6); Line(32,47,32,55,6); Line(49,47,49,55,6); break;
            }
            var texture = new Texture2D(64,64,TextureFormat.RGBA32,false) { name = "RTS command " + kind, filterMode = FilterMode.Bilinear };
            texture.SetPixels(pixels); texture.Apply(false,true); icons[kind] = texture; return texture;
        }
    }
}
