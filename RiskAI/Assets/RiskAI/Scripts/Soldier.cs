using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.AI;

namespace RiskAI
{
    /// <summary>Test seam for the move-order path budget. Armed only by PlayMode tests.</summary>
    public static class SoldierPathBudget
    {
        public static bool Armed;
        public static int SetDestination, ResetPath, Stopped, Sampled, Calculated;
        public static void Arm()
        {
            Armed = true;
            SetDestination = ResetPath = Stopped = Sampled = Calculated = 0;
        }
        public static void NoteSetDestination() { if (Armed) SetDestination++; }
        public static void NoteResetPath() { if (Armed) ResetPath++; }
        public static void NoteStopped() { if (Armed) Stopped++; }
        public static void NoteSample() { if (Armed) Sampled++; }
        public static void NoteCalculated() { if (Armed) Calculated++; }
    }

    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class Soldier : CombatTarget, IOrderable, IPostClaimant, IQueuedOrderRunner
    {
        enum OrderMode { Idle, Move, AttackMove, Attack, Hold, Patrol, Follow, Capture, Embark }
        public UnitKind Kind { get; private set; }
        public override ref readonly UnitType Type => ref UnitCatalog.Get(Kind);
        public int OriginCountry { get; set; } = -1;
        public override float MaxHealth => Type.MaxHealth;
        public override Vector3 AimPoint => transform.position + Vector3.up * 1.05f;
        public override AttackKind AttackType => Type.AttackType;
        public override ArmorKind ArmorType => Type.ArmorType;
        public override float Armor => Type.Armor;
        public bool Selected { get; private set; }
        public CityClaimZone Garrison { get; private set; }
        public bool IsGarrison => Garrison != null;
        public bool IsIdle => !IsGarrison && isActiveAndEnabled && mode == OrderMode.Idle && !target && Agent && Agent.enabled && !Agent.hasPath;
        public NavMeshAgent Agent { get; private set; }
        public CombatTarget CurrentTarget => target;
        // Presentation-only projection of the strike already scheduled by SimTick.
        // It does not schedule, cancel, or resolve combat.
        public float StrikeWindupProgress => strikeAt < 0 || !strikeTarget || !session ? -1 :
            Mathf.Clamp01(1-(strikeAt-session.BattleTime)/Mathf.Max(.001f,Type.Weapon.AttackPoint));
        // Observational animation phase only; damage remains owned by SimTick.
        public float AttackPresentationProgress
        {
            get
            {
                if (!session || attackPresentationStartedAt < 0) return -1;
                if (attackPresentationContactTick == session.Clock.TickCount)
                    return Type.AttackContact;
                float elapsed = session.BattleTime - attackPresentationStartedAt;
                float duration = attackPresentationAttackPoint + attackPresentationRecovery;
                if (elapsed < 0 || elapsed > duration) return -1;
                return AttackPresentationTiming.NormalizedTime(elapsed,
                    attackPresentationAttackPoint, attackPresentationRecovery,
                    Type.AttackContact);
            }
        }
        public long LastAttackContactTick => attackPresentationContactTick;
        public bool IsHolding => !IsGarrison && isActiveAndEnabled && mode==OrderMode.Hold && !target && Agent && Agent.enabled;
        public string OrderLabel => IsGarrison ? "Guarnición · mantiene el edificio" : target ? "En combate" : mode == OrderMode.Move || mode == OrderMode.Embark ? "Moviendo" : mode == OrderMode.AttackMove || mode == OrderMode.Capture ? "Avanzando y atacando" : mode == OrderMode.Hold ? "Manteniendo posición" : mode == OrderMode.Patrol ? "Patrullando" : mode == OrderMode.Follow ? "Siguiendo" : "Preparado";
        public Transform LeftLeg, RightLeg, Weapon;
        internal BattleSession session;
        CombatTarget target, strikeTarget;
        int followTargetId;
        LineRenderer ring;
        SoldierAnimator visualAnimator;
        UnitPresentationLodView presentationLod;
        OrderMode mode;
        Vector3 destination, anchor, pursuitOrigin, patrolOrigin, garrisonAnchor;
        float nextSense, nextPath, nextAttack, strikeAt = -1, stalled;
        float attackPresentationStartedAt = -1, attackPresentationAttackPoint, attackPresentationRecovery;
        long attackPresentationContactTick = -1;
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
        RoarSupport roar;
        float roarUntil = -1, roarBonus;
        /// <summary>Aroa buff: the caster's rolled-damage bonus until the source duration ends.</summary>
        public bool IsRoaring => session && roarUntil > session.BattleTime;
        public void ApplyRoar(float until, float damageBonus) { if (IsAlive) { roarUntil = Mathf.Max(roarUntil, until); roarBonus = damageBonus; } }
        public ManaPool Mana => medic ? medic.Mana : roar ? roar.Mana : null;
        readonly List<CombatTarget> nearby = new List<CombatTarget>(64);
        readonly List<CombatTarget> alerted = new List<CombatTarget>(32);
        readonly OrderQueue orders = new OrderQueue();
        readonly Vector3[] pathCorners = new Vector3[48];
        NavMeshPath pathQuery;
        int pathCornerCount;
        UnitCommand activeCommand;
        bool hasActiveCommand;
        CaptureOrderState capture;
        CapturePlan.View captureView;
        bool keepEmbarkStash;
        Vector3 capturePoint;
        Vector3 pathDestination;
        Vector3 attackRouteAnchor;
        int pathRevision = int.MinValue;

        public void Initialize(BattleSession battle, int team, UnitKind kind)
        {
            ClearHumanMoveTelemetry(true);
            session=battle; Team=team; Kind=kind; Health=MaxHealth; OriginCountry=-1;
            Garrison=null; simulationPaused=false; enabled=true; roarUntil=-1; roarBonus=0;
            anchor=destination=pursuitOrigin=patrolOrigin=transform.position;
            mode=OrderMode.Idle; target=strikeTarget=null; followTargetId=0; hasActiveCommand=false; capture.Clear(); captureView=default; orders.Reset();
            admissionSnapReady=false; admissionSnapId=0; admissionSnap=default;
            nextPath=nextAttack=stalled=0; strikeAt=-1; attackPresentationStartedAt=-1; attackPresentationContactTick=-1; wasFighting=false;
            humanMoveSubmittedAt=-1; humanMoveRouteResolved=false; pathPendingSince=-1;
            bool first=!Agent;
            Agent=GetComponent<NavMeshAgent>(); Agent.enabled=true;
            // SourceGeometry holds verified W3U/SLK collision sizes. Height is
            // separate from rendered standing bounds: navigation clearance stays unchanged.
            Agent.radius=UnitCatalog.Get(kind).CollisionRadius; Agent.height=1.3f; Agent.speed=UnitCatalog.Get(kind).Speed;
            Agent.acceleration=32; Agent.angularSpeed=540; Agent.stoppingDistance=.15f; Agent.autoBraking=true;
            Agent.updatePosition=true; Agent.updateRotation=true;
            Agent.obstacleAvoidanceType=ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            forestCellX=forestCellZ=int.MinValue;forestRevision=-1;
            UpdateTerrainSpeed();
            session.RegisterTarget(this);
            Agent.avoidancePriority=25+EntityId%32;
            if(Agent.isOnNavMesh){Agent.Warp(transform.position);Agent.ResetPath();SoldierPathBudget.NoteResetPath();Agent.isStopped=false;}
            transform.rotation=Quaternion.Euler(0,team==0?180:0,0);
            if(first)
            {
                var collider=gameObject.AddComponent<CapsuleCollider>();collider.radius=.4f;collider.height=1.5f;collider.center=Vector3.up*.7f;collider.isTrigger=true;
                VisualFactory.Soldier(this);
                presentationLod=gameObject.AddComponent<UnitPresentationLodView>();presentationLod.Initialize(kind,team);
                if(UnitCatalog.Get(kind).Heal.Enabled)medic=gameObject.AddComponent<MedicSupport>();
                if(UnitCatalog.Get(kind).Roar.Enabled)roar=gameObject.AddComponent<RoarSupport>();
                visualAnimator=GetComponent<SoldierAnimator>();
                ring=VisualFactory.Ring(transform,Mathf.Max(.43f,UnitCatalog.Get(kind).CollisionRadius*1.15f),.045f,new Color(.5f,1f,.6f));
            }
            GetComponent<Collider>().enabled=true;
            if(medic)medic.Initialize(this,battle);
            if(roar)roar.Initialize(this,battle);
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
            Agent.ResetPath(); SoldierPathBudget.NoteResetPath(); Agent.isStopped=true; SoldierPathBudget.NoteStopped();
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
            Garrison=null; CancelStrike(); target=null; followTargetId=0; mode=OrderMode.Idle;
            anchor=destination=transform.position; nextSense=0; wasFighting=false;
            PublishRoute();
            if(!Agent || !Agent.enabled)return;
            Agent.updatePosition=true; Agent.updateRotation=true;
            Agent.obstacleAvoidanceType=ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            if(Agent.isOnNavMesh){Agent.Warp(transform.position);Agent.ResetPath();SoldierPathBudget.NoteResetPath();Agent.isStopped=false;}
        }

        void MaintainGarrisonAnchor()
        {
            if(!IsGarrison || !Agent || !Agent.enabled)return;
            Agent.isStopped=true;
            if((transform.position-garrisonAnchor).sqrMagnitude>.000001f)transform.position=garrisonAnchor;
            if(Agent.isOnNavMesh && (Agent.nextPosition-garrisonAnchor).sqrMagnitude>.000001f)Agent.Warp(garrisonAnchor);
        }

        public void Select(bool value) { bool pop = value && !Selected; Selected = value; if (ring) { ring.enabled = value; if (pop) GameFeel.PopRing(ring); } if(presentationLod)presentationLod.SetSelected(value); }
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
        public bool TryMoveTo(Vector3 point,bool attackMove,bool append) =>
            ApplyOrder(new UnitCommand(Team, EntityId, attackMove ? UnitCommandKind.AttackMove : UnitCommandKind.Move, point.x, point.y, point.z, append: append));
        public bool Patrol(Vector3 point, bool append) =>
            ApplyOrder(new UnitCommand(Team, EntityId, UnitCommandKind.Patrol, point.x, point.y, point.z, append: append));
        bool OrderBusy => mode != OrderMode.Idle && mode != OrderMode.Hold;
        void Apply(OrderMode orderMode, Vector3 point, int targetId)
        {
            ClearHumanMoveTelemetry(true);
            CancelStrike(); mode = orderMode; destination = point; patrolOrigin = anchor = transform.position;
            target = null; followTargetId = targetId; stalled = 0; nextSense = 0; wasFighting = false;
            ResumePath();
        }
        void ResumePath()
        {
            Agent.isStopped = false; Agent.stoppingDistance = .15f; nextPath = 0;
            if (mode == OrderMode.Move || mode == OrderMode.AttackMove || mode == OrderMode.Patrol)
            {
                SoldierPathBudget.NoteSetDestination();
                if(!Agent.SetDestination(destination))LastMoveError="No se ha podido calcular la ruta a ese destino.";
            }
            else { Agent.ResetPath(); SoldierPathBudget.NoteResetPath(); }
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
            SoldierPathBudget.NoteSetDestination();
            return Agent.SetDestination(point);
        }
        public void Attack(CombatTarget enemy, bool append = false)
        {
            if (!enemy) return;
            var point = enemy.transform.position;
            ApplyOrder(new UnitCommand(Team, EntityId, UnitCommandKind.Attack, point.x, point.y, point.z, enemy.EntityId, append));
        }
        void BeginAttack(CombatTarget enemy)
        {
            ClearHumanMoveTelemetry(true);
            CancelStrike();
            followTargetId = 0;
            mode = OrderMode.Attack;
            SetTarget(enemy);
            Agent.isStopped = false;
        }
        public void Follow(Soldier ally, bool append = false)
        {
            if (!ally) return;
            var point = ally.transform.position;
            ApplyOrder(new UnitCommand(Team, EntityId, UnitCommandKind.Follow, point.x, point.y, point.z, ally.EntityId, append));
        }
        public void Stop() => ApplyOrder(new UnitCommand(Team, EntityId, UnitCommandKind.Stop));
        public void HoldPosition() => ApplyOrder(new UnitCommand(Team, EntityId, UnitCommandKind.Hold));
        void Stand(OrderMode orderMode)
        {
            if(IsGarrison)return;
            if (!keepEmbarkStash) orders.ClearStash();
            orders.Clear(); hasActiveCommand = false; capture.Clear(); ClearHumanMoveTelemetry(true); CancelStrike(); target = null; followTargetId = 0; mode = orderMode; anchor = transform.position; nextSense = 0; wasFighting = false; PublishRoute();
            if (Agent && Agent.isOnNavMesh) { Agent.ResetPath(); SoldierPathBudget.NoteResetPath(); Agent.isStopped = false; }
        }
        void Complete(bool failed=false)
        {
            if (OrderAdvance.Drain(orders, this)) { PublishRoute(); return; }
            if (mode == OrderMode.Patrol && !failed) { var swap = destination; destination = patrolOrigin; patrolOrigin = swap; ResumePath(); }
            else Stand(OrderMode.Idle);
            PublishRoute();
        }
        /// <summary>A dequeued command runs on the motor. It is not admitted again (its append flag would re-queue it).</summary>
        bool RunQueued(in UnitCommand command)
        {
            // The destination was sampled when the order was admitted. Starting it must not sample again.
            if (!OrderValidation.Check(session, this, command, false, out _)) { LastMoveError = null; return false; }
            if (!ReleasePost(command, true, out _)) { LastMoveError = null; return false; }
            return Execute(command);
        }
        bool IQueuedOrderRunner.TryStartQueued(in UnitCommand command) => RunQueued(command);
        void SetTarget(CombatTarget enemy) { target = enemy; pursuitOrigin = transform.position; nextPath = 0; }
        void CancelStrike()
        {
            if(strikeAt>=0)
            {
                attackPresentationStartedAt=-1;attackPresentationContactTick=-1;
                if(visualAnimator)visualAnimator.CancelStrike();
            }
            strikeAt = -1; strikeTarget = null;
        }
        bool Visible(CombatTarget enemy) => UnitTargeting.Visible(Type.Acquisition.Visibility, this, enemy);
        bool ValidTarget()
        {
            if (!UnitTargeting.CanTarget(this, Team, Type.Weapon, target)) return false;
            if (mode == OrderMode.Attack) return true;
            float leash = AutonomousLeash();
            var origin = mode == OrderMode.Idle || mode == OrderMode.Hold ? anchor : pursuitOrigin;
            // Acquisition and firing measure an attackable surface. The autonomous
            // leash must use that same surface or a long ship can be in weapon range
            // while its pivot silently cancels the pending strike.
            return Vector3.Distance(target.ApproachPoint(origin), origin) <= leash;
        }
        float AutonomousLeash() => UnitRules.Leash(Type.Acquisition, Team == PlayerRules.NeutralTeam);
        void Acquire()
        {
            if (mode == OrderMode.Move || mode == OrderMode.Follow || mode == OrderMode.Attack || mode == OrderMode.Embark) return;
            ref readonly var type = ref Type;
            bool neutral = Team == PlayerRules.NeutralTeam;
            float radius = UnitRules.AcquireRadius(type.Acquisition, mode == OrderMode.Hold, neutral);
            // An idle (or neutral) unit only takes targets inside its leash around the anchor.
            bool leashed = type.Acquisition.HasLeash && (mode == OrderMode.Idle || neutral);
            var best = UnitTargeting.Acquire(session, this, Team, type, radius, leashed, anchor, AutonomousLeash(), nearby);
            if (best) SetTarget(best);
        }
        public void SimTick(float delta)
        {
            simDelta=delta;
            if (!session || session.Paused || session.Winner >= 0 || !IsAlive || !Agent || !Agent.enabled || !Agent.isOnNavMesh) return;
            if (CaptureFinished()) { Complete(); return; }
            UpdateTerrainSpeed();
            if (Agent.pathPending)
            {
                if (pathPendingSince < 0) pathPendingSince = session.BattleTime;
            }
            else pathPendingSince = -1;
            // Warcraft's Ahea heal is an autocast order. A medic can resume combat
            // afterwards, but cannot resolve a heal and an attack in the same tick.
            bool healed=medic&&medic.SimTick(delta);
            if(roar)roar.SimTick(delta);
            if(healed){CancelStrike();nextAttack=Mathf.Max(nextAttack,session.BattleTime+Type.Heal.Cooldown);}
            if (!healed && strikeAt >= 0 && session.BattleTime >= strikeAt)
            {
                attackPresentationContactTick=session.Clock.TickCount;
                if(visualAnimator)visualAnimator.SampleStrikeContact();
                if (strikeTarget && strikeTarget.Health > 0 && UnitRules.StrikeLands(Type.Weapon, AttackDistance(strikeTarget)) && Visible(strikeTarget))
                {
                    float damage = session.RollDamage(Type.Weapon) * (IsRoaring ? 1 + roarBonus : 1);
                    if (Type.Weapon.Ranged) session.Combat.FireWeapon(AimPoint, strikeTarget.AimPoint, strikeTarget, damage, Team, this, Type.Weapon);
                    else { strikeTarget.ReceiveAttack(damage, AttackType, Team, this); session.Feedback.RaiseImpact(strikeTarget.AimPoint, AttackType, 0, ImpactKind.Melee, this); }
                }
                strikeAt = -1; strikeTarget = null;
            }
            if (!ValidTarget())
            {
                target = null;
                if (mode == OrderMode.Capture)
                {
                    CancelStrike();
                    if (!StepCapture()) Complete();
                }
                else if (wasFighting || mode == OrderMode.Attack)
                {
                    CancelStrike();
                    if (UnitRules.OnTargetLost(CommandKind(mode)) == UnitRules.TargetLost.Advance) Complete();
                    else ResumePath();
                    if (mode == OrderMode.Idle && Vector3.Distance(transform.position, anchor) > 1.5f) RequestAutonomousPath(anchor);
                }
            }
            if (!target && session.BattleTime >= nextSense) { nextSense = session.BattleTime + .2f; Acquire(); }
            if (target) { if(!healed)Fight(); } else Travel();
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
            Agent.speed=Type.Speed*(Type.ForestPenalty?canopies.MovementMultiplier(cell):1f);
        }
        void LateUpdate()
        {
            MaintainGarrisonAnchor();
            if(!session || session.Paused || session.Winner>=0 || !IsAlive || !Agent || !Agent.enabled)return;
            float stride = Agent.velocity.magnitude > .15f ? Mathf.Sin(session.BattleTime * 13 + EntityId) * 30 : 0;
            if (LeftLeg) LeftLeg.localRotation = Quaternion.Euler(stride, 0, 0);
            if (RightLeg) RightLeg.localRotation = Quaternion.Euler(-stride, 0, 0);
            float presentation=AttackPresentationProgress;
            if (Weapon)
            {
                float pose=AttackPresentationTiming.ContactPose(presentation,
                    Type.AttackContact);
                Weapon.localRotation = Quaternion.Euler(-15-pose*95,0,0);
            }
        }
        /// <summary>
        /// Distance the attack range is measured over. Melee reach is edge to edge, as in the
        /// source (gap between the two collision circles; building/ship approach points already
        /// lie on their surface). Ranged units keep the centre-to-approach-point distance.
        /// </summary>
        float AttackDistance(CombatTarget other) => UnitTargeting.WeaponDistance(this, Type.Weapon, other);
        /// <summary>Centre distance at which a melee unit of <paramref name="kind"/> engages a target of the given radius.</summary>
        CombatTarget meleeEngaged;
        void Fight()
        {
            float distance = AttackDistance(target);
            // Melee hysteresis: close to just inside the reach before the first blow, then
            // keep striking anywhere within it. Without this a charging lancer halts at the
            // very edge and every small drift restarts the approach.
            float reach = UnitRules.Reach(Type.Weapon, meleeEngaged == target);
            if (!Type.Weapon.Ranged && distance > Type.Weapon.Range) meleeEngaged = null;
            bool visible = Visible(target);
            if(UnitRules.TooClose(Type.Weapon, distance))
            {
                if(mode==OrderMode.Hold){target=null;Agent.isStopped=IsGarrison;return;}
                if(session.BattleTime>=nextPath)
                {
                    nextPath=session.BattleTime+.4f;var away=transform.position-target.transform.position;away.y=0;
                    if(away.sqrMagnitude<.01f)away=transform.forward;
                    var retreat=transform.position+away.normalized*(Type.Weapon.MinRange+1.5f-distance);
                    if(NavMesh.SamplePosition(retreat,out var spot,3,NavMesh.AllAreas)){Agent.isStopped=false;Agent.stoppingDistance=.15f;RequestAutonomousPath(spot.position);}
                }
                return;
            }
            if (distance <= reach && visible)
            {
                if (!Type.Weapon.Ranged) meleeEngaged = target;
                Agent.isStopped = true;
                Vector3 direction = target.transform.position - transform.position; direction.y = 0;
                if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 650 * simDelta);
                if (session.BattleTime >= nextAttack && strikeAt < 0)
                {
                    ref readonly var profile=ref Type.Weapon;
                    nextAttack = session.BattleTime + profile.Cooldown; strikeAt = session.BattleTime + profile.AttackPoint;
                    strikeTarget = target;
                    attackPresentationStartedAt=session.BattleTime;
                    attackPresentationAttackPoint=profile.AttackPoint;
                    attackPresentationRecovery=AttackPresentationTiming.RecoverySeconds(profile.AttackPoint,profile.Backswing,profile.Cooldown);
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
                if (Type.Weapon.Ranged && visible)
                {
                    float range = Type.Weapon.Range;
                    var probe = approach + (transform.position - approach).normalized * (range - .2f);
                    if (NavMesh.SamplePosition(probe, out var firing, .75f, NavMesh.AllAreas) &&
                        Vector3.Distance(firing.position, approach) <= range &&
                        Vector3.Distance(firing.position, approach) >= Type.Weapon.MinRange &&
                        !NavMesh.Raycast(transform.position, firing.position, out _, NavMesh.AllAreas) &&
                        !Physics.Linecast(firing.position + Vector3.up * 1.05f, target.AimPoint,
                            1 << MapLayout.TerrainLayer, QueryTriggerInteraction.Ignore))
                    {
                        Agent.stoppingDistance = .05f;
                        RequestAutonomousPath(firing.position);
                        return;
                    }
                }
                if (!Type.Weapon.Ranged)
                {
                    // Stop at the edge of the reach instead of pressing into the target.
                    var from = transform.position - approach; from.y = 0;
                    if (from.sqrMagnitude > .0001f)
                    {
                        var probe = approach + from.normalized * UnitRules.EngageDistance(Type, target.Type.BodyRadius);
                        if (NavMesh.SamplePosition(probe, out var spot, .75f, NavMesh.AllAreas) &&
                            !NavMesh.Raycast(transform.position, spot.position, out _, NavMesh.AllAreas))
                        {
                            Agent.stoppingDistance = .05f;
                            RequestAutonomousPath(spot.position);
                            return;
                        }
                    }
                }
                // An obstructed firing position still requires following the
                // target's path to find a reachable point with line of sight.
                Agent.stoppingDistance = Mathf.Max(.15f, Type.Weapon.Ranged && visible
                    ? Type.Weapon.Range - .12f : Type.Weapon.Range * .76f);
                RequestAutonomousPath(approach);
            }
        }
        void Travel()
        {
            if(IsGarrison){Agent.isStopped=true;return;}
            Agent.isStopped = false;
            if (mode == OrderMode.Embark)
            {
                var ship = session.FindTarget(activeCommand.TargetId) as Ship;
                if (!ship || !ship.IsAlive || !ship.Type.CanTransport || ship.Team != Team)
                {
                    if (orders.StashCount > 0) RestoreEmbarkOrders();
                    else Complete();
                    return;
                }
                if (WithinLoad(ship) && ship.TryEmbark(this)) return;
                if (session.BattleTime >= nextPath)
                {
                    nextPath = session.BattleTime + .2f;
                    if (NavMesh.SamplePosition(ship.transform.position, out var berth, 8, NavMesh.AllAreas)) RequestAutonomousPath(berth.position);
                }
                return;
            }
            if (mode == OrderMode.Follow)
            {
                var followTarget = session.FindTarget(followTargetId) as Soldier;
                if (!followTarget || !followTarget.IsAlive || followTarget.Team != Team) { followTargetId = 0; Complete(); return; }
                if (session.BattleTime >= nextPath) { nextPath = session.BattleTime + .2f; Agent.stoppingDistance = 2; RequestAutonomousPath(followTarget.transform.position); }
                if (followTarget.CurrentTarget && Vector3.Distance(transform.position, followTarget.CurrentTarget.transform.position) < 9) SetTarget(followTarget.CurrentTarget);
                return;
            }
            if (mode == OrderMode.Move || mode == OrderMode.AttackMove || mode == OrderMode.Patrol || mode == OrderMode.Capture)
            {
                if (Agent.pathPending) return;
                float distance = Vector3.Distance(transform.position, destination);
                stalled = Agent.velocity.sqrMagnitude < .04f ? stalled + simDelta : 0;
                if(Agent.pathStatus!=NavMeshPathStatus.PathComplete && distance>.65f)
                {
                    if(LastMoveError==null)
                    {
                        LastMoveError="No hay un camino terrestre hasta ese punto. Elige otro destino o usa un transporte.";
                        if(Team==0){session.Message(LastMoveError, MessageKind.Info);Debug.Log("RISKAI_ROUTE_BLOCKED: id="+EntityId+" tick="+session.Clock.TickCount+" destination="+destination);}
                    }
                    // Follow the reachable segment once. Re-requesting an exhausted partial
                    // path keeps pathPending cycling and never lets a stalled timer complete.
                    if(!Agent.hasPath || Agent.remainingDistance<.4f || stalled>1.2f)Complete(true);
                    return;
                }
                if (distance < .65f || (Agent.pathStatus==NavMeshPathStatus.PathComplete && ((Agent.hasPath && Agent.remainingDistance < .4f) || (stalled > 1.2f && distance < 3))))
                {
                    if (mode == OrderMode.Capture) { if (!StepCapture()) Complete(); }
                    else Complete();
                }
                else if (!Agent.hasPath && session.BattleTime >= nextPath) { Agent.stoppingDistance = .15f; SoldierPathBudget.NoteSetDestination(); Agent.SetDestination(destination); nextPath = session.BattleTime + .5f; }
            }
        }
        /// <summary>An idle ally joins against the attacker of a nearby friend.</summary>
        public override void JoinAlert(CombatTarget attacker) { if (IsIdle) SetTarget(attacker); }
        public OrderQueue Orders => orders;
        public int OrderLegCount => OrderLegView.Count(orders);
        public Vector3 OrderLegPoint(int index) => OrderLegView.Point(orders, index, session, hasActiveCommand, activeCommand);
        public UnitCommandKind OrderLegKind(int index) => OrderLegView.Kind(orders, index);
        public int ActivePathCount => pathCornerCount;
        public Vector3 ActivePathPoint(int index) => pathCorners[index];
        public void RefreshActivePath()
        {
            if (!Agent || !Agent.isOnNavMesh || Agent.pathPending || !Agent.hasPath) { if (Agent && !Agent.hasPath) pathCornerCount = 0; return; }
            var destinationNow = Agent.destination;
            bool attackMoved = hasActiveCommand && activeCommand.Kind == UnitCommandKind.Attack && target &&
                (target.transform.position - attackRouteAnchor).sqrMagnitude > .25f;
            if (!attackMoved && pathRevision == orders.Revision && destinationNow == pathDestination && pathCornerCount >= 2) return;
            if (target) attackRouteAnchor = target.transform.position;
            RememberPath();
            pathDestination = destinationNow;
            pathRevision = orders.Revision;
        }
        string IOrderable.OrderError => LastMoveError;
        void IOrderable.ClearOrderError() => LastMoveError = null;
        bool IOrderable.Authorize(ref UnitCommand command, bool plan)
        {
            admissionSnapReady = false;
            bool ok = OrderValidation.Check(session, this, command, plan, out var error);
            if (!ok) LastMoveError = string.IsNullOrEmpty(error) ? OrderQueue.InvalidError : error;
            else if (admissionSnapReady && admissionSnapId == command.CommandId)
                command = command.WithSnap(admissionSnap.x, admissionSnap.y, admissionSnap.z);
            admissionSnapReady = false;
            return ok;
        }
        bool IOrderable.ApplyOrder(in UnitCommand command) => ApplyOrder(command);
        bool IOrderable.HumanMoveEligible(in UnitCommand command) =>
            command.PlayerId == 0 && !command.Append &&
            (command.Kind == UnitCommandKind.Move || command.Kind == UnitCommandKind.AttackMove) &&
            CanBeginHumanMoveTelemetry;
        void IOrderable.BeginHumanMove(in UnitCommand command, double submittedAt, double pausedAtSubmit, bool eligible)
        {
            if (command.PlayerId != 0 || command.Append) return;
            if (command.Kind != UnitCommandKind.Move && command.Kind != UnitCommandKind.AttackMove) return;
            BeginHumanMoveTelemetry(submittedAt, pausedAtSubmit, eligible, new Vector3(command.X, command.Y, command.Z));
        }

        public bool ReleasePost(in UnitCommand command, bool commitRelease, out string error)
        {
            error = null;
            if (!IsGarrison || command.Kind == UnitCommandKind.Stop || command.Kind == UnitCommandKind.Hold) return true;
            var zone = Garrison;
            if (zone != null && (commitRelease ? zone.TryReleaseGuardianForOrder(session, this) : zone.HasRelief(session, this)))
                return true;
            error = "El defensor necesita un relevo aliado dentro del círculo.";
            return false;
        }

        public bool Reach(in UnitCommand command, bool plan, out string error)
        {
            error = null;
            switch (command.Kind)
            {
                case UnitCommandKind.Move:
                case UnitCommandKind.AttackMove:
                case UnitCommandKind.Patrol:
                    return plan ? SnapDestination(command, out error) : OnMesh(out error);
                case UnitCommandKind.Attack:
                case UnitCommandKind.Capture:
                case UnitCommandKind.Embark:
                    return OnMesh(out error);
                default:
                    return true;
            }
        }

        bool admissionSnapReady;
        int admissionSnapId;
        Vector3 admissionSnap;
        bool SnapDestination(in UnitCommand command, out string error)
        {
            error = null;
            admissionSnapReady = false;
            if (!OnMesh(out error)) return false;
            SoldierPathBudget.NoteSample();
            if (!NavMesh.SamplePosition(new Vector3(command.X, command.Y, command.Z), out var hit, 8, NavMesh.AllAreas))
            {
                error = "Ese destino no es transitable; usa un transporte para cruzar el agua.";
                return false;
            }
            admissionSnapId = command.CommandId;
            admissionSnap = hit.position;
            admissionSnapReady = true;
            return true;
        }

        bool ResolveDestination(in UnitCommand command, out Vector3 point)
        {
            if (command.HasSnap)
            {
                point = new Vector3(command.SnapX, command.SnapY, command.SnapZ);
                return true;
            }
            SoldierPathBudget.NoteSample();
            if (!NavMesh.SamplePosition(new Vector3(command.X, command.Y, command.Z), out var hit, 8, NavMesh.AllAreas))
            {
                point = default;
                return false;
            }
            point = hit.position;
            return true;
        }

        bool OnMesh(out string error)
        {
            error = null;
            if (Agent && Agent.enabled && Agent.isOnNavMesh) return true;
            error = "La unidad no está sobre terreno transitable.";
            return false;
        }

        bool ApplyOrder(in UnitCommand command)
        {
            // plan=false: admission already snapped a command that came through the inbox.
            bool valid = OrderValidation.Check(session, this, command, false, out var error);
            if (!valid) LastMoveError = string.IsNullOrEmpty(error) ? OrderQueue.InvalidError : error;
            bool willRun = valid && UnitRules.Queue(command.Kind, command.Append, OrderBusy) != UnitRules.OrderQueueAction.Append;
            if (willRun && command.Kind != UnitCommandKind.Stop && command.Kind != UnitCommandKind.Hold
                && !ReleasePost(command, true, out error))
            {
                LastMoveError = string.IsNullOrEmpty(error) ? OrderQueue.InvalidError : error;
                valid = false;
            }
            // A replacing order cancels the voyage plan. An appended order keeps it,
            // and a full queue must not wipe the stash before Commit refuses the order.
            bool replacing = willRun && command.Kind != UnitCommandKind.Stop && command.Kind != UnitCommandKind.Hold
                && command.Kind != UnitCommandKind.Embark;
            if (valid && replacing && !keepEmbarkStash) orders.ClearStash();
            else if (valid && command.Kind == UnitCommandKind.Embark && !command.Append)
                KeepEmbarkPlan();
            else if (valid && hasActiveCommand && activeCommand.Kind == UnitCommandKind.Embark
                && UnitRules.Queue(command.Kind, command.Append, OrderBusy) == UnitRules.OrderQueueAction.Append
                && !orders.CanStash(command))
            {
                LastMoveError = OrderQueue.FullError;
                valid = false;
            }
            switch (orders.Commit(command, OrderBusy, valid))
            {
                case OrderQueue.AdmitResult.Rejected:
                    if (string.IsNullOrEmpty(LastMoveError)) LastMoveError = OrderQueue.InvalidError;
                    return false;
                case OrderQueue.AdmitResult.Full:
                    LastMoveError = OrderQueue.FullError;
                    return false;
                case OrderQueue.AdmitResult.Queued:
                    if (hasActiveCommand && activeCommand.Kind == UnitCommandKind.Embark)
                        orders.AppendStash(command);
                    PublishRoute();
                    return true;
                default:
                    bool ok = Execute(command);
                    PublishRoute();
                    return ok;
            }
        }

        bool Execute(in UnitCommand command)
        {
            switch (command.Kind)
            {
                case UnitCommandKind.Move: return ExecuteMove(command, OrderMode.Move);
                case UnitCommandKind.AttackMove: return ExecuteMove(command, OrderMode.AttackMove);
                case UnitCommandKind.Patrol: return ExecuteMove(command, OrderMode.Patrol);
                case UnitCommandKind.Attack: return ExecuteAttack(command);
                case UnitCommandKind.Follow: return ExecuteFollow(command);
                case UnitCommandKind.Capture: return ExecuteCapture(command);
                case UnitCommandKind.Embark: return ExecuteEmbark(command);
                case UnitCommandKind.Stop: Stand(OrderMode.Idle); return true;
                case UnitCommandKind.Hold: Stand(OrderMode.Hold); return true;
                default: LastMoveError = OrderQueue.InvalidError; return false;
            }
        }

        bool ExecuteMove(in UnitCommand command, OrderMode orderMode)
        {
            LastMoveError = null;
            if (!ResolveDestination(command, out var hit))
            { LastMoveError = "Ese destino no es transitable; usa un transporte para cruzar el agua."; return false; }
            Remember(command);
            Apply(orderMode, hit, 0);
            return string.IsNullOrEmpty(LastMoveError);
        }

        bool ExecuteAttack(in UnitCommand command)
        {
            LastMoveError = null;
            var enemy = session.FindTarget(command.TargetId);
            if (!enemy) { LastMoveError = OrderQueue.InvalidError; return false; }
            Remember(command);
            BeginAttack(enemy);
            return true;
        }

        bool ExecuteFollow(in UnitCommand command)
        {
            var ally = session.FindTarget(command.TargetId) as Soldier;
            if (!ally) { LastMoveError = OrderQueue.InvalidError; return false; }
            Remember(command);
            Apply(OrderMode.Follow, ally.transform.position, ally.EntityId);
            return true;
        }

        CapturePlan.View CaptureView(in UnitCommand command)
        {
            captureView = CapturePlan.Fresh(session, command, captureView);
            return captureView;
        }

        bool CaptureFinished()
        {
            if (mode != OrderMode.Capture || !capture.Active) return false;
            var view = CaptureView(activeCommand);
            capturePoint = view.Point;
            // Land has no voyage after the post becomes ours, so the approach counts as finished.
            if (!capture.Done(view.Found, view.Owner, Team, true, Type.CanCapture)) return false;
            capture.Clear();
            captureView = default;
            return true;
        }

        bool ExecuteCapture(in UnitCommand command)
        {
            var view = CapturePlan.Look(session, command);
            if (!view.Found) { LastMoveError = "Elige una ciudad o un puerto."; return false; }
            Remember(command);
            captureView = view;
            capture.Begin(view.Found, view.Owner);
            capturePoint = view.Point;
            if (capture.Done(view.Found, view.Owner, Team, true, Type.CanCapture))
            {
                hasActiveCommand = false;
                capture.Clear();
                captureView = default;
                Complete();
                return true;
            }
            return StepCapture();
        }

        /// <summary>False when this capture is finished and the next queued order should run.</summary>
        bool StepCapture()
        {
            var view = CaptureView(activeCommand);
            capturePoint = view.Point;
            if (capture.Done(view.Found, view.Owner, Team, true, Type.CanCapture)) { capture.Clear(); captureView = default; return false; }
            if (CapturePlan.HostileGuardian(view, Team))
            {
                BeginAttack(view.Guardian);
                mode = OrderMode.Capture;
                return true;
            }
            if (!NavMesh.SamplePosition(view.Point, out var hit, 8, NavMesh.AllAreas))
            { LastMoveError = "Ese destino no es transitable; usa un transporte para cruzar el agua."; return false; }
            mode = OrderMode.Capture;
            target = null;
            followTargetId = 0;
            destination = hit.position;
            var flat = transform.position - hit.position;
            flat.y = 0;
            if (flat.sqrMagnitude <= .65f * .65f) { if (Agent) Agent.isStopped = true; return true; }
            ResumePath();
            return string.IsNullOrEmpty(LastMoveError);
        }

        bool ExecuteEmbark(in UnitCommand command)
        {
            var ship = session.FindTarget(command.TargetId) as Ship;
            if (!ship) { LastMoveError = "Selecciona un transporte."; return false; }
            Remember(command);
            // Orders still behind this embark survive the voyage. A replacing embark already stashed them.
            orders.MergeQueue();
            mode = OrderMode.Embark;
            target = null;
            followTargetId = 0;
            var landing = new Vector3(command.X, command.Y, command.Z);
            destination = ship.transform.position;
            if (landing.sqrMagnitude > .01f && NavMesh.SamplePosition(landing, out var shore, 8, NavMesh.AllAreas))
                destination = shore.position;
            else if (NavMesh.SamplePosition(ship.transform.position, out var hit, 8, NavMesh.AllAreas))
                destination = hit.position;
            if (WithinLoad(ship) && ship.TryEmbark(this)) return true;
            ResumePath();
            return true;
        }

        /// <summary>The boarding retry re-walks the embark already in progress. It does not submit a second order.</summary>
        public void RetryEmbarkApproach()
        {
            if (mode != OrderMode.Embark || !hasActiveCommand || activeCommand.Kind != UnitCommandKind.Embark) return;
            var ship = session.FindTarget(activeCommand.TargetId) as Ship;
            if (!ship) return;
            var landing = new Vector3(activeCommand.X, activeCommand.Y, activeCommand.Z);
            if (landing.sqrMagnitude > .01f && NavMesh.SamplePosition(landing, out var shore, 8, NavMesh.AllAreas))
                destination = shore.position;
            else if (NavMesh.SamplePosition(ship.transform.position, out var hit, 8, NavMesh.AllAreas))
                destination = hit.position;
            ResumePath();
        }

        bool WithinLoad(Ship ship)
        {
            var flat = transform.position - ship.transform.position;
            flat.y = 0;
            float radius = UnitCatalog.TransportLoadRadius;
            return flat.sqrMagnitude <= radius * radius;
        }

        void Remember(in UnitCommand command)
        {
            activeCommand = command;
            hasActiveCommand = command.Kind != UnitCommandKind.Stop && command.Kind != UnitCommandKind.Hold;
        }

        static UnitCommandKind CommandKind(OrderMode mode)
        {
            switch (mode)
            {
                case OrderMode.AttackMove: return UnitCommandKind.AttackMove;
                case OrderMode.Attack: return UnitCommandKind.Attack;
                case OrderMode.Patrol: return UnitCommandKind.Patrol;
                case OrderMode.Follow: return UnitCommandKind.Follow;
                case OrderMode.Hold: return UnitCommandKind.Hold;
                default: return UnitCommandKind.Move;
            }
        }

        /// <summary>A replacing embark keeps the live plan. An existing stash is merged, not left behind.</summary>
        void KeepEmbarkPlan()
        {
            if (orders.StashCount == 0)
            {
                orders.Stash(hasActiveCommand && activeCommand.Kind != UnitCommandKind.Embark, activeCommand);
                return;
            }
            if (hasActiveCommand && activeCommand.Kind != UnitCommandKind.Embark)
                orders.AppendStash(activeCommand);
            orders.MergeQueue();
        }

        /// <summary>Boarding keeps every queued command, including its target, for after the unload.</summary>
        public void RetainOrdersForEmbark()
        {
            // A direct board (no Embark order) still has the live move as the active command.
            // An Embark order already stashed that plan; the active command is then the embark itself.
            if (hasActiveCommand && activeCommand.Kind != UnitCommandKind.Embark)
                orders.AppendStash(activeCommand);
            orders.MergeQueue();
        }

        /// <summary>Stop the motor but keep the passenger plan that <see cref="RetainOrdersForEmbark"/> just stored.</summary>
        public void StopKeepingPassengerPlan()
        {
            RetainOrdersForEmbark();
            keepEmbarkStash = true;
            Stop();
            keepEmbarkStash = false;
        }

        readonly UnitCommand[] embarkRestore = new UnitCommand[OrderQueue.LegCap];
        public void RestoreEmbarkOrders()
        {
            int count = orders.StashCount;
            if (count <= 0) return;
            if (count > embarkRestore.Length) count = embarkRestore.Length;
            for (int i = 0; i < count; i++) embarkRestore[i] = orders.StashedCommand(i);
            orders.ClearStash();
            for (int i = 0; i < count; i++) ApplyOrder(embarkRestore[i].WithAppend(i > 0));
        }

        public int PathCornerCount => pathCornerCount;
        public Vector3 PathCorner(int index) => pathCorners[index];
        public void RememberPath()
        {
            if (!Agent || !Agent.isOnNavMesh || !Agent.hasPath) { pathCornerCount = 0; return; }
            if (pathQuery == null) pathQuery = new NavMeshPath();
            SoldierPathBudget.NoteCalculated();
            if (!Agent.CalculatePath(Agent.destination, pathQuery)) { pathCornerCount = 0; return; }
            pathCornerCount = pathQuery.GetCornersNonAlloc(pathCorners);
        }

        void PublishRoute()
        {
            if (Selected) RememberPath();
            if (!hasActiveCommand) { OrderLegView.Publish(orders, false, UnitCommandKind.Move, Vector3.zero); return; }
            Vector3 point = activeCommand.Kind == UnitCommandKind.Capture ? capturePoint
                : activeCommand.Kind == UnitCommandKind.Attack && target ? target.transform.position
                : destination;
            OrderLegView.Publish(orders, true, activeCommand.Kind, point);
        }

        bool IPostClaimant.ContendsOnFoot => this && isActiveAndEnabled && IsAlive && Agent && Agent.enabled && Agent.isOnNavMesh &&
            (PlayerRules.IsPlayer(Team) || Team == PlayerRules.NeutralTeam);
        bool IPostClaimant.BlockedByOtherPost(CityClaimZone zone) => this && IsGarrison && Garrison != zone;
        bool IPostClaimant.TryBindPost(CityClaimZone zone) =>
            this && zone != null && ((IPostClaimant)this).ContendsOnFoot && !(IsGarrison && Garrison != zone) && BindGarrison(zone);
        void IPostClaimant.ReleasePost(CityClaimZone zone) { if (this) ReleaseGarrison(zone); }
        bool IPostClaimant.DropStale(CityClaimZone zone) => !this || !((IPostClaimant)this).ContendsOnFoot || Garrison != zone;

        public float Heal(float amount)
        {
            if(!IsAlive||amount<=0||float.IsNaN(amount)||float.IsInfinity(amount))return 0;
            float before=Health;Health=Mathf.Min(MaxHealth,Health+amount);return Health-before;
        }
        public void DestroyEmbarked(int attacker)
        {
            if(Health<=0)return;ClearHumanMoveTelemetry(true);Health=0;
            if(PlayerRules.IsPlayer(attacker)&&attacker<session.PlayerCount){session.Kills[attacker]++;session.Economy.GrantBounty(attacker,Type.Points);}
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
                if (PlayerRules.IsPlayer(attacker) && attacker < session.PlayerCount) { session.Kills[attacker]++; session.Economy.GrantBounty(attacker,Type.Points); }
                Garrison=null;session.Units.Remove(this);session.UnregisterTarget(this);Select(false);Agent.enabled=false;GetComponent<Collider>().enabled=false;enabled=false;
                if(visualAnimator)visualAnimator.Die();
                session.Feedback.RaiseDied(this);
                session.SoldierPool.Retire(this,SoldierPool.CorpseDelay(session,visualAnimator));
                return;
            }
            ref readonly var acquisition = ref Type.Acquisition;
            if (source && acquisition.Retaliate && mode != OrderMode.Move && mode != OrderMode.Hold && mode != OrderMode.Follow && !target)
                SetTarget(source);
            float alert = acquisition.AllyAlertRadius;
            if(source && alert > 0)
            {
                session.Spatial.Query(transform.position,alert,alerted);
                foreach(var candidate in alerted)
                    if(candidate && candidate.Team==Team && (transform.position-candidate.transform.position).sqrMagnitude<alert*alert)
                        candidate.JoinAlert(source);
            }
        }
    }
}
