using UnityEngine;

namespace RiskAI
{
    /// <summary>Derives a compact land/sea layout from imported coastal terrain.</summary>
    public static class ImportedPortLayout
    {
        public const float ClaimDistanceFromCoast = 2.8f;
        public const float BuildingSeawardOffset = .65f;

        public readonly struct Anchors
        {
            public readonly Vector3 Claim, Building, Seaward;
            public Anchors(Vector3 claim,Vector3 building,Vector3 seaward)
            { Claim=claim;Building=building;Seaward=seaward; }
        }

        public static Anchors Resolve(Vector3 city,Vector3 sourceClaim)
        {
            // Source claim anchors vary: most sit inland, while a smaller set is
            // authored on a wet quay. Use that classification only as a tie-breaker;
            // the radial terrain search below remains authoritative.
            Vector3 fallback=MapLayout.IsLand(sourceClaim.x,sourceClaim.z)?city-sourceClaim:sourceClaim-city;fallback.y=0;
            if(fallback.sqrMagnitude<.01f)fallback=Vector3.forward;
            else fallback.Normalize();
            Vector3 seaward=NearestWaterDirection(city,fallback);
            Vector3 claim=NearCoastClaim(city,sourceClaim);
            Vector3 building=city+seaward*BuildingSeawardOffset;building.y=city.y;
            return new Anchors(claim,building,seaward);
        }

        public static Vector3 TowerPoint(Vector3 city,Vector3 claim,Vector3 seaward)
        {
            Vector3 landward=-seaward;Vector3 side=new Vector3(-seaward.z,0,seaward.x);
            for(int attempt=0;attempt<6;attempt++)
            {
                float sign=(attempt&1)==0?1:-1;
                float lateral=3.2f+(attempt/2)*.7f;
                Vector3 point=city+landward*1.8f+side*(lateral*sign);
                if(MapLayout.IsLand(point.x,point.z))return MapLayout.Point(point.x,point.z);
            }
            return MapLayout.Point(claim.x,claim.z);
        }

        static Vector3 NearestWaterDirection(Vector3 city,Vector3 fallback)
        {
            Vector3 best=fallback;float bestDistance=float.MaxValue;
            const int directions=32;
            for(int i=0;i<directions;i++)
            {
                float angle=i*Mathf.PI*2/directions;
                Vector3 direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                for(float distance=.5f;distance<=10f;distance+=.5f)
                {
                    Vector3 point=city+direction*distance;
                    if(MapLayout.IsLand(point.x,point.z))continue;
                    if(distance<bestDistance-.001f || Mathf.Abs(distance-bestDistance)<.001f && Vector3.Dot(direction,fallback)>Vector3.Dot(best,fallback))
                    {best=direction;bestDistance=distance;}
                    break;
                }
            }
            return best.normalized;
        }

        static Vector3 NearCoastClaim(Vector3 city,Vector3 sourceClaim)
        {
            Vector3 offset=sourceClaim-city;offset.y=0;
            float distance=offset.magnitude;
            if(distance<.01f)return sourceClaim;
            // Preserve the source-authored approach direction while shortening its
            // common six-metre offset. The source endpoint is already protected by
            // the imported coast mesh and the shortened segment remains on that quay.
            Vector3 claim=city+offset/distance*Mathf.Min(ClaimDistanceFromCoast,distance);
            claim.y=Mathf.Lerp(city.y,sourceClaim.y,Mathf.Min(1,ClaimDistanceFromCoast/distance));
            return claim;
        }
    }
}
