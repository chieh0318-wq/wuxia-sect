using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    /// <summary>
    /// The save-backed world-map host: AP comes from the shared monthly pool, travel days advance the
    /// shared calendar (running month-end processing whenever a month boundary is crossed), and the
    /// party position and scouted factions are stored in <see cref="StoryProgress"/>.
    /// </summary>
    public sealed partial class GameState : IWorldMapHost
    {
        public const int DaysPerMonth = 30;

        /// <summary>Month-end reports produced while travelling; the bootstrap shows them when the map closes.</summary>
        internal readonly List<MonthlyReport> PendingReports = new();
        internal Action<string, Action<string[]>> ChooseMapParty;
        private string[] travellingMembers;

        internal bool CommitMapTravel(IEnumerable<string> party, int ap, string destination)
        {
            var members = party.ToArray();
            if (members.Length == 0 || members.Any(id => AssignmentSystem.Block(this, id) != null) ||
                !TrySpendAp(ap, "travel:" + destination)) return false;
            AssignmentSystem.Commit(this, members);
            travellingMembers = members;
            return true;
        }

        public int Day => Mathf.Clamp(Story.day, 1, DaysPerMonth);
        public string DateText => DateWithDay;

        public string StartNode
        {
            get
            {
                var map = WorldMapData.Load();
                return map.Nodes.ContainsKey(Story.mapNode ?? "") ? Story.mapNode : map.DefaultNode;
            }
        }

        public string PartyNode
        {
            get => string.IsNullOrEmpty(Story.partyNode) ? StartNode : Story.partyNode;
            set
            {
                if (Story.partyNode == value) return;
                Story.partyNode = value;
                EvaluateQuestlines();
            }
        }

        public void AdvanceDays(int days)
        {
            if (days <= 0)
                return;
            int day = Day + days;
            while (day > DaysPerMonth)
            {
                day -= DaysPerMonth;
                Story.day = 1;
                var before = (Silver, Food, Reputation);
                var report = EndMonth();
                report.Net = (Silver - before.Silver, Food - before.Food, Reputation - before.Reputation);
                PendingReports.Add(report);
            }
            Story.day = day;
        }

        public bool IsFactionKnown(string id) =>
            (Story.knownFactions ?? Array.Empty<string>()).Contains(id) ||
            (WorldMapData.Load().Factions.FirstOrDefault(faction => faction.id == id)?.known ?? false);

        public int RelationShift(string id) =>
            (Story.relations ?? Array.Empty<StoryValue>()).FirstOrDefault(entry => entry.id == id)?.value ?? 0;

        public string TravelFortune()
        {
            string leader = travellingMembers?.FirstOrDefault() ?? HeroId;
            float fortune = AttributeSystem.FortuneFactor(AssignmentSystem.Attribute(this, leader, "luk"));
            foreach (string member in travellingMembers ?? Array.Empty<string>())
                AssignmentSystem.Credit(this, member, 1);
            travellingMembers = null;
            float chance = Mathf.Clamp(.06f * fortune, .01f, .5f);
            if (UnityEngine.Random.value >= chance)
                return null;
            switch (UnityEngine.Random.Range(0, 3))
            {
                case 0:
                    int silver = UnityEngine.Random.Range(10, 31);
                    ApplyEffects(new[] { new GameEffect { key = "silver", value = silver } });
                    return $"奇遇：路旁古廟拾得前人遺下的錢囊，得銀兩 {silver}。";
                case 1:
                    ApplyEffects(new[] { new GameEffect { key = "reputation", value = 3 } });
                    return "奇遇：途中仗義解了商隊之圍，門派聲望上升。";
                default:
                    ApplyEffects(new[] { new GameEffect { key = "food", value = 20 } });
                    return "奇遇：山民感念相助，贈糧食 20。";
            }
        }

        public void SetFactionKnown(string id)
        {
            if (string.IsNullOrEmpty(id) || IsFactionKnown(id))
                return;
            Story.knownFactions = (Story.knownFactions ?? Array.Empty<string>()).Append(id).ToArray();
        }
    }
}
