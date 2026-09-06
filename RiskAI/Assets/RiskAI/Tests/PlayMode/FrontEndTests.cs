using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

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
            foreach(var root in scene.GetRootGameObjects())
            {
                Assert.That(root.GetComponentInChildren<BattleSession>(),Is.Null);
                Assert.That(root.GetComponentInChildren<RiskBootstrap>(),Is.Null);
                Assert.That(root.GetComponentInChildren<MeshCollider>(),Is.Null,"Setup cannot generate or bake the map before Start.");
            }
            SceneManager.SetActiveScene(previous);yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
