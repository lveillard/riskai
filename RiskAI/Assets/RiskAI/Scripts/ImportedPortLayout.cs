using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// Adapts source-authored amphibious port posts without moving their city or
    /// Circle of Power coordinates. Warcraft supplies pathing under those water
    /// posts; RiskAI supplies the equivalent walkable quay.
    /// </summary>
    public static class ImportedPortLayout
    {
        public const float WalkwayWidth = 3.4f;
        public const float StableLandRadius = 1.35f;

        public enum PierShape { Direct, CliffRamp }

        public readonly struct Anchors
        {
            public readonly Vector3 City, Claim, Shore, Building, Tower, Seaward;
            public readonly PierShape Shape;
            public Anchors(Vector3 city,Vector3 claim,Vector3 shore,Vector3 building,Vector3 tower,Vector3 seaward,PierShape shape)
            { City=city;Claim=claim;Shore=shore;Building=building;Tower=tower;Seaward=seaward;Shape=shape; }
        }

        public static Anchors Resolve(Vector3 sourceCity,Vector3 sourceClaim)
        {
            Vector3 sourceDirection=sourceClaim-sourceCity;sourceDirection.y=0;
            if(sourceDirection.sqrMagnitude<.01f)sourceDirection=Vector3.forward;
            else sourceDirection.Normalize();

            Vector3 shore=NearestLand(sourceCity,-sourceDirection,true);
            Vector3 seaward=sourceCity-shore;seaward.y=0;
            if(seaward.sqrMagnitude<.01f)seaward=sourceDirection;else seaward.Normalize();
            Vector3 side=new Vector3(-seaward.z,0,seaward.x);

            // Keep the central shore-to-circle lane clear. The reusable harbor
            // house and tower occupy opposite land shoulders beside that lane.
            Vector3 building=BestLandShoulder(shore,seaward,side,1);
            Vector3 tower=BestTowerShoulder(shore,seaward,side,sourceClaim);
            float rise=Mathf.Abs(shore.y-sourceCity.y);
            PierShape shape=rise>.75f?PierShape.CliffRamp:PierShape.Direct;
            return new Anchors(sourceCity,sourceClaim,shore,building,tower,seaward,shape);
        }

        static Vector3 NearestLand(Vector3 origin,Vector3 preferred,bool requireClearance)
        {
            Vector3 best=origin;float bestScore=float.MaxValue;
            const int directions=64;
            for(float distance=.5f;distance<=16f;distance+=.5f)
            for(int i=0;i<directions;i++)
            {
                float angle=i*Mathf.PI*2/directions;
                Vector3 direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                Vector3 candidate=origin+direction*distance;
                if(!MapLayout.IsLand(candidate.x,candidate.z)||requireClearance&&!HasLandClearance(candidate,StableLandRadius))continue;
                float score=distance-Vector3.Dot(direction,preferred)*.2f;
                if(score>=bestScore)continue;
                best=MapLayout.Point(candidate.x,candidate.z);bestScore=score;
            }
            if(bestScore<float.MaxValue)return best;
            return requireClearance?NearestLand(origin,preferred,false):MapLayout.Point(origin.x,origin.z);
        }

        static bool HasLandClearance(Vector3 point,float radius)
        {
            if(!MapLayout.IsLand(point.x,point.z))return false;
            for(int i=0;i<8;i++)
            {
                float angle=i*Mathf.PI*.25f;
                if(!MapLayout.IsLand(point.x+Mathf.Cos(angle)*radius,point.z+Mathf.Sin(angle)*radius))return false;
            }
            return true;
        }

        static Vector3 BestLandShoulder(Vector3 shore,Vector3 seaward,Vector3 side,float sign)
        {
            Vector3 seed=shore-seaward*.6f+side*(2.4f*sign);
            if(HasLandClearance(seed,1.2f))return MapLayout.Point(seed.x,seed.z);
            return NearestLand(seed,-seaward,true);
        }

        static Vector3 BestTowerShoulder(Vector3 shore,Vector3 seaward,Vector3 side,Vector3 ownClaim)
        {
            Vector3 seed=shore-seaward*1.7f-side*3.4f;
            Vector3 best=seed;float bestScore=float.MaxValue;
            const float clearance=14f;
            for(float radius=0;radius<=20f;radius+=.5f)
            for(int i=0;i<48;i++)
            {
                float angle=i*Mathf.PI/24f;
                Vector3 candidate=seed+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
                if(!HasLandClearance(candidate,.8f)||!ClearOfForeignClaims(candidate,ownClaim,clearance))continue;
                float score=radius+Mathf.Max(0,Vector3.Dot(candidate-shore,seaward))*.5f;
                if(score>=bestScore)continue;
                best=MapLayout.Point(candidate.x,candidate.z);bestScore=score;
            }
            return bestScore<float.MaxValue?best:NearestLand(seed,-seaward,true);
        }

        static bool ClearOfForeignClaims(Vector3 point,Vector3 ownClaim,float clearance)
        {
            float clearanceSquared=clearance*clearance;
            foreach(var city in MapLayout.Towns)
            {
                Vector3 claim=city.ClaimPoint;
                if((claim-ownClaim).sqrMagnitude<.01f)continue;
                Vector3 offset=claim-point;offset.y=0;
                if(offset.sqrMagnitude<clearanceSquared)return false;
            }
            return true;
        }

    }
}
