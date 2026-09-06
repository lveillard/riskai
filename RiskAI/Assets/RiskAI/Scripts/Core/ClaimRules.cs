using System;
namespace RiskAI.Core
{
    public static class ClaimRules
    {
        public const float CircleRadius = 1.55f;
        public const float ProtectionRadius = 4.43f;
        public const float TakeoverRadius = 6f;
        public static bool IsValidOwner(int owner) => owner == PlayerRules.NeutralOwner || PlayerRules.IsPlayer(owner);

        /// <summary>
        /// Compares two possible post-defender successors without depending on Unity objects.
        /// A living unit owned by the current town owner always wins over an opposing unit;
        /// within the same tier, the nearest unit wins and EntityId makes ties deterministic.
        /// An EntityId of zero means that no candidate has been selected yet.
        /// </summary>
        public static bool BetterCandidate(
            int ownerTeam,
            int candidateTeam,
            float candidateDistanceSquared,
            int candidateEntityId,
            int bestTeam,
            float bestDistanceSquared,
            int bestEntityId)
        {
            if (candidateEntityId <= 0 || float.IsNaN(candidateDistanceSquared) || float.IsInfinity(candidateDistanceSquared))
                return false;
            if (bestEntityId <= 0 || float.IsNaN(bestDistanceSquared) || float.IsInfinity(bestDistanceSquared))
                return true;

            bool candidateIsOwner = ownerTeam >= 0 && candidateTeam == ownerTeam;
            bool bestIsOwner = ownerTeam >= 0 && bestTeam == ownerTeam;
            if (candidateIsOwner != bestIsOwner)
                return candidateIsOwner;

            const float DistanceTolerance = 0.00001f;
            if (candidateDistanceSquared < bestDistanceSquared - DistanceTolerance)
                return true;
            if (candidateDistanceSquared > bestDistanceSquared + DistanceTolerance)
                return false;
            return candidateEntityId < bestEntityId;
        }
    }
}
