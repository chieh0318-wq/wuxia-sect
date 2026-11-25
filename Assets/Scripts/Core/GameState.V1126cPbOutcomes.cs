using System;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    public sealed partial class GameState
    {
        internal const int FeastLoyalty = 6, FeastExtraHealing = 1, DecreeLoyalty = 3, LodgeLoyalty = 2, LodgeHealing = 1;
        internal float NextRumorBonus => Mathf.Min(.75f, Story.rumorBonus + .25f);

        internal (int Hero, int Members) RecoveryAmount(string worker)
        {
            float quality = worker == null ? ProductionMultiplier("medicine") :
                AssignmentSystem.Predict(this, "medicine", worker).Quality;
            int amount = Mathf.Max(1, Mathf.RoundToInt(quality * (worker == null ? 1 : 1.5f)));
            return (Mathf.Max(1, amount + AttributeBonus("con") / 3), amount);
        }

        internal float[] ForgeGearOdds(string worker, bool hall = false)
        {
            int forging = AssignmentSystem.Skill(this, worker, "forging");
            float quality = AssignmentSystem.Predict(this, hall ? "forge" : "forge_gear", worker).Quality;
            float boost = !hall && HasTech("craft2") ? Mathf.Max(1, TechValue("craft2", n => n.value2)) : 1;
            int levels = hall ? Mathf.Max(0, HallLevel("forge_hall") - 1) : 0;
            float tier3 = hall ? .04f + .02f * forging + .04f * levels : .03f + .015f * forging;
            float tier2 = hall ? .25f + .04f * forging + .08f * levels : .12f + .03f * forging;
            float legendary = Mathf.Clamp01(tier3 * boost * quality);
            float fine = Mathf.Min(1 - legendary, tier2 * boost * quality);
            float good = hall ? 1 - legendary - fine : Mathf.Min(1 - legendary - fine, .40f);
            return new[] { 1 - legendary - fine - good, good, fine, legendary };
        }

        internal const string PatrolBreakthrough = "qiyu_cliff_cave";

        internal float BreakthroughChance(string id, float bonus, float fortune)
        {
            var entry = RankSystem.Catalog.breakthroughs?.FirstOrDefault(value => value.id == id);
            if (entry == null || (entry.oneTime && Protagonist.breakthroughs.Contains(id))) return 0;
            var line = entry.target == "martial" ? RankSystem.BestCombat(Protagonist.lines) :
                Array.Find(Protagonist.lines, value => value.id == entry.target);
            if (line == null || RankSystem.Index(line.cap) >= RankSystem.Index(RankSystem.Top)) return 0;
            return Mathf.Clamp01((entry.chance + bonus + AttributeBonus("wit") * AttributeSystem.InsightPerPoint) * fortune);
        }

        internal float PatrolFortune(string worker) => AttributeSystem.FortuneFactor(AssignmentSystem.Attribute(this, worker, "luk"));

        internal float PatrolRealmChance(string worker, float rumor) =>
            Mathf.Clamp((.22f + rumor) * PatrolFortune(worker), .05f, .9f);

        internal float PatrolInsightChance(string worker) =>
            BreakthroughChance(PatrolBreakthrough, Story.insightChance + Story.rumorBonus, PatrolFortune(worker));

        internal float PatrolChance(string worker) =>
            1 - (1 - PatrolInsightChance(worker)) * (1 - PatrolRealmChance(worker, Story.rumorBonus));

        internal float RumorDiscoveryChance(string worker) =>
            Mathf.Clamp01(.35f + .025f * (AssignmentSystem.Eloquence(this, worker) - 15));

        internal (string Label, float Chance, string Success, string Failure)[] DiplomaticAssignmentOutcomes(
            DiploAction action, MapFaction faction, string worker)
        {
            int improvement = action.id == "mediate" ? Mathf.Max(0, Mathf.Min(action.gain, 10 - Attitude(faction))) : action.gain;
            string success = $"態度 {improvement:+0;-0;0}";
            if (action.id is "trade" or "pact" or "alliance" or "vassal" or "merge") success += "、締立 1 項約書";
            string failure = $"態度 {Mathf.Min(0, action.fail):+0;-0;0}" +
                (action.id == "intimidate" ? "、聲望 −2" : "");
            return new[] { (action.name, DiploChance(action, faction, worker), success, failure) };
        }

        internal (string Label, float Chance, string Success, string Failure)[] AssignmentOutcomes(string action, string worker)
        {
            if (action == "explore")
            {
                var entry = RankSystem.Catalog.breakthroughs?.FirstOrDefault(value => value.id == PatrolBreakthrough);
                return new[]
                {
                    ("武學奇遇", PatrolInsightChance(worker), $"最強武學上限 +{entry?.capDelta ?? 0} 階", "上限不變"),
                    ("秘境入口", PatrolRealmChance(worker, Story.rumorBonus), "發現 1 處，可選擇進入", "未發現入口")
                };
            }
            if (action == "gather_rumors")
                return new[] { ("探得秘境", RumorDiscoveryChance(worker), "發現 1 處，可選擇進入", "只記下傳聞") };
            if (action is "forge_gear" or "forge")
                return ForgeGearOdds(worker, hall: action == "forge").Select((chance, grade) =>
                    ($"{GearGrades[grade]}品級", chance, $"{GearGrades[grade]}裝備 ×1", "取得其他品級裝備")).Reverse().ToArray();
            return Array.Empty<(string, float, string, string)>();
        }
    }
}
