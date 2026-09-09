using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiskAI
{
    /// <summary>
    /// Small, own-made vegetation meshes.  This deliberately uses neither source
    /// doodad art nor physics: it only gives open ground a few low visual cues.
    /// </summary>
    public sealed class GroundCover : MonoBehaviour
    {
        const float CellSize = 64f;
        const int AuthoredClusterLimit = 1000;
        const int ImportedClusterLimit = 1500;
        const int CandidateLimit = 6000;

        static readonly Color[] Colors =
        {
            new Color(.30f, .40f, .15f),
            new Color(.39f, .46f, .18f),
            new Color(.46f, .48f, .22f),
            new Color(.43f, .37f, .16f),
            new Color(.50f, .43f, .20f),
            new Color(.34f, .34f, .14f)
        };

        readonly List<Mesh> meshes = new List<Mesh>();

        sealed class CellMesh
        {
            public readonly List<Vector3> Vertices = new List<Vector3>(256);
            public readonly List<int> Triangles = new List<int>(384);
        }

        /// <summary>Creates deterministic decorative cover after towns, camps, and ports exist.</summary>
        public static GroundCover Create(BattleSession session, Transform parent)
        {
            if (!session || !parent) return null;
            var root = new GameObject("Original meadow cover");
            root.transform.SetParent(parent, false);
            var cover = root.AddComponent<GroundCover>();
            cover.Build(session);
            return cover;
        }

        void Build(BattleSession session)
        {
            int target = MapLayout.IsImported ? ImportedClusterLimit : AuthoredClusterLimit;
            // The map identity, rather than the match seed, makes this visual repeatable
            // without consuming or coupling to combat/allocation random streams.
            var random = new System.Random(unchecked(0x47524356 ^ ((int)MapLayout.Scenario + 1) * 0x45d9f3b));
            var groups = new Dictionary<long, CellMesh>();
            Vector2 min = MapLayout.PlayableMin + Vector2.one * 3f;
            Vector2 max = MapLayout.PlayableMax - Vector2.one * 3f;
            int placed = 0;
            for (int attempt = 0; attempt < CandidateLimit && placed < target; attempt++)
            {
                float x = Mathf.Lerp(min.x, max.x, Next(random));
                float z = Mathf.Lerp(min.y, max.y, Next(random));
                if (!AcceptPatch(x, z) || !SuitableGround(x, z) || !ClearOfPosts(session, x, z)) continue;

                bool dry=MapLayout.Scenario==ScenarioMap.Classic&&z<-43*MapLayout.Spacing;
                int material = (dry?3:0)+random.Next(3);
                int cellX = Mathf.FloorToInt(x / CellSize), cellZ = Mathf.FloorToInt(z / CellSize);
                long key = CellKey(cellX, cellZ, material);
                if (!groups.TryGetValue(key, out var mesh)) groups.Add(key, mesh = new CellMesh());
                AddCluster(mesh, random, x, z);
                placed++;
            }

            foreach (var pair in groups)
            {
                var source = pair.Value;
                if (source.Vertices.Count == 0) continue;
                int material = (int)(pair.Key & 7L);
                var mesh = new Mesh { name = "Original meadow cover mesh" };
                if (source.Vertices.Count > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(source.Vertices);
                mesh.SetTriangles(source.Triangles, 0);
                var normals=new Vector3[source.Vertices.Count];
                for(int i=0;i<normals.Length;i++)normals[i]=Vector3.up;
                mesh.normals=normals;
                mesh.RecalculateBounds();
                meshes.Add(mesh);

                var go = new GameObject("Meadow cover " + material);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = VisualFactory.Mat(Colors[material]);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }
            name = "Original meadow cover · " + placed + " clusters · " + groups.Count + " cells";
        }

        static float Next(System.Random random) => (float)random.NextDouble();

        static long CellKey(int x, int z, int material)
            => ((long)x << 35) ^ ((long)(uint)z << 3) ^ (uint)material;

        static bool AcceptPatch(float x, float z)
        {
            float noise = Mathf.PerlinNoise(x * .075f + 17.3f * ((int)MapLayout.Scenario + 1), z * .075f + 9.7f);
            // Narrow absences make irregular meadows while keeping the cover sparse
            // and leaving enough possible placements for the source-scale maps.
            if(MapLayout.Scenario==ScenarioMap.Classic&&z<-43*MapLayout.Spacing)return noise>=.44f&&noise<=.86f;
            return noise >= .32f && noise <= .94f;
        }

        static bool SuitableGround(float x, float z)
        {
            if (!MapLayout.IsLand(x, z)) return false;
            const float probe = .65f;
            float height = MapLayout.Height(x, z);
            if (Mathf.Abs(MapLayout.Height(x + probe, z) - height) > .43f ||
                Mathf.Abs(MapLayout.Height(x - probe, z) - height) > .43f ||
                Mathf.Abs(MapLayout.Height(x, z + probe) - height) > .43f ||
                Mathf.Abs(MapLayout.Height(x, z - probe) - height) > .43f)
                return false;
            return true;
        }

        static bool ClearOfPosts(BattleSession session, float x, float z)
        {
            const float cityRadius = 8f;
            const float campRadius = 5f;
            float citySquared = cityRadius * cityRadius;
            foreach (var town in session.Towns)
            {
                if (!town) continue;
                if (DistanceSquared(x, z, town.transform.position) < citySquared ||
                    DistanceSquared(x, z, town.ClaimPoint) < citySquared)
                    return false;
            }
            if (session.Naval != null)
                foreach (var harbor in session.Naval.Harbors)
                    if (harbor && DistanceSquared(x, z, harbor.Landing) < citySquared)
                        return false;
            float campSquared = campRadius * campRadius;
            foreach (var camp in session.Camps)
                if (camp && DistanceSquared(x, z, camp.SpawnPoint) < campSquared)
                    return false;
            return true;
        }

        static float DistanceSquared(float x, float z, Vector3 point)
        {
            float dx = x - point.x, dz = z - point.z;
            return dx * dx + dz * dz;
        }

        static void AddCluster(CellMesh mesh, System.Random random, float x, float z)
        {
            int bladeCount = random.Next(4, 9);
            bool shrublet = random.Next(7) == 0;
            for (int blade = 0; blade < bladeCount; blade++)
            {
                float angle = Next(random) * Mathf.PI * 2f;
                float radius = Mathf.Sqrt(Next(random)) * .52f;
                float width = Mathf.Lerp(.08f, .18f, Next(random));
                float height = shrublet && blade == bladeCount - 1 ? .8f : Mathf.Lerp(.25f, .65f, Next(random));
                float lean = Mathf.Lerp(.015f, .13f, Next(random));
                float bx = x + Mathf.Cos(angle) * radius, bz = z + Mathf.Sin(angle) * radius;
                AddBlade(mesh, bx, bz, width, height, angle + Mathf.PI * .5f, lean);
            }
        }

        static void AddBlade(CellMesh mesh, float x, float z, float width, float height, float angle, float lean)
        {
            Vector3 side = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (width * .5f);
            Vector3 forward = new Vector3(-side.z, 0, side.x).normalized * lean;
            float y = MapLayout.Height(x, z) + .018f;
            Vector3 basePoint = new Vector3(x, y, z);
            Vector3 tip = basePoint + Vector3.up * height + forward;
            int index = mesh.Vertices.Count;
            mesh.Vertices.Add(basePoint - side);
            mesh.Vertices.Add(basePoint + side);
            mesh.Vertices.Add(tip + side * .18f);
            mesh.Vertices.Add(tip - side * .18f);
            mesh.Triangles.Add(index); mesh.Triangles.Add(index + 1); mesh.Triangles.Add(index + 2);
            mesh.Triangles.Add(index); mesh.Triangles.Add(index + 2); mesh.Triangles.Add(index + 3);
            mesh.Triangles.Add(index + 2); mesh.Triangles.Add(index + 1); mesh.Triangles.Add(index);
            mesh.Triangles.Add(index + 3); mesh.Triangles.Add(index + 2); mesh.Triangles.Add(index);
        }

        void OnDestroy()
        {
            foreach (var mesh in meshes) if (mesh) Destroy(mesh);
            meshes.Clear();
        }
    }
}
