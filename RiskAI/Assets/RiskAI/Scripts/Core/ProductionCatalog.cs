using System;
using System.Collections.Generic;

namespace RiskAI.Core
{
    /// <summary>Shared, engine-independent production availability. Costs and timing remain in the unit profiles.</summary>
    public static class ProductionCatalog
    {
        static readonly IReadOnlyList<UnitKind> settlementUnits=Array.AsReadOnly(new[]
        {
            UnitKind.Footman,UnitKind.Archer,UnitKind.Guard,UnitKind.Mage,UnitKind.Mortar,UnitKind.Medic
        });
        static readonly IReadOnlyList<UnitKind> harborUnits=Array.AsReadOnly(new[]
        {
            UnitKind.MarinePrivate,UnitKind.MarineMajor,UnitKind.MarineGeneral
        });
        static readonly IReadOnlyList<NavalUnitKind> harborShips=Array.AsReadOnly(new[]
        {
            NavalUnitKind.Galley,NavalUnitKind.Transport
        });

        public static IReadOnlyList<UnitKind> SettlementUnits=>settlementUnits;
        public static IReadOnlyList<UnitKind> HarborUnits=>harborUnits;
        public static IReadOnlyList<NavalUnitKind> HarborShips=>harborShips;

        static bool Contains<T>(IReadOnlyList<T> options,T value)
        {
            for(int i=0;i<options.Count;i++)if(EqualityComparer<T>.Default.Equals(options[i],value))return true;
            return false;
        }
        public static bool AllowsSettlementUnit(UnitKind kind) => Contains(settlementUnits,kind);
        public static bool AllowsHarborUnit(UnitKind kind) => Contains(harborUnits,kind);
        public static bool AllowsHarborShip(NavalUnitKind kind) => Contains(harborShips,kind);

    }
}
