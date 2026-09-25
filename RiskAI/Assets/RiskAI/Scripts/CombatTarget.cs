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
        float roarUntil = -1f, roarBonus;
        /// <summary>Aroa buff from units.json roar.mask. The caster's rolled-damage bonus until it ends.</summary>
        public bool IsRoaring => BattleSession.Current && roarUntil > BattleSession.Current.BattleTime;
        public float RoarDamageScale => IsRoaring ? 1f + roarBonus : 1f;
        public void ApplyRoar(float until, float damageBonus)
        {
            if (!IsAlive) return;
            if (until > roarUntil) roarUntil = until;
            roarBonus = damageBonus;
        }
        public void ClearRoar() { roarUntil = -1f; roarBonus = 0f; }
        /// <summary>Land motor is on the NavMesh. Sea and static actors are not.</summary>
        public virtual bool OnLandMotor => false;
        public float Heal(float amount)
        {
            if (!IsAlive || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return 0;
            float before = Health;
            Health = Mathf.Min(MaxHealth, Health + amount);
            return Health - before;
        }
        /// <summary>The units.json type of this actor.</summary>
        public abstract ref readonly UnitType Type { get; }
        /// <summary>The weapon this actor fights with (a post's depends on its host building).</summary>
        public virtual ref readonly WeaponProfile AttackWeapon => ref Type.Weapon;
        /// <summary>Asked when an ally within its alert radius is hit; units that join the fight override it.</summary>
        public virtual void JoinAlert(CombatTarget attacker) { }
        public abstract float MaxHealth { get; }
        public bool IsAlive => Health > 0 && gameObject.activeInHierarchy;
        public virtual bool Selected { get; protected set; }
        public virtual void Select(bool value) { Selected = value; }
        /// <summary>Bound to a post circle (city guardian or harbor guard).</summary>
        public virtual bool IsGarrison => false;
        /// <summary>The player may select and order this actor: on its motor and on the player's team.</summary>
        public virtual bool PlayerSelectable => IsAlive && isActiveAndEnabled && Team == 0;
        /// <summary>Units aboard a transport capability.</summary>
        public virtual int CargoCount => 0;
        /// <summary>Who this actor is fighting. Follow reads it; actors with no target leave it empty.</summary>
        public virtual CombatTarget CurrentTarget => null;
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
