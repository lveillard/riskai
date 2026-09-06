using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// Optional, runtime-only relief for imported maps. It retains source metres
    /// and XY coordinates, changes neither water nor shore samples, and leaves
    /// wide clear ground around every imported gameplay anchor.
    /// </summary>
    public static class ImportedLandscapeAugment
    {
        const float MaximumLift = 5f;
        // A 24 m half-width keeps the added 5 m ridge below a 35 degree grade.
        const float RidgeHalfWidth = 24f;
        const float AnchorClearRadius = 8f;
        const float AnchorBlendRadius = 20f;
        static readonly float MaximumAxisRisePerMetre = Mathf.Tan(35f * Mathf.Deg2Rad) / Mathf.Sqrt(2f);
        static readonly ConditionalWeakTable<ImportedMapData, object> Applied = new ConditionalWeakTable<ImportedMapData, object>();
        static readonly object AppliedMarker = new object();

        /// <summary>Menu/setup code may set this before loading a map.</summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// Raises only fully inland source samples. Calling it repeatedly on the
        /// same data is harmless; JSON resources remain unmodified.
        /// </summary>
        public static bool Apply(ImportedMapData data)
        {
            if (!Enabled || data == null || !Supports(data) || Applied.TryGetValue(data, out _)) return false;
            if (data.heightSamples == null || data.landSamples == null ||
                data.heightSamples.Length != data.width * data.height || data.landSamples.Length != data.width * data.height)
                throw new ArgumentException("Imported landscape data must be validated before augmentation.", nameof(data));

            var original = (float[])data.heightSamples.Clone();
            var raised = (float[])data.heightSamples.Clone();
            for (int z = 0; z < data.height; z++)
            for (int x = 0; x < data.width; x++)
            {
                if (!IsInland(data, x, z)) continue;
                float worldX = data.originX + x * data.cellSize;
                float worldZ = data.originZ + z * data.cellSize;
                float ridge = RidgeReliefAt(data, worldX, worldZ);
                if (ridge <= 0) continue;
                float lift = ridge * GameplayAnchorMask(data, worldX, worldZ);
                if (lift > 0) raised[z * data.width + x] += lift;
            }
            LimitAddedGrade(data, original, raised);
            Array.Copy(raised, data.heightSamples, raised.Length);
            Applied.Add(data, AppliedMarker);
            return true;
        }

        /// <summary>0..1 tint weight for an imported-ground rock/snow shader.</summary>
        public static float RockSnowWeightAt(ImportedMapData data, float x, float z)
        {
            return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(MaximumLift * .30f, MaximumLift, ReliefAt(data, x, z)));
        }

        static bool Supports(ImportedMapData data)
        {
            return string.Equals(data.mapId, "Europe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(data.mapId, "NewWorld", StringComparison.OrdinalIgnoreCase);
        }

        static float ReliefAt(ImportedMapData data, float x, float z)
        {
            float ridge = RidgeReliefAt(data, x, z);
            return ridge <= 0 ? 0 : ridge * GameplayAnchorMask(data, x, z);
        }

        static float RidgeReliefAt(ImportedMapData data, float x, float z)
        {
            if (data == null || !Supports(data)) return 0;
            float offset = string.Equals(data.mapId, "NewWorld", StringComparison.OrdinalIgnoreCase) ? 163.84f : 0;
            return Mathf.Max(
                RidgeHeight(x, z, -115 + offset, -96, -36 + offset, -92),
                RidgeHeight(x, z, -208 + offset, -136, -167 + offset, -140));
        }

        static float RidgeHeight(float x, float z, float ax, float az, float bx, float bz)
        {
            float distance = DistanceToSegment(x, z, ax, az, bx, bz);
            return MaximumLift * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(RidgeHalfWidth, 0, distance));
        }

        static float GameplayAnchorMask(ImportedMapData data, float x, float z)
        {
            float nearest = float.PositiveInfinity;
            if (data.cities != null)
                foreach (var city in data.cities)
                {
                    nearest = Mathf.Min(nearest, Distance(x, z, city.x, city.z));
                    nearest = Mathf.Min(nearest, Distance(x, z, city.claimX, city.claimZ));
                }
            if (data.countries != null)
                foreach (var country in data.countries) nearest = Mathf.Min(nearest, Distance(x, z, country.x, country.z));
            return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(AnchorClearRadius, AnchorBlendRadius, nearest));
        }

        static bool IsInland(ImportedMapData data, int x, int z)
        {
            // Keeping a one-vertex ring around every non-land sample untouched
            // preserves the original coastline and water mesh exactly.
            if (x == 0 || z == 0 || x == data.width - 1 || z == data.height - 1) return false;
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
                if (data.landSamples[(z + dz) * data.width + x + dx] == 0) return false;
            return true;
        }

        static void LimitAddedGrade(ImportedMapData data, float[] original, float[] raised)
        {
            // ImportedTerrain triangles have independent X/Z gradients. Limiting
            // each axis to tan(35°)/sqrt(2) keeps the added surface under 35°.
            // Original source slopes are never lowered or otherwise rewritten.
            float limit = MaximumAxisRisePerMetre * data.cellSize;
            for (int pass = 0; pass < 3; pass++)
            for (int z = 0; z < data.height; z++)
            for (int x = 0; x < data.width; x++)
            {
                int index = z * data.width + x;
                if (raised[index] <= original[index]) continue;
                float ceiling = float.PositiveInfinity;
                if (x > 0) ceiling = Mathf.Min(ceiling, raised[index - 1] + limit);
                if (x + 1 < data.width) ceiling = Mathf.Min(ceiling, raised[index + 1] + limit);
                if (z > 0) ceiling = Mathf.Min(ceiling, raised[index - data.width] + limit);
                if (z + 1 < data.height) ceiling = Mathf.Min(ceiling, raised[index + data.width] + limit);
                raised[index] = Mathf.Max(original[index], Mathf.Min(raised[index], ceiling));
            }
        }

        static float Distance(float x, float z, float otherX, float otherZ)
        {
            float dx = x - otherX, dz = z - otherZ;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static float DistanceToSegment(float x, float z, float ax, float az, float bx, float bz)
        {
            float dx = bx - ax, dz = bz - az;
            float lengthSquared = dx * dx + dz * dz;
            float t = lengthSquared > 0 ? Mathf.Clamp01(((x - ax) * dx + (z - az) * dz) / lengthSquared) : 0;
            return Distance(x, z, ax + dx * t, az + dz * t);
        }
    }
}
