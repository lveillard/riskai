using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;

namespace RiskAI
{
    public sealed class DefenseTower : CombatTarget
    {
        public Settlement Town { get; private set; }
        public Harbor Harbor { get; private set; }
        public BuildingVariant VisualVariant { get; private set; }
        public int HostOwner => Town ? Town.State.Owner : Harbor ? Harbor.Owner : -1;
        public string HostName => Town ? Town.DisplayName : Harbor ? Harbor.DisplayName : "torre";
        public override ref readonly UnitType Type => ref UnitCatalog.Get(UnitKind.Tower);
        public override float MaxHealth => Type.MaxHealth;
        /// <summary>units.json: a capturable post is never a target; attack its guardian.</summary>
        public override bool CanBeAttacked => Type.CanBeAttacked && IsAlive;
        public Soldier Defender => Town ? Town.Defender : Harbor ? Harbor.Defender : null;
        public CombatTarget Guardian => Town ? Town.ClaimZone.Guardian : Harbor ? Harbor.ClaimZone.Guardian : null;
        public override Vector3 AimPoint => transform.position + Vector3.up *
            (BuildingVariants.IsIntegrated(VisualVariant)?VisualMetrics.IntegratedTowerGalleryHeight:2.8f);
        public Vector3 AttackOrigin => transform.position + Vector3.up *
            (BuildingVariants.IsIntegrated(VisualVariant)?VisualMetrics.IntegratedTowerAttackHeight:3.8f);
        public override AttackKind AttackType => HostWeapon.DamageType;
        public override ArmorKind ArmorType => Type.ArmorType;
        public override float Armor => Type.Armor;
        /// <summary>The post weapon depends on its host building (h00N city / h00O shipyard).</summary>
        ref readonly WeaponProfile HostWeapon => ref (Town ? ref Type.TownWeapon : ref Type.HarborWeapon);
        public override ref readonly WeaponProfile AttackWeapon => ref HostWeapon;
        public bool UnderConstruction { get; private set; }
        public float BuildProgress { get; private set; }
        public int ShotsFired { get; private set; }
        public CombatTarget CurrentTarget { get; private set; }
        public bool IsWindingUp => launchAt >= 0;
        BattleSession session;
        GameObject upper, scaffolding;
        Renderer banner;
        float nextShot, launchAt = -1;
        int launchTargetId;
        readonly System.Collections.Generic.List<CombatTarget> nearby = new System.Collections.Generic.List<CombatTarget>(48);
        float AttackCooldown=>HostWeapon.Cooldown;
        float AttackRange=>Type.Acquisition.RadiusHostile;

        public void Initialize(BattleSession battle, Settlement town, bool built, BuildingVariant? visualVariant=null)
        {
            session = battle; Town = town; Harbor = null; Team = CombatTeam(town.State.Owner);
            VisualVariant=visualVariant??town.VisualVariant;
            VisualFactory.Tower(transform, Team,VisualVariant,out upper, out scaffolding, out banner);
            // Targeting stays trigger-only. The selected host variant/terrain owns
            // navigation collision, so an integrated turret adds no second footprint.
            var targetCollider = gameObject.AddComponent<BoxCollider>();
            targetCollider.center = Vector3.up * 1.6f; targetCollider.size = new Vector3(1.8f, 3.3f, 1.8f);
            targetCollider.isTrigger = true;
            session.Towers.Add(this);
            if (built) CompleteBuild(); else RefreshVisuals();
        }

        public void Initialize(BattleSession battle, Harbor harbor, bool built, BuildingVariant? visualVariant=null)
        {
            session = battle; Town = null; Harbor = harbor; Team = CombatTeam(harbor.Owner);
            VisualVariant=visualVariant??harbor.VisualVariant;
            VisualFactory.Tower(transform, Team,VisualVariant,out upper, out scaffolding, out banner);
            var targetCollider = gameObject.AddComponent<BoxCollider>();
            targetCollider.center = Vector3.up * 1.6f; targetCollider.size = new Vector3(1.8f, 3.3f, 1.8f);
            targetCollider.isTrigger = true;
            if(VisualVariant==BuildingVariant.PierHarbor)
            {
                var obstacle = gameObject.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box; obstacle.center = Vector3.up * .4f; obstacle.size = new Vector3(2.8f, .8f, 2.8f);
                obstacle.carving = true; obstacle.carveOnlyStationary = true;
            }
            session.Towers.Add(this);
            if (built) CompleteBuild(); else RefreshVisuals();
        }

