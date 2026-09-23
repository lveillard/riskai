using UnityEngine;
using RiskAI.Core;

namespace RiskAI
{
    /// <summary>Speed-matched gait for KayKit units so feet do not skate; presentation only.</summary>
    public static class GaitPolicy
    {
        // Running_A looked right at 1.15x playback for a 5.4 m/s footman; Walking_A covers
        // roughly 40% of that stride. Both scale with the rendered standing height.
        public const float RunReferenceSpeed = 5.4f / 1.15f;
        public const float WalkReferenceSpeed = 1.9f;
        public const float RunEnter = 2.9f, RunExit = 2.4f;
        public const float MovingThreshold = .22f;

        public static float HeightRatio(Core.UnitKind kind) =>
            Mathf.Clamp(UnitCatalog.Get(kind).VisualHeight / Mathf.Max(.01f, UnitCatalog.Get(Core.UnitKind.Footman).VisualHeight), .6f, 1.6f);

        /// <summary>Whether the run clip should play, with hysteresis around the walk/run threshold.</summary>
        public static bool Runs(float speed, float heightRatio, bool running) =>
            speed > (running ? RunExit : RunEnter) * heightRatio;

        public static float RunPlayback(float speed, float heightRatio) => Mathf.Clamp(speed / (RunReferenceSpeed * heightRatio), .55f, 1.7f);
        public static float WalkPlayback(float speed, float heightRatio) => Mathf.Clamp(speed / (WalkReferenceSpeed * heightRatio), .45f, 1.6f);
    }

    public sealed class SoldierAnimator : MonoBehaviour
    {
        Soldier soldier;Animation anim;string current,attackClip,hitClip;bool dead;
        float smoothedSpeed,hitUntil,heightRatio=1;bool hasWalk;
        public void Initialize(Soldier owner,GameObject model)
        {
            soldier=owner;anim=model.GetComponentInChildren<Animation>();
            foreach(AnimationState state in anim)state.wrapMode=WrapMode.Loop;
            hasWalk=anim["Walking_A"];heightRatio=GaitPolicy.HeightRatio(owner.Kind);
            Play("Idle",.1f);
        }
        public void ResetForReuse()
        {
            dead=false;current=attackClip=hitClip=null;smoothedSpeed=0;hitUntil=0;
            if(soldier)heightRatio=GaitPolicy.HeightRatio(soldier.Kind);
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
            if(!anim)return;string clip=UnitCatalog.Get(soldier.Kind).AttackClip;
            if(string.IsNullOrEmpty(clip)||!anim[clip])return;hitClip=null;hitUntil=0;
            current=attackClip=clip;anim[clip].time=0;anim[clip].speed=0;anim[clip].wrapMode=WrapMode.Once;
            anim.CrossFade(clip,.065f);
        }
        public void SampleStrikeContact()
        {
            if(!anim||string.IsNullOrEmpty(attackClip)||!anim[attackClip])return;
            var state=anim[attackClip];state.speed=0;
            state.time=state.length*UnitCatalog.Get(soldier.Kind).AttackContact;
            anim.Sample();
        }
        public void CancelStrike()
        {
            if(string.IsNullOrEmpty(attackClip))return;
            attackClip=null;current=null;Play("Idle",.065f);
        }
        /// <summary>Quick flinch; skipped mid-attack or while moving so it never hides a strike or a stride.</summary>
        public void Hit()
        {
            if(dead||!anim||attackClip!=null||smoothedSpeed>GaitPolicy.MovingThreshold||Time.time<hitUntil)return;
            string clip=soldier&&(soldier.EntityId&1)==0?"Hit_A":"Hit_B";
            if(!anim[clip])clip="Hit_A";
            if(!anim[clip])return;
            var state=anim[clip];state.wrapMode=WrapMode.Once;state.time=0;state.speed=1.35f;
            hitClip=clip;current=clip;hitUntil=Time.time+Mathf.Min(.42f,state.length/state.speed*.85f);
            anim.CrossFade(clip,.05f);
        }
        public void Die()
        {
            dead=true;if(!anim)return;
            // Both clips ship in the KayKit FBX; the entity id keeps the choice stable per death.
            string clip=soldier&&(soldier.EntityId&1)!=0&&anim["Death_B"]?"Death_B":"Death_A";
            if(!anim[clip])return;
            anim[clip].wrapMode=WrapMode.ClampForever;anim[clip].speed=1;anim.CrossFade(clip,.08f);
        }
        void OnEnable()=>RefreshPose(true);
        void Update()=>RefreshPose(false);
        void RefreshPose(bool sampleImmediately)
        {
            if(dead||!soldier||!anim||BattleSession.Current && (BattleSession.Current.Paused || BattleSession.Current.Winner>=0))return;
            float attackProgress=soldier.AttackPresentationProgress;
            if(attackProgress>=0&&attackClip!=null&&anim[attackClip])
            {
                current=attackClip;anim[attackClip].speed=0;anim[attackClip].time=anim[attackClip].length*attackProgress;
                if(sampleImmediately)anim.Sample();
                return;
            }
            attackClip=null;
            var velocity=soldier.Agent?soldier.Agent.velocity:Vector3.zero;velocity.y=0;
            float speed=velocity.magnitude;
            smoothedSpeed=Mathf.Lerp(smoothedSpeed,speed,1-Mathf.Exp(-12f*Time.deltaTime));
            if(hitClip!=null)
            {
                if(Time.time<hitUntil&&smoothedSpeed<=GaitPolicy.MovingThreshold)return;
                hitClip=null;current=null;
            }
            if(smoothedSpeed>GaitPolicy.MovingThreshold)
            {
                bool run=!hasWalk||GaitPolicy.Runs(smoothedSpeed,heightRatio,current=="Running_A");
                if(run){if(anim["Running_A"])anim["Running_A"].speed=GaitPolicy.RunPlayback(smoothedSpeed,heightRatio);Play("Running_A",.2f);}
                else{anim["Walking_A"].speed=GaitPolicy.WalkPlayback(smoothedSpeed,heightRatio);Play("Walking_A",.2f);}
            }
            else Play(soldier.CurrentTarget&&soldier.Kind==Core.UnitKind.Archer?"2H_Ranged_Aiming":
                soldier.CurrentTarget&&soldier.Kind==Core.UnitKind.MarinePrivate?"1H_Ranged_Aiming":"Idle",.15f);
        }
    }
}
