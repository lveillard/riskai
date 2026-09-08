using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;

namespace RiskAI
{
    /// <summary>One bounded AI transport mission using only the normal naval order APIs.</summary>
    public sealed class NavalExpeditionCommander
    {
        enum Phase { Planning, WaitingForTransport, Gathering, Sailing, Landing, Attacking, ReturningCargo, Cooldown }
        const float DecisionSeconds = 1f;
        const float PhaseTimeout = 45f;
        const float NoPlanRetrySeconds = 6f;
        const float RetrySeconds = 10f;
        const int MinimumTroops = 2;
        const int MaximumTroops = 4;
        const int SourceProbeBudget = 4;
        const int TroopRouteProbeBudget = 8;
        const int TargetProbeBudget = 6;
        readonly NavalWorld world;
        readonly BattleSession session;
        readonly PlayerBuildingCommands buildingCommands;
        readonly int team;
        // This is the session-owned reservation. NavalWorld only queries its
        // handful of commanders, so it cannot survive into a later match.
        readonly List<Soldier> troops = new List<Soldier>(MaximumTroops);
        readonly List<Soldier> scratchTroops = new List<Soldier>(MaximumTroops);
        readonly List<Settlement> targetCandidates = new List<Settlement>(TargetProbeBudget);
        readonly List<Soldier> landed = new List<Soldier>(MaximumTroops);
        readonly NavMeshPath landPath = new NavMeshPath();
        Phase phase;
        Harbor source, destination, returnHarbor;
        Vector3 sourceLanding, sourceTransportBerth, destinationTransportBerth;
        Settlement target;
        Ship transport;
        float nextDecision, phaseDeadline, retryAt;
        float plannedSeaDistance, plannedGatherDistance;
        int sourceHarborCursor, troopCursor, targetHarborCursor, recoveryHarborCursor;
        bool embarkOrdersIssued, sailOrderIssued, attackOrderIssued;

        public bool IsActive => phase != Phase.Planning && phase != Phase.Cooldown;

        public NavalExpeditionCommander(NavalWorld naval,int player)
        {
            world=naval;session=naval.Session;team=player;buildingCommands=new PlayerBuildingCommands(session);
            // Spread the bounded route probes over the one-second cadence instead
            // of making every AI team calculate paths on the same simulation tick.
            nextDecision=(player-1)*DecisionSeconds/Mathf.Max(1,session.PlayerCount-1);
        }

        public bool Reserves(Soldier soldier) => soldier && troops.Contains(soldier);

        public void Tick(float delta)
        {
            if(!session.AiEnabled||session.Paused||session.Winner>=0||session.BattleTime<nextDecision)return;
            nextDecision=session.BattleTime+DecisionSeconds;
            if(phase!=Phase.Cooldown&&phase!=Phase.Planning&&session.BattleTime>phaseDeadline){Fail();return;}
            switch(phase)
            {
                case Phase.Planning: Plan(); break;
                case Phase.WaitingForTransport: WaitForTransport(); break;
                case Phase.Gathering: Gather(); break;
                case Phase.Sailing: Sail(); break;
                case Phase.Landing: Land(); break;
                case Phase.Attacking: Attack(); break;
                case Phase.ReturningCargo: ReturnCargo(); break;
                case Phase.Cooldown: if(session.BattleTime>=retryAt){phase=Phase.Planning;ClearPlan();} break;
            }
        }

