using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace RiskAI.Tests
{
    public sealed class FrontEndTests
    {
        [UnityTest]
        public IEnumerator SetupScreenDoesNotCreateBattlefieldOrSimulation()
        {
            var previous=SceneManager.GetActiveScene();var scene=SceneManager.CreateScene("Frontend isolation");
            SceneManager.SetActiveScene(scene);
            var menu=new GameObject("Standalone match setup").AddComponent<FrontEndController>();
            yield return null;
            Assert.That(menu,Is.Not.Null);
            var runtime=menu.GetComponent<RtsUiRuntime>();
            Assert.That(runtime,Is.Not.Null,"The standalone setup must use the retained shared UI runtime.");
            Assert.That(menu.GetComponent<UIDocument>(),Is.Not.Null);
            Assert.That(runtime.Root,Is.Not.Null);
            Assert.That(runtime.Theme,Is.Not.Null,"The retained UI must use the portable project theme.");
            Assert.That(runtime.Root.Q("Front end content"),Is.Not.Null);
            foreach(var root in scene.GetRootGameObjects())
            {
                Assert.That(root.GetComponentInChildren<BattleSession>(),Is.Null);
                Assert.That(root.GetComponentInChildren<RiskBootstrap>(),Is.Null);
                Assert.That(root.GetComponentInChildren<MeshCollider>(),Is.Null,"Setup cannot generate or bake the map before Start.");
            }
            SceneManager.SetActiveScene(previous);yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator MapDefaultsFollowCapacityUntilPlayerCountIsAdjusted()
        {
            var previous=SceneManager.GetActiveScene();var scene=SceneManager.CreateScene("Frontend player capacity");
            SceneManager.SetActiveScene(scene);
            var savedMap=BattleSession.MapForNewMatch;
            BattleSession.MapForNewMatch=ScenarioMap.Classic;
            var menu=new GameObject("Player capacity setup").AddComponent<FrontEndController>();
            yield return null;
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var count=typeof(FrontEndController).GetField("selectedPlayers",flags);
            var map=typeof(FrontEndController).GetMethod("SelectMap",flags);
            var adjust=typeof(FrontEndController).GetMethod("AdjustPlayers",flags);
            Assert.That(count.GetValue(menu),Is.EqualTo(11));
            map.Invoke(menu,new object[]{ScenarioMap.Riverlands});
            Assert.That(count.GetValue(menu),Is.EqualTo(14));
            map.Invoke(menu,new object[]{ScenarioMap.Europe});
            Assert.That(count.GetValue(menu),Is.EqualTo(16));
            adjust.Invoke(menu,new object[]{-1});
            Assert.That(count.GetValue(menu),Is.EqualTo(15));
            map.Invoke(menu,new object[]{ScenarioMap.Classic});
            Assert.That(count.GetValue(menu),Is.EqualTo(11),"Manual selection clamps to the smaller map.");
            map.Invoke(menu,new object[]{ScenarioMap.NewWorld});
            Assert.That(count.GetValue(menu),Is.EqualTo(11),"A larger map preserves the manually adjusted count.");
            adjust.Invoke(menu,new object[]{-20});
            Assert.That(count.GetValue(menu),Is.EqualTo(2));
            adjust.Invoke(menu,new object[]{30});
            Assert.That(count.GetValue(menu),Is.EqualTo(16));
            BattleSession.MapForNewMatch=savedMap;
            SceneManager.SetActiveScene(previous);yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator ReservedHudBandsExcludeWorldInputAtRuntime()
        {
            UiViewport.SetHudHeights(44,232);
            yield return null;
            var world=UiViewport.WorldRect;
            Assert.That(world.height,Is.GreaterThan(0));
            Assert.That(RtsUiInput.BlocksWorld(world.center),Is.False);
            Assert.That(RtsUiInput.BlocksWorld(new Vector2(world.center.x,world.yMin-1)),Is.True);
            Assert.That(RtsUiInput.BlocksWorld(new Vector2(world.center.x,world.yMax+1)),Is.True);
            Assert.That(UiViewport.BottomPixels,Is.GreaterThanOrEqualTo(UiViewport.SafeRect.yMin));
            Assert.That(UiViewport.TopPixels,Is.GreaterThanOrEqualTo(0));
            UiViewport.ResetHudHeights();
        }
    }
}
