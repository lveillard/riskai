using System.Collections;
using System.Linq;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace RiskAI.Tests
{
    public sealed class PlayerBuildingCommandsTests
    {
        Scene previous,scene;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        int previousPlayers;
        BattleSession battle;
        PlayerBuildingCommands commands;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousMap=BattleSession.MapForNewMatch;previousLayout=BattleSession.LayoutForNewMatch;previousPlayers=BattleSession.PlayerCountForNewMatch;
            BattleSession.MapForNewMatch=ScenarioMap.Europe;BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;BattleSession.PlayerCountForNewMatch=2;
            previous=SceneManager.GetActiveScene();scene=SceneManager.CreateScene("Building command boundary");SceneManager.SetActiveScene(scene);
            new GameObject("Building command bootstrap").AddComponent<RiskBootstrap>();
            battle=BattleSession.Current;battle.AiEnabled=false;commands=new PlayerBuildingCommands(battle);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CatalogRoutesImportedPortMarinesToTheLinkedTownQueue()
        {
            var port=NavalWorld.Current.Harbors.First(item=>item.IsImportedPort);
            var town=port.LinkedTown;town.State.Owner=0;battle.Economy.Gold[0]=100;
            Assert.That(port.BuildingId.Kind,Is.EqualTo(BuildingKind.Harbor));
            Assert.That(port.BuildingId.LocalId,Is.EqualTo("imported/"+town.State.Id));
            Assert.That(commands.Execute(0,PlayerBuildingIntent.Recruit(port.BuildingId,UnitKind.MarinePrivate)),Is.Null);
            Assert.That(town.QueueCount,Is.EqualTo(1));
            Assert.That(port.LandQueueCount,Is.EqualTo(town.QueueCount),"An imported dock is an alias of its source town queue, not a second land queue.");
            Assert.That(commands.Execute(0,PlayerBuildingIntent.Recruit(town.BuildingId,UnitKind.MarinePrivate)),Is.Not.Null);
            Assert.That(commands.Execute(0,PlayerBuildingIntent.Recruit(port.BuildingId,UnitKind.Footman)),Is.Not.Null);
            Assert.That(town.Recruit(UnitKind.MarinePrivate),Is.Not.Null,"A city API cannot bypass the harbor Marine catalog.");
            int gold=battle.Economy.Gold[0];
            Assert.That(town.Recruit(UnitKind.Footman),Is.Not.Null,"The linked city must not bypass the port catalog with a regular unit.");
            Assert.That(commands.Execute(0,PlayerBuildingIntent.Recruit(town.BuildingId,UnitKind.Footman)),Is.Not.Null);
            Assert.That(town.QueueCount,Is.EqualTo(1));
            Assert.That(battle.Economy.Gold[0],Is.EqualTo(gold));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ImportedPortHudUsesOnlyNavalCatalogAndShowsBothQueues()
        {
            var port=battle.Naval.Harbors.First(item=>item.IsImportedPort);
            port.LinkedTown.State.Owner=0;battle.Economy.Gold[0]=100;
            Assert.That(commands.Execute(0,PlayerBuildingIntent.Recruit(port.BuildingId,UnitKind.MarinePrivate)),Is.Null);
            Assert.That(commands.Execute(0,PlayerBuildingIntent.BuyShip(port.BuildingId,NavalUnitKind.Galley)),Is.Null);
            var controller=Object.FindFirstObjectByType<RtsController>();controller.enabled=false;
            controller.SelectTown(port.LinkedTown);
            battle.TogglePause();
            yield return null;
            var root=Object.FindFirstObjectByType<BattleHud>().GetComponent<UIDocument>().rootVisualElement;
            if(UiViewport.IsCompact){Activate(root.Q<Button>("HUD tab 2"));yield return null;}
            Assert.That(root.Q<Button>("Recruit Footman"),Is.Null,"A port's legacy SelectedTown alias must not display regular-city production.");
            foreach(var kind in ProductionCatalog.HarborUnits)Assert.That(root.Q<Button>("Recruit "+kind),Is.Not.Null);
            Assert.That(root.Q<Button>("Build ship Galley"),Is.Not.Null);
            Assert.That(root.Q<Button>("Build ship Transport"),Is.Not.Null);
            if(UiViewport.IsCompact){Activate(root.Q<Button>("HUD tab 0"));yield return null;}
            Assert.That(root.Query<Label>().ToList().Any(label=>label.text!=null&&label.text.Contains("tierra 1 / 5")&&label.text.Contains("barcos 1 / 5")),Is.True,
                "Selecting the house or tower must expose the same port land/naval queues.");
            var city=battle.Towns.First(item=>!item.IsPort);city.State.Owner=0;
            controller.SelectTown(city,true);yield return null;
            if(UiViewport.IsCompact){Activate(root.Q<Button>("HUD tab 2"));yield return null;}
            Assert.That(root.Q<Button>("Recruit Footman"),Is.Not.Null,"A mixed selection still exposes regular production for its real city.");
            Assert.That(root.Q<Button>("Recruit MarinePrivate"),Is.Not.Null);
        }

        static void Activate(Button button)
        {
            Assert.That(button,Is.Not.Null);
            using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=button;button.SendEvent(evt);}
        }

        [UnityTest]
        public IEnumerator CompletedRecruitThatCannotSpawnDequeuesAndRefundsWhilePopulationWaitsRemainSeparate()
        {
            var town=battle.Towns.First(item=>!item.IsPort&&item.Defender);town.State.Owner=0;
            int cost=BattleRules.Cost(UnitKind.Footman);battle.Economy.Gold[0]=cost;
            Assert.That(town.Recruit(UnitKind.Footman,0),Is.Null);
            Assert.That(town.QueueCount,Is.EqualTo(1));

            // Its defender prevents unrelated capture logic from altering this isolated queue case.
            town.transform.position=new Vector3(10000,0,10000);
            town.SimTick(BattleRules.TrainTime(UnitKind.Footman)+.1f);

            Assert.That(town.QueueCount,Is.Zero,"A completed order with no valid spawn must not remain paid forever.");
            Assert.That(battle.Economy.Gold[0],Is.EqualTo(cost),"A true spawn failure refunds exactly once; population-cap waits keep their order instead.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator LocalExecutorRejectsStaleOwnershipInvalidValuesAndSupportsCampRally()
        {
            var town=battle.Towns.First(item=>!item.IsPort);town.State.Owner=0;battle.Economy.Gold[0]=100;
            Vector3 original=town.Rally;
            Assert.That(commands.Execute(0,PlayerBuildingIntent.SetLandRally(town.BuildingId,float.NaN,0,0)),Is.Not.Null);
            Assert.That(town.Rally,Is.EqualTo(original));
            Assert.That(commands.Execute(0,PlayerBuildingIntent.CancelLand(town.BuildingId,-1)),Is.Not.Null);
            Assert.That(commands.Execute(0,PlayerBuildingIntent.Recruit(new BuildingId(BuildingKind.Settlement,"removed-town"),UnitKind.Footman)),Is.Not.Null);
            town.State.Owner=1;
            Assert.That(commands.Execute(0,PlayerBuildingIntent.Recruit(town.BuildingId,UnitKind.Footman)),Is.Not.Null);
            Assert.That(town.QueueCount,Is.Zero);

            var port=NavalWorld.Current.Harbors.First(item=>item.IsImportedPort);port.LinkedTown.State.Owner=0;
            Vector3 portRally=port.LandRally;
            Assert.That(commands.Execute(0,PlayerBuildingIntent.SetNavalRally(port.BuildingId,portRally.x,portRally.y,portRally.z)),Is.Not.Null,"Ports expose only land rally commands; naval rally is intentionally unsupported.");
            Assert.That(port.LandRally,Is.EqualTo(portRally));

            var camp=battle.Camps.First(item=>item!=null);
            foreach(var member in battle.Towns.Where(item=>item.State.Country==camp.Country))member.State.Owner=0;
            Assert.That(commands.Execute(0,PlayerBuildingIntent.SetLandRally(camp.BuildingId,town.Rally.x,town.Rally.y,town.Rally.z)),Is.Null);
            Assert.That(camp.HasRally,Is.True);
            Assert.That(commands.Execute(0,PlayerBuildingIntent.ClearRally(camp.BuildingId)),Is.Null);
            Assert.That(camp.HasRally,Is.False);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleSession.MapForNewMatch=previousMap;BattleSession.LayoutForNewMatch=previousLayout;BattleSession.PlayerCountForNewMatch=previousPlayers;
            MapLayout.Configure(previousMap);SceneManager.SetActiveScene(previous);yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
