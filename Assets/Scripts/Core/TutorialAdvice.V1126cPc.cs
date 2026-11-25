using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    /// <summary>
    /// v1126c PC3 #11 / #15: live numbers for the new-player guide. Every figure is read from the same data the
    /// simulation uses (buildings.json income/upkeep, actions.json costs, GameState.ApMax / ApDescription, the
    /// monthly food draw in <see cref="GameState.EndMonth"/>), never a copy typed into the text.
    /// </summary>
    internal static class TutorialAdvice
    {
        internal static ActionData Action(GameState state, string type) =>
            state.Data.Actions?.FirstOrDefault(action => action != null && action.type == type);

        /// <summary>Monthly food produced by buildings (with the farming multiplier) plus site and agreements, minus mouths.</summary>
        internal static (int income, int eaten) FoodPerMonth(GameState state)
        {
            int food = state.Buildings.Where(building => building != null).Sum(building => building.income?.food ?? 0);
            food = Mathf.RoundToInt(food * state.ProductionMultiplier("farming")) + state.SiteMonthly.food + state.Story.monthlyFood;
            return (food, state.Disciples.Count * state.Data.Start.foodPerDisciple);
        }

        internal static (int income, int upkeep) SilverPerMonth(GameState state)
        {
            int silver = state.Buildings.Where(building => building != null).Sum(building => building.income?.silver ?? 0);
            silver = Mathf.RoundToInt(silver * state.ProductionMultiplier("commerce")) + state.SiteMonthly.silver + state.Story.monthlySilver;
            return (silver, state.Buildings.Where(building => building != null).Sum(building => building.upkeep));
        }

        internal static Cost BuildTotal(GameState state, BuildingData building)
        {
            var action = Action(state, "build");
            return new Cost
            {
                silver = state.BuildSilver(building.cost.silver + (action?.cost?.silver ?? 0)),
                food = building.cost.food + (action?.cost?.food ?? 0)
            };
        }

        internal static string Yield(GameState state, BuildingData building)
        {
            var parts = new List<string>();
            var income = building.income ?? new Yield();
            if (income.silver != 0) parts.Add(Scaled("每月銀兩", income.silver, state.ProductionMultiplier("commerce")));
            if (income.food != 0) parts.Add(Scaled("每月糧食", income.food, state.ProductionMultiplier("farming")));
            if (income.reputation != 0) parts.Add($"每月聲望 {income.reputation:+0;-0}");
            if (income.materials != 0) parts.Add($"每月材料 {income.materials:+0;-0}");
            if (building.upkeep != 0) parts.Add($"維護銀 −{building.upkeep}");
            return parts.Count == 0 ? (building.summary ?? "") : string.Join("、", parts);

            static string Scaled(string what, int value, float multiplier)
            {
                int real = Mathf.RoundToInt(value * multiplier);
                return real == value ? $"{what} {real:+0;-0}" : $"{what} {real:+0;-0}（基礎 {value}，加成 ×{multiplier:0.##}）";
            }
        }

        /// <summary>
        /// The one building the guide recommends right now: food first while the granary runs at a loss or holds under
        /// three months, otherwise the best monthly return per tael. Only buildings the sect can raise today count.
        /// </summary>
        internal static (BuildingData building, string reason)? Recommend(GameState state)
        {
            var options = state.Data.Buildings.Where(state.IsAvailable).ToList();
            if (options.Count == 0) return null;
            var (foodIn, eaten) = FoodPerMonth(state);
            int foodNet = foodIn - eaten;
            int monthsLeft = foodNet >= 0 ? 99 : state.Food / Mathf.Max(1, -foodNet);
            var (silverIn, upkeep) = SilverPerMonth(state);
            int silverNet = silverIn - upkeep;
            bool hungry = foodNet < 0 && monthsLeft < 6 || state.Food < eaten * 3;

            float Score(BuildingData building, bool food)
            {
                var income = building.income ?? new Yield();
                float value = food ? income.food : income.silver + income.food * .5f + income.reputation * 3 + income.materials * .5f;
                value -= building.upkeep;
                return value <= 0 ? -1 : value / Mathf.Max(1, BuildTotal(state, building).silver);
            }

            var pool = hungry ? options.Where(building => (building.income?.food ?? 0) > 0).ToList() : options;
            if (pool.Count == 0) pool = options;
            var pick = pool.OrderByDescending(building => Score(building, hungry))
                .ThenBy(building => BuildTotal(state, building).silver).First();
            var total = BuildTotal(state, pick);
            var yield = pick.income ?? new Yield();
            string reason;
            if (hungry)
                reason = $"門中 {state.Disciples.Count} 人每月耗糧 {eaten}，屋舍與地利每月產糧 {foodIn}，每月" +
                    (foodNet < 0 ? $"虧 {-foodNet}，存糧 {state.Food} 約撐 {monthsLeft} 個月" : $"僅餘 {foodNet}") +
                    $"。{pick.name}花銀 {total.silver} 即可每月添糧 {Mathf.RoundToInt(yield.food * state.ProductionMultiplier("farming"))}，是眼下最划算的一筆。";
            else
            {
                float net = yield.silver + yield.food * .5f - pick.upkeep;
                int payback = net > 0 ? Mathf.CeilToInt(total.silver / net) : 0;
                reason = $"糧食尚足（每月 {foodNet:+0;-0}），銀兩每月 {silverNet:+0;-0}。{pick.name}花銀 {total.silver}" +
                    (payback > 0 ? $"，約 {payback} 個月回本，" : "，") + "在可建屋舍中每兩銀子回報最高。";
            }
            return (pick, reason);
        }

        /// <summary>One line on this month's action points: how many are left, the cap and every source of the cap.</summary>
        internal static string ApLine(GameState state)
        {
            string sources = state.ApDescription.Replace("\n", "、");
            return $"本月尚餘行動力 {state.ActionPoints}／{state.ApMax}。上限來源：{sources}。";
        }

        internal static string Cost(ActionData action)
        {
            if (action == null) return "";
            var parts = new List<string>();
            // U+00A0 keeps 「行動力 2」 on one line when the card wraps.
            if (action.apCost > 0) parts.Add($"行動力\u00A0{action.apCost}");
            if (action.cost != null && action.cost.silver > 0) parts.Add($"銀\u00A0{action.cost.silver}");
            if (action.cost != null && action.cost.food > 0) parts.Add($"糧\u00A0{action.cost.food}");
            return parts.Count == 0 ? "不花費" : string.Join("・", parts);
        }

        /// <summary>A few common actions with their real costs, for the 行動力 step.</summary>
        internal static string ActionCosts(GameState state)
        {
            var list = new List<string>();
            foreach (string type in new[] { "build", "recruit", "train", "explore" })
            {
                var action = Action(state, type) ?? state.Data.Actions?.FirstOrDefault(entry => entry != null && entry.id == type);
                if (action != null) list.Add($"{action.name}（{Cost(action)}）");
            }
            return string.Join("、", list);
        }

        /// <summary>
        /// The 軍師's extra pointer during the first three months: next month's AP and where it comes from, how long
        /// the granary lasts, and the building to raise next with its numbers.
        /// </summary>
        internal static string FirstMonthsTip(GameState state)
        {
            int played = state.Story.pcMonthsPlayed;
            if (played < 1 || played > 3) return null;
            var (foodIn, eaten) = FoodPerMonth(state);
            var (silverIn, upkeep) = SilverPerMonth(state);
            var tip = new List<string>
            {
                $"開山第 {played} 月提點：來月行動力 {state.ApMax} 點（{state.ApDescription.Replace("\n", "、")}）；興建議事堂每級可再添 1 點。",
                $"糧食每月產 {foodIn}、耗 {eaten}，淨 {foodIn - eaten:+0;-0}；銀兩每月進 {silverIn}、維護 −{upkeep}，淨 {silverIn - upkeep:+0;-0}。"
            };
            var pick = Recommend(state);
            if (pick.HasValue)
            {
                var total = BuildTotal(state, pick.Value.building);
                tip.Add($"若要興建，先考慮【{pick.Value.building.name}】（銀 {total.silver}、{Yield(state, pick.Value.building)}）。");
            }
            return string.Join("", tip);
        }
    }

    public sealed partial class GameState
    {
        /// <summary>v1126c PC3 #15: months closed since the sect was founded (for the first-months guide).</summary>
        internal void CountPlayedMonth() => Story.pcMonthsPlayed++;
    }
}
