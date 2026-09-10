using System.Collections;
using System.Linq;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class PresentationIsolationTests
    {
        Scene scene, previous;
        BattleSession battle;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            BattleSession.ModeForNewMatch = BattleSession.VictoryMode.Conquest;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.Fixed;
            previous = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("Presentation isolation");
            SceneManager.SetActiveScene(scene);
            new GameObject("Presentation isolation bootstrap").AddComponent<RiskBootstrap>();
            battle = BattleSession.Current;
            battle.AiEnabled = false;
            var controller = Object.FindFirstObjectByType<RtsController>();
            if (controller) controller.enabled = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyingProjectileViewsDoesNotCancelSimulationHit()
        {
            var source = battle.Units.First(u => u && u.Team == 0);
            var target = battle.Units.First(u => u && u.Team == PlayerRules.NeutralTeam);
            StopBackgroundCombat(source, target);
            battle.RegisterTarget(source);
            battle.RegisterTarget(target);
            source.enabled = false;
            target.enabled = false;

            float healthBefore = target.Health;
            int projectileId = battle.Combat.FireProjectile(source.AimPoint, target.AimPoint, target, 24, source.Team, source, AttackKind.Piercing);
            Assert.That(projectileId, Is.GreaterThan(0));

            var views = Object.FindObjectsOfType<ArrowFlight>();
            Assert.That(views.Length, Is.GreaterThan(0), "The simulation projectile should have a presentation view.");
            for (int i = 0; i < views.Length; i++)
            {
                if (!views[i]) continue;
                if ((i & 1) == 0) Object.Destroy(views[i].gameObject);
                else views[i].enabled = false;
            }

            yield return new WaitForSecondsRealtime(.85f);
            Assert.That(battle.Combat.ActiveProjectileCount, Is.Zero);
            Assert.That(target.Health, Is.LessThan(healthBefore), "Removing a projectile view must not remove its simulation hit.");
        }

        [UnityTest]
        public IEnumerator ImpactViewsReuseTheirBoundedPool()
        {
            StopBackgroundCombat(null, null);
            // Existing simulation projectiles can still resolve after their owners are
            // disabled. Let those unscaled presentation views drain before measuring
            // this pool's allocation count.
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(VisualFactory.ActiveImpactViewCount, Is.Zero);
            Color color = new Color(.52f, .71f, .96f);
            for (int i = 0; i < 24; i++) VisualFactory.Impact(new Vector3(300 + i, 0, 300), color, .2f);
            yield return null;
            int created = VisualFactory.ImpactPoolCreatedCount;
            Assert.That(created, Is.GreaterThan(0));

            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(VisualFactory.ActiveImpactViewCount, Is.Zero);
            for (int i = 0; i < 24; i++) VisualFactory.Impact(new Vector3(300 + i, 0, 300), color, .2f);
            yield return null;
            Assert.That(VisualFactory.ImpactPoolCreatedCount, Is.EqualTo(created), "A completed impact burst should be served by the existing pool.");
        }

        [UnityTest]
        public IEnumerator ProjectileProfilesArePooledVisualsAndDoNotNeedTargetsForSimulation()
        {
            StopBackgroundCombat(null, null);
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(VisualFactory.ActiveProjectileViewCount, Is.Zero);

            Vector3 from=new Vector3(280,2,280),to=new Vector3(290,2,280);
            battle.Combat.FireProjectile(from,to,null,0,0,null,AttackKind.Piercing);
            battle.Combat.FireProjectile(from,to,null,0,0,null,AttackKind.Magic);
            battle.Combat.FireProjectile(from,to,null,0,0,null,AttackKind.Siege);
            yield return null;

            var views=Object.FindObjectsOfType<ArrowFlight>();
            Assert.That(views.Any(view=>view.transform.Find("Piercing projectile").gameObject.activeSelf),Is.True);
            Assert.That(views.Any(view=>view.transform.Find("Magic projectile").gameObject.activeSelf),Is.True);
            Assert.That(views.Any(view=>view.transform.Find("Siege projectile").gameObject.activeSelf),Is.True);
            int created=VisualFactory.ProjectilePoolCreatedCount;
            Assert.That(created,Is.GreaterThanOrEqualTo(3));

            yield return new WaitForSecondsRealtime(.85f);
            Assert.That(battle.Combat.ActiveProjectileCount,Is.Zero);
            Assert.That(VisualFactory.ActiveProjectileViewCount,Is.Zero);
            battle.Combat.FireProjectile(from,to,null,0,0,null,AttackKind.Siege);
            yield return null;
            Assert.That(VisualFactory.ProjectilePoolCreatedCount,Is.EqualTo(created));
        }

        [UnityTest]
        public IEnumerator CrossbowBoltDealsDamageOnlyWhenItsVisibleFlightArrives()
        {
            var source=battle.Units.First(unit=>unit&&unit.Team==0&&unit.Kind==UnitKind.Archer);
            var target=battle.Units.First(unit=>unit&&unit.Team==PlayerRules.NeutralTeam);
            StopBackgroundCombat(source,target);source.enabled=false;target.enabled=false;
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(VisualFactory.ActiveProjectileViewCount,Is.Zero);
            float health=target.Health;
            Vector3 launch=target.AimPoint+Vector3.left*8;
            int projectile=battle.Combat.FireWeapon(launch,target.AimPoint,target,12,source.Team,source,
                SourceWeapons.For(UnitKind.Archer,AttackKind.Piercing));
            Assert.That(projectile,Is.GreaterThan(0));
            Assert.That(target.Health,Is.EqualTo(health),"The bolt cannot deal damage before contact.");
            Assert.That(battle.Combat.ActiveProjectileCount,Is.EqualTo(1));
            Assert.That(VisualFactory.ActiveProjectileViewCount,Is.EqualTo(1),"The simulated bolt needs a readable presentation view.");
            yield return new WaitForSecondsRealtime(.7f);
            Assert.That(target.Health,Is.LessThan(health),"Damage resolves when the bolt reaches its target.");
            Assert.That(battle.Combat.ActiveProjectileCount,Is.Zero);
            Assert.That(VisualFactory.ActiveProjectileViewCount,Is.Zero);
        }

        [UnityTest]
        public IEnumerator MarinePrivateHasItsOwnPiratePistolPresentation()
        {
            var marine=battle.Spawn(0,UnitKind.MarinePrivate,battle.Towns.First(t=>t.State.Owner==0).Rally);
            Assert.That(marine,Is.Not.Null);
            var parts=marine.GetComponentsInChildren<Transform>(true);
            Assert.That(parts.Any(part=>part.name=="Marine private identity"),Is.True);
            Assert.That(parts.Any(part=>part.name=="Short flintlock pistol"&&part.gameObject.activeInHierarchy),Is.True);
            Assert.That(parts.Any(part=>part.name=="Tricorn crown"&&part.gameObject.activeInHierarchy),Is.True);
            Assert.That(parts.Any(part=>part.name=="Rogue_Head_Hooded"&&part.gameObject.activeInHierarchy),Is.False);
            Assert.That(parts.Any(part=>part.name=="2H_Crossbow"&&part.gameObject.activeInHierarchy),Is.False);
            yield return null;
        }

        void StopBackgroundCombat(CombatTarget keepA, CombatTarget keepB)
        {
            foreach (var tower in battle.Towers.ToArray())
            {
                if (!tower) continue;
                // Towers are permanent capturable structures and intentionally
                // ignore TakeDamage; disable them before isolating the fixture.
                if (tower) tower.enabled = false;
            }
            foreach (var unit in battle.Units.ToArray())
            {
                if (!unit || unit == keepA || unit == keepB) continue;
                unit.enabled = false;
                if (unit.Agent) unit.Agent.enabled = false;
            }
            if (battle.Naval)
                foreach (var ship in battle.Naval.Ships.ToArray())
                    if (ship) ship.gameObject.SetActive(false);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            BattleSession.LayoutForNewMatch = BattleSession.StartLayout.RandomCities;
            SceneManager.SetActiveScene(previous);
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
