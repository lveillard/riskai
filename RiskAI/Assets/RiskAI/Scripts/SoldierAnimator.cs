using UnityEngine;

namespace RiskAI
{
    public sealed class SoldierAnimator : MonoBehaviour
    {
        Soldier soldier;Animation anim;float lockedUntil;string current;bool dead;
        public void Initialize(Soldier owner,GameObject model)
        {
            soldier=owner;anim=model.GetComponentInChildren<Animation>();
            foreach(AnimationState state in anim)state.wrapMode=WrapMode.Loop;
            Play("Idle",.1f);
        }
        public void ResetForReuse()
        {
            dead=false;lockedUntil=0;current=null;
            if(anim){foreach(AnimationState state in anim){state.time=0;state.speed=1;state.wrapMode=WrapMode.Loop;}Play("Idle",.01f);}
        }
        public void SetPaused(bool paused)
        {
            if(anim)foreach(AnimationState state in anim)state.speed=paused?0:1;
        }
        void Play(string clip,float fade)
        {
            if(!anim||!anim[clip]||current==clip)return;current=clip;anim.CrossFade(clip,fade);
        }
        public void Strike()
        {
            if(!anim)return;string clip=soldier.Kind==Core.UnitKind.Footman?"1H_Melee_Attack_Slice_Horizontal":!Core.BattleRules.Ranged(soldier.Kind)?"2H_Melee_Attack_Slice":soldier.Kind==Core.UnitKind.Mage?"Spellcast_Shoot":"2H_Ranged_Shoot";
            if(!anim[clip])return;current=clip;anim[clip].time=0;anim[clip].speed=1.9f;anim[clip].wrapMode=WrapMode.Once;
            anim.CrossFade(clip,.065f);lockedUntil=(BattleSession.Current?BattleSession.Current.BattleTime:0)+.45f;
        }
        public void Die()
        {
            dead=true;if(!anim||!anim["Death_A"])return;
            anim["Death_A"].wrapMode=WrapMode.ClampForever;anim.CrossFade("Death_A",.08f);
        }
        void Update()
        {
            if(dead||!soldier||!anim||BattleSession.Current && (BattleSession.Current.Paused || BattleSession.Current.Winner>=0)||(BattleSession.Current?BattleSession.Current.BattleTime:0)<lockedUntil)return;
            if(soldier.Agent.velocity.sqrMagnitude>.05f){if(anim["Running_A"])anim["Running_A"].speed=1.15f;Play("Running_A",.12f);}
            else Play(soldier.CurrentTarget&&soldier.Kind==Core.UnitKind.Archer?"2H_Ranged_Aiming":"Idle",.15f);
        }
    }
}
