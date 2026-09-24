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
        // A transport carries ten; the difficulty profile decides how much of it a wave fills.
        const int TroopCapacity = 10;
        const int SourceProbeBudget = 4;
        const int TroopRouteProbeBudget = 12;
        const int TargetProbeBudget = 6;
        const float TroopSearchRadius = 40f;
        const float BoardingGraceSeconds = 8f;
        const float LandingThreatRadius = 16f;
        const float BerthThreatRadius = 24f;
        readonly NavalWorld world;
        readonly BattleSession session;
        readonly PlayerBuildingCommands buildingCommands;
        readonly int team;
        // This is the session-owned reservation. NavalWorld only queries its
        // handful of commanders, so it cannot survive into a later match.
        readonly List<Soldier> troops = new List<Soldier>(TroopCapacity);
        readonly List<Soldier> scratchTroops = new List<Soldier>(TroopCapacity);
        readonly List<CombatTarget> nearbyTargets = new List<CombatTarget>(32);
        struct TargetCandidate { public Settlement Town; public Harbor Harbor; }
        struct TargetPair : System.IEquatable<TargetPair>
        {
            readonly Harbor source,destination;
            public TargetPair(Harbor embark,Harbor landing){source=embark;destination=landing;}
            public bool Equals(TargetPair other) => source==other.source&&destination==other.destination;
            public override bool Equals(object value) => value is TargetPair other&&Equals(other);
            public override int GetHashCode()
            {
                unchecked{return (source?source.GetInstanceID():0)*397^(destination?destination.GetInstanceID():0);}
            }
        }
        sealed class TownDistanceComparer : IComparer<Settlement>
        {
            public Vector3 Origin;
            public int Compare(Settlement left,Settlement right)
            {
                int compare=DistanceXZ(left.ClaimPoint,Origin).CompareTo(DistanceXZ(right.ClaimPoint,Origin));
                return compare!=0?compare:string.CompareOrdinal(left.State.Id,right.State.Id);
            }
        }
        readonly List<TargetCandidate> targetCandidates = new List<TargetCandidate>(TargetProbeBudget);
        // The town frontier deliberately retains every eligible town. Route work
        // remains bounded by targetCandidates, while its pair cursor can reach a
        // farther town after nearer ones fail.
        readonly List<Settlement> targetTownFrontier = new List<Settlement>(64);
        readonly TownDistanceComparer townDistanceComparer = new TownDistanceComparer();
        readonly HashSet<Harbor> attemptedSources = new HashSet<Harbor>();
        readonly HashSet<Harbor> examinedSources = new HashSet<Harbor>();
        readonly Dictionary<Harbor,int> targetHarborCursors = new Dictionary<Harbor,int>();
        readonly Dictionary<TargetPair,int> targetTownCursors = new Dictionary<TargetPair,int>();
        readonly List<Soldier> landed = new List<Soldier>(TroopCapacity);
        readonly NavMeshPath landPath = new NavMeshPath();
        SkirmishCommander landCommander;
        Phase phase;
        Harbor source, destination, returnHarbor;
        Vector3 sourceLanding, sourceTransportBerth, destinationTransportBerth;
        Settlement target;
        Ship transport;
        float nextDecision, phaseDeadline, retryAt;
        float plannedSeaDistance, plannedGatherDistance, boardingDeadline;
        int sourceHarborCursor, troopCursor, recoveryHarborCursor, recoveryPass;
        bool embarkOrdersIssued, sailOrderIssued, sailConfirmed, attackOrderIssued;
        int sailCommandId;

        public bool IsActive => phase != Phase.Planning && phase != Phase.Cooldown;
        int MaximumTroops => Mathf.Clamp(session.AiProfile.ExpeditionTroops, MinimumTroops, TroopCapacity);

        public NavalExpeditionCommander(NavalWorld naval,int player)
        {
            world=naval;session=naval.Session;team=player;buildingCommands=new PlayerBuildingCommands(session);
            // Spread the bounded route probes over the one-second cadence instead
            // of making every AI team calculate paths on the same simulation tick.
            nextDecision=(player-1)*DecisionSeconds/Mathf.Max(1,session.PlayerCount-1);
        }

        public bool Reserves(Soldier soldier) => phase != Phase.WaitingForTransport && soldier && troops.Contains(soldier);

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
            transport=null;
            // Avoid land-route probes when neither a paid replacement nor any
            // empty transport can possibly start an expedition. An empty
            // disconnected transport must not veto a local purchase.
            bool canBuyTransport=CanFundTransportPurchase();
            if(!canBuyTransport&&!HasEligibleEmptyTransport()){Defer();return;}
            if(!TryChooseSourceAndTroops(canBuyTransport,out source,out transport))
            {
                if(SourcePassComplete()){attemptedSources.Clear();examinedSources.Clear();}
                ClearPlan();retryAt=session.BattleTime+NoPlanRetrySeconds;return;
            }
            if(!TryChooseTarget(source,out target,out destination))
            {
                attemptedSources.Add(source);Defer();return;
            }
            if(!transport)
            {
                // Recheck mutable cheap conditions immediately before the normal
                // paid harbor command.
                if(!CanFundTransportPurchase()||!CanQueueTransportAt(source)){Defer();return;}
                if(buildingCommands.Execute(team,PlayerBuildingIntent.Recruit(source.BuildingId,UnitKind.Transport))!=null){Fail();return;}
                phase=Phase.WaitingForTransport;phaseDeadline=session.BattleTime+UnitCatalog.Get(UnitKind.Transport).TrainSeconds+PhaseTimeout;return;
            }
            BeginGathering();
        }

        void WaitForTransport()
        {
            // Training does not immobilize the land army. Recheck the available
            // squad when a boat in this source's sea component is ready.
            if(!source||source.Owner!=team||!source.TryTransportLanding(out sourceLanding,out sourceTransportBerth)){Fail();return;}
            transport=FindCompatibleTransport(sourceTransportBerth);
            if(!transport)return;
            CollectTroops(sourceLanding,troops);
            if(troops.Count<MinimumTroops){Fail();return;}
            plannedGatherDistance=0;
            for(int i=0;i<troops.Count;i++)plannedGatherDistance+=DistanceXZ(troops[i].transform.position,sourceLanding);
            plannedGatherDistance/=troops.Count;
            BeginGathering();
        }

        void BeginGathering()
        {
            if(!transport||!source||source.Owner!=team||!source.TryTransportLanding(out sourceLanding,out sourceTransportBerth)){Fail();return;}
            if(DistanceXZ(transport.transform.position,sourceLanding)>UnitCatalog.TransportLoadRadius&&!transport.IsAtOrRoutingTo(sourceTransportBerth))
            {
                var sailed=session.Commands.SubmitResult(team,transport.EntityId,UnitCommandKind.Move,sourceTransportBerth.x,sourceTransportBerth.y,sourceTransportBerth.z);
                if(!sailed.Accepted){Fail();return;}
            }
            embarkOrdersIssued=false;phase=Phase.Gathering;phaseDeadline=session.BattleTime+GatherDeadline();
        }

        void Gather()
        {
            if(!transport||!transport.IsAlive||!source||source.Owner!=team||!source.TryTransportLanding(out sourceLanding,out sourceTransportBerth)){Fail();return;}
            if(DistanceXZ(transport.transform.position,sourceLanding)>UnitCatalog.TransportLoadRadius)
            {
                if(!transport.IsAtOrRoutingTo(sourceTransportBerth))
                {
                    var sailed=session.Commands.SubmitResult(team,transport.EntityId,UnitCommandKind.Move,sourceTransportBerth.x,sourceTransportBerth.y,sourceTransportBerth.z);
                    if(!sailed.Accepted){Fail();return;}
                }
                return;
            }
            if(!embarkOrdersIssued)
            {
                for(int i=0;i<troops.Count;i++)
                {
                    var soldier=troops[i];if(!Eligible(soldier))continue;
                    if(!world.TryOrderEmbarkAt(transport,soldier,source,out _)){Fail();return;}
                }
                // Wait briefly for the whole squad instead of sailing with the first two aboard.
                embarkOrdersIssued=true;boardingDeadline=session.BattleTime+BoardingGraceSeconds;return;
            }
            int viable=transport.CargoCount;
            for(int i=0;i<troops.Count;i++)
            {
                var soldier=troops[i];
                if(!Eligible(soldier)||transport.CargoCount>=transport.Type.Transport.Capacity)continue;
                viable++;
                if(DistanceXZ(transport.transform.position,soldier.transform.position)<=UnitCatalog.TransportLoadRadius)transport.TryEmbark(soldier);
            }
            // Claim simulation may legitimately bind a unit that is waiting on a
            // harbor circle. Do not hold the only mission slot until timeout when
            // fewer than a legal wave remain able to board.
            if(viable<MinimumTroops){Fail();return;}
            if(transport.CargoCount>=MinimumTroops&&(transport.CargoCount>=viable||transport.CargoCount>=transport.Type.Transport.Capacity||session.BattleTime>=boardingDeadline))
            {
                // Only people actually aboard can be part of an overseas order.
                // A full four-unit candidate list may legally leave with two cargo.
                scratchTroops.Clear();scratchTroops.AddRange(troops);troops.Clear();
                for(int i=0;i<transport.Cargo.Count;i++)if(transport.Cargo[i])troops.Add(transport.Cargo[i]);
                for(int i=0;i<scratchTroops.Count;i++)if(!troops.Contains(scratchTroops[i])&&Eligible(scratchTroops[i]))scratchTroops[i].Stop();
                phase=Phase.Sailing;phaseDeadline=session.BattleTime+SailingDeadline();
            }
        }

        void Sail()
        {
            if(!transport||!transport.IsAlive||!destination||!destination.TryTransportLanding(out _,out destinationTransportBerth)){Fail();return;}
            if(!sailOrderIssued)
            {
                var submitted=world.SubmitDisembark(transport,destination);
                if(submitted.CommandId==0||!submitted.Accepted){Fail();return;}
                sailCommandId=submitted.CommandId;sailConfirmed=false;sailOrderIssued=true;
                if(session.AiProfile.NavalEscort)OrderEscort();
                return;
            }
            if(!sailConfirmed)
            {
                sailConfirmed=true;
                if(!session.Commands.TryGetResult(sailCommandId,out var stored)||!stored.Accepted){sailOrderIssued=false;Fail();return;}
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
                if(!DisembarkAccepted(world.SubmitDisembark(transport,returnHarbor))){phase=Phase.Cooldown;retryAt=session.BattleTime+RetrySeconds;return;}
                phaseDeadline=session.BattleTime+ReturnDeadline();
            }
        }

        bool TryChooseSourceAndTroops(bool canBuyTransport,out Harbor selected,out Ship selectedTransport)
        {
            selected=null;selectedTransport=null;if(world.Harbors.Count==0)return false;
            float best=float.MaxValue;int probes=0;int count=world.Harbors.Count;
            int start=PositiveModulo(sourceHarborCursor,count);sourceHarborCursor=(start+1)%count;
            for(int offset=0;offset<count&&probes<SourceProbeBudget;offset++)
            {
                var harbor=world.Harbors[(start+offset)%count];
                if(!harbor||harbor.Owner!=team)continue;
                examinedSources.Add(harbor);
                if(attemptedSources.Contains(harbor))continue;
                probes++;
                if(!harbor.TryTransportLanding(out var landing,out var berth))continue;
                // Pair the source with the nearest empty transport in its sea
                // component. Otherwise it needs a legal paid local replacement.
                Ship sourceTransport=FindCompatibleTransport(berth);
                if(!sourceTransport&&(!canBuyTransport||!CanQueueTransportAt(harbor)))continue;
                CollectTroops(landing,scratchTroops);
                if(scratchTroops.Count<MinimumTroops)continue;
                float score=0;for(int i=0;i<scratchTroops.Count;i++)score+=DistanceXZ(scratchTroops[i].transform.position,landing);
                if(score>=best)continue;
                best=score;selected=harbor;selectedTransport=sourceTransport;sourceLanding=landing;sourceTransportBerth=berth;troops.Clear();troops.AddRange(scratchTroops);
            }
            if(selected)plannedGatherDistance=best/Mathf.Max(1,troops.Count);
            return selected;
        }

        bool SourcePassComplete()
        {
            foreach(var harbor in world.Harbors)if(harbor&&harbor.Owner==team&&!examinedSources.Contains(harbor))return false;
            return true;
        }

        void CollectTroops(Vector3 landing,List<Soldier> result)
        {
            result.Clear();if(session.Units.Count==0)return;
            int probes=0,maximum=MaximumTroops;
            // Nearest idle troops first; the roster cursor below fills any remainder.
            session.Spatial.Query(landing,TroopSearchRadius,nearbyTargets);
            SortByDistance(nearbyTargets,landing);
            for(int i=0;i<nearbyTargets.Count&&probes<TroopRouteProbeBudget&&result.Count<maximum;i++)
            {
                var soldier=nearbyTargets[i] as Soldier;
                if(!Eligible(soldier)||(!soldier.IsIdle&&!soldier.IsHolding)||DistanceXZ(soldier.transform.position,landing)>TroopSearchRadius||LandCommitted(soldier))continue;
                probes++;
                if(CanWalk(soldier.transform.position,landing))result.Add(soldier);
            }
            if(result.Count>=maximum)return;
            int count=session.Units.Count,start=PositiveModulo(troopCursor,count);
            troopCursor=(start+TroopRouteProbeBudget)%count;
            for(int offset=0;offset<count&&probes<TroopRouteProbeBudget;offset++)
            {
                var soldier=session.Units[(start+offset)%count];
                if(!Eligible(soldier)||(!soldier.IsIdle&&!soldier.IsHolding)||result.Contains(soldier)||LandCommitted(soldier))continue;
                probes++;
                if(CanWalk(soldier.transform.position,landing))result.Add(soldier);
                if(result.Count>=maximum)break;
            }
        }

        bool TryChooseTarget(Harbor embark,out Settlement selected,out Harbor landing)
        {
            selected=null;landing=null;plannedSeaDistance=0;targetCandidates.Clear();
            if(world.Harbors.Count==0)return false;
            int count=world.Harbors.Count;
            int start=targetHarborCursors.TryGetValue(embark,out var cursor)?PositiveModulo(cursor,count):0;
            int nextCursor=start,probes=0;
            // Harbor locations are the bounded frontier, so a mission never
            // treats an arbitrary coast pixel as an embarkable destination.
            for(int offset=0;offset<count&&probes<TargetProbeBudget;offset++)
            {
                int index=(start+offset)%count;nextCursor=(index+1)%count;
                var harbor=world.Harbors[index];
                if(!harbor||harbor==embark||!harbor.CanLaunch)continue;
                probes++;
                // Advance at discovery, before any route check, so each revisit
                // tries the next nearest capturable town for this harbor pair.
                Settlement town=NextTargetTown(embark,harbor);
                if(town)InsertTargetCandidate(town,harbor,embark.Landing);
            }
            targetHarborCursors[embark]=nextCursor;
            for(int i=0;i<targetCandidates.Count;i++)
            {
                var candidate=targetCandidates[i];var town=candidate.Town;var harbor=candidate.Harbor;
                if(CanWalk(sourceLanding,town.ClaimPoint))continue;
                if(!harbor||!harbor.TryTransportLanding(out var landingPoint,out var transportBerth))continue;
                // Skip beaches held by a mobile army or covered by enemy warships.
                if(troops.Count>0&&LandingThreat(landingPoint,transportBerth)>TroopPower())continue;
                if(!CanWalk(landingPoint,town.ClaimPoint)||!SeaNavigation.TryBuildPath(sourceTransportBerth,transportBerth,out var seaPath))continue;
                selected=town;landing=harbor;destinationTransportBerth=transportBerth;plannedSeaDistance=PathDistance(sourceTransportBerth,seaPath,transportBerth);return true;
            }
            return false;
        }

        Settlement NextTargetTown(Harbor embark,Harbor destination)
        {
            targetTownFrontier.Clear();townDistanceComparer.Origin=destination.Landing;
            foreach(var town in session.Towns)
            {
                if(town&&town.State.Owner!=team)targetTownFrontier.Add(town);
            }
            targetTownFrontier.Sort(townDistanceComparer);
            if(targetTownFrontier.Count==0)return null;
            var pair=new TargetPair(embark,destination);
            int cursor=targetTownCursors.TryGetValue(pair,out var saved)?PositiveModulo(saved,targetTownFrontier.Count):0;
            targetTownCursors[pair]=(cursor+1)%targetTownFrontier.Count;
            return targetTownFrontier[cursor];
        }

        void InsertTargetCandidate(Settlement town,Harbor harbor,Vector3 origin)
        {
            float distance=(town.ClaimPoint-origin).sqrMagnitude;int insert=targetCandidates.Count;
            for(int i=0;i<targetCandidates.Count;i++)if(distance<(targetCandidates[i].Town.ClaimPoint-origin).sqrMagnitude){insert=i;break;}
            if(insert>=TargetProbeBudget)return;
            targetCandidates.Insert(insert,new TargetCandidate{Town=town,Harbor=harbor});
            if(targetCandidates.Count>TargetProbeBudget)targetCandidates.RemoveAt(targetCandidates.Count-1);
        }

        Harbor NearestRecoveryHarbor(Vector3 point)
        {
            if(world.Harbors.Count==0)return null;
            int count=world.Harbors.Count,probes=0;
            // Both the pass and the next unprobed index survive the budget. A
            // large set of unreachable owned docks must not starve other docks.
            while(recoveryPass<2)
            {
                while(recoveryHarborCursor<count)
                {
                    var harbor=world.Harbors[recoveryHarborCursor];
                    if(!harbor||(recoveryPass==0?harbor.Owner!=team:harbor.Owner==team)){recoveryHarborCursor++;continue;}
                    if(probes>=TargetProbeBudget)return null;
                    probes++;recoveryHarborCursor++;
                    if(!harbor.TryTransportLanding(out _,out var berth))continue;
                    if(SeaNavigation.TryBuildPath(point,berth,out _))
                    {
                        recoveryHarborCursor=0;recoveryPass=0;return harbor;
                    }
                }
                recoveryHarborCursor=0;recoveryPass++;
            }
            recoveryPass=0;
            return null;
        }

        // Do not strip a land wave that is assembling at its staging point.
        bool LandCommitted(Soldier soldier)
        {
            if(landCommander==null&&session.Commanders!=null)
                for(int i=0;i<session.Commanders.Count;i++)if(session.Commanders[i].Team==team)landCommander=session.Commanders[i];
            return landCommander!=null&&landCommander.IsCommitted(soldier);
        }

        float TroopPower()
        {
            float power=0;
            for(int i=0;i<troops.Count;i++)if(troops[i])power+=AiUnitAnalysis.For(troops[i].Kind).Value;
            return power;
        }

        float LandingThreat(Vector3 landing,Vector3 berth)
        {
            float threat=0;
            session.Spatial.Query(landing,LandingThreatRadius,nearbyTargets);
            for(int i=0;i<nearbyTargets.Count;i++)
                if(nearbyTargets[i] is Soldier soldier&&soldier.IsAlive&&!soldier.IsGarrison&&soldier.Team!=team&&PlayerRules.IsPlayer(soldier.Team)&&
                   DistanceXZ(soldier.transform.position,landing)<=LandingThreatRadius)threat+=AiUnitAnalysis.For(soldier.Kind).Value;
            foreach(var ship in world.Ships)
                if(ship&&ship.IsAlive&&ship.Team!=team&&PlayerRules.IsPlayer(ship.Team)&&ship.Type.CanAttack&&DistanceXZ(ship.transform.position,berth)<=BerthThreatRadius)
                    threat+=AiUnitAnalysis.ShipValue(ship.Type)*1.5f;
            return threat;
        }

        void OrderEscort()
        {
            Ship best=null;float distance=float.MaxValue;
            foreach(var ship in world.Ships)
            {
                if(!ship||!ship.IsAlive||ship.Team!=team||!ship.Type.CanAttack||ship.IsGarrison||ship.CurrentTarget)continue;
                float next=DistanceXZ(ship.transform.position,transport.transform.position);
                if(next<distance&&SeaNavigation.AreConnected(ship.transform.position,destinationTransportBerth)){distance=next;best=ship;}
            }
            if(best&&!best.IsAtOrRoutingTo(destinationTransportBerth))session.Commands.Submit(new UnitCommand(team,best.EntityId,UnitCommandKind.AttackMove,destinationTransportBerth.x,destinationTransportBerth.y,destinationTransportBerth.z));
        }

        static void SortByDistance(List<CombatTarget> targets,Vector3 point)
        {
            for(int i=1;i<targets.Count;i++)
            {
                var target=targets[i];float distance=target?DistanceXZ(target.transform.position,point):float.MaxValue;int j=i-1;
                while(j>=0&&(targets[j]?DistanceXZ(targets[j].transform.position,point):float.MaxValue)>distance){targets[j+1]=targets[j];j--;}
                targets[j+1]=target;
            }
        }

        bool RecoverLoadedTransport()
        {
            foreach(var ship in world.Ships)
            {
                if(!ship||!ship.IsAlive||ship.Team!=team||!ship.Type.CanTransport||ship.CargoCount==0)continue;
                transport=ship;
                returnHarbor=NearestRecoveryHarbor(ship.transform.position);
                if(!returnHarbor){retryAt=session.BattleTime+RetrySeconds;return true;}
                if(!DisembarkAccepted(world.SubmitDisembark(ship,returnHarbor))){retryAt=session.BattleTime+RetrySeconds;return true;}
                phase=Phase.ReturningCargo;phaseDeadline=session.BattleTime+ReturnDeadline();
                return true;
            }
            return false;
        }

        bool CanFundTransportPurchase()
        {
            int transportCost=UnitCatalog.Get(UnitKind.Transport).Cost;
            return session.Economy.Gold[team]>=transportCost+world.FirstFleetSavingsTargetFor(team)&&TeamNavalCount()<Harbor.FleetCapacity;
        }
        bool CanQueueTransportAt(Harbor harbor) => harbor&&harbor.Owner==team&&harbor.QueueCount==0;
        int TeamNavalCount()
        {
            int count=world.PendingShips(team);
            foreach(var ship in world.Ships)if(ship&&ship.IsAlive&&ship.Team==team)count++;
            return count;
        }
        bool HasEligibleEmptyTransport()
        {
            foreach(var ship in world.Ships)
                if(ship&&ship.IsAlive&&ship.Team==team&&ship.Type.CanTransport&&!ship.IsGarrison&&ship.CargoCount==0)return true;
            return false;
        }
        Ship FindCompatibleTransport(Vector3 berth)
        {
            Ship best=null;float distance=float.MaxValue;
            foreach(var ship in world.Ships)
            {
                if(!ship||!ship.IsAlive||ship.Team!=team||!ship.Type.CanTransport||ship.IsGarrison||ship.CargoCount!=0||!SeaNavigation.AreConnected(ship.transform.position,berth))continue;
                float next=DistanceXZ(ship.transform.position,berth);
                if(next<distance){distance=next;best=ship;}
            }
            return best;
        }

        float GatherDeadline()
        {
            float troopSpeed=3f;
            for(int i=0;i<troops.Count;i++)if(troops[i]&&troops[i].Agent)troopSpeed=Mathf.Min(troopSpeed,Mathf.Max(.5f,troops[i].Agent.speed));
            float boatTravel=transport?transport.RemainingRouteDistance/Mathf.Max(.5f,transport.Speed):0;
            return Mathf.Clamp(boatTravel*1.6f+plannedGatherDistance/troopSpeed*1.75f+16f,30f,240f);
        }
        float SailingDeadline() => Mathf.Clamp(plannedSeaDistance/Mathf.Max(.5f,transport.Speed)*1.6f+20f,40f,240f);
        float ReturnDeadline()
        {
            if(!transport)return PhaseTimeout;
            // The route is applied on the next command drain. Until then the straight
            // line is enough for the deadline, and it does not search again.
            float distance=transport.RemainingRouteDistance;
            if(distance<1f&&returnHarbor)distance=DistanceXZ(transport.transform.position,returnHarbor.Berth);
            return Mathf.Clamp(distance/Mathf.Max(.5f,transport.Speed)*1.6f+20f,40f,240f);
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
                returnHarbor=retreat;
                if(DisembarkAccepted(world.SubmitDisembark(transport,returnHarbor)))
                {
                    phase=Phase.ReturningCargo;phaseDeadline=session.BattleTime+ReturnDeadline();return;
                }
            }
            attemptedSources.Clear();examinedSources.Clear();transport=null;phase=Phase.Cooldown;retryAt=session.BattleTime+RetrySeconds;
        }
        void Reset(){attemptedSources.Clear();examinedSources.Clear();transport=null;phase=Phase.Planning;retryAt=session.BattleTime+RetrySeconds;ClearPlan();}
        void Defer(){retryAt=session.BattleTime+RetrySeconds;ClearPlan();}
        void ClearPlan()
        {
            source=null;destination=null;target=null;returnHarbor=null;sourceLanding=sourceTransportBerth=destinationTransportBerth=default;troops.Clear();plannedSeaDistance=plannedGatherDistance=boardingDeadline=0;
            embarkOrdersIssued=sailOrderIssued=sailConfirmed=attackOrderIssued=false;sailCommandId=0;
        }
        static int PositiveModulo(int value,int divisor) => divisor<=0?0:(value%divisor+divisor)%divisor;
        static float DistanceXZ(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
        bool DisembarkAccepted(CommandResult submitted) =>
            submitted.CommandId != 0 && session.Commands.TryGetResult(submitted.CommandId, out var stored) && stored.Accepted;
    }
}
