using System.Collections.Generic;
using System.Linq;

namespace Wuxia
{
    /// <summary>
    /// v1127 N1: courtyard land is the cell grid itself. Since v1126c PH3 every lot holds 4 cells and footprints were
    /// rescaled, but the build menu still capped the summed footprints at the legacy Scales[].land (草創 17) — the
    /// starting compound alone used 15, so after two small buildings the menu said 「用地不足」 while green empty cells
    /// remained. Capacity is now 4 cells per unlocked lot, usage is the distinct occupied cells (old saves whose
    /// rescaled footprints overlap are not double counted), and a building is offered only if some unlocked anchor
    /// can really hold its whole footprint.
    /// </summary>
    public sealed partial class GameState
    {
        internal int UnlockedLots => Data.Start.slots.Count(slot => slot.scale <= SectScale);

        internal HashSet<int> OccupiedCells()
        {
            var occupied = new HashSet<int>();
            for (int index = 0; index < Buildings.Length; index++)
                if (Buildings[index] != null && PlotUnlocked(index))
                    occupied.UnionWith(FootprintCells(index, Buildings[index]));
            return occupied;
        }

        internal bool HasRoomFor(BuildingData building) =>
            building != null && Enumerable.Range(0, Buildings.Length).Any(index => CanPlaceInCells(index, building));

        /// <summary>Land each sect scale unlocks (4 cells per lot), for the expansion screen.</summary>
        internal int LandAtScale(int scale) => CellsPerLot * Data.Start.slots.Count(slot => slot.scale <= scale);

        /// <summary>Menu hint when nothing fits: says how to get more land.</summary>
        internal string LandHint(BuildingData building)
        {
            string need = $"　用地不足（需相連 {Footprint(building)} 格空地）";
            if (SectScale >= Scales.Length - 1) return need;
            var next = Scales[SectScale + 1];
            return $"{need}\n擴建山門至{next.name}可再開 {LandAtScale(SectScale + 1) - LandCapacity} 格：聲望 {next.reputation}・弟子 {next.disciples}・建築 {next.buildings} 座・銀 {next.silver}";
        }
    }
}
