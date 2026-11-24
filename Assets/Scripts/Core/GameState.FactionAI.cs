using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    [Serializable] public sealed class FactionRealm
    {
        public string id, patron;
        public int silver, food, troops, defenses, martial, nextOffer;
        public string[] holdings;
    }

    [Serializable] public sealed class FactionConflict
    {
        public string attacker, defender, target;
        public int declared, due, troops;
    }

    [Serializable] public sealed class FactionAccord
    {
        public string first, second, kind;
        public int until;
    }

    [Serializable] public sealed class FactionEnvoy
    {
        public string faction, kind, target;
        public int due, silver, food;
    }

    [Serializable] public sealed class FactionCampaign
    {
        public int difficulty = 1, annexations, raids;
        public FactionRealm[] realms;
        public FactionConflict[] wars = Array.Empty<FactionConflict>();
        public FactionAccord[] accords = Array.Empty<FactionAccord>();
        public FactionEnvoy envoy;
        public SectRaid[] invasions = Array.Empty<SectRaid>();
    }

    [Serializable] internal sealed class FactionPolicy
    {
        public int acceptance = 60, grace = 3, occupationMonths = 8;
        public bool annex = true;
    }

    public sealed partial class GameState
    {
        internal FactionCampaign FactionWorld => Story.factionCampaign ??= new FactionCampaign();
        private static WorldMapData FactionMap => WorldMapData.Load();
        internal string FactionDifficultyLabel => FactionWorld.difficulty switch
        {
            0 => "寬鬆", 2 => "險峻", _ => "一般"
        };

        private FactionPolicy Policy => Diplomacy.Data.factionPolicy ?? new FactionPolicy();

        private void SeedFactionWorld()
        {
            if (FactionWorld.realms != null) return;
            FactionWorld.realms = FactionMap.Factions.Where(f => FactionMap.Nodes.ContainsKey(f.seat))
                .Select(f => new FactionRealm
                {
                    id = f.id, troops = Mathf.Max(60, f.disciples), silver = Mathf.Max(50, f.money),
                    food = Mathf.Max(100, f.disciples / 2), defenses = 15,
                    martial = Mathf.Max(0, RankSystem.Index(f.leaderRank)) * 8,
                    holdings = (f.territory ?? Array.Empty<string>()).Prepend(f.seat)
                        .Where(FactionMap.Nodes.ContainsKey).Distinct().ToArray()
                }).ToArray();
        }

        internal void FactionMonth(MonthlyReport report)
        {
            SeedFactionWorld();
            var world = FactionWorld;
            var news = new List<string>();
            int difficulty = Mathf.Clamp(world.difficulty, 0, 2);
            int age = MonthStamp - 13;
            if (Story.sectRaid == null && world.invasions.Length > 0)
            {
                var raid = world.invasions.OrderBy(r => r.due).First();
                world.invasions = world.invasions.Where(r => r != raid).ToArray();
                raid.due = Math.Max(raid.due, MonthStamp + 1);
                Story.sectRaid = raid;
                news.Add(raid.detected ? $"【已證實】{NameOf(raid.faction)}來軍接近{WarNode(raid.target)}。"
                    : "【傳聞】山下另有一支人馬逼近。");
            }
            world.accords = world.accords.Where(accord => accord.until > MonthStamp).ToArray();
            if (world.envoy != null && world.envoy.due < MonthStamp)
            {
                string expired = AnswerFactionEnvoy("reject");
                if (expired.Contains("已出兵")) news.Add("【已證實】" + expired);
            }
            var busy = new HashSet<string>();
            foreach (var realm in world.realms.Where(Active))
            {
                int seats = realm.holdings.Length;
                int marching = world.wars.Where(war => war.attacker == realm.id).Sum(war => war.troops);
                realm.food = Mathf.Clamp(realm.food + seats * 32 - (realm.troops + marching) / 55, 0, 9000);
                realm.silver = Mathf.Clamp(realm.silver + seats * 20 - realm.troops / 145 +
                    6 * world.accords.Count(a => a.kind == "trade" && Involves(a, realm.id)), 0, 9000);
            }
            foreach (var war in world.wars.ToArray())
            {
                var attacker = Realm(war.attacker);
                var defender = Realm(war.defender);
                if (!Active(attacker) || !Active(defender) || !defender.holdings.Contains(war.target) || HoldsNode(war.target) ||
                    Protected(attacker.id, defender.id))
                {
                    if (Active(attacker)) attacker.troops += war.troops;
                    world.wars = world.wars.Where(entry => entry != war).ToArray();
                    continue;
                }
                if (war.troops == 0)
                {
                    if (war.declared >= MonthStamp) continue;
                    war.troops = MarchingStrength(attacker);
                    attacker.troops -= war.troops;
                    attacker.food = Mathf.Max(0, attacker.food - 24);
                    continue;
                }
                if (war.due > MonthStamp) continue;
                busy.Add(attacker.id);
                busy.Add(defender.id);
                float strength = war.troops + attacker.martial;
                float defense = Defense(defender);
                bool won = strength > defense * UnityEngine.Random.Range(.85f, 1.15f);
                attacker.troops += Mathf.RoundToInt(war.troops * (won ? .83f : .63f));
                defender.troops = Mathf.Max(20, Mathf.RoundToInt(defender.troops * (won ? .75f : .92f)));
                if (won && Policy.annex && (defender.holdings.Length > 1 || age >= Policy.occupationMonths))
                {
                    defender.holdings = defender.holdings.Where(node => node != war.target).ToArray();
                    attacker.holdings = attacker.holdings.Append(war.target).Distinct().ToArray();
                    attacker.troops = Mathf.Max(20, attacker.troops - 35);
                    if (!Active(defender))
                    {
                        defender.patron = attacker.id;
                        world.annexations++;
                        world.accords = world.accords.Where(a => !Involves(a, defender.id)).ToArray();
                    }
                    news.Add(Bulletin(attacker.id, $"{WarNode(war.target)}易主，由{NameOf(attacker.id)}駐守。"));
                }
                else
                    news.Add(Bulletin(defender.id, $"{NameOf(defender.id)}守住{WarNode(war.target)}。"));
                MakeAccord(attacker.id, defender.id, "peace", 7);
                world.wars = world.wars.Where(entry => entry != war).ToArray();
            }

            foreach (var realm in world.realms.Where(Active))
            {
                if (busy.Contains(realm.id) || world.wars.Any(war => war.attacker == realm.id)) continue;
                var faction = FindFaction(realm.id);
                string primary = faction.aiPrimary, secondary = faction.aiSecondary;
                if (age > Policy.grace && TryPlayerPlan(realm, faction, difficulty, news)) continue;
                bool expansion = primary == "expansion" || secondary == "expansion";
                if (age > Policy.grace && !faction.Court && realm.food >= 45 &&
                    (expansion || UnityEngine.Random.value < .10f) && UnityEngine.Random.value < .24f)
                {
                    var target = world.realms.Where(other => other != realm && Active(other) && !FindFaction(other.id).Court &&
                            !Protected(other.id, realm.id) && !world.wars.Any(war => war.defender == other.id ||
                                war.attacker == other.id && war.defender == realm.id))
                        .Select(other => (realm: other, days: FactionMap.Route(faction.seat, FindFaction(other.id).seat)?.Days ?? 999,
                            node: other.holdings.FirstOrDefault(node => !realm.holdings.Contains(node) && !HoldsNode(node))))
                        .Where(entry => entry.node != null && entry.days <= (expansion ? 45 : 28))
                        .Where(entry => (MarchingStrength(realm) + realm.martial - entry.days * 2) / Defense(entry.realm) >=
                            Threshold(primary, entry.days, difficulty))
                        .OrderBy(entry => entry.days).FirstOrDefault();
                    if (target.realm != null)
                    {
                        world.wars = world.wars.Append(new FactionConflict
                        {
                            attacker = realm.id, defender = target.realm.id, target = target.node, declared = MonthStamp,
                            due = MonthStamp + 2 + target.days / 30
                        }).ToArray();
                        news.Add(Bulletin(realm.id, $"{faction.Label}向{NameOf(target.realm.id)}宣戰。"));
                        continue;
                    }
                }
                if (TryFactionAccord(realm, primary, news)) continue;
                if (realm.food < 35 || realm.silver < 18) continue;
                if ((primary == "defense" || (secondary == "defense" && realm.defenses < 30)) && realm.defenses < 90)
                    realm.defenses += 7;
                else if (primary == "prestige" && realm.martial < 150)
                    realm.martial += 4;
                else if (realm.troops < Mathf.Max(240, faction.disciples * 2))
                    realm.troops += Mathf.RoundToInt((18 + realm.holdings.Length * 4) * (.85f + .15f * difficulty));
                else continue;
                realm.silver -= 18;
                realm.food -= 8;
                if (MonthStamp % 6 == 0 && realm.troops > faction.disciples + 100)
                    news.Add(Bulletin(realm.id, $"{faction.Label}整飭軍伍，兵力漸盛。"));
            }
            report.Lines.Add("江湖大事");
            report.Lines.AddRange(news.Count == 0 ? new[] { "未聞要事。" } : news.ToArray());
            var rising = world.realms.Where(r => Active(r) && !FindFaction(r.id).Court)
                .OrderByDescending(r => r.holdings.Length).ThenByDescending(r => r.troops).FirstOrDefault();
            if (rising != null && rising.holdings.Length >= 3 && MonthStamp % 3 == 0)
                report.Lines.Add($"軍師觀勢　{NameOf(rising.id)}據地 {rising.holdings.Length} 處，宜留心其動向。");
        }

        private bool TryFactionAccord(FactionRealm realm, string primary, List<string> news)
        {
            if (realm.nextOffer > MonthStamp || UnityEngine.Random.value > .10f) return false;
            var faction = FindFaction(realm.id);
            var partner = FactionWorld.realms.Where(r => r != realm && Active(r) && !Protected(r.id, realm.id)
                && !FactionWorld.wars.Any(w => w.attacker == r.id && w.defender == realm.id || w.defender == r.id && w.attacker == realm.id))
                .Select(r => (realm: r, days: FactionMap.Route(faction.seat, FindFaction(r.id).seat)?.Days ?? 999))
                .Where(entry => entry.days <= 28).OrderBy(entry => entry.days).FirstOrDefault();
            if (partner.realm == null) return false;
            bool commonThreat = FactionWorld.realms.Any(r => r != realm && r != partner.realm && Active(r)
                && r.troops > 2 * Math.Max(realm.troops, partner.realm.troops) &&
                (FactionMap.Route(faction.seat, FindFaction(r.id).seat)?.Days ?? 999) <= 35);
            string kind = primary == "trade" ? "trade" : "aid";
            int score = 52 + (commonThreat ? 18 : 0) + (kind == "trade" ? 15 : 0) - partner.days / 4;
            realm.nextOffer = MonthStamp + 2;
            if (score < Policy.acceptance) return true;
            MakeAccord(realm.id, partner.realm.id, kind, 8);
            news.Add(Bulletin(realm.id, $"{faction.Label}與{NameOf(partner.realm.id)}{(kind == "trade" ? "互通商旅" : "立約互援")}。"));
            return true;
        }

        private bool TryPlayerPlan(FactionRealm realm, MapFaction faction, int difficulty, List<string> news)
        {
            string target = StartNode;
            int days = FactionMap.Route(faction.seat, target)?.Days ?? 999;
            if (Story.fieldDispatches.Length == 0)
            {
                foreach (string holding in Story.heldNodes)
                {
                    int distance = FactionMap.Route(faction.seat, holding)?.Days ?? 999;
                    if (distance >= days) continue;
                    target = holding;
                    days = distance;
                }
            }
            if (days > 45 || realm.nextOffer > MonthStamp || realm.id == Story.patronFaction ||
                HasTie(realm.id, Diplomacy.Pact | Diplomacy.Alliance | Diplomacy.Merged | Diplomacy.Vassal) ||
                Protected(HeroId, realm.id)) return false;
            bool hostile = Attitude(faction) <= -25;
            float defense = 150 + SectPower * 2 + 50 * (WarWorks("wall") + WarWorks("watchtower"))
                - Story.fieldDispatches.Length * 70;
            bool freeFront = FactionWorld.invasions.Length + (Story.sectRaid == null ? 0 : 1) < difficulty + 1 &&
                Story.sectRaid?.faction != realm.id && !FactionWorld.invasions.Any(r => r.faction == realm.id);
            if (hostile && freeFront && MonthStamp >= Story.raidResolvedMonth + 4 &&
                FactionWorld.envoy == null && realm.food >= 40 && UnityEngine.Random.value < .30f &&
                MarchingStrength(realm) / Mathf.Max(180, defense) >= Threshold(faction.aiPrimary, days, difficulty))
            {
                FactionWorld.envoy = new FactionEnvoy { faction = faction.id, kind = "ultimatum",
                    target = target, due = MonthStamp + 1, silver = 35, food = 25 };
                news.Add($"【已證實】{faction.Label}送來最後通牒。");
                return true;
            }
            if (FactionWorld.envoy != null || UnityEngine.Random.value >= .025f ||
                faction.type == "court" && (Patron != null || Attitude(faction) < 0)) return false;
            string kind = faction.type == "court" ? "submission" :
                faction.type == "foreign" || faction.aiPrimary == "trade" ? "trade" : hostile ? "peace" : "aid";
            if (kind == "trade" && HasTie(faction.id, Diplomacy.Trade)) return false;
            FactionWorld.envoy = new FactionEnvoy { faction = faction.id, kind = kind, due = MonthStamp + 2,
                silver = kind == "trade" && faction.type != "foreign" ? 12 : 0,
                food = faction.type == "foreign" ? 20 : 0 };
            news.Add($"【已證實】{faction.Label}遣使議{OfferLabel(kind)}。");
            return true;
        }

        internal static string OfferLabel(string kind) => kind switch
        {
            "aid" => "互援", "trade" => "通商", "peace" => "求和", "submission" => "奉表臣服", _ => "最後通牒"
        };

        internal string AnswerFactionEnvoy(string answer)
        {
            var offer = FactionWorld.envoy;
            if (offer == null) return "使者已離去。";
            var faction = FindFaction(offer.faction);
            var realm = Realm(offer.faction);
            int silver = answer == "counter" ? (offer.silver + 1) / 2 : offer.silver;
            int food = answer == "counter" ? (offer.food + 1) / 2 : offer.food;
            int score = 66 + Attitude(faction) / 4 - (answer == "counter" ? 8 : 0);
            bool agreed = answer == "accept" || answer == "counter" && score >= Policy.acceptance;
            if (agreed && (Silver < silver || Food < food)) return "庫中錢糧不足。";
            FactionWorld.envoy = null;
            if (realm != null) realm.nextOffer = MonthStamp + 2;
            if (agreed)
            {
                Silver -= silver;
                Food -= food;
                if (offer.kind == "trade")
                {
                    SetTie(faction, Diplomacy.Trade, true);
                    Tie(faction.id).tradeIncome = faction.type == "foreign" ? 12 : 8;
                }
                else if (offer.kind == "aid")
                {
                    SetTie(faction, Diplomacy.Alliance, true);
                    Tie(faction.id).pactPower = 8;
                }
                else if (offer.kind == "submission")
                    Story.patronFaction = faction.id;
                else
                {
                    MakeAccord(HeroId, faction.id, "peace", 6);
                    ShiftAttitude(faction, 10);
                }
                return $"已與{faction.Label}議定{OfferLabel(offer.kind)}。";
            }
            if (offer.kind == "ultimatum" && Active(realm))
            {
                int difficulty = Mathf.Clamp(FactionWorld.difficulty, 0, 2);
                int troops = MarchingStrength(realm);
                realm.troops -= troops / 6;
                var invasion = new SectRaid
                {
                    faction = realm.id, target = HoldsNode(offer.target) ? offer.target : StartNode, troops = troops, due = MonthStamp + 3 - difficulty,
                    detected = WarWorks("spy_lodge") + WarWorks("watchtower") > 0
                };
                if (Story.sectRaid == null) Story.sectRaid = invasion;
                else FactionWorld.invasions = FactionWorld.invasions.Append(invasion).ToArray();
                FactionWorld.raids++;
                return $"{faction.Label}已出兵，宜遣細作探查。";
            }
            return "議約未成，使者告辭。";
        }

        internal string FactionWarning
        {
            get
            {
                var rising = FactionWorld.realms?.Where(r => Active(r) && IsFactionKnown(r.id) &&
                    !FindFaction(r.id).Court && r.holdings.Length > 2)
                    .OrderByDescending(r => r.holdings.Length).FirstOrDefault();
                return rising == null ? null : $"{NameOf(rising.id)}近來據地漸廣，宜遣人察其動向。";
            }
        }

        internal IEnumerable<MapFaction> FactionControllers(MapNode node)
        {
            if (FactionWorld.realms == null) return FactionMap.Controllers(node);
            var original = FactionMap.Controllers(node).Where(f => Realm(f.id)?.holdings.Contains(node.id) != false);
            var occupiers = FactionWorld.realms.Where(r => r.holdings.Contains(node.id))
                .Select(r => FindFaction(r.id)).Where(f => f != null && !f.Controls(node.id));
            return occupiers.Concat(original);
        }

        internal string FactionMapNotice(string node)
        {
            var raid = FactionWorld.invasions.Prepend(Story.sectRaid)
                .FirstOrDefault(entry => entry?.detected == true && entry.target == node);
            if (raid != null)
                return $"{NameOf(raid.faction)}來軍　{(raid.due - 1) % 12 + 1}月抵達";
            var war = FactionWorld.wars.FirstOrDefault(w => w.target == node && IsFactionKnown(w.attacker));
            return war == null ? null : $"{NameOf(war.attacker)}交戰中";
        }

        private bool Protected(string first, string second) =>
            FactionWorld.accords.Any(a => Involves(a, first) && Involves(a, second));
        private static bool Involves(FactionAccord accord, string id) => accord.first == id || accord.second == id;
        private void MakeAccord(string first, string second, string kind, int length) =>
            FactionWorld.accords = FactionWorld.accords.Append(new FactionAccord
                { first = first, second = second, kind = kind, until = MonthStamp + length }).ToArray();
        private FactionRealm Realm(string id) => FactionWorld.realms?.FirstOrDefault(r => r.id == id);
        private static bool Active(FactionRealm realm) => realm?.holdings?.Length > 0;
        private static string NameOf(string id) => FindFaction(id)?.Label ?? id;
        private string Bulletin(string id, string news) => (IsFactionKnown(id) ? "【已證實】" : "【傳聞】") + news;
        private static int MarchingStrength(FactionRealm realm) => Mathf.Max(1, realm.troops * 2 / 3);
        private float Defense(FactionRealm realm) => Mathf.Max(70, realm.troops * .7f + realm.martial + realm.defenses +
            FactionWorld.accords.Where(a => a.kind == "aid" && Involves(a, realm.id))
                .Sum(a => (Realm(a.first == realm.id ? a.second : a.first)?.troops ?? 0) * .13f));
        private static float Threshold(string primary, int days, int difficulty) =>
            (primary == "expansion" && days <= 18 ? 1.10f : primary == "defense" && days > 18 ? 1.50f : 1.25f)
            + (difficulty == 0 ? .15f : difficulty == 2 ? -.10f : 0);
    }
}
