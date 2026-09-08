using UnityEngine;
using UnityEngine.AI;

namespace RiskAI
{
    /// <summary>Shared shore policy for transport commands and the painted ground.</summary>
    public static class ShoreAccess
    {
        static ScenarioMap cachedScenario=(ScenarioMap)(-1);
        static Vector3[] authoredPorts;
        static Vector3[] AuthoredPorts()
        {
            if(authoredPorts!=null&&cachedScenario==MapLayout.Scenario)return authoredPorts;
            cachedScenario=MapLayout.Scenario;
            int count=MapLayout.MainlandHarborX.Length;
            authoredPorts=new Vector3[count+MapLayout.Islands.Length];
            for(int i=0;i<count;i++)authoredPorts[i]=MapLayout.MainlandHarborLanding(i);
            for(int i=0;i<MapLayout.Islands.Length;i++)authoredPorts[count+i]=MapLayout.IslandHarborLanding(i);
            return authoredPorts;
        }
        // Source A00V/A00X requires Vcbp. Ports also expose an explicit walkable
        // pier: its platform may stand over W3E water, so IsLand alone is wrong.
        public static bool TryLanding(Vector3 requested,out Vector3 landing,out string error)
        {
            landing=default;error=null;
            if(!NavMesh.SamplePosition(requested,out var hit,1.25f,NavMesh.AllAreas))
            {error="Elige una playa de arena o un muelle transitable.";return false;}
            if(IsDock(hit.position)){landing=hit.position;return true;}
            if(!MapLayout.IsLand(requested.x,requested.z)||!MapLayout.IsLand(hit.position.x,hit.position.z)||
                SurfaceWeights(hit.position.x,hit.position.z).x<.55f)
            {error="Sólo se puede embarcar en playas de arena y muelles; las orillas verdes o rocosas no sirven.";return false;}
            if(!Gentle(hit.position))
            {error="Ese borde es demasiado escarpado para desembarcar.";return false;}
            if(!SeaNavigation.TryNearestOcean(hit.position,Ship.LoadRadius,out _))
            {error="La playa queda demasiado lejos del agua navegable.";return false;}
            landing=hit.position;return true;
        }

        public static bool IsWalkableLanding(Vector3 point) => MapLayout.IsLand(point.x,point.z)||IsDock(point);

        static bool IsDock(Vector3 point)
        {
            var naval=NavalWorld.Current;if(!naval)return false;
            foreach(var harbor in naval.Harbors)
            {
                if(!harbor||!harbor.CanLaunch)continue;
                var delta=point-harbor.Landing;
                if(delta.x*delta.x+delta.z*delta.z<=3.4f*3.4f&&Mathf.Abs(delta.y)<=1.5f)return true;
            }
            return false;
        }

        /// <returns>Sand and rock weights. Green is the remainder.</returns>
        public static Vector2 SurfaceWeights(float x,float z)
        {
            if(MapLayout.IsImported)
            {
                var data=MapLayout.Imported;
                int tile=ImportedMapData.GroundTileIndex(data.TileAt(x,z));
                bool sand=data.tileNames!=null&&tile<data.tileNames.Length&&data.tileNames[tile]=="Vcbp";
                return sand?Vector2.right:new Vector2(0,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.48f,.67f,Mathf.PerlinNoise(x*.027f+17,z*.027f+43))));
            }
            // These are authored landscape choices, not extra map-specific rules.
            // Bake the same weights into terrain vertices; commands read this field.
            float sandWeight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.44f,.60f,Mathf.PerlinNoise(x*.024f+19,z*.024f+57)));
            var ports=AuthoredPorts();
            for(int i=0;i<ports.Length;i++)sandWeight=Mathf.Max(sandWeight,NearPort(x,z,ports[i]));
            float rockWeight=(1-sandWeight)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.46f,.61f,Mathf.PerlinNoise(x*.031f+71,z*.031f+9)));
            return new Vector2(sandWeight,rockWeight);
        }
        static float NearPort(float x,float z,Vector3 port)
        {
            float dx=x-port.x,dz=z-port.z;
            return 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(7,12,Mathf.Sqrt(dx*dx+dz*dz)));
        }
        static bool Gentle(Vector3 point)
        {
            const float probe=.8f;
            for(int i=0;i<4;i++)
            {
                var offset=i==0?Vector3.right*probe:i==1?Vector3.left*probe:i==2?Vector3.forward*probe:Vector3.back*probe;
                if(!NavMesh.SamplePosition(point+offset,out var neighbor,1.25f,NavMesh.AllAreas)||
                    !MapLayout.IsLand(neighbor.position.x,neighbor.position.z)||Mathf.Abs(neighbor.position.y-point.y)>.7f)return false;
            }
            return true;
        }
    }
}
