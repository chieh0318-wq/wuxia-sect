using System;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    [Serializable] public sealed class ConstructionPb
    {
        public int slot, quality, builtMonth;
        public string building;
    }

    public sealed partial class StoryProgress
    {
        public ConstructionPb[] constructionPb = Array.Empty<ConstructionPb>();
    }

    public sealed partial class GameState
    {
        private const int ServiceInterval = 12;
        private int ConstructionMonth => (Year - 1) * 12 + Month;

        internal static int ConstructionQuality(float raw) => Mathf.Clamp(Mathf.RoundToInt(raw * 100),
            BalanceV1126c.BuildQuality.minQualityPct, BalanceV1126c.BuildQuality.maxQualityPct);

        internal static (int Upkeep, int Output, int ServiceMonths) QualityTerms(int quality)
        {
            var balance = BalanceV1126c.BuildQuality;
            float steps = (quality - 100) / 10f;
            return (Mathf.RoundToInt(steps * balance.upkeepPctPer10Above100),
                Mathf.RoundToInt(steps * balance.outputPctPer10Above100),
                Mathf.Max(1, ServiceInterval + Mathf.RoundToInt(steps * balance.durabilityMonthsPer10)));
        }

        internal static string QualityText(int quality)
        {
            var terms = QualityTerms(quality);
            return $"品質 {quality}%　月維護 {terms.Upkeep:+0;-0;0}%　月產出 {terms.Output:+0;-0;0}%　每 {terms.ServiceMonths} 月整修";
        }

        internal void RecordConstruction(int slot, BuildingData building, int quality)
        {
            var records = (Story.constructionPb ?? Array.Empty<ConstructionPb>()).Where(entry => entry?.slot != slot);
            Story.constructionPb = records.Append(new ConstructionPb
                { slot = slot, building = building.id, quality = quality, builtMonth = ConstructionMonth }).ToArray();
        }

        private ConstructionPb ConstructionRecord(int slot, BuildingData building) => Story.constructionPb?
            .FirstOrDefault(entry => entry?.slot == slot && entry.building == building.id);

        internal (float Silver, float Food, float Reputation, float Materials, float Upkeep, int Service) BuildingMonthAt(
            int slot, BuildingData building)
        {
            var record = ConstructionRecord(slot, building);
            var terms = QualityTerms(record?.quality ?? 100);
            float output = 1 + terms.Output / 100f;
            float upkeep = building.upkeep * (1 + terms.Upkeep / 100f);
            int age = ConstructionMonth - (record?.builtMonth ?? 1) + 1;
            int service = age > 0 && age % terms.ServiceMonths == 0
                ? Mathf.RoundToInt(upkeep) : 0;
            return (building.income.silver * output, building.income.food * output, building.income.reputation * output,
                building.income.materials * output, upkeep, service);
        }

        internal (int Silver, int Food, int Reputation, int Materials, int Upkeep, int Service) BuildingMonth()
        {
            float silver = 0, food = 0, reputation = 0, materials = 0, upkeep = 0;
            int service = 0;
            for (int slot = 0; slot < Buildings.Length; slot++)
            {
                var building = Buildings[slot];
                if (building == null) continue;
                var flow = BuildingMonthAt(slot, building);
                silver += flow.Silver;
                food += flow.Food;
                reputation += flow.Reputation;
                materials += flow.Materials;
                upkeep += flow.Upkeep;
                service += flow.Service;
            }
            return (Mathf.RoundToInt(silver * ProductionMultiplier("commerce")),
                Mathf.RoundToInt(food * ProductionMultiplier("farming")), Mathf.RoundToInt(reputation),
                Mathf.RoundToInt(materials), Mathf.RoundToInt(upkeep), service);
        }

        internal string BuildingQualityTooltip(BuildingData building, int slot = -1)
        {
            if (slot < 0) slot = Array.IndexOf(Buildings, building);
            var record = ConstructionRecord(slot, building);
            var flow = BuildingMonthAt(slot, building);
            var terms = QualityTerms(record?.quality ?? 100);
            return $"品質 {record?.quality ?? 100}%　月維護 {terms.Upkeep:+0;-0;0}%　月產出 {terms.Output:+0;-0;0}%" +
                $"\n月產出　銀 {flow.Silver:0.##}／糧 {flow.Food:0.##}／望 {flow.Reputation:0.##}／材 {flow.Materials:0.##}" +
                $"\n月維護銀 {flow.Upkeep:0.##}；每 {terms.ServiceMonths} 月另付整修銀 {Mathf.RoundToInt(flow.Upkeep)}。";
        }
    }
}
