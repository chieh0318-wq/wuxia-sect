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

        [Serializable] public sealed class MartialData
        { public int progressMax = 100; public int drillProgress = 6; public int practiceProgress = 4; public int teacherBonusPct = 25; }

        [Serializable] public sealed class BuildQualityData
        {
            public int upkeepPctPer10Above100 = -3; public int outputPctPer10Above100 = 4;
            public int durabilityMonthsPer10 = 2; public int maxQualityPct = 140; public int minQualityPct = 70;
        }

        [Serializable] public sealed class TradeData
        { public int tongshangSilverPerMonth = 6; public int startDelayMonths = 1; }

        [Serializable] public sealed class ReputationData
        { public int gainScalePct = 100; }

        /// <summary>One tech-tree node override (id matches the tech node id in code).</summary>
        [Serializable] public sealed class TechNodeData
        { public string id; public int silver; public int ap; public int tier; public string requires; public float value; }

        [Serializable] public sealed class VictoryData { }

        [Serializable] public sealed class Root
        {
            public LifeSkillData lifeSkill = new();
            public MartialData martial = new();
            public BuildQualityData buildQuality = new();
            public TradeData trade = new();
            public ReputationData reputation = new();
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
                root.trade ??= new(); root.reputation ??= new(); root.victory ??= new();
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
