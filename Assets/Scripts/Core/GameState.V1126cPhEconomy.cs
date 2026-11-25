using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    public sealed partial class GameState
    {
        private int MonthlyRations => Disciples.Count * Data.Start.foodPerDisciple;
        private (int silver, int food) MonthlyHoldingIncome =>
            ((Story.heldNodes?.Length ?? 0) * 4, (Story.heldNodes?.Length ?? 0) * 2);

        private (int silver, int food, int reputation, int upkeep, int materials) BuildingMonthLedger()
        {
            int silver = 0, food = 0, reputation = 0, upkeep = 0, materials = 0;
            foreach (var building in Buildings)
            {
                if (building == null)
                    continue;
                silver += building.income.silver;
                food += building.income.food;
                reputation += building.income.reputation;
                upkeep += building.upkeep;
                materials += building.income.materials;
            }

            silver = UnityEngine.Mathf.RoundToInt(silver * ProductionMultiplier("commerce"));
            food = UnityEngine.Mathf.RoundToInt(food * ProductionMultiplier("farming"));
            return (silver, food, reputation, upkeep, materials);
        }

        private void ApplyMonthlyTechnology(ref int silver, ref int food)
        {
            if (HasTech("craft1") && BuildingLevel("infirmary") > 0) silver += 4;
            if (HasTech("craft2")) silver += 6 * Buildings.Count(building => building?.id == "smithy");
            if (HasTech("craft4"))
            {
                silver = Mathf.RoundToInt(silver * 1.35f);
                food = Mathf.RoundToInt(food * 1.35f);
            }
        }

        private int MonthlyTradeIncome(MapFaction faction) =>
            HasTie(faction.id, Diplomacy.Trade) && !HasTie(faction.id, Diplomacy.Merged) ? TradeIncomeOf(faction) : 0;

        private int TributeIncomeOf(MapFaction faction) =>
            Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp(faction.money / 600, 5, 30) *
                (100 + (Tie(faction.id)?.tributePct ?? 0)) / 100f), 5, 30);

        internal (string source, int value)[] MonthlyResourceBreakdown(int resource)
        {
            var rows = new List<(string, int)>();
            var (silver, food, reputation, upkeep, _) = BuildingMonthLedger();
            int techSilver = silver, techFood = food;
            ApplyMonthlyTechnology(ref techSilver, ref techFood);
            var (heldSilver, heldFood) = MonthlyHoldingIncome;
            if (resource == 0)
            {
                rows.Add(("建築產出", silver));
                Add("藏經閣工藝", techSilver - silver);
                Add("據點上繳", heldSilver);
                foreach (var faction in WorldMapData.Load().Factions)
                {
                    Add($"通商・{faction.Label}", MonthlyTradeIncome(faction));
                    if (HasTie(faction.id, Diplomacy.Vassal) && !HasTie(faction.id, Diplomacy.Merged))
                        Add($"朝貢・{faction.Label}", TributeIncomeOf(faction));
                }
                Add("去留約定", Story.monthlySilver);
                rows.Add(("建築維護", -upkeep));
            }
            if (resource == 1)
            {
                rows.Add(("建築產出", food));
                Add("藏經閣工藝", techFood - food);
                Add("據點上繳", heldFood);
                Add("去留約定", Story.monthlyFood);
                rows.Add(("弟子口糧", -MonthlyRations));
            }
            if (resource == 2)
            {
                rows.Add(("建築產出", reputation));
                Add("銀兩不足", Silver + MonthlyResourceBreakdown(0).Sum(row => row.value) < 0 ? -1 : 0);
                Add("糧食不足", Food + MonthlyResourceBreakdown(1).Sum(row => row.value) < 0 ? -1 : 0);
            }
            if (resource == 3) rows.Add(("每月固定增減", 0));
            if (resource == 4)
            {
                rows.Add(("月底重置當月餘額", -ActionPoints));
                rows.Add(("次月行動力上限", ApMax));
            }
            return rows.ToArray();

            void Add(string source, int value)
            {
                if (value != 0) rows.Add((source, value));
            }
        }
    }
}
