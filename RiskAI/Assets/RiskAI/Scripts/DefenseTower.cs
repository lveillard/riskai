using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;

namespace RiskAI
{
    public sealed class DefenseTower : CombatTarget
    {
        public Settlement Town { get; private set; }
        public Harbor Harbor { get; private set; }
        public int HostOwner => Town ? Town.State.Owner : Harbor ? Harbor.Owner : -1;
        public string HostName => Town ? Town.DisplayName : Harbor ? Harbor.DisplayName : "torre";
        public override float MaxHealth => BattleRules.TowerHealth;
        public override Vector3 AimPoint => transform.position + Vector3.up * 2.8f;
        public override AttackKind AttackType => AttackKind.Piercing;
        public override ArmorKind ArmorType => ArmorKind.Fortified;
        public override float Armor => 3;
        public bool UnderConstruction { get; private set; }
        public float BuildProgress { get; private set; }
        public int ShotsFired { get; private set; }
        public CombatTarget CurrentTarget { get; private set; }
        BattleSession session;
        GameObject upper, scaffolding;
        Renderer banner;
        float nextShot;
        readonly System.Collections.Generic.List<CombatTarget> nearby = new System.Collections.Generic.List<CombatTarget>(48);
        float AttackCooldown=>ReforgedProfiles.Tower.Cooldown;
        float AttackRange=>ReforgedProfiles.Tower.Range;

        public void Initialize(BattleSession battle, Settlement town, bool built)
        {
            session = battle; Town = town; Harbor = null; Team = CombatTeam(town.State.Owner);
            VisualFactory.Tower(transform, Team, out upper, out scaffolding, out banner);
            // The permanent stone foundation defines this slot's NavMesh footprint.
            var targetCollider = gameObject.AddComponent<BoxCollider>();
            targetCollider.center = Vector3.up * 1.6f; targetCollider.size = new Vector3(1.8f, 3.3f, 1.8f);
            targetCollider.isTrigger = true;
            session.Towers.Add(this);
            if (built) CompleteBuild(); else RefreshVisuals();
        }

        public void Initialize(BattleSession battle, Harbor harbor, bool built)
        {
            session = battle; Town = null; Harbor = harbor; Team = CombatTeam(harbor.Owner);
            VisualFactory.Tower(transform, Team, out upper, out scaffolding, out banner);
            var targetCollider = gameObject.AddComponent<BoxCollider>();
            targetCollider.center = Vector3.up * 1.6f; targetCollider.size = new Vector3(1.8f, 3.3f, 1.8f);
            targetCollider.isTrigger = true;
            var obstacle = gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box; obstacle.center = Vector3.up * .4f; obstacle.size = new Vector3(2.8f, .8f, 2.8f);
            obstacle.carving = true; obstacle.carveOnlyStationary = true;
            session.Towers.Add(this);
            if (built) CompleteBuild(); else RefreshVisuals();
        }

        public override Vector3 ApproachPoint(Vector3 from)
        {
            Vector3 direction = from - transform.position; direction.y = 0;
            direction = direction.sqrMagnitude > .001f ? direction.normalized : Vector3.forward;
            // Approach the square foundation from outside the NavMesh obstacle, including diagonals.
            var point=transform.position + direction * (1.75f / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.z)));
            // The NavMesh interpolates ground height beside the stepped foundation.
            return NavMesh.SamplePosition(point,out var hit,.8f,NavMesh.AllAreas)?hit.position:point;
        }

        public void BeginBuild()
        {
            Team = CombatTeam(HostOwner); UnderConstruction = true; BuildProgress = 0; CurrentTarget=null; RefreshVisuals();
        }
        public void SetBuildProgress(float progress) { BuildProgress = progress; }
        public void CancelBuild() { UnderConstruction = false; BuildProgress = 0; RefreshVisuals(); }
        public void CompleteBuild()
        {
            Team = CombatTeam(HostOwner); Health = MaxHealth; UnderConstruction = false; BuildProgress = 1;
            CurrentTarget=null;nextShot=0;
            session.RegisterTarget(this);
            RefreshVisuals();
        }
        public void ChangeOwner() { Team = CombatTeam(HostOwner); CurrentTarget=null; nextShot=Mathf.Max(nextShot,session.BattleTime+.2f); RefreshVisuals(); }
        void RefreshVisuals()
        {
            upper.SetActive(IsAlive); scaffolding.SetActive(UnderConstruction);
            banner.sharedMaterial = VisualFactory.Mat(VisualFactory.TeamColor(Team));
            foreach(var roof in upper.GetComponentsInChildren<Renderer>())if(roof.name=="Faction roof")roof.sharedMaterial=WorldArt.RoofMaterial(Team);
        }

        public void SimTick(float delta)
        {
            if (!IsAlive || UnderConstruction) { CurrentTarget=null; return; }
            if (session.Paused || session.Winner >= 0) return;
            if (!IsValidTarget(CurrentTarget)) CurrentTarget=FindTarget();
            if (!CurrentTarget || session.BattleTime < nextShot) return;
            nextShot = session.BattleTime + AttackCooldown; ShotsFired++;
            session.Combat.FireProjectile(AimPoint + Vector3.up, CurrentTarget.AimPoint, CurrentTarget, session.RollDamage(ReforgedProfiles.Tower), Team, this, AttackType);
        }

        CombatTarget FindTarget()
        {
            CombatTarget best=null;float bestDistance=float.MaxValue;
            session.Spatial.Query(transform.position,AttackRange,nearby);
            foreach(var unit in nearby)
            {
                if(!IsValidTarget(unit))continue;
                float distance=DistanceXZ(transform.position,unit.transform.position);
                if(distance<bestDistance){best=unit;bestDistance=distance;}
            }
            return best;
        }
        bool IsValidTarget(CombatTarget candidate)
        {
            if(!candidate||!candidate.IsAlive||candidate.Team==Team||candidate.Team<0)return false;
            if(DistanceXZ(transform.position,candidate.transform.position)>AttackRange)return false;
            Vector3 from=AimPoint,to=candidate.AimPoint,delta=to-from;
            return delta.sqrMagnitude<.001f||!Physics.Raycast(from,delta.normalized,delta.magnitude,1<<MapLayout.TerrainLayer,QueryTriggerInteraction.Ignore);
        }
        static float DistanceXZ(Vector3 a,Vector3 b)
        {
            a.y=b.y=0;return Vector3.Distance(a,b);
        }

        public override void TakeDamage(float damage, int attacker, CombatTarget source = null)
        {
            if (!IsAlive || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage) || attacker == Team) return;
            Health = Mathf.Max(0, Health - damage);
            if (IsAlive) return;
            CurrentTarget=null;
            session.UnregisterTarget(this); RefreshVisuals();
            VisualFactory.Impact(AimPoint, new Color(.9f, .65f, .3f), 1.1f);
            session.Message("Ha caído la torre de " + HostName + ".");
        }

        static int CombatTeam(int owner) => owner >= 0 && owner <= 1 ? owner : 2;
    }
}
