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
        int previousFrameRate, previousVSync;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousFrameRate=Application.targetFrameRate;previousVSync=QualitySettings.vSyncCount;
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
            var neutral=BattleTestScenario.Mobile(battle,2,UnitKind.Footman,tower.transform.position+Vector3.forward*7);
            float before=neutral.Health;
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(tower.ShotsFired,Is.GreaterThan(0));
            Assert.That(neutral.Health,Is.LessThan(before));
        }

        [UnityTest]
        public IEnumerator LoneArcherCanApproachFromFarSideKillGuardAndClaimWithoutTowerFire()
        {
            Settlement town=null;Vector3 start=default;
            foreach(var candidate in battle.Towns.Where(t=>t.State.Owner==1&&t.Defender))
            {
                var away=candidate.ClaimPoint-candidate.Defense.transform.position;away.y=0;away.Normalize();
                var probe=candidate.ClaimPoint+away*(BattleRules.Range(UnitKind.Archer)+2);
                probe=MapLayout.Point(probe.x,probe.z);
                if(!NavMesh.SamplePosition(probe,out var hit,.8f,NavMesh.AllAreas))continue;
                var firing=candidate.ClaimPoint+away*(BattleRules.Range(UnitKind.Archer)-.15f);
                firing=MapLayout.Point(firing.x,firing.z);
                if(!NavMesh.SamplePosition(firing,out var fireHit,.4f,NavMesh.AllAreas))continue;
                if(Vector3.Distance(fireHit.position,candidate.ClaimPoint)>BattleRules.Range(UnitKind.Archer))continue;
                var path=new NavMeshPath();
                if(!NavMesh.CalculatePath(hit.position,fireHit.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                town=candidate;start=hit.position;break;
            }
            Assert.That(town,Is.Not.Null,"A city must have a walkable far-side approach for this tactic.");
            var tower=town.Defense;
            var guard=battle.Spawn(1,UnitKind.Footman,town.ClaimPoint);
            town.ClaimZone.SetDefender(guard);
            var archer=battle.Spawn(0,UnitKind.Archer,start);
            Assert.That(archer,Is.Not.Null);
            var otherEnemy=battle.Towns.First(t=>t!=town&&t.State.Owner==1&&t.Defender).Defender;
            var otherAlly=battle.Towns.First(t=>t.State.Owner==0&&t.Defender).Defender;
            KeepOnly(archer,guard,otherEnemy,otherAlly); // Keep the match alive after this guard dies.
            int shots=tower.ShotsFired;float health=archer.Health;
            QualitySettings.vSyncCount=0;Application.targetFrameRate=10;
            Time.timeScale=4; // Exercise long movement frames between the 20 Hz combat ticks.
            archer.Attack(guard);
            float deadline=Time.realtimeSinceStartup+40;
            while(guard.IsAlive&&Time.realtimeSinceStartup<deadline)
            {
                Assert.That(tower.ShotsFired,Is.EqualTo(shots),
                    $"Far-side approach: town={town.DisplayName}, expanded={MapLayout.IsExpanded}, " +
                    $"start={start}, archer={archer.transform.position}, guard={guard.transform.position}, tower={tower.transform.position}, " +
                    $"towerTarget={(tower.CurrentTarget ? tower.CurrentTarget.name : "none")}, " +
                    $"range={Vector3.Distance(archer.transform.position,guard.transform.position)}, stop={archer.Agent.stoppingDistance}, " +
                    $"remaining={archer.Agent.remainingDistance}, velocity={archer.Agent.velocity}, path={archer.Agent.pathStatus}.");
                yield return null;
            }
            Assert.That(guard.IsAlive,Is.False,"One archer must be able to defeat an isolated melee garrison.");
            Assert.That(archer.Health,Is.EqualTo(health));
            Assert.That(tower.ShotsFired,Is.EqualTo(shots));
            Time.timeScale=1;
            archer.MoveTo(town.ClaimPoint,false,false);
            deadline=Time.realtimeSinceStartup+5;
            while(town.State.Owner!=0&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(town.State.Owner,Is.Zero);
            Assert.That(town.Defender,Is.SameAs(archer));
            Assert.That(tower.Team,Is.Zero);
            Assert.That(archer.Health,Is.EqualTo(health),"The same surviving archer must take the circle.");
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
            var enemy=BattleTestScenario.Mobile(battle,1,UnitKind.Footman,tower.transform.position+Vector3.forward*7);
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
            Application.targetFrameRate=previousFrameRate;QualitySettings.vSyncCount=previousVSync;
            Time.timeScale=1;BattleSession.LayoutForNewMatch=BattleSession.StartLayout.RandomCities; SceneManager.SetActiveScene(previous); yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