        void Plan()
        {
            if(session.BattleTime<session.AiFirstNavalOffensiveTime||session.BattleTime<retryAt)return;
            if(RecoverLoadedTransport())return;
            transport=FindTransport();
            if(!transport)
            {
                // Route probes cannot make an expedition viable while the only
                // possible next step is a transport purchase the team cannot fund.
                // Keep the existing retry cadence and first-galley reserve.
                if(HasAnyTransport()){Defer();return;}
                int transportCost=Harbor.Cost(ShipKind.Transport);
                int firstFleetReserve=world.FirstFleetSavingsTargetFor(team);
                if(session.Economy.Gold[team]<transportCost+firstFleetReserve){Defer();return;}
            }
            if(!TryChooseSourceAndTroops(out source))
            {
                ClearPlan();retryAt=session.BattleTime+NoPlanRetrySeconds;return;
            }
            // This source cannot create a transport while it is already training.
            // Do not spend target/sea route searches until its queue is available.
            if(!transport&&source.QueueCount>0){Defer();return;}
            if(!TryChooseTarget(source,out target,out destination)){Defer();return;}
            if(!transport)
            {
                int transportCost=Harbor.Cost(ShipKind.Transport);
                int firstFleetReserve=world.FirstFleetSavingsTargetFor(team);
                // Recheck the state used for the early gate immediately before purchase.
                if(HasAnyTransport()||source.QueueCount>0||session.Economy.Gold[team]<transportCost+firstFleetReserve){Defer();return;}
                if(buildingCommands.Execute(team,PlayerBuildingIntent.BuyShip(source.BuildingId,NavalUnitKind.Transport))!=null){Fail();return;}
                phase=Phase.WaitingForTransport;phaseDeadline=session.BattleTime+Harbor.TrainTime(ShipKind.Transport)+PhaseTimeout;return;
            }
            BeginGathering();
        }

        void WaitForTransport()
        {
            transport=FindTransport();
            if(transport)BeginGathering();
        }

        void BeginGathering()
        {
            if(!transport||!source||source.Owner!=team||!source.TryTransportLanding(out sourceLanding,out sourceTransportBerth)){Fail();return;}
            if(DistanceXZ(transport.transform.position,sourceLanding)>Ship.LoadRadius&&!transport.IsAtOrRoutingTo(sourceTransportBerth))transport.MoveTo(sourceTransportBerth);
            if(!string.IsNullOrEmpty(transport.LastActionError)){Fail();return;}
            embarkOrdersIssued=false;phase=Phase.Gathering;phaseDeadline=session.BattleTime+GatherDeadline();
        }

        void Gather()
        {
            if(!transport||!transport.IsAlive||!source||source.Owner!=team||!source.TryTransportLanding(out sourceLanding,out sourceTransportBerth)){Fail();return;}
            if(DistanceXZ(transport.transform.position,sourceLanding)>Ship.LoadRadius)
            {
                if(!transport.IsAtOrRoutingTo(sourceTransportBerth))transport.MoveTo(sourceTransportBerth);
                if(!string.IsNullOrEmpty(transport.LastActionError)){Fail();return;}
                return;
            }
            if(!embarkOrdersIssued)
            {
                for(int i=0;i<troops.Count;i++)
                {
                    var soldier=troops[i];if(!Eligible(soldier))continue;
                    if(!world.TryOrderEmbarkAt(transport,soldier,source,out _)){Fail();return;}
                }
                embarkOrdersIssued=true;return;
            }
            int viable=transport.CargoCount;
            for(int i=0;i<troops.Count;i++)
            {
                var soldier=troops[i];
                if(!Eligible(soldier)||transport.CargoCount>=transport.Profile.Capacity)continue;
                viable++;
                if(DistanceXZ(transport.transform.position,soldier.transform.position)<=Ship.LoadRadius)transport.TryEmbark(soldier);
            }
            // Claim simulation may legitimately bind a unit that is waiting on a
            // harbor circle. Do not hold the only mission slot until timeout when
            // fewer than a legal wave remain able to board.
            if(viable<MinimumTroops){Fail();return;}
            if(transport.CargoCount>=MinimumTroops){phase=Phase.Sailing;phaseDeadline=session.BattleTime+SailingDeadline();}
        }

        void Sail()
        {
            if(!transport||!transport.IsAlive||!destination||!destination.TryTransportLanding(out _,out destinationTransportBerth)){Fail();return;}
            if(!sailOrderIssued)
            {
                world.OrderDisembark(transport,destination);
                if(!string.IsNullOrEmpty(transport.LastActionError)){Fail();return;}
                sailOrderIssued=true;return;
            }
            if(transport.CargoCount==0)
            {
                phase=Phase.Landing;phaseDeadline=session.BattleTime+PhaseTimeout;
            }
        }

