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
    }
}
