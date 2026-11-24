using System;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    [Serializable] public sealed class SkillLine { public string id, rank, cap; public float progress; }
    [Serializable] internal sealed class RankTier { public string id, color; public float statMul; }
    [Serializable] internal sealed class RankCatalog
    {
        public RankTier[] tiers;
        public Breakthrough[] breakthroughs;
        public ProtagonistPath protagonistPath;
    }
    [Serializable] internal sealed class Breakthrough
    {
        public string id, name, type, target, requirement, desc;
        /// <summary>v1124 C14: the martial art (martial_arts.json id) this breakthrough teaches.</summary>
        public string art;
        public int capDelta, effectiveRankDelta;
        public float chance;
        public bool oneTime;
    }
    [Serializable] internal sealed class ProtagonistPath
    {
        public string defaultRank, defaultCap;
        public FocusStep[] martialFocusSteps;
    }
    [Serializable] internal sealed class FocusStep { public string breakthrough, condition; }
    [Serializable] internal sealed class SkillCategory { public string id, name, family; }

    public static class RankSystem
    {
        private static readonly string[] Tiers = { "E", "D", "C", "B", "A", "A+", "S", "SS", "SSS" };
        private static readonly string[] FallbackColors =
            { "#6a6a66", "#7a7568", "#7c776d", "#a37649", "#548461", "#4a918a", "#aa4234", "#db894e", "#e7bd64" };
        private static readonly float[] Multipliers = { .8f, .9f, 1, 1.25f, 1.6f, 1.85f, 2.2f, 2.8f, 3.4f };
        /// <summary>Primary sect art (劍) base tiers before any creation pick; every other line starts one tier lower.</summary>
        internal const string PrimaryRank = "C", PrimaryCap = "B", BaseRank = "D", BaseCap = "C";
        internal static string Top => Tiers[Tiers.Length - 1];
        internal static int Count => Tiers.Length;
        internal static readonly RankCatalog Catalog = M2Data.Load<RankCatalog>("ranks");
        internal static readonly SkillCategory[] Categories = M2Data.Load<SkillCatalog>("skills").categories
            ?? new[]
            {
                Category("fist", "拳掌"), Category("blade", "刀"), Category("sword", "劍"),
                Category("hidden", "暗器"), Category("qinggong", "輕功"), Category("neigong", "內功"),
                Category("medicine", "醫術", true), Category("alchemy", "煉丹製藥", true),
                Category("forging", "鍛造", true), Category("commerce", "經商", true),
                Category("farming", "耕作", true), Category("construction", "建築", true),
                Category("teaching", "教導", true)
            };

        private static SkillCategory Category(string id, string name, bool production = false) =>
            new() { id = id, name = name, family = production ? "production" : "combat" };

        // Saves from the 7-tier era may still hold "S+"; it was the tier just above S, now named SS.
        internal static int Index(string tier) => Math.Max(0, Array.IndexOf(Tiers, tier == "S+" ? "SS" : tier));
        internal static string At(int index) => Tiers[Mathf.Clamp(index, 0, Tiers.Length - 1)];
        public static string Format(string rank, string cap) => $"{At(Index(rank))} / {At(Index(cap))}";
        /// <summary>Tier order, or -1 for a missing/unknown tier (used for sorting intel).</summary>
        internal static int Order(string tier) => Array.IndexOf(Tiers, tier == "S+" ? "SS" : tier);
        /// <summary>Like <see cref="Format"/> but shows "?" for missing data instead of defaulting to C.</summary>
        internal static string FormatKnown(string rank, string cap) =>
            $"{(Order(rank) < 0 ? "?" : rank)} / {(Order(cap) < 0 ? "?" : cap)}";
        public static Color Color(string tier)
        {
            var configured = Catalog.tiers?.FirstOrDefault(entry => entry.id == tier)?.color;
            ColorUtility.TryParseHtmlString(configured ?? FallbackColors[Index(tier)], out var color);
            return color;
        }

        public static float Multiplier(string tier)
        {
            float value = Catalog.tiers?.FirstOrDefault(entry => entry.id == tier)?.statMul ?? 0;
            return value > 0 ? value : Multipliers[Index(tier)];
        }

        internal static SkillLine[] Seed(SkillLine[] existing, string rank, string cap, string defaultCap,
            string defaultRank = null)
        {
            var primary = Categories.Any(entry => entry.id == "sword") ? "sword" :
                Categories.FirstOrDefault(entry => entry.family == "combat")?.id;
            bool quickSeed = existing == null || existing.Length == 0;
            return Categories.Select(category =>
            {
                var line = existing?.FirstOrDefault(entry => entry != null && entry.id == category.id);
                string current = line?.rank ?? (quickSeed && category.id == primary ? rank : null) ?? defaultRank;
                string ceiling = line?.cap ?? (quickSeed && category.id == primary ? cap : null) ?? defaultCap;
                return new SkillLine { id = category.id, rank = At(Math.Min(Index(current), Index(ceiling))),
                    cap = At(Index(ceiling)), progress = line?.progress ?? 0 };
            }).ToArray();
        }

        /// <summary>v1124 C12: the protagonist's chosen 主修 line, falling back to the strongest combat line.</summary>
        internal static SkillLine Major(SkillLine[] lines, string major) =>
            (IsCombat(major) ? Array.Find(lines ?? Array.Empty<SkillLine>(), line => line?.id == major) : null)
            ?? BestCombat(lines ?? Array.Empty<SkillLine>());

        internal static SkillLine BestCombat(SkillLine[] lines) =>
            lines.Where(line => IsCombat(line.id)).OrderByDescending(line => Index(line.rank))
                .ThenByDescending(line => Index(line.cap)).FirstOrDefault();

        internal static bool IsCombat(string id) => Categories.Any(category => category.id == id && category.family == "combat");

        internal static string Martial(SkillLine[] lines, bool cap = false, int weaponDelta = 0)
        {
            var best = lines.Where(line => IsCombat(line.id)).Select(line => Index(cap ? line.cap : line.rank))
                .OrderByDescending(tier => tier).Take(2).ToArray();
            int tier = best.Length == 0 ? 0 : best[0];
            if (best.Length == 2 && best[1] >= Index("A") && tier - best[1] <= 1)
                tier++;
            return At(tier + (cap ? 0 : weaponDelta));
        }

        internal static void Shift(SkillLine line, int delta, bool cap)
        {
            if (line == null)
                return;
            if (cap)
                line.cap = At(Index(line.cap) + delta);
            line.rank = At(Math.Min(Index(line.cap), Index(line.rank) + (cap ? 0 : delta)));
        }

        /// <summary>
        /// Additive climb: the cap rises by <paramref name="capDelta"/> and the rank by <paramref name="rankDelta"/>;
        /// if the rank would pass the cap, the cap is carried up with it. Both clamp at the top tier (SSS).
        /// </summary>
        internal static void Raise(SkillLine line, int rankDelta, int capDelta)
        {
            if (line == null)
                return;
            int cap = Mathf.Clamp(Index(line.cap) + capDelta, 0, Tiers.Length - 1);
            int rank = Mathf.Clamp(Index(line.rank) + rankDelta, 0, Tiers.Length - 1);
            line.cap = At(Math.Max(cap, rank));
            line.rank = At(rank);
        }

        /// <summary>Training tiers earned by the creation pick, bounded by the resulting potential.</summary>
        internal static int LearnSteps(CreationOption art) => Mathf.Clamp(art?.cost ?? 0, 1, 3);

        internal static int LearnCap(CreationOption art) => Mathf.Clamp(art.capBonus, 0, art.cost >= 3 ? 2 : 1);

        internal static void Learn(SkillLine[] lines, CreationOption art)
        {
            var line = Array.Find(lines, entry => entry.id == art.category);
            Shift(line, LearnCap(art), true);
            Shift(line, LearnSteps(art), false);
        }
    }
}
