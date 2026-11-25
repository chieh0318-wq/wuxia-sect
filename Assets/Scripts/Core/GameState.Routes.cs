using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    /// <summary>
    /// v1126b 決定 3: thresholds of the four long-term routes (外交結盟、武力征服、富甲天下、天下第一), read from
    /// <c>victory_routes.json</c>. Defaults match the integrated build calibration so a missing file still plays.
    /// </summary>
    [Serializable] internal sealed class RouteRules
    {
        [Serializable] internal sealed class Diplomacy
        {
            public int loyal = 12, subjects = 6, reputation = 380, holdMonths = 6;
        }

        [Serializable] internal sealed class Conquest
        {
            public int held = 20, reputation = 450, holdMonths = 6;
        }

        [Serializable] internal sealed class Wealth
        {
            /// <summary>庫銀、商路條數（每條連續通商滿 routeMonths 月）、近 12 月商路收入、聲望。</summary>
            public int silver = 8000, routes = 16, routeMonths = 12, tradeYear = 1800, reputation = 350;
        }

        [Serializable] internal sealed class Champion
        {
            /// <summary>主修階位、擊敗成名高手人數、其中 SS 級宗師人數、聲望。</summary>
            public string rank = "SS";
            public int masters = 9, grandmasters = 3, reputation = 600;
            /// <summary>A master accepts a challenge when our 聲望 ≥ their 聲望 × this, and the hero is at most one tier below.</summary>
            public float reputationRatio = 1f;
            public int tierGap = 1, apCost = 2, silverCost = 20, retryMonths = 3;
            public string minimumLeaderRank = "A+";
        }

        public Diplomacy diplomacy = new();
        public Conquest conquest = new();
        public Wealth wealth = new();
        public Champion champion = new();

        private static RouteRules data;
        internal static RouteRules Data
        {
            get
            {
                if (data != null) return data;
                try { data = M2Data.Load<RouteRules>("victory_routes"); }
                catch (Exception error) { Debug.LogWarning($"[Routes] victory_routes.json unreadable: {error.Message}"); }
                return data ??= new RouteRules();
            }
        }
    }

    /// <summary>One route's live state for the 任務 panel: a progress line, whether it is met and whether it was declared.</summary>
    internal readonly struct RouteState
    {
        public readonly string Id, Progress;
        public readonly bool Met, Declared;
        public RouteState(string id, string progress, bool met, bool declared)
        {
            Id = id; Progress = progress; Met = met; Declared = declared;
        }
    }

    public sealed partial class GameState
    {
        internal static readonly string[] RouteIds = { "diplomacy", "conquest", "wealth", "champion" };

        private int RouteFlag(string id) => ValueOf(Story.flags ?? Array.Empty<StoryValue>(), id);
        private void SetRouteFlag(string id, int value) => SetValue(ref Story.flags, id, value, false);

        // ── 富甲天下 ──

        /// <summary>Monthly silver a 通商 tie brings (same formula as the month-end diplomacy income).</summary>
        internal int TradeIncomeOf(MapFaction faction) =>
            Mathf.Max(1, (faction.type == "merchant" ? BalanceV1126c.Trade.merchantSilverPerMonth : BalanceV1126c.Trade.tongshangSilverPerMonth) +
                (Tie(faction.id)?.tradeIncome ?? 0));

        /// <summary>Consecutive months each 通商 tie has run (reset when it lapses).</summary>
        internal int TradeMonths(string factionId) => RouteFlag("route_trade_" + factionId);

        internal int SettledTradeRoutes => WorldMapData.Load().Factions.Count(faction =>
            HasTie(faction.id, Diplomacy.Trade) && TradeMonths(faction.id) >= RouteRules.Data.wealth.routeMonths);

        internal int TradeRoutes => WorldMapData.Load().Factions.Count(faction => HasTie(faction.id, Diplomacy.Trade));

        /// <summary>Trade income over the last twelve month-ends (a ring of twelve flags).</summary>
        internal int TradeYear => Enumerable.Range(0, 12).Sum(slot => RouteFlag("route_tradeinc_" + slot));

        // ── 天下第一 ──

        internal static bool IsMaster(MapFaction faction) => faction != null && !faction.special && !faction.Outlaw &&
            !string.IsNullOrWhiteSpace(faction.leader) &&
            RankSystem.Order(faction.leaderRank) >= RankSystem.Order(RouteRules.Data.champion.minimumLeaderRank);

        internal static IEnumerable<MapFaction> Masters => WorldMapData.Load().Factions.Where(IsMaster);

        internal bool MasterDefeated(string factionId) => RouteFlag("master_" + factionId) > 0;

        internal int MastersDefeated => Masters.Count(faction => MasterDefeated(faction.id));

        internal int GrandmastersDefeated => Masters.Count(faction => MasterDefeated(faction.id) &&
            RankSystem.Order(faction.leaderRank) >= RankSystem.Order("SS"));

        internal SkillLine HeroMajorLine => RankSystem.Major(Protagonist.lines, Protagonist.major) ??
            RankSystem.BestCombat(Protagonist.lines ?? Array.Empty<SkillLine>());

        internal int HeroMajorTier => RankSystem.Index(HeroMajorLine?.rank);

        /// <summary>Why a master would not accept the hero's challenge now (null when the duel can be fought).</summary>
        internal string MasterBlock(MapFaction faction)
        {
            var rules = RouteRules.Data.champion;
            if (!IsMaster(faction)) return "此人並非成名高手";
            if (MasterDefeated(faction.id)) return "已勝過此人";
            if (!IsFactionKnown(faction.id)) return "尚未探明此派";
            int retry = RouteFlag("master_retry_" + faction.id);
            if (retry > MonthStamp) return $"上回落敗，須待 {retry - MonthStamp} 個月再訪";
            int need = Mathf.CeilToInt(faction.reputation * rules.reputationRatio);
            if (Reputation < need) return $"聲望須達 {need}，對方才肯動手";
            int tier = RankSystem.Index(faction.leaderRank) - rules.tierGap;
            if (HeroMajorTier < tier) return $"主修須達 {RankSystem.At(tier)}";
            if (Protagonist.injury >= 3) return "主角傷勢未癒";
            if (ActionPoints < rules.apCost) return $"需要 {rules.apCost} 點行動力";
            if (Silver < rules.silverCost) return $"盤纏需銀兩 {rules.silverCost}";
            return null;
        }

        /// <summary>Duel difficulty: harder the further the master's tier is above the hero's 主修.</summary>
        internal float MasterDifficulty(MapFaction faction) =>
            Mathf.Clamp(1f + .2f * (RankSystem.Index(faction.leaderRank) - HeroMajorTier), .8f, 1.6f);

        /// <summary>Pays the visit (AP and travelling money); false when blocked.</summary>
        internal bool BeginMasterChallenge(MapFaction faction)
        {
            var rules = RouteRules.Data.champion;
            if (MasterBlock(faction) != null || !TrySpendAp(rules.apCost, "master:" + faction.id)) return false;
            Silver -= rules.silverCost;
            return true;
        }

        /// <summary>Records a duel against a master: a win counts toward 天下第一, a loss means a wound and a wait.</summary>
        internal string ResolveMasterChallenge(MapFaction faction, bool won)
        {
            if (faction == null) return "";
            if (won)
            {
                bool first = !MasterDefeated(faction.id);
                SetRouteFlag("master_" + faction.id, 1);
                int fame = first ? Mathf.Max(2, faction.reputation / 10) : 0;
                int before = Reputation;
                ApplyEffects(new[]
                {
                    new GameEffect { key = "reputation", value = fame },
                    new GameEffect { key = "relation", @ref = faction.id, value = -3 }
                });
                fame = Reputation - before; // the gain actually applied (reputation.gainScalePct, 魅力)
                return $"勝過{faction.leader}。{(fame > 0 ? $"聲望 +{fame}。" : "")}成名高手 {MastersDefeated}／{RouteRules.Data.champion.masters}";
            }
            Injure(1);
            SetRouteFlag("master_retry_" + faction.id, MonthStamp + RouteRules.Data.champion.retryMonths);
            ApplyEffects(new[] { new GameEffect { key = "reputation", value = -1 } });
            return $"敗於{faction.leader}之手，主角負傷，聲望 −1。{RouteRules.Data.champion.retryMonths} 個月後可再登門。";
        }

        /// <summary>A commander duel won by the hero in a 門派戰役 also counts as beating that faction's master.</summary>
        internal void RecordCommanderDuel(MapFaction faction, string duelist, bool won)
        {
            if (won && duelist == HeroId && IsMaster(faction) && !MasterDefeated(faction.id))
                SetRouteFlag("master_" + faction.id, 1);
        }

        // ── month end ──

        /// <summary>Called from <see cref="EndMonth"/>: trade route ages, the twelve-month trade ring and the hold counters.</summary>
        internal void RoutesMonth(MonthlyReport report)
        {
            int income = 0;
            foreach (var faction in WorldMapData.Load().Factions)
            {
                string key = "route_trade_" + faction.id;
                if (HasTie(faction.id, Diplomacy.Trade))
                {
                    SetValue(ref Story.flags, key, 1, true);
                    income += MonthlyTradeIncome(faction);
                }
                else if (RouteFlag(key) != 0) SetRouteFlag(key, 0);
            }
            SetRouteFlag("route_tradeinc_" + MonthStamp % 12, income);

            var victory = Victory;
            var rules = RouteRules.Data;
            Hold("route_hold_diplomacy", victory.Diplomatic);
            Hold("route_hold_conquest", victory.Conquest);

            foreach (var route in Routes())
            {
                if (!route.Met || route.Declared || RouteFlag("route_told_" + route.Id) > 0) continue;
                SetRouteFlag("route_told_" + route.Id, 1);
                report?.Lines.Add($"破關之路：{RouteName(route.Id)}已達成，可在任務欄宣告。");
            }

            void Hold(string key, bool met)
            {
                if (met) SetValue(ref Story.flags, key, 1, true);
                else if (RouteFlag(key) != 0) SetRouteFlag(key, 0);
            }
        }

        internal static string RouteName(string id) => id switch
        {
            "diplomacy" => "外交結盟",
            "conquest" => "武力征服",
            "wealth" => "富甲天下",
            "champion" => "天下第一",
            _ => id
        };

        internal bool RouteDeclared(string id) => RouteFlag("route_done_" + id) > 0 ||
            (id == "diplomacy" && RouteFlag("diplo_victory") > 0);

        /// <summary>Declares a route's ending once it is met (true the first time). The game carries on afterwards.</summary>
        internal bool DeclareRoute(string id)
        {
            var route = Route(id);
            if (!route.Met || route.Declared) return false;
            SetRouteFlag("route_done_" + id, MonthStamp);
            if (id == "diplomacy") SetValue(ref Story.flags, "diplo_victory", 1, false);
            return true;
        }

        internal IEnumerable<RouteState> Routes() => RouteIds.Select(Route);

        internal RouteState Route(string id)
        {
            var rules = RouteRules.Data;
            var victory = Victory;
            switch (id)
            {
                case "diplomacy":
                {
                    var r = rules.diplomacy;
                    int hold = RouteFlag("route_hold_diplomacy");
                    bool met = victory.Diplomatic && hold >= r.holdMonths;
                    return new RouteState(id, $"結盟或歸附 {victory.Loyal}／{r.loyal}・歸附 {victory.Subjects}／{r.subjects}" +
                        $"・聲望 {Reputation}／{r.reputation}" + (victory.Diplomatic ? $"・盟約穩固 {Mathf.Min(hold, r.holdMonths)}／{r.holdMonths} 月" : ""),
                        met, RouteDeclared(id));
                }
                case "conquest":
                {
                    var r = rules.conquest;
                    int hold = RouteFlag("route_hold_conquest");
                    bool met = victory.Conquest && hold >= r.holdMonths;
                    return new RouteState(id, $"直轄據點 {victory.Held}／{r.held}・聲望 {Reputation}／{r.reputation}" +
                        (victory.Conquest ? $"・守穩 {Mathf.Min(hold, r.holdMonths)}／{r.holdMonths} 月" : ""), met, RouteDeclared(id));
                }
                case "wealth":
                {
                    var r = rules.wealth;
                    int settled = SettledTradeRoutes, year = TradeYear;
                    bool met = Silver >= r.silver && settled >= r.routes && year >= r.tradeYear && Reputation >= r.reputation;
                    return new RouteState(id, $"庫銀 {Silver:N0}／{r.silver:N0}・商路 {settled}／{r.routes}（滿 {r.routeMonths} 月）" +
                        $"・年商路收入 {year:N0}／{r.tradeYear:N0}・聲望 {Reputation}／{r.reputation}", met, RouteDeclared(id));
                }
                case "champion":
                {
                    var r = rules.champion;
                    int masters = MastersDefeated, grand = GrandmastersDefeated;
                    string rank = HeroMajorLine?.rank ?? "—";
                    bool met = HeroMajorTier >= RankSystem.Index(r.rank) && masters >= r.masters && grand >= r.grandmasters &&
                        Reputation >= r.reputation;
                    return new RouteState(id, $"主修 {rank}／{r.rank}・勝成名高手 {masters}／{r.masters}（宗師 {grand}／{r.grandmasters}）" +
                        $"・聲望 {Reputation}／{r.reputation}", met, RouteDeclared(id));
                }
                default:
                    return new RouteState(id, "", false, false);
            }
        }
    }
}