        void Land()
        {
            if(!transport||!transport.IsAlive||!target||target.State.Owner==team){Fail();return;}
            if(transport.CargoCount>0)return; // Ship.SimTick performs the queued harbor unload.
            landed.Clear();
            for(int i=0;i<troops.Count;i++)if(Eligible(troops[i]))landed.Add(troops[i]);
            if(landed.Count<MinimumTroops){Fail();return;}
            BattleSession.GiveFormation(landed,target.ClaimPoint,true,false);
            attackOrderIssued=true;phase=Phase.Attacking;phaseDeadline=session.BattleTime+PhaseTimeout;
        }

        void Attack()
        {
            if(!target||target.State.Owner==team){Reset();return;}
            if(!attackOrderIssued){Fail();return;}
            for(int i=0;i<troops.Count;i++)if(Eligible(troops[i]))return;
            // A dead landing wave has no order left to protect. Release it now
            // rather than reserving the team until the attack timeout expires.
            Reset();
        }

        void ReturnCargo()
        {
            if(!transport||!transport.IsAlive){transport=null;phase=Phase.Cooldown;retryAt=session.BattleTime+RetrySeconds;return;}
            if(transport.CargoCount==0){transport=null;phase=Phase.Cooldown;retryAt=session.BattleTime+RetrySeconds;return;}
            if(!returnHarbor||!returnHarbor.TryTransportLanding(out _,out var returnBerth)||!transport.IsAtOrRoutingTo(returnBerth))
            {
                returnHarbor=NearestRecoveryHarbor(transport.transform.position);
                if(!returnHarbor){phase=Phase.Cooldown;retryAt=session.BattleTime+RetrySeconds;return;}
                world.OrderDisembark(transport,returnHarbor);
                if(!string.IsNullOrEmpty(transport.LastActionError)){phase=Phase.Cooldown;retryAt=session.BattleTime+RetrySeconds;return;}
                phaseDeadline=session.BattleTime+ReturnDeadline(returnHarbor);
            }
        }

        bool TryChooseSourceAndTroops(out Harbor selected)
        {
            selected=null;if(world.Harbors.Count==0)return false;
            float best=float.MaxValue;int probes=0;int count=world.Harbors.Count;
            int start=PositiveModulo(sourceHarborCursor,count);sourceHarborCursor=(start+1)%count;
            for(int offset=0;offset<count&&probes<SourceProbeBudget;offset++)
            {
                var harbor=world.Harbors[(start+offset)%count];
                if(!harbor||harbor.Owner!=team||!harbor.TryTransportLanding(out var landing,out var berth))continue;
                probes++;CollectTroops(landing,scratchTroops);
                if(scratchTroops.Count<MinimumTroops)continue;
                float score=0;for(int i=0;i<scratchTroops.Count;i++)score+=DistanceXZ(scratchTroops[i].transform.position,landing);
                if(score>=best)continue;
                best=score;selected=harbor;sourceLanding=landing;sourceTransportBerth=berth;troops.Clear();troops.AddRange(scratchTroops);
            }
            if(selected)plannedGatherDistance=best/Mathf.Max(1,troops.Count);
            return selected;
        }

        void CollectTroops(Vector3 landing,List<Soldier> result)
        {
            result.Clear();if(session.Units.Count==0)return;
            int probes=0,count=session.Units.Count,start=PositiveModulo(troopCursor,count);
            troopCursor=(start+TroopRouteProbeBudget)%count;
            for(int offset=0;offset<count&&probes<TroopRouteProbeBudget;offset++)
            {
                var soldier=session.Units[(start+offset)%count];
                if(!Eligible(soldier)||(!soldier.IsIdle&&!soldier.IsHolding))continue;
                probes++;
                if(CanWalk(soldier.transform.position,landing))result.Add(soldier);
                if(result.Count>=MaximumTroops)break;
            }
        }

