using UnityEngine;
using RiskAI.Core;

namespace RiskAI
{
    public abstract class CombatTarget : MonoBehaviour
    {
        public int Team { get; protected set; }
        public float Health { get; protected set; }
        public abstract float MaxHealth { get; }
        public bool IsAlive => Health > 0 && gameObject.activeInHierarchy;
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
            TakeDamage(CombatRules.ResolveDamage(damage, attack, ArmorType, Armor), attacker, source);
        }
        public void ReceiveAttack(float damage, int attacker, AttackKind attack, CombatTarget source = null) => ReceiveAttack(damage, attack, attacker, source);
        public void ReceiveAttack(float damage, AttackKind attack, CombatTarget source = null) => ReceiveAttack(damage, attack, source ? source.Team : -1, source);
        public abstract void TakeDamage(float damage, int attacker, CombatTarget source = null);
    }
}
