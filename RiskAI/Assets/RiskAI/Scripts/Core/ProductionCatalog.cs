using System;
using System.Collections.Generic;

namespace RiskAI.Core
{
    /// <summary>Shared, engine-independent production availability. Costs and timing remain in the unit profiles.</summary>
    public static class ProductionCatalog
    {
        static readonly IReadOnlyList<UnitKind> settlementUnits=Array.AsReadOnly(new[]
        {
            UnitKind.Footman,UnitKind.Archer,UnitKind.Knight,UnitKind.Mage,UnitKind.Mortar,UnitKind.Medic,
            // roster.shops h00N: h00F, h00I, h00J, h00M, h01A.
            UnitKind.EliteRifleman,UnitKind.Roarer,UnitKind.ArmyGeneral,UnitKind.Artillery,UnitKind.Tank
        });
        static readonly IReadOnlyList<UnitKind> harborUnits=Array.AsReadOnly(new[]
        {
            UnitKind.MarinePrivate,UnitKind.MarineMajor,UnitKind.MarineGeneral
        });
        static readonly IReadOnlyList<NavalUnitKind> harborShips=Array.AsReadOnly(new[]
        {
            NavalUnitKind.Frigate,NavalUnitKind.Transport,
            // roster.shops h00O: h00U, h001, n007.
            NavalUnitKind.Warship,NavalUnitKind.Battleship,NavalUnitKind.ArmoredTransport
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
