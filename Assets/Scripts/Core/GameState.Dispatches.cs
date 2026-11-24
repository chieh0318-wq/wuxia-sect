using System;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    [Serializable]
    public sealed class FieldDispatch
    {
        public string site;
        public string[] party;
        public float distance, traveled, speed;
        public bool returning;
    }

    public sealed partial class GameState
    {
        internal bool IsDispatched(string worker) => Story.fieldDispatches.Any(mission => mission.party.Contains(worker));

        internal static float MarchSpeed(string tier) => tier switch
        {
            "E" => .5f, "D" => .75f, "C" => 1f, "B" => 2f, "A" => 3f,
            "A+" => 3.5f, "S" => 4f, "SS" => 5f, "SSS" => 6f, _ => .5f
        };

        private float MarchSpeedOf(string worker)
        {
            var lines = worker == HeroId ? Protagonist.lines : Disciples.Find(member => member.id == worker)?.lines;
            return MarchSpeed(lines?.FirstOrDefault(line => line.id == "qinggong")?.rank) + TalentValue(worker, "march", 1);
        }

        internal string SendTreasureDispatch(TreasureSite site, string[] party)
        {
            var route = WorldMapData.Load().Route(StartNode, site.node);
            if (route == null) return "沒有可通的道路。";
            if (Story.fieldDispatches.Any(mission => mission.site == site.id)) return "已有隊伍前往此處。";
            if (party.Length is < 1 or > 3 || party.Any(worker => AssignmentSystem.Block(this, worker) != null)
                || ActionPoints < 1 || !AssignmentSystem.Commit(this, party)) return "所選隊伍無法出勤。";
            TrySpendAp(1, "treasure:" + site.id);
            var mission = new FieldDispatch
            {
                site = site.id, party = party, distance = route.Nodes.Count - 1,
                speed = party.Average(MarchSpeedOf)
            };
            Story.fieldDispatches = Story.fieldDispatches.Append(mission).ToArray();
            return $"已派隊前往{site.name}，預計 {TravelMonths(mission.distance, mission.speed)} 個月抵達。";
        }

        private static int TravelMonths(float distance, float speed) => Mathf.CeilToInt(distance / speed);
        internal int DispatchReturnMonth(FieldDispatch mission) => MonthStamp + TravelMonths(
            mission.returning ? mission.traveled : mission.distance, mission.speed) +
            (mission.returning ? 0 : TravelMonths(mission.distance - mission.traveled, mission.speed));

        internal void RecallDispatch(FieldDispatch mission)
        {
            mission.returning = true;
            if (mission.traveled <= 0) CompleteDispatch(mission);
        }

        private void CompleteDispatch(FieldDispatch mission) =>
            Story.fieldDispatches = Story.fieldDispatches.Where(entry => entry != mission).ToArray();

        internal bool DispatchArrived(FieldDispatch mission) => !mission.returning && mission.traveled >= mission.distance;

        internal bool ForceDispatchAmbush { get; set; }

        private void AdvanceDispatches(MonthlyReport report)
        {
            foreach (var mission in Story.fieldDispatches)
            {
                if (DispatchArrived(mission)) continue;
                string name = RealmContent.Treasure(mission.site)?.name ?? mission.site;
                if (ForceDispatchAmbush || UnityEngine.Random.value < .12f)
                {
                    ForceDispatchAmbush = false;
                    InjureWorker(mission.party[UnityEngine.Random.Range(0, mission.party.Length)]);
                    report.Lines.Add($"前往{name}的外派隊伍遇伏，一人負傷。");
                }
                if (mission.returning)
                {
                    mission.traveled = Mathf.Max(0, mission.traveled - mission.speed);
                    if (mission.traveled > 0) continue;
                    CompleteDispatch(mission);
                    report.Lines.Add($"{name}外派隊伍已返山。");
                }
                else
                {
                    mission.traveled = Mathf.Min(mission.distance, mission.traveled + mission.speed);
                    if (DispatchArrived(mission))
                        report.Lines.Add($"外派隊伍已抵達{name}，可在人事的外派名單入境。");
                }
            }
        }
    }
}
