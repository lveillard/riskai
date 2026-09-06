using System.Collections.Generic;
using UnityEngine;

namespace RiskAI
{
    /// <summary>Decorative, collider-free posts marking ownership borders between nearby towns.</summary>
    [DisallowMultipleComponent]
    public sealed class TerritoryMarkers : MonoBehaviour
    {
        const float GridStep = 2.5f;
        const float MinimumPostDistance = 3.6f;
        const float TownClearance = 5f;
        const float MaximumSlope = .65f;
        const int MaximumMarkers = 400;
        const float OwnerRefreshSeconds = .4f;

        sealed class Post
        {
            public GameObject Object;
            public int CityA;
            public int CityB;
            public int CountryA;
            public int CountryB;
            public int OwnerA;
            public int OwnerB;
            public bool Visible;
            public Renderer HeadA;
            public Renderer HeadB;
            public Vector3 Position;
        }

        readonly List<Post> posts = new List<Post>(MaximumMarkers);
        readonly List<Renderer> postRenderers = new List<Renderer>(MaximumMarkers);
        BattleSession session;
        Transform markerRoot;
        float ownerRefreshTimer;

        public int MarkerCount => posts.Count;

        /// <summary>Returns the nearest map town's country for an XZ world position.</summary>
        public static int CountryAt(Vector3 position)
        {
            return CityAt(position, out int city) && city >= 0 ? MapLayout.Towns[city].Country : -1;
        }

        public static TerritoryMarkers Create(BattleSession battle, Transform root)
        {
            if (!battle || !root) return null;
            var markers = root.GetComponent<TerritoryMarkers>();
            if (!markers) markers = root.gameObject.AddComponent<TerritoryMarkers>();
            markers.Initialize(battle, root);
            return markers;
        }

        void Initialize(BattleSession battle, Transform root)
        {
            session = battle;
            posts.Clear();
            postRenderers.Clear();
            ownerRefreshTimer = 0;

            var oldRoot = root.Find("Territory posts");
            if (oldRoot)
            {
                if (Application.isPlaying) Destroy(oldRoot.gameObject);
                else DestroyImmediate(oldRoot.gameObject);
            }
            var container = new GameObject("Territory posts");
            container.transform.SetParent(root, false);
            markerRoot = container.transform;
            BuildPosts();
        }

        void BuildPosts()
        {
            float step=MapLayout.IsImported?10:GridStep;int limit=MapLayout.IsImported?1800:MaximumMarkers;
            int xCount = Mathf.CeilToInt(MapLayout.HalfWidth * 2f / step);
            int zCount = Mathf.CeilToInt(MapLayout.HalfDepth * 2f / step);
            for (int ix = 0; ix <= xCount && posts.Count < limit; ix++)
            {
                float x = Mathf.Min(MapLayout.HalfWidth, -MapLayout.HalfWidth + ix * step);
                for (int iz = 0; iz <= zCount && posts.Count < limit; iz++)
                {
                    float z = Mathf.Min(MapLayout.HalfDepth, -MapLayout.HalfDepth + iz * step);
                    var sample = new Vector3(x, 0, z);
                    TryBoundary(sample, new Vector3(Mathf.Min(MapLayout.HalfWidth, x + step), 0, z));
                    if (posts.Count >= limit) break;
                    TryBoundary(sample, new Vector3(x, 0, Mathf.Min(MapLayout.HalfDepth, z + step)));
                }
            }
        }

        void TryBoundary(Vector3 first, Vector3 second)
        {
            if (Mathf.Approximately(first.x, second.x) && Mathf.Approximately(first.z, second.z)) return;
            if (!MapLayout.IsLand(first.x, first.z) || !MapLayout.IsLand(second.x, second.z)) return;
            if (Mathf.Abs(MapLayout.Height(first.x, first.z) - MapLayout.Height(second.x, second.z)) > MaximumSlope) return;

            Vector3 candidate = (first + second) * .5f;
            if (!MapLayout.IsLand(candidate.x, candidate.z)) return;
            if (!MapLayout.IsImported && candidate.z > MapLayout.Coast(candidate.x)) return;
            if (!MapLayout.IsImported && TerrainHydrology.DistanceToRiver(candidate.x, candidate.z) < 3f) return;
            if (NearTown(candidate)) return;

            if (!CityAt(first, out int cityA) || !CityAt(second, out int cityB) || cityA == cityB) return;
            candidate.y = MapLayout.Height(candidate.x, candidate.z) + .025f;
            if (!FarEnoughFromPosts(candidate)) return;
            AddPost(candidate, cityA, cityB);
        }

        static bool CityAt(Vector3 position, out int city)
        {
            city = -1;
            if (float.IsNaN(position.x) || float.IsNaN(position.z) ||
                float.IsInfinity(position.x) || float.IsInfinity(position.z)) return false;

            float nearest = float.PositiveInfinity;
            for (int i = 0; i < MapLayout.Towns.Length; i++)
            {
                Vector3 town = MapLayout.Towns[i].Position;
                float dx = position.x - town.x;
                float dz = position.z - town.z;
                float distance = dx * dx + dz * dz;
                if (distance < nearest)
                {
                    nearest = distance;
                    city = i;
                }
            }
            return city >= 0;
        }

