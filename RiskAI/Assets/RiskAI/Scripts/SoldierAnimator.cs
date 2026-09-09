using UnityEngine;

namespace RiskAI
{
    public sealed class SoldierAnimator : MonoBehaviour
    {
        Soldier soldier;Animation anim;string current,attackClip;bool dead;
        public void Initialize(Soldier owner,GameObject model)
        {
            soldier=owner;anim=model.GetComponentInChildren<Animation>();
            foreach(AnimationState state in anim)state.wrapMode=WrapMode.Loop;
            Play("Idle",.1f);
        }
        public void ResetForReuse()
        {
            dead=false;current=attackClip=null;
            if(anim){foreach(AnimationState state in anim){state.time=0;state.speed=1;state.wrapMode=WrapMode.Loop;}Play("Idle",.01f);}
        }
        public void SetPaused(bool paused)
        {
            if(anim)foreach(AnimationState state in anim)state.speed=paused||state.name==attackClip?0:1;
        }
        void Play(string clip,float fade)
        {
            if(!anim||!anim[clip]||current==clip)return;current=clip;anim.CrossFade(clip,fade);
        }
        public void Strike()
        {
            if(!anim)return;string clip=AttackPresentationTiming.Clip(soldier.Kind);
            if(string.IsNullOrEmpty(clip)||!anim[clip])return;current=attackClip=clip;anim[clip].time=0;anim[clip].speed=0;anim[clip].wrapMode=WrapMode.Once;
            anim.CrossFade(clip,.065f);
        }
        public void SampleStrikeContact()
        {
            if(!anim||string.IsNullOrEmpty(attackClip)||!anim[attackClip])return;
            var state=anim[attackClip];state.speed=0;
            state.time=state.length*AttackPresentationTiming.ContactNormalizedTime(soldier.Kind);
            anim.Sample();
        }
        public void CancelStrike()
        {
            if(string.IsNullOrEmpty(attackClip))return;
            attackClip=null;current=null;Play("Idle",.065f);
        }
        public void Die()
        {
            dead=true;if(!anim||!anim["Death_A"])return;
            anim["Death_A"].wrapMode=WrapMode.ClampForever;anim.CrossFade("Death_A",.08f);
        }
        void Update()
        {
            if(dead||!soldier||!anim||BattleSession.Current && (BattleSession.Current.Paused || BattleSession.Current.Winner>=0))return;
            float attackProgress=soldier.AttackPresentationProgress;
            if(attackProgress>=0&&attackClip!=null&&anim[attackClip])
            {
                current=attackClip;anim[attackClip].speed=0;anim[attackClip].time=anim[attackClip].length*attackProgress;return;
            }
            attackClip=null;
            if(soldier.Agent.velocity.sqrMagnitude>.05f){if(anim["Running_A"])anim["Running_A"].speed=1.15f;Play("Running_A",.12f);}
            else Play(soldier.CurrentTarget&&soldier.Kind==Core.UnitKind.Archer?"2H_Ranged_Aiming":
                soldier.CurrentTarget&&soldier.Kind==Core.UnitKind.MarinePrivate?"1H_Ranged_Aiming":"Idle",.15f);
        }
    }
}
