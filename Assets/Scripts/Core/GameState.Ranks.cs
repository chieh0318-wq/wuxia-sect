using System;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    public sealed partial class GameState
    {
        internal float ProductionMultiplier(string id)
        {
            int best = RankSystem.Index(Array.Find(Protagonist.lines, line => line.id == id)?.rank);
            foreach (var disciple in Disciples.Where(member => member.id != Story.delegateId))
            {
                var line = disciple.lines?.FirstOrDefault(entry => entry.id == id);
                best = Math.Max(best, RankSystem.Index(line?.rank));
            }
            return RankSystem.Multiplier(RankSystem.At(best));
        }

        internal int BuildSilver(int amount) => Mathf.Max(0, Mathf.RoundToInt(
            amount * (1 + Story.buildCostPct / 100f) / ProductionMultiplier("construction")));

        internal int WeaponRankDelta(string weapon)
        {
            if (string.IsNullOrEmpty(weapon))
                return 0;
            return RankSystem.Catalog.breakthroughs?.FirstOrDefault(entry => entry.id == weapon && entry.type == "weapon")
                ?.effectiveRankDelta ?? 1;
        }

        internal string TrainMartial()
        {
            var hero = Protagonist;
            hero.martialTraining++;
            int monthKey = Year * 12 + Month;
            if (hero.lastTrainingMonth != monthKey)
            {
                hero.lastTrainingMonth = monthKey;
                hero.trainingMonths++;
            }
            var line = RankSystem.BestCombat(hero.lines);
            if (line == null)
                return "";
            float experience = Mathf.Max(.1f, 1 + Story.trainBonusPct / 100f) * ProductionMultiplier("teaching");
            TrainLine(line, experience);
            foreach (var disciple in Disciples.Where(member => member.id != Story.delegateId))
                TrainLine(RankSystem.BestCombat(disciple.lines), experience);


            return CheckMartialPath(false);
        }

        private static void TrainLine(SkillLine line, float experience)
        {
            if (line == null || RankSystem.Index(line.rank) >= RankSystem.Index(line.cap))
                return;
            line.progress += experience;
            int steps = (int)(line.progress / 2);
            RankSystem.Shift(line, steps, false);
            line.progress -= steps * 2;
        }

        internal string TryBreakthrough(string id, float bonus)
        {
            var entry = RankSystem.Catalog.breakthroughs?.FirstOrDefault(value => value.id == id);
            if (entry == null || (entry.oneTime && Protagonist.breakthroughs.Contains(id)) ||
                UnityEngine.Random.value >= Mathf.Clamp01(entry.chance + bonus))
                return "";
            var line = entry.target == "martial" ? RankSystem.BestCombat(Protagonist.lines) :
                Array.Find(Protagonist.lines, value => value.id == entry.target);
            if (line == null || RankSystem.Index(line.cap) >= RankSystem.Index("SS"))
                return "";
            RankSystem.Shift(line, entry.capDelta, true);
            if (entry.oneTime)
                Protagonist.breakthroughs = Protagonist.breakthroughs.Append(id).ToArray();
            return $"突破：{entry.name}　{line.rank}／{line.cap}";
        }

        private string CheckMartialPath(bool monthEnd)
        {
            var hero = Protagonist;
            var strongest = RankSystem.BestCombat(hero.lines);
            if (strongest == null || RankSystem.Index(strongest.cap) >= RankSystem.Index("SS"))
                return "";
            bool learnedCombat = (M2Data.Load<SkillCatalog>("skills").skills ?? Array.Empty<CreationOption>())
                .Any(art => hero.skills.Contains(art.id) && RankSystem.IsCombat(art.category));
            bool martialTalent = (M2Data.Load<TalentCatalog>("talents").talents ?? Array.Empty<CreationOption>())
                .Any(talent => hero.talents.Contains(talent.id) && (talent.effects ?? Array.Empty<GameEffect>())
                    .Any(effect => effect.key == "trainBonusPct" || effect.key == "rank" ||
                        effect.key == "str" || RankSystem.IsCombat(effect.@ref)));
            if (!learnedCombat && !martialTalent)
                return "";

            var path = RankSystem.Catalog.protagonistPath?.martialFocusSteps ?? new[]
            {
                new FocusStep { breakthrough = "seclusion_houshan" },
                new FocusStep { breakthrough = "manual_xiaoshan_full" },
                new FocusStep { breakthrough = "mentor_master_return" }
            };
            foreach (var step in path)
            {
                if (hero.breakthroughs.Contains(step.breakthrough))
                    continue;
                bool eligible = step.breakthrough switch
                {
                    "seclusion_houshan" => !monthEnd && hero.trainingMonths >= 6,
                    "manual_xiaoshan_full" => !monthEnd && hero.breakthroughs.Contains("seclusion_houshan") &&
                        (RankSystem.Index(strongest.rank) >= RankSystem.Index("S") || hero.trainingMonths >= 18),
                    "mentor_master_return" => monthEnd && Year >= 3 && Month == 1 && hero.trainingMonths >= 6,
                    _ => false
                };
                if (!eligible)
                    continue;
                var entry = RankSystem.Catalog.breakthroughs?.FirstOrDefault(value => value.id == step.breakthrough);
                var target = entry == null || entry.target == "martial" ? strongest :
                    Array.Find(hero.lines, value => value.id == entry.target);
                if (target == null)
                    continue;
                RankSystem.Shift(target, entry?.capDelta ?? 1, true);
                hero.breakthroughs = hero.breakthroughs.Append(step.breakthrough).ToArray();
                return $"突破：{entry?.name ?? "武學精進"}　{target.rank}／{target.cap}";
            }
            return "";
        }
        internal void RecoverInjuries()
        {
            int amount = Mathf.Max(1, Mathf.RoundToInt(ProductionMultiplier("medicine")));
            Protagonist.injury = Mathf.Max(0, Protagonist.injury - amount);
            foreach (var member in Disciples)
                member.injury = Mathf.Max(0, member.injury - amount);
        }
    }
}
