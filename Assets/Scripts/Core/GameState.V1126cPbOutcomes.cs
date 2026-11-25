using System;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    public sealed partial class GameState
    {
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

        internal (string Label, float Chance, string Success, string Failure)[] AssignmentOutcomes(string action, string worker)
        {
            if (action == "explore")
            {
                var entry = RankSystem.Catalog.breakthroughs?.FirstOrDefault(value => value.id == PatrolBreakthrough);
                return new[]
                {
                    ("武學奇遇", PatrolInsightChance(worker), $"主修上限 +{entry?.capDelta ?? 0} 階", "上限不變"),
                    ("秘境入口", PatrolRealmChance(worker, Story.rumorBonus), "發現 1 處，可選擇進入", "未發現入口")
                };
            }
            if (action == "gather_rumors")
                return new[] { ("探得秘境", RumorDiscoveryChance(worker), "發現 1 處，可選擇進入", "只記下傳聞") };
            return Array.Empty<(string, float, string, string)>();
        }
    }
}
