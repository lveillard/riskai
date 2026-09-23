using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    /// <summary>Exposes a unit's source mana pool to HUD and tooltips.</summary>
    public interface IManaUser { ManaPool Mana { get; } }

    /// <summary>
    /// Aroa (Roar) for h00I Roarer and h00J Army General. Source: +25% base damage to
    /// friendly units in 700 native range for 45 s, 100 mana. Local adaptation: the source
    /// ability is cast manually; here it autocasts once an ally nearby is fighting and at
    /// least one ally in the area is not already roaring.
    /// </summary>
    public sealed class RoarSupport : MonoBehaviour, IManaUser
    {
        readonly ManaPool mana = new ManaPool();
        readonly System.Collections.Generic.List<CombatTarget> nearby = new System.Collections.Generic.List<CombatTarget>(64);
        Soldier self;
        BattleSession session;
        float nextEvaluation;

        public ManaPool Mana => mana;
        public int CastCount { get; private set; }
        public long LastCastTick { get; private set; } = -1;

        public void Initialize(Soldier owner, BattleSession battle)
        {
            self=owner;session=battle;CastCount=0;LastCastTick=-1;
            mana.Reset(SupportAbilities.Mana(owner.Kind));
            nextEvaluation=battle.BattleTime+SupportAbilities.RoarEvaluationInterval;
        }

        /// <summary>Returns true when the roar was cast on this simulation tick.</summary>
        public bool SimTick(float delta)
        {
            if(!self||!self.IsAlive||!self.isActiveAndEnabled||!session||session.Paused||session.Winner>=0)return false;
            mana.Tick(delta);
            if(session.BattleTime<nextEvaluation||!mana.CanSpend(SupportAbilities.RoarManaCost))return false;
            nextEvaluation=session.BattleTime+SupportAbilities.RoarEvaluationInterval;
            Vector3 origin=self.transform.position;float area=SupportAbilities.RoarArea;
            session.Spatial.Query(origin,area,nearby);
            bool fighting=self.CurrentTarget,needed=!self.IsRoaring;
            foreach(var entity in nearby)
            {
                if(!(entity is Soldier ally)||ally.Team!=self.Team||!ally.IsAlive||!ally.isActiveAndEnabled)continue;
                Vector3 offset=ally.transform.position-origin;offset.y=0;if(offset.sqrMagnitude>area*area)continue;
                if(ally.CurrentTarget)fighting=true;
                if(!ally.IsRoaring)needed=true;
                if(fighting&&needed)break;
            }
            if(!fighting||!needed||!mana.TrySpend(SupportAbilities.RoarManaCost))return false;
            float until=session.BattleTime+SupportAbilities.RoarDuration;
            foreach(var entity in nearby)
            {
                if(!(entity is Soldier ally)||ally.Team!=self.Team||!ally.IsAlive)continue;
                Vector3 offset=ally.transform.position-origin;offset.y=0;
                if(offset.sqrMagnitude<=area*area)ally.ApplyRoar(until);
            }
            self.ApplyRoar(until);
            CastCount++;LastCastTick=session.Clock.TickCount;
            if(session.Combat.PresentationEnabled)VisualFactory.Impact(self.AimPoint+Vector3.up*.6f,new Color(1f,.46f,.16f),.9f);
            return true;
        }
    }
}
