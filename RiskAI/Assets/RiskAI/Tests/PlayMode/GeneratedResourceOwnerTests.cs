using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.AI.Navigation;

namespace RiskAI.Tests
{
    public sealed class GeneratedResourceOwnerTests
    {
        [UnityTest]
        public IEnumerator TrackingAfterDisposeAlsoReleasesTheLateRuntimeResource()
        {
            var root=new GameObject("Disposed generated resource root");
            var owner=root.AddComponent<GeneratedResourceOwner>();
            var initial=owner.Track(new Mesh { name="Initial generated mesh" });
            owner.DisposeOwnedResources();
            var late=owner.Track(new Mesh { name="Late generated mesh" });
            yield return null;

            Assert.That(initial==null,Is.True);
            Assert.That(late==null,Is.True,"A producer that races teardown must not leak a resource after its owner has disposed.");
            Assert.That(owner.Count,Is.Zero);
            Object.Destroy(root);
        }

        [UnityTest]
        public IEnumerator BootstrapDestroysItsOwnedRuntimeNavMeshDataOnSceneUnload()
        {
            var previous=SceneManager.GetActiveScene();
            var previousMap=BattleSession.MapForNewMatch;
            var previousLayout=BattleSession.LayoutForNewMatch;
            var previousPlayers=BattleSession.PlayerCountForNewMatch;
            BattleSession.MapForNewMatch=ScenarioMap.Classic;
            BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;
            BattleSession.PlayerCountForNewMatch=2;
            var generated=SceneManager.CreateScene("Generated NavMesh ownership");
            SceneManager.SetActiveScene(generated);
            new GameObject("NavMesh ownership bootstrap").AddComponent<RiskBootstrap>();
            yield return null;
            var surface=Object.FindFirstObjectByType<NavMeshSurface>();
            Assert.That(surface,Is.Not.Null);
            var data=surface.navMeshData;
            Assert.That(data,Is.Not.Null);

            SceneManager.SetActiveScene(previous);
            yield return SceneManager.UnloadSceneAsync(generated);
            yield return null;
            Assert.That(data==null,Is.True,"Runtime NavMeshData must share the battlefield scene lifetime.");

            BattleSession.MapForNewMatch=previousMap;
            BattleSession.LayoutForNewMatch=previousLayout;
            BattleSession.PlayerCountForNewMatch=previousPlayers;
            MapLayout.Configure(previousMap);
        }

        [UnityTest]
        public IEnumerator SceneRestartDestroysTrackedRuntimeMeshAndMaterial()
        {
            var previous=SceneManager.GetActiveScene();
            var generated=SceneManager.CreateScene("Generated resource ownership");
            SceneManager.SetActiveScene(generated);
            var root=new GameObject("Generated resource root");
            var owner=root.AddComponent<GeneratedResourceOwner>();
            var mesh=owner.Track(new Mesh { name="Test generated mesh" });
            var template=Resources.Load<Material>("Meadow");
            Assert.That(template,Is.Not.Null,"The authored meadow material is a required shared asset for this test.");
            var material=owner.Track(new Material(template) { name="Test generated material" });
            Assert.That(owner.Count,Is.EqualTo(2));

            SceneManager.SetActiveScene(previous);
            yield return SceneManager.UnloadSceneAsync(generated);
            yield return null;

            Assert.That(mesh==null,Is.True,"The scene owner must explicitly release generated meshes on restart.");
            Assert.That(material==null,Is.True,"The scene owner must explicitly release generated material instances on restart.");
            Assert.That(template!=null,Is.True,"The owner must never destroy a shared Resources material.");

            var restarted=SceneManager.CreateScene("Generated resource restart");
            SceneManager.SetActiveScene(restarted);
            var fresh=new GameObject("Fresh generated resource root").AddComponent<GeneratedResourceOwner>();
            Assert.That(fresh.Count,Is.Zero,"A new match starts with no resources inherited from the prior scene.");
            SceneManager.SetActiveScene(previous);
            yield return SceneManager.UnloadSceneAsync(restarted);
        }
    }
}
