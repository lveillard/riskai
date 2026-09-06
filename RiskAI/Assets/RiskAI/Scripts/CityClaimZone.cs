using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;
namespace RiskAI
{
    // Spatial adapter for garrison selection and pure candidate ranking.
    public sealed class CityClaimZone
    {
        public const float DefaultHalfExtent = ClaimRules.CircleRadius;
        public const float VerticalExtent = 1.25f;
        readonly List<CombatTarget> nearby = new List<CombatTarget>(32);
        public Vector3 Center { get; }
        // This is deliberately resolved once.  Every replacement defender must use
        // the same walkable point, otherwise NavMesh sampling around a sloped ring
        // can make a hand-off look like the guard has moved off the post.
        Vector3 garrisonAnchor;
        bool hasGarrisonAnchor;
        public float HalfExtent { get; }
        public Soldier Defender { get; private set; }
        public bool Contested { get; private set; }
        public float Progress => 0;
        public int CapturingTeam => -1;
        public CityClaimZone(Vector3 center, float halfExtent = DefaultHalfExtent) { Center = center; HalfExtent = halfExtent; }
        internal bool TryGetGarrisonAnchor(out Vector3 anchor)
        {
            if (hasGarrisonAnchor)
            {
                anchor = garrisonAnchor;
                return true;
            }
            if (!NavMesh.SamplePosition(Center, out var hit, .9f, NavMesh.AllAreas))
            {
                anchor = default;
                return false;
            }
            garrisonAnchor = anchor = hit.position;
            hasGarrisonAnchor = true;
            return true;
        }
        public int Step(BattleSession session, int owner, float delta)
        {
            session.Spatial.Query(Center, ClaimRules.TakeoverRadius, nearby);
            return StepTargets(nearby, owner, delta);
        }
        public int Step(IReadOnlyList<Soldier> soldiers, int owner, float delta = .05f) => StepTargets(soldiers, owner, delta);
        int StepTargets<T>(IReadOnlyList<T> soldiers, int owner, float delta) where T : CombatTarget
        {
            int ownerTeam = PlayerRules.ToCombatTeam(owner);
            Soldier friendly = null, enemy = null;
            float friendlyDistance = float.MaxValue, enemyDistance = float.MaxValue;
            Contested = false;
            if (Defender && (!IsEligible(Defender) || Defender.Garrison != this)) Defender = null;
            for (int i = 0; soldiers != null && i < soldiers.Count; i++)
            {
                var unit = soldiers[i] as Soldier;
                if (!IsEligible(unit) || unit.IsGarrison && unit.Garrison != this) continue;
                var difference = unit.transform.position - Center;
                float distance = difference.x * difference.x + difference.z * difference.z;
                if (Mathf.Abs(difference.y) > VerticalExtent) continue;
                if (unit.Team == ownerTeam && distance <= ClaimRules.ProtectionRadius * ClaimRules.ProtectionRadius)
                {
                    if (ClaimRules.BetterCandidate(ownerTeam,unit.Team,distance,unit.EntityId,friendly?friendly.Team:-1,friendlyDistance,friendly?friendly.EntityId:0))
                    { friendly = unit; friendlyDistance = distance; }
                }
                else if (unit.Team != ownerTeam && PlayerRules.IsPlayer(unit.Team) && distance <= ClaimRules.TakeoverRadius * ClaimRules.TakeoverRadius)
                {
                    Contested = true;
                    if (ClaimRules.BetterCandidate(ownerTeam,unit.Team,distance,unit.EntityId,enemy?enemy.Team:-1,enemyDistance,enemy?enemy.EntityId:0))
                    { enemy = unit; enemyDistance = distance; }
                }
            }
            if (Defender) return owner;
            // A living allied replacement has priority, even when an enemy is closer.
            // The nearest enemy inherits an undefended post; otherwise it becomes neutral.
            var successor = friendly ? friendly : enemy;
            if (!successor) { return -1; }
            SetDefender(successor);
            return Defender && PlayerRules.IsPlayer(successor.Team) ? successor.Team : PlayerRules.NeutralOwner;
        }
        public void SetDefender(Soldier defender)
        {
            if (Defender == defender) return;
            if (!defender)
            {
                if (Defender) Defender.ReleaseGarrison(this);
                Defender = null;
                Contested = false;
                return;
            }
            if (!IsEligible(defender) || defender.IsGarrison && defender.Garrison != this || !defender.BindGarrison(this)) return;
            var previous = Defender;
            Defender = defender;
            if (previous) previous.ReleaseGarrison(this);
            Contested=false;
        }
        static bool IsEligible(Soldier unit) => unit && unit.isActiveAndEnabled && unit.IsAlive &&
            unit.Agent && unit.Agent.enabled && unit.Agent.isOnNavMesh &&
            (PlayerRules.IsPlayer(unit.Team) || unit.Team == PlayerRules.NeutralTeam);
    }
}
