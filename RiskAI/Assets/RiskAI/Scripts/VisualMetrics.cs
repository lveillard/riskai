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
    }
}
