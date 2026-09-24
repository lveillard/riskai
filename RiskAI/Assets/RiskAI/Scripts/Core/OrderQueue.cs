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

        public enum AdmitResult { Run, Queued, Full, Rejected }

        readonly UnitCommand[] items = new UnitCommand[Limit];
        readonly float[] legX = new float[LegCap];
        readonly float[] legY = new float[LegCap];
        readonly float[] legZ = new float[LegCap];
        readonly byte[] legKind = new byte[LegCap];
        readonly UnitCommand[] stash = new UnitCommand[LegCap];
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
        /// Plan §4.5. A rejected order leaves the queue as it was. Stop and Hold clear and still run.
        /// Shift while busy appends. Anything else replaces the queue and runs now.
        /// </summary>
        public AdmitResult Admit(in UnitCommand command, bool busy, bool valid)
        {
            if (!valid) return AdmitResult.Rejected;
            if (UnitRules.Queue(command.Kind, command.Append, busy) == UnitRules.OrderQueueAction.Append)
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

        public bool TryAt(int index, out UnitCommand command)
        {
            if (index < 0 || index >= count)
            {
                command = default;
                return false;
            }
            command = items[(head + index) % Limit];
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

        /// <summary>Boarding keeps the active command and the queue, kinds and targets included. The active order plus the queue is exactly LegCap.</summary>
        public void Stash(bool hasActive, in UnitCommand active)
        {
            stashCount = 0;
            if (hasActive) stash[stashCount++] = active;
            for (int i = 0; i < count; i++)
                stash[stashCount++] = items[(head + i) % Limit];
        }

        public UnitCommand StashedCommand(int index) => stash[index];

        public void ClearStash() => stashCount = 0;

        /// <summary>Keeps an order for after the voyage. A second copy of the same order is ignored. False when the plan is full.</summary>
        public bool AppendStash(in UnitCommand command)
        {
            if (ContainsStash(command)) return true;
            if (stashCount >= LegCap) return false;
            stash[stashCount++] = command;
            return true;
        }

        /// <summary>Orders queued while walking to the ship join the plan. Existing stash entries stay.</summary>
        public void MergeQueue()
        {
            for (int i = 0; i < count && stashCount < LegCap; i++)
                AppendStash(items[(head + i) % Limit]);
        }

        bool ContainsStash(in UnitCommand command)
        {
            for (int i = 0; i < stashCount; i++)
                if (SameOrder(stash[i], command)) return true;
            return false;
        }

        static bool SameOrder(in UnitCommand a, in UnitCommand b) =>
            a.Kind == b.Kind && a.TargetId == b.TargetId && a.CommandId == b.CommandId
            && a.X == b.X && a.Y == b.Y && a.Z == b.Z && a.StructureId == b.StructureId;
    }
}
