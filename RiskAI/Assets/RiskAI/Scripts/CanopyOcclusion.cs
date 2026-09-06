using System.Collections.Generic;
using UnityEngine;

namespace RiskAI
{
    /// <summary>Conservative foliage visibility for HUD bars, without physics colliders or render passes.</summary>
    public sealed class CanopyOcclusion
    {
        const float CellSize = 16;
        const float MovementCellSize = 4;
        const byte ForestDensity = 2;
        public const float ForestSpeedMultiplier = .82f;
        readonly Dictionary<Vector2Int, List<Bounds>> cells = new Dictionary<Vector2Int, List<Bounds>>();
        // Built once from the same exact crown renderers used by the HUD occlusion
        // index.  This is a density field, never a physics or NavMesh obstacle.
        readonly Dictionary<Vector2Int, byte> movement = new Dictionary<Vector2Int, byte>();
        float highestCanopy = float.NegativeInfinity;
        bool building;
        public int MovementRevision { get; private set; }

        public void Build(Transform terrain)
        {
            cells.Clear();
            movement.Clear();
            highestCanopy = float.NegativeInfinity;
            building=true;
            // Once after terrain construction, including imported tree scales/rotations.
            foreach (var renderer in terrain.GetComponentsInChildren<MeshRenderer>())
                if (renderer.name == "Layered evergreen boughs" || renderer.name == "Painted foliage crown")
                    Add(renderer.bounds);
            building=false;
            MovementRevision++;
        }

        public void Add(Bounds bounds)
        {
            highestCanopy = Mathf.Max(highestCanopy, bounds.max.y);
            for (int x = Cell(bounds.min.x); x <= Cell(bounds.max.x); x++)
                for (int z = Cell(bounds.min.z); z <= Cell(bounds.max.z); z++)
                {
                    var key = new Vector2Int(x, z);
                    if (!cells.TryGetValue(key, out var bucket)) cells.Add(key, bucket = new List<Bounds>(8));
                    bucket.Add(bounds);
                }
            AddMovement(bounds);
            if(!building)MovementRevision++;
        }

        void AddMovement(Bounds bounds)
        {
            Vector2 center=new Vector2(bounds.center.x,bounds.center.z);
            float radius=Mathf.Max(bounds.extents.x,bounds.extents.z);
            int minX=MovementCell(bounds.min.x),maxX=MovementCell(bounds.max.x);
            int minZ=MovementCell(bounds.min.z),maxZ=MovementCell(bounds.max.z);
            for(int x=minX;x<=maxX;x++)for(int z=minZ;z<=maxZ;z++)
            {
                var key=new Vector2Int(x,z);
                var point=new Vector2((x+.5f)*MovementCellSize,(z+.5f)*MovementCellSize);
                if((point-center).sqrMagnitude>radius*radius)continue;
                movement.TryGetValue(key,out byte density);
                if(density<byte.MaxValue)movement[key]=(byte)(density+1);
            }
        }

        public Vector2Int MovementCellAt(Vector3 position) => new Vector2Int(MovementCell(position.x),MovementCell(position.z));
        public float MovementMultiplier(Vector2Int cell) => movement.TryGetValue(cell,out byte density)&&density>=ForestDensity?ForestSpeedMultiplier:1;

        public bool Obscures(Vector3 cameraPosition, Vector3 unitPosition)
        {
            if (unitPosition.y >= highestCanopy || cells.Count == 0) return false;
            var delta = cameraPosition - unitPosition;
            float distance = delta.magnitude;
            if (distance < .001f) return false;
            var direction = delta / distance;
            // RTS cameras are above the battlefield. Stop testing once above all crowns.
            if (direction.y <= .01f) return false;
            distance = Mathf.Min(distance, (highestCanopy - unitPosition.y) / direction.y);
            var end = unitPosition + direction * distance;
            var ray = new Ray(unitPosition, direction);
            for (int x = Cell(Mathf.Min(unitPosition.x, end.x)); x <= Cell(Mathf.Max(unitPosition.x, end.x)); x++)
                for (int z = Cell(Mathf.Min(unitPosition.z, end.z)); z <= Cell(Mathf.Max(unitPosition.z, end.z)); z++)
                    if (cells.TryGetValue(new Vector2Int(x, z), out var bucket))
                        for (int i = 0; i < bucket.Count; i++)
                            if (bucket[i].IntersectRay(ray, out float hit) && hit <= distance) return true;
            return false;
        }

        static int Cell(float coordinate) => Mathf.FloorToInt(coordinate / CellSize);
        static int MovementCell(float coordinate) => Mathf.FloorToInt(coordinate / MovementCellSize);
    }
}
