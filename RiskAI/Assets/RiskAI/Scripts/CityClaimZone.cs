using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;
namespace RiskAI
{
    // Spatial adapter for garrison selection and a pure timed ownership transition.
    public sealed class CityClaimZone
    {
        public const float DefaultHalfExtent = ClaimRules.CircleRadius;
        public const float VerticalExtent = 1.25f;
        readonly ClaimTransition transition = new ClaimTransition();
        readonly List<CombatTarget> nearby = new List<CombatTarget>(32);
        public Vector3 Center { get; }
        public float HalfExtent { get; }
        public Soldier Defender { get; private set; }
        public bool Contested { get; private set; }
        public float Progress => transition.Progress;
        public int CapturingTeam => transition.CandidateTeam;
        public CityClaimZone(Vector3 center, float halfExtent = DefaultHalfExtent) { Center = center; HalfExtent = halfExtent; }
        public int Step(BattleSession session, int owner, float delta)
        {
            session.Spatial.Query(Center, ClaimRules.ProtectionRadius, nearby);
            return StepTargets(nearby, owner, delta);
        }
        public int Step(IReadOnlyList<Soldier> soldiers, int owner, float delta = .05f) => StepTargets(soldiers, owner, delta);
        int StepTargets<T>(IReadOnlyList<T> soldiers, int owner, float delta) where T : CombatTarget
        {
            int ownerTeam = owner >= 0 ? owner : 2;
            Soldier ownInside = null, candidate = null;
            bool ownerNearby = false;
            float ownDistance = float.MaxValue, candidateDistance = float.MaxValue;
            Contested = false;
            if (Defender && (!IsEligible(Defender) || Defender.Garrison != this)) Defender = null;
            for (int i = 0; soldiers != null && i < soldiers.Count; i++)
            {
                var unit = soldiers[i] as Soldier; if (!IsEligible(unit)) continue;
                var difference = unit.transform.position - Center;
                float distance = difference.x*difference.x + difference.z*difference.z;
                if (Mathf.Abs(difference.y)>VerticalExtent || distance>ClaimRules.ProtectionRadius*ClaimRules.ProtectionRadius) continue;
                if (unit.Team == ownerTeam) ownerNearby = true; else Contested = true;
                if (distance>HalfExtent*HalfExtent || unit.IsGarrison && unit.Garrison != this) continue;
                if (unit.Team == ownerTeam && (distance<ownDistance || distance==ownDistance && (!ownInside || unit.EntityId<ownInside.EntityId)))
                { ownInside=unit; ownDistance=distance; }
                else if (unit.Team != ownerTeam && unit.Team<2 && (distance<candidateDistance || distance==candidateDistance && (!candidate || unit.EntityId<candidate.EntityId)))
                { candidate=unit; candidateDistance=distance; }
            }
            if (Defender) { transition.Reset(); return owner; }
            if (ownInside) { SetDefender(ownInside); return owner; }
            bool blocked=ownerNearby;
            if (candidate)
                for (int i=0;i<soldiers.Count;i++)
                {
                    var unit=soldiers[i] as Soldier; if (!IsEligible(unit) || unit.Team==candidate.Team) continue;
                    var d=unit.transform.position-Center;
                    if (Mathf.Abs(d.y)<=VerticalExtent && d.x*d.x+d.z*d.z<=ClaimRules.ProtectionRadius*ClaimRules.ProtectionRadius)
                    { blocked=true; break; }
                }
            Contested=candidate && blocked;
            if (transition.Advance(candidate?candidate.EntityId:0,candidate?candidate.Team:-1,blocked,delta))
            {
                int nextOwner=candidate.Team; SetDefender(candidate); return Defender?nextOwner:owner;
            }
            return owner;
        }
        public void SetDefender(Soldier defender)
        {
            if (Defender && Defender != defender) Defender.ReleaseGarrison(this);
            Defender=null;
            if (IsEligible(defender) && (!defender.IsGarrison || defender.Garrison==this) && defender.BindGarrison(this)) Defender=defender;
            transition.Reset(); Contested=false;
        }
        static bool IsEligible(Soldier unit) => unit && unit.isActiveAndEnabled && unit.IsAlive &&
            unit.Agent && unit.Agent.enabled && unit.Agent.isOnNavMesh && unit.Team>=0;
    }
}
