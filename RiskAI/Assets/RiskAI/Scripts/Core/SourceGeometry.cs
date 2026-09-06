namespace RiskAI.Core
{
    /// <summary>
    /// Source collision sizes verified in Resources/Maps/SourceGeometry.json.
    /// They configure physical NavMesh separation only; visual/model bounds and
    /// picking remain independent; standing MDX dimensions are recorded separately.
    /// </summary>
    public static class SourceGeometry
    {
        public const float NativePerUnity = 50f;
        const float LocalUnverifiedRadius = .24f;

        // Main Stand-sequence Z extents from the owned RoC MDX, modelScale=1.
        // Zero means a local adaptation without verified source model bounds.
        public static float StandingHeight(UnitKind kind)
        {
            switch(kind)
            {
                case UnitKind.Archer:return 81.837f/NativePerUnity;
                case UnitKind.Medic:return 122.367f/NativePerUnity;
                case UnitKind.Guard:return 152.895f/NativePerUnity;
                case UnitKind.Mortar:return 80.253f/NativePerUnity;
                default:return 0;
            }
        }
        public static float StandingWidth(UnitKind kind)
        {
            switch(kind)
            {
                case UnitKind.Archer:return 94.895f/NativePerUnity;
                case UnitKind.Medic:return 94.414f/NativePerUnity;
                case UnitKind.Guard:return 148.094f/NativePerUnity;
                case UnitKind.Mortar:return 113.644f/NativePerUnity;
                default:return 0;
            }
        }
        public static float AgentRadius(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Archer: return 16f / NativePerUnity; // h00B / hrif ucol=16
                case UnitKind.Medic: return 16f / NativePerUnity;  // h00E / hmpr inherited ucol=16
                case UnitKind.Guard: return 32f / NativePerUnity;  // h00G / hkni inherited ucol=32
                case UnitKind.Mortar: return 32f / NativePerUnity; // h00H / hmtm inherited ucol=32
                default: return LocalUnverifiedRadius;
            }
        }
    }
}
