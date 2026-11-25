using System.Linq;
using UnityEngine;

namespace Wuxia
{
    public sealed partial class GameState
    {
        internal static readonly (string name, int reputation, int disciples, int buildings, int silver, int land)[] Scales =
        {
            ("草創", 0, 0, 0, 0, 17),
            // v1126c PA3: disciples +4 (the sect now starts with 9 members incl. 石長庚／田秋禾／祝青芷).
            ("小有規模", 22, 10, 8, 24, 28),
            ("一方大派", 50, 13, 11, 48, 40),
            ("名門正宗", 85, 17, 15, 80, 54)
        };

        internal int SectScale => Mathf.Clamp(Story.sectScale, 0, Scales.Length - 1);
        internal int Materials => Story.materials;
        internal int LandUsed => Buildings.Where(building => building != null).Sum(Footprint);
        internal int LandCapacity => Scales[SectScale].land;
        internal static int Footprint(BuildingData building) => Mathf.Max(1, building.footprint);
        internal int BuildingLimit(BuildingData building) => building.unique ? 1 : building.limit > 0 ? building.limit : int.MaxValue;
        internal int BuildingCount(BuildingData building) => Buildings.Count(placed => placed?.id == building.id);
        internal bool PlotUnlocked(int index) => index >= 0 && index < Buildings.Length && Data.Start.slots[index].scale <= SectScale;

        internal string ExpansionBlock()
        {
            if (SectScale >= Scales.Length - 1) return "山門已擴至最大規模";
            var next = Scales[SectScale + 1];
            if (Reputation < next.reputation || Disciples.Count < next.disciples ||
                Buildings.Count(building => building != null) < next.buildings)
                return "聲望、弟子或建築尚未達標";
            return Silver < next.silver ? "銀兩不足" : ActionPoints < 1 ? "行動力不足" : null;
        }

        internal bool ExpandSect(string worker)
        {
            if (ExpansionBlock() != null || AssignmentSystem.Block(this, worker) != null ||
                !TrySpendAp(1, "expand")) return false;
            var next = Scales[SectScale + 1];
            int silver = Mathf.CeilToInt(next.silver / Mathf.Max(1, AssignmentSystem.Predict(this, "build", worker).Quality));
            Pay(new Cost { silver = silver });
            Story.sectScale++;
            AssignmentSystem.Commit(this, new[] { worker });
            AssignmentSystem.Credit(this, worker, 3);
            return true;
        }
    }
}
