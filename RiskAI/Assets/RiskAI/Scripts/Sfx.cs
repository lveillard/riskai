using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// Sound ids. Files live in Resources/Audio/&lt;id&gt; or &lt;id&gt;_1..&lt;id&gt;_12 (mp3/ogg/wav);
    /// see docs/AUDIO-PROMPTS.md and Resources/Audio/clips.json.
    /// </summary>
    public enum SfxId
    {
        HitSword, HitLance, ShotCrossbow, ShotRifle, HitArrow, MagicBolt, MagicImpact, MortarFire, Explosion, ShipCannon, Death,
        OrderMove, OrderAttack, UiClick, Purchase, NoGold, UnitTrained, CityCaptured, CityLost, CountryCompleted, Income,
        UnderAttack, Victory, Defeat, Chat,
        // Appended: ordinals index Table below.
        CountryLost
    }

    /// <summary>Per-clip rate limit: at most <c>budget</c> plays inside a sliding window. Pure and allocation-free.</summary>
    public sealed class SfxThrottle
    {
        public const float WindowSeconds = .06f;
        readonly float[][] recent;
        public SfxThrottle(int clipCount, int maxBudget = 4)
        {
            recent = new float[clipCount][];
            for (int i = 0; i < clipCount; i++)
            {
                recent[i] = new float[maxBudget];
                for (int j = 0; j < maxBudget; j++) recent[i][j] = float.NegativeInfinity;
            }
        }

        /// <summary>Consumes one play for <paramref name="clip"/> if fewer than <paramref name="budget"/> happened in the last window.</summary>
        public bool TryPlay(int clip, int budget, float now, float window = WindowSeconds)
        {
            if (clip < 0 || clip >= recent.Length) return false;
            var slots = recent[clip];
            budget = Mathf.Clamp(budget, 1, slots.Length);
            int used = 0, oldest = 0;
            for (int i = 0; i < budget; i++)
            {
                if (now - slots[i] < window) used++;
                if (slots[i] < slots[oldest]) oldest = i;
            }
            if (used >= budget) return false;
            slots[oldest] = now;
            return true;
        }
    }

    /// <summary>
    /// Pooled audio. World sounds use 16 3D voices heard around the camera focus;
    /// interface sounds use two 2D voices. Missing clips are skipped silently.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        readonly struct ClipInfo
        {
            public readonly string Id; public readonly bool Spatial; public readonly float Volume; public readonly int Budget; public readonly int Priority;
            public ClipInfo(string id, bool spatial, float volume, int budget, int priority) { Id = id; Spatial = spatial; Volume = volume; Budget = budget; Priority = priority; }
        }

        // Keep in the same order as SfxId and in sync with Resources/Audio/clips.json.
        static readonly ClipInfo[] Table = {
            new ClipInfo("hit_sword", true, .8f, 3, 1), new ClipInfo("hit_lance", true, .85f, 2, 1),
            new ClipInfo("shot_crossbow", true, .6f, 3, 1), new ClipInfo("shot_rifle", true, .65f, 2, 1),
            new ClipInfo("hit_arrow", true, .45f, 2, 0), new ClipInfo("magic_bolt", true, .6f, 2, 1),
            new ClipInfo("magic_impact", true, .6f, 2, 1), new ClipInfo("mortar_fire", true, .8f, 2, 2),
            new ClipInfo("explosion", true, .9f, 2, 3), new ClipInfo("ship_cannon", true, .85f, 2, 2),
            new ClipInfo("death", true, .55f, 2, 1),
            new ClipInfo("order_move", false, .45f, 1, 4), new ClipInfo("order_attack", false, .5f, 1, 4),
            new ClipInfo("ui_click", false, .35f, 1, 4), new ClipInfo("purchase", false, .55f, 1, 4),
            new ClipInfo("no_gold", false, .55f, 1, 4), new ClipInfo("unit_trained", false, .5f, 1, 3),
            new ClipInfo("city_captured", false, .7f, 1, 5), new ClipInfo("city_lost", false, .7f, 1, 5),
            new ClipInfo("country_completed", false, .8f, 1, 6), new ClipInfo("income", false, .45f, 1, 3),
            new ClipInfo("under_attack", false, .75f, 1, 5), new ClipInfo("victory", false, .85f, 1, 7),
            new ClipInfo("defeat", false, .85f, 1, 7), new ClipInfo("chat", false, .4f, 1, 3),
            new ClipInfo("country_lost", false, .8f, 1, 6)
        };
        public const int WorldVoices = 16;
        public const int InterfaceVoices = 2;
        const string MasterKey = "riskai.audio.master", EffectsKey = "riskai.audio.sfx", MuteKey = "riskai.audio.mute";

        public static Sfx Current { get; private set; }
        public static float MasterVolume { get; private set; } = .8f;
        public static float EffectsVolume { get; private set; } = .8f;
        public static bool Muted { get; private set; }

        static bool preferencesLoaded;
        static AudioClip[][] clips;
        readonly SfxThrottle throttle = new SfxThrottle(Table.Length);
        readonly AudioSource[] world = new AudioSource[WorldVoices];
        readonly int[] worldPriority = new int[WorldVoices];
        readonly float[] worldStarted = new float[WorldVoices];
        readonly AudioSource[] ui = new AudioSource[InterfaceVoices];
        int nextUi;
        uint seed = 0x9E3779B9u;
        BattleSession session;
        RtsCameraRig rig;
        Camera cam;
        Transform listener;
        AudioListener ownListener, cameraListener;
        bool focused = true;
        float audibleRadius = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; clips = null; preferencesLoaded = false; }

        public static Sfx Attach(GameObject host, BattleSession battle, RtsCameraRig cameraRig, Camera camera)
        {
            var go = new GameObject("RiskAI audio");
            go.transform.SetParent(host.transform, false);
            var sfx = go.AddComponent<Sfx>();
            sfx.Initialize(battle, cameraRig, camera);
            return sfx;
        }

        void Initialize(BattleSession battle, RtsCameraRig cameraRig, Camera camera)
        {
            session = battle; rig = cameraRig; cam = camera; Current = this;
            LoadPreferences();
            // Hear the battle from the camera focus on the ground, not from the camera
            // 80+ units above it: rolloff then matches what is on screen at any zoom.
            listener = new GameObject("Audio listener (camera focus)").transform;
            listener.SetParent(transform, false);
            cameraListener = camera ? camera.GetComponent<AudioListener>() : null;
            if (cameraListener) cameraListener.enabled = false;
            ownListener = listener.gameObject.AddComponent<AudioListener>();
            for (int i = 0; i < WorldVoices; i++)
            {
                var voice = new GameObject("World voice " + i).AddComponent<AudioSource>();
                voice.transform.SetParent(transform, false);
                voice.playOnAwake = false; voice.spatialBlend = 1; voice.rolloffMode = AudioRolloffMode.Linear;
                voice.dopplerLevel = 0; voice.spread = 40; voice.minDistance = 12; voice.maxDistance = 80;
                world[i] = voice; worldPriority[i] = -1;
            }
            for (int i = 0; i < InterfaceVoices; i++)
            {
                var voice = new GameObject("Interface voice " + i).AddComponent<AudioSource>();
                voice.transform.SetParent(transform, false);
                voice.playOnAwake = false; voice.spatialBlend = 0;
                ui[i] = voice;
            }
            ApplyListenerVolume();
            var feedback = session.Feedback;
            feedback.WeaponFired += OnFired;
            feedback.Impacted += OnImpact;
            feedback.SoldierDied += OnDied;
        }

        void OnDestroy()
        {
            if (session && session.Feedback != null)
            {
                session.Feedback.WeaponFired -= OnFired;
                session.Feedback.Impacted -= OnImpact;
                session.Feedback.SoldierDied -= OnDied;
            }
            if (Current == this) Current = null;
            if (cameraListener) cameraListener.enabled = true;
        }

        static void LoadPreferences()
        {
            if (preferencesLoaded) return;
            preferencesLoaded = true;
            try
            {
                MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterKey, .8f));
                EffectsVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(EffectsKey, .8f));
                Muted = PlayerPrefs.GetInt(MuteKey, 0) != 0;
            }
            catch (System.Exception) { }
        }

        static void SavePreferences()
        {
            try { PlayerPrefs.SetFloat(MasterKey, MasterVolume); PlayerPrefs.SetFloat(EffectsKey, EffectsVolume); PlayerPrefs.SetInt(MuteKey, Muted ? 1 : 0); PlayerPrefs.Save(); }
            catch (System.Exception) { }
        }

        public static void SetMasterVolume(float value) { MasterVolume = Mathf.Clamp01(value); SavePreferences(); if (Current) Current.ApplyListenerVolume(); }
        public static void SetEffectsVolume(float value) { EffectsVolume = Mathf.Clamp01(value); SavePreferences(); }
        public static void SetMuted(bool value) { Muted = value; SavePreferences(); if (Current) Current.ApplyListenerVolume(); }

        // Batch runs (automated tests, builds) keep every audio decision but never make noise.
        void ApplyListenerVolume() => AudioListener.volume = Muted || !focused || Application.isBatchMode ? 0 : MasterVolume;

        void OnApplicationFocus(bool hasFocus) { focused = hasFocus; ApplyListenerVolume(); }
        void OnApplicationPause(bool paused) { focused = !paused; ApplyListenerVolume(); }

        void LateUpdate()
        {
            if (!listener || !cam) return;
            Vector3 focus = rig ? rig.FocusPoint : cam.transform.position;
            float zoom = rig ? Mathf.Max(8, rig.TargetZoom) : 34;
            // A low listener keeps left/right panning readable at the oblique RTS angle.
            listener.position = focus + Vector3.up * (zoom * .25f);
            var forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (forward.sqrMagnitude > .001f) listener.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            audibleRadius = zoom * 2.2f;
        }

        static AudioClip[] Clips(int index)
        {
            if (clips == null) clips = new AudioClip[Table.Length][];
            var loaded = clips[index];
            if (loaded != null) return loaded;
            var list = new List<AudioClip>(4);
            string id = Table[index].Id;
            var single = Resources.Load<AudioClip>("Audio/" + id);
            if (single) list.Add(single);
            for (int variant = 1; variant <= 12; variant++)
            {
                var clip = Resources.Load<AudioClip>("Audio/" + id + "_" + variant);
                if (clip) list.Add(clip);
            }
            return clips[index] = list.ToArray();
        }

        uint Next() { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return seed; }
        float Jitter(float amount) => ((Next() & 0xFFFF) / 65535f * 2 - 1) * amount;

        bool Prepare(SfxId id, out AudioClip clip, out ClipInfo info)
        {
            int index = (int)id; clip = null; info = Table[index];
            if (Muted || EffectsVolume <= 0) return false;
            var available = Clips(index);
            if (available.Length == 0) return false;
            if (!throttle.TryPlay(index, info.Budget, Time.unscaledTime)) return false;
            clip = available[available.Length == 1 ? 0 : (int)(Next() % (uint)available.Length)];
            return true;
        }

        /// <summary>World sound at <paramref name="point"/>; culled when far outside the audible area.</summary>
        public static void At(SfxId id, Vector3 point, float volume = 1)
        {
            var sfx = Current;
            if (!sfx || !sfx.isActiveAndEnabled || !sfx.listener) return;
            if (sfx.session && !sfx.session.Feedback.Enabled) return;
            if (StrategicMapView.Active) volume *= .35f;
            Vector3 delta = point - sfx.listener.position; delta.y = 0;
            if (delta.sqrMagnitude > sfx.audibleRadius * sfx.audibleRadius) return;
            if (!sfx.Prepare(id, out var clip, out var info)) return;
            int voice = sfx.PickWorldVoice(info.Priority);
            if (voice < 0) return;
            var source = sfx.world[voice];
            source.transform.position = point;
            source.minDistance = sfx.audibleRadius * .2f; source.maxDistance = sfx.audibleRadius;
            source.clip = clip; source.volume = Mathf.Clamp01(info.Volume * volume * EffectsVolume * (1 + sfx.Jitter(.1f)));
            source.pitch = 1 + sfx.Jitter(.06f);
            source.Play();
            sfx.worldPriority[voice] = info.Priority; sfx.worldStarted[voice] = Time.unscaledTime;
        }

        /// <summary>Interface sound (2D).</summary>
        public static void Ui(SfxId id, float volume = 1)
        {
            var sfx = Current;
            if (!sfx || !sfx.isActiveAndEnabled) return;
            if (sfx.session && !sfx.session.Feedback.Enabled) return;
            if (!sfx.Prepare(id, out var clip, out var info)) return;
            var source = sfx.ui[sfx.nextUi]; sfx.nextUi = (sfx.nextUi + 1) % InterfaceVoices;
            source.pitch = 1 + sfx.Jitter(.03f);
            source.PlayOneShot(clip, Mathf.Clamp01(info.Volume * volume * EffectsVolume));
        }

        // ---------------------------------------------------------------- world events

        static bool Mounted(UnitKind kind) => UnitCatalog.Get(kind).Silhouette == UnitSilhouette.Mounted;
        static bool Firearm(UnitKind kind) => kind == UnitKind.MarinePrivate || kind == UnitKind.EliteRifleman;

        void OnFired(CombatTarget source, Vector3 from, Vector3 to, AttackKind attack)
        {
            if (source is Ship) At(SfxId.ShipCannon, from);
            else if (attack == AttackKind.Siege) At(SfxId.MortarFire, from);
            else if (attack == AttackKind.Magic) At(SfxId.MagicBolt, from);
            else if (source is Soldier soldier && Firearm(soldier.Kind)) At(SfxId.ShotRifle, from);
            else At(SfxId.ShotCrossbow, from);
        }

        void OnImpact(Vector3 point, AttackKind attack, float radius, ImpactKind kind, CombatTarget source)
        {
            if (kind == ImpactKind.Melee) At(source is Soldier soldier && Mounted(soldier.Kind) ? SfxId.HitLance : SfxId.HitSword, point);
            else if (attack == AttackKind.Siege) At(SfxId.Explosion, point);
            else if (attack == AttackKind.Magic) At(SfxId.MagicImpact, point);
            else At(SfxId.HitArrow, point, .8f);
        }

        void OnDied(Soldier unit) { if (unit) At(SfxId.Death, unit.transform.position); }

        int PickWorldVoice(int priority)
        {
            int steal = -1; float oldest = float.MaxValue;
            for (int i = 0; i < WorldVoices; i++)
            {
                if (!world[i].isPlaying) return i;
                if (worldPriority[i] <= priority && worldStarted[i] < oldest) { oldest = worldStarted[i]; steal = i; }
            }
            return steal;
        }
    }
}
