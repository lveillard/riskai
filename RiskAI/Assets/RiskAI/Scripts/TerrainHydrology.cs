using System.Collections.Generic;
using UnityEngine;

namespace RiskAI
{
    // A single smooth course drives erosion, water, vegetation and the bank material.
    public static class TerrainHydrology
    {
        static readonly Vector3[] ClassicCourse = { new(54,14.2f,29),new(50,9.2f,33),new(46,6,36),new(43,3.5f,40),new(38,1.4f,43),new(35,.12f,47),new(33,-.24f,51),new(29,-.24f,58) };
        static readonly Vector3[] ExpandedCourse = { new(8,5.2f,-82),new(4,4.6f,-62),new(10,3.7f,-42),new(1,2.7f,-22),new(7,1.5f,0),new(-3,.5f,22),new(5,-.12f,47),new(1,-.24f,69) };
        public static Vector3[] Course { get; private set; } = ClassicCourse;
        static Vector3[] samples;
        static bool buildingSamples;
        public static Vector3[] Samples { get { EnsureSamples(); return samples; } }
        public static Vector3[] CrossingPoints { get { EnsureSamples(); return new[] { CrossingPoint(28, .24f), CrossingPoint(40, .18f) }; } }

        public static void Configure(bool expanded)
        {
            Course = expanded ? ExpandedCourse : ClassicCourse;
            samples = null;
        }
        static void EnsureSamples()
        {
            if (samples != null || buildingSamples) return;
            buildingSamples = true;
            samples = BuildSamples();
            buildingSamples = false;
        }
        public static Vector3 Point(int i) => new Vector3(Course[i].x * MapLayout.Spacing, Course[i].y, Course[i].z * MapLayout.Spacing);
        static float Tangent(float a, float b) => a * b <= 0 ? 0 : 2 * a * b / (a + b);
        static Vector3[] BuildSamples()
        {
            var result = new Vector3[57];
            for (int s = 0; s < 7; s++) for (int j = 0; j < 8; j++)
            {
                float t = j / 8f, t2 = t * t, t3 = t2 * t;
                var a = Point(Mathf.Max(0, s - 1)); var b = Point(s); var c = Point(s + 1); var d = Point(Mathf.Min(7, s + 2));
                var p = .5f * ((2 * b) + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3);
                float delta = c.y - b.y, m0 = s == 0 ? delta : Tangent(b.y - a.y, delta), m1 = s == 6 ? delta : Tangent(delta, d.y - c.y);
                p.y = (2 * t3 - 3 * t2 + 1) * b.y + (t3 - 2 * t2 + t) * m0 + (-2 * t3 + 3 * t2) * c.y + (t3 - t2) * m1;
                result[s * 8 + j] = p;
            }
            result[56] = Point(7); return result;
        }
        public static float Width(float t) => Mathf.Lerp(.65f, 1.9f, Mathf.SmoothStep(0, 1, t)) + 4.2f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.71f, 1f, t));
        public static void Sample(float x, float z, out float distance, out float level, out float width)
        {
            EnsureSamples(); distance = float.MaxValue; level = -.24f; width = 0;
            float minX = MapLayout.IsExpanded ? -25 : 21, maxX = MapLayout.IsExpanded ? 25 : 58, minZ = MapLayout.IsExpanded ? -86 : 25, maxZ = MapLayout.IsExpanded ? 74 : 65;
            if (x < minX * MapLayout.Spacing || x > maxX * MapLayout.Spacing || z < minZ * MapLayout.Spacing || z > maxZ * MapLayout.Spacing) return;
            var p = new Vector2(x, z);
            for (int i = 0; i < samples.Length - 1; i++)
            {
                var a = samples[i]; var b = samples[i + 1]; var ab = new Vector2(b.x - a.x, b.z - a.z);
                float t = Mathf.Clamp01(Vector2.Dot(p - new Vector2(a.x, a.z), ab) / ab.sqrMagnitude); float d = (p - new Vector2(a.x, a.z) - ab * t).sqrMagnitude;
                if (d >= distance) continue; distance = d; level = Mathf.Lerp(a.y, b.y, t); width = Width((i + t) / (samples.Length - 1));
            }
            distance = Mathf.Sqrt(distance);
        }
        public static float DistanceToRiver(float x, float z) { Sample(x, z, out float d, out _, out _); return d; }
        public static bool IsChannel(float x, float z)
        {
            if (!MapLayout.IsExpanded) return false;
            Sample(x, z, out float distance, out float level, out float width);
            return distance <= width * .95f;
        }
        public static float Carve(float x, float z, float height)
        {
            Sample(x, z, out float d, out float level, out float width); if (d > width + 5.5f) return height;
            if (level <= -.23f && height < level - .65f) return height;
            float shoulder = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(width * .45f, width + 1.5f, d));
            float bed = level - .65f + .23f * Mathf.Clamp01(d / Mathf.Max(width, .1f));
            float bank = level <= -.23f ? height : Mathf.Lerp(Mathf.Max(height, level + .45f), height, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(width + 1.5f, width + 5.5f, d)));
            return Mathf.Lerp(bed, bank, shoulder);
        }
        public static void Create(Transform root)
        {
            EnsureSamples();
            var points = new Vector4[samples.Length]; float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            for (int i = 0; i < samples.Length; i++)
            {
                var p = samples[i]; float width = Width(i / (float)(samples.Length - 1)); points[i] = new Vector4(p.x, p.y, p.z, width);
                minX = Mathf.Min(minX, p.x - width - 6f); minZ = Mathf.Min(minZ, p.z - width - 6f); maxX = Mathf.Max(maxX, p.x + width + 6f); maxZ = Mathf.Max(maxZ, p.z + width + 6f);
            }
            Shader.SetGlobalVectorArray("_RiskRiver", points); Shader.SetGlobalVector("_RiskRiverBounds", new Vector4(minX, minZ, maxX, maxZ));
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var flow = new List<Vector2>(); var triangles = new List<int>(); float length = 0; const int across = 8;
            for (int i = 0; i < samples.Length; i++)
            {
                var p = samples[i]; var before = samples[Mathf.Max(0, i - 1)]; var after = samples[Mathf.Min(samples.Length - 1, i + 1)]; var dir = after - before; float slope = Mathf.Abs(dir.y) / Mathf.Max(.1f, new Vector2(dir.x, dir.z).magnitude); dir.y = 0; var side = Vector3.Cross(Vector3.up, dir.normalized);
                float progress = i / (float)(samples.Length - 1), width = Width(progress) + 1.5f; if (i > 0) length += Vector3.Distance(p, before);
                for (int j = 0; j <= across; j++)
                {
                    float u = j / (float)across; v.Add(p + side * ((u * 2 - 1) * width) + Vector3.up * .006f); uv.Add(new Vector2(u, length)); flow.Add(new Vector2(slope, progress));
                    if (i >= samples.Length - 1 || j == across) continue; int k = i * (across + 1) + j, n = k + across + 1; triangles.Add(k); triangles.Add(n); triangles.Add(k + 1); triangles.Add(k + 1); triangles.Add(n); triangles.Add(n + 1);
                }
            }
            var mesh = new Mesh { name = "Continuous spring, rapids and estuary" }; mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetUVs(1, flow); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject("Río de la Sierra · cauce erosionado"); go.transform.SetParent(root, false); go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = Resources.Load<Material>("Cascade");
            for (int i = 0; i < 18; i++) { int s = 2 + i * 2; var p = samples[s]; var dir = samples[Mathf.Min(s + 1, samples.Length - 1)] - samples[s]; dir.y = 0; p += Vector3.Cross(Vector3.up, dir.normalized) * (i % 2 == 0 ? 1 : -1) * (Width(s / (float)(samples.Length - 1)) + 1.1f + (i % 3) * .4f); if (!MapLayout.IsLand(p.x, p.z)) continue; p.y = MapLayout.Height(p.x, p.z) - .13f; WorldArt.Rock(root, p, .42f + (i % 4) * .13f, 210 + i); }
        }
        public static void CreateCrossings(Transform root)
        {
            if (!MapLayout.IsExpanded) return;
            if (root && root.Find("Puente central")) return;
            EnsureSamples(); CreateCrossing(root, samples[28], samples[27], "Puente central", .24f, 6.5f, 28); CreateCrossing(root, samples[40], samples[39], "Vado norte", .18f, 5f, 40);
        }
        static Vector3 CrossingPoint(int sampleIndex, float thickness)
        {
            var point = samples[sampleIndex];
            var previous = samples[sampleIndex - 1];
            var tangent = point - previous; tangent.y = 0; if (tangent.sqrMagnitude < .01f) tangent = Vector3.forward; tangent.Normalize();
            var side = Vector3.Cross(Vector3.up, tangent); float riverWidth = Width(sampleIndex / (float)(samples.Length - 1)); float bankOffset = riverWidth + 1.25f;
            float bankA = MapLayout.Height(point.x + side.x * bankOffset, point.z + side.z * bankOffset);
            float bankB = MapLayout.Height(point.x - side.x * bankOffset, point.z - side.z * bankOffset);
            return new Vector3(point.x, Mathf.Max(bankA, bankB) + .08f - thickness * .5f, point.z);
        }
        static void CreateCrossing(Transform root, Vector3 point, Vector3 previous, string name, float thickness, float length, int sampleIndex)
        {
            var tangent = point - previous; tangent.y = 0; if (tangent.sqrMagnitude < .01f) tangent = Vector3.forward; tangent.Normalize();
            var side = Vector3.Cross(Vector3.up, tangent); float progress = sampleIndex / (float)(samples.Length - 1), riverWidth = Width(progress); float across = riverWidth * 2 + 3.2f;
            float bankOffset = riverWidth + 1.25f;
            float bankA = MapLayout.Height(point.x + side.x * bankOffset, point.z + side.z * bankOffset);
            float bankB = MapLayout.Height(point.x - side.x * bankOffset, point.z - side.z * bankOffset);
            float deckTop = Mathf.Max(bankA, bankB) + .08f;
            var bridge = VisualFactory.Shape(root, PrimitiveType.Cube, name, new Vector3(point.x, deckTop - thickness * .5f, point.z), new Vector3(across, thickness, length), new Color(.34f, .22f, .12f), true);
            bridge.layer = MapLayout.TerrainLayer;
            bridge.transform.rotation = Quaternion.LookRotation(tangent);
            var decor=new GameObject(name+" · tablones y barandillas");decor.transform.SetParent(root,false);
            for(int plank=0;plank<16;plank++)
            {
                float offset=(plank/15f-.5f)*across;
                var board=VisualFactory.Shape(decor.transform,PrimitiveType.Cube,"Tablón",new Vector3(point.x,deckTop+.025f,point.z)+side*offset,new Vector3(across/16*.94f,.05f,length),new Color(.43f+(plank%3)*.025f,.29f,.14f));
                board.transform.rotation=Quaternion.LookRotation(tangent);
            }
            for(int bank=-1;bank<=1;bank+=2)
            {
                var railPoint=new Vector3(point.x,deckTop+.85f,point.z)+tangent*bank*(length*.5f-.15f);
                var rail=VisualFactory.Shape(decor.transform,PrimitiveType.Cube,"Barandilla",railPoint,new Vector3(across,.12f,.12f),new Color(.28f,.16f,.07f));rail.transform.rotation=Quaternion.LookRotation(tangent);
                for(int post=0;post<5;post++)VisualFactory.Shape(decor.transform,PrimitiveType.Cube,"Poste",railPoint+side*((post/4f-.5f)*across)-Vector3.up*.4f,new Vector3(.16f,.9f,.16f),new Color(.28f,.16f,.07f));
            }
            StaticBatchingUtility.Combine(decor);
            CreateRamp(root, point, side, 1, bankOffset, deckTop, bankA, length, name + " · margen este");
            CreateRamp(root, point, side, -1, bankOffset, deckTop, bankB, length, name + " · margen oeste");
        }
        static void CreateRamp(Transform root, Vector3 center, Vector3 side, int sign, float length, float deckTop, float bankTop, float bridgeWidth, string name)
        {
            float rise = deckTop - bankTop;
            if (rise < .16f) return;
            const float thickness = .18f;
            float angle = Mathf.Atan2(rise, length) * Mathf.Rad2Deg;
            var ramp = VisualFactory.Shape(root, PrimitiveType.Cube, name,
                new Vector3(center.x,0,center.z) + side * sign * (length * .5f) + Vector3.up * ((deckTop + bankTop) * .5f - thickness * .5f),
                new Vector3(bridgeWidth, thickness, length), new Color(.34f, .22f, .12f), true);
            ramp.layer = MapLayout.TerrainLayer;
            ramp.transform.rotation = Quaternion.LookRotation(side * sign) * Quaternion.Euler(angle, 0, 0);
        }
    }
}
