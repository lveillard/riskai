using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    public readonly struct ProjectileVisualState
    {
        public readonly Vector3 From, To, Position;
        public readonly float Progress, Duration;
        public readonly ProjectileLook Look;
        public ProjectileVisualState(Vector3 from, Vector3 to, Vector3 position, float progress, float duration, ProjectileLook look)
        { From = from; To = to; Position = position; Progress = progress; Duration = duration; Look = look; }
    }

    /// <summary>Damage and flight lifetime belong to simulation, even if every effect is disabled.</summary>
    public sealed class CombatWorld
    {
        struct Projectile
        {
            public int Id, TargetId, SourceId, Team;
            public Vector3 From, To, Position;
            public float Elapsed, Duration, Damage;
            public AttackKind Attack;
            public WeaponProfile Weapon;
            public bool Miss;
        }
        readonly BattleSession session;
        readonly List<Projectile> projectiles = new List<Projectile>(256);
        readonly Dictionary<int, int> projectileIndices = new Dictionary<int, int>(256);
        readonly List<CombatTarget> splash = new List<CombatTarget>(128);
        int nextId,firstProjectileCreatedThisTick=int.MaxValue;
        public bool PresentationEnabled { get; set; } = true;
        public int ActiveProjectileCount => projectiles.Count;
        public int ResolvedProjectiles { get; private set; }
        public CombatWorld(BattleSession battle) { session = battle; }
        public void BeginSimulationTick() => firstProjectileCreatedThisTick=nextId+1;

        public int FireWeapon(Vector3 from, Vector3 to, CombatTarget target, float damage, int team,
            CombatTarget source, WeaponProfile weapon)
        {
            if (!weapon.IsValid || damage < 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return 0;
            bool miss = source && target && session.RollMiss(CombatRules.UphillMissChance(
                weapon.DamageType, target.transform.position.y - source.transform.position.y));
            session.Feedback.RaiseFired(source, from, to, weapon.FeedbackAttack);
            if (!weapon.IsProjectile)
            {
                if (PresentationEnabled && weapon.Projectile != ProjectileLook.None)
                    VisualFactory.InstantProjectileView(from, to, weapon.Projectile);
                if (!miss && target && target.CanBeAttacked)
                {
                    target.ReceiveAttack(damage, weapon.DamageType, team, source);
                    session.Feedback.RaiseImpact(to, weapon.DamageType, 0, ImpactKind.Instant, source);
                }
                return 0;
            }
            return EnqueueProjectile(from, to, target, damage, team, source, weapon, miss);
        }

        int EnqueueProjectile(Vector3 from, Vector3 to, CombatTarget target, float damage, int team,
            CombatTarget source, WeaponProfile weapon, bool? knownMiss = null)
        {
            var shot = new Projectile
            {
                Id = ++nextId, TargetId = target ? target.EntityId : 0, SourceId = source ? source.EntityId : 0,
                Team = team, From = from, To = to, Position = from, Damage = damage, Attack = weapon.DamageType, Weapon = weapon,
                Miss = knownMiss ?? source && target && session.RollMiss(CombatRules.UphillMissChance(
                    weapon.DamageType, target.transform.position.y-source.transform.position.y)),
                Duration = weapon.FlightTime(Vector3.Distance(from, to))
            };
            projectileIndices.Add(shot.Id, projectiles.Count);
            projectiles.Add(shot);
            if (PresentationEnabled) VisualFactory.ProjectileView(session, shot.Id, from, to, shot.Duration, weapon.Projectile);
            return shot.Id;
        }

        public bool TryGetProjectile(int id, out ProjectileVisualState state)
        {
            if (projectileIndices.TryGetValue(id, out int index))
            {
                var shot = projectiles[index];
                float progress=shot.Duration>0?Mathf.Clamp01(shot.Elapsed/shot.Duration):1;
                Vector3 position=shot.Weapon.Delivery==WeaponDelivery.Artillery
                    ? ArcPosition(shot.From,shot.To,progress)
                    : shot.Position;
                state = new ProjectileVisualState(shot.From, shot.To, position, progress, shot.Duration, shot.Weapon.Projectile);
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
                // A shot launched by Soldier/Tower/Ship earlier in this simulation
                // tick starts travelling on the next quantum instead of receiving a
                // free 50 ms step immediately at release.
                if(shot.Id>=firstProjectileCreatedThisTick){projectiles[i++]=shot;continue;}
                shot.Elapsed += delta;
                bool arrived;
                if(shot.Weapon.Delivery==WeaponDelivery.Artillery)
                    arrived=shot.Elapsed+.00001f>=shot.Duration;
                else
                {
                    Vector3 remaining=shot.To-shot.Position;
                    float step=shot.Weapon.ProjectileSpeed*delta;
                    arrived=remaining.sqrMagnitude<=step*step+.000001f;
                    shot.Position=arrived?shot.To:shot.Position+remaining.normalized*step;
                }
                if (!arrived) { projectiles[i++] = shot; continue; }
                projectiles.RemoveAt(i);
                projectileIndices.Remove(shot.Id);
                for (int j = i; j < projectiles.Count; j++) projectileIndices[projectiles[j].Id] = j;
                var source = session.FindTarget(shot.SourceId);
                float radius = shot.Weapon.SmallDamageRadius;
                if (!shot.Miss && shot.Weapon.HasSplash)
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
                {
                    VisualFactory.Impact(shot.To, shot.Attack, radius > 0 ? .75f : .32f);
                    session.Feedback.RaiseImpact(shot.To, shot.Weapon.FeedbackAttack, radius, ImpactKind.Projectile, source);
                }
            }
            firstProjectileCreatedThisTick=int.MaxValue;
        }

        static Vector3 ArcPosition(Vector3 from,Vector3 to,float progress)
        {
            float arc=Mathf.Lerp(1.6f,3.4f,Mathf.Clamp01(Vector3.Distance(from,to)/18f));
            return Vector3.Lerp(from,to,progress)+Vector3.up*Mathf.Sin(progress*Mathf.PI)*arc;
        }

        static bool EligibleForSplash(Projectile shot, CombatTarget target)
        {
            if (!target || !target.IsAlive) return false;
            // The same target-flag rule as direct attacks, with the splash mask.
            bool self = shot.SourceId != 0 && target.EntityId == shot.SourceId;
            var relation = UnitRules.Relation(shot.Team, target.Team, self, PlayerRules.NeutralTeam);
            return UnitRules.Allows(shot.Weapon.SplashTargets, UnitRules.TargetClass(target.Type), relation);
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
