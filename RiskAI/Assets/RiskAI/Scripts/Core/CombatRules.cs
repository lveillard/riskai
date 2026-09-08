using System;

namespace RiskAI.Core
{
    public enum AttackKind { Normal=0, Piercing=1, Siege=2, Magic=3, Chaos=4, Hero=5, Spells=6 }
    public enum ArmorKind { Unarmored=0, Light=1, Medium=2, Heavy=3, Fortified=4, Normal=5, Hero=6, Divine=7 }

    public static class CombatRules
    {
        // Full explicit Saran/world war3mapMisc overrides. Columns are source
        // Light, Medium, Large, Fortified, Normal, Hero, Divine, None;
        // enum ordinals intentionally retain the original public/persisted values.
        static readonly float[,] DamageBonuses =
        {
            { 1f, 1.5f, 1f, .7f, .7f, 1f, .05f, 1f },       // Normal
            { 2f, .75f, 1f, .35f, .35f, .5f, .05f, 1.5f },  // Pierce
            { 1f, .5f, 1f, 1.5f, 1.5f, .5f, .05f, 1.5f },  // Siege
            { 1.5f, .75f, 2f, .35f, .35f, .5f, .05f, 1f },  // Magic
            { 1.75f, .75f, 1f, .35f, .35f, .5f, .05f, 1.5f }, // Chaos (map override)
            { 1f, 1f, 1f, .5f, .5f, 1f, .05f, 1f },        // Hero
            { 1f, 1f, 1f, 1f, 1f, .75f, .05f, 1f }         // Spells
        };
        // Units\MiscData.txt:129 from the owned RoC WAR3.MPQ establishes
        // DefenseArmor=0.06. Historical baseline, not a resolved Reforged patch.
        public const float ArmorCoefficient=.06f;

        // Only physical missiles; small sculpted undulations do not count as a cliff.
        public static float UphillMissChance(AttackKind attack,float heightGain) => attack==AttackKind.Piercing && heightGain>=2.5f ? .25f : 0;

        public static float DamageMultiplier(AttackKind attack, ArmorKind armor)
        {
            if((uint)attack>=(uint)DamageBonuses.GetLength(0))throw new ArgumentOutOfRangeException(nameof(attack));
            int column;
            switch(armor)
            {
                case ArmorKind.Light:column=0;break;
                case ArmorKind.Medium:column=1;break;
                case ArmorKind.Heavy:column=2;break;
                case ArmorKind.Fortified:column=3;break;
                case ArmorKind.Normal:column=4;break;
                case ArmorKind.Hero:column=5;break;
                case ArmorKind.Divine:column=6;break;
                case ArmorKind.Unarmored:column=7;break;
                default:throw new ArgumentOutOfRangeException(nameof(armor));
            }
            return DamageBonuses[(int)attack,column];
        }

        public static float ArmorMultiplier(float armor)
        {
            // Preserve the existing classic negative-armor curve. The text table
            // establishes its coefficient, not the engine's implementation.
            return armor >= 0 ? 1f / (1f + ArmorCoefficient * armor) : 2f - (float)Math.Pow(1f-ArmorCoefficient, -armor);
        }

        public static float ArmorReduction(float armor) => ArmorMultiplier(armor);
        public static float Multiplier(AttackKind attack, ArmorKind armor) => DamageMultiplier(attack, armor);

        public static float ResolveDamage(float damage, AttackKind attack, ArmorKind armor, float armorValue)
        {
            if (damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return 0;
            return damage * DamageMultiplier(attack, armor) * ArmorMultiplier(armorValue);
        }

        // Short aliases keep the rule useful to callers that describe this as a bonus table.
        public static float Bonus(AttackKind attack, ArmorKind armor) => DamageMultiplier(attack, armor);
        public static float Damage(float damage, AttackKind attack, ArmorKind armor, float armorValue) => ResolveDamage(damage, attack, armor, armorValue);
    }
}