        static bool NearTown(Vector3 position)
        {
            float clearance = TownClearance * TownClearance;
            for (int i = 0; i < MapLayout.Towns.Length; i++)
            {
                Vector3 town = MapLayout.Towns[i].Position;
                float dx = position.x - town.x;
                float dz = position.z - town.z;
                if (dx * dx + dz * dz < clearance) return true;
            }
            return false;
        }

        bool FarEnoughFromPosts(Vector3 position)
        {
            float spacing = MinimumPostDistance * MinimumPostDistance;
            for (int i = 0; i < posts.Count; i++)
            {
                Vector3 other = posts[i].Position;
                float dx = position.x - other.x;
                float dz = position.z - other.z;
                if (dx * dx + dz * dz < spacing) return false;
            }
            return true;
        }

        void AddPost(Vector3 position, int cityA, int cityB)
        {
            int countryA = MapLayout.Towns[cityA].Country;
            int countryB = MapLayout.Towns[cityB].Country;
            int ownerA = OwnerOfCity(cityA);
            int ownerB = OwnerOfCity(cityB);

            var postObject = new GameObject("Territory post");
            postObject.transform.SetParent(markerRoot, false);
            postObject.transform.localPosition = position;

            var footing = VisualFactory.Shape(postObject.transform, PrimitiveType.Cube, "Stone footing",
                new Vector3(0, .07f, 0), new Vector3(.4f, .14f, .4f), Color.white);
            footing.GetComponent<Renderer>().sharedMaterial = WorldArt.Painted(0, new Color(.52f, .53f, .48f), .35f);

            var wood = VisualFactory.Shape(postObject.transform, PrimitiveType.Cube, "Whitewashed wooden post",
                new Vector3(0, .52f, 0), new Vector3(.23f, .9f, .23f), Color.white);
            wood.GetComponent<Renderer>().sharedMaterial = WorldArt.Painted(2, new Color(.82f, .72f, .53f), .4f);
            postRenderers.Add(wood.GetComponent<Renderer>());

            var headA = VisualFactory.Shape(postObject.transform, PrimitiveType.Cube, "Town colour A",
                new Vector3(-.08f, .87f, 0), new Vector3(.16f, .26f, .24f), TeamColor(ownerA));
            var headB = VisualFactory.Shape(postObject.transform, PrimitiveType.Cube, "Town colour B",
                new Vector3(.08f, .87f, 0), new Vector3(.16f, .26f, .24f), TeamColor(ownerB));
            var cap = VisualFactory.Cone(postObject.transform, "Bronze post cap", new Vector3(0, .96f, 0), .17f, .06f,
                new Color(.62f, .42f, .16f), 4, 45);
            cap.GetComponent<Renderer>().sharedMaterial = VisualFactory.Mat(new Color(.62f, .42f, .16f));
            postRenderers.Add(headA.GetComponent<Renderer>());
            postRenderers.Add(headB.GetComponent<Renderer>());
            postRenderers.Add(cap.GetComponent<Renderer>());

            bool visible = ShouldShow(countryA, countryB, ownerA, ownerB);
            postObject.SetActive(visible);
            posts.Add(new Post
            {
                Object = postObject,
                CityA = cityA,
                CityB = cityB,
                CountryA = countryA,
                CountryB = countryB,
                OwnerA = ownerA,
                OwnerB = ownerB,
                Visible = visible,
                HeadA = headA.GetComponent<Renderer>(),
                HeadB = headB.GetComponent<Renderer>(),
                Position = position
            });
        }

        int OwnerOfCity(int city)
        {
            if (!session || city < 0 || city >= session.Towns.Count) return -1;
            var town = session.Towns[city];
            return town ? town.State.Owner : -1;
        }

        static Color TeamColor(int owner)
        {
            return VisualFactory.TeamColor(owner);
        }

        /// <summary>Internal country borders vanish whenever both adjacent cities share an owner.</summary>
        public static bool IsOwnershipBoundary(int ownerA, int ownerB) => ownerA != ownerB;

        static bool ShouldShow(int countryA, int countryB, int ownerA, int ownerB)
        {
            return IsOwnershipBoundary(ownerA, ownerB);
        }

        void Update()
        {
            if (!session || posts.Count == 0) return;
            ownerRefreshTimer += Time.unscaledDeltaTime;
            if (ownerRefreshTimer < OwnerRefreshSeconds) return;
            ownerRefreshTimer = 0;

            for (int i = 0; i < posts.Count; i++)
            {
                var post = posts[i];
                int ownerA = OwnerOfCity(post.CityA);
                int ownerB = OwnerOfCity(post.CityB);
                if (ownerA == post.OwnerA && ownerB == post.OwnerB) continue;
                post.OwnerA = ownerA;
                post.OwnerB = ownerB;
                post.HeadA.sharedMaterial = VisualFactory.Mat(TeamColor(ownerA));
                post.HeadB.sharedMaterial = VisualFactory.Mat(TeamColor(ownerB));
                post.Visible = ShouldShow(post.CountryA, post.CountryB, ownerA, ownerB);
                post.Object.SetActive(post.Visible);
            }
        }
    }
}
