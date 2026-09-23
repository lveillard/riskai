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
        struct Order { public Vector3 Point; public OrderMode Mode; public int TargetId; }
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
        public string OrderLabel => IsGarrison ? "Guarnición · mantiene el edificio" : target ? "En combate" : mode == OrderMode.Move ? "Moviendo" : mode == OrderMode.AttackMove ? "Avanzando y atacando" : mode == OrderMode.Hold ? "Manteniendo posición" : mode == OrderMode.Patrol ? "Patrullando" : mode == OrderMode.Follow ? "Siguiendo" : "Preparado";
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
        readonly Queue<Order> orders = new Queue<Order>();

        public void Initialize(BattleSession battle, int team, UnitKind kind)
        {
            ClearHumanMoveTelemetry(true);
            session=battle; Team=team; Kind=kind; Health=MaxHealth; OriginCountry=-1;
            Garrison=null; simulationPaused=false; enabled=true; roarUntil=-1; roarBonus=0;
            anchor=destination=pursuitOrigin=patrolOrigin=transform.position;
            mode=OrderMode.Idle; target=strikeTarget=null; followTargetId=0; orders.Clear();
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
            if(Agent.isOnNavMesh){Agent.Warp(transform.position);Agent.ResetPath();Agent.isStopped=false;}
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
            Garrison=null; orders.Clear(); CancelStrike(); target=null; followTargetId=0; mode=OrderMode.Idle;
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
            target = null; followTargetId = order.TargetId; stalled = 0; nextSense = 0; wasFighting = false;
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
            orders.Clear(); ClearHumanMoveTelemetry(true); CancelStrike(); followTargetId=0; mode = OrderMode.Attack; SetTarget(enemy); Agent.isStopped = false;
        }
        public void Follow(Soldier ally)
        {
            if (IsGarrison || session.Paused || session.Winner>=0 || !ally || ally == this || ally.Team != Team) return;
            orders.Clear(); ClearHumanMoveTelemetry(true); Apply(new Order { Mode = OrderMode.Follow, TargetId = ally.EntityId });
        }
        public void Stop() => Stand(OrderMode.Idle);
        public void HoldPosition() => Stand(OrderMode.Hold);
        void Stand(OrderMode orderMode)
        {
            if(IsGarrison)return;
            orders.Clear(); ClearHumanMoveTelemetry(true); CancelStrike(); target = null; followTargetId = 0; mode = orderMode; anchor = transform.position; nextSense = 0; wasFighting = false;
            if (Agent && Agent.isOnNavMesh) { Agent.ResetPath(); Agent.isStopped = false; }
        }
        void Complete(bool failed=false)
        {
            if (orders.Count > 0) Apply(orders.Dequeue());
            else if (mode == OrderMode.Patrol && !failed) { var swap = destination; destination = patrolOrigin; patrolOrigin = swap; ResumePath(); }
            else Stop();
        }
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
        bool Visible(CombatTarget enemy)
        {
            if (!enemy) return false;
            if (Type.Weapon.Ranged)
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
            // Acquisition and firing measure an attackable surface. The autonomous
            // leash must use that same surface or a long ship can be in weapon range
            // while its pivot silently cancels the pending strike.
            return Vector3.Distance(target.ApproachPoint(origin), origin) <= leash;
        }
        float AutonomousLeash()
        {
            ref readonly var acquisition = ref Type.Acquisition;
            return Team == PlayerRules.NeutralTeam ? acquisition.LeashNeutral : acquisition.LeashHostile;
        }
        void Acquire()
        {
            if (mode == OrderMode.Move || mode == OrderMode.Follow || mode == OrderMode.Attack) return;
            ref readonly var acquisition = ref Type.Acquisition;
            float radius = mode == OrderMode.Hold ? acquisition.RadiusHold : Team == PlayerRules.NeutralTeam ? acquisition.RadiusNeutral : acquisition.RadiusHostile;
            CombatTarget best = null; float score = float.MaxValue;
            session.Spatial.Query(transform.position,radius+acquisition.QueryPadding,nearby);
            foreach (var enemy in nearby)
            {
                if (!enemy || enemy.Team == Team || !enemy.CanBeAttacked) continue;
                float distance = Vector3.Distance(transform.position, enemy.ApproachPoint(transform.position));
                if (distance > radius || !Visible(enemy)) continue;
                if ((mode == OrderMode.Idle || Team == PlayerRules.NeutralTeam) && Vector3.Distance(anchor, enemy.ApproachPoint(anchor)) > AutonomousLeash()) continue;
                int pressure = session.Spatial.Pressure(Team, enemy);
                float candidate = distance + pressure * acquisition.PressureBias;
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
            // Warcraft's Ahea heal is an autocast order. A medic can resume combat
            // afterwards, but cannot resolve a heal and an attack in the same tick.
            bool healed=medic&&medic.SimTick(delta);
            if(roar)roar.SimTick(delta);
            if(healed){CancelStrike();nextAttack=Mathf.Max(nextAttack,session.BattleTime+Type.Heal.Cooldown);}
            if (!healed && strikeAt >= 0 && session.BattleTime >= strikeAt)
            {
                attackPresentationContactTick=session.Clock.TickCount;
                if(visualAnimator)visualAnimator.SampleStrikeContact();
                if (strikeTarget && strikeTarget.Health > 0 && AttackDistance(strikeTarget) <= Type.Weapon.Range + Type.Weapon.StrikeTolerance && AttackDistance(strikeTarget)>=Type.Weapon.MinRange && Visible(strikeTarget))
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
                if (wasFighting || mode == OrderMode.Attack)
                {
                    CancelStrike();
                    if (mode == OrderMode.Attack) Complete(); else ResumePath();
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
            Agent.speed=Type.Speed*canopies.MovementMultiplier(cell);
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
        static float ReachRadius(CombatTarget other) => other is Soldier soldier ? soldier.Type.BodyRadius : 0;
        /// <summary>
        /// Distance the attack range is measured over. Melee reach is edge to edge, as in the
        /// source (gap between the two collision circles; building/ship approach points already
        /// lie on their surface). Ranged units keep the centre-to-approach-point distance.
        /// </summary>
        float AttackDistance(CombatTarget other)
        {
            float distance = Vector3.Distance(transform.position, other.ApproachPoint(transform.position));
            return Type.Weapon.Ranged ? distance : distance - Type.BodyRadius - ReachRadius(other);
        }
        /// <summary>Centre distance at which a melee unit of <paramref name="kind"/> engages a target of the given radius.</summary>
        public static float MeleeEngageDistance(UnitKind kind, float targetRadius) =>
            UnitCatalog.Get(kind).BodyRadius + targetRadius + Mathf.Max(.05f, UnitCatalog.Get(kind).Weapon.Range - UnitCatalog.Get(kind).Weapon.ApproachMargin);
        CombatTarget meleeEngaged;
        void Fight()
        {
            float distance = AttackDistance(target);
            // Melee hysteresis: close to just inside the reach before the first blow, then
            // keep striking anywhere within it. Without this a charging lancer halts at the
            // very edge and every small drift restarts the approach.
            float reach = Type.Weapon.Range;
            if (!Type.Weapon.Ranged)
            {
                if (meleeEngaged != target) reach = Mathf.Max(.05f, reach - Type.Weapon.HoldMargin);
                if (distance > Type.Weapon.Range) meleeEngaged = null;
            }
            bool visible = Visible(target);
            if(distance<Type.Weapon.MinRange)
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
                        var probe = approach + from.normalized * MeleeEngageDistance(Kind, ReachRadius(target));
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
            if (mode == OrderMode.Follow)
            {
                var followTarget = session.FindTarget(followTargetId) as Soldier;
                if (!followTarget || !followTarget.IsAlive || followTarget.Team != Team) { followTargetId = 0; Complete(); return; }
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
                        if(Team==0){session.Message(LastMoveError, MessageKind.Info);Debug.Log("RISKAI_ROUTE_BLOCKED: id="+EntityId+" tick="+session.Clock.TickCount+" destination="+destination);}
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
