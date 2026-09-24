namespace RiskAI.Core
{
    /// <summary>
    /// The one order queue for every unit. Soldiers and ships each keep a single instance and
    /// only turn "run this command" into their motor. The cap, Shift append/replace, Stop/Hold
    /// clearing, the drawn legs and the embark stash all live here. No per-tick allocation:
    /// the buffers are fixed and cleared in <see cref="Reset"/> when a pooled actor is reused.
    /// </summary>
    public sealed class OrderQueue
    {
        public const int Limit = 35;
        public const int LegCap = Limit + 1;
        public const string FullError = "La cola de órdenes está llena.";
        public const string InvalidError = "La orden ya no es válida para esa unidad o su objetivo.";

        public enum AdmitResult { Run, Queued, Full }

        readonly UnitCommand[] items = new UnitCommand[Limit];
        readonly float[] legX = new float[LegCap];
        readonly float[] legY = new float[LegCap];
        readonly float[] legZ = new float[LegCap];
        readonly byte[] legKind = new byte[LegCap];
        readonly float[] stashX = new float[LegCap];
        readonly float[] stashY = new float[LegCap];
        readonly float[] stashZ = new float[LegCap];
        readonly byte[] stashKind = new byte[LegCap];
        int head, count, legCount, revision, stashCount;

        public int Count => count;
        public int LegCount => legCount;
        public int Revision => revision;
        public int StashCount => stashCount;

        public void Reset()
        {
            Clear();
            legCount = 0;
            stashCount = 0;
            revision++;
        }

        public void Clear()
        {
            head = 0;
            count = 0;
        }

        /// <summary>
        /// Plan §4.5. Stop and Hold clear and still run (the motor stands; the command is not queued).
        /// Shift while the unit is busy appends. Anything else replaces the queue and runs now.
        /// </summary>
        public AdmitResult Admit(in UnitCommand command, bool busy)
        {
            var action = UnitRules.Queue(command.Kind, command.Append, busy);
            if (action == UnitRules.OrderQueueAction.Clear)
            {
                Clear();
                return AdmitResult.Run;
            }
            if (action == UnitRules.OrderQueueAction.Append)
                return TryEnqueue(command) ? AdmitResult.Queued : AdmitResult.Full;
            Clear();
            return AdmitResult.Run;
        }

        public bool TryEnqueue(in UnitCommand command)
        {
            if (count >= Limit) return false;
            items[(head + count) % Limit] = command;
            count++;
            return true;
        }

        public bool TryDequeue(out UnitCommand command)
        {
            if (count == 0)
            {
                command = default;
                return false;
            }
            command = items[head];
            head = (head + 1) % Limit;
            count--;
            if (count == 0) head = 0;
            return true;
        }

        /// <summary>Redrawn legs: the order being carried out, then the queue. Called when orders change, not per tick.</summary>
        public void Publish(bool hasActive, UnitCommandKind kind, float x, float y, float z)
        {
            revision++;
            legCount = 0;
            if (hasActive)
            {
                legX[0] = x;
                legY[0] = y;
                legZ[0] = z;
                legKind[0] = (byte)kind;
                legCount = 1;
            }
            for (int i = 0; i < count && legCount < LegCap; i++)
            {
                var order = items[(head + i) % Limit];
                int slot = legCount++;
                legX[slot] = order.X;
                legY[slot] = order.Y;
                legZ[slot] = order.Z;
                legKind[slot] = (byte)order.Kind;
            }
        }

        public void Leg(int index, out float x, out float y, out float z, out byte kind)
        {
            x = legX[index];
            y = legY[index];
            z = legZ[index];
            kind = legKind[index];
        }

        /// <summary>Boarding keeps the drawn route (active leg plus queue) for after the unload.</summary>
        public void StashLegs()
        {
            stashCount = legCount;
            for (int i = 0; i < stashCount; i++)
            {
                stashX[i] = legX[i];
                stashY[i] = legY[i];
                stashZ[i] = legZ[i];
                stashKind[i] = legKind[i];
            }
        }

        public void Stashed(int index, out float x, out float y, out float z, out byte kind)
        {
            x = stashX[index];
            y = stashY[index];
            z = stashZ[index];
            kind = stashKind[index];
        }

        public void ClearStash() => stashCount = 0;
    }
}
