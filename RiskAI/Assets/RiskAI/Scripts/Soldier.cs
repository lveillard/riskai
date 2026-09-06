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
        public string OrderLabel => IsGarrison ? "Guarnición · mantiene el edificio" : target ? "En combate" : mode == OrderMode.Move ? "Moviendo" : mode == OrderMode.AttackMove ? "Avanzando y atacando" : mode == OrderMode.Hold ? "Manteniendo posición" : mode == OrderMode.Patrol ? "Patrullando" : mode == OrderMode.Follow ? "Siguiendo" : "Preparado";
        public Transform LeftLeg, RightLeg, Weapon;
        internal BattleSession session;
        CombatTarget target, strikeTarget;
        Soldier followTarget;
        LineRenderer ring;
        SoldierAnimator visualAnimator;
        OrderMode mode;
        Vector3 destination, anchor, pursuitOrigin, patrolOrigin;
        float nextSense, nextPath, nextAttack, strikeAt = -1, attackFlash, stalled;
        bool wasFighting, simulationPaused, stoppedBeforePause;
        float simDelta;
        MedicSupport medic;
        readonly List<CombatTarget> nearby = new List<CombatTarget>(64);
        readonly List<CombatTarget> alerted = new List<CombatTarget>(32);
        readonly Queue<Order> orders = new Queue<Order>();

        public void Initialize(BattleSession battle, int team, UnitKind kind)
        {
            session=battle; Team=team; Kind=kind; Health=MaxHealth; OriginCountry=-1;
            Garrison=null; simulationPaused=false; enabled=true;
            anchor=destination=pursuitOrigin=patrolOrigin=transform.position;
            mode=OrderMode.Idle; target=strikeTarget=followTarget=null; orders.Clear();
            nextPath=nextAttack=attackFlash=stalled=0; strikeAt=-1; wasFighting=false;
            bool first=!Agent;
            Agent=GetComponent<NavMeshAgent>(); Agent.enabled=true;
            Agent.radius=.24f; Agent.height=1.3f; Agent.speed=BattleRules.Speed(kind);
            Agent.acceleration=32; Agent.angularSpeed=540; Agent.stoppingDistance=.15f; Agent.autoBraking=true;
            Agent.obstacleAvoidanceType=ObstacleAvoidanceType.LowQualityObstacleAvoidance;
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
                ring=VisualFactory.Ring(transform,.43f,.045f,new Color(.5f,1f,.6f));
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
            if(!IsAlive || IsGarrison && Garrison!=zone || !Agent || !Agent.isOnNavMesh)return false;
            if(!NavMesh.SamplePosition(zone.Center,out var hit,.9f,NavMesh.AllAreas))return false;
            Stand(OrderMode.Hold);Agent.Warp(hit.position);anchor=hit.position;Garrison=zone;Agent.isStopped=true;
            return true;
        }
        internal void ReleaseGarrison(CityClaimZone zone) { if(Garrison==zone)Garrison=null; }

        public void Select(bool value) { Selected = value; if (ring) ring.enabled = value; }
        public void MoveTo(Vector3 point, bool attackMove, bool append) => Issue(point, attackMove ? OrderMode.AttackMove : OrderMode.Move, append);
        public void Patrol(Vector3 point, bool append) => Issue(point, OrderMode.Patrol, append);
        void Issue(Vector3 point, OrderMode orderMode, bool append)
        {
            if (IsGarrison || session.Paused || session.Winner>=0 || !Agent || !Agent.enabled || !Agent.isOnNavMesh || !NavMesh.SamplePosition(point, out var hit, 8, NavMesh.AllAreas)) return;
            var order = new Order { Point = hit.position, Mode = orderMode };
            if (append && mode != OrderMode.Idle && mode != OrderMode.Hold) { if (orders.Count < 35) orders.Enqueue(order); return; }
            orders.Clear(); Apply(order);
        }
        void Apply(Order order)
        {
            CancelStrike(); mode = order.Mode; destination = order.Point; patrolOrigin = anchor = transform.position;
            target = null; followTarget = order.Target; stalled = 0; nextSense = 0; wasFighting = false;
            ResumePath();
        }
        void ResumePath()
        {
            Agent.isStopped = false; Agent.stoppingDistance = .15f; nextPath = 0;
            if (mode == OrderMode.Move || mode == OrderMode.AttackMove || mode == OrderMode.Patrol) Agent.SetDestination(destination);
            else Agent.ResetPath();
        }
        public void Attack(CombatTarget enemy)
        {
            if (IsGarrison || session.Paused || session.Winner>=0 || !enemy || enemy.Team == Team || !Agent.enabled || !Agent.isOnNavMesh) return;
            orders.Clear(); CancelStrike(); mode = OrderMode.Attack; SetTarget(enemy); Agent.isStopped = false;
        }
        public void Follow(Soldier ally)
        {
            if (IsGarrison || session.Paused || session.Winner>=0 || !ally || ally == this || ally.Team != Team) return;
            orders.Clear(); Apply(new Order { Mode = OrderMode.Follow, Target = ally });
        }
        public void Stop() => Stand(OrderMode.Idle);
        public void HoldPosition() => Stand(OrderMode.Hold);
        void Stand(OrderMode orderMode)
        {
            if(IsGarrison)return;
            orders.Clear(); CancelStrike(); target = followTarget = null; mode = orderMode; anchor = transform.position; nextSense = 0; wasFighting = false;
            if (Agent && Agent.isOnNavMesh) { Agent.ResetPath(); Agent.isStopped = false; }
        }
        void Complete()
        {
            if (orders.Count > 0) Apply(orders.Dequeue());
            else if (mode == OrderMode.Patrol) { var swap = destination; destination = patrolOrigin; patrolOrigin = swap; ResumePath(); }
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
            float leash = BattleRules.Ranged(Kind) ? BattleRules.Range(Kind) + 2 : Team == 2 ? 7 : 11;
            var origin = mode == OrderMode.Idle || mode == OrderMode.Hold ? anchor : pursuitOrigin;
            return Vector3.Distance(target.transform.position, origin) <= leash;
        }
        void Acquire()
        {
            if (mode == OrderMode.Move || mode == OrderMode.Follow || mode == OrderMode.Attack) return;
            float radius = BattleRules.Ranged(Kind) ? BattleRules.Range(Kind) + 1 : mode == OrderMode.Hold ? BattleRules.Range(Kind) : Team == 2 ? 5 : 7.5f;
            CombatTarget best = null; float score = float.MaxValue;
            session.Spatial.Query(transform.position,radius+3,nearby);
            foreach (var enemy in nearby)
            {
                if (!enemy || enemy.Team == Team || !enemy.CanBeAttacked) continue;
                float distance = Vector3.Distance(transform.position, enemy.ApproachPoint(transform.position));
                if (distance > radius || !Visible(enemy)) continue;
                if ((mode == OrderMode.Idle || Team == 2) && Vector3.Distance(anchor, enemy.transform.position) > (BattleRules.Ranged(Kind) ? BattleRules.Range(Kind) + 2 : Team == 2 ? 7 : 11)) continue;
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
            if (strikeAt >= 0 && session.BattleTime >= strikeAt)
            {
                if (strikeTarget && strikeTarget.Health > 0 && Vector3.Distance(transform.position, strikeTarget.ApproachPoint(transform.position)) <= BattleRules.Range(Kind) + .55f && Vector3.Distance(transform.position,strikeTarget.ApproachPoint(transform.position))>=BattleRules.MinimumRange(Kind) && Visible(strikeTarget))
                {
                    float damage = session.RollDamage(BattleRules.Profile(Kind));
                    if (BattleRules.Ranged(Kind)) session.Combat.FireProjectile(AimPoint, strikeTarget.AimPoint, strikeTarget, damage, Team, this, AttackType);
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
                    if (mode == OrderMode.Idle && Vector3.Distance(transform.position, anchor) > 1.5f) Agent.SetDestination(anchor);
                }
            }
            if (!target && session.BattleTime >= nextSense) { nextSense = session.BattleTime + .2f; Acquire(); }
            if (target) Fight(); else Travel();
            wasFighting = target != null;
            if(IsGarrison)Agent.isStopped=true;
            if(medic)medic.SimTick(delta);
        }
        void LateUpdate()
        {
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
            if(distance<BattleRules.MinimumRange(Kind))
            {
                if(mode==OrderMode.Hold){target=null;Agent.isStopped=false;return;}
                if(session.BattleTime>=nextPath)
                {
                    nextPath=session.BattleTime+.4f;var away=transform.position-target.transform.position;away.y=0;
                    if(away.sqrMagnitude<.01f)away=transform.forward;
                    var retreat=transform.position+away.normalized*(BattleRules.MinimumRange(Kind)+1.5f-distance);
                    if(NavMesh.SamplePosition(retreat,out var spot,3,NavMesh.AllAreas)){Agent.isStopped=false;Agent.stoppingDistance=.15f;Agent.SetDestination(spot.position);}
                }
                return;
            }
            if (distance <= BattleRules.Range(Kind) && Visible(target))
            {
                Agent.isStopped = true;
                Vector3 direction = target.transform.position - transform.position; direction.y = 0;
                if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 650 * simDelta);
                if (session.BattleTime >= nextAttack && strikeAt < 0)
                {
                    nextAttack = session.BattleTime + BattleRules.AttackInterval(Kind); strikeAt = session.BattleTime + (Kind == UnitKind.Footman ? .17f : .24f);
                    strikeTarget = target; attackFlash = .4f;
                    if(visualAnimator)visualAnimator.Strike();
                }
            }
            else if (mode == OrderMode.Hold) { target = null; Agent.isStopped = false; }
            else if (strikeAt < 0 && session.BattleTime >= nextPath)
            {
                nextPath = session.BattleTime + .16f; Agent.isStopped = false;
                Agent.stoppingDistance = Mathf.Max(.15f, BattleRules.Range(Kind) * .76f);
                Agent.SetDestination(target.ApproachPoint(transform.position));
            }
        }
        void Travel()
        {
            if(IsGarrison){Agent.isStopped=true;return;}
            Agent.isStopped = false;
            if (mode == OrderMode.Follow)
            {
                if (!followTarget) { Complete(); return; }
                if (session.BattleTime >= nextPath) { nextPath = session.BattleTime + .2f; Agent.stoppingDistance = 2; Agent.SetDestination(followTarget.transform.position); }
                if (followTarget.CurrentTarget && Vector3.Distance(transform.position, followTarget.CurrentTarget.transform.position) < 9) SetTarget(followTarget.CurrentTarget);
                return;
            }
            if (mode == OrderMode.Move || mode == OrderMode.AttackMove || mode == OrderMode.Patrol)
            {
                if (Agent.pathPending) return;
                float distance = Vector3.Distance(transform.position, destination);
                stalled = Agent.velocity.sqrMagnitude < .04f ? stalled + simDelta : 0;
                if (distance < .65f || (Agent.hasPath && Agent.remainingDistance < .4f) || (stalled > 1.2f && distance < 3)) Complete();
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
            if(Health<=0)return;Health=0;
            if(attacker>=0&&attacker<2){session.Kills[attacker]++;session.Economy.GrantBounty(attacker,BattleRules.PointValue(Kind));}
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
                if (attacker >= 0 && attacker < 2) { session.Kills[attacker]++; session.Economy.GrantBounty(attacker,BattleRules.PointValue(Kind)); }
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

