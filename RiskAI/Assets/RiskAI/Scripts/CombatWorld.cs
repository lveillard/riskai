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
            public bool Miss;
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
            var shot = new Projectile
            {
                Id = ++nextId, TargetId = target ? target.EntityId : 0, SourceId = source ? source.EntityId : 0,
                Team = team, From = from, To = to, Damage = damage, Attack = attack,
                Miss=source && target && session.RollMiss(CombatRules.UphillMissChance(attack,target.transform.position.y-source.transform.position.y)),
                Duration = Mathf.Clamp(Vector3.Distance(from, to) / 25, .15f, .6f)
            };
            projectileIndices.Add(shot.Id, projectiles.Count);
            projectiles.Add(shot);
            if (PresentationEnabled) VisualFactory.ProjectileView(session, shot.Id, from, to, shot.Duration, attack);
            return shot.Id;
        }

        public bool TryGetProjectile(int id, out ProjectileVisualState state)
        {
            if (projectileIndices.TryGetValue(id, out int index))
            {
                var shot = projectiles[index];
                state = new ProjectileVisualState(shot.From, shot.To, Mathf.Clamp01(shot.Elapsed / shot.Duration), shot.Duration, shot.Attack);
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
                if (target && target.IsAlive) shot.To = target.AimPoint;
                shot.Elapsed += delta;
                if (shot.Elapsed + .00001f < shot.Duration) { projectiles[i++] = shot; continue; }
                projectiles.RemoveAt(i);
                projectileIndices.Remove(shot.Id);
                for (int j = i; j < projectiles.Count; j++) projectileIndices[projectiles[j].Id] = j;
                var source = session.FindTarget(shot.SourceId);
                if (!shot.Miss && target && target.CanBeAttacked) target.ReceiveAttack(shot.Damage, shot.Attack, shot.Team, source);
                float radius = shot.Attack == AttackKind.Magic ? 2.4f : shot.Attack == AttackKind.Siege ? 1.5f : 0;
                if (radius > 0)
                {
                    session.Spatial.Query(shot.To, radius, splash);
                    // Stable resolution also protects simultaneous area deaths from registry mutation.
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
                ResolvedProjectiles++;
                if (PresentationEnabled)
                    VisualFactory.Impact(shot.To, shot.Attack == AttackKind.Magic ? new Color(.55f, .7f, 1) : new Color(1, .72f, .35f), radius > 0 ? .75f : .32f);
            }
        }
        sealed class EntityOrder : IComparer<CombatTarget>
        {
            public static readonly EntityOrder Instance = new EntityOrder();
            public int Compare(CombatTarget a, CombatTarget b) => a.EntityId.CompareTo(b.EntityId);
        }
    }
}
