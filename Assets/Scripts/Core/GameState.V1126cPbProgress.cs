using System;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    public sealed partial class StoryProgress
    {
        public WorkPracticePb[] workPracticePb = Array.Empty<WorkPracticePb>();
        public StudyOrderPb[] studyOrdersPb = Array.Empty<StudyOrderPb>();
        public int martialProgressScalePb;
    }

    [Serializable] public sealed class WorkPracticePb
    {
        public string member, skill;
        public int initialCap, tasks, xp;
    }

    [Serializable] public sealed class StudyOrderPb
    {
        public string member, art;
    }

    public sealed partial class GameState
    {
        internal static int MartialProgressMax => Mathf.Max(1, BalanceV1126c.Martial.progressMax);
        internal static int LifeProgressMax => Mathf.Max(1, BalanceV1126c.LifeSkill.xpPerLevel);
        internal SkillLine[] MemberLines(string id) => (id == HeroId ? Protagonist.lines :
            Disciples.Find(member => member.id == id)?.lines) ?? Array.Empty<SkillLine>();
        internal WorkPracticePb WorkPractice(string id, string skill) =>
            Story.workPracticePb?.FirstOrDefault(record => record?.member == id && record.skill == skill);

        internal void MigrateMartialProgress()
        {
            int previousScale = Story.martialProgressScalePb;
            if (previousScale == MartialProgressMax) return;
            // Earlier saves stored the fraction of a two-point martial experience tier.
            float ratio = MartialProgressMax / (float)(previousScale > 0 ? previousScale : 2);
            foreach (var line in MemberLines(HeroId).Concat(Disciples.SelectMany(member => MemberLines(member.id))))
                if (line != null && RankSystem.IsCombat(line.id))
                    line.progress = Mathf.Clamp(line.progress * ratio, 0, MartialProgressMax - .01f);
            Story.martialProgressScalePb = MartialProgressMax;
        }

        internal static bool CanAdvance(SkillLine line) => line != null &&
            RankSystem.Index(line.rank) < RankSystem.Index(line.cap);

        internal void PracticeWork(string id, string skill)
        {
            if (skill == null || RankSystem.IsCombat(skill)) return;
            var line = MemberLines(id).FirstOrDefault(entry => entry?.id == skill);
            if (line == null) return;
            var record = WorkPractice(id, skill);
            if (record == null)
            {
                record = new WorkPracticePb { member = id, skill = skill, initialCap = RankSystem.Index(line.cap) };
                Story.workPracticePb = (Story.workPracticePb ?? Array.Empty<WorkPracticePb>()).Append(record).ToArray();
            }
            var balance = BalanceV1126c.LifeSkill;
            record.tasks++;
            int target = record.initialCap + Mathf.Min(balance.capMaxAboveStart,
                record.tasks / Mathf.Max(1, balance.capGainPerTasks));
            RankSystem.Shift(line, Mathf.Max(0, target - RankSystem.Index(line.cap)), true);
            record.xp = Mathf.Min(record.xp + balance.xpPerTask,
                (RankSystem.Index(line.cap) - RankSystem.Index(line.rank)) * LifeProgressMax + LifeProgressMax);
            while (CanAdvance(line) && record.xp >= LifeProgressMax)
            {
                RankSystem.Shift(line, 1, false);
                record.xp -= LifeProgressMax;
            }
        }

        internal MartialArt CurrentStudy(string id, out bool assigned)
        {
            var lines = MemberLines(id);
            var arts = TrainingManuals(id);
            var order = Story.studyOrdersPb?.FirstOrDefault(entry => entry?.member == id);
            var manual = arts.FirstOrDefault(art => art.id == order?.art && CanStudy(art));
            assigned = manual != null;
            if (assigned) return manual;
            return arts.Where(CanStudy).OrderByDescending(art => id == HeroId && art.category == Protagonist.major)
                .ThenByDescending(art => RankSystem.Index(Line(art).rank))
                .ThenByDescending(art => RankSystem.Index(art.tier)).FirstOrDefault();

            SkillLine Line(MartialArt art) => lines.FirstOrDefault(line => line?.id == art.category);
            bool CanStudy(MartialArt art) => CanAdvance(Line(art));
        }

        internal SkillLine CurrentStudyLine(string id)
        {
            var lines = MemberLines(id);
            var art = CurrentStudy(id, out _);
            if (art != null) return lines.FirstOrDefault(line => line?.id == art.category);
            var major = id == HeroId ? RankSystem.Major(lines, Protagonist.major) : null;
            return CanAdvance(major) ? major : lines.Where(line => RankSystem.IsCombat(line?.id) && CanAdvance(line))
                .OrderByDescending(line => RankSystem.Index(line.rank)).FirstOrDefault();
        }

        public bool AssignStudy(string memberId, string artId)
        {
            if (MemberLines(memberId).Length == 0) return false;
            if (!string.IsNullOrEmpty(artId))
            {
                var art = TrainingManuals(memberId).FirstOrDefault(entry => entry.id == artId);
                if (art == null || !CanAdvance(MemberLines(memberId).FirstOrDefault(line => line.id == art.category)))
                    return false;
            }
            var orders = (Story.studyOrdersPb ?? Array.Empty<StudyOrderPb>()).Where(entry => entry?.member != memberId);
            if (!string.IsNullOrEmpty(artId)) orders = orders.Append(new StudyOrderPb { member = memberId, art = artId });
            Story.studyOrdersPb = orders.ToArray();
            return true;
        }

        public bool GrantManual(string memberId, string artId)
        {
            var art = MartialArts.Find(artId);
            if (art == null || !art.Combat || MemberLines(memberId).All(line => line?.id != art.category)) return false;
            if (memberId == HeroId)
            {
                if (KnowsArt(art.id)) return false;
                LearnArt(art.id);
                return true;
            }
            var member = Disciples.Find(entry => entry.id == memberId);
            if (member == null || ArtsOf(member).Contains(art)) return false;
            return LearnDiscipleArt(member, art.id, false).Length > 0;
        }

        internal string StudyLabel(string id, bool compact = false)
        {
            var art = CurrentStudy(id, out bool assigned);
            var line = CurrentStudyLine(id);
            string prefix = compact ? "" : "修習中　";
            if (line == null) return prefix + "已達上限";
            string title = art?.name ?? $"基礎{EffectText.LineName(line.id)}";
            string mode = assigned ? "掌門指定" : "自選";
            return $"{prefix}{title}　{line.progress:0.#}／{MartialProgressMax}{(compact ? "\n" : "　")}{mode}";
        }

        internal static string MartialLineProgress(SkillLine line) => line == null ? "已達上限" :
            $"{EffectText.LineName(line.id)} {line.rank}／{line.cap}　" +
            (CanAdvance(line) ? $"{line.progress:0.#}／{MartialProgressMax}" : "已達上限");

        internal string PracticeMartial(string id, float amount) => PracticeMartialLine(id, CurrentStudyLine(id), amount);

        private string PracticeMartialLine(string id, SkillLine line, float amount)
        {
            string before = MartialLineProgress(line);
            TrainLine(line, amount / Mathf.Max(1, BalanceV1126c.Martial.drillProgress));
            if (line != null && !CanAdvance(line))
            {
                var order = Story.studyOrdersPb?.FirstOrDefault(entry => entry?.member == id);
                if (MartialArts.Find(order?.art)?.category == line.id) AssignStudy(id, null);
            }
            string after = MartialLineProgress(line);
            return before == after ? after : $"{before} → {after}";
        }

        internal static float PracticeProgressFor(SkillLine line, float points) => CanAdvance(line)
            ? ProgressFor(line, points / Mathf.Max(1, BalanceV1126c.Martial.drillProgress)) : 0;

        internal float DrillGain(string teacher) => BalanceV1126c.Martial.drillProgress *
            AssignmentSystem.Predict(this, "train", teacher).Quality *
            (AssignmentSystem.Skill(this, teacher, "teaching") >= RankSystem.Index("B")
                ? 1 + BalanceV1126c.Martial.teacherBonusPct / 100f : 1);
    }
}
