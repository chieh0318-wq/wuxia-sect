using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Wuxia
{
    /// <summary>
    /// v1126c shared balance numbers. Read-only loader for StreamingAssets/Data/balance_v1126c.json.
    /// Owned by PA3 (balance). Other phases only READ values; numbers are tuned in the JSON.
    /// Defaults below mirror the JSON so a missing/invalid file still yields a playable game.
    /// </summary>
    public static class BalanceV1126c
    {
        [Serializable] public sealed class LifeSkillData
        { public int xpPerTask = 12; public int xpPerLevel = 100; public int capGainPerTasks = 10; public int capMaxAboveStart = 2; }

        /// <summary>
        /// Martial progress. progressMax points per tier; one 督促操練 (experience ≈ 1) adds drillProgress × tierGainPct[tier]%.
        /// tierGainPct is indexed E, D, C, B, A, A+, S, SS, SSS. seclusion/manual/masterReturn: the protagonist's fixed
        /// cap breakthroughs (後山閉關 after N training months, 《崤山劍經》全本 after N months or rank S, 師父歸來 in year N).
        /// </summary>
        [Serializable] public sealed class MartialData
        {
            public int progressMax = 100; public int drillProgress = 6; public int practiceProgress = 4; public int teacherBonusPct = 25;
            public int[] tierGainPct = { 200, 170, 140, 100, 70, 50, 35, 25, 20 };
            public int seclusionTrainingMonths = 12; public int manualTrainingMonths = 36; public int masterReturnYear = 5;
        }

        /// <summary>Sect housing: beds per 弟子廂房 (before 山門營造法式).</summary>
        [Serializable] public sealed class SectData { public int bedsPerQuarters = 5; }

        /// <summary>Per-action number overrides applied to actions.json at load (-1 = keep the data value).</summary>
        [Serializable] public sealed class ActionOverride { public string id; public int reputation = -1; public int silver = -1; public int food = -1; }

        [Serializable] public sealed class BuildQualityData
        {
            public int upkeepPctPer10Above100 = -3; public int outputPctPer10Above100 = 4;
            public int durabilityMonthsPer10 = 2; public int maxQualityPct = 140; public int minQualityPct = 70;
        }

        [Serializable] public sealed class TradeData
        { public int tongshangSilverPerMonth = 6; public int merchantSilverPerMonth = 12; public int startDelayMonths = 1; }

        [Serializable] public sealed class ReputationData
        { public int gainScalePct = 60; }

        /// <summary>
        /// One tech-tree node (id matches GameState.TechTree). silver/ap: research cost; requires: prerequisite node id
        /// (empty = previous tier of the same branch); minReputation / minScale: extra gates (聲望, 山門規模 index);
        /// minHeroRank: v1126d gate on the protagonist's major martial grade (E…SSS; empty = none);
        /// value / value2 / value3: the node's effect magnitudes (meaning per node documented in GameState.M6).
        /// </summary>
        [Serializable] public sealed class TechNodeData
        {
            public string id; public int silver; public int ap; public int tier; public string requires;
            public int minReputation; public int minScale; public float value; public float value2; public float value3;
            public string minHeroRank;
        }

        [Serializable] public sealed class VictoryData { }

        [Serializable] public sealed class Root
        {
            public LifeSkillData lifeSkill = new();
            public MartialData martial = new();
            public BuildQualityData buildQuality = new();
            public TradeData trade = new();
            public ReputationData reputation = new();
            public SectData sect = new();
            public ActionOverride[] actions = Array.Empty<ActionOverride>();
            public TechNodeData[] techTree = Array.Empty<TechNodeData>();
            public VictoryData victory = new();
        }

        private static Root data;

        public static Root Data => data ??= Load();
        public static LifeSkillData LifeSkill => Data.lifeSkill;
        public static MartialData Martial => Data.martial;
        public static BuildQualityData BuildQuality => Data.buildQuality;
        public static TradeData Trade => Data.trade;
        public static ReputationData Reputation => Data.reputation;
        public static SectData Sect => Data.sect;
        public static ActionOverride[] Actions => Data.actions;

        /// <summary>martial.tierGainPct for a tier index (E = 0 … SSS = 8); 100 when the table is short.</summary>
        public static int TierGainPct(int tier)
        {
            int[] table = Martial.tierGainPct;
            if (table == null || table.Length == 0) return 100;
            return table[Math.Clamp(tier, 0, table.Length - 1)];
        }

        /// <summary>The override row for an action id (null when none).</summary>
        public static ActionOverride Action(string id)
        {
            foreach (ActionOverride entry in Actions)
                if (entry != null && entry.id == id) return entry;
            return null;
        }
        public static TechNodeData[] TechTree => Data.techTree;
        public static VictoryData Victory => Data.victory;

        /// <summary>Scale a positive reputation gain by reputation.gainScalePct (rounded, keeps +1 minimum).</summary>
        public static int ScaleReputation(int gain)
        {
            if (gain <= 0) return gain;
            return Math.Max(1, (int)Math.Round(gain * Reputation.gainScalePct / 100.0));
        }

        public static TechNodeData Tech(string id)
        {
            foreach (TechNodeData node in TechTree)
                if (node != null && node.id == id) return node;
            return null;
        }

        /// <summary>Writes the actions[] overrides into the loaded action table (cost and effect are what the UI shows).</summary>
        public static void ApplyActionOverrides(ActionData[] actions)
        {
            if (actions == null) return;
            foreach (ActionData action in actions)
            {
                ActionOverride tuned = action == null ? null : Action(action.id);
                if (tuned == null) continue;
                action.effect ??= new Yield();
                if (tuned.reputation >= 0) action.effect.reputation = tuned.reputation;
                if (tuned.silver >= 0) action.effect.silver = tuned.silver;
                if (tuned.food >= 0) action.effect.food = tuned.food;
            }
        }

        /// <summary>Force a reload (editor tools / tests).</summary>
        public static void Reload() => data = null;

        private static Root Load()
        {
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, "Data", "balance_v1126c.json");
                Root root = File.Exists(path) ? JsonUtility.FromJson<Root>(File.ReadAllText(path, Encoding.UTF8)) : null;
                if (root == null) return new Root();
                root.lifeSkill ??= new(); root.martial ??= new(); root.buildQuality ??= new();
                root.trade ??= new(); root.reputation ??= new(); root.victory ??= new(); root.sect ??= new();
                root.actions ??= Array.Empty<ActionOverride>();
                root.techTree ??= Array.Empty<TechNodeData>();
                return root;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Wuxia: balance_v1126c.json: {ex.Message} Using built-in defaults.");
                return new Root();
            }
        }
    }
}
