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
        private static readonly string[] Tiers = { "C", "B", "A", "A+", "S", "S+", "SS" };
        private static readonly string[] FallbackColors = { "#7c776d", "#a37649", "#548461", "#4a918a", "#aa4234", "#db894e", "#e7bd64" };
        private static readonly float[] Multipliers = { 1, 1.25f, 1.6f, 1.85f, 2.2f, 2.6f, 3.2f };
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

        internal static int Index(string tier) => Math.Max(0, Array.IndexOf(Tiers, tier));
        internal static string At(int index) => Tiers[Mathf.Clamp(index, 0, Tiers.Length - 1)];
        public static string Format(string rank, string cap) => $"{At(Index(rank))} / {At(Index(cap))}";
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

        internal static SkillLine[] Seed(SkillLine[] existing, string rank, string cap, string defaultCap)
        {
            var primary = Categories.Any(entry => entry.id == "sword") ? "sword" :
                Categories.FirstOrDefault(entry => entry.family == "combat")?.id;
            bool quickSeed = existing == null || existing.Length == 0;
            return Categories.Select(category =>
            {
                var line = existing?.FirstOrDefault(entry => entry != null && entry.id == category.id);
                string current = line?.rank ?? (quickSeed && category.id == primary ? rank : null);
                string ceiling = line?.cap ?? (quickSeed && category.id == primary ? cap : null) ?? defaultCap;
                return new SkillLine { id = category.id, rank = At(Math.Min(Index(current), Index(ceiling))),
                    cap = At(Index(ceiling)), progress = line?.progress ?? 0 };
            }).ToArray();
        }

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

        internal static void Learn(SkillLine[] lines, CreationOption art)
        {
            var line = Array.Find(lines, entry => entry.id == art.category);
            if (line == null)
                return;
            Shift(line, art.capBonus, true);
            line.rank = At(Math.Min(Index(line.cap), Math.Max(Index(line.rank), Index(art.grantsRank))));
        }
    }
}
