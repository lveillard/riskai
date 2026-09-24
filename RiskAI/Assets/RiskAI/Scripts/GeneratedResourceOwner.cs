using System.Collections.Generic;
using UnityEngine;

namespace RiskAI
{
    /// <summary>Owns runtime-created scene resources; never register Resources assets or static caches here.</summary>
    public sealed class GeneratedResourceOwner : MonoBehaviour
    {
        readonly List<Object> resources = new List<Object>();
        bool disposed;

        public int Count => resources.Count;

        public T Track<T>(T resource) where T : Object
        {
            if (!resource) return resource;
            if (disposed) { DestroyResource(resource); return resource; }
            resources.Add(resource);
            return resource;
        }

        /// <summary>
        /// Early release for a tracked resource that ends before the session does: a scratch
        /// mesh, or an overlay rebuilt on resize. Disposal stays single-source; the owner is
        /// still the only code that destroys generated native assets.
        /// </summary>
        public void Release<T>(T resource) where T : Object
        {
            resources.Remove(resource);
            if (resource) DestroyResource(resource);
        }

        static void DestroyResource(Object resource)
        {
            if (Application.isPlaying) Destroy(resource); else DestroyImmediate(resource);
        }

        public static GeneratedResourceOwner For(Transform root)
        {
            if (!root) return null;
            var owner = root.GetComponent<GeneratedResourceOwner>();
            return owner ? owner : root.gameObject.AddComponent<GeneratedResourceOwner>();
        }

        /// <summary>
        /// Static-batches the renderers under <paramref name="root"/> and takes ownership of the
        /// mesh Unity builds. <see cref="StaticBatchingUtility.Combine(GameObject)"/> copies the
        /// geometry into a runtime mesh that no scene object owns, so unloading the scene never
        /// releases it; the batch must die with the session that asked for it, exactly like every
        /// other generated mesh.
        /// </summary>
        public static void CombineStaticBatches(Transform root)
        {
            if (!root) return;
            var owner = For(root);
            // Snapshot the meshes the renderers use before batching: the only new mesh after the
            // call is the combined one, so it is never confused with a shared or authored asset.
            var before = new HashSet<int>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh) before.Add(filter.sharedMesh.GetInstanceID());
            StaticBatchingUtility.Combine(root.gameObject);
            var batched = new HashSet<int>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (!mesh) continue;
                int id = mesh.GetInstanceID();
                if (before.Contains(id) || !batched.Add(id)) continue;
                owner.Track(mesh);
            }
        }

        public void DisposeOwnedResources()
        {
            if (disposed) return;
            disposed = true;
            for (int i = 0; i < resources.Count; i++)
                if (resources[i]) DestroyResource(resources[i]);
            resources.Clear();
        }

        void OnDestroy() => DisposeOwnedResources();
    }
}
