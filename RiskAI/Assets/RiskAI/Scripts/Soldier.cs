using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;

namespace RiskAI
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class Soldier : CombatTarget
    {
        enum OrderMode { Idle, Move, AttackMove, Attack, Hold, Patrol, Follow }
        struct Order { public Vector3 Point; public OrderMode Mode; public Soldier Target; }
        public UnitKind Kind { get; private set; }
        public int OriginCountry { get; set; } = -1;
        public override float MaxHealth => BattleRules.Health(Kind);
        public override Vector3 AimPoint => transform.position + Vector3.up * 1.05f;
        public override AttackKind AttackType => BattleRules.Profile(Kind).Attack;
        public override ArmorKind ArmorType => BattleRules.Profile(Kind).Defense;
        public override float Armor => BattleRules.Profile(Kind).Armor;
        public bool Selected { get; private set; }
        public CityClaimZone Garrison { get; private set; }
        public bool IsGarrison => Garrison != null;
        public bool IsIdle => !IsGarrison && isActiveAndEnabled && mode == OrderMode.Idle && !target && Agent && Agent.enabled && !Agent.hasPath;
        public NavMeshAgent Agent { get; private set; }
        public CombatTarget CurrentTarget => target;
        // Presentation-only projection of the strike already scheduled by SimTick.
        // It does not schedule, cancel, or resolve combat.
        public float StrikeWindupProgress => strikeAt < 0 || !strikeTarget || !session ? -1 :
            Mathf.Clamp01(1-(strikeAt-session.BattleTime)/Mathf.Max(.001f,BattleRules.AttackPoint(Kind)));
        public bool IsHolding => !IsGarrison && isActiveAndEnabled && mode==OrderMode.Hold && !target && Agent && Agent.enabled;
        public string OrderLabel => IsGarrison ? "Guarnición · mantiene el edificio" : target ? "En combate" : mode == OrderMode.Move ? "Moviendo" : mode == OrderMode.AttackMove ? "Avanzando y atacando" : mode == OrderMode.Hold ? "Manteniendo posición" : mode == OrderMode.Patrol ? "Patrullando" : mode == OrderMode.Follow ? "Siguiendo" : "Preparado";
        public Transform LeftLeg, RightLeg, Weapon;
        internal BattleSession session;
        CombatTarget target, strikeTarget;
        Soldier followTarget;
        LineRenderer ring;
        SoldierAnimator visualAnimator;
        OrderMode mode;
        Vector3 destination, anchor, pursuitOrigin, patrolOrigin, garrisonAnchor;
        float nextSense, nextPath, nextAttack, strikeAt = -1, attackFlash, stalled;
        // These fields are observational only: direct player moves are timed from accepted command submit to first observed velocity.
        double humanMoveSubmittedAt = -1;
        double humanMovePausedSecondsAtSubmit;
        double humanMoveAppliedActiveSeconds, humanMoveRouteActiveSeconds = -1, humanMoveSpeedActiveSeconds = -1;
        Vector3 humanMoveDestination;
        bool humanMoveRouteResolved;
        float pathPendingSince = -1;
        int forestCellX=int.MinValue,forestCellZ=int.MinValue,forestRevision=-1;
        bool wasFighting, simulationPaused, stoppedBeforePause;
        float simDelta;
        MedicSupport medic;
        readonly List<CombatTarget> nearby = new List<CombatTarget>(64);
        readonly List<CombatTarget> alerted = new List<CombatTarget>(32);
        readonly Queue<Order> orders = new Queue<Order>();

        public void Initialize(BattleSession battle, int team, UnitKind kind)
        {
            ClearHumanMoveTelemetry(true);
            session=battle; Team=team; Kind=kind; Health=MaxHealth; OriginCountry=-1;
            Garrison=null; simulationPaused=false; enabled=true;
            anchor=destination=pursuitOrigin=patrolOrigin=transform.position;
            mode=OrderMode.Idle; target=strikeTarget=followTarget=null; orders.Clear();
            nextPath=nextAttack=attackFlash=stalled=0; strikeAt=-1; wasFighting=false;
            humanMoveSubmittedAt=-1; humanMoveRouteResolved=false; pathPendingSince=-1;
            bool first=!Agent;
            Agent=GetComponent<NavMeshAgent>(); Agent.enabled=true;
            // SourceGeometry holds verified W3U/SLK collision sizes. Height is
            // separate from rendered standing bounds: navigation clearance stays unchanged.
            Agent.radius=SourceGeometry.AgentRadius(kind); Agent.height=1.3f; Agent.speed=BattleRules.Speed(kind);
            Agent.acceleration=32; Agent.angularSpeed=540; Agent.stoppingDistance=.15f; Agent.autoBraking=true;
            Agent.updatePosition=true; Agent.updateRotation=true;
            Agent.obstacleAvoidanceType=ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            forestCellX=forestCellZ=int.MinValue;forestRevision=-1;
            UpdateTerrainSpeed();
            session.RegisterTarget(this);
            Agent.avoidancePriority=25+EntityId%32;
            if(Agent.isOnNavMesh){Agent.Warp(transform.position);Agent.ResetPath();Agent.isStopped=false;}
            transform.rotation=Quaternion.Euler(0,team==0?180:0,0);
            if(first)
            {
                var collider=gameObject.AddComponent<CapsuleCollider>();collider.radius=.4f;collider.height=1.5f;collider.center=Vector3.up*.7f;collider.isTrigger=true;
                VisualFactory.Soldier(this);
                if(kind==UnitKind.Medic)medic=gameObject.AddComponent<MedicSupport>();
                visualAnimator=GetComponent<SoldierAnimator>();
                ring=VisualFactory.Ring(transform,Mathf.Max(.43f,SourceGeometry.AgentRadius(kind)*1.15f),.045f,new Color(.5f,1f,.6f));
            }
            GetComponent<Collider>().enabled=true;
            if(medic)medic.Initialize(this,battle);
            if(visualAnimator)visualAnimator.ResetForReuse();
            Select(false);nextSense=session.BattleTime+(EntityId%4)*.05f;
        }
        public void SetSimulationPaused(bool paused)
        {
            if(simulationPaused==paused)return;
            simulationPaused=paused;
            if(Agent && Agent.enabled && Agent.isOnNavMesh)
            {
                if(paused){stoppedBeforePause=Agent.isStopped;Agent.isStopped=true;}
                else Agent.isStopped=stoppedBeforePause || IsGarrison;
            }
            if(visualAnimator)visualAnimator.SetPaused(paused);
        }
        internal bool BindGarrison(CityClaimZone zone)
        {
            if(zone==null || !IsAlive || IsGarrison && Garrison!=zone || !Agent || !Agent.isOnNavMesh)return false;
            if(!zone.TryGetGarrisonAnchor(out var point))return false;
            Stand(OrderMode.Hold);
            Agent.updatePosition=true; Agent.updateRotation=true;
            Agent.ResetPath(); Agent.isStopped=true;
            if(!Agent.Warp(point))return false;
            transform.position=point;
            anchor=garrisonAnchor=point;Garrison=zone;
            // A guard is an anchored combat target.  Letting its NavMeshAgent keep
            // writing the transform allows local avoidance to separate overlapping
            // agents, even though the guard has no path.
            Agent.obstacleAvoidanceType=ObstacleAvoidanceType.NoObstacleAvoidance;
            Agent.updatePosition=false; Agent.updateRotation=false;
            return true;
        }
        internal void ReleaseGarrison(CityClaimZone zone)
        {
            if(Garrison!=zone)return;
            Garrison=null; orders.Clear(); CancelStrike(); target=followTarget=null; mode=OrderMode.Idle;
            anchor=destination=transform.position; nextSense=0; wasFighting=false;
            if(!Agent || !Agent.enabled)return;
            Agent.updatePosition=true; Agent.updateRotation=true;
            Agent.obstacleAvoidanceType=ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            if(Agent.isOnNavMesh){Agent.Warp(transform.position);Agent.ResetPath();Agent.isStopped=false;}
        }

        void MaintainGarrisonAnchor()
        {
            if(!IsGarrison || !Agent || !Agent.enabled)return;
            Agent.isStopped=true;
            if((transform.position-garrisonAnchor).sqrMagnitude>.000001f)transform.position=garrisonAnchor;
            if(Agent.isOnNavMesh && (Agent.nextPosition-garrisonAnchor).sqrMagnitude>.000001f)Agent.Warp(garrisonAnchor);
        }

        public void Select(bool value) { Selected = value; if (ring) ring.enabled = value; }
        public string LastMoveError { get; private set; }
        internal bool PathPendingForTelemetry => Agent && Agent.enabled && Agent.isOnNavMesh && Agent.pathPending;
        internal float PathPendingAgeForTelemetry => pathPendingSince >= 0 && session != null ? Mathf.Max(0, session.BattleTime-pathPendingSince) : 0;
        // Only orders issued while the agent is fully stationary are eligible. This
        // avoids reporting old velocity as the response to a redirected movement.
        internal bool CanBeginHumanMoveTelemetry => Agent && Agent.enabled && Agent.isOnNavMesh &&
            !Agent.pathPending && !Agent.hasPath && Agent.velocity.sqrMagnitude < .0025f;
        internal void BeginHumanMoveTelemetry(double submittedAt, double pausedSecondsAtSubmit, bool eligible, Vector3 fallbackDestination)
        {
            ClearHumanMoveTelemetry(true);
            if (!eligible) return;
            humanMoveSubmittedAt = submittedAt;
            humanMovePausedSecondsAtSubmit = pausedSecondsAtSubmit;
            humanMoveDestination = Agent && Agent.enabled ? Agent.destination : fallbackDestination;
            humanMoveRouteResolved = false;
            humanMoveAppliedActiveSeconds = session.Commands.HumanMoveActiveSeconds(submittedAt, pausedSecondsAtSubmit);
            humanMoveRouteActiveSeconds = humanMoveSpeedActiveSeconds = -1;
            session.Commands.RecordHumanFirstMoveEligible();
        }
        void ClearHumanMoveTelemetry(bool cancelled)
        {
            if (humanMoveSubmittedAt >= 0 && session != null)
            {
                if (cancelled) session.Commands.RecordHumanFirstMoveCancelled();
                session.Commands.RecordHumanMoveEnded();
            }
            humanMoveSubmittedAt = -1;
            humanMoveRouteResolved = false;
        }
        void OnDisable() { ClearHumanMoveTelemetry(true); }
        public void MoveTo(Vector3 point, bool attackMove, bool append) => TryMoveTo(point,attackMove,append);
        public bool TryMoveTo(Vector3 point,bool attackMove,bool append) => Issue(point,attackMove?OrderMode.AttackMove:OrderMode.Move,append);
        public bool Patrol(Vector3 point, bool append) => Issue(point, OrderMode.Patrol, append);
        bool Issue(Vector3 point, OrderMode orderMode, bool append)
        {
            LastMoveError=null;
            if(IsGarrison){LastMoveError="El defensor está retenido en su círculo.";return false;}
            if(session.Paused||session.Winner>=0){LastMoveError="La partida está detenida.";return false;}
            if(!Agent||!Agent.enabled||!Agent.isOnNavMesh){LastMoveError="La unidad no está sobre terreno transitable.";return false;}
            if(!NavMesh.SamplePosition(point,out var hit,8,NavMesh.AllAreas)){LastMoveError="Ese destino no es transitable; usa un transporte para cruzar el agua.";return false;}
            var order = new Order { Point = hit.position, Mode = orderMode };
            if (append && mode != OrderMode.Idle && mode != OrderMode.Hold) { if (orders.Count < 35){orders.Enqueue(order);return true;}LastMoveError="La cola de órdenes está llena.";return false; }
            orders.Clear(); Apply(order);return LastMoveError==null;
        }
        void Apply(Order order)
        {
            ClearHumanMoveTelemetry(true);
            CancelStrike(); mode = order.Mode; destination = order.Point; patrolOrigin = anchor = transform.position;
            target = null; followTarget = order.Target; stalled = 0; nextSense = 0; wasFighting = false;
            ResumePath();
        }
        void ResumePath()
        {
            Agent.isStopped = false; Agent.stoppingDistance = .15f; nextPath = 0;
            if (mode == OrderMode.Move || mode == OrderMode.AttackMove || mode == OrderMode.Patrol)
            {if(!Agent.SetDestination(destination))LastMoveError="No se ha podido calcular la ruta a ese destino.";}
            else Agent.ResetPath();
        }
        // Autonomous behaviors run frequently.  Never discard an in-flight path for one of
        // their small destination adjustments, and retain a route that already reaches it.
        // Explicit player orders still go through ResumePath and intentionally pre-empt here.
        bool RequestAutonomousPath(Vector3 point)
        {
            if (!Agent || !Agent.enabled || !Agent.isOnNavMesh || Agent.pathPending) return false;
            var delta = Agent.destination - point;
            delta.y = 0;
            if (Agent.hasPath && delta.sqrMagnitude <= .1225f) return true;
            return Agent.SetDestination(point);
        }
        public void Attack(CombatTarget enemy)
        {
            if (IsGarrison || session.Paused || session.Winner>=0 || !enemy || enemy.Team == Team || !Agent.enabled || !Agent.isOnNavMesh) return;
            orders.Clear(); ClearHumanMoveTelemetry(true); CancelStrike(); mode = OrderMode.Attack; SetTarget(enemy); Agent.isStopped = false;
        }
        public void Follow(Soldier ally)
        {
            if (IsGarrison || session.Paused || session.Winner>=0 || !ally || ally == this || ally.Team != Team) return;
            orders.Clear(); ClearHumanMoveTelemetry(true); Apply(new Order { Mode = OrderMode.Follow, Target = ally });
        }
        public void Stop() => Stand(OrderMode.Idle);
        public void HoldPosition() => Stand(OrderMode.Hold);
        void Stand(OrderMode orderMode)
        {
            if(IsGarrison)return;
            orders.Clear(); ClearHumanMoveTelemetry(true); CancelStrike(); target = followTarget = null; mode = orderMode; anchor = transform.position; nextSense = 0; wasFighting = false;
            if (Agent && Agent.isOnNavMesh) { Agent.ResetPath(); Agent.isStopped = false; }
        }
        void Complete(bool failed=false)
        {
            if (orders.Count > 0) Apply(orders.Dequeue());
            else if (mode == OrderMode.Patrol && !failed) { var swap = destination; destination = patrolOrigin; patrolOrigin = swap; ResumePath(); }
            else Stop();
        }
        void SetTarget(CombatTarget enemy) { target = enemy; pursuitOrigin = transform.position; nextPath = 0; }
        void CancelStrike() { strikeAt = -1; strikeTarget = null; attackFlash = 0; }
        bool Visible(CombatTarget enemy)
        {
            if (!enemy) return false;
            if (BattleRules.Ranged(Kind))
            {
                Vector3 from = AimPoint, to = enemy.AimPoint, delta = to - from;
                return delta.sqrMagnitude < .001f || !Physics.Raycast(from, delta.normalized, delta.magnitude, 1 << MapLayout.TerrainLayer, QueryTriggerInteraction.Ignore);
            }
            return !NavMesh.Raycast(transform.position, enemy.ApproachPoint(transform.position), out _, NavMesh.AllAreas);
        }
        bool ValidTarget()
        {
            if (!target || !target.CanBeAttacked || target.Team == Team) return false;
            if (mode == OrderMode.Attack) return true;
            float leash = AutonomousLeash();
            var origin = mode == OrderMode.Idle || mode == OrderMode.Hold ? anchor : pursuitOrigin;
            return Vector3.Distance(target.transform.position, origin) <= leash;
        }
        float AutonomousLeash()
        {
            float existing = BattleRules.Ranged(Kind) ? BattleRules.Range(Kind) + 2 : Team == PlayerRules.NeutralTeam ? 7 : 11;
            return Mathf.Max(existing, SourceWeapons.AcquisitionRange(Kind));
        }
        void Acquire()
        {
            if (mode == OrderMode.Move || mode == OrderMode.Follow || mode == OrderMode.Attack) return;
            float sourceRadius = SourceWeapons.AcquisitionRange(Kind);
            float radius = sourceRadius > 0 ? sourceRadius : mode == OrderMode.Hold ? BattleRules.Range(Kind) : Team == PlayerRules.NeutralTeam ? 5 : 7.5f;
            CombatTarget best = null; float score = float.MaxValue;
            session.Spatial.Query(transform.position,radius+3,nearby);
            foreach (var enemy in nearby)
            {
                if (!enemy || enemy.Team == Team || !enemy.CanBeAttacked) continue;
                float distance = Vector3.Distance(transform.position, enemy.ApproachPoint(transform.position));
                if (distance > radius || !Visible(enemy)) continue;
                if ((mode == OrderMode.Idle || Team == PlayerRules.NeutralTeam) && Vector3.Distance(anchor, enemy.transform.position) > AutonomousLeash()) continue;
                int pressure = session.Spatial.Pressure(Team, enemy);
                float candidate = distance + (Kind == UnitKind.Footman ? pressure * .48f : pressure * .1f);
                if (candidate < score || candidate == score && (!best || enemy.EntityId < best.EntityId)) { best = enemy; score = candidate; }
            }
            if (best) SetTarget(best);
        }
        public void SimTick(float delta)
        {
            simDelta=delta;
            if (!session || session.Paused || session.Winner >= 0 || !IsAlive || !Agent || !Agent.enabled || !Agent.isOnNavMesh) return;
            UpdateTerrainSpeed();
            if (Agent.pathPending)
            {
                if (pathPendingSince < 0) pathPendingSince = session.BattleTime;
            }
            else pathPendingSince = -1;
            if (strikeAt >= 0 && session.BattleTime >= strikeAt)
            {
                if (strikeTarget && strikeTarget.Health > 0 && Vector3.Distance(transform.position, strikeTarget.ApproachPoint(transform.position)) <= BattleRules.Range(Kind) + .55f && Vector3.Distance(transform.position,strikeTarget.ApproachPoint(transform.position))>=BattleRules.MinimumRange(Kind) && Visible(strikeTarget))
                {
                    float damage = session.RollDamage(BattleRules.Profile(Kind));
                    if (BattleRules.Ranged(Kind)) session.Combat.FireWeapon(AimPoint, strikeTarget.AimPoint, strikeTarget, damage, Team, this, SourceWeapons.For(Kind, AttackType));
                    else strikeTarget.ReceiveAttack(damage, AttackType, Team, this);
                }
                strikeAt = -1; strikeTarget = null;
            }
            if (!ValidTarget())
            {
                target = null;
                if (wasFighting || mode == OrderMode.Attack)
                {
                    CancelStrike();
                    if (mode == OrderMode.Attack) Complete(); else ResumePath();
                    if (mode == OrderMode.Idle && Vector3.Distance(transform.position, anchor) > 1.5f) RequestAutonomousPath(anchor);
                }
            }
            if (!target && session.BattleTime >= nextSense) { nextSense = session.BattleTime + .2f; Acquire(); }
            if (target) Fight(); else Travel();
            wasFighting = target != null;
            if(IsGarrison)Agent.isStopped=true;
            if (humanMoveSubmittedAt >= 0)
            {
                double activeSeconds = session.Commands.HumanMoveActiveSeconds(humanMoveSubmittedAt, humanMovePausedSecondsAtSubmit);
                // First observed non-pending state; this does not timestamp the actual solver completion.
                if (!Agent.pathPending && !humanMoveRouteResolved)
                {
                    humanMoveRouteResolved = true;
                    humanMoveRouteActiveSeconds = activeSeconds;
                    session.Commands.RecordHumanRouteReady(activeSeconds - humanMoveAppliedActiveSeconds);
                }
                Vector3 toward = humanMoveDestination - transform.position;
                toward.y = 0;
                Vector3 velocity = Agent.velocity;
                velocity.y = 0;
                if (humanMoveSpeedActiveSeconds < 0 && velocity.sqrMagnitude > .04f)
                {
                    humanMoveSpeedActiveSeconds = activeSeconds;
                    session.Commands.RecordHumanSpeed(activeSeconds,
                        humanMoveRouteActiveSeconds >= 0 ? activeSeconds - humanMoveRouteActiveSeconds : -1);
                }
                if (humanMoveRouteResolved && velocity.sqrMagnitude > .04f &&
                    (toward.sqrMagnitude < .25f || Vector3.Dot(velocity, toward) > 0))
                {
                    session.Commands.RecordHumanSpeedToDirected(activeSeconds - humanMoveSpeedActiveSeconds);
                    session.Commands.RecordHumanFirstMotion(humanMoveSubmittedAt, humanMovePausedSecondsAtSubmit);
                    ClearHumanMoveTelemetry(false);
                }
            }
            if(medic)medic.SimTick(delta);
        }
        void UpdateTerrainSpeed()
        {
            if(!session||!Agent||!Agent.enabled)return;
            var canopies=session.Canopies;
            var cell=canopies.MovementCellAt(transform.position);
            if(cell.x==forestCellX&&cell.y==forestCellZ&&forestRevision==canopies.MovementRevision)return;
            forestCellX=cell.x;forestCellZ=cell.y;forestRevision=canopies.MovementRevision;
            // Forest drag is an own terrain adaptation. BattleRules.Speed remains
            // the source-derived base speed and guards retain their anchored state.
            Agent.speed=BattleRules.Speed(Kind)*canopies.MovementMultiplier(cell);
        }
        void LateUpdate()
        {
            MaintainGarrisonAnchor();
            if(!session || session.Paused || session.Winner>=0 || !IsAlive || !Agent || !Agent.enabled)return;
            float stride = Agent.velocity.magnitude > .15f ? Mathf.Sin(session.BattleTime * 13 + EntityId) * 30 : 0;
            if (LeftLeg) LeftLeg.localRotation = Quaternion.Euler(stride, 0, 0);
            if (RightLeg) RightLeg.localRotation = Quaternion.Euler(-stride, 0, 0);
            attackFlash = Mathf.MoveTowards(attackFlash, 0, Time.deltaTime);
            if (Weapon) Weapon.localRotation = Quaternion.Euler(-15 - Mathf.Sin(attackFlash * Mathf.PI / .4f) * 95, 0, 0);
        }
        void Fight()
        {
            float distance = Vector3.Distance(transform.position, target.ApproachPoint(transform.position));
            bool visible = Visible(target);
            if(distance<BattleRules.MinimumRange(Kind))
            {
                if(mode==OrderMode.Hold){target=null;Agent.isStopped=IsGarrison;return;}
                if(session.BattleTime>=nextPath)
                {
                    nextPath=session.BattleTime+.4f;var away=transform.position-target.transform.position;away.y=0;
                    if(away.sqrMagnitude<.01f)away=transform.forward;
                    var retreat=transform.position+away.normalized*(BattleRules.MinimumRange(Kind)+1.5f-distance);
                    if(NavMesh.SamplePosition(retreat,out var spot,3,NavMesh.AllAreas)){Agent.isStopped=false;Agent.stoppingDistance=.15f;RequestAutonomousPath(spot.position);}
                }
                return;
            }
            if (distance <= BattleRules.Range(Kind) && visible)
            {
                Agent.isStopped = true;
                Vector3 direction = target.transform.position - transform.position; direction.y = 0;
                if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 650 * simDelta);
                if (session.BattleTime >= nextAttack && strikeAt < 0)
                {
                    nextAttack = session.BattleTime + BattleRules.AttackInterval(Kind); strikeAt = session.BattleTime + BattleRules.AttackPoint(Kind);
                    strikeTarget = target; attackFlash = .4f;
                    if(visualAnimator)visualAnimator.Strike();
                }
            }
            else if (mode == OrderMode.Hold) { target = null; Agent.isStopped = false; }
            else if (strikeAt < 0 && session.BattleTime >= nextPath)
            {
                nextPath = session.BattleTime + .16f; Agent.isStopped = false;
                // End a clear ranged approach at the firing position itself.
                // A large stoppingDistance around the enemy is only a braking
                // radius: long NavMesh frames can still carry us deep inside it.
                var approach = target.ApproachPoint(transform.position);
                if (BattleRules.Ranged(Kind) && visible)
                {
                    float range = BattleRules.Range(Kind);
                    var probe = approach + (transform.position - approach).normalized * (range - .2f);
                    if (NavMesh.SamplePosition(probe, out var firing, .75f, NavMesh.AllAreas) &&
                        Vector3.Distance(firing.position, approach) <= range &&
                        Vector3.Distance(firing.position, approach) >= BattleRules.MinimumRange(Kind) &&
                        !NavMesh.Raycast(transform.position, firing.position, out _, NavMesh.AllAreas) &&
                        !Physics.Linecast(firing.position + Vector3.up * 1.05f, target.AimPoint,
                            1 << MapLayout.TerrainLayer, QueryTriggerInteraction.Ignore))
                    {
                        Agent.stoppingDistance = .05f;
                        RequestAutonomousPath(firing.position);
                        return;
                    }
                }
                // An obstructed firing position still requires following the
                // target's path to find a reachable point with line of sight.
                Agent.stoppingDistance = Mathf.Max(.15f, BattleRules.Ranged(Kind) && visible
                    ? BattleRules.Range(Kind) - .12f : BattleRules.Range(Kind) * .76f);
                RequestAutonomousPath(approach);
            }
        }
        void Travel()
        {
            if(IsGarrison){Agent.isStopped=true;return;}
            Agent.isStopped = false;
            if (mode == OrderMode.Follow)
            {
                if (!followTarget) { Complete(); return; }
                if (session.BattleTime >= nextPath) { nextPath = session.BattleTime + .2f; Agent.stoppingDistance = 2; RequestAutonomousPath(followTarget.transform.position); }
                if (followTarget.CurrentTarget && Vector3.Distance(transform.position, followTarget.CurrentTarget.transform.position) < 9) SetTarget(followTarget.CurrentTarget);
                return;
            }
            if (mode == OrderMode.Move || mode == OrderMode.AttackMove || mode == OrderMode.Patrol)
            {
                if (Agent.pathPending) return;
                float distance = Vector3.Distance(transform.position, destination);
                stalled = Agent.velocity.sqrMagnitude < .04f ? stalled + simDelta : 0;
                if(Agent.pathStatus!=NavMeshPathStatus.PathComplete && distance>.65f)
                {
                    if(LastMoveError==null)
                    {
                        LastMoveError="No hay un camino terrestre hasta ese punto. Elige otro destino o usa un transporte.";
                        if(Team==0){session.Message(LastMoveError);Debug.Log("RISKAI_ROUTE_BLOCKED: id="+EntityId+" tick="+session.Clock.TickCount+" destination="+destination);}
                    }
                    // Follow the reachable segment once. Re-requesting an exhausted partial
                    // path keeps pathPending cycling and never lets a stalled timer complete.
                    if(!Agent.hasPath || Agent.remainingDistance<.4f || stalled>1.2f)Complete(true);
                    return;
                }
                if (distance < .65f || (Agent.pathStatus==NavMeshPathStatus.PathComplete && ((Agent.hasPath && Agent.remainingDistance < .4f) || (stalled > 1.2f && distance < 3)))) Complete();
                else if (!Agent.hasPath && session.BattleTime >= nextPath) { Agent.stoppingDistance = .15f; Agent.SetDestination(destination); nextPath = session.BattleTime + .5f; }
            }
        }
        public float Heal(float amount)
        {
            if(!IsAlive||amount<=0||float.IsNaN(amount)||float.IsInfinity(amount))return 0;
            float before=Health;Health=Mathf.Min(MaxHealth,Health+amount);return Health-before;
        }
        public void DestroyEmbarked(int attacker)
        {
            if(Health<=0)return;ClearHumanMoveTelemetry(true);Health=0;
            if(PlayerRules.IsPlayer(attacker)&&attacker<session.PlayerCount){session.Kills[attacker]++;session.Economy.GrantBounty(attacker,BattleRules.PointValue(Kind));}
            Garrison=null;session.Units.Remove(this);session.UnregisterTarget(this);
            session.SoldierPool.Retire(this,0);
        }
        public override void TakeDamage(float damage, int attacker, CombatTarget source = null)
        {
            if (Health <= 0) return;
            if (damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage) || attacker == Team) return;
            Health = Mathf.Max(0, Health - damage);
            if (Health <= 0)
            {
                ClearHumanMoveTelemetry(true);
                if (PlayerRules.IsPlayer(attacker) && attacker < session.PlayerCount) { session.Kills[attacker]++; session.Economy.GrantBounty(attacker,BattleRules.PointValue(Kind)); }
                Garrison=null;session.Units.Remove(this);session.UnregisterTarget(this);Select(false);Agent.enabled=false;GetComponent<Collider>().enabled=false;enabled=false;
                if(visualAnimator)visualAnimator.Die();
                session.SoldierPool.Retire(this,visualAnimator?1.4f:0);
                return;
            }
            if (source && mode != OrderMode.Move && mode != OrderMode.Hold && mode != OrderMode.Follow && !target)
                SetTarget(source);
            if(source)
            {
                session.Spatial.Query(transform.position,5,alerted);
                foreach(var candidate in alerted)
                    if(candidate is Soldier ally && ally.Team==Team && ally.IsIdle && (transform.position-ally.transform.position).sqrMagnitude<25)
                        ally.SetTarget(source);
            }
        }
    }
}
