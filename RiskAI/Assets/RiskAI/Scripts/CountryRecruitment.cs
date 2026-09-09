using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    /// <summary>JASS country credits: ceil(cities/2) per round, one recruit every 500 ms.</summary>
    public sealed class CountryRecruitment
    {
        readonly BattleSession session;
        readonly int[] credits,cityCounts,alivePoints,sequence;
        float elapsed;
        // Only the explicitly launched, isolated navigation benchmark sets this.
        internal bool SuspendedForProbe { get; set; }
        public CountryRecruitment(BattleSession battle)
        {
            session=battle;int count=MapLayout.Countries.Length;
            credits=new int[count];cityCounts=new int[count];alivePoints=new int[count];sequence=new int[count];
            foreach(var city in MapLayout.Towns)if(city.Country>=0&&city.Country<count)cityCounts[city.Country]++;
        }
        public int Pending(int country)=>country>=0&&country<credits.Length?credits[country]:0;
        void CountAlive()
        {
            System.Array.Clear(alivePoints,0,alivePoints.Length);
            foreach(var unit in session.Units)
                if(unit&&unit.IsAlive&&unit.OriginCountry>=0&&unit.OriginCountry<alivePoints.Length)
                    alivePoints[unit.OriginCountry]+=BattleRules.PointValue(unit.Kind);
        }
        public void CreditRound()
        {
            if (SuspendedForProbe) return;
            CountAlive();
            for(int country=0;country<credits.Length;country++)
                if(session.Economy.CountryOwner(country)>=0&&alivePoints[country]<cityCounts[country]*BattleRules.CountryReinforcementPointCapPerCity)
                    credits[country]+=MapLayout.Countries[country].PerTurn;
        }
        public void Tick(float delta)
        {
            if (SuspendedForProbe) return;
            elapsed+=delta;if(elapsed+.0001f<BattleRules.CountryReinforcementStepSeconds)return;elapsed-=BattleRules.CountryReinforcementStepSeconds;
            CountAlive();
            for(int country=0;country<credits.Length;country++)
            {
                int owner=session.Economy.CountryOwner(country);
                if(country<session.Camps.Count&&session.Camps[country])session.Camps[country].ReconcileOwner(owner);
                if(credits[country]<=0)continue;
                if(owner<0||country>=session.Camps.Count||!session.Camps[country])continue;
                // The prototype's mobile cap is a local limit, so defer credits while it is full.
                if(session.RecruitmentReservations(owner)>=BattleRules.PopulationLimit)continue;
                credits[country]--;
                if(alivePoints[country]>=cityCounts[country]*BattleRules.CountryReinforcementPointCapPerCity)continue;
                var camp=session.Camps[country];int index=sequence[country]++;
                float angle=index*2.399963f,radius=.65f+Mathf.Sqrt(index%12)*.55f;
                var spot=camp.SpawnPoint+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
                var unit=session.Spawn(owner,MapLayout.Countries[country].Reinforcement,spot,country);
                if(unit)camp.ApplyRally(unit);
            }
        }
    }
}
