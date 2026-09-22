using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class StaticArchitectureBatchingTests
    {
        [Test]
        public void DisableFlagProvidesSameBuildAbControl()
        {
            Assert.That(StaticArchitectureBatching.EnabledFor(null),Is.False);
            Assert.That(StaticArchitectureBatching.EnabledFor(new[]{"game.exe","--RISKAI-DISABLE-ARCHITECTURE-BATCHING"}),Is.False);
            Assert.That(StaticArchitectureBatching.EnabledFor(new[]{"game.exe","--riskai-disable-unit-lod"}),Is.False);
            Assert.That(StaticArchitectureBatching.ModeFor(new[]{"game.exe",StaticArchitectureBatching.NativeFlag}),
                Is.EqualTo(StaticArchitectureBatching.Mode.Native));
            Assert.That(StaticArchitectureBatching.ModeFor(new[]{"game.exe",StaticArchitectureBatching.ManualFlag}),
                Is.EqualTo(StaticArchitectureBatching.Mode.Manual));
            Assert.That(StaticArchitectureBatching.ModeFor(new[]{"game.exe",StaticArchitectureBatching.NativeFlag,StaticArchitectureBatching.ManualFlag}),
                Is.EqualTo(StaticArchitectureBatching.Mode.Manual),"Manual mode wins when both opt-in flags are present.");
            Assert.That(StaticArchitectureBatching.ModeFor(new[]{"game.exe",StaticArchitectureBatching.ManualFlag,StaticArchitectureBatching.DisableFlag}),
                Is.EqualTo(StaticArchitectureBatching.Mode.Disabled),"The global opt-out must take precedence over manual mode.");
        }

        [UnityTest]
        public IEnumerator NativeBatchPreservesBoundsAndCollidersAndDisposesGeneratedMeshes()
        {
            var root=new GameObject("Native static architecture contract");
            root.transform.SetPositionAndRotation(new Vector3(41,3,-27),Quaternion.Euler(0,31,0));
            root.transform.localScale=Vector3.one*.72f;
            var first=VisualFactory.Shape(root.transform,PrimitiveType.Cube,"Native masonry A",new Vector3(-1.4f,1,0),new Vector3(2,2,2),Color.gray,true);
            var second=VisualFactory.Shape(root.transform,PrimitiveType.Cube,"Native masonry B",new Vector3(1.4f,.7f,.3f),new Vector3(1.5f,1.4f,1.8f),Color.gray,true);
            var renderers=new[]{first.GetComponent<MeshRenderer>(),second.GetComponent<MeshRenderer>()};
            var bounds=renderers.Select(renderer=>renderer.bounds).ToArray();
            var colliders=new[]{first.GetComponent<Collider>(),second.GetComponent<Collider>()};
            var originals=new HashSet<Mesh>(root.GetComponentsInChildren<MeshFilter>(true).Select(filter=>filter.sharedMesh));

            var result=StaticArchitectureBatching.Combine(root.transform,StaticArchitectureBatching.Mode.Native);

            Assert.That(result.Applied,Is.True);Assert.That(result.CandidateCount,Is.EqualTo(2));
            Assert.That(renderers.All(renderer=>renderer.isPartOfStaticBatch),Is.True);
            for(int i=0;i<renderers.Length;i++)
            {
                Assert.That(Vector3.Distance(renderers[i].bounds.center,bounds[i].center),Is.LessThan(.001f));
                Assert.That(Vector3.Distance(renderers[i].bounds.size,bounds[i].size),Is.LessThan(.001f));
            }
            Assert.That(colliders.All(collider=>collider&&collider.enabled&&!collider.isTrigger),Is.True);
            var generated=root.GetComponentsInChildren<MeshFilter>(true).Select(filter=>filter.sharedMesh)
                .Where(mesh=>mesh&&!originals.Contains(mesh)).Distinct().ToArray();
            Assert.That(generated.Length,Is.EqualTo(result.CreatedMeshCount));
            Assert.That(root.GetComponent<GeneratedResourceOwner>().Count,Is.EqualTo(generated.Length));

            int ownedAfterFirstBatch=root.GetComponent<GeneratedResourceOwner>().Count;
            var repeated=StaticArchitectureBatching.Combine(root.transform,StaticArchitectureBatching.Mode.Native);
            Assert.That(repeated.Applied,Is.False);Assert.That(repeated.CreatedMeshCount,Is.EqualTo(0));
            Assert.That(root.GetComponent<GeneratedResourceOwner>().Count,Is.EqualTo(ownedAfterFirstBatch));
            Object.Destroy(root);yield return null;yield return null;
            Assert.That(generated.All(mesh=>!mesh),Is.True);
        }

        [UnityTest]
        public IEnumerator ManualBatchPreservesBoundsCollidersMutablesAndDisposesGeneratedMeshes()
        {
            var root=new GameObject("Static architecture contract");
            root.transform.SetPositionAndRotation(new Vector3(41,3,-27),Quaternion.Euler(0,31,0));
            root.transform.localScale=Vector3.one*.72f;
            var first=VisualFactory.Shape(root.transform,PrimitiveType.Cube,"Stable masonry A",new Vector3(-1.4f,1,0),new Vector3(2,2,2),Color.gray,true);
            var second=VisualFactory.Shape(root.transform,PrimitiveType.Cube,"Stable masonry B",new Vector3(1.4f,.7f,.3f),new Vector3(1.5f,1.4f,1.8f),Color.gray,true);
            var mutable=VisualFactory.Shape(root.transform,PrimitiveType.Cube,"Mutable standard",new Vector3(0,2.8f,0),Vector3.one,Color.red);
            var overridden=VisualFactory.Shape(root.transform,PrimitiveType.Cube,"Property block override",new Vector3(0,.5f,2.5f),Vector3.one,Color.gray);
            var properties=new MaterialPropertyBlock();properties.SetColor("_BaseColor",Color.yellow);overridden.GetComponent<Renderer>().SetPropertyBlock(properties);
            var transparent=VisualFactory.Shape(root.transform,PrimitiveType.Quad,"Transparent ornament",new Vector3(0,3.5f,0),Vector3.one,Color.white);
            var transparentMaterial=new Material(Shader.Find("Sprites/Default"));transparentMaterial.renderQueue=3001;
            transparent.GetComponent<Renderer>().sharedMaterial=transparentMaterial;
            var shadow=VisualFactory.Shape(root.transform,PrimitiveType.Quad,"Soft ground shadow",Vector3.zero,Vector3.one,Color.black);
            var rotor=new GameObject("Animated mill rotor");rotor.transform.SetParent(root.transform,false);rotor.AddComponent<MillSails>();
            var sailA=VisualFactory.Shape(rotor.transform,PrimitiveType.Cube,"Sail A",Vector3.up,new Vector3(.2f,2,.1f),Color.white);
            var sailB=VisualFactory.Shape(rotor.transform,PrimitiveType.Cube,"Sail B",Vector3.right,new Vector3(2,.2f,.1f),Color.white);

            var stableRenderers=new[]{first.GetComponent<MeshRenderer>(),second.GetComponent<MeshRenderer>()};
            var beforeVertexBounds=VertexBounds(stableRenderers.Select(renderer=>renderer.GetComponent<MeshFilter>()));
            var colliders=new[]{first.GetComponent<Collider>(),second.GetComponent<Collider>()};
            var firstMesh=first.GetComponent<MeshFilter>().sharedMesh;var secondMesh=second.GetComponent<MeshFilter>().sharedMesh;
            var originalMeshes=new HashSet<Mesh>(root.GetComponentsInChildren<MeshFilter>(true).Select(filter=>filter.sharedMesh));
            var stableMaterial=stableRenderers[0].sharedMaterial;var mutableMaterial=mutable.GetComponent<Renderer>().sharedMaterial;

            var result=StaticArchitectureBatching.Combine(root.transform,StaticArchitectureBatching.Mode.Manual,mutable.GetComponent<Renderer>());

            Assert.That(result.Applied,Is.True);Assert.That(result.CandidateCount,Is.EqualTo(2));
            Assert.That(stableRenderers.All(renderer=>!renderer.enabled),Is.True);
            var combined=root.GetComponentsInChildren<MeshRenderer>().Where(renderer=>renderer.gameObject.name.StartsWith("Manual architecture batch · ")).ToArray();
            Assert.That(combined.Length,Is.EqualTo(1));Assert.That(combined[0].sharedMaterial,Is.SameAs(stableMaterial));
            var afterVertexBounds=VertexBounds(combined.Select(renderer=>renderer.GetComponent<MeshFilter>()));
            Assert.That(Vector3.Distance(afterVertexBounds.center,beforeVertexBounds.center),Is.LessThan(.001f));
            Assert.That(Vector3.Distance(afterVertexBounds.size,beforeVertexBounds.size),Is.LessThan(.001f));
            foreach(var renderer in combined)AssertRendererContainsMesh(renderer);
            Assert.That(mutable.GetComponent<Renderer>().enabled,Is.True);Assert.That(mutable.GetComponent<Renderer>().sharedMaterial,Is.SameAs(mutableMaterial));
            Assert.That(overridden.GetComponent<Renderer>().enabled,Is.True);Assert.That(overridden.GetComponent<Renderer>().HasPropertyBlock(),Is.True);
            Assert.That(transparent.GetComponent<Renderer>().enabled,Is.True);
            Assert.That(shadow.GetComponent<Renderer>().enabled,Is.True);
            Assert.That(sailA.GetComponent<Renderer>().enabled&&sailB.GetComponent<Renderer>().enabled,Is.True,
                "Animated mill descendants must retain independent transforms.");
            Assert.That(colliders.All(collider=>collider&&collider.enabled&&!collider.isTrigger),Is.True);
            Assert.That(first.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(firstMesh));
            Assert.That(second.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(secondMesh),
                "Manual batching retains each original MeshFilter reference.");

            var generated=root.GetComponentsInChildren<MeshFilter>(true).Select(filter=>filter.sharedMesh)
                .Where(mesh=>mesh&&!originalMeshes.Contains(mesh)).Distinct().ToArray();
            Assert.That(generated.Length,Is.EqualTo(result.CreatedMeshCount));
            Assert.That(root.GetComponent<GeneratedResourceOwner>().Count,Is.EqualTo(generated.Length));
            int ownedAfterFirstBatch=root.GetComponent<GeneratedResourceOwner>().Count;
            var repeated=StaticArchitectureBatching.Combine(root.transform,StaticArchitectureBatching.Mode.Manual,mutable.GetComponent<Renderer>());
            Assert.That(repeated.Applied,Is.False);Assert.That(repeated.CandidateCount,Is.EqualTo(0));
            Assert.That(repeated.CreatedMeshCount,Is.EqualTo(0));
            Assert.That(root.GetComponent<GeneratedResourceOwner>().Count,Is.EqualTo(ownedAfterFirstBatch),
                "Combining an already batched group must not allocate or register another mesh.");

            Object.Destroy(transparentMaterial);Object.Destroy(root);
            yield return null;yield return null;
            Assert.That(generated.All(mesh=>!mesh),Is.True,"Scene-owned static batch meshes must be released with their architecture root.");
        }

        [UnityTest]
        public IEnumerator TowerBatchLeavesFactionSurfacesAndScaffoldingDynamic()
        {
            if(StaticArchitectureBatching.ActiveMode!=StaticArchitectureBatching.Mode.Native)Assert.Ignore("This contract exercises native mode.");
            var root=new GameObject("Tower batching contract");
            VisualFactory.Tower(root.transform,0,BuildingVariant.IntegratedTown,out var upper,out var scaffold,out var banner);
            var roof=upper.GetComponentsInChildren<MeshRenderer>().Single(renderer=>renderer.name=="Faction roof");
            var stable=upper.GetComponentsInChildren<MeshRenderer>().First(renderer=>renderer.name=="Integrated stone turret");

            Assert.That(stable.isPartOfStaticBatch,Is.True);
            Assert.That(roof.isPartOfStaticBatch,Is.False);Assert.That(banner.isPartOfStaticBatch,Is.False);
            Assert.That(scaffold.GetComponentsInChildren<MeshRenderer>().Any(renderer=>renderer.isPartOfStaticBatch),Is.False);
            var replacement=VisualFactory.Mat(new Color(.13f,.47f,.81f));
            roof.sharedMaterial=replacement;banner.sharedMaterial=replacement;
            Assert.That(roof.sharedMaterial,Is.SameAs(replacement));Assert.That(banner.sharedMaterial,Is.SameAs(replacement));

            upper.SetActive(false);scaffold.SetActive(true);yield return null;
            Assert.That(stable.gameObject.activeInHierarchy,Is.False);
            Assert.That(scaffold.GetComponentsInChildren<MeshRenderer>().All(renderer=>renderer.gameObject.activeInHierarchy),Is.True);
            upper.SetActive(true);scaffold.SetActive(false);yield return null;
            Assert.That(stable.gameObject.activeInHierarchy,Is.True);
            Object.Destroy(root);yield return null;
        }

        [UnityTest]
        public IEnumerator ManualTowerBatchLeavesFactionSurfacesAndScaffoldingDynamic()
        {
            if(StaticArchitectureBatching.ActiveMode!=StaticArchitectureBatching.Mode.Manual)Assert.Ignore("Run with the manual architecture flag.");
            var root=new GameObject("Manual tower batching contract");
            VisualFactory.Tower(root.transform,0,BuildingVariant.IntegratedTown,out var upper,out var scaffold,out var banner);
            var roof=upper.GetComponentsInChildren<MeshRenderer>().Single(renderer=>renderer.name=="Faction roof");
            var originalGallery=upper.GetComponentsInChildren<MeshRenderer>(true).First(renderer=>renderer.name=="Integrated gallery");
            var combined=upper.GetComponentsInChildren<MeshRenderer>().Where(renderer=>renderer.gameObject.name.StartsWith("Manual architecture batch · ")).ToArray();

            Assert.That(originalGallery.enabled,Is.False);Assert.That(combined,Is.Not.Empty);
            Assert.That(roof.enabled,Is.True);Assert.That(banner.enabled,Is.True);
            Assert.That(scaffold.GetComponentsInChildren<MeshRenderer>().Any(renderer=>renderer.gameObject.name.StartsWith("Manual architecture batch · ")),Is.False);
            var replacement=VisualFactory.Mat(new Color(.19f,.58f,.72f));
            roof.sharedMaterial=replacement;banner.sharedMaterial=replacement;
            Assert.That(roof.sharedMaterial,Is.SameAs(replacement));Assert.That(banner.sharedMaterial,Is.SameAs(replacement));

            upper.SetActive(false);scaffold.SetActive(true);yield return null;
            Assert.That(combined.All(renderer=>!renderer.gameObject.activeInHierarchy),Is.True);
            Assert.That(scaffold.GetComponentsInChildren<MeshRenderer>().All(renderer=>renderer.gameObject.activeInHierarchy),Is.True);
            upper.SetActive(true);scaffold.SetActive(false);yield return null;
            Assert.That(combined.All(renderer=>renderer.gameObject.activeInHierarchy),Is.True);
            Object.Destroy(root);yield return null;
        }

        static Bounds VertexBounds(IEnumerable<MeshFilter> filters)
        {
            bool found=false;var bounds=default(Bounds);
            foreach(var filter in filters)foreach(var vertex in filter.sharedMesh.vertices)
            {
                var world=filter.transform.TransformPoint(vertex);
                if(!found){bounds=new Bounds(world,Vector3.zero);found=true;}else bounds.Encapsulate(world);
            }
            Assert.That(found,Is.True);return bounds;
        }

        static void AssertRendererContainsMesh(MeshRenderer renderer)
        {
            var bounds=renderer.bounds;var filter=renderer.GetComponent<MeshFilter>();
            foreach(var vertex in filter.sharedMesh.vertices)
            {
                var world=filter.transform.TransformPoint(vertex);
                Assert.That(world.x,Is.InRange(bounds.min.x-.001f,bounds.max.x+.001f));
                Assert.That(world.y,Is.InRange(bounds.min.y-.001f,bounds.max.y+.001f));
                Assert.That(world.z,Is.InRange(bounds.min.z-.001f,bounds.max.z+.001f));
            }
        }
    }
}
