using System;

namespace RiskAI.Core
{
    public enum AttackKind { Normal, Piercing, Siege, Magic }
    public enum ArmorKind { Unarmored, Light, Medium, Heavy, Fortified }

    public static class CombatRules
    {
        // The source table is ordered Light, Medium, Heavy, Fortified, Unarmored.
        static readonly float[,] DamageBonuses =
        {
            { 1f, 1.5f, 1f, .7f, 1f },
            { 2f, .75f, 1f, .35f, 1.5f },
            { 1f, .5f, 1f, 1.5f, 1.5f },
            { 1.5f, .75f, 2f, .35f, 1f }
        };

        // Only physical missiles; small sculpted undulations do not count as a cliff.
        public static float UphillMissChance(AttackKind attack,float heightGain) => attack==AttackKind.Piercing && heightGain>=2.5f ? .25f : 0;

        public static float DamageMultiplier(AttackKind attack, ArmorKind armor)
        {
            int column = armor == ArmorKind.Light ? 0 : armor == ArmorKind.Medium ? 1 : armor == ArmorKind.Heavy ? 2 : armor == ArmorKind.Fortified ? 3 : 4;
            return DamageBonuses[(int)attack, column];
        }

        public static float ArmorMultiplier(float armor)
        {
            return armor >= 0 ? 1f / (1f + .06f * armor) : 2f - (float)Math.Pow(.94f, -armor);
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
