using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    /// <summary>
    /// Selection and control groups must own an actor identity, not a recycled view.
    /// Every fixture uses the real death, delayed pool return and rent path while the
    /// controller is unfocused or disabled, which is exactly when Update cannot purge.
    /// </summary>
    public sealed class RtsSelectionLifetimeTests
    {
        Scene scene, previous;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        BattleSession.VictoryMode previousMode;
        int previousPlayers, previousSeed;
        BattleSession battle;
        RtsController controller;
        Mouse mouse, previousMouse;
        Keyboard keyboard, previousKeyboard;
        InputSettings.BackgroundBehavior previousBackground;
        InputSettings.EditorInputBehaviorInPlayMode previousEditor;
        bool previousRunBackground;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousBackground=InputSystem.settings.backgroundBehavior;
            previousEditor=InputSystem.settings.editorInputBehaviorInPlayMode;
            previousRunBackground=Application.runInBackground;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Application.runInBackground=true;
            previous=SceneManager.GetActiveScene();previousMap=BattleSession.MapForNewMatch;previousLayout=BattleSession.LayoutForNewMatch;
            previousMode=BattleSession.ModeForNewMatch;previousPlayers=BattleSession.PlayerCountForNewMatch;previousSeed=BattleSession.SeedForNewMatch;
            BattleSession.MapForNewMatch=ScenarioMap.Classic;BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;
            BattleSession.ModeForNewMatch=BattleSession.VictoryMode.Conquest;BattleSession.PlayerCountForNewMatch=2;BattleSession.SeedForNewMatch=19031;
            scene=SceneManager.CreateScene("Rts selection lifetime");SceneManager.SetActiveScene(scene);
            new GameObject("Selection lifetime bootstrap").AddComponent<RiskBootstrap>();
            battle=BattleSession.Current;battle.AiEnabled=false;
            controller=Object.FindFirstObjectByType<RtsController>();controller.enabled=false;
            previousMouse=Mouse.current;previousKeyboard=Keyboard.current;
            mouse=InputSystem.AddDevice<Mouse>("Selection lifetime mouse fixture");
            keyboard=InputSystem.AddDevice<Keyboard>("Selection lifetime keyboard fixture");
            yield return null;
            SetFocus(true);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if(mouse!=null)InputSystem.RemoveDevice(mouse);
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            if(previousMouse!=null)previousMouse.MakeCurrent();
            if(previousKeyboard!=null)previousKeyboard.MakeCurrent();
            SceneManager.SetActiveScene(previous);yield return SceneManager.UnloadSceneAsync(scene);
            BattleSession.MapForNewMatch=previousMap;BattleSession.LayoutForNewMatch=previousLayout;BattleSession.ModeForNewMatch=previousMode;
            BattleSession.PlayerCountForNewMatch=previousPlayers;BattleSession.SeedForNewMatch=previousSeed;
            InputSystem.settings.backgroundBehavior=previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode=previousEditor;
            Application.runInBackground=previousRunBackground;
        }

        void SetFocus(bool focused) => controller.SendMessage("OnApplicationFocus",focused);

        /// <summary>One controller frame with the given keys held; the controller stays disabled otherwise.</summary>
        void Pump(params Key[] keys)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState { position=UiViewport.WorldRect.center });
            InputSystem.QueueStateEvent(keyboard,keys.Length==0?new KeyboardState():new KeyboardState(keys));
            InputSystem.Update();mouse.MakeCurrent();keyboard.MakeCurrent();
            controller.SendMessage("Update");
        }

        void SimulateSeconds(float seconds)
        {
            int steps=Mathf.CeilToInt(seconds/(float)SimClock.StepSeconds);
            for(int i=0;i<steps;i++)battle.Clock.Advance(SimClock.StepSeconds,false,battle.World.Tick);
        }

        /// <summary>Real kill, real delayed pool return and a real rent that reuses the same view.</summary>
        Soldier KillAndReuse(Soldier actor,out int deadId)
        {
            deadId=actor.EntityId;var kind=actor.Kind;var point=actor.transform.position;
            actor.TakeDamage(actor.MaxHealth+1,1);
            Assert.That(battle.FindTarget(deadId),Is.Null,"The kill must retire the simulation identity.");
            SimulateSeconds(2.5f);
            Assert.That(actor.gameObject.activeSelf,Is.False,"The dead actor must reach the pool before rent.");
            var reused=battle.Spawn(0,kind,point);
            Assert.That(reused,Is.SameAs(actor),"This fixture must actually reuse the pooled view, not a fresh object.");
            Assert.That(reused.EntityId,Is.Not.EqualTo(deadId),"A rent must produce a new simulation identity.");
            return reused;
        }

        [UnityTest]
        public IEnumerator PoolReusedActorIsNotCommandedThroughAStaleSelectionWhileUnfocused()
        {
            var home=battle.Towns.First(town=>town.State.Owner==0);
            var troops=BattleTestScenario.MobileArmy(battle,0,UnitKind.Archer,2,home.ClaimPoint);
            controller.SelectOnly(troops[0]);
            Assert.That(controller.Selection,Is.EquivalentTo(new[]{troops[0]}));

            // Focus is lost, so Update's own housekeeping is skipped for the whole interval.
            SetFocus(false);
            var reused=KillAndReuse(troops[0],out int deadId);

            long appliedBefore=battle.Commands.AppliedCount;
            controller.Hold();
            SimulateSeconds(.2f);
            Assert.That(controller.Selection,Is.Empty,"A pool-reused view must not stay in the selection of the dead actor.");
            Assert.That(battle.Commands.PendingCount,Is.Zero);
            Assert.That(battle.Commands.AppliedCount,Is.EqualTo(appliedBefore),"No order may reach the new actor rented into the old view.");
            Assert.That(reused.IsHolding,Is.False,"The reused actor must keep its own orders.");
            Assert.That(reused.Selected,Is.False);
            Assert.That(deadId,Is.Not.EqualTo(reused.EntityId));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SurvivingSelectionIsKeptAcrossFocusLossWhileTheReusedViewIsDropped()
        {
            var home=battle.Towns.First(town=>town.State.Owner==0);
            var troops=BattleTestScenario.MobileArmy(battle,0,UnitKind.Archer,3,home.ClaimPoint);
            controller.SelectAll();
            var survivor=troops[1];int survivorId=survivor.EntityId;
            Assert.That(controller.Selection,Has.Member(survivor));
            Assert.That(controller.Selection,Has.Member(troops[0]));

            SetFocus(false);
            var reused=KillAndReuse(troops[0],out _);
            Pump();
            Assert.That(controller.Selection,Has.No.Member(reused),"Update must release ownership before its unfocused early return.");

            SetFocus(true);
            Pump();
            Assert.That(controller.Selection,Has.Member(survivor),"An actor that survived the interval stays selected.");
            Assert.That(survivor.EntityId,Is.EqualTo(survivorId));
            Assert.That(survivor.Selected,Is.True);
            Assert.That(controller.Selection,Has.No.Member(reused));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PublicListEditsAfterPoolReuseDoNotRebindTheStaleActor()
        {
            var home=battle.Towns.First(town=>town.State.Owner==0);
            var troops=BattleTestScenario.MobileArmy(battle,0,UnitKind.Archer,3,home.ClaimPoint);
            controller.SelectOnly(troops[0]);
            Assert.That(controller.Selection,Is.EquivalentTo(new[]{troops[0]}));

            SetFocus(false);
            var reused=KillAndReuse(troops[0],out _);

            // Existing callers see Selection as a plain public list. Appending and
            // removing other live actors changes its count and order, which must not
            // re-take an identity for the actor already recorded as selected.
            controller.Selection.Add(troops[2]);
            controller.Selection.Add(troops[1]);
            controller.Selection.Remove(troops[2]);
            Assert.That(controller.Selection,Is.EquivalentTo(new[]{reused,troops[1]}),"This fixture must edit the public list around the stale entry.");

            long applied=battle.Commands.AppliedCount;
            controller.Hold();
            SimulateSeconds(.2f);
            Assert.That(controller.Selection,Is.EquivalentTo(new[]{troops[1]}),"List edits must not revive ownership of a pool-reused actor.");
            Assert.That(battle.Commands.AppliedCount,Is.EqualTo(applied+1),"Exactly the appended live actor may receive the order.");
            Assert.That(troops[1].IsHolding,Is.True);
            Assert.That(reused.IsHolding,Is.False,"The rented-back actor must never inherit the dead actor's orders.");
            Assert.That(troops[2].IsHolding,Is.False,"A removed actor keeps its own orders.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ControlGroupRecallSkipsAStoredActorThatThePoolReused()
        {
            var home=battle.Towns.First(town=>town.State.Owner==0);
            var troops=BattleTestScenario.MobileArmy(battle,0,UnitKind.Archer,2,home.ClaimPoint);
            controller.SelectAll();
            int stored=controller.Selection.Count;
            Assert.That(stored,Is.GreaterThanOrEqualTo(2));
            Pump(Key.LeftCtrl,Key.Digit1);Pump();
            var survivor=troops[1];
            controller.Clear();
            Assert.That(controller.Selection,Is.Empty);

            // The stored actor is no longer selected, so only the group can resurrect it.
            SetFocus(false);
            var reused=KillAndReuse(troops[0],out _);
            SetFocus(true);

            Pump(Key.Digit1);Pump();
            Assert.That(controller.Selection,Has.No.Member(reused),"A recalled group must not include a new actor rented into a stored view.");
            Assert.That(controller.Selection,Has.Member(survivor),"Surviving stored members are still recalled.");
            Assert.That(controller.Selection.Count,Is.EqualTo(stored-1));
            Assert.That(reused.Selected,Is.False);

            long applied=battle.Commands.AppliedCount;
            controller.Hold();
            SimulateSeconds(.2f);
            Assert.That(reused.IsHolding,Is.False,"Commands after a recall must reach only the recalled identities.");
            Assert.That(battle.Commands.AppliedCount,Is.GreaterThan(applied),"The surviving recalled member must still accept orders.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyedShipLeavesTheFleetBeforeFleetCommandsRun()
        {
            var naval=NavalWorld.Current;
            Assert.That(naval,Is.Not.Null,"This naval fixture needs the map's ocean world.");
            var harbor=naval.Harbors.First();
            var galley=BattleTestScenario.Ship(naval,0,ShipKind.Galley,harbor.Berth);
            var transport=BattleTestScenario.Ship(naval,0,ShipKind.Transport,harbor.Berth);
            controller.SelectShip(galley);controller.SelectShip(transport,true);
            Assert.That(controller.Fleet,Is.EquivalentTo(new[]{galley,transport}));

            // Ships are destroyed rather than pooled today, so the identity check must
            // still drop them while no focused Update frame can run.
            SetFocus(false);
            galley.TakeDamage(galley.MaxHealth+1,1);
            yield return null;

            controller.Stop();
            Assert.That(controller.Fleet,Is.EquivalentTo(new[]{transport}),"A destroyed ship cannot stay in the fleet of a disabled controller.");
            Assert.That(transport.Selected,Is.True);
        }

        [UnityTest]
        public IEnumerator ClearReleasesAnExternallyListedActorButNotAReplacedOne()
        {
            var home=battle.Towns.First(town=>town.State.Owner==0);
            var troops=BattleTestScenario.MobileArmy(battle,0,UnitKind.Archer,3,home.ClaimPoint);
            controller.SelectOnly(troops[1]);
            Assert.That(troops[1].Selected,Is.True);
            // Drop the entry straight from the public list and let a normal command
            // entry point rebuild the records: the actor keeps its ring while this
            // controller no longer holds an identity for it.
            controller.Selection.Remove(troops[1]);
            controller.Stop();
            Assert.That(controller.Selection,Is.Empty);
            Assert.That(troops[1].Selected,Is.True,"This fixture needs a still-ringed actor with no record.");

            controller.SelectOnly(troops[0]);
            SetFocus(false);
            var reused=KillAndReuse(troops[0],out _);
            controller.Selection.Add(troops[1]);
            Assert.That(controller.Selection,Is.EquivalentTo(new[]{reused,troops[1]}));

            controller.Clear();
            Assert.That(controller.Selection,Is.Empty);
            Assert.That(troops[1].Selected,Is.False,"Clear must release an actor it listed, even with no earlier record.");
            Assert.That(reused.Selected,Is.False,"A replaced actor is dropped, never re-owned by the old selection.");

            // Clearing must leave the replaced actor an ordinary selectable unit.
            SetFocus(true);
            controller.SelectOnly(reused);
            Assert.That(reused.Selected,Is.True);
            long applied=battle.Commands.AppliedCount;
            controller.Hold();
            SimulateSeconds(.2f);
            Assert.That(battle.Commands.AppliedCount,Is.EqualTo(applied+1));
            Assert.That(reused.IsHolding,Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FriendlyAppendToggleTracksIdentityAcrossPoolReuse()
        {
            var home=battle.Towns.First(town=>town.State.Owner==0);
            var troops=BattleTestScenario.MobileArmy(battle,0,UnitKind.Archer,3,home.ClaimPoint);
            SelectPrimary(troops[0],false);
            SelectPrimary(troops[1],true);
            Assert.That(controller.Selection,Is.EquivalentTo(new[]{troops[0],troops[1]}));
            SelectPrimary(troops[1],true);
            Assert.That(controller.Selection,Is.EquivalentTo(new[]{troops[0]}),"Appending an already selected friendly toggles it off.");
            Assert.That(troops[1].Selected,Is.False);
            Assert.That(troops[0].Selected,Is.True);

            SetFocus(false);
            var reused=KillAndReuse(troops[0],out _);
            SetFocus(true);
            SelectPrimary(reused,true);
            Assert.That(controller.Selection,Is.EquivalentTo(new[]{reused}),"A stale entry must not turn an append into a deselection.");
            Assert.That(reused.Selected,Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NavalGroupRecallAndAppendToggleFollowShipIdentity()
        {
            var naval=NavalWorld.Current;
            Assert.That(naval,Is.Not.Null,"This naval fixture needs the map's ocean world.");
            var harbor=naval.Harbors.First();
            var galley=BattleTestScenario.Ship(naval,0,ShipKind.Galley,harbor.Berth);
            var transport=BattleTestScenario.Ship(naval,0,ShipKind.Transport,harbor.Berth);
            controller.SelectShip(galley);controller.SelectShip(transport,true);
            Pump(Key.LeftCtrl,Key.Digit1);Pump();

            controller.SelectShip(transport,true);
            Assert.That(controller.Fleet,Is.EquivalentTo(new[]{galley}),"Appending an already selected ship toggles it off.");
            Assert.That(transport.Selected,Is.False);
            controller.SelectShip(transport,true);
            Assert.That(controller.Fleet,Is.EquivalentTo(new[]{galley,transport}));

            controller.Clear();
            galley.TakeDamage(galley.MaxHealth+1,1);
            yield return null;

            Pump(Key.Digit1);Pump();
            Assert.That(controller.Fleet,Is.EquivalentTo(new[]{transport}),"Recall must skip a ship destroyed since the group was stored.");
            Assert.That(transport.Selected,Is.True);
        }

        [UnityTest]
        public IEnumerator PendingBoardingDropsAReusedBoarderInsteadOfLandingOrLoadingIt()
        {
            var naval=NavalWorld.Current;
            Assert.That(naval,Is.Not.Null,"This naval fixture needs the map's ocean world.");
            var harbor=naval.Harbors.First(candidate=>candidate&&candidate.TryTransportLanding(out _,out _));
            var home=battle.Towns.First(town=>town.State.Owner==0);
            var transport=BattleTestScenario.Ship(naval,0,ShipKind.Transport,harbor.Berth);
            // The boarder starts inland, out of loading range, so the controller
            // plans a real shore rendezvous instead of embarking immediately.
            var boarder=BattleTestScenario.Mobile(battle,0,UnitKind.Footman,home.ClaimPoint);
            controller.SelectOnly(boarder);controller.SelectShip(transport,true);
            long submitted=battle.Commands.PendingCount+battle.Commands.AppliedCount;
            BeginBoarding(transport);
            Assert.That(battle.Commands.PendingCount+battle.Commands.AppliedCount,Is.GreaterThan(submitted),
                "This fixture must actually queue a landing order for a pending boarder.");
            SimulateSeconds(.3f);

            // The controller is disabled and unfocused for the whole interval, so
            // only the boarding pass itself can protect the queue.
            SetFocus(false);
            var replacement=KillAndReuse(boarder,out int deadId);
            Assert.That(battle.FindTarget(deadId),Is.Null,"The dead boarder's global id must not resolve after the rent.");
            Assert.That(battle.FindTarget(replacement.EntityId),Is.SameAs(replacement));

            long applied=battle.Commands.AppliedCount,rejected=battle.Commands.RejectedCount;
            // Drive the real pending boarding and stall-recovery cadence.
            for(int i=0;i<6;i++){Pump();SimulateSeconds(.3f);}
            Pump();

            Assert.That(transport.CargoCount,Is.Zero,"A rented-back view must not become the dead boarder's cargo.");
            Assert.That(replacement.gameObject.activeSelf,Is.True);
            Assert.That(replacement.IsIdle,Is.True,"The replacement must never receive the old landing or recovery order.");
            Assert.That(battle.Commands.AppliedCount,Is.EqualTo(applied));
            Assert.That(battle.Commands.RejectedCount,Is.EqualTo(rejected));
            Assert.That(battle.Messages.Any(message=>message.StartsWith("Embarque terminado")),Is.True,
                "The queue must resolve through the identity check, not stall on a replaced boarder.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator EmbarkedSelectedActorIsReleasedFromTheSelection()
        {
            var naval=NavalWorld.Current;
            Assert.That(naval,Is.Not.Null,"This naval fixture needs the map's ocean world.");
            var harbor=naval.Harbors.First(candidate=>candidate);
            var transport=BattleTestScenario.Ship(naval,0,ShipKind.Transport,harbor.Berth);
            var passenger=BattleTestScenario.Mobile(battle,0,UnitKind.Footman,harbor.Landing);
            controller.SelectOnly(passenger);
            Assert.That(passenger.Selected,Is.True);

            // The real embark path deactivates the actor, so the selection must
            // release it rather than keep an entry it can no longer command.
            Assert.That(transport.TryEmbark(passenger),Is.True,transport.LastActionError);
            Assert.That(transport.CargoCount,Is.EqualTo(1));
            Pump();
            Assert.That(controller.Selection,Has.No.Member(passenger),"An embarked actor is released from the selection.");
            Assert.That(passenger.Selected,Is.False,"Nothing may keep a selection ring on a boarded actor.");
            yield return null;
        }

        /// <summary>The controller's own boarding entry, as used by the right-click transport action.</summary>
        void BeginBoarding(Ship transport) =>
            typeof(RtsController).GetMethod("BeginBoarding",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(Ship)},null)
                .Invoke(controller,new object[]{transport});

        /// <summary>The real friendly click path, without depending on a picking fixture.</summary>
        void SelectPrimary(Soldier unit,bool append) =>
            typeof(RtsController).GetMethod("SelectPrimaryUnit",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(controller,new object[]{unit,append,false});
    }
}
