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
        public const float UnitRadius = .34f;
        public const float TowerHeight = 4.6f;
        public const float TowerRadius = 1.05f;
    }
}
