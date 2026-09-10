using UnityEngine;

namespace RiskAI
{
    public static class VisualMetrics
    {
        public const float UnitScale = .46f;
        public const float TownScale = .72f;
        public const float TowerScale = .70f;
        public const float UnitHeight = 1.4f;
        public static float HeightFor(Core.UnitKind kind) => Core.SourceGeometry.StandingHeight(kind)>0?Core.SourceGeometry.StandingHeight(kind):UnitHeight;
        public static float RadiusFor(Core.UnitKind kind) => Core.SourceGeometry.StandingWidth(kind)>0?Core.SourceGeometry.StandingWidth(kind)*.5f:UnitRadius;
        // Stable gameplay presentation footprint. It deliberately does not inspect
        // the active renderer, so switching to the strategic proxy cannot collapse
        // freshly recruited units onto one point.
        public static float SpawnRadiusFor(Core.UnitKind kind) => Mathf.Clamp(RadiusFor(kind)*.72f,.58f,1.05f);
        public const float UnitRadius = .34f;
        public const float TowerHeight = 4.6f;
        public const float TowerRadius = 1.05f;
    }
}
