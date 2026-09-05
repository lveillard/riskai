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
        void Play(string clip,float fade)
        {
            if(!anim||!anim[clip]||current==clip)return;current=clip;anim.CrossFade(clip,fade);
        }
        public void Strike()
        {
            if(!anim)return;string clip=soldier.Kind==Core.UnitKind.Footman?"1H_Melee_Attack_Slice_Horizontal":soldier.Kind==Core.UnitKind.Guard?"2H_Melee_Attack_Slice":soldier.Kind==Core.UnitKind.Mage?"Spellcast_Shoot":"2H_Ranged_Shoot";
            if(!anim[clip])return;current=clip;anim[clip].time=0;anim[clip].speed=1.9f;anim[clip].wrapMode=WrapMode.Once;
            anim.CrossFade(clip,.065f);lockedUntil=Time.time+.45f;
        }
        public void Die()
        {
            dead=true;if(!anim||!anim["Death_A"])return;
            anim["Death_A"].wrapMode=WrapMode.ClampForever;anim.CrossFade("Death_A",.08f);
        }
        void Update()
        {
            if(dead||!soldier||!anim||Time.time<lockedUntil)return;
            if(soldier.Agent.velocity.sqrMagnitude>.05f){if(anim["Running_A"])anim["Running_A"].speed=1.15f;Play("Running_A",.12f);}
            else Play(soldier.CurrentTarget&&soldier.Kind==Core.UnitKind.Archer?"2H_Ranged_Aiming":"Idle",.15f);
        }
    }
}
