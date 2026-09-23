namespace RiskAI
{
    public static class ShipPortrait
    {
        // New v0.30 hulls fall back to the Galley/Transport portrait until the art setup renders theirs.
        public static string Resource(ShipKind kind) => UnitVariantViews.PortraitResource(kind);
    }
}
