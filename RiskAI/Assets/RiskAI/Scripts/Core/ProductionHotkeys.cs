using System;
using System.Collections.Generic;

namespace RiskAI.Core
{
    /// <summary>The two production command cards. A selected building shows exactly one of them.</summary>
    public enum ProductionBuilding { City, Harbor }

    /// <summary>One purchasable product: any unit type (land units and hulls alike).</summary>
    public readonly struct ProductionOption : IEquatable<ProductionOption>
    {
        public readonly UnitKind Kind;
        ProductionOption(UnitKind kind){Kind=kind;}
        public static ProductionOption For(UnitKind kind) => new ProductionOption(kind);
        /// <summary>Sea-motor hulls queue on the harbor; the grid places them after the land block.</summary>
        public bool SeaMotor => UnitCatalog.Get(Kind).SeaMotor;
        public int Cost => UnitCatalog.Get(Kind).Cost;
        public bool Equals(ProductionOption other) => Kind==other.Kind;
        public override bool Equals(object obj) => obj is ProductionOption other&&Equals(other);
        public override int GetHashCode() => Kind.GetHashCode();
        public override string ToString() => Kind.ToString();
    }

    /// <summary>A product's cell on the command card: page, row/column and its grid hotkey.</summary>
    public readonly struct ProductionSlot
    {
        public readonly ProductionOption Option;
        public readonly int Index,Page,Cell;
        public int Row => Cell/ProductionHotkeys.Columns;
        public int Column => Cell%ProductionHotkeys.Columns;
        public string Key => ProductionHotkeys.KeyForCell(Cell);
        public ProductionSlot(ProductionOption option,int index,int page,int cell){Option=option;Index=index;Page=page;Cell=cell;}
    }

    /// <summary>
    /// WC3-style positional "grid hotkeys" for the production command card.
    /// Rows are Q W E R / A S D F / Z X C V. Land units come first, then hulls, each
    /// ordered cheap to expensive with units.json order breaking ties, so a new
    /// catalog entry automatically receives the next free cell. A card with more than
    /// twelve products pages: the last cell (V) then switches pages.
    /// </summary>
    public static class ProductionHotkeys
    {
        public const int Columns=4, Rows=3, CellsPerPage=Columns*Rows;
        /// <summary>The cell that switches pages when a card overflows.</summary>
        public const int PageCell=CellsPerPage-1;
        static readonly string[] keys={"Q","W","E","R","A","S","D","F","Z","X","C","V"};
        static ProductionSlot[] city, harbor;

        public static IReadOnlyList<string> GridKeys => keys;
        public static string PageKey => keys[PageCell];
        public static string KeyForCell(int cell) => cell>=0&&cell<keys.Length?keys[cell]:null;
        public static int CellForKey(string key)
        {
            if(string.IsNullOrEmpty(key))return -1;
            for(int i=0;i<keys.Length;i++)if(string.Equals(keys[i],key,StringComparison.OrdinalIgnoreCase))return i;
            return -1;
        }

        /// <summary>Products on one page. Twelve when everything fits, otherwise eleven plus the page cell.</summary>
        public static int PageCapacity(int productCount) => productCount<=CellsPerPage?CellsPerPage:CellsPerPage-1;
        public static int PageCount(int productCount) => productCount<=0?1:(productCount+PageCapacity(productCount)-1)/PageCapacity(productCount);
        public static int PageCount(ProductionBuilding building) => PageCount(Layout(building).Count);

        public static IReadOnlyList<ProductionOption> Options(ProductionBuilding building)
        {
            var land=new List<ProductionOption>();var naval=new List<ProductionOption>();
            if(building==ProductionBuilding.City)
                foreach(var kind in UnitCatalog.CityUnits)land.Add(ProductionOption.For(kind));
            else
            {
                foreach(var kind in UnitCatalog.HarborUnits)land.Add(ProductionOption.For(kind));
                foreach(var kind in UnitCatalog.HarborShips)naval.Add(ProductionOption.For(kind));
            }
            SortByCost(land);SortByCost(naval);
            land.AddRange(naval);return land;
        }

        public static IReadOnlyList<ProductionSlot> Layout(ProductionBuilding building)
        {
            if(layoutRevision!=UnitCatalog.Revision){city=harbor=null;layoutRevision=UnitCatalog.Revision;}
            if(building==ProductionBuilding.City)return city??=Arrange(Options(building));
            return harbor??=Arrange(Options(building));
        }
        static int layoutRevision=-1;

        /// <summary>Places an ordered product list on pages of the 4×3 grid.</summary>
        public static ProductionSlot[] Arrange(IReadOnlyList<ProductionOption> options)
        {
            int capacity=PageCapacity(options.Count);
            var slots=new ProductionSlot[options.Count];
            for(int i=0;i<options.Count;i++)slots[i]=new ProductionSlot(options[i],i,i/capacity,i%capacity);
            return slots;
        }

        public static bool TryFind(ProductionBuilding building,int page,int cell,out ProductionSlot slot)
        {
            var layout=Layout(building);
            for(int i=0;i<layout.Count;i++)
                if(layout[i].Page==page&&layout[i].Cell==cell){slot=layout[i];return true;}
            slot=default;return false;
        }

        public static bool TryFind(ProductionBuilding building,ProductionOption option,out ProductionSlot slot)
        {
            var layout=Layout(building);
            for(int i=0;i<layout.Count;i++)
                if(layout[i].Option.Equals(option)){slot=layout[i];return true;}
            slot=default;return false;
        }

        public static ProductionBuilding BuildingFor(UnitKind kind) =>
            (UnitCatalog.Get(kind).Building==UnitBuilding.Harbor)?ProductionBuilding.Harbor:ProductionBuilding.City;

        /// <summary>Effective hotkey shown on the card and used by the keyboard; null when not produced.</summary>
        public static string Hotkey(UnitKind kind) =>
            TryFind(BuildingFor(kind),ProductionOption.For(kind),out var slot)?slot.Key:null;

        // Stable insertion sort: equal costs keep catalog order.
        static void SortByCost(List<ProductionOption> options)
        {
            for(int i=1;i<options.Count;i++)
            {
                var item=options[i];int cost=item.Cost;int j=i-1;
                while(j>=0&&options[j].Cost>cost){options[j+1]=options[j];j--;}
                options[j+1]=item;
            }
        }
    }
}
