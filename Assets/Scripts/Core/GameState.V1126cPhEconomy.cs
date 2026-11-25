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

        private int MonthlyTradeIncome(MapFaction faction) =>
            HasTie(faction.id, Diplomacy.Trade) && !HasTie(faction.id, Diplomacy.Merged) &&
            MonthStamp >= RouteFlag("trade_first_" + faction.id) ? TradeIncomeOf(faction) : 0;

        private string TradeFirstDate(MapFaction faction)
        {
            int stamp = System.Math.Max(MonthStamp, RouteFlag("trade_first_" + faction.id));
            int year = (stamp - 1) / 12;
            return $"開元{(year == 1 ? "元" : ChineseNumber(year))}年 {Months[(stamp - 1) % 12]}";
        }

        private int TributeIncomeOf(MapFaction faction) =>
            Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp(faction.money / 600, 5, 30) *
                (100 + (Tie(faction.id)?.tributePct ?? 0)) / 100f), 5, 30);

        internal (string source, int value)[] MonthlyResourceBreakdown(int resource, bool includePatron = true)
        {
            var rows = new List<(string, int)>();
            var (silver, food, reputation, _, upkeep, service) = BuildingMonth();
            int techSilver = silver, techFood = food;
            M6MonthIncome(ref techSilver, ref techFood, null);
            var (heldSilver, heldFood) = MonthlyHoldingIncome;
            if (resource == 0)
            {
                rows.Add(("建築產出", silver));
                Add("藏經閣工藝", techSilver - silver - heldSilver);
                Add("據點上繳", heldSilver);
                foreach (var faction in WorldMapData.Load().Factions)
                {
                    if (HasTie(faction.id, Diplomacy.Trade) && !HasTie(faction.id, Diplomacy.Merged))
                    {
                        int trade = MonthlyTradeIncome(faction);
                        rows.Add((trade == 0 ? $"通商・{faction.Label}（{TradeFirstDate(faction)}起）" : $"通商・{faction.Label}", trade));
                    }
                    if (HasTie(faction.id, Diplomacy.Vassal) && !HasTie(faction.id, Diplomacy.Merged))
                        Add($"朝貢・{faction.Label}", TributeIncomeOf(faction));
                }
                AddHallEffects("silver");
                Add("山門地利", SiteMonthly.silver);
                Add("去留約定", Story.monthlySilver);
                rows.Add(("建築維護", -upkeep));
                Add("建築整修", -service);
            }
            if (resource == 1)
            {
                rows.Add(("建築產出", food));
                Add("藏經閣工藝", techFood - food - heldFood);
                Add("據點上繳", heldFood);
                AddHallEffects("food");
                Add("山門地利", SiteMonthly.food);
                Add("去留約定", Story.monthlyFood);
                rows.Add(("弟子口糧", -MonthlyRations));
            }
            if (resource == 2)
            {
                rows.Add(("建築產出", reputation));
                Add("銀兩不足", Silver + MonthlyResourceBreakdown(0, false).Sum(row => row.value) < 0 ? -1 : 0);
                Add("糧食不足", Food + MonthlyResourceBreakdown(1, false).Sum(row => row.value) < 0 ? -1 : 0);
            }
            if (resource == 3) rows.Add(("每月固定增減", 0));
            if (resource == 4)
            {
                rows.Add(("月底重置當月餘額", -ActionPoints));
                rows.Add(("次月行動力上限", ApMax));
            }
            if (includePatron && resource is 0 or 1 && Patron is { } patron && Patronage(patron) is { } terms)
            {
                int delta = rows.Sum(row => row.Item2);
                int silverOnHand = Silver + (resource == 0 ? delta : MonthlyResourceBreakdown(0, false).Sum(row => row.value));
                int foodOnHand = Food + (resource == 1 ? delta : MonthlyResourceBreakdown(1, false).Sum(row => row.value));
                if (CanPayPatron(patron, terms, Mathf.Max(0, silverOnHand), Mathf.Max(0, foodOnHand)))
                    Add($"上繳・{patron.Label}", resource == 0 ? -terms.silver : -terms.food);
            }
            return rows.ToArray();

            void AddHallEffects(string key)
            {
                foreach (var (hall, _, effects) in MonthlyHallEffects())
                    Add(hall.name, effects.Where(effect => effect.key == key).Sum(effect => Mathf.RoundToInt(effect.value)));
            }

            void Add(string source, int value)
            {
                if (value != 0) rows.Add((source, value));
            }
        }
    }
}
