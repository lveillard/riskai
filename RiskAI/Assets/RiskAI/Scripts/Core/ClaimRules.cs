using System;
namespace RiskAI.Core
{
    public static class ClaimRules
    {
        public const float CircleRadius = 1.1f;
        public const float ProtectionRadius = 2.8f;
        public const float ConversionSeconds = 1.25f;
    }
    /// <summary>Continuous, uncontested occupation by the same entity is required.</summary>
    public sealed class ClaimTransition
    {
        public int CandidateId { get; private set; }
        public int CandidateTeam { get; private set; } = -1;
        public float Elapsed { get; private set; }
        public float Progress => Math.Min(1, Elapsed / ClaimRules.ConversionSeconds);
        public bool Advance(int entityId, int team, bool blocked, float delta)
        {
            if (delta < 0 || float.IsNaN(delta) || float.IsInfinity(delta)) throw new ArgumentOutOfRangeException(nameof(delta));
            if (blocked || entityId <= 0 || team < 0) { Reset(); return false; }
            if (CandidateId != entityId || CandidateTeam != team)
            { Reset(); CandidateId = entityId; CandidateTeam = team; }
            Elapsed += delta;
            return Elapsed + .00001f >= ClaimRules.ConversionSeconds;
        }
        public void Reset() { CandidateId = 0; CandidateTeam = -1; Elapsed = 0; }
    }
}
