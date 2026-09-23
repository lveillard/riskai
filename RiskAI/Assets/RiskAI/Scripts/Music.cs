using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// Quiet shuffled background playlist from Resources/Music/melancholia-N. Master volume,
    /// mute and focus come from the shared AudioListener that <see cref="Sfx"/> drives.
    /// </summary>
    public sealed class Music : MonoBehaviour
    {
        const string VolumeKey = "riskai.music.volume", EnabledKey = "riskai.music.enabled";
        const float FadeInSeconds = 3, CrossfadeSeconds = 2, DefaultVolume = .15f;

        public static Music Current { get; private set; }
        public static float MusicVolume { get; private set; } = DefaultVolume;
        public static bool MusicEnabled { get; private set; } = true;
        static bool preferencesLoaded;

        readonly AudioSource[] sources = new AudioSource[2];
        AudioClip[] tracks;
        int[] order;
        int orderIndex = -1, active, lastTrack = -1;
        float fade = 1, fadeSpeed = 1 / FadeInSeconds, retryAt;
        bool started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; preferencesLoaded = false; }

        public static Music Attach(GameObject host)
        {
            LoadPreferences();
            if (Application.isBatchMode) return null;
            if (Current) return Current;
            var go = new GameObject("RiskAI music");
            go.transform.SetParent(host.transform, false);
            return go.AddComponent<Music>();
        }

        void Awake()
        {
            Current = this;
            var list = new System.Collections.Generic.List<AudioClip>();
            for (int n = 1; ; n++) { var clip = Resources.Load<AudioClip>("Music/melancholia-" + n); if (!clip) break; list.Add(clip); }
            tracks = list.ToArray(); order = new int[tracks.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false; source.loop = false; source.spatialBlend = 0; source.priority = 0;
                source.bypassReverbZones = true; source.bypassEffects = true; source.volume = 0;
                sources[i] = source;
            }
            if (tracks.Length > 0) PlayNext(true);
        }

        void OnDestroy() { if (Current == this) Current = null; }

        void PlayNext(bool first)
        {
            if (++orderIndex >= order.Length || orderIndex == 0) Shuffle();
            int track = order[orderIndex];
            if (!first) active = 1 - active;
            var source = sources[active];
            source.clip = tracks[track]; source.volume = 0; source.Play();
            lastTrack = track; fade = 0; fadeSpeed = 1 / (first ? FadeInSeconds : CrossfadeSeconds); started = false; retryAt = Time.unscaledTime + 2;
        }

        void Shuffle()
        {
            orderIndex = 0;
            for (int i = 0; i < order.Length; i++) order[i] = i;
            for (int i = order.Length - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (order[i], order[j]) = (order[j], order[i]); }
            // Never repeat the track that just ended across a reshuffle.
            if (order.Length > 1 && order[0] == lastTrack) (order[0], order[1]) = (order[1], order[0]);
        }

        void Update()
        {
            if (tracks.Length == 0) return;
            fade = Mathf.Min(1, fade + Time.unscaledDeltaTime * fadeSpeed);
            float target = MusicEnabled ? MusicVolume : 0;
            var current = sources[active]; var previous = sources[1 - active];
            current.volume = target * fade;
            previous.volume = target * (1 - fade);
            if (fade >= 1 && previous.isPlaying) previous.Stop();
            if (current.isPlaying && current.time > 0) started = true;
            // Crossfade shortly before the end. A streaming clip reports length 0 (or a stale time)
            // until it has loaded, so only trust the clock once the track is loaded and well under way.
            var clip = current.clip;
            bool nearEnd = clip && clip.loadState == AudioDataLoadState.Loaded && clip.length > CrossfadeSeconds * 4 &&
                current.time > CrossfadeSeconds && clip.length - current.time <= CrossfadeSeconds;
            if (current.isPlaying && started && nearEnd) PlayNext(false);
            else if (!current.isPlaying && started) PlayNext(false);
            // Browsers may block audio until the first gesture: retry the same track instead of skipping through the list.
            else if (!current.isPlaying && Time.unscaledTime >= retryAt) { retryAt = Time.unscaledTime + 2; current.Play(); }
        }

        static void LoadPreferences()
        {
            if (preferencesLoaded) return;
            preferencesLoaded = true;
            try { MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, DefaultVolume)); MusicEnabled = PlayerPrefs.GetInt(EnabledKey, 1) != 0; }
            catch (System.Exception) { }
        }

        static void SavePreferences()
        {
            try { PlayerPrefs.SetFloat(VolumeKey, MusicVolume); PlayerPrefs.SetInt(EnabledKey, MusicEnabled ? 1 : 0); PlayerPrefs.Save(); }
            catch (System.Exception) { }
        }

        public static void SetMusicVolume(float value) { LoadPreferences(); MusicVolume = Mathf.Clamp01(value); SavePreferences(); }
        public static void SetMusicEnabled(bool value) { LoadPreferences(); MusicEnabled = value; SavePreferences(); }
        public static bool ToggleMusic() { SetMusicEnabled(!MusicEnabled); return MusicEnabled; }
    }
}
