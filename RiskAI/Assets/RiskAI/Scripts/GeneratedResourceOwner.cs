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
            if (disposed) { Destroy(resource); return resource; }
            resources.Add(resource);
            return resource;
        }

        public static GeneratedResourceOwner For(Transform root)
        {
            if (!root) return null;
            var owner = root.GetComponent<GeneratedResourceOwner>();
            return owner ? owner : root.gameObject.AddComponent<GeneratedResourceOwner>();
        }

        public void DisposeOwnedResources()
        {
            if (disposed) return;
            disposed = true;
            for (int i = 0; i < resources.Count; i++)
                if (resources[i]) Destroy(resources[i]);
            resources.Clear();
        }

        void OnDestroy() => DisposeOwnedResources();
    }
}