        bool TryChooseTarget(Harbor embark,out Settlement selected,out Harbor landing)
        {
            selected=null;landing=null;plannedSeaDistance=0;targetCandidates.Clear();
            if(world.Harbors.Count==0)return false;
            int count=world.Harbors.Count,start=PositiveModulo(targetHarborCursor,count),probes=0;
            targetHarborCursor=(start+1)%count;
            // Harbor locations are the bounded frontier, so a mission never
            // treats an arbitrary coast pixel as an embarkable destination.
            for(int offset=0;offset<count&&probes<TargetProbeBudget;offset++)
            {
                var harbor=world.Harbors[(start+offset)%count];
                if(!harbor||harbor==embark||!harbor.CanLaunch)continue;
                probes++;
                Settlement town=NearestCapturableTown(harbor.Landing);
                if(town&&!targetCandidates.Contains(town))InsertNearestCandidate(town,embark.Landing);
            }
            for(int i=0;i<targetCandidates.Count;i++)
            {
                var town=targetCandidates[i];
                if(CanWalk(sourceLanding,town.ClaimPoint))continue;
                var harbor=NearestLandingHarbor(town.ClaimPoint,embark);
                if(!harbor||!harbor.TryTransportLanding(out var landingPoint,out var transportBerth)||!CanWalk(landingPoint,town.ClaimPoint)||!SeaNavigation.TryBuildPath(sourceTransportBerth,transportBerth,out var seaPath))continue;
                selected=town;landing=harbor;destinationTransportBerth=transportBerth;plannedSeaDistance=PathDistance(sourceTransportBerth,seaPath,transportBerth);return true;
            }
            return false;
        }

        Settlement NearestCapturableTown(Vector3 point)
        {
            Settlement best=null;float distance=float.MaxValue;
            foreach(var town in session.Towns)
            {
                if(!town||town.State.Owner==team)continue;
                float next=DistanceXZ(town.ClaimPoint,point);if(next<distance){distance=next;best=town;}
            }
            return best;
        }

        void InsertNearestCandidate(Settlement town,Vector3 origin)
        {
            float distance=(town.ClaimPoint-origin).sqrMagnitude;int insert=targetCandidates.Count;
            for(int i=0;i<targetCandidates.Count;i++)if(distance<(targetCandidates[i].ClaimPoint-origin).sqrMagnitude){insert=i;break;}
            if(insert>=TargetProbeBudget)return;
            targetCandidates.Insert(insert,town);if(targetCandidates.Count>TargetProbeBudget)targetCandidates.RemoveAt(targetCandidates.Count-1);
        }

        Harbor NearestLandingHarbor(Vector3 point,Harbor excluded)
        {
            Harbor best=null;float distance=float.MaxValue;
            foreach(var harbor in world.Harbors)
            {
                if(!harbor||harbor==excluded||!harbor.TryTransportLanding(out var landing,out _))continue;
                float next=DistanceXZ(landing,point);if(next<distance){distance=next;best=harbor;}
            }
            return best;
        }

        Harbor NearestRecoveryHarbor(Vector3 point)
        {
            if(world.Harbors.Count==0)return null;
            int count=world.Harbors.Count,start=PositiveModulo(recoveryHarborCursor,count),probes=0;
            // Owned docks first, then any valid dock. Continue across decisions
            // rather than running an unbounded sea A* for every port on the map.
            for(int pass=0;pass<2;pass++)for(int offset=0;offset<count;offset++)
            {
                int index=(start+offset)%count;
                var harbor=world.Harbors[index];
                if(!harbor||(pass==0?harbor.Owner!=team:harbor.Owner==team)||
                    !harbor.TryTransportLanding(out _,out var berth))continue;
                recoveryHarborCursor=(index+1)%count;
                if(++probes>TargetProbeBudget)return null;
                if(SeaNavigation.TryBuildPath(point,berth,out _))return harbor;
            }
            return null;
        }

        bool RecoverLoadedTransport()
        {
            foreach(var ship in world.Ships)
            {
                if(!ship||!ship.IsAlive||ship.Team!=team||ship.Kind!=ShipKind.Transport||ship.CargoCount==0)continue;
                transport=ship;
                returnHarbor=NearestRecoveryHarbor(ship.transform.position);
                if(!returnHarbor){retryAt=session.BattleTime+RetrySeconds;return true;}
                world.OrderDisembark(ship,returnHarbor);
                if(!string.IsNullOrEmpty(ship.LastActionError)){retryAt=session.BattleTime+RetrySeconds;return true;}
                phase=Phase.ReturningCargo;phaseDeadline=session.BattleTime+ReturnDeadline(returnHarbor);
                return true;
            }
            return false;
        }

