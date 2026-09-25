using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// Sound ids. Each is the snake_case id of a clip in Resources/Audio/clips.json,
    /// which lists its files, volume, budget and priority.
    /// </summary>
    public enum SfxId
    {
        HitSword, HitLance, ShotCrossbow, ShotRifle, HitArrow, MagicBolt, MagicImpact, MortarFire, Explosion, ShipCannon, Death, Sink,
        OrderMove, OrderAttack, UiClick, SelectHarbor, Purchase, NoGold, UnitTrained, CityCaptured, CityLost, CountryCompleted, Income,
        UnderAttack, Victory, Defeat, Chat, CountryLost
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
        // Resources/Audio/clips.json is the single source for every clip: files, volume, budget and priority.
        // Each SfxId maps to the clip whose id is its snake_case name; both directions must match.
        sealed class ClipManifest
        {
            [JsonProperty("loader", Required = Required.Always)] public string Loader;
            [JsonProperty("folder", Required = Required.Always)] public string Folder;
            [JsonProperty("style", Required = Required.Always)] public string Style;
            [JsonProperty("clips", Required = Required.Always)] public List<ClipEntry> Clips;
        }
        sealed class ClipEntry
        {
            [JsonProperty("id", Required = Required.Always)] public string Id;
            [JsonProperty("duration_seconds", Required = Required.Always)] public float DurationSeconds;
            [JsonProperty("spatial", Required = Required.Always)] public string Spatial;
            [JsonProperty("volume", Required = Required.Always)] public float Volume;
            [JsonProperty("trigger", Required = Required.Always)] public string Trigger;
            [JsonProperty("files", Required = Required.Always)] public List<ClipFile> Files;
            [JsonProperty("budget", Required = Required.Always)] public int Budget;
            [JsonProperty("priority", Required = Required.Always)] public int Priority;
        }
        sealed class ClipFile
        {
            [JsonProperty("file", Required = Required.Always)] public string File;
            [JsonProperty("prompt", Required = Required.Always)] public string Prompt;
        }
        readonly struct ClipInfo
        {
            public readonly string Id; public readonly string[] Files; public readonly float Volume; public readonly int Budget; public readonly int Priority;
            public ClipInfo(string id, string[] files, float volume, int budget, int priority)
            { Id = id; Files = files; Volume = volume; Budget = budget; Priority = priority; }
        }

        const string ClipResource = "Audio/clips";
        static readonly int ClipCount = Enum.GetValues(typeof(SfxId)).Length;
        static ClipInfo[] table;
        static Dictionary<string, SfxId> byClipId;
        // Indexed by SfxId after the name match, so enum order never has to follow the file.
        static ClipInfo[] Table => table ??= LoadTable();

        static ClipInfo[] LoadTable()
        {
            var asset = Resources.Load<TextAsset>(ClipResource);
            if (!asset) throw new InvalidOperationException("Resources/" + ClipResource + ".json is missing.");
            var manifest = JsonConvert.DeserializeObject<ClipManifest>(asset.text, new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
            var ids = new Dictionary<string, SfxId>(ClipCount, StringComparer.Ordinal);
            foreach (SfxId sound in Enum.GetValues(typeof(SfxId))) ids.Add(ClipId(sound), sound);
            var rows = new ClipInfo[ClipCount];
            var seen = new bool[ClipCount];
            foreach (var clip in manifest.Clips)
            {
                if (!ids.TryGetValue(clip.Id, out var sound)) throw new InvalidOperationException("clips.json: clip \"" + clip.Id + "\" has no SfxId.");
                if (seen[(int)sound]) throw new InvalidOperationException("clips.json: duplicate clip \"" + clip.Id + "\".");
                if (clip.Spatial != "2D" && clip.Spatial != "3D") throw new InvalidOperationException("clips.json: \"" + clip.Id + "\" spatial must be 2D or 3D.");
                if (clip.Files.Count == 0) throw new InvalidOperationException("clips.json: \"" + clip.Id + "\" lists no files.");
                var files = new string[clip.Files.Count];
                for (int i = 0; i < files.Length; i++) files[i] = System.IO.Path.GetFileNameWithoutExtension(clip.Files[i].File);
                seen[(int)sound] = true;
                rows[(int)sound] = new ClipInfo(clip.Id, files, clip.Volume, clip.Budget, clip.Priority);
            }
            for (int i = 0; i < ClipCount; i++)
                if (!seen[i]) throw new InvalidOperationException("clips.json: no clip \"" + ClipId((SfxId)i) + "\" for SfxId." + (SfxId)i + ".");
            byClipId = ids;
            return rows;
        }

        /// <summary>The clips.json id of <paramref name="sound"/>: its snake_case name (HitSword is hit_sword).</summary>
        public static string ClipId(SfxId sound)
        {
            string name = sound.ToString();
            var text = new System.Text.StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                if (char.IsUpper(name[i]) && i > 0) text.Append('_');
                text.Append(char.ToLowerInvariant(name[i]));
            }
            return text.ToString();
        }

        /// <summary>The SfxId of a clips.json id, as named by units.json deathSound. Throws for an unknown id.</summary>
        public static SfxId FromClipId(string id)
        {
            _ = Table;
            if (id != null && byClipId.TryGetValue(id, out var sound)) return sound;
            throw new InvalidOperationException("clips.json has no clip \"" + id + "\".");
        }

        /// <summary>Loads every file of <paramref name="sound"/>; throws when clips.json names a file missing from Resources/Audio.</summary>
        public static int LoadedVariants(SfxId sound) => Clips((int)sound).Length;

        // One presentation map from units.json WeaponSound to the clip played on fire and on impact.
        static readonly (WeaponSound Sound, bool Fires, SfxId Fired, SfxId Impact)[] WeaponAudio = {
            (WeaponSound.Blade, false, default, SfxId.HitSword),
            (WeaponSound.Lance, false, default, SfxId.HitLance),
            (WeaponSound.Bow, true, SfxId.ShotCrossbow, SfxId.HitArrow),
            (WeaponSound.Firearm, true, SfxId.ShotRifle, SfxId.HitArrow),
            (WeaponSound.Magic, true, SfxId.MagicBolt, SfxId.MagicImpact),
            (WeaponSound.Mortar, true, SfxId.MortarFire, SfxId.Explosion),
            (WeaponSound.Cannon, true, SfxId.ShipCannon, SfxId.HitArrow)
        };
        static bool WeaponClip(WeaponSound sound, out bool fires, out SfxId fired, out SfxId impact)
        {
            for (int i = 0; i < WeaponAudio.Length; i++)
                if (WeaponAudio[i].Sound == sound) { fires = WeaponAudio[i].Fires; fired = WeaponAudio[i].Fired; impact = WeaponAudio[i].Impact; return true; }
            fires = false; fired = default; impact = SfxId.HitArrow; return false;
        }
        public const int WorldVoices = 16;
        public const int InterfaceVoices = 2;
        const string MasterKey = "riskai.audio.master", EffectsKey = "riskai.audio.sfx", MuteKey = "riskai.audio.mute";

        public static Sfx Current { get; private set; }
        public static float MasterVolume { get; private set; } = .8f;
        public static float EffectsVolume { get; private set; } = .8f;
        public static bool Muted { get; private set; }

        static bool preferencesLoaded;
        static AudioClip[][] clips;
        readonly SfxThrottle throttle = new SfxThrottle(ClipCount);
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
        static void ResetStatics() { Current = null; clips = null; table = null; byClipId = null; preferencesLoaded = false; }

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
            feedback.UnitDied += OnDied;
        }

        void OnDestroy()
        {
            if (session && session.Feedback != null)
            {
                session.Feedback.WeaponFired -= OnFired;
                session.Feedback.Impacted -= OnImpact;
                session.Feedback.UnitDied -= OnDied;
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
            if (clips == null) clips = new AudioClip[ClipCount][];
            var loaded = clips[index];
            if (loaded != null) return loaded;
            var files = Table[index].Files;
            var list = new AudioClip[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                list[i] = Resources.Load<AudioClip>("Audio/" + files[i]);
                if (!list[i]) throw new InvalidOperationException("clips.json names Resources/Audio/" + files[i] + " but the file is missing.");
            }
            return clips[index] = list;
        }

        uint Next() { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return seed; }
        float Jitter(float amount) => ((Next() & 0xFFFF) / 65535f * 2 - 1) * amount;

        bool Prepare(SfxId id, out AudioClip clip, out ClipInfo info)
        {
            clip = null; info = default;
            int index = (int)id;
            info = Table[index];
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

        void OnFired(CombatTarget source, Vector3 from, Vector3 to, AttackKind attack)
        {
            var sound = source ? source.AttackWeapon.Sound : WeaponSound.Bow;
            if (WeaponClip(sound, out bool fires, out var fired, out _)) { if (fires) At(fired, from); }
        }

        void OnImpact(Vector3 point, AttackKind attack, float radius, ImpactKind kind, CombatTarget source)
        {
            var sound = source ? source.AttackWeapon.Sound : WeaponSound.Blade;
            if (!WeaponClip(sound, out _, out _, out var impact)) impact = SfxId.HitArrow;
            At(impact, point, impact == SfxId.HitArrow ? .8f : 1f);
        }

        // units.json deathSound picks the clip: a sinking hull for ships, a fall for everyone else.
        void OnDied(CombatTarget unit) { if (unit) At(FromClipId(unit.Type.DeathSound), unit.transform.position); }

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
