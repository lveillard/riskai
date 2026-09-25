using System;
using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    /// <summary>Presentation category of a log line. Decides its colour, sound and toast.</summary>
    public enum MessageKind { Info, Income, Capture, Country, Loss, Attack, NoGold, Purchase, Chat, Victory, Defeat }

    public enum ImpactKind { Melee, Instant, Projectile }

    public readonly struct MessageEntry
    {
        public readonly string Text;
        public readonly MessageKind Kind;
        public readonly int Team;
        public readonly bool HasFocus;
        public readonly Vector3 Focus;
        public readonly float Time;
        public readonly int Repeat;
        public MessageEntry(string text, MessageKind kind, int team, bool hasFocus, Vector3 focus, float time, int repeat = 1)
        { Text = text; Kind = kind; Team = team; HasFocus = hasFocus; Focus = focus; Time = time; Repeat = repeat; }
        public MessageEntry At(float time, int repeat) => new MessageEntry(Text, Kind, Team, HasFocus, Focus, time, repeat);
    }

    public readonly struct CaptureEvent
    {
        public readonly Vector3 Position;
        public readonly int Previous, Owner, Country;
        public readonly string Name;
        public readonly Transform Flag;
        public readonly bool Harbor, CountryCompleted, CountryLost;
        public CaptureEvent(Vector3 position, int previous, int owner, string name, Transform flag, bool harbor,
            int country, bool countryCompleted, bool countryLost)
        {
            Position = position; Previous = previous; Owner = owner; Name = name; Flag = flag; Harbor = harbor;
            Country = country; CountryCompleted = countryCompleted; CountryLost = countryLost;
        }
    }

    /// <summary>
    /// Bounded, newest-first log of recent HUD lines. Time is supplied by the caller,
    /// so ordering, merging and fading are deterministic and testable.
    /// </summary>
    public sealed class MessageLog
    {
        public const int Capacity = 8;
        public const float HoldSeconds = 5f;
        public const float FadeSeconds = 1f;
        public const float LifetimeSeconds = HoldSeconds + FadeSeconds;
        readonly MessageEntry[] entries = new MessageEntry[Capacity];
        int head, count;
        public int Count => count;
        /// <summary>Changes whenever a line is added or merged.</summary>
        public int Revision { get; private set; }

        /// <summary>0 is the newest entry.</summary>
        public MessageEntry this[int newest]
        {
            get
            {
                if (newest < 0 || newest >= count) throw new ArgumentOutOfRangeException(nameof(newest));
                return entries[(head - 1 - newest + Capacity * 2) % Capacity];
            }
        }

        public void Add(MessageEntry entry)
        {
            if (string.IsNullOrEmpty(entry.Text)) return;
            Revision++;
            // Repeated feedback ("not enough gold" while tapping) refreshes one line instead of flooding.
            if (count > 0)
            {
                int last = (head - 1 + Capacity) % Capacity;
                var previous = entries[last];
                if (previous.Text == entry.Text && previous.Kind == entry.Kind && entry.Time - previous.Time < HoldSeconds)
                {
                    entries[last] = entry.At(entry.Time, previous.Repeat + 1);
                    return;
                }
            }
            entries[head] = entry;
            head = (head + 1) % Capacity;
            if (count < Capacity) count++;
        }

        public void Clear() { count = 0; head = 0; Revision++; }

        public static float Alpha(float age)
        {
            if (age < 0) return 1;
            if (age < HoldSeconds) return 1;
            if (age >= LifetimeSeconds) return 0;
            return 1 - (age - HoldSeconds) / FadeSeconds;
        }

        /// <summary>Number of newest entries still visible at <paramref name="now"/>, at most <paramref name="max"/>.</summary>
        public int VisibleCount(float now, int max)
        {
            int visible = 0;
            for (int i = 0; i < count && visible < max; i++)
            {
                if (Alpha(now - this[i].Time) <= 0) break;
                visible++;
            }
            return visible;
        }
    }

    /// <summary>"Under attack" throttling: one alert per map area every <see cref="ThrottleSeconds"/>.</summary>
    public sealed class AttackAlerts
    {
        public const float ThrottleSeconds = 8f;
        public const float JumpWindowSeconds = 30f;
        public const float CellSize = 36f;
        public const int PingCapacity = 6;
        public const float PingSeconds = 2.4f;
        readonly Dictionary<long, float> lastByCell = new Dictionary<long, float>();
        readonly Vector3[] pingPositions = new Vector3[PingCapacity];
        readonly float[] pingTimes = new float[PingCapacity];
        int nextPing;
        public bool HasAlert { get; private set; }
        public Vector3 LastPosition { get; private set; }
        public float LastTime { get; private set; } = float.NegativeInfinity;

        public AttackAlerts() { for (int i = 0; i < PingCapacity; i++) pingTimes[i] = float.NegativeInfinity; }

        static long Cell(int x, int z) => ((long)x << 32) ^ (uint)z;

        /// <summary>
        /// Records damage at <paramref name="position"/>. Returns true when it should raise a new alert:
        /// no alert was raised in this cell or its eight neighbours during the throttle window.
        /// </summary>
        public bool TryRaise(Vector3 position, float now)
        {
            int cx = Mathf.FloorToInt(position.x / CellSize), cz = Mathf.FloorToInt(position.z / CellSize);
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    if (lastByCell.TryGetValue(Cell(cx + dx, cz + dz), out float last) && now - last < ThrottleSeconds)
                        return false;
            lastByCell[Cell(cx, cz)] = now;
            HasAlert = true; LastPosition = position; LastTime = now;
            pingPositions[nextPing] = position; pingTimes[nextPing] = now;
            nextPing = (nextPing + 1) % PingCapacity;
            if (lastByCell.Count > 256) Prune(now);
            return true;
        }

        readonly List<long> expired = new List<long>();
        void Prune(float now)
        {
            expired.Clear();
            foreach (var pair in lastByCell) if (now - pair.Value >= ThrottleSeconds) expired.Add(pair.Key);
            for (int i = 0; i < expired.Count; i++) lastByCell.Remove(expired[i]);
        }

        public bool CanJump(float now) => HasAlert && now - LastTime <= JumpWindowSeconds;

        /// <summary>Ping progress in [0,1) for slot <paramref name="index"/>, or -1 when inactive.</summary>
        public float PingProgress(int index, float now, out Vector3 position)
        {
            position = pingPositions[index];
            float age = now - pingTimes[index];
            return age >= 0 && age < PingSeconds ? age / PingSeconds : -1;
        }

        public void Reset()
        {
            lastByCell.Clear(); HasAlert = false; LastTime = float.NegativeInfinity;
            for (int i = 0; i < PingCapacity; i++) pingTimes[i] = float.NegativeInfinity;
        }
    }

    /// <summary>
    /// Per-battle presentation hub. Simulation code raises these only while presentation is
    /// enabled; subscribers (HUD, effects, audio) never feed anything back into rules.
    /// </summary>
    public sealed class BattleFeedback
    {
        readonly BattleSession session;
        public readonly MessageLog Log = new MessageLog();
        public readonly AttackAlerts Alerts = new AttackAlerts();
        public BattleFeedback(BattleSession battle) { session = battle; }

        /// <summary>True when effects/audio should be produced (headless fixtures switch this off).</summary>
        public bool Enabled => session && session.Combat != null && session.Combat.PresentationEnabled;

        public event Action<MessageEntry> MessagePosted;
        public event Action<CombatTarget, int, CombatTarget> Damaged;
        public event Action<CombatTarget> UnitDied;
        public event Action<Soldier> SoldierSpawned;
        public event Action<CombatTarget, Vector3, Vector3, AttackKind> WeaponFired;
        public event Action<Vector3, AttackKind, float, ImpactKind, CombatTarget> Impacted;
        public event Action<CaptureEvent> Captured;
        public event Action<int, int> IncomeReceived;
        public event Action<int> WinnerDecided;

        /// <summary>Adds a line to the HUD log only. It never enters <see cref="BattleSession.Messages"/>.</summary>
        public void Post(string text, MessageKind kind, int team = -1, Vector3? focus = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            var entry = new MessageEntry(text, kind, team, focus.HasValue, focus ?? Vector3.zero, Time.unscaledTime);
            Log.Add(entry);
            MessagePosted?.Invoke(entry);
        }

        internal void RaiseDamaged(CombatTarget victim, int attacker, CombatTarget source) { if (Enabled) Damaged?.Invoke(victim, attacker, source); }
        internal void RaiseDied(CombatTarget unit) { if (Enabled) UnitDied?.Invoke(unit); }
        internal void RaiseSpawned(Soldier unit) { if (Enabled) SoldierSpawned?.Invoke(unit); }
        internal void RaiseFired(CombatTarget source, Vector3 from, Vector3 to, AttackKind attack) { if (Enabled) WeaponFired?.Invoke(source, from, to, attack); }
        internal void RaiseImpact(Vector3 point, AttackKind attack, float radius, ImpactKind kind, CombatTarget source) { if (Enabled) Impacted?.Invoke(point, attack, radius, kind, source); }
        internal void RaiseCaptured(CaptureEvent capture) { if (Enabled) Captured?.Invoke(capture); }
        internal void RaiseIncome(int team, int amount) { if (Enabled) IncomeReceived?.Invoke(team, amount); }
        internal void RaiseWinner(int team) { if (Enabled) WinnerDecided?.Invoke(team); }

        /// <summary>Settlement/Harbor helper: whether <paramref name="country"/> was fully owned by <paramref name="team"/> before one city changed.</summary>
        public static bool CountryWasComplete(BattleSession session, int country, int team, Settlement changed)
        {
            if (!session || country < 0 || team < 0) return false;
            bool any = false;
            foreach (var town in session.Towns)
            {
                if (!town || town.State == null || town.State.Country != country) continue;
                any = true;
                if (town != changed && town.State.Owner != team) return false;
            }
            return any;
        }
    }
}
