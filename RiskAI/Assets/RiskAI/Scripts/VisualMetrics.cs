using UnityEngine;

namespace RiskAI
{
    public static class VisualMetrics
    {
        public const float UnitScale = .46f;
        public const float TownScale = .72f;
        public const float TowerScale = .70f;
        // Integrated keep (TowerArt): the fighting platform is the aim point and
        // bolts leave from just above the parapet. Range and damage are unaffected.
        public const float IntegratedTowerGalleryHeight = TowerArt.WalkHeight * TowerScale;
        public const float IntegratedTowerAttackHeight = TowerArt.ParapetTop * TowerScale;
        /// <summary>Roof tip of the integrated keep, used for picking height.</summary>
        public const float IntegratedTowerTopHeight = (TowerArt.RoofBase + TowerArt.RoofHeight) * TowerScale;
        public static float BuildingLabelHeight(BuildingVariant variant) =>
            BuildingVariants.IsIntegrated(variant) ? TowerArt.MastTop * TowerScale + .25f : 4.8f;
        public const float UnitHeight = 1.4f;
        public static float HeightFor(Core.UnitKind kind) => StandingHeightTarget(kind)>0?StandingHeightTarget(kind):UnitHeight;
        public static float RadiusFor(Core.UnitKind kind) => StandingWidthTarget(kind)>0?StandingWidthTarget(kind)*.5f:UnitRadius;
        // Verified MDX extents first; v0.30 roster art has no owned MDX bounds, so it uses a
        // local visual target relative to its base unit's verified model (roughly by ucol).
        public static float StandingHeightTarget(Core.UnitKind kind)
        {
            float source=Core.SourceGeometry.StandingHeight(kind);if(source>0)return source;
            switch(kind)
            {
                case Core.UnitKind.EliteRifleman:return Core.SourceGeometry.StandingHeight(Core.UnitKind.Archer)*1.1f;
                case Core.UnitKind.Roarer:return Core.SourceGeometry.StandingHeight(Core.UnitKind.Archer)*1.12f; // war herald on the Rogue base
                case Core.UnitKind.ArmyGeneral:return Core.SourceGeometry.StandingHeight(Core.UnitKind.Guard)*1.12f;
                // Mounted marines (h014/h015 inherit hkni): the knight's mount, the general a little larger.
                case Core.UnitKind.MarineMajor:return Core.SourceGeometry.StandingHeight(Core.UnitKind.Guard);
                case Core.UnitKind.MarineGeneral:return Core.SourceGeometry.StandingHeight(Core.UnitKind.Guard)*1.05f;
                case Core.UnitKind.Artillery:return 1.95f;
                case Core.UnitKind.Tank:return 2.3f;
                default:return 0;
            }
        }
        public static float StandingWidthTarget(Core.UnitKind kind)
        {
            float source=Core.SourceGeometry.StandingWidth(kind);if(source>0)return source;
            switch(kind)
            {
                case Core.UnitKind.EliteRifleman:return Core.SourceGeometry.StandingWidth(Core.UnitKind.Archer)*1.1f;
                case Core.UnitKind.Roarer:return Core.SourceGeometry.StandingWidth(Core.UnitKind.Archer)*1.12f;
                case Core.UnitKind.ArmyGeneral:return Core.SourceGeometry.StandingWidth(Core.UnitKind.Guard)*1.12f;
                case Core.UnitKind.MarineMajor:return Core.SourceGeometry.StandingWidth(Core.UnitKind.Guard);
                case Core.UnitKind.MarineGeneral:return Core.SourceGeometry.StandingWidth(Core.UnitKind.Guard)*1.05f;
                case Core.UnitKind.Artillery:return 2.7f;
                case Core.UnitKind.Tank:return 2.9f;
                default:return 0;
            }
        }
        // Stable gameplay presentation footprint. It deliberately does not inspect
        // the active renderer, so switching to the strategic proxy cannot collapse
        // freshly recruited units onto one point.
        public static float SpawnRadiusFor(Core.UnitKind kind) => Mathf.Clamp(RadiusFor(kind)*.72f,.58f,1.05f);
        public const float UnitRadius = .34f;
        public const float TowerHeight = 4.6f;
        public const float TowerRadius = 1.05f;
    }
}
