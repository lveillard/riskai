using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// Pooled, presentation-only juice: hit flashes, corpses, spawn pops, capture rings,
    /// selection pops, scorch decals and a small optional camera shake. It only observes
    /// <see cref="BattleFeedback"/> events and never changes simulation state.
    /// </summary>
    public sealed class GameFeel : MonoBehaviour
    {
        public const float FlashSeconds = .08f;
        public const float SpawnSeconds = .25f;
        public const float RingPopSeconds = .12f;
        const int MaxFlashes = 64, MaxWorldRings = 16, MaxDecals = 40;
        const string ShakeKey = "riskai.camera.shake";
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color FlashColor = new Color(2.4f, 2.3f, 2.1f, 1);

        public static GameFeel Current { get; private set; }
        public static bool ShakeEnabled { get; private set; } = true;
        static bool shakeLoaded;

        sealed class UnitView { public Renderer[] Renderers; public bool[] Applied; public SoldierAnimator Animator; public float FlashUntil = -1; public int FlashId; }
        struct Corpse { public Soldier Unit; public int Id; public float DiedAt; public Vector3 Origin; public Quaternion Rotation; public bool Animated; public float Tilt; }
        struct Grow { public Soldier Unit; public int Id; public float Start; }
        struct RingPop { public LineRenderer Ring; public float Start, Width; }
        struct Pop { public Transform Target; public Vector3 Base; public float Start, Duration, Amount; public bool Vertical; }
        sealed class WorldRing
        {
            public LineRenderer Line; public float Start, Duration, From, To; public Color Color;
            public CombatTarget Follow; public int FollowId; public bool Blink; public bool Active;
        }
        sealed class Decal
        {
            public Transform Transform; public MeshRenderer Renderer; public float Start, Duration, From, To, FadeIn; public Color Color; public bool Active;
        }

        BattleSession session;
        Camera cam;
        readonly Dictionary<Soldier, UnitView> views = new Dictionary<Soldier, UnitView>();
        readonly List<UnitView> flashing = new List<UnitView>(MaxFlashes);
        readonly List<Corpse> corpses = new List<Corpse>(128);
        readonly List<Grow> growing = new List<Grow>(64);
        readonly List<RingPop> ringPops = new List<RingPop>(64);
        readonly List<Pop> pops = new List<Pop>(32);
        readonly List<WorldRing> rings = new List<WorldRing>(MaxWorldRings);
        readonly List<Decal> decals = new List<Decal>(MaxDecals);
        readonly List<Renderer> scratch = new List<Renderer>(32);
        MaterialPropertyBlock flashBlock, fxBlock;
        Material glow;
        Mesh disc;
        Transform fxRoot;
        float shakeUntil, shakeAmplitude;
        uint seed = 0x2545F491u;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; shakeLoaded = false; ShakeEnabled = true; }

        public static GameFeel Attach(GameObject host, BattleSession battle, Camera camera)
        {
            var feel = host.GetComponent<GameFeel>();
            if (!feel) feel = host.AddComponent<GameFeel>();
            feel.Initialize(battle, camera);
            return feel;
        }

        void Initialize(BattleSession battle, Camera camera)
        {
            Unsubscribe();
            session = battle; cam = camera; Current = this;
            if (!shakeLoaded) { shakeLoaded = true; try { ShakeEnabled = PlayerPrefs.GetInt(ShakeKey, 1) != 0; } catch (System.Exception) { } }
            flashBlock = new MaterialPropertyBlock(); flashBlock.SetColor(BaseColorId, FlashColor);
            fxBlock = new MaterialPropertyBlock();
            glow = Resources.Load<Material>("TrainingGlow");
            var feedback = session.Feedback;
            feedback.Damaged += OnDamaged;
            feedback.UnitDied += OnDied;
            feedback.SoldierSpawned += OnSpawned;
            feedback.Impacted += OnImpact;
            feedback.Captured += OnCaptured;
        }

        void Unsubscribe()
        {
            if (!session || session.Feedback == null) return;
            var feedback = session.Feedback;
            feedback.Damaged -= OnDamaged;
            feedback.UnitDied -= OnDied;
            feedback.SoldierSpawned -= OnSpawned;
            feedback.Impacted -= OnImpact;
            feedback.Captured -= OnCaptured;
        }

        void OnDestroy()
        {
            Unsubscribe();
            if (Current == this) Current = null;
            // Pooled decals and rings live under fxRoot; destroying the pool root is their
            // lifecycle (they are scene objects), the disc mesh dies with its owner.
            if (fxRoot) Destroy(fxRoot.gameObject);
        }

        public static void SetShakeEnabled(bool value)
        {
            ShakeEnabled = value; shakeLoaded = true;
            try { PlayerPrefs.SetInt(ShakeKey, value ? 1 : 0); PlayerPrefs.Save(); } catch (System.Exception) { }
        }

        bool Active => session && session.Feedback.Enabled && cam;
        bool WorldPaused => !session || session.Paused || session.Winner >= 0;

        bool OnScreen(Vector3 point, float margin = .08f)
        {
            if (!cam) return false;
            var v = cam.WorldToViewportPoint(point);
            return v.z > 0 && v.x > -margin && v.x < 1 + margin && v.y > -margin && v.y < 1 + margin;
        }

        UnitView View(Soldier unit)
        {
            if (views.TryGetValue(unit, out var view) && view.Renderers != null) return view;
            unit.GetComponentsInChildren(true, scratch);
            var lod = unit.GetComponent<UnitPresentationLodView>();
            var proxy = lod ? lod.ProxyRenderer : null;
            int count = 0;
            for (int i = 0; i < scratch.Count; i++) if (Flashable(scratch[i], proxy)) count++;
            var renderers = new Renderer[count]; count = 0;
            for (int i = 0; i < scratch.Count; i++) if (Flashable(scratch[i], proxy)) renderers[count++] = scratch[i];
            scratch.Clear();
            view = new UnitView { Renderers = renderers, Applied = new bool[renderers.Length], Animator = unit.GetComponent<SoldierAnimator>() };
            views[unit] = view;
            return view;
        }

        static bool Flashable(Renderer renderer, Renderer proxy) =>
            renderer && renderer != proxy && !(renderer is LineRenderer) && renderer.gameObject.name != "Soft ground shadow";

        // ---------------------------------------------------------------- events

        void OnDamaged(CombatTarget victim, int attacker, CombatTarget source)
        {
            if (!Active || StrategicMapView.Active || !(victim is Soldier unit) || !unit.IsAlive) return;
            if (!OnScreen(unit.transform.position)) return;
            var view = View(unit);
            if (view.Animator) view.Animator.Hit();
            if (view.FlashUntil < 0 && flashing.Count >= MaxFlashes) return;
            float now = Time.unscaledTime;
            if (view.FlashUntil < 0)
            {
                for (int i = 0; i < view.Renderers.Length; i++)
                {
                    var renderer = view.Renderers[i];
                    // Never clobber a property block owned by other presentation code.
                    view.Applied[i] = renderer && !renderer.HasPropertyBlock();
                    if (view.Applied[i]) renderer.SetPropertyBlock(flashBlock);
                }
                flashing.Add(view);
            }
            view.FlashUntil = now + FlashSeconds; view.FlashId = unit.EntityId;
        }

        void EndFlash(UnitView view)
        {
            for (int i = 0; i < view.Renderers.Length; i++)
            {
                var renderer = view.Renderers[i];
                if (renderer && view.Applied[i]) renderer.SetPropertyBlock(null);
                view.Applied[i] = false;
            }
            view.FlashUntil = -1;
        }

        void OnDied(CombatTarget actor)
        {
            var unit = actor as Soldier;
            if (!Active || !unit) return;
            if (views.TryGetValue(unit, out var view) && view.FlashUntil >= 0) { EndFlash(view); flashing.Remove(view); }
            bool animated = unit.GetComponent<SoldierAnimator>();
            corpses.Add(new Corpse
            {
                Unit = unit, Id = unit.EntityId, DiedAt = session.BattleTime, Origin = unit.transform.position,
                Rotation = unit.transform.rotation, Animated = animated, Tilt = (unit.EntityId & 1) == 0 ? 1 : -1
            });
        }

        void OnSpawned(Soldier unit)
        {
            if (!Active || !unit || session.Clock.TickCount == 0) return;
            unit.transform.localScale = Vector3.one * .6f;
            growing.Add(new Grow { Unit = unit, Id = unit.EntityId, Start = Time.unscaledTime });
            if (OnScreen(unit.transform.position)) Dust(unit.transform.position, .5f);
        }

        void OnImpact(Vector3 point, AttackKind attack, float radius, ImpactKind kind, CombatTarget source)
        {
            if (!Active || StrategicMapView.Active) return;
            bool visible = OnScreen(point);
            if (kind == ImpactKind.Melee) { if (visible) VisualFactory.Impact(point, new Color(1f, .93f, .78f), .2f); return; }
            if (kind == ImpactKind.Instant) { if (visible) VisualFactory.Impact(point, attack, .24f); return; }
            if (attack != AttackKind.Siege) return;
            var ground = point; ground.y = MapLayout.Height(point.x, point.z);
            if (visible) Scorch(ground, Mathf.Max(1.2f, radius * 1.1f));
            if (ShakeEnabled) Shake(ground);
        }

        void OnCaptured(CaptureEvent capture)
        {
            if (!Active) return;
            Color team = VisualFactory.TeamColor(capture.Owner);
            var ground = capture.Position; ground.y = MapLayout.Height(ground.x, ground.z) + .12f;
            if (OnScreen(ground, .3f))
            {
                RentRing(ground, team, 1.2f, capture.CountryCompleted ? 9f : 6.5f, .9f, null, false, .16f);
                if (capture.CountryCompleted) RentRing(ground, team, .6f, 12f, 1.3f, null, false, .1f);
            }
            if (capture.Flag) PopTransform(capture.Flag, .3f, .3f);
        }

        // ---------------------------------------------------------------- public helpers

        /// <summary>Selection feedback: the ring pops 1.25 → 1 with a width ramp.</summary>
        public static void PopRing(LineRenderer ring)
        {
            var feel = Current;
            if (!feel || !ring || !feel.Active) return;
            for (int i = 0; i < feel.ringPops.Count; i++)
                if (feel.ringPops[i].Ring == ring) { var existing = feel.ringPops[i]; existing.Start = Time.unscaledTime; feel.ringPops[i] = existing; return; }
            feel.ringPops.Add(new RingPop { Ring = ring, Start = Time.unscaledTime, Width = ring.widthMultiplier });
            ring.transform.localScale = Vector3.one * 1.25f;
            ring.widthMultiplier = 0;
        }

        /// <summary>Scale pop (1 → 1+amount → 1). Restarting a running pop keeps its original scale.</summary>
        public static void PopTransform(Transform target, float amount = .3f, float duration = .3f, bool vertical = false)
        {
            var feel = Current;
            if (!feel || !target || !feel.Active) return;
            for (int i = 0; i < feel.pops.Count; i++)
                if (feel.pops[i].Target == target) { var existing = feel.pops[i]; existing.Start = Time.unscaledTime; existing.Amount = amount; existing.Duration = duration; feel.pops[i] = existing; return; }
            if (feel.pops.Count >= 64) return;
            feel.pops.Add(new Pop { Target = target, Base = target.localScale, Start = Time.unscaledTime, Duration = duration, Amount = amount, Vertical = vertical });
        }

        /// <summary>Brief red ring on an attack target so an A-click or right-click reads as locked on.</summary>
        public static void FlashTarget(CombatTarget target)
        {
            var feel = Current;
            if (!feel || !target || !feel.Active) return;
            float radius = target is Ship ? 2.6f : target is DefenseTower ? 1.4f : target is Soldier soldier ? Mathf.Max(.55f, UnitCatalog.Get(soldier.Kind).CollisionRadius * 1.35f) : 1f;
            feel.RentRing(target.transform.position, new Color(1f, .22f, .16f), radius, radius, .55f, target, true, .09f);
        }

        /// <summary>Camera offset for this frame; zero when shake is off or idle.</summary>
        public static Vector3 ShakeOffset()
        {
            var feel = Current;
            if (!feel || !ShakeEnabled) return Vector3.zero;
            float remaining = feel.shakeUntil - Time.unscaledTime;
            if (remaining <= 0) return Vector3.zero;
            float strength = feel.shakeAmplitude * (remaining / .25f);
            return new Vector3(feel.Jitter() * strength, feel.Jitter() * strength * .6f, feel.Jitter() * strength);
        }

        float Jitter() { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed & 0xFFFF) / 65535f * 2 - 1; }

        void Shake(Vector3 point)
        {
            var rig = RtsCameraRigFocus(out float zoom);
            Vector3 delta = point - rig; delta.y = 0;
            float reach = zoom * .7f;
            if (delta.sqrMagnitude > reach * reach) return;
            float falloff = 1 - Mathf.Sqrt(delta.sqrMagnitude) / reach;
            float amplitude = .1f * falloff * Mathf.Clamp(zoom / 34f, .6f, 1.4f);
            if (Time.unscaledTime < shakeUntil) amplitude = Mathf.Max(amplitude, shakeAmplitude);
            shakeAmplitude = Mathf.Min(amplitude, .16f); shakeUntil = Time.unscaledTime + .25f;
        }

        Vector3 RtsCameraRigFocus(out float zoom)
        {
            var rig = cam ? cam.GetComponentInParent<RtsCameraRig>() : null;
            if (!rig) rig = FindAnyRig();
            zoom = rig ? rig.TargetZoom : 34;
            return rig ? rig.FocusPoint : cam ? cam.transform.position : Vector3.zero;
        }

        RtsCameraRig cachedRig;
        RtsCameraRig FindAnyRig()
        {
            if (!cachedRig) cachedRig = FindFirstObjectByType<RtsCameraRig>();
            return cachedRig;
        }

        // ---------------------------------------------------------------- pools

        Transform FxRoot()
        {
            if (!fxRoot) { fxRoot = new GameObject("RiskAI game feel FX").transform; fxRoot.SetParent(transform, false); }
            return fxRoot;
        }

        void RentRing(Vector3 point, Color color, float from, float to, float duration, CombatTarget follow, bool blink, float width)
        {
            if (!glow) return;
            WorldRing ring = null;
            for (int i = 0; i < rings.Count; i++) if (!rings[i].Active) { ring = rings[i]; break; }
            if (ring == null)
            {
                if (rings.Count >= MaxWorldRings) return;
                var line = VisualFactory.Ring(FxRoot(), 1, width, color);
                line.sharedMaterial = glow;
                ring = new WorldRing { Line = line };
                rings.Add(ring);
            }
            ring.Active = true; ring.Start = Time.unscaledTime; ring.Duration = duration; ring.From = from; ring.To = to;
            ring.Color = color; ring.Follow = follow; ring.FollowId = follow ? follow.EntityId : 0; ring.Blink = blink;
            ring.Line.widthMultiplier = width;
            ring.Line.transform.position = point; ring.Line.transform.localScale = Vector3.one * from;
            ring.Line.startColor = ring.Line.endColor = color;
            ring.Line.enabled = true;
        }

        Mesh Disc()
        {
            if (disc) return disc;
            const int segments = 18;
            var vertices = new Vector3[segments + 1]; var colors = new Color[segments + 1]; var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero; colors[0] = Color.white;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * .5f, 0, Mathf.Sin(angle) * .5f);
                colors[i + 1] = new Color(1, 1, 1, 0);
                triangles[i * 3] = 0; triangles[i * 3 + 1] = 1 + (i + 1) % segments; triangles[i * 3 + 2] = 1 + i;
            }
            disc = GeneratedResourceOwner.For(transform).Track(new Mesh { name = "Soft FX disc" });
            disc.vertices = vertices; disc.colors = colors; disc.triangles = triangles; disc.RecalculateBounds();
            return disc;
        }

        Decal RentDecal()
        {
            if (!glow) return null;
            Decal oldest = null;
            for (int i = 0; i < decals.Count; i++)
            {
                if (!decals[i].Active) return decals[i];
                if (oldest == null || decals[i].Start < oldest.Start) oldest = decals[i];
            }
            if (decals.Count >= MaxDecals) return oldest;
            var go = new GameObject("FX decal");
            go.transform.SetParent(FxRoot(), false);
            go.AddComponent<MeshFilter>().sharedMesh = Disc();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = glow; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            var decal = new Decal { Transform = go.transform, Renderer = renderer };
            decals.Add(decal);
            return decal;
        }

        void Scorch(Vector3 ground, float size)
        {
            var decal = RentDecal(); if (decal == null) return;
            ShowDecal(decal, ground + Vector3.up * .06f, new Color(.07f, .05f, .035f, .78f), size * 1.6f, size * 1.9f, 5f, .05f);
            decal.Transform.localRotation = Quaternion.Euler(0, (seed = seed * 1664525u + 1013904223u) % 360, 0);
            var flash = RentDecal(); if (flash == null || flash == decal) return;
            ShowDecal(flash, ground + Vector3.up * .1f, new Color(1f, .55f, .18f, .7f), size * .8f, size * 2.6f, .45f, 0);
        }

        void Dust(Vector3 ground, float size)
        {
            var decal = RentDecal(); if (decal == null) return;
            ShowDecal(decal, ground + Vector3.up * .08f, new Color(.72f, .63f, .47f, .55f), size, size * 3.2f, .5f, 0);
        }

        void ShowDecal(Decal decal, Vector3 point, Color color, float from, float to, float duration, float fadeIn)
        {
            decal.Active = true; decal.Start = Time.unscaledTime; decal.Duration = duration; decal.From = from; decal.To = to; decal.FadeIn = fadeIn;
            decal.Color = color; decal.Transform.position = point; decal.Transform.localScale = Vector3.one * from;
            decal.Renderer.gameObject.SetActive(true);
            fxBlock.SetColor(BaseColorId, fadeIn > 0 ? new Color(color.r, color.g, color.b, 0) : color);
            decal.Renderer.SetPropertyBlock(fxBlock);
        }

        // ---------------------------------------------------------------- update

        void LateUpdate()
        {
            if (!session) return;
            float now = Time.unscaledTime;
            UpdateFlashes(now);
            UpdateGrowth(now);
            UpdateCorpses();
            UpdateRingPops(now);
            UpdatePops(now);
            UpdateRings(now);
            UpdateDecals(now);
        }

        void UpdateFlashes(float now)
        {
            for (int i = flashing.Count - 1; i >= 0; i--)
            {
                var view = flashing[i];
                if (now < view.FlashUntil) continue;
                EndFlash(view);
                flashing.RemoveAt(i);
            }
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1;
            t -= 1;
            return 1 + c3 * t * t * t + c1 * t * t;
        }

        void UpdateGrowth(float now)
        {
            for (int i = growing.Count - 1; i >= 0; i--)
            {
                var grow = growing[i];
                bool valid = grow.Unit && grow.Unit.EntityId == grow.Id && grow.Unit.IsAlive;
                float t = Mathf.Clamp01((now - grow.Start) / SpawnSeconds);
                if (!valid || t >= 1)
                {
                    if (valid) grow.Unit.transform.localScale = Vector3.one;
                    growing.RemoveAt(i);
                    continue;
                }
                grow.Unit.transform.localScale = Vector3.one * Mathf.LerpUnclamped(.6f, 1f, EaseOutBack(t));
            }
        }

        void UpdateCorpses()
        {
            float time = session.BattleTime;
            float sinkStart = SoldierPool.CorpseSeconds - 1f;
            for (int i = corpses.Count - 1; i >= 0; i--)
            {
                var corpse = corpses[i];
                var unit = corpse.Unit;
                if (!unit || unit.EntityId != corpse.Id || unit.IsAlive || !unit.gameObject.activeSelf) { corpses.RemoveAt(i); continue; }
                float age = time - corpse.DiedAt;
                var t = unit.transform;
                if (!corpse.Animated)
                {
                    // Procedural knight and mortar have no death clip: topple slightly and settle.
                    float fall = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / .4f));
                    t.rotation = corpse.Rotation * Quaternion.Euler(fall * 10f, 0, fall * 20f * corpse.Tilt);
                }
                float sink = age <= sinkStart ? 0 : Mathf.Clamp01(age - sinkStart);
                float drop = (corpse.Animated ? 0 : .06f * Mathf.Clamp01(age / .4f)) + sink * sink * .7f;
                t.position = corpse.Origin - Vector3.up * drop;
                t.localScale = Vector3.one * Mathf.Lerp(1f, .5f, sink);
            }
        }

        void UpdateRingPops(float now)
        {
            for (int i = ringPops.Count - 1; i >= 0; i--)
            {
                var pop = ringPops[i];
                if (!pop.Ring) { ringPops.RemoveAt(i); continue; }
                float t = Mathf.Clamp01((now - pop.Start) / RingPopSeconds);
                float eased = 1 - (1 - t) * (1 - t);
                pop.Ring.transform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, eased);
                pop.Ring.widthMultiplier = pop.Width * Mathf.Lerp(.15f, 1f, eased);
                if (t >= 1) { pop.Ring.transform.localScale = Vector3.one; pop.Ring.widthMultiplier = pop.Width; ringPops.RemoveAt(i); }
            }
        }

        void UpdatePops(float now)
        {
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var pop = pops[i];
                if (!pop.Target) { pops.RemoveAt(i); continue; }
                float t = Mathf.Clamp01((now - pop.Start) / Mathf.Max(.01f, pop.Duration));
                float k = 1 + pop.Amount * Mathf.Sin(t * Mathf.PI);
                pop.Target.localScale = pop.Vertical ? new Vector3(pop.Base.x, pop.Base.y * k, pop.Base.z) : pop.Base * k;
                if (t >= 1) { pop.Target.localScale = pop.Base; pops.RemoveAt(i); }
            }
        }

        void UpdateRings(float now)
        {
            for (int i = 0; i < rings.Count; i++)
            {
                var ring = rings[i];
                if (!ring.Active) continue;
                float t = Mathf.Clamp01((now - ring.Start) / ring.Duration);
                if (ring.Follow)
                {
                    if (ring.Follow.EntityId != ring.FollowId || !ring.Follow.IsAlive) t = 1;
                    else ring.Line.transform.position = ring.Follow.transform.position;
                }
                if (t >= 1) { ring.Active = false; ring.Line.enabled = false; ring.Follow = null; continue; }
                float eased = 1 - (1 - t) * (1 - t) * (1 - t);
                ring.Line.transform.localScale = Vector3.one * Mathf.Lerp(ring.From, ring.To, eased);
                var color = ring.Color;
                color.a = ring.Blink ? (Mathf.Cos(t * Mathf.PI * 4) * .5f + .5f) * (1 - t * .5f) : 1 - t;
                ring.Line.startColor = ring.Line.endColor = color;
            }
        }

        void UpdateDecals(float now)
        {
            for (int i = 0; i < decals.Count; i++)
            {
                var decal = decals[i];
                if (!decal.Active) continue;
                float age = now - decal.Start;
                if (age >= decal.Duration) { decal.Active = false; decal.Renderer.gameObject.SetActive(false); continue; }
                float t = age / decal.Duration;
                float grow = decal.FadeIn > 0 ? Mathf.Clamp01(age / .12f) : 1 - (1 - t) * (1 - t);
                decal.Transform.localScale = Vector3.one * Mathf.Lerp(decal.From, decal.To, grow);
                float alpha = decal.FadeIn > 0
                    ? Mathf.Clamp01(age / decal.FadeIn) * (t < .6f ? 1 : 1 - (t - .6f) / .4f)
                    : 1 - t;
                var color = decal.Color; color.a *= alpha;
                fxBlock.SetColor(BaseColorId, color);
                decal.Renderer.SetPropertyBlock(fxBlock);
            }
        }
    }
}
