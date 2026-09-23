using UnityEngine;
using RiskAI.Core;

namespace RiskAI
{
    public abstract class CombatTarget : MonoBehaviour
    {
        // Monotonic per battle; pooled objects receive a new identity on every spawn.
        public int EntityId { get; internal set; }
        public int Team { get; protected set; }
        public float Health { get; protected set; }
        /// <summary>The units.json type of this actor.</summary>
        public abstract ref readonly UnitType Type { get; }
        /// <summary>The weapon this actor fights with (a post's depends on its host building).</summary>
        public virtual ref readonly WeaponProfile AttackWeapon => ref Type.Weapon;
        /// <summary>Asked when an ally within its alert radius is hit; units that join the fight override it.</summary>
        public virtual void JoinAlert(CombatTarget attacker) { }
        public abstract float MaxHealth { get; }
        public bool IsAlive => Health > 0 && gameObject.activeInHierarchy;
        public virtual bool CanBeAttacked => IsAlive;
        public virtual Vector3 AimPoint => transform.position + Vector3.up * 1.5f;
        public virtual Vector3 ApproachPoint(Vector3 from) => transform.position;
        public virtual AttackKind AttackType => AttackKind.Normal;
        public virtual ArmorKind ArmorType => ArmorKind.Unarmored;
        public virtual float Armor => 0;
        public AttackKind AttackKind => AttackType;
        public ArmorKind ArmorKind => ArmorType;
        public virtual void ReceiveAttack(float damage, AttackKind attack, int attacker, CombatTarget source = null)
        {
            if (!IsAlive || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage) || attacker == Team) return;
            float before = Health;
            TakeDamage(CombatRules.ResolveDamage(damage, attack, ArmorType, Armor), attacker, source);
            var battle = BattleSession.Current;
            if (Health < before && battle && battle.Feedback != null) battle.Feedback.RaiseDamaged(this, attacker, source);
        }
        public void ReceiveAttack(float damage, int attacker, AttackKind attack, CombatTarget source = null) => ReceiveAttack(damage, attack, attacker, source);
        public void ReceiveAttack(float damage, AttackKind attack, CombatTarget source = null) => ReceiveAttack(damage, attack, source ? source.Team : -1, source);
        public abstract void TakeDamage(float damage, int attacker, CombatTarget source = null);
    }
}
