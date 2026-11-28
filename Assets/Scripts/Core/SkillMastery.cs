using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    /// <summary>
    /// v1128 skill rework: learned martial skills (creation picks + acquired manuals) grant permanent category-specific
    /// combat bonuses (劍法/刀法/拳掌/槍棍/暗器/內功/輕功) plus dual-line mastery synergy, instead of inflating raw rank.
    /// </summary>
    internal static class SkillMastery
    {
        private static Dictionary<string, CreationOption> skillCatalog;

        private static Dictionary<string, CreationOption> Skills => skillCatalog ??=
            (M2Data.Load<SkillCatalog>("skills")?.skills ?? Array.Empty<CreationOption>())
            .Where(s => s != null && !string.IsNullOrEmpty(s.id))
            .GroupBy(s => s.id).ToDictionary(g => g.Key, g => g.First());

        internal readonly struct Bonus
        {
            public readonly float AttackPct, QiPowerPct, MaxHpPct, MaxQiPct, OuterDefPct, InnerDefPct;
            public readonly int FlatAttack, Speed, QiRegen;
            public readonly float Dodge, Hit, Crit;

            public Bonus(float attackPct, float qiPowerPct, float maxHpPct, float maxQiPct,
                float outerDefPct, float innerDefPct, int flatAttack, int speed, int qiRegen,
                float dodge, float hit, float crit)
            {
                AttackPct = attackPct; QiPowerPct = qiPowerPct; MaxHpPct = maxHpPct; MaxQiPct = maxQiPct;
                OuterDefPct = outerDefPct; InnerDefPct = innerDefPct; FlatAttack = flatAttack;
                Speed = speed; QiRegen = qiRegen; Dodge = dodge; Hit = hit; Crit = crit;
            }
        }

        private static int TierWeight(int cost) => cost >= 3 ? 3 : cost == 2 ? 2 : 1;

        private static int ArtWeight(MartialArt art)
        {
            int idx = RankSystem.Index(art?.tier);
            return idx >= RankSystem.Index("S") ? 3 : idx >= RankSystem.Index("A") ? 2 : 1;
        }

        /// <summary>Computes cumulative passive bonuses from all learned skills, manuals, and dual-line mastery.</summary>
        internal static Bonus Compute(SkillLine[] lines, IEnumerable<string> skillIds, IEnumerable<string> artIds = null)
        {
            float atkPct = 0f, qiPowPct = 0f, hpPct = 0f, qiPct = 0f, oDefPct = 0f, iDefPct = 0f;
            int flatAtk = 0, speed = 0, qiRegen = 0;
            float dodge = 0f, hit = 0f, crit = 0f;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in skillIds ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                if (Skills.TryGetValue(id, out var opt))
                    Accumulate(opt.category, TierWeight(opt.cost));
            }
            foreach (string id in artIds ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                var art = MartialArts.Find(id);
                if (art != null && art.Combat)
                    Accumulate(art.category, ArtWeight(art));
            }

            // Dual-line synergy replaces the old free "+1 rank tier" inflation.
            float dual = RankSystem.DualMasteryBonus(lines);
            atkPct += dual;
            qiPowPct += dual;

            return new Bonus(
                Mathf.Min(atkPct, 0.36f), Mathf.Min(qiPowPct, 0.38f),
                Mathf.Min(hpPct, 0.24f), Mathf.Min(qiPct, 0.32f),
                Mathf.Min(oDefPct, 0.24f), Mathf.Min(iDefPct, 0.28f),
                Mathf.Min(flatAtk, 24), Mathf.Min(speed, 18), Mathf.Min(qiRegen, 5),
                Mathf.Min(dodge, 0.10f), Mathf.Min(hit, 0.12f), Mathf.Min(crit, 0.12f));

            void Accumulate(string category, int w)
            {
                switch (category)
                {
                    case "sword":
                        atkPct += w == 3 ? 0.09f : w == 2 ? 0.05f : 0.03f;
                        crit += w == 3 ? 0.03f : w == 2 ? 0.02f : 0.01f;
                        break;
                    case "blade":
                        atkPct += w == 3 ? 0.10f : w == 2 ? 0.06f : 0.03f;
                        flatAtk += w == 3 ? 7 : w == 2 ? 4 : 2;
                        break;
                    case "fist":
                        hpPct += w == 3 ? 0.08f : w == 2 ? 0.05f : 0.03f;
                        oDefPct += w == 3 ? 0.08f : w == 2 ? 0.05f : 0.03f;
                        break;
                    case "polearm":
                        atkPct += w == 3 ? 0.08f : w == 2 ? 0.05f : 0.03f;
                        speed += w == 3 ? 3 : w == 2 ? 2 : 1;
                        break;
                    case "hidden":
                        hit += w == 3 ? 0.04f : w == 2 ? 0.02f : 0.01f;
                        crit += w == 3 ? 0.03f : w == 2 ? 0.02f : 0.01f;
                        speed += w == 3 ? 3 : w == 2 ? 2 : 1;
                        break;
                    case "neigong":
                        qiPowPct += w == 3 ? 0.10f : w == 2 ? 0.06f : 0.03f;
                        qiPct += w == 3 ? 0.10f : w == 2 ? 0.06f : 0.04f;
                        iDefPct += w == 3 ? 0.09f : w == 2 ? 0.05f : 0.03f;
                        if (w >= 2) qiRegen += 1;
                        break;
                    case "qinggong":
                        speed += w == 3 ? 6 : w == 2 ? 4 : 2;
                        dodge += w == 3 ? 0.03f : w == 2 ? 0.02f : 0.01f;
                        break;
                }
            }
        }

        internal static DerivedStats Apply(DerivedStats raw, SkillLine[] lines,
            IEnumerable<string> skillIds, IEnumerable<string> artIds = null)
        {
            var b = Compute(lines, skillIds, artIds);
            return new DerivedStats(
                Mathf.RoundToInt(raw.Attack * (1f + b.AttackPct)) + b.FlatAttack,
                raw.Speed + b.Speed,
                Mathf.RoundToInt(raw.MaxHp * (1f + b.MaxHpPct)),
                Mathf.RoundToInt(raw.MaxQi * (1f + b.MaxQiPct)),
                Mathf.RoundToInt(raw.QiPower * (1f + b.QiPowerPct)),
                Mathf.Clamp(raw.Dodge + b.Dodge, 0f, 0.75f),
                raw.Fortune, raw.Study,
                raw.Hit + b.Hit,
                Mathf.Clamp(raw.Crit + b.Crit, 0.02f, 0.60f),
                raw.QiRegen + b.QiRegen,
                Mathf.RoundToInt(raw.OuterDefense * (1f + b.OuterDefPct)),
                Mathf.RoundToInt(raw.InnerDefense * (1f + b.InnerDefPct)));
        }

        /// <summary>Short UI tag describing the permanent mastery bonus of a single creation skill.</summary>
        internal static string DescribeOption(CreationOption opt)
        {
            if (opt == null || string.IsNullOrEmpty(opt.category)) return "";
            int w = TierWeight(opt.cost);
            return opt.category switch
            {
                "sword" => $"劍法攻 +{(w == 3 ? 9 : w == 2 ? 5 : 3)}%・暴擊 +{w}%",
                "blade" => $"刀法攻 +{(w == 3 ? 10 : w == 2 ? 6 : 3)}%・重擊 +{(w == 3 ? 7 : w == 2 ? 4 : 2)}",
                "fist" => $"氣血 +{(w == 3 ? 8 : w == 2 ? 5 : 3)}%・外防 +{(w == 3 ? 8 : w == 2 ? 5 : 3)}%",
                "polearm" => $"槍棍攻 +{(w == 3 ? 8 : w == 2 ? 5 : 3)}%・先手 +{w}",
                "hidden" => $"命中 +{(w == 3 ? 4 : w == 2 ? 2 : 1)}%・暴擊 +{w}%・先手 +{w}",
                "neigong" => $"內功威 +{(w == 3 ? 10 : w == 2 ? 6 : 3)}%・內力 +{(w == 3 ? 10 : w == 2 ? 6 : 4)}%・內防 +{(w == 3 ? 9 : w == 2 ? 5 : 3)}%" + (w >= 2 ? "・回氣 +1" : ""),
                "qinggong" => $"先手 +{w * 2}・閃避 +{w}%",
                _ => ""
            };
        }
    }
}
