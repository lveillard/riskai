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
        public const float AnchorSearchRadius = .9f;
        public const float RingWidth = .07f;
        public static readonly Color RingColor = Color.white;
        public static readonly Color ContestedRingColor = new Color(1f,.68f,.12f);
        public static Color VisibleRingColor(bool contested) => contested ? ContestedRingColor : RingColor;
        readonly List<CombatTarget> nearby = new List<CombatTarget>(32);
        public Vector3 Center { get; }
        // This is deliberately resolved once.  Every replacement defender must use
        // the same walkable point, otherwise NavMesh sampling around a sloped ring
        // can make a hand-off look like the guard has moved off the post.
        Vector3 garrisonAnchor;
        bool hasGarrisonAnchor;
        public float HalfExtent { get; }
        CombatTarget guardian;
        Harbor harbor;
        internal Harbor Port => harbor;
        public Soldier Defender { get=>guardian as Soldier; private set=>guardian=value; }
        public Ship NavalDefender=>guardian as Ship;
        public CombatTarget Guardian=>guardian&&guardian.IsAlive?guardian:null;
        public bool Contested { get; private set; }
        public float Progress => 0;
        public int CapturingTeam => -1;
        public CityClaimZone(Vector3 center, float halfExtent = DefaultHalfExtent) { Center = center; HalfExtent = halfExtent; }
        internal void AttachHarbor(Harbor port) { harbor=port; }
        internal bool TryGetGarrisonAnchor(out Vector3 anchor)
        {
            if (hasGarrisonAnchor)
            {
                anchor = garrisonAnchor;
                return true;
            }
            if (!NavMesh.SamplePosition(Center, out var hit, AnchorSearchRadius, NavMesh.AllAreas))
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
            CombatTarget friendly = null, enemy = null;
            float friendlyDistance = float.MaxValue, enemyDistance = float.MaxValue;
            Contested = false;
            if (guardian && guardian is IPostClaimant stale && stale.DropStale(this)) guardian = null;
            for (int i = 0; soldiers != null && i < soldiers.Count; i++)
            {
                var unit = soldiers[i];
                if (!unit) continue;
                var claimant = unit as IPostClaimant;
                if (claimant == null || !claimant.ContendsOnFoot || claimant.BlockedByOtherPost(this)) continue;
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
            // A living guardian holds ownership, but enemies in the capture area
            // still make the post contested for visuals and combat decisions.
            if (guardian) return owner;
            // A living allied replacement has priority, even when an enemy is closer.
            // The nearest enemy inherits an undefended post; otherwise it becomes neutral.
            CombatTarget successor = friendly ? friendly : enemy;
            float distanceSquared=friendly?friendlyDistance:enemyDistance;
            var docked=harbor?harbor.FindDockedSuccessor(owner,null,false):null;
            if(docked&&ClaimRules.BetterCandidate(ownerTeam,docked.Team,FlatDistance(docked.transform.position,harbor.Berth),docked.EntityId,
                successor?successor.Team:-1,distanceSquared,successor?successor.EntityId:0))successor=docked;
            if (!successor) return PlayerRules.NeutralOwner;
            return TrySetGuardian(successor) && PlayerRules.IsPlayer(successor.Team) ? successor.Team : PlayerRules.NeutralOwner;
        }
        public void SetDefender(Soldier defender)
        {
            if (!defender)
            {
                if (guardian is IPostClaimant claimant && claimant.ContendsOnFoot) claimant.ReleasePost(this);
                guardian = null;
                Contested = false;
                return;
            }
            TrySetGuardian(defender);
        }
        internal void SetNavalDefender(Ship ship,Harbor harbor)
        {
            // Clearing a naval candidate must never erase a valid land defender.
            if (!ship)
            {
                if (guardian is IPostClaimant claimant && !claimant.ContendsOnFoot)
                {
                    claimant.ReleasePost(this);
                    guardian = null;
                }
                return;
            }
            TrySetGuardian(ship);
        }
        internal bool HasRelief(BattleSession session, CombatTarget departing) =>
            guardian == departing && departing && FindCircleReplacement(session, departing.Team, departing);
        internal bool TryReleaseGuardianForOrder(BattleSession session,CombatTarget departing)
        {
            if(guardian!=departing||!departing)return false;
            var replacement=FindCircleReplacement(session,departing.Team,departing);
            return replacement&&TrySetGuardian(replacement);
        }
        bool TrySetGuardian(CombatTarget candidate)
        {
            if (!candidate) return false;
            if (guardian == candidate) return true;
            var claimant = candidate as IPostClaimant;
            if (claimant == null || !claimant.TryBindPost(this)) return false;
            var previous = guardian;
            guardian = candidate;
            if (previous && previous != candidate && previous is IPostClaimant previousClaimant) previousClaimant.ReleasePost(this);
            Contested = false;
            return true;
        }
        CombatTarget FindCircleReplacement(BattleSession session, int team, CombatTarget excluded)
        {
            if (!session) return null;
            session.Spatial.Query(Center, ClaimRules.ReliefRadius, nearby);
            CombatTarget best = null;
            float bestDistance = float.MaxValue;
            float radiusSquared = ClaimRules.ReliefRadius * ClaimRules.ReliefRadius;
            for (int i = 0; i < nearby.Count; i++)
            {
                var unit = nearby[i];
                if (!unit) continue;
                var claimant = unit as IPostClaimant;
                if (unit == excluded || claimant == null || !claimant.ContendsOnFoot || unit.Team != team) continue;
                if (unit is IOrderable held && held.IsGarrison) continue;
                var difference = unit.transform.position - Center;
                float distance = difference.x * difference.x + difference.z * difference.z;
                if (Mathf.Abs(difference.y) > VerticalExtent || distance > radiusSquared) continue;
                if (ClaimRules.BetterCandidate(team, unit.Team, distance, unit.EntityId,
                    best ? best.Team : -1, bestDistance, best ? best.EntityId : 0))
                {
                    best = unit;
                    bestDistance = distance;
                }
            }
            var docked=harbor?harbor.FindDockedSuccessor(team,excluded,true):null;
            if(docked&&ClaimRules.BetterCandidate(team,docked.Team,FlatDistance(docked.transform.position,harbor.Berth),docked.EntityId,
                best?best.Team:-1,bestDistance,best?best.EntityId:0))return docked;
            return best;
        }
        static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.SqrMagnitude(a-b);}
    }
}
