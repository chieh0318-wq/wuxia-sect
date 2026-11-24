using System;
using System.Collections.Generic;
using System.Linq;

namespace Wuxia
{
    [Serializable] internal sealed class QuestStep { public string text, metric; public int target; }
    [Serializable] internal sealed class QuestStage { public string id, title, brief; public QuestStep[] steps = Array.Empty<QuestStep>(); }
    [Serializable] internal sealed class QuestRoute { public string id, name, direction, condition, progress; }
    [Serializable] internal sealed class QuestIntro { public string id, text; }
    [Serializable] internal sealed class QuestBonds { public string hero, heroNoBackground, friend, master, uncle_schemer, disciple, recruit; }

    /// <summary>
    /// v1126 #8 / #16 / #18 (T-3 #8, #16, #14–15): quests.json — the 軍師's opening about the four routes, the
    /// 任務 panel's short-term stages and the four pinned long-term routes, the 客將 placeholder sect and the
    /// 與本門淵源 texts. Route conditions are data: an empty condition shows 「達成條件：待定」.
    /// </summary>
    [Serializable] internal sealed class QuestBoardData
    {
        public string guestSect = "（待定）派", guestRole = "客將", advisorTitle = "軍師";
        public string[] opening = Array.Empty<string>();
        public QuestIntro[] factionIntro = Array.Empty<QuestIntro>();
        public QuestRoute[] routes = Array.Empty<QuestRoute>();
        public QuestStage[] stages = Array.Empty<QuestStage>();
        public QuestBonds bonds = new();

        private static QuestBoardData data;
        internal static QuestBoardData Data
        {
            get
            {
                if (data != null) return data;
                try { data = M2Data.Load<QuestBoardData>("quests"); }
                catch (Exception error) { UnityEngine.Debug.LogWarning($"[Quests] quests.json unreadable: {error.Message}"); }
                return data ??= new QuestBoardData();
            }
        }
    }

    /// <summary>Read-only evaluation of the 任務 panel against the live game state (no rules are changed here).</summary>
    internal static class QuestBoard
    {
        internal static int Metric(GameState state, string metric)
        {
            var factions = WorldMapData.Load().Factions;
            switch (metric)
            {
                case "disciples": return state.Disciples.Count;
                case "buildings": return state.Buildings.Count(building => building != null);
                case "reputation": return state.Reputation;
                case "knownFactions": return factions.Count(faction => state.IsFactionKnown(faction.id));
                case "friendlyFactions":
                    return factions.Count(faction => state.IsFactionKnown(faction.id) &&
                        faction.StanceWith(state.RelationShift(faction.id)) == "friendly");
                case "loyalFactions": return state.Victory.Loyal + state.Victory.Subjects;
                case "heldNodes": return state.Victory.Held;
                default: return 0;
            }
        }

        internal static bool Done(GameState state, QuestStage stage) =>
            (stage.steps ?? Array.Empty<QuestStep>()).All(step => step == null || Metric(state, step.metric) >= step.target);

        /// <summary>The first short-term stage not yet met, or null once every stage is done.</summary>
        internal static QuestStage Current(GameState state) =>
            (QuestBoardData.Data.stages ?? Array.Empty<QuestStage>()).FirstOrDefault(stage => stage != null && !Done(state, stage));

        internal static IEnumerable<(string text, int current, int target)> Steps(GameState state, QuestStage stage) =>
            (stage?.steps ?? Array.Empty<QuestStep>()).Where(step => step != null)
                .Select(step => (step.text, Math.Min(Metric(state, step.metric), step.target), step.target));

        /// <summary>One line of progress for a long-term route; the built-in victories keep their own counters.</summary>
        internal static string RouteProgress(GameState state, QuestRoute route)
        {
            var victory = state.Victory;
            var rules = victory.Rules;
            switch (route.progress)
            {
                case "diplomacy":
                    return $"友好 {victory.Loyal}/{rules.loyalNeeded}・歸附 {victory.Subjects}/{rules.subjectNeeded}・聲望 {victory.Reputation}/{rules.reputationNeeded}" +
                        (victory.Diplomatic ? "　已可宣告" : "");
                case "conquest":
                    return $"據點 {victory.Held}/{rules.conquestNodes}・聲望 {victory.Reputation}/{rules.conquestReputation}" +
                        (victory.Conquest ? "　已達成" : "");
                default:
                    return string.IsNullOrWhiteSpace(route.condition) ? "達成條件：待定" : route.condition;
            }
        }
    }
}
