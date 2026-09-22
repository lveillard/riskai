using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI.Tests
{
    public class UnitPresentationLodTests
    {
        [Test]
        public void ZoomPolicyUsesDifferentCompactThresholdsAndHysteresis()
        {
            Assert.That(UnitPresentationLodPolicy.UseProxy(87, true, false), Is.False);
            Assert.That(UnitPresentationLodPolicy.UseProxy(88, true, false), Is.True);
            Assert.That(UnitPresentationLodPolicy.UseProxy(82, true, true), Is.True);
            Assert.That(UnitPresentationLodPolicy.UseProxy(78, true, true), Is.False);
            Assert.That(UnitPresentationLodPolicy.UseProxy(95, false, false), Is.False);
            Assert.That(UnitPresentationLodPolicy.UseProxy(96, false, false), Is.True);
        }

        [Test]
        public void ViewportPolicyKeepsPartialSilhouettesAndRejectsUnitsBehindTheCamera()
        {
            Assert.That(UnitPresentationLodPolicy.IsInsideViewport(new Vector3(-.10f,.5f,10),.03f,.05f),Is.True,
                "The visual radius plus the 8% margin keeps an edge silhouette active.");
            Assert.That(UnitPresentationLodPolicy.IsInsideViewport(new Vector3(-.20f,.5f,10),.03f,.05f),Is.False);
            Assert.That(UnitPresentationLodPolicy.IsInsideViewport(new Vector3(.5f,.5f,-1),.1f,.1f),Is.False);
        }

        [Test]
        public void SharedProxyReplacesDetailButSelectionRestoresIt()
        {
            var root = new GameObject("LOD test unit");
            var detailObject = new GameObject("Detailed model");
            detailObject.transform.SetParent(root.transform, false);
            var detail = detailObject.AddComponent<MeshRenderer>();
            var view = root.AddComponent<UnitPresentationLodView>();
            view.Initialize(UnitKind.Archer, 0);
            try
            {
                Assert.That(view.DetailRendererCount, Is.EqualTo(1));
                Assert.That(detail.enabled, Is.True);
                Assert.That(view.ProxyRenderer.enabled, Is.False);

                view.SetGlobalProxy(true);
                Assert.That(view.UsingProxy, Is.True);
                Assert.That(detail.enabled, Is.False);
                Assert.That(view.ProxyRenderer.enabled, Is.True);

                view.SetSelected(true);
                Assert.That(view.UsingProxy, Is.False);
                Assert.That(detail.enabled, Is.True);
                Assert.That(view.ProxyRenderer.enabled, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SameArchetypeUsesOneSharedProxyMesh()
        {
            var first = new GameObject("First proxy");
            var second = new GameObject("Second proxy");
            try
            {
                var a = first.AddComponent<UnitPresentationLodView>();a.Initialize(UnitKind.Archer, 0);
                var b = second.AddComponent<UnitPresentationLodView>();b.Initialize(UnitKind.Archer, 1);
                Assert.That(a.ProxyRenderer.GetComponent<MeshFilter>().sharedMesh,
                    Is.SameAs(b.ProxyRenderer.GetComponent<MeshFilter>().sharedMesh));
            }
            finally { Object.DestroyImmediate(first);Object.DestroyImmediate(second); }
        }


        [Test]
        public void OffscreenStateStopsOnlyControllersAndCombinesWithProxyAndSelection()
        {
            var root=new GameObject("Culled unit");var detailObject=new GameObject("Detailed model");
            detailObject.transform.SetParent(root.transform,false);var detail=detailObject.AddComponent<MeshRenderer>();
            var legacyAnimation=detailObject.AddComponent<Animation>();
            var controller=root.AddComponent<SoldierAnimator>();var view=root.AddComponent<UnitPresentationLodView>();
            view.Initialize(UnitKind.Archer,0);
            try
            {
                Assert.That(view.AnimationControllerCount,Is.EqualTo(1));
                Assert.That(view.ControllersActive,Is.True);
                detail.enabled=false;legacyAnimation.enabled=false;
                view.SetInCameraView(false);
                Assert.That(controller.enabled,Is.False);
                Assert.That(detail.enabled,Is.False,"Camera culling must not undo an external renderer override.");
                Assert.That(legacyAnimation.enabled,Is.False,"Camera culling must not undo an external animation override.");
                view.SetInCameraView(true);
                Assert.That(detail.enabled,Is.False);
                Assert.That(legacyAnimation.enabled,Is.False);
                detail.enabled=true;legacyAnimation.enabled=true;
                view.SetInCameraView(false);

                view.SetGlobalProxy(true);
                Assert.That(view.UsingProxy,Is.True);
                view.SetSelected(true);
                Assert.That(view.UsingProxy,Is.False,"Selection still restores the detailed silhouette.");
                Assert.That(controller.enabled,Is.False,"Selection outside the camera does not restart per-frame animation work.");
                view.SetInCameraView(true);
                Assert.That(controller.enabled,Is.True);
            }
            finally{Object.DestroyImmediate(root);}
        }

    }
}
