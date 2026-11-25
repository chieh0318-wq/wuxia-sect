using System;
using System.Linq;

namespace Wuxia
{
    public sealed partial class GameState
    {
        private const int CellsPerLot = 4;

        internal SlotData[] CourtyardCells()
        {
            int lots = Data.Start.slots.Length;
            return Enumerable.Range(0, Buildings.Length).Select(index =>
            {
                if (!PlotUnlocked(index)) return null;
                var lot = Data.Start.slots[index % lots];
                int cell = index / lots;
                return new SlotData
                {
                    x = lot.x + ((cell & 1) == 0 ? -1.58f : 1.58f),
                    z = lot.z + ((cell & 2) == 0 ? -1.47f : 1.47f),
                };
            }).ToArray();
        }

        internal int[] FootprintCells(int anchor, BuildingData building)
        {
            int lots = Data.Start.slots.Length;
            int lot = anchor % lots, corner = anchor / lots;
            return Enumerable.Range(0, Footprint(building)).Select(offset => lot + (corner ^ offset) * lots).ToArray();
        }

        internal bool CanPlaceInCells(int anchor, BuildingData building)
        {
            if (building == null || !PlotUnlocked(anchor) || Footprint(building) > CellsPerLot) return false;
            int lots = Data.Start.slots.Length, lot = anchor % lots;
            var occupied = new System.Collections.Generic.HashSet<int>();
            for (int index = lot; index < Buildings.Length; index += lots)
                if (Buildings[index] != null)
                    occupied.UnionWith(FootprintCells(index, Buildings[index]));
            return !FootprintCells(anchor, building).Any(occupied.Contains);
        }
    }
}
