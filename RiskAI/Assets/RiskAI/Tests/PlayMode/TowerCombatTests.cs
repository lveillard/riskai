using System.Collections;
using System.Linq;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class TowerCombatTests
    {
        Scene scene, previous;
        BattleSession battle;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            BattleSession.ModeForNewMatch=BattleSession.VictoryMode.Conquest;
            BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;
            previous=SceneManager.GetActiveScene(); scene=SceneManager.CreateScene("Tower combat"); SceneManager.SetActiveScene(scene);
            new GameObject("Tower combat bootstrap").AddComponent<RiskBootstrap>();
            battle=BattleSession.Current; battle.AiEnabled=false;
            Object.FindFirstObjectByType<RtsController>().enabled=false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TypedAttacksDamageEnemyAndNeutralButNeverAnAlly()
        {
            var ally=battle.Units.First(u=>u.Team==0); float allyHealth=ally.Health;
            ally.ReceiveAttack(100,AttackKind.Siege,0);
            Assert.That(ally.Health,Is.EqualTo(allyHealth),"Friendly attacks must be ignored.");

            var enemy=battle.Units.First(u=>u.Team==1); float enemyHealth=enemy.Health;
            enemy.ReceiveAttack(20,AttackKind.Normal,0);
            Assert.That(enemy.Health,Is.LessThan(enemyHealth));

            var neutral=battle.Units.First(u=>u.Team==2); float neutralHealth=neutral.Health;
            neutral.ReceiveAttack(20,AttackKind.Piercing,0);
            Assert.That(neutral.Health,Is.LessThan(neutralHealth));

            int kills=battle.Kills[0];
            enemy.ReceiveAttack(enemy.Health/CombatRules.ResolveDamage(1,AttackKind.Siege,enemy.ArmorType,enemy.Armor)+1,AttackKind.Siege,0);
            enemy.ReceiveAttack(10000,AttackKind.Siege,0); // A dead target must not award another kill.
            yield return null;
            Assert.That(battle.Kills[0],Is.EqualTo(kills+1));
        }

        [UnityTest]
        public IEnumerator TowerFiresAtNeutralUnitsInRange()
        {
            var tower=battle.Towns.First(t=>t.State.Owner==0&&t.IsCapital).Defense;
            var neutral=battle.Units.First(u=>u.Team==2);
            Assert.That(neutral.Agent.Warp(tower.transform.position+Vector3.forward*7),Is.True);
            float before=neutral.Health;
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(tower.ShotsFired,Is.GreaterThan(0));
            Assert.That(neutral.Health,Is.LessThan(before));
        }

        [UnityTest]
        public IEnumerator MortarAttacksLivingDefenderOutsideTowerRange()
        {
            var town=battle.Towns.First(t=>t.State.Owner==1&&t.IsCapital&&t.Defender);
            var tower=town.Defense;
            var defender=town.Defender;
            Soldier mortar=null;
            for(int i=0;i<32&&mortar==null;i++)
            {
                float angle=i*Mathf.PI/16;
                var candidate=tower.transform.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*15f;
                if(NavMesh.SamplePosition(candidate,out var hit,.8f,NavMesh.AllAreas) &&
                   Vector3.Distance(hit.position,tower.transform.position)>ReforgedProfiles.CapturableTower.Range+.5f &&
                   Vector3.Distance(hit.position,defender.transform.position)<=BattleRules.Range(UnitKind.Mortar) &&
                   !Physics.Linecast(hit.position+Vector3.up,defender.AimPoint,1<<MapLayout.TerrainLayer,QueryTriggerInteraction.Ignore))
                    mortar=battle.Spawn(0,UnitKind.Mortar,hit.position);
            }
            Assert.That(mortar,Is.Not.Null,"A mortar test position must be on the baked practice NavMesh.");
            KeepOnly(mortar,defender);
            mortar.HoldPosition();
            float mortarHealth=mortar.Health,defenderHealth=defender.Health,towerHealth=tower.Health;
            yield return new WaitForSecondsRealtime(6f);
            Assert.That(Vector3.Distance(mortar.transform.position,tower.transform.position),Is.GreaterThan(ReforgedProfiles.CapturableTower.Range));
            Assert.That(defender.Health,Is.LessThan(defenderHealth),"The mortar must attack the living tower defender from outside tower range.");
            Assert.That(tower.Health,Is.EqualTo(towerHealth),"Permanent towers are not damageable targets.");
            Assert.That(mortar.Health,Is.EqualTo(mortarHealth),"A mortar outside tower range must not be hit in return.");
        }

        [UnityTest]
        public IEnumerator SiegeProjectileKeepsItsTypeAfterItsSourceDiesAndHitsOnce()
        {
            var source=battle.Units.First(u=>u.Team==0);
            var target=battle.Units.First(u=>u.Team==1);
            foreach(var unit in battle.Units.ToArray())if(unit!=source&&unit!=target){unit.enabled=false;if(unit.Agent)unit.Agent.enabled=false;}
            source.enabled=false; if(source.Agent)source.Agent.enabled=false;
            target.enabled=false; if(target.Agent)target.Agent.enabled=false;
            float before=target.Health;
            VisualFactory.Arrow(source.AimPoint,target.AimPoint,target,32,source.Team,source,AttackKind.Siege);
            Object.Destroy(source.gameObject);
            yield return new WaitForSecondsRealtime(.8f);
            float expected=CombatRules.ResolveDamage(32,AttackKind.Siege,target.ArmorType,target.Armor);
            Assert.That(target.Health,Is.EqualTo(before-expected).Within(.001f));
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(target.Health,Is.EqualTo(before-expected).Within(.001f),"One projectile must resolve one hit after its source is destroyed.");
        }

        [UnityTest]
        public IEnumerator TerrainBlocksTowerLineOfSightUntilRemoved()
        {
            var town=battle.Towns.First(t=>t.State.Owner==0&&t.IsCapital&&t.Defender);
            var tower=town.Defense;
            var defender=town.Defender;
            var enemy=battle.Units.First(u=>u.Team==1);
            Assert.That(enemy.Agent.Warp(tower.transform.position+Vector3.forward*7),Is.True);
            KeepOnly(enemy,defender); enemy.enabled=false; if(enemy.Agent)enemy.Agent.enabled=false;
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="LOS terrain wall";wall.layer=MapLayout.TerrainLayer;
            wall.transform.position=tower.transform.position+Vector3.forward*3.5f+Vector3.up*2;wall.transform.localScale=new Vector3(4,4,.5f);
            Physics.SyncTransforms();
            float before=enemy.Health;
            yield return new WaitForSecondsRealtime(1.4f);
            Assert.That(tower.ShotsFired,Is.Zero,"Terrain must block tower fire.");
            Assert.That(enemy.Health,Is.EqualTo(before));
            Object.Destroy(wall);
            yield return new WaitForSecondsRealtime(1.8f);
            Assert.That(tower.ShotsFired,Is.GreaterThan(0));
            Assert.That(enemy.Health,Is.LessThan(before));
        }

        void KeepOnly(params Soldier[] survivors)
        {
            foreach(var unit in battle.Units.ToArray())if(!survivors.Contains(unit))
            {
                unit.enabled=false;if(unit.Agent)unit.Agent.enabled=false;
                battle.Units.Remove(unit);battle.Targets.Remove(unit);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale=1;BattleSession.LayoutForNewMatch=BattleSession.StartLayout.RandomCities; SceneManager.SetActiveScene(previous); yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
