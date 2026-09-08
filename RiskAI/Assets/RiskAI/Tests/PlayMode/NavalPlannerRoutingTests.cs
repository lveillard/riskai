using System.Collections;
using System.Collections.Generic;
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
        static readonly FieldInfo PlannerTransport=typeof(NavalExpeditionCommander).GetField("transport",PrivateInstance);
        static readonly FieldInfo RecoveryCursor=typeof(NavalExpeditionCommander).GetField("recoveryHarborCursor",PrivateInstance);
        static readonly FieldInfo RecoveryPass=typeof(NavalExpeditionCommander).GetField("recoveryPass",PrivateInstance);
        static readonly MethodInfo NearestRecovery=typeof(NavalExpeditionCommander).GetMethod("NearestRecoveryHarbor",PrivateInstance);
        static readonly MethodInfo ChooseSource=typeof(NavalExpeditionCommander).GetMethod("TryChooseSourceAndTroops",PrivateInstance);
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

        static Harbor Dock(string name,int owner,Vector3 berth,Vector3 landing=default)
        {
            var harbor=new GameObject(name).AddComponent<Harbor>();
            HarborState.SetValue(harbor,new TownState(name,owner,-1,-1));
            HarborLandingCached.SetValue(harbor,true);HarborCachedLanding.SetValue(harbor,landing);HarborCachedBerth.SetValue(harbor,berth);
            return harbor;
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