        public override Vector3 ApproachPoint(Vector3 from)
        {
            Vector3 direction = from - transform.position; direction.y = 0;
            direction = direction.sqrMagnitude > .001f ? direction.normalized : Vector3.forward;
            // Approach the square foundation from outside the NavMesh obstacle, including diagonals.
            var point=transform.position + direction * (Type.FootprintSize * .5f / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.z)));
            // The NavMesh interpolates ground height beside the stepped foundation.
            return NavMesh.SamplePosition(point,out var hit,.8f,NavMesh.AllAreas)?hit.position:point;
        }

        public void BeginBuild()
        {
            Team = CombatTeam(HostOwner); UnderConstruction = true; BuildProgress = 0; CurrentTarget=null; CancelLaunch(); RefreshVisuals();
        }
        public void SetBuildProgress(float progress) { BuildProgress = progress; }
        public void CancelBuild() { UnderConstruction = false; BuildProgress = 0; CancelLaunch(); RefreshVisuals(); }
        public void CompleteBuild()
        {
            Team = CombatTeam(HostOwner); Health = MaxHealth; UnderConstruction = false; BuildProgress = 1;
            CurrentTarget=null;nextShot=0;CancelLaunch();
            session.RegisterTarget(this);
            RefreshVisuals();
        }
        public void ChangeOwner() { Team = CombatTeam(HostOwner); CurrentTarget=null; CancelLaunch(); nextShot=Mathf.Max(nextShot,session.BattleTime+.2f); RefreshVisuals(); }
        void RefreshVisuals()
        {
            upper.SetActive(IsAlive); scaffolding.SetActive(UnderConstruction);
            banner.sharedMaterial = VisualFactory.Mat(VisualFactory.TeamMaterialColor(Team));
            foreach(var roof in upper.GetComponentsInChildren<Renderer>())if(roof.name=="Faction roof"||roof.name=="Faction pennant")roof.sharedMaterial=WorldArt.RoofMaterial(Team);
        }

        public void SimTick(float delta)
        {
            if (!IsAlive || UnderConstruction || !Guardian || !Guardian.IsAlive) { CurrentTarget=null; CancelLaunch(); return; }
            if (session.Paused || session.Winner >= 0) return;
            if (launchAt >= 0 && session.BattleTime >= launchAt)
            {
                var launchTarget = session.FindTarget(launchTargetId);
                CancelLaunch();
                if (IsValidTarget(launchTarget))
                {
                    CurrentTarget = launchTarget;
                    ShotsFired++;
                    ref readonly var weapon = ref HostWeapon;
                    session.Combat.FireWeapon(AttackOrigin, launchTarget.AimPoint, launchTarget,
                        session.RollDamage(weapon), Team, this, weapon);
                }
            }
            if (!IsValidTarget(CurrentTarget)) CurrentTarget=FindTarget();
            if (!CurrentTarget || launchAt >= 0 || session.BattleTime < nextShot) return;
            nextShot = session.BattleTime + AttackCooldown;
            launchAt = session.BattleTime + HostWeapon.AttackPoint;
            launchTargetId = CurrentTarget.EntityId;
        }

        void CancelLaunch() { launchAt = -1; launchTargetId = 0; }

        CombatTarget FindTarget() => UnitTargeting.Acquire(session, this, Team, Type, AttackRange, false, default, 0, nearby);
        bool IsValidTarget(CombatTarget candidate)
        {
            if(!UnitTargeting.CanTarget(this,Team,HostWeapon,candidate))return false;
            if(UnitTargeting.WeaponDistance(this,HostWeapon,candidate)>AttackRange)return false;
            return UnitTargeting.Visible(Type.Acquisition.Visibility,this,candidate);
        }

        public override void TakeDamage(float damage, int attacker, CombatTarget source = null)
        {
            // Permanent capturable structure: attack the soldier in its circle.
        }

        static int CombatTeam(int owner) => PlayerRules.ToCombatTeam(owner);
    }
}