        Ship FindTransport()
        {
            foreach(var ship in world.Ships)if(ship&&ship.IsAlive&&ship.Team==team&&ship.Kind==ShipKind.Transport&&!ship.IsGarrison&&ship.CargoCount==0)return ship;
            return null;
        }

        bool HasAnyTransport()
        {
            foreach(var ship in world.Ships)if(ship&&ship.IsAlive&&ship.Team==team&&ship.Kind==ShipKind.Transport)return true;
            return false;
        }

        float GatherDeadline()
        {
            float troopSpeed=3f;
            for(int i=0;i<troops.Count;i++)if(troops[i]&&troops[i].Agent)troopSpeed=Mathf.Min(troopSpeed,Mathf.Max(.5f,troops[i].Agent.speed));
            return Mathf.Clamp(plannedGatherDistance/troopSpeed*1.75f+16f,30f,90f);
        }
        float SailingDeadline() => Mathf.Clamp(plannedSeaDistance/Mathf.Max(.5f,transport.Speed)*1.6f+20f,40f,240f);
        float ReturnDeadline(Harbor harbor)
        {
            if(!transport||!harbor||!harbor.TryTransportLanding(out _,out var berth)||!SeaNavigation.TryBuildPath(transport.transform.position,berth,out var route))return PhaseTimeout;
            return Mathf.Clamp(PathDistance(transport.transform.position,route,berth)/Mathf.Max(.5f,transport.Speed)*1.6f+20f,40f,240f);
        }
        static float PathDistance(Vector3 start,IReadOnlyList<Vector3> route,Vector3 end)
        {
            float distance=0;Vector3 previous=start;
            for(int i=0;i<route.Count;i++){distance+=DistanceXZ(previous,route[i]);previous=route[i];}
            return distance+DistanceXZ(previous,end);
        }
        bool CanWalk(Vector3 from,Vector3 to) => NavMesh.CalculatePath(from,to,NavMesh.AllAreas,landPath)&&landPath.status==NavMeshPathStatus.PathComplete;
        bool Eligible(Soldier soldier) => soldier&&soldier.IsAlive&&!soldier.IsGarrison&&soldier.Team==team&&soldier.isActiveAndEnabled&&soldier.Agent&&soldier.Agent.enabled&&soldier.Agent.isOnNavMesh;

        void Fail()
        {
            var retreat=transport&&transport.IsAlive&&transport.CargoCount>0?NearestRecoveryHarbor(transport.transform.position):null;
            ClearPlan();
            if(retreat&&transport&&transport.IsAlive&&transport.CargoCount>0)
            {
                returnHarbor=retreat;world.OrderDisembark(transport,returnHarbor);
                if(string.IsNullOrEmpty(transport.LastActionError))
                {
                    phase=Phase.ReturningCargo;phaseDeadline=session.BattleTime+ReturnDeadline(returnHarbor);return;
                }
            }
            transport=null;phase=Phase.Cooldown;retryAt=session.BattleTime+RetrySeconds;
        }
        void Reset(){transport=null;phase=Phase.Planning;retryAt=session.BattleTime+RetrySeconds;ClearPlan();}
        void Defer(){retryAt=session.BattleTime+RetrySeconds;ClearPlan();}
        void ClearPlan()
        {
            source=null;destination=null;target=null;returnHarbor=null;sourceLanding=sourceTransportBerth=destinationTransportBerth=default;troops.Clear();plannedSeaDistance=plannedGatherDistance=0;
            embarkOrdersIssued=sailOrderIssued=attackOrderIssued=false;
        }
        static int PositiveModulo(int value,int divisor) => divisor<=0?0:(value%divisor+divisor)%divisor;
        static float DistanceXZ(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
    }
}
