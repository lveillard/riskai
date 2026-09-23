using System;
using System.Collections.Generic;

namespace RiskAI.Core
{
    /// <summary>
    /// The one unit registry. Holds the types resolved from units.json by <see cref="Bind"/>
    /// (the Runtime loader calls it before any consumer exists). Reading before Bind fails loudly.
    /// Types are found by <see cref="UnitKind"/> (generated from the same ids) through a map built
    /// at Bind; consumers index their own tables by <see cref="UnitType.Index"/>, never by ordinal.
    /// </summary>
    public static class UnitCatalog
    {
        static UnitType[] types;
        static int[] indexOfKind;
        static UnitKind[] kindOfIndex;
        static Dictionary<string, int> byId;
        static UnitKind[] cityUnits, harborUnits, harborShips;
        static float transportLoadRadius;
        static int transportLoadLimit;

        public static bool IsBound => types != null;
        /// <summary>Incremented on every Bind so derived caches can refresh.</summary>
        public static int Revision { get; private set; }
        /// <summary>Number of types; valid dense indices are 0..Count-1.</summary>
        public static int Count => Types.Length;

        static UnitType[] Types => types ?? throw new InvalidOperationException(
            "Unit catalog is not bound: RiskAI.UnitConfigLoader must load Resources/Config/units.json and call UnitCatalog.Bind first.");

        public static void Bind(UnitsFile file)
        {
            var errors = UnitConfigValidation.Errors(file);
            if (errors.Count > 0) throw new ArgumentException("units.json is invalid:\n  " + string.Join("\n  ", errors), nameof(file));
            var resolved = new UnitType[file.Units.Length];
            var ids = new Dictionary<string, int>(resolved.Length, StringComparer.Ordinal);
            for (int i = 0; i < resolved.Length; i++) { resolved[i] = UnitType.From(file.Units[i], i); ids.Add(resolved[i].Id, i); }

            // The generated identity must match the file exactly (npm run build regenerates it).
            var names = Enum.GetNames(typeof(UnitKind));
            var values = (UnitKind[])Enum.GetValues(typeof(UnitKind));
            if (names.Length != resolved.Length)
                throw new ArgumentException("UnitKind.g.cs is out of date with units.json (" + names.Length + " vs " + resolved.Length + " types): run npm run build in scripts/config.", nameof(file));
            int maxOrdinal = 0;
            for (int i = 0; i < values.Length; i++) maxOrdinal = Math.Max(maxOrdinal, (int)values[i]);
            var index = new int[maxOrdinal + 1];
            for (int i = 0; i < index.Length; i++) index[i] = -1;
            var kinds = new UnitKind[resolved.Length];
            for (int i = 0; i < values.Length; i++)
            {
                if (!ids.TryGetValue(names[i], out int dense))
                    throw new ArgumentException("UnitKind." + names[i] + " has no units.json type: run npm run build in scripts/config.", nameof(file));
                index[(int)values[i]] = dense; kinds[dense] = values[i];
            }

            var city = new List<UnitKind>(); var harbor = new List<UnitKind>(); var ships = new List<UnitKind>();
            float loadRadius = 0; int loadLimit = 0;
            for (int i = 0; i < resolved.Length; i++)
            {
                ref readonly var type = ref resolved[i];
                if (type.Building == UnitBuilding.City) city.Add(kinds[i]);
                if (type.Building == UnitBuilding.Harbor) (type.Domain == UnitDomain.Sea ? ships : harbor).Add(kinds[i]);
                if (!type.CanTransport) continue;
                if (loadLimit != 0 && (type.Transport.LoadRadius != loadRadius || type.Transport.LoadLimit != loadLimit))
                    throw new ArgumentException("Every transport shares one load radius/limit (shore and harbor geometry use it).", nameof(file));
                loadRadius = type.Transport.LoadRadius; loadLimit = type.Transport.LoadLimit;
            }

            types = resolved; byId = ids; indexOfKind = index; kindOfIndex = kinds;
            cityUnits = city.ToArray(); harborUnits = harbor.ToArray(); harborShips = ships.ToArray();
            transportLoadRadius = loadRadius; transportLoadLimit = loadLimit;
            Revision++;
        }

        /// <summary>Editor/test domain reset (SubsystemRegistration).</summary>
        public static void Unbind()
        {
            types = null; byId = null; indexOfKind = null; kindOfIndex = null;
            cityUnits = harborUnits = harborShips = null;
        }

        public static bool IsDefined(UnitKind kind) =>
            (uint)kind < (uint)(indexOfKind ?? Array.Empty<int>()).Length && indexOfKind[(int)kind] >= 0;

        public static ref readonly UnitType Get(UnitKind kind)
        {
            var all = Types;
            if (!IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown unit type");
            return ref all[indexOfKind[(int)kind]];
        }

        public static ref readonly UnitType At(int index) => ref Types[index];

        public static UnitKind KindAt(int index) { var _ = Types; return kindOfIndex[index]; }

        public static bool TryIndexOf(string id, out int index)
        {
            var _ = Types;
            return byId.TryGetValue(id ?? "", out index);
        }

        /// <summary>Land units trained in a city, in units.json order.</summary>
        public static IReadOnlyList<UnitKind> CityUnits { get { var _ = Types; return cityUnits; } }
        /// <summary>Land units trained in a harbor, in units.json order.</summary>
        public static IReadOnlyList<UnitKind> HarborUnits { get { var _ = Types; return harborUnits; } }
        /// <summary>Hulls built in a harbor, in units.json order.</summary>
        public static IReadOnlyList<UnitKind> HarborShips { get { var _ = Types; return harborShips; } }
        /// <summary>Load radius shared by every transport (A00V 512 native); shore and harbor geometry use it.</summary>
        public static float TransportLoadRadius { get { var _ = Types; return transportLoadRadius; } }
        public static int TransportLoadLimit { get { var _ = Types; return transportLoadLimit; } }
    }
}
