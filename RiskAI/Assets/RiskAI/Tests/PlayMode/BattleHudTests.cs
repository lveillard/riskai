using System.Collections;
using System.Linq;
using RiskAI.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace RiskAI.Tests
{
    public sealed class BattleHudTests
    {
        Scene scene, previous;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        BattleSession.VictoryMode previousMode;
        int previousPlayers, previousSeed;
        float previousTimeScale;
        BattleSession battle;
        BattleHud hud;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous=SceneManager.GetActiveScene();previousMap=BattleSession.MapForNewMatch;previousLayout=BattleSession.LayoutForNewMatch;
            previousMode=BattleSession.ModeForNewMatch;previousPlayers=BattleSession.PlayerCountForNewMatch;previousSeed=BattleSession.SeedForNewMatch;previousTimeScale=Time.timeScale;
            BattleSession.MapForNewMatch=ScenarioMap.Classic;BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;BattleSession.ModeForNewMatch=BattleSession.VictoryMode.Conquest;
            BattleSession.PlayerCountForNewMatch=2;BattleSession.SeedForNewMatch=19031;
            scene=SceneManager.CreateScene("Battle HUD gold refresh");SceneManager.SetActiveScene(scene);
            new GameObject("Battle HUD bootstrap").AddComponent<RiskBootstrap>();battle=BattleSession.Current;battle.AiEnabled=false;
            Object.FindFirstObjectByType<RtsController>().enabled=false;hud=Object.FindFirstObjectByType<BattleHud>();
            yield return null;
        }

        [UnityTest]
        public IEnumerator EmptySelectionReleasesWorldAndCitySelectionClearsImmediately()
        {
            var controller=Object.FindFirstObjectByType<RtsController>();
            var cities=battle.Towns.Where(t=>t.State.Owner==0&&!t.IsPort).Take(2).ToArray();
            controller.SelectTown(cities[0]);yield return null;yield return null;
            Assert.That(cities[0].SelectionRing.enabled,Is.True);
            controller.SelectTown(cities[1]);
            Assert.That(cities[0].SelectionRing.enabled,Is.False,"Deselect presentation must not wait for Settlement.Update.");
            Assert.That(cities[1].SelectionRing.enabled,Is.True);
            var root=hud.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root.Q<VisualElement>("HUD footer"),Is.Not.Null);
            controller.Clear();yield return null;yield return null;
            Assert.That(root.Q<VisualElement>("HUD footer"),Is.Null);
            Assert.That(UiViewport.WorldRect.yMin,Is.EqualTo(UiViewport.SafeRect.yMin));
            Assert.That(RtsUiInput.BlocksWorld(new Vector2(UiViewport.WorldRect.center.x,UiViewport.WorldRect.yMin+20)),Is.False);
        }

        [UnityTest]
        public IEnumerator ResourceButtonsOpenIncomeAndRankingAndExposeRecruitmentLimit()
        {
            yield return null;
            var root=hud.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root.Q<Label>("HUD unit population").text,Does.Contain("/"+BattleRules.PopulationLimit));
            Assert.That(root.Q<Label>("HUD guard population"),Is.Null);
            var gold=root.Q<Button>("HUD gold button");
            using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=gold;gold.SendEvent(evt);}
            Assert.That(root.Query<Label>().ToList().Any(l=>l.text=="DESGLOSE DEL ORO"),Is.True);
            var cities=root.Q<Button>("HUD cities button");
            using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=cities;cities.SendEvent(evt);}
            Assert.That(root.Query<VisualElement>().ToList().Count(e=>e.name!=null&&e.name.StartsWith("HUD ranking row ")),Is.EqualTo(battle.PlayerCount));
        }

        [UnityTest]
        public IEnumerator OwnProductionFloatsWithoutSelectionAndNeverExposesEnemyQueues()
        {
            var controller=Object.FindFirstObjectByType<RtsController>();
            var own=battle.Towns.First(t=>t.State.Owner==0&&!t.IsPort);
            var enemy=battle.Towns.First(t=>t.State.Owner==1&&!t.IsPort);
            Assert.That(own.Recruit(UnitKind.Archer,0),Is.Null);
            Assert.That(enemy.Recruit(UnitKind.Archer,1),Is.Null);
            battle.TogglePause();controller.Clear();controller.Focus(own.transform.position);
            yield return null;yield return null;
            var root=hud.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root.Q<VisualElement>("HUD footer"),Is.Null);
            Assert.That(root.Q<VisualElement>("HUD building queue "+own.GetInstanceID()),Is.Not.Null);
            Assert.That(root.Q<VisualElement>("HUD building queue "+enemy.GetInstanceID()),Is.Null);
            // Capturing a paused building must remove its queue without waiting for
            // the next economy or claim simulation tick to refund its orders.
            own.State.Owner=1;yield return null;
            Assert.That(root.Q<VisualElement>("HUD building queue "+own.GetInstanceID()),Is.Null);
        }

        [UnityTest]
        public IEnumerator ProductionFollowsSelectedBuildingOwnershipWithoutReselecting()
        {
            var controller=Object.FindFirstObjectByType<RtsController>();
            var town=battle.Towns.First(item=>item.State.Owner==0&&!item.IsPort);
            if(!battle.Paused)battle.TogglePause();
            controller.SelectTown(town);
            yield return null;yield return null;
            var root=hud.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root.Q<VisualElement>("HUD tabs"),Is.Null,"Production is shown directly without a tab change.");
            Assert.That(root.Q<Button>("Recruit Archer"),Is.Not.Null);
            town.State.Owner=1;
            yield return null;yield return null;
            Assert.That(root.Q<Button>("Recruit Archer"),Is.Null,"Enemy ownership must remove production even when the selected object is unchanged.");
            town.State.Owner=PlayerRules.NeutralOwner;
            yield return null;
            Assert.That(root.Q<Button>("Recruit Archer"),Is.Null);
            town.State.Owner=0;
            yield return null;
            Assert.That(root.Q<Button>("Recruit Archer"),Is.Not.Null,"Capturing the selected city restores its production.");

            var harbor=battle.Naval.Harbors.First(item=>item.Owner==0);
            controller.SelectHarbor(harbor);
            yield return null;
            Assert.That(root.Q<Button>("Build ship Galley"),Is.Not.Null);
            harbor.State.Owner=1;
            yield return null;
            Assert.That(root.Q<Button>("Build ship Galley"),Is.Null);
            Assert.That(root.Q<Button>("Recruit MarinePrivate"),Is.Null);
        }

        [UnityTest]
        public IEnumerator SelectedArmyExposesSixDirectVectorActionsAndLiveResourceIcons()
        {
            var controller=Object.FindFirstObjectByType<RtsController>();
            var town=battle.Towns.First(item=>item.State.Owner==0);
            var troops=BattleTestScenario.MobileArmy(battle,0,UnitKind.Archer,2,town.ClaimPoint);
            controller.SelectOnly(troops[0]);
            yield return null;yield return null;
            var root=hud.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root.Q<VisualElement>("HUD tabs"),Is.Null);
            var actions=root.Q<VisualElement>("HUD direct actions");Assert.That(actions,Is.Not.Null);
            Assert.That(actions.Query<Button>().ToList().Count,Is.EqualTo(6));
            Assert.That(actions.Query<RtsHudIcon>().ToList().Count,Is.EqualTo(6));
            var buttons=actions.Query<Button>().ToList();
            foreach(var button in buttons)
            {
                Assert.That(button.worldBound.y,Is.EqualTo(buttons[0].worldBound.y).Within(1),"All six commands must stay on one row.");
                Assert.That(button.worldBound.xMax,Is.LessThanOrEqualTo(actions.worldBound.xMax+1),"The final command must remain inside the action panel.");
            }
            var move=root.Q<Button>("HUD action Mover");Assert.That(move.tooltip,Is.EqualTo("Mover"));
            using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=move;move.SendEvent(evt);}
            Assert.That(controller.MoveCursor,Is.True,"The direct icon must call the existing move action.");
            Assert.That(root.Q<Image>("HUD unit portrait"),Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator GoldHeaderRefreshesAfterGrantWhilePausedBeforeNextSimulationTick()
        {
            var document=hud.GetComponent<UIDocument>();Assert.That(document,Is.Not.Null);
            var gold=document.rootVisualElement.Q<Label>("HUD gold");Assert.That(gold,Is.Not.Null,"The retained header needs a stable gold label for model-driven refresh verification.");
            Assert.That(document.rootVisualElement.Q<RtsGoldIcon>("Gold coin icon"),Is.Not.Null);
            long tickBefore=battle.Clock.TickCount;
            int granted=997;battle.Economy.Grant(0,granted-battle.Economy.Gold[0]);
            if(!battle.Paused)battle.TogglePause();
            yield return new WaitForSecondsRealtime(.12f);
            yield return null;

            Assert.That(battle.Clock.TickCount,Is.EqualTo(tickBefore),"The assertion must cover a model change before any next simulation tick.");
            Assert.That(gold.text,Does.StartWith(granted+" ORO"),"The retained header must read economy gold even while its simulation snapshot is paused.");
        }

        [UnityTest]
        public IEnumerator HelpSheetIsReplacedByActualVictoryResult()
        {
            var controller=Object.FindFirstObjectByType<RtsController>();
            controller.HelpVisible=true;
            yield return null;

            // Establish the conquest condition through the public settlement state, then
            // drive the normal BattleWorld rule pass. Opponent guards remain alive, so
            // this specifically covers the timed 60-percent victory rather than a
            // synthetic Winner assignment or elimination shortcut.
            foreach(var town in battle.Towns)town.State.Owner=0;
            int steps=0;
            while(battle.Winner<0&&steps++<1000)
                battle.Clock.Advance(SimClock.StepSeconds,false,battle.World.Tick);
            Assert.That(battle.Winner,Is.EqualTo(0),"Fixture must reach victory through BattleSession.TickRules.");

            yield return null;
            var labels=hud.GetComponent<UIDocument>().rootVisualElement.Query<Label>().ToList();
            Assert.That(labels.Any(label=>label.text=="VICTORIA"),Is.True,
                "A modal already open as help must rebuild as the result sheet when the normal victory rule fires.");
        }

        [UnityTest]
        public IEnumerator ArmyRosterShowsPortraitsAndLiveHealthAndSelectsOneUnit()
        {
            var controller=Object.FindFirstObjectByType<RtsController>();
            var home=battle.Towns.First(town=>town.State.Owner==0);
            var troops=BattleTestScenario.MobileArmy(battle,0,UnitKind.Archer,2,home.ClaimPoint);
            controller.SelectAll();battle.TogglePause();
            yield return null;yield return null;
            var root=hud.GetComponent<UIDocument>().rootVisualElement;
            var cards=root.Query<Button>(className:"riskai-selection-card").ToList();
            Assert.That(cards.Count,Is.EqualTo(2));
            Assert.That(cards.All(card=>card.Q<Image>()?.image),Is.True,"Each selected soldier must have its own portrait.");
            Assert.That(cards.All(card=>card.resolvedStyle.width>=44&&card.resolvedStyle.height>=44),Is.True);
            var target=troops[1];var card=root.Q<Button>("HUD selected actor "+target.EntityId);
            target.TakeDamage(50,1);
            yield return new WaitForSecondsRealtime(.15f);
            yield return null;
            var health=card.Q<VisualElement>("HUD selection health");
            Assert.That(health.parent.resolvedStyle.width,Is.GreaterThan(30),"The track must have actual layout width, not just a requested percentage.");
            Assert.That(health.resolvedStyle.width/health.parent.resolvedStyle.width,
                Is.EqualTo(target.Health/target.MaxHealth).Within(.02f));
            using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=card;card.SendEvent(evt);}
            Assert.That(controller.Selection,Is.EquivalentTo(new[]{target}));
            Assert.That(controller.Fleet,Is.Empty);
            yield return null;
            Assert.That(root.Q<Image>("HUD unit portrait"),Is.Not.Null,"Isolating a card restores full unit details.");
        }

        [UnityTest]
        public IEnumerator MixedRosterKeepsSoldiersAndShipsAndSelectsTheShip()
        {
            var controller=Object.FindFirstObjectByType<RtsController>();
            var home=battle.Towns.First(town=>town.State.Owner==0);
            BattleTestScenario.MobileArmy(battle,0,UnitKind.Archer,2,home.ClaimPoint);
            var naval=NavalWorld.Current;
            var ship=BattleTestScenario.Ship(naval,0,ShipKind.Transport,naval.Harbors.First().Berth);
            controller.SelectAll();controller.SelectShip(ship,true);battle.TogglePause();
            yield return null;yield return null;
            var root=hud.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root.Query<Button>(className:"riskai-selection-card").ToList().Count,Is.EqualTo(3));
            var card=root.Q<Button>("HUD selected actor "+ship.EntityId);
            Assert.That(card,Is.Not.Null,"A fleet selection must not hide selected land troops or the ship card.");
            var health=card.Q<VisualElement>("HUD selection health");
            Assert.That(health.resolvedStyle.width,Is.GreaterThan(30));
            Assert.That(health.resolvedStyle.width,Is.EqualTo(health.parent.resolvedStyle.width).Within(.1f));
            using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=card;card.SendEvent(evt);}
            Assert.That(controller.Fleet,Is.EquivalentTo(new[]{ship}));
            Assert.That(controller.Selection,Is.Empty);
        }

        [UnityTest]
        public IEnumerator OldRosterCardCannotSelectANewLifetimeOfTheSameView()
        {
            var controller=Object.FindFirstObjectByType<RtsController>();
            var home=battle.Towns.First(town=>town.State.Owner==0);
            var troops=BattleTestScenario.MobileArmy(battle,0,UnitKind.Archer,2,home.ClaimPoint);
            controller.SelectAll();battle.TogglePause();
            yield return null;yield return null;
            var root=hud.GetComponent<UIDocument>().rootVisualElement;
            var actor=troops[0];int oldId=actor.EntityId;
            var oldCard=root.Q<Button>("HUD selected actor "+oldId);
            // Use the real death, delayed pool return and spawn path while keeping
            // the old button attached until the next rendered HUD frame.
            Vector3 point=actor.transform.position;
            actor.TakeDamage(actor.MaxHealth+1,1);
            Assert.That(battle.FindTarget(oldId),Is.Null);
            battle.TogglePause();
            for(int i=0;i<40;i++)battle.Clock.Advance(SimClock.StepSeconds,false,battle.World.Tick);
            Assert.That(actor.gameObject.activeSelf,Is.False,"The dead actor must reach the pool before rent.");
            var replacement=battle.Spawn(0,UnitKind.Archer,point);
            battle.TogglePause();
            Assert.That(replacement,Is.SameAs(actor),"This fixture must actually reuse the old view.");
            Assert.That(actor.EntityId,Is.Not.EqualTo(oldId));
            using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=oldCard;oldCard.SendEvent(evt);}
            Assert.That(controller.Selection.Count,Is.EqualTo(2),"A stale card must not isolate the reused view.");
            yield return null;
            Assert.That(root.Q<Button>("HUD selected actor "+oldId),Is.Null);
            Assert.That(root.Q<Button>("HUD selected actor "+actor.EntityId),Is.Not.Null,"Actor identity must invalidate the retained roster.");
        }

        [UnityTest]
        public IEnumerator AppendingFriendlyLandSelectionReplacesEnemyInspection()
        {
            var controller=Object.FindFirstObjectByType<RtsController>();
            var home=battle.Towns.First(town=>town.State.Owner==0);
            var troops=BattleTestScenario.MobileArmy(battle,0,UnitKind.Archer,2,home.ClaimPoint);
            var enemy=battle.Units.First(unit=>unit.Team==1);
            battle.TogglePause();
            // Establish inspection without making this retained-HUD regression
            // depend on a particular camera or world picking fixture.
            typeof(RtsController).GetProperty(nameof(RtsController.InspectedTarget)).SetValue(controller,enemy);
            typeof(RtsController).GetMethod("SelectUnits",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)
                .Invoke(controller,new object[]{troops,true});
            Assert.That(controller.InspectedTarget,Is.Null);
            yield return null;yield return null;
            var root=hud.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root.Query<Button>(className:"riskai-selection-card").ToList().Count,Is.EqualTo(2));
            Assert.That(root.Q<Image>("HUD unit portrait"),Is.Null,"The enemy portrait must not wrap the friendly army roster.");
        }

        [UnityTest]
        public IEnumerator DifferentArmiesWithTheSameLegacyHashRebuildTheRoster()
        {
            var controller=Object.FindFirstObjectByType<RtsController>();
            var home=battle.Towns.First(town=>town.State.Owner==0);
            var troops=BattleTestScenario.MobileArmy(battle,0,UnitKind.Archer,34,home.ClaimPoint);
            controller.SelectAll();battle.TogglePause();
            yield return null;yield return null;
            var root=hud.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root.Query<Button>(className:"riskai-selection-card").ToList().Count,Is.EqualTo(34),"Scrolling must retain every selected actor.");
            var select=typeof(RtsController).GetMethod("SelectUnits",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            var first=new[]{troops[0],troops[33]};var second=new[]{troops[1],troops[2]};
            Assert.That(first[0].EntityId*31+first[1].EntityId,Is.EqualTo(second[0].EntityId*31+second[1].EntityId),"Fixture establishes the old hash collision using real allocated ids.");
            select.Invoke(controller,new object[]{first,false});yield return null;
            Assert.That(root.Q<Button>("HUD selected actor "+first[0].EntityId),Is.Not.Null);
            select.Invoke(controller,new object[]{second,false});yield return null;
            Assert.That(root.Q<Button>("HUD selected actor "+first[0].EntityId),Is.Null);
            Assert.That(root.Q<Button>("HUD selected actor "+second[0].EntityId),Is.Not.Null);
            Assert.That(root.Q<Button>("HUD selected actor "+second[1].EntityId),Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale=previousTimeScale;UiViewport.ResetHudHeights();BattleSession.MapForNewMatch=previousMap;BattleSession.LayoutForNewMatch=previousLayout;
            BattleSession.ModeForNewMatch=previousMode;BattleSession.PlayerCountForNewMatch=previousPlayers;BattleSession.SeedForNewMatch=previousSeed;
            SceneManager.SetActiveScene(previous);yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
