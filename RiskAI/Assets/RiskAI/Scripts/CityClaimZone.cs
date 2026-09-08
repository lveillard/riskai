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
        CombatTarget guardian;
        Harbor harbor;
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
            if (NavalDefender)
            {
                if (!harbor || !harbor.HasNavalDefender) SetNavalDefender(null,harbor);
            }
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
            // A living guardian holds ownership, but enemies in the capture area
            // still make the post contested for visuals and combat decisions.
            if (Defender || NavalDefender) return owner;
            // A living allied replacement has priority, even when an enemy is closer.
            // The nearest enemy inherits an undefended post; otherwise it becomes neutral.
            CombatTarget successor = friendly ? friendly : enemy;
            float distanceSquared=friendly?friendlyDistance:enemyDistance;
            var ship=harbor?harbor.FindDockedSuccessor(owner,null,false):null;
            if(ship&&ClaimRules.BetterCandidate(ownerTeam,ship.Team,FlatDistance(ship.transform.position,harbor.Berth),ship.EntityId,
                successor?successor.Team:-1,distanceSquared,successor?successor.EntityId:0))successor=ship;
            if (!successor) return PlayerRules.NeutralOwner;
            return TrySetGuardian(successor) && PlayerRules.IsPlayer(successor.Team) ? successor.Team : PlayerRules.NeutralOwner;
        }
        public void SetDefender(Soldier defender)
        {
            TrySetDefender(defender);
        }
        bool TrySetDefender(Soldier defender)
        {
            if (Defender == defender) return true;
            if (!defender)
            {
                if (Defender) Defender.ReleaseGarrison(this);
                Defender = null;
                Contested = false;
                return true;
            }
            if (!IsEligible(defender) || defender.IsGarrison && defender.Garrison != this || !defender.BindGarrison(this)) return false;
            var previous = Defender;
            var previousShip = NavalDefender;
            Defender = defender;
            if (previous) previous.ReleaseGarrison(this);
            if (previousShip) previousShip.ReleaseHarborGuard(previousShip.Garrison);
            Contested=false;
            return true;
        }
        internal void SetNavalDefender(Ship ship,Harbor harbor)
        {
            if(ship&&(!ship.IsAlive||ship.Kind!=ShipKind.Galley||ship.Garrison&&ship.Garrison!=harbor))return;
            var previousShip=NavalDefender;
            if(previousShip==ship)return;
            // Land and sea are movement adapters for one logical garrison slot.
            // Clearing a naval candidate must never erase a valid land defender.
            if(!ship)
            {
                if(previousShip){guardian=null;previousShip.ReleaseHarborGuard(harbor);}
                return;
            }
            if(Defender)Defender.ReleaseGarrison(this);
            if(previousShip)previousShip.ReleaseHarborGuard(harbor);
            guardian=ship;ship.BindHarborGuard(harbor);Contested=false;
        }
        internal bool CanReleaseDefenderForOrder(BattleSession session, Soldier defender)
        {
            return Defender == defender && FindCircleReplacement(session, defender.Team, defender);
        }
        internal bool TryReleaseDefenderForOrder(BattleSession session, Soldier defender)
        {
            return TryReleaseGuardianForOrder(session,defender);
        }
        internal bool TryReleaseGuardianForOrder(BattleSession session,CombatTarget departing)
        {
            if(guardian!=departing||!departing)return false;
            var replacement=FindCircleReplacement(session,departing.Team,departing);
            return replacement&&TrySetGuardian(replacement);
        }
        bool TrySetGuardian(CombatTarget candidate)
        {
            if(candidate is Soldier soldier)return TrySetDefender(soldier);
            if(candidate is Ship ship&&harbor){SetNavalDefender(ship,harbor);return guardian==ship;}
            return false;
        }
        CombatTarget FindCircleReplacement(BattleSession session, int team, CombatTarget excluded)
        {
            if (!session) return null;
            session.Spatial.Query(Center, ClaimRules.ReliefRadius, nearby);
            Soldier best = null;
            float bestDistance = float.MaxValue;
            float radiusSquared = ClaimRules.ReliefRadius * ClaimRules.ReliefRadius;
            for (int i = 0; i < nearby.Count; i++)
            {
                var unit = nearby[i] as Soldier;
                if (unit == excluded || !IsEligible(unit) || unit.IsGarrison || unit.Team != team) continue;
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
            var ship=harbor?harbor.FindDockedSuccessor(team,excluded as Ship,true):null;
            if(ship&&ClaimRules.BetterCandidate(team,ship.Team,FlatDistance(ship.transform.position,harbor.Berth),ship.EntityId,
                best?best.Team:-1,bestDistance,best?best.EntityId:0))return ship;
            return best;
        }
        static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.SqrMagnitude(a-b);}
        static bool IsEligible(Soldier unit) => unit && unit.isActiveAndEnabled && unit.IsAlive &&
            unit.Agent && unit.Agent.enabled && unit.Agent.isOnNavMesh &&
            (PlayerRules.IsPlayer(unit.Team) || unit.Team == PlayerRules.NeutralTeam);
    }
}
