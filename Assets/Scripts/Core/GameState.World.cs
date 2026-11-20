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
                PendingReports.Add(EndMonth());
            }
            Story.day = day;
        }

        public bool IsFactionKnown(string id) =>
            (Story.knownFactions ?? Array.Empty<string>()).Contains(id) ||
            (WorldMapData.Load().Factions.FirstOrDefault(faction => faction.id == id)?.known ?? false);

        public int RelationShift(string id) =>
            (Story.relations ?? Array.Empty<StoryValue>()).FirstOrDefault(entry => entry.id == id)?.value ?? 0;

        public void SetFactionKnown(string id)
        {
            if (string.IsNullOrEmpty(id) || IsFactionKnown(id))
                return;
            Story.knownFactions = (Story.knownFactions ?? Array.Empty<string>()).Append(id).ToArray();
        }
    }
}
