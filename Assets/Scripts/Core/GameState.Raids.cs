using System;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    [Serializable]
    public sealed class SectRaid
    {
        public string faction, target, counter;
        public int due, troops, preparation, aid;
        public bool detected;
        public string[] guards;
    }

    internal enum RaidOrder { Scout, Garrison, Prepare, Reinforce, Rumor, Intercept, Bribe }

    public sealed partial class GameState
    {
        internal int WarWorks(string id) => Buildings.Count(building => building?.id == id);
        internal string WarNode(string id) => WorldMapData.Load().NodeLabel(id);
        internal bool RaidReady => Story.sectRaid != null && Story.sectRaid.due <= MonthStamp;
        internal int Fortification(SectRaid raid) => raid.target == StartNode
            ? 4 * WarWorks("wall") + 3 * WarWorks("watchtower") : 3;

        internal float RaidChance(RaidOrder order, string id)
        {
            string attr = order switch
            {
                RaidOrder.Garrison or RaidOrder.Prepare => "con",
                RaidOrder.Intercept or RaidOrder.Scout => "agi",
                RaidOrder.Reinforce or RaidOrder.Bribe => "cha",
                _ => "wit"
            };
            return order == RaidOrder.Garrison ? 1 :
                Mathf.Clamp(.66f + .025f * (AssignmentSystem.Attribute(this, id, attr) - 10)
                    + .055f * AssignmentSystem.Grade(this, id) + .04f * WarWorks("spy_lodge"), .3f, .96f);
        }

        internal string RaidBlock(RaidOrder order)
        {
            var raid = Story.sectRaid;
            if (raid == null) return "目前沒有待查的敵情。";
            if (RaidReady) return "敵軍已抵達，請迎戰。";
            if (order == RaidOrder.Scout && raid.detected) return "已探明敵情。";
            if (order == RaidOrder.Garrison && raid.guards?.Length > 0) return "已有守將領命。";
            if (order != RaidOrder.Garrison && order != RaidOrder.Scout && !raid.detected)
                return "先弄清敵軍動向。";
            if (order == RaidOrder.Prepare && raid.preparation > 0) return "已完成佈防。";
            if (order == RaidOrder.Reinforce && raid.aid > 0) return "援軍已答應出兵。";
            if (order == RaidOrder.Reinforce && !CanReinforce(raid)) return "沒有可調兵的分舵或友軍。";
            if (order == RaidOrder.Bribe && Silver < 20) return "收買內線需銀兩 20。";
            if (order is RaidOrder.Rumor or RaidOrder.Intercept or RaidOrder.Bribe && raid.counter == "disrupted")
                return "敵方斥候已失去聯絡。";
            return null;
        }

        private bool CanReinforce(SectRaid raid) => (Story.heldNodes ?? Array.Empty<string>()).Any(id => id != raid.target)
            || WorldMapData.Load().Factions.Any(f => HasTie(f.id, Diplomacy.Alliance) || Attitude(f) >= 35);

        internal string RunRaidOrder(RaidOrder order, string[] party)
        {
            string block = RaidBlock(order);
            if (block != null) return block;
            if (!AssignmentSystem.Commit(this, party)) return "所選人手無法出勤。";
            var raid = Story.sectRaid;
            if (order == RaidOrder.Bribe) Silver -= 20;
            if (UnityEngine.Random.value > RaidChance(order, party[0])) return "此行失手，未能取得成果。";
            foreach (string id in party) AssignmentSystem.Credit(this, id, 1);
            switch (order)
            {
                case RaidOrder.Scout: raid.detected = true; return "細作已查清來軍動向。";
                case RaidOrder.Garrison: raid.guards = party; return "守將已領命，將在敵軍抵達時參戰。";
                case RaidOrder.Prepare: raid.preparation = 5 + Fortification(raid); return "已據險佈防，迎敵時獲得防護。";
                case RaidOrder.Reinforce: raid.aid = 350; return "已獲援兵 350。";
                default:
                    raid.counter = "disrupted";
                    int reduction = order == RaidOrder.Rumor ? 25 : order == RaidOrder.Intercept ? 20 : 30;
                    raid.troops -= raid.troops * reduction / 100;
                    return "敵方情報失靈，來軍兵力已削弱，敵方奇策亦遭破壞。";
            }
        }

        internal void RaidMonth(MonthlyReport report)
        {
            var raid = Story.sectRaid;
            if (raid != null)
            {
                if (raid.due > MonthStamp) return;
                if (raid.due < MonthStamp)
                {
                    report.Lines.Add(FinishRaid(false, "守軍未及迎戰。"));
                    return;
                }
                if (raid.counter != "disrupted")
                {
                    raid.counter = new[] { "謠言惑眾", "截殺哨探", "收買內應" }[UnityEngine.Random.Range(0, 3)];
                    switch (raid.counter)
                    {
                        case "謠言惑眾": raid.aid /= 2; break;
                        case "截殺哨探": raid.detected = false; raid.preparation = 0; break;
                        default: raid.preparation /= 2; break;
                    }
                    if (raid.detected) report.Lines.Add($"敵軍以「{raid.counter}」干擾守備。");
                }
                report.Lines.Add($"{WarNode(raid.target)}告急！請從「征伐 → 山門軍情」迎戰。");
                return;
            }
            bool exposed = Story.fieldDispatches.Length > 0 && UnityEngine.Random.value < .25f;
            if (Story.raidResolvedMonth + 3 > MonthStamp || (!exposed && Month % 3 != 0)) return;
            var map = WorldMapData.Load();
            var opponents = map.Factions.Where(f => Attitude(f) <= -25
                && f.id != Story.patronFaction && !HasTie(f.id, Diplomacy.Pact | Diplomacy.Alliance | Diplomacy.Merged)).ToArray();
            if (opponents.Length == 0) return;
            var attacker = opponents[UnityEngine.Random.Range(0, opponents.Length)];
            var holdings = (Story.heldNodes ?? Array.Empty<string>()).Where(map.Nodes.ContainsKey).Append(StartNode).Distinct().ToArray();
            Story.sectRaid = new SectRaid
            {
                faction = attacker.id, due = MonthStamp + 1,
                target = exposed ? StartNode : holdings[UnityEngine.Random.Range(0, holdings.Length)],
                troops = Mathf.Clamp(attacker.disciples / 3, 80, 1800),
                detected = WarWorks("spy_lodge") + WarWorks("watchtower") > 0
            };
            report.Lines.Add(Story.sectRaid.detected ? "哨探截得敵軍動向，詳見山門軍情。" : "近日山道有可疑人馬，可遣細作查探。");
        }

        internal string FinishRaid(bool won, string prefix = "")
        {
            var raid = Story.sectRaid;
            if (raid == null) return "";
            Story.sectRaid = null;
            Story.raidResolvedMonth = MonthStamp;
            string location = WarNode(raid.target);
            if (won)
            {
                Apply(new Yield { reputation = 4, silver = 15 });
                return $"{location}守住了。銀兩＋15、聲望＋4。";
            }
            Silver = Mathf.Max(0, Silver - 35);
            Food = Mathf.Max(0, Food - 25);
            Reputation -= 5;
            if (raid.target != StartNode)
                Story.heldNodes = (Story.heldNodes ?? Array.Empty<string>()).Where(id => id != raid.target).ToArray();
            return $"{prefix}{location}{(raid.target == StartNode ? "遭洗劫" : "失守")}。銀兩最多損失35、糧食最多損失25、聲望 −5。";
        }
    }
}
