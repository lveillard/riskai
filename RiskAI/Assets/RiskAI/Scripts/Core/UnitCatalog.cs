using System;
using System.Collections.Generic;

namespace RiskAI.Core
{
    /// <summary>
    /// The one unit registry. Holds the types resolved from units.json by <see cref="Bind"/>
    /// (the Runtime loader calls it before any consumer exists). Reading before Bind fails loudly.
    /// </summary>
    public static class UnitCatalog
    {
        public const string TowerId = "Tower";
        static UnitType[] types;
        static int[] landIndex, navalIndex;
        static int towerIndex = -1;
        static Dictionary<string, int> byId;
        static UnitKind[] cityUnits, harborUnits;
        static NavalUnitKind[] harborShips;
        static float transportLoadRadius;
        static int transportLoadLimit;

        public static bool IsBound => types != null;
        /// <summary>Incremented on every Bind so derived caches can refresh.</summary>
        public static int Revision { get; private set; }
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

            var land = Map<UnitKind>(ids, UnitDomain.Land, resolved);
            var naval = Map<NavalUnitKind>(ids, UnitDomain.Sea, resolved);
            if (!ids.TryGetValue(TowerId, out int tower) || resolved[tower].Domain != UnitDomain.Static)
                throw new ArgumentException("units.json needs the static '" + TowerId + "' post.", nameof(file));
            for (int i = 0; i < resolved.Length; i++)
            {
                bool known = resolved[i].Domain == UnitDomain.Land ? Enum.IsDefined(typeof(UnitKind), resolved[i].Id)
                    : resolved[i].Domain == UnitDomain.Sea ? Enum.IsDefined(typeof(NavalUnitKind), resolved[i].Id) : i == tower;
                if (!known) throw new ArgumentException("units.json type '" + resolved[i].Id + "' has no runtime identity.", nameof(file));
            }

            var city = new List<UnitKind>(); var harbor = new List<UnitKind>(); var ships = new List<NavalUnitKind>();
            float loadRadius = 0; int loadLimit = 0;
            for (int i = 0; i < resolved.Length; i++)
            {
                ref readonly var type = ref resolved[i];
                if (type.Domain == UnitDomain.Land && type.Building == UnitBuilding.City) city.Add((UnitKind)Enum.Parse(typeof(UnitKind), type.Id));
                if (type.Domain == UnitDomain.Land && type.Building == UnitBuilding.Harbor) harbor.Add((UnitKind)Enum.Parse(typeof(UnitKind), type.Id));
                if (type.Domain == UnitDomain.Sea && type.Building == UnitBuilding.Harbor) ships.Add((NavalUnitKind)Enum.Parse(typeof(NavalUnitKind), type.Id));
                if (!type.CanTransport) continue;
                if (loadLimit != 0 && (type.Transport.LoadRadius != loadRadius || type.Transport.LoadLimit != loadLimit))
                    throw new ArgumentException("Every transport shares one load radius/limit (shore and harbor geometry use it).", nameof(file));
                loadRadius = type.Transport.LoadRadius; loadLimit = type.Transport.LoadLimit;
            }

            types = resolved; byId = ids; landIndex = land; navalIndex = naval; towerIndex = tower;
            cityUnits = city.ToArray(); harborUnits = harbor.ToArray(); harborShips = ships.ToArray();
            transportLoadRadius = loadRadius; transportLoadLimit = loadLimit;
            Revision++;
        }

        /// <summary>Editor/test domain reset (SubsystemRegistration).</summary>
        public static void Unbind()
        {
            types = null; byId = null; landIndex = navalIndex = null; towerIndex = -1;
            cityUnits = harborUnits = null; harborShips = null;
        }

        static int[] Map<T>(Dictionary<string, int> ids, UnitDomain domain, UnitType[] resolved) where T : struct, Enum
        {
            var names = Enum.GetNames(typeof(T));
            var index = new int[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                if (!ids.TryGetValue(names[i], out index[i]) || resolved[index[i]].Domain != domain)
                    throw new ArgumentException("units.json has no " + domain + " type '" + names[i] + "'.");
            }
            return index;
        }

        public static ref readonly UnitType Get(UnitKind kind)
        {
            var all = Types; int ordinal = (int)kind;
            if ((uint)ordinal >= (uint)landIndex.Length) throw new ArgumentOutOfRangeException(nameof(kind));
            return ref all[landIndex[ordinal]];
        }

        public static ref readonly UnitType Get(NavalUnitKind kind)
        {
            var all = Types; int ordinal = (int)kind;
            if ((uint)ordinal >= (uint)navalIndex.Length) throw new ArgumentOutOfRangeException(nameof(kind));
            return ref all[navalIndex[ordinal]];
        }

        /// <summary>The capturable city/harbor post.</summary>
        public static ref readonly UnitType Tower => ref Types[towerIndex];

        public static ref readonly UnitType At(int index) => ref Types[index];

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
        public static IReadOnlyList<NavalUnitKind> HarborShips { get { var _ = Types; return harborShips; } }
        /// <summary>Load radius shared by every transport (A00V 512 native); shore and harbor geometry use it.</summary>
        public static float TransportLoadRadius { get { var _ = Types; return transportLoadRadius; } }
        public static int TransportLoadLimit { get { var _ = Types; return transportLoadLimit; } }
    }
}
