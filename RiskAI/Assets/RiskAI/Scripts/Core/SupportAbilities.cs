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

    /// <summary>
    /// Source data for the support abilities represented by the runtime.
    /// Ahea/Aroa rows come from the local RoC AbilityData baseline (the same baseline
    /// used for inherited unit fields); the map W3A only changes Ahea targets and Aroa area/targets.
    /// </summary>
    public static class SupportAbilities
    {
        // Ahea (RoC AbilityData): Rng1=250 native, Data11=25 HP, Cool1=1 s, Cost1=5 mana.
        // W3A keeps the organic target flag: mechanical units are not healed.
        public const float HealRange = 5f;
        public const float HealAmount = 25f;
        public const float HealCooldown = 1f;
        public const float HealManaCost = 5f;

        // Aroa (RoC AbilityData): Dur1=45 s, Cost1=100 mana, Cool1=0, Data11=+25% base damage.
        // war3map.w3a explicitly sets Area1=700 native (14 Unity) and friendly/self targets.
        public const float RoarArea = 14f;
        public const float RoarDuration = 45f;
        public const float RoarManaCost = 100f;
        public const float RoarDamageBonus = .25f;
        // Local autocast adaptation: the source ability is manual; the runtime only
        // re-evaluates it this often so a pool of Roarers does not cast every tick.
        public const float RoarEvaluationInterval = .5f;

        public static bool CanHeal(UnitKind kind) => kind == UnitKind.Medic;
        public static bool CanRoar(UnitKind kind) => kind == UnitKind.Roarer || kind == UnitKind.ArmyGeneral;

        /// <summary>
        /// h00E: hmpr manaN=200 and mana0=75 (RoC UnitBalance), explicit umpr=1.5.
        /// h00I: explicit umpm=300 and umpr=2; inherits hmpr mana0=75.
        /// h00J: explicit umpm=300 and umpr=3; hkni has no initial mana ("-" → 0).
        /// </summary>
        public static ManaProfile Mana(UnitKind kind)
        {
            switch(kind)
            {
                case UnitKind.Medic: return new ManaProfile(200,75,1.5f);
                case UnitKind.Roarer: return new ManaProfile(300,75,2);
                case UnitKind.ArmyGeneral: return new ManaProfile(300,0,3);
                default: return default;
            }
        }

        public static float DamageMultiplier(bool roaring) => roaring ? 1 + RoarDamageBonus : 1;
    }
}
