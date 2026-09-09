using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    public readonly struct ProjectileVisualState
    {
        public readonly Vector3 From, To;
        public readonly float Progress, Duration;
        public readonly AttackKind Attack;
        public ProjectileVisualState(Vector3 from, Vector3 to, float progress, float duration, AttackKind attack)
        { From = from; To = to; Progress = progress; Duration = duration; Attack = attack; }
    }

    /// <summary>Damage and flight lifetime belong to simulation, even if every effect is disabled.</summary>
    public sealed class CombatWorld
    {
        struct Projectile
        {
            public int Id, TargetId, SourceId, Team;
            public Vector3 From, To;
            public float Elapsed, Duration, Damage;
            public AttackKind Attack;
            public WeaponProfile Weapon;
            public bool Miss, LegacyRules;
        }
        readonly BattleSession session;
        readonly List<Projectile> projectiles = new List<Projectile>(256);
        readonly Dictionary<int, int> projectileIndices = new Dictionary<int, int>(256);
        readonly List<CombatTarget> splash = new List<CombatTarget>(128);
        int nextId;
        public bool PresentationEnabled { get; set; } = true;
        public int ActiveProjectileCount => projectiles.Count;
        public int ResolvedProjectiles { get; private set; }
        public CombatWorld(BattleSession battle) { session = battle; }

        public int FireProjectile(Vector3 from, Vector3 to, CombatTarget target, float damage, int team,
            CombatTarget source = null, AttackKind attack = AttackKind.Piercing)
        {
            if (damage < 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return 0;
            // Public compatibility path for presentation fixtures and callers without
            // source weapon data. Its historical clamp and explicit legacy splash stay local here.
            float radius = attack == AttackKind.Magic ? 2.4f : attack == AttackKind.Siege ? 1.5f : 0;
            var weapon = new WeaponProfile(attack, WeaponDelivery.Missile, 25, WeaponTargeting.Target,
                0, radius, radius, radius > 0 ? attack == AttackKind.Magic ? .5f : .35f : 0,
                radius > 0 ? attack == AttackKind.Magic ? .5f : .35f : 0, .15f, .6f);
            return EnqueueProjectile(from, to, target, damage, team, source, weapon, legacyRules: true);
        }

        public int FireWeapon(Vector3 from, Vector3 to, CombatTarget target, float damage, int team,
            CombatTarget source, WeaponProfile weapon)
        {
            if (!weapon.IsValid || damage < 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return 0;
            bool miss = source && target && session.RollMiss(CombatRules.UphillMissChance(
                weapon.DamageType, target.transform.position.y - source.transform.position.y));
            if (!weapon.IsProjectile)
            {
                if (PresentationEnabled && source is Soldier soldier &&
                    (soldier.Kind == UnitKind.Archer || soldier.Kind == UnitKind.MarinePrivate))
                    VisualFactory.InstantProjectileView(from, to, weapon.DamageType);
                if (!miss && target && target.CanBeAttacked)
                    target.ReceiveAttack(damage, weapon.DamageType, team, source);
                return 0;
            }
            return EnqueueProjectile(from, to, target, damage, team, source, weapon, miss);
        }

        int EnqueueProjectile(Vector3 from, Vector3 to, CombatTarget target, float damage, int team,
            CombatTarget source, WeaponProfile weapon, bool? knownMiss = null, bool legacyRules = false)
        {
            var shot = new Projectile
            {
                Id = ++nextId, TargetId = target ? target.EntityId : 0, SourceId = source ? source.EntityId : 0,
                Team = team, From = from, To = to, Damage = damage, Attack = weapon.DamageType, Weapon = weapon,
                Miss = knownMiss ?? source && target && session.RollMiss(CombatRules.UphillMissChance(
                    weapon.DamageType, target.transform.position.y-source.transform.position.y)),
                Duration = weapon.FlightTime(Vector3.Distance(from, to)), LegacyRules = legacyRules
            };
            projectileIndices.Add(shot.Id, projectiles.Count);
            projectiles.Add(shot);
            if (PresentationEnabled) VisualFactory.ProjectileView(session, shot.Id, from, to, shot.Duration, weapon.DamageType);
            return shot.Id;
        }

        public bool TryGetProjectile(int id, out ProjectileVisualState state)
        {
            if (projectileIndices.TryGetValue(id, out int index))
            {
                var shot = projectiles[index];
                state = new ProjectileVisualState(shot.From, shot.To,
                    shot.Duration > 0 ? Mathf.Clamp01(shot.Elapsed / shot.Duration) : 1, shot.Duration, shot.Attack);
                return true;
            }
            state = default;
            return false;
        }

        public void Tick(float delta)
        {
            for (int i = 0; i < projectiles.Count;)
            {
                var shot = projectiles[i];
                var target = session.FindTarget(shot.TargetId);
                if (shot.Weapon.TracksTarget && target && target.IsAlive) shot.To = target.AimPoint;
                shot.Elapsed += delta;
                if (shot.Elapsed + .00001f < shot.Duration) { projectiles[i++] = shot; continue; }
                projectiles.RemoveAt(i);
                projectileIndices.Remove(shot.Id);
                for (int j = i; j < projectiles.Count; j++) projectileIndices[projectiles[j].Id] = j;
                var source = session.FindTarget(shot.SourceId);
                float radius = shot.Weapon.SmallDamageRadius;
                if (!shot.Miss && shot.LegacyRules)
                {
                    if (target && target.CanBeAttacked) target.ReceiveAttack(shot.Damage, shot.Attack, shot.Team, source);
                    if (radius > 0)
                    {
                        session.Spatial.Query(shot.To, radius, splash);
                        splash.Sort(EntityOrder.Instance);
                        for (int j = 0; j < splash.Count; j++)
                        {
                            var other = splash[j];
                            if (!other || !other.IsAlive || other.EntityId == shot.TargetId || other.Team == shot.Team) continue;
                            if (shot.Attack == AttackKind.Magic && !(other is Soldier)) continue;
                            if ((other.AimPoint - shot.To).sqrMagnitude < radius * radius)
                                other.ReceiveAttack(shot.Damage * (shot.Attack == AttackKind.Magic ? .5f : .35f), shot.Attack, shot.Team, source);
                        }
                    }
                }
                else if (!shot.Miss && shot.Weapon.HasSplash)
                {
                    // Resolve the primary by identity so a homing msplash shot cannot lose its
                    // intended hit to spatial sampling around an elevated or freshly moved aim point.
                    if (EligibleForSplash(shot, target))
                    {
                        Vector3 primaryOffset = target.AimPoint - shot.To;
                        primaryOffset.y = 0;
                        float primaryFactor = shot.Weapon.SplashFactor(primaryOffset.magnitude);
                        if (primaryFactor > 0) ApplySplashAttack(target, shot.Damage * primaryFactor, shot, source);
                    }
                    session.Spatial.Query(shot.To, radius, splash);
                    // Stable resolution also protects simultaneous area deaths from registry mutation.
                    splash.Sort(EntityOrder.Instance);
                    for (int j = 0; j < splash.Count; j++)
                    {
                        var other = splash[j];
                        if (other && other.EntityId == shot.TargetId) continue;
                        if (!EligibleForSplash(shot, other)) continue;
                        Vector3 offset = other.AimPoint - shot.To;
                        offset.y = 0;
                        float factor = shot.Weapon.SplashFactor(offset.magnitude);
                        if (factor > 0) ApplySplashAttack(other, shot.Damage * factor, shot, source);
                    }
                }
                else if (!shot.Miss && target && target.CanBeAttacked)
                    target.ReceiveAttack(shot.Damage, shot.Attack, shot.Team, source);
                ResolvedProjectiles++;
                if (PresentationEnabled)
                    VisualFactory.Impact(shot.To, shot.Attack, radius > 0 ? .75f : .32f);
            }
        }

        static bool EligibleForSplash(Projectile shot, CombatTarget target)
        {
            if (!target || !target.IsAlive) return false;
            var mask = shot.Weapon.SplashTargets;
            const WeaponTargetMask classes = WeaponTargetMask.Air | WeaponTargetMask.Debris |
                WeaponTargetMask.Ground | WeaponTargetMask.Item | WeaponTargetMask.Structure |
                WeaponTargetMask.Ward | WeaponTargetMask.Tree | WeaponTargetMask.Wall | WeaponTargetMask.Soldier;
            WeaponTargetMask targetClass = target is DefenseTower
                ? WeaponTargetMask.Structure
                : target is Soldier
                    ? WeaponTargetMask.Ground | WeaponTargetMask.Soldier
                    : target is Ship ? WeaponTargetMask.Ground : WeaponTargetMask.None;
            if ((mask & classes) != 0 && (mask & targetClass) == 0) return false;

            bool self = shot.SourceId != 0 && target.EntityId == shot.SourceId;
            if (self) return (mask & WeaponTargetMask.Self) != 0;

            const WeaponTargetMask relations = WeaponTargetMask.Enemy | WeaponTargetMask.Neutral | WeaponTargetMask.Ally;
            if ((mask & relations) == 0) return true;
            if (target.Team == shot.Team) return (mask & WeaponTargetMask.Ally) != 0;
            if (target.Team == PlayerRules.NeutralTeam || target.Team < 0)
                return (mask & WeaponTargetMask.Neutral) != 0;
            return (mask & WeaponTargetMask.Enemy) != 0;
        }

        static void ApplySplashAttack(CombatTarget target, float damage, Projectile shot, CombatTarget source)
        {
            if (target.Team != shot.Team)
            {
                target.ReceiveAttack(damage, shot.Attack, shot.Team, source);
                return;
            }
            // Direct attacks reject same-team damage. An unrestricted source splash mask
            // still damages allies, without crediting a friendly kill or provoking retaliation.
            target.ReceiveAttack(damage, shot.Attack, int.MinValue, null);
        }

        sealed class EntityOrder : IComparer<CombatTarget>
        {
            public static readonly EntityOrder Instance = new EntityOrder();
            public int Compare(CombatTarget a, CombatTarget b) => a.EntityId.CompareTo(b.EntityId);
        }
    }
}
