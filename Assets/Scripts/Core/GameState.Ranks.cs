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
            float best = IsDispatched(HeroId) ? 0 : WorkerProduction(HeroId, Protagonist.lines);
            foreach (var disciple in Disciples.Where(member => member.id != Story.delegateId && !IsDispatched(member.id)))
            {
                best = Mathf.Max(best, WorkerProduction(disciple.id, disciple.lines));
            }
            return best > 0 ? best : RankSystem.Multiplier("E");

            float WorkerProduction(string worker, SkillLine[] lines) =>
                RankSystem.Multiplier(lines?.FirstOrDefault(line => line.id == id)?.rank) *
                (1 + TalentValue(worker, id, 20) / 100f);
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

        internal string TrainMartial(string worker = HeroId, string manual = null, float? fixedGain = null)
        {
            bool hero = worker == HeroId;
            var lines = hero ? Protagonist.lines : Disciples.Find(member => member.id == worker)?.lines;
            if (lines == null) return "";
            var art = manual == null ? CurrentStudy(worker, out _) : TrainingManuals(worker).FirstOrDefault(entry => entry.id == manual);
            if (manual != null && art == null) return "";
            var line = art != null ? Array.Find(lines, entry => entry.id == art.category) : CurrentStudyLine(worker);
            if (line == null) return "";

            float experience = fixedGain ?? TrainingGain(worker, line, art);
            string practice = PracticeMartialLine(worker, line, experience);
            if (art != null)
            {
                string id = worker + ":" + art.id;
                var record = (Story.manualTraining ??= Array.Empty<ManualPractice>()).FirstOrDefault(entry => entry.id == id);
                if (record == null)
                {
                    record = new ManualPractice { id = id };
                    Story.manualTraining = Story.manualTraining.Append(record).ToArray();
                }
                record.experience += experience;
            }
            if (!hero) return practice;
            Protagonist.martialTraining++;
            int monthKey = Year * 12 + Month;
            if (Protagonist.lastTrainingMonth != monthKey)
            {
                Protagonist.lastTrainingMonth = monthKey;
                Protagonist.trainingMonths++;
            }
            return practice + "　" + CheckMartialPath(false);
        }

        internal float TrainingGain(string worker, SkillLine line, MartialArt art)
        {
            if (!CanAdvance(line)) return 0;
            bool hero = worker == HeroId;
            var lines = MemberLines(worker);
            float experience = BalanceV1126c.Martial.practiceProgress * Mathf.Max(.1f, 1 + Story.trainBonusPct / 100f) *
                AssignmentSystem.Predict(this, "train", worker).Quality *
                AttributeSystem.StudyFactor(AssignmentSystem.Attribute(this, worker, "wit"));
            // v1126b 決定 1: training speed comes from 悟性 only (武力 no longer adds its own share).
            if (hero)
                experience *= StudyMultiplier(line.id);
            if (TalentValue(worker, "growth", 1) > 0) experience *= Year <= 3 ? .9f : 1.3f;
            string major = hero ? Protagonist.major : RankSystem.BestCombat(lines)?.id;
            if (line.id == major) experience *= 1.2f;
            if (art != null) experience *= 1 + TalentValue(worker, "read", 43) / 100f;
            return experience;
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

        /// <summary>
        /// v1126c PA3: 修習進度 points that <paramref name="experience"/> (one 督促操練 ≈ 1) adds to a line at its current
        /// tier — martial.drillProgress per unit × martial.tierGainPct[tier] (higher tiers learn slower).
        /// </summary>
        internal static float ProgressFor(SkillLine line, float experience) =>
            experience * BalanceV1126c.Martial.drillProgress * BalanceV1126c.TierGainPct(RankSystem.Index(line?.rank)) / 100f;

        /// <summary>Progress points needed for one tier (v1128 tiered curve: E=60 … SS=1600).</summary>
        internal static int ProgressMax => MartialProgressMax;
        internal static int ProgressMaxFor(SkillLine line) => BalanceV1126c.ProgressMaxFor(RankSystem.Index(line?.rank));

        private static void TrainLine(SkillLine line, float experience)
        {
            if (line == null || RankSystem.Index(line.rank) >= RankSystem.Index(line.cap))
                return;
            // Each tier needs ProgressMaxFor(tier) points; higher tiers require substantially more XP.
            float left = experience;
            while (left > 0 && RankSystem.Index(line.rank) < RankSystem.Index(line.cap))
            {
                float perUnit = ProgressFor(line, 1);
                if (perUnit <= 0) break;
                int tierMax = ProgressMaxFor(line);
                float need = tierMax - line.progress;
                float gain = left * perUnit;
                if (gain < need) { line.progress += gain; break; }
                left -= need / perUnit;
                line.progress = 0;
                RankSystem.Shift(line, 1, false);
            }
            if (RankSystem.Index(line.rank) >= RankSystem.Index(line.cap))
                line.progress = Mathf.Min(line.progress, ProgressMaxFor(line) - 1);
        }

        internal string TryBreakthrough(string id, float bonus, float fortune = 1)
        {
            var entry = RankSystem.Catalog.breakthroughs?.FirstOrDefault(value => value.id == id);
            float chance = BreakthroughChance(id, bonus, fortune);
            if (chance <= 0 || UnityEngine.Random.value >= chance)
                return "";
            var line = entry.target == "martial" ? RankSystem.Major(Protagonist.lines, Protagonist.major) :
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
            var strongest = RankSystem.Major(hero.lines, hero.major);
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
                new FocusStep { breakthrough = "duel_milestone_3" },
                new FocusStep { breakthrough = "manual_xiaoshan_full" },
                new FocusStep { breakthrough = "duel_milestone_6" },
                new FocusStep { breakthrough = "mentor_master_return" }
            };
            foreach (var step in path)
            {
                if (hero.breakthroughs.Contains(step.breakthrough))
                    continue;
                bool eligible = step.breakthrough switch
                {
                    "seclusion_houshan" => !monthEnd && hero.trainingMonths >= BalanceV1126c.Martial.seclusionTrainingMonths,
                    "duel_milestone_3" => MastersDefeated >= 3 && RankSystem.Index(strongest.cap) < RankSystem.Index("SS"),
                    "manual_xiaoshan_full" => !monthEnd && hero.breakthroughs.Contains("seclusion_houshan") &&
                        (RankSystem.Index(strongest.rank) >= RankSystem.Index("S") || hero.trainingMonths >= BalanceV1126c.Martial.manualTrainingMonths),
                    "duel_milestone_6" => MastersDefeated >= 6 && RankSystem.Index(strongest.cap) < RankSystem.Index("SSS"),
                    "mentor_master_return" => monthEnd && Year >= BalanceV1126c.Martial.masterReturnYear && Month == 1 &&
                        hero.trainingMonths >= BalanceV1126c.Martial.seclusionTrainingMonths,
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
                if (step.breakthrough == "duel_milestone_3") TrainLine(target, 15f);
                else if (step.breakthrough == "duel_milestone_6") TrainLine(target, 25f);
                hero.breakthroughs = hero.breakthroughs.Append(step.breakthrough).ToArray();
                return $"突破：{entry?.name ?? "武學精進"}　{target.rank}／{target.cap}" + RecordArt(entry?.art);
            }
            return "";
        }
        internal void RecoverInjuries(string worker = null)
        {
            var (hero, amount) = RecoveryAmount(worker);
            Protagonist.injury = Mathf.Max(0, Protagonist.injury - hero);
            foreach (var member in Disciples)
                member.injury = Mathf.Max(0, member.injury - amount);
        }
    }
}
