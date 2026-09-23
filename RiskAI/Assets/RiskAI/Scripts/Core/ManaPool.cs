using System;

namespace RiskAI.Core
{
    /// <summary>Engine-independent mana pool: source max/initial/regeneration per second.</summary>
    public sealed class ManaPool
    {
        public float Maximum { get; private set; }
        public float Regeneration { get; private set; }
        public float Current { get; private set; }
        public bool Enabled => Maximum > 0;

        public void Reset(ManaProfile profile)
        {
            Maximum=Math.Max(0,profile.Maximum);Regeneration=Math.Max(0,profile.Regeneration);
            Current=Math.Min(Maximum,Math.Max(0,profile.Initial));
        }
        public void Tick(float delta)
        {
            if(delta<=0||float.IsNaN(delta)||float.IsInfinity(delta)||!Enabled)return;
            Current=Math.Min(Maximum,Current+Regeneration*delta);
        }
        public bool CanSpend(float amount) => Enabled && amount>=0 && Current+.0001f>=amount;
        public bool TrySpend(float amount)
        {
            if(!CanSpend(amount))return false;
            Current=Math.Max(0,Current-amount);return true;
        }
    }

    public readonly struct ManaProfile
    {
        public readonly float Maximum, Initial, Regeneration;
        public ManaProfile(float maximum,float initial,float regeneration){Maximum=maximum;Initial=initial;Regeneration=regeneration;}
        public bool Enabled => Maximum > 0;
    }
}
