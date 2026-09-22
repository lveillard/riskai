namespace RiskAI
{
    /// <summary>Presentation/footprint family. Map provenance does not determine the variant.</summary>
    public enum BuildingVariant
    {
        DetachedTown,
        IntegratedTown,
        PierHarbor,
        IntegratedHarbor
    }

    public static class BuildingVariants
    {
        public static bool IsHarbor(BuildingVariant variant) =>
            variant == BuildingVariant.PierHarbor || variant == BuildingVariant.IntegratedHarbor;

        public static bool IsIntegrated(BuildingVariant variant) =>
            variant == BuildingVariant.IntegratedTown || variant == BuildingVariant.IntegratedHarbor;

        public static BuildingVariant DefaultFor(bool harbor) =>
            harbor ? BuildingVariant.PierHarbor : BuildingVariant.DetachedTown;

        public static bool Matches(BuildingVariant variant, bool harbor) => IsHarbor(variant) == harbor;
    }
}
