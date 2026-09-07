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
