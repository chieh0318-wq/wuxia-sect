using System;
using System.Collections.Generic;
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

        internal IEnumerable<MartialArt> TrainingManuals(string worker) =>
            (worker == HeroId ? ProtagonistArts : ArtsOf(Disciples.Find(member => member.id == worker)))
                .Where(art => art.Combat).GroupBy(art => art.id).Select(group => group.First());

        internal string TrainMartial(string worker = HeroId, string manual = null)
        {
            bool hero = worker == HeroId;
            var lines = hero ? Protagonist.lines : Disciples.Find(member => member.id == worker)?.lines;
            if (lines == null) return "";
            var art = TrainingManuals(worker).FirstOrDefault(entry => entry.id == manual);
            if (manual != null && art == null) return "";
            var line = art != null ? Array.Find(lines, entry => entry.id == art.category) :
                lines.Where(entry => RankSystem.IsCombat(entry.id) && RankSystem.Index(entry.rank) < RankSystem.Index(entry.cap))
                    .OrderByDescending(entry => RankSystem.Index(entry.rank)).FirstOrDefault() ?? RankSystem.BestCombat(lines);
            if (line == null) return "";

            float experience = Mathf.Max(.1f, 1 + Story.trainBonusPct / 100f) *
                AssignmentSystem.Predict(this, "train", worker).Quality * Mathf.Max(.5f, 1 + .05f * AttributeBonus("wit"));
            if (hero)
                experience *= Mathf.Max(.5f, 1 + .02f * AttributeBonus("str")) * StudyMultiplier(line.id);
            if (hero && line.id == Protagonist.major) experience *= 1.2f;
            TrainLine(line, experience);
            if (art != null)
            {
                string id = worker + ":" + art.id;
                var record = Story.manualTraining.FirstOrDefault(entry => entry.id == id);
                if (record == null)
                {
                    record = new ManualPractice { id = id };
                    Story.manualTraining = Story.manualTraining.Append(record).ToArray();
                }
                record.experience += experience;
            }
            if (!hero) return "";
            Protagonist.martialTraining++;
            int monthKey = Year * 12 + Month;
            if (Protagonist.lastTrainingMonth != monthKey)
            {
                Protagonist.lastTrainingMonth = monthKey;
                Protagonist.trainingMonths++;
            }
            return CheckMartialPath(false);
        }

        /// <summary>v1124 C13: talent study speed, e.g. 劍癡 +30% on 劍, 天縱奇才 on every combat line.</summary>
        internal float StudyMultiplier(string lineId)
        {
            var study = Protagonist.study ?? Array.Empty<StoryValue>();
            int pct = ValueOf(study, lineId) + (RankSystem.IsCombat(lineId) ? ValueOf(study, "all") : 0)
                + (lineId == Protagonist.major ? ValueOf(study, "martial") : 0);
            return Mathf.Max(.25f, 1 + pct / 100f);
        }

        /// <summary>Study keys: "all" every combat line (ref ""), "martial" the 主修, otherwise a line id.</summary>
        private static string StudyKey(string id) => string.IsNullOrEmpty(id) ? "all" : id;

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
            if (line == null || RankSystem.Index(line.cap) >= RankSystem.Index(RankSystem.Top))
                return "";
            RankSystem.Shift(line, entry.capDelta, true);
            if (entry.oneTime)
                Protagonist.breakthroughs = Protagonist.breakthroughs.Append(id).ToArray();
            return $"突破：{entry.name}　{line.rank}／{line.cap}" + RecordArt(entry.art);
        }

        private string CheckMartialPath(bool monthEnd)
        {
            var hero = Protagonist;
            var strongest = RankSystem.BestCombat(hero.lines);
            if (strongest == null || RankSystem.Index(strongest.cap) >= RankSystem.Index(RankSystem.Top))
                return "";
            bool learnedCombat = (M2Data.Load<SkillCatalog>("skills").skills ?? Array.Empty<CreationOption>())
                .Any(art => hero.skills.Contains(art.id) && RankSystem.IsCombat(art.category));
            bool martialTalent = (M2Data.Load<TalentCatalog>("talents").talents ?? Array.Empty<CreationOption>())
                .Any(talent => hero.talents.Contains(talent.id) && (talent.effects ?? Array.Empty<GameEffect>())
                    .Any(effect => effect.key == "trainBonusPct" || effect.key == "rank" || effect.key == "studyPct" ||
                        effect.key == "str" || effect.@ref == "martial" || RankSystem.IsCombat(effect.@ref)));
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
                return $"突破：{entry?.name ?? "武學精進"}　{target.rank}／{target.cap}" + RecordArt(entry?.art);
            }
            return "";
        }
        internal void RecoverInjuries(string worker = null)
        {
            float quality = worker == null ? ProductionMultiplier("medicine")
                : AssignmentSystem.Predict(this, "medicine", worker).Quality;
            int amount = Mathf.Max(1, Mathf.RoundToInt(quality * (worker == null ? 1 : 1.5f)));
            // 根骨 governs how fast the protagonist's own wounds close.
            Protagonist.injury = Mathf.Max(0, Protagonist.injury - Mathf.Max(1, amount + AttributeBonus("con") / 3));
            foreach (var member in Disciples)
                member.injury = Mathf.Max(0, member.injury - amount);
        }
    }
}
