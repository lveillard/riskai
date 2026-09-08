using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class NavalPlannerRoutingTests
    {
        static readonly BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly BindingFlags PrivateStatic=BindingFlags.Static|BindingFlags.NonPublic;
        static readonly FieldInfo HarborState=typeof(Harbor).GetField("state",PrivateInstance);
        static readonly FieldInfo HarborLandingCached=typeof(Harbor).GetField("transportLandingCached",PrivateInstance);
        static readonly FieldInfo HarborCachedLanding=typeof(Harbor).GetField("cachedTransportLanding",PrivateInstance);
        static readonly FieldInfo HarborCachedBerth=typeof(Harbor).GetField("cachedTransportBerth",PrivateInstance);
        static readonly FieldInfo HarborCanLaunch=typeof(Harbor).GetField("canLaunch",PrivateInstance);
        static readonly FieldInfo HarborLanding=typeof(Harbor).GetField("<Landing>k__BackingField",PrivateInstance);
        static readonly FieldInfo PlannerTransport=typeof(NavalExpeditionCommander).GetField("transport",PrivateInstance);
        static readonly FieldInfo PlannerSource=typeof(NavalExpeditionCommander).GetField("source",PrivateInstance);
        static readonly FieldInfo PlannerTroops=typeof(NavalExpeditionCommander).GetField("troops",PrivateInstance);
        static readonly FieldInfo PlannerEmbarkOrdersIssued=typeof(NavalExpeditionCommander).GetField("embarkOrdersIssued",PrivateInstance);
        static readonly FieldInfo PlannerAttemptedSources=typeof(NavalExpeditionCommander).GetField("attemptedSources",PrivateInstance);
        static readonly FieldInfo PlannerTroopCursor=typeof(NavalExpeditionCommander).GetField("troopCursor",PrivateInstance);
        static readonly FieldInfo RecoveryCursor=typeof(NavalExpeditionCommander).GetField("recoveryHarborCursor",PrivateInstance);
        static readonly FieldInfo RecoveryPass=typeof(NavalExpeditionCommander).GetField("recoveryPass",PrivateInstance);
        static readonly MethodInfo NearestRecovery=typeof(NavalExpeditionCommander).GetMethod("NearestRecoveryHarbor",PrivateInstance);
        static readonly MethodInfo ChooseSource=typeof(NavalExpeditionCommander).GetMethod("TryChooseSourceAndTroops",PrivateInstance);
        static readonly MethodInfo ChooseTarget=typeof(NavalExpeditionCommander).GetMethod("TryChooseTarget",PrivateInstance);
        static readonly MethodInfo Plan=typeof(NavalExpeditionCommander).GetMethod("Plan",PrivateInstance);
        static readonly FieldInfo PlannerSourceLanding=typeof(NavalExpeditionCommander).GetField("sourceLanding",PrivateInstance);
        static readonly FieldInfo PlannerSourceBerth=typeof(NavalExpeditionCommander).GetField("sourceTransportBerth",PrivateInstance);
        static readonly FieldInfo Imported=typeof(MapLayout).GetField("<Imported>k__BackingField",PrivateStatic);

        Scene previous, scene;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        int previousPlayers, previousSeed;
        BattleSession battle;
        NavalWorld naval;
        object previousImported;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous=SceneManager.GetActiveScene();previousMap=BattleSession.MapForNewMatch;previousLayout=BattleSession.LayoutForNewMatch;
            previousPlayers=BattleSession.PlayerCountForNewMatch;previousSeed=BattleSession.SeedForNewMatch;
            BattleSession.MapForNewMatch=ScenarioMap.Classic;BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;
            BattleSession.PlayerCountForNewMatch=2;BattleSession.SeedForNewMatch=77131;
            scene=SceneManager.CreateScene("Naval planner routing");SceneManager.SetActiveScene(scene);
            new GameObject("Naval planner routing bootstrap").AddComponent<RiskBootstrap>();
            battle=BattleSession.Current;battle.AiEnabled=false;naval=NavalWorld.Current;
            Object.FindFirstObjectByType<RtsController>().enabled=false;
            previousImported=Imported.GetValue(null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RecoveryCursorReachesNeutralFallbackAfterSevenUnreachableOwnedDocks()
        {
            InstallSplitSea();
            Vector3 stranded=new Vector3(-20,-.24f,0),unreachable=new Vector3(20,-.24f,0),fallbackPoint=new Vector3(-12,-.24f,14);
            Assert.That(SeaNavigation.TryBuildPath(stranded,unreachable,out _),Is.False,"The synthetic sea barrier must create a real disconnected route.");
            Assert.That(SeaNavigation.TryBuildPath(stranded,fallbackPoint,out _),Is.True);

            naval.Harbors.Clear();
            for(int i=0;i<7;i++)naval.Harbors.Add(Dock("blocked "+i,1,unreachable+Vector3.forward*i*2f));
            var fallback=Dock("neutral fallback",-1,fallbackPoint);naval.Harbors.Add(fallback);
            var planner=new NavalExpeditionCommander(naval,1);

            Assert.That(NearestRecovery.Invoke(planner,new object[]{stranded}),Is.Null,"The six-route decision budget must stop before the seventh owned dock.");
            Assert.That(RecoveryCursor.GetValue(planner),Is.EqualTo(6),"The next decision must resume at the seventh candidate rather than restart at dock zero.");
            Assert.That(RecoveryPass.GetValue(planner),Is.EqualTo(0));
            Assert.That(NearestRecovery.Invoke(planner,new object[]{stranded}),Is.SameAs(fallback),"The persisted cursor must continue with dock seven and then reach the connected neutral fallback.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExistingTransportChoosesOnlySourceInItsSeaComponent()
        {
            var original=naval.Harbors[0];
            Assert.That(NavMesh.SamplePosition(original.Landing,out var land,3f,NavMesh.AllAreas),Is.True,"The source troops require the live Classic NavMesh.");
            InstallSplitSea();
            Vector3 transportPoint=new Vector3(-20,-.24f,0),isolatedBerth=new Vector3(20,-.24f,0),reachableBerth=new Vector3(-12,-.24f,14);
            Assert.That(SeaNavigation.AreConnected(transportPoint,isolatedBerth),Is.False);
            Assert.That(SeaNavigation.AreConnected(transportPoint,reachableBerth),Is.True);

            naval.Harbors.Clear();
            var isolated=Dock("isolated source",1,isolatedBerth,land.position);
            var reachable=Dock("reachable source",1,reachableBerth,land.position);
            naval.Harbors.Add(isolated);naval.Harbors.Add(reachable);
            var transport=naval.Spawn(1,ShipKind.Transport,transportPoint);
            Assert.That(transport,Is.Not.Null);
            BattleTestScenario.MobileArmy(battle,1,UnitKind.Footman,2,land.position);
            var planner=new NavalExpeditionCommander(naval,1);PlannerTransport.SetValue(planner,transport);
            var arguments=new object[]{null};
            Assert.That((bool)ChooseSource.Invoke(planner,arguments),Is.True);
            Assert.That(arguments[0],Is.SameAs(reachable),"A transport in the left sea component must not gather at the earlier isolated dock.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TargetKeepsItsReachableHarborInsteadOfReplacingItWithNearestDock()
        {
            var mainland=naval.Harbors.First(h=>h.Owner==1&&!h.IsIsland);
            var town=battle.Towns.First(t=>MapLayout.IslandDistance(t.ClaimPoint.x,t.ClaimPoint.z,0)>=0&&t.State.Owner!=1);
            Assert.That(NavMesh.SamplePosition(mainland.Landing,out var sourceLanding,3f,NavMesh.AllAreas),Is.True);
            Assert.That(NavMesh.SamplePosition(town.ClaimPoint,out var nearTown,3f,NavMesh.AllAreas),Is.True);
            InstallSplitSea();
            Assert.That(NavMesh.SamplePosition(nearTown.position+Vector3.right*2f,out var secondLanding,4f,NavMesh.AllAreas),Is.True);
            Vector3 sourceBerth=new Vector3(-20,-.24f,0),blockedBerth=new Vector3(20,-.24f,0),reachableBerth=new Vector3(-12,-.24f,14);
            Assert.That(SeaNavigation.TryBuildPath(sourceBerth,blockedBerth,out _),Is.False);
            Assert.That(SeaNavigation.TryBuildPath(sourceBerth,reachableBerth,out _),Is.True);

            naval.Harbors.Clear();
            var source=Dock("source",1,sourceBerth,sourceLanding.position);
            var blocked=Dock("nearest blocked dock",-1,blockedBerth,nearTown.position);
            var reachable=Dock("second reachable dock",-1,reachableBerth,secondLanding.position);
            EnableCandidateDock(blocked,nearTown.position);
            EnableCandidateDock(reachable,secondLanding.position);
            naval.Harbors.Add(source);naval.Harbors.Add(blocked);naval.Harbors.Add(reachable);

            var planner=new NavalExpeditionCommander(naval,1);
            PlannerSourceLanding.SetValue(planner,sourceLanding.position);
            PlannerSourceBerth.SetValue(planner,sourceBerth);
            var arguments=new object[]{source,null,null};
            Assert.That((bool)ChooseTarget.Invoke(planner,arguments),Is.True);
            Assert.That(arguments[1],Is.SameAs(town));
            Assert.That(arguments[2],Is.SameAs(reachable),"A reachable town/harbor pair must survive when a nearer dock is in another sea component.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator FailedCheapestSourceYieldsToTheNextSourceWithALegalRoute()
        {
            var mainland=naval.Harbors.First(h=>h.Owner==1&&!h.IsIsland);
            var island=battle.Towns.First(t=>MapLayout.IslandDistance(t.ClaimPoint.x,t.ClaimPoint.z,0)>=0&&t.State.Owner!=1);
            Assert.That(NavMesh.SamplePosition(mainland.Landing,out var mainlandLanding,3f,NavMesh.AllAreas),Is.True);
            Assert.That(NavMesh.SamplePosition(island.ClaimPoint,out var islandLanding,3f,NavMesh.AllAreas),Is.True);
            BattleTestScenario.MobileArmy(battle,1,UnitKind.Footman,2,mainlandLanding.position);
            InstallSplitSea();
            Vector3 cheapBerth=new Vector3(20,-.24f,0),viableBerth=new Vector3(-20,-.24f,0),targetBerth=new Vector3(-12,-.24f,14);
            Assert.That(SeaNavigation.TryBuildPath(cheapBerth,targetBerth,out _),Is.False);
            Assert.That(SeaNavigation.TryBuildPath(viableBerth,targetBerth,out _),Is.True);

            naval.Harbors.Clear();
            var cheap=Dock("cheap blocked source",1,cheapBerth,mainlandLanding.position);
            var viable=Dock("second viable source",1,viableBerth,mainlandLanding.position);
            var targetDock=Dock("island target",-1,targetBerth,islandLanding.position);
            EnableCandidateDock(targetDock,islandLanding.position);
            naval.Harbors.Add(cheap);naval.Harbors.Add(viable);naval.Harbors.Add(targetDock);
            battle.Economy.Gold[1]=50;
            while(battle.BattleTime<=battle.AiFirstNavalOffensiveTime)battle.Clock.Advance(1f,false,_=>{});

            var planner=new NavalExpeditionCommander(naval,1);
            Plan.Invoke(planner,null);
            var attempted=(HashSet<Harbor>)PlannerAttemptedSources.GetValue(planner);
            Assert.That(attempted.Contains(cheap),Is.True,"The cheapest source must be remembered only after its destination frontier fails.");
            Assert.That(attempted.Contains(viable),Is.False,"An unselected viable source must remain available for the next decision.");

            PlannerTroopCursor.SetValue(planner,0);
            var sourceArguments=new object[]{null};
            Assert.That((bool)ChooseSource.Invoke(planner,sourceArguments),Is.True);
            Assert.That(sourceArguments[0],Is.SameAs(viable));
            var targetArguments=new object[]{viable,null,null};
            Assert.That((bool)ChooseTarget.Invoke(planner,targetArguments),Is.True);
            Assert.That(targetArguments[1],Is.SameAs(island));
            Assert.That(targetArguments[2],Is.SameAs(targetDock));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PartiallyBoardedWaveAttacksOnlyTheActualCargo()
        {
            var home=naval.Harbors.First(h=>h.Owner==1&&h.CanLaunch);
            Assert.That(home.TryTransportLanding(out var landing,out var berth),Is.True);
            var transport=BattleTestScenario.Ship(naval,1,ShipKind.Transport,berth);
            var near=BattleTestScenario.MobileArmy(battle,1,UnitKind.Footman,2,landing);
            var inland=landing-berth;inland.y=0;if(inland.sqrMagnitude<.01f)inland=Vector3.forward;else inland.Normalize();
            Assert.That(NavMesh.SamplePosition(landing+inland*14f,out var far,8f,NavMesh.AllAreas),Is.True);
            Assert.That(NavMesh.SamplePosition(far.position+Vector3.right,out var farSecond,3f,NavMesh.AllAreas),Is.True);
            var squad=new[]{near[0],near[1],BattleTestScenario.Mobile(battle,1,UnitKind.Footman,far.position),BattleTestScenario.Mobile(battle,1,UnitKind.Footman,farSecond.position)};
            Assert.That(transport.TryEmbark(squad[0]),Is.True);
            Assert.That(transport.TryEmbark(squad[1]),Is.True);
            Assert.That(transport.CargoCount,Is.EqualTo(2));
            Assert.That(squad[2].TryMoveTo(landing,false,false),Is.True);
            Assert.That(squad[3].TryMoveTo(landing,false,false),Is.True);
            Assert.That(squad[2].IsIdle,Is.False);
            Assert.That(squad[3].IsIdle,Is.False);

            var planner=new NavalExpeditionCommander(naval,1);
            PlannerTransport.SetValue(planner,transport);PlannerSource.SetValue(planner,home);
            var planned=(List<Soldier>)PlannerTroops.GetValue(planner);planned.AddRange(squad);
            PlannerEmbarkOrdersIssued.SetValue(planner,true);
            typeof(NavalExpeditionCommander).GetMethod("Gather",PrivateInstance).Invoke(planner,null);

            Assert.That(planned,Is.EquivalentTo(transport.Cargo),"The sailing wave must be replaced by the units actually aboard.");
            Assert.That(squad[2].IsIdle,Is.True,"A non-boarded candidate must stop at its own shore instead of inheriting the overseas attack.");
            Assert.That(squad[3].IsIdle,Is.True);
            yield return null;
        }

        static Harbor Dock(string name,int owner,Vector3 berth,Vector3 landing=default)
        {
            var harbor=new GameObject(name).AddComponent<Harbor>();
            HarborState.SetValue(harbor,new TownState(name,owner,-1,-1));
            HarborLandingCached.SetValue(harbor,true);HarborCachedLanding.SetValue(harbor,landing);HarborCachedBerth.SetValue(harbor,berth);
            return harbor;
        }

        static void EnableCandidateDock(Harbor harbor,Vector3 landing)
        {
            HarborCanLaunch.SetValue(harbor,true);HarborLanding.SetValue(harbor,landing);
        }

        static void InstallSplitSea()
        {
            const int size=81;const float cell=1f;const float origin=-40f;int count=size*size;
            var map=new ImportedMapData
            {
                mapId="naval-planner-split-sea",width=size,height=size,originX=origin,originZ=origin,cellSize=cell,
                heightSamples=new float[count],waterSamples=new float[count],landSamples=new int[count],tileSamples=new int[count],
                cities=System.Array.Empty<ImportedMapData.City>(),countries=new[]{new ImportedMapData.Country{name="test",count=1}}
            };
            for(int i=0;i<count;i++){map.heightSamples[i]=-1f;map.waterSamples[i]=-.24f;}
            // A full land wall creates two actual SeaNavigation components; no
            // mocked path result is involved in either planner assertion.
            for(int z=0;z<size;z++)for(int x=38;x<=42;x++)map.heightSamples[z*size+x]=1f;
            Imported.SetValue(null,map);SeaNavigation.Prepare();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Imported.SetValue(null,previousImported);MapLayout.Configure(previousMap);
            BattleSession.MapForNewMatch=previousMap;BattleSession.LayoutForNewMatch=previousLayout;
            BattleSession.PlayerCountForNewMatch=previousPlayers;BattleSession.SeedForNewMatch=previousSeed;
            SceneManager.SetActiveScene(previous);if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
