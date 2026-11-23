using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    /// <summary>Saved diplomatic ties with one faction (bit set of <see cref="Diplomacy"/> flags).</summary>
    [Serializable] public sealed class DiploTie
    {
        public string id;
        public int status;
        /// <summary>Month stamp (year*12+month) when the faction became a vassal.</summary>
        public int since;
    }

    [Serializable] internal sealed class DiploAction
    {
        public string id, name, desc, success, failure, forbid, requires;
        public int ap, silver, reputation, chance, gain, fail, vassalMonths;
        public int minAttitude = -100, maxAttitude = 100;
        public float needRatio;
        public bool awedBypass;
        public string[] groups;
    }

    [Serializable] internal sealed class DiploOption
    {
        public string label, result;
        public int ap, silver;
        public GameEffect[] effects;
    }

    [Serializable] internal sealed class DiploEvent
    {
        public string id, title, text, target;
        public int weight = 1;
        public DiploOption[] options;
    }

    [Serializable] internal sealed class DiploVictory
    {
        public string[] jianghuTypes = { "sect", "clan", "merchant" };
        public int loyalNeeded = 9, subjectNeeded = 4, reputationNeeded = 60, conquestNodes = 12, conquestReputation = 60;
    }

    [Serializable] internal sealed class DiplomacyData
    {
        public DiploVictory victory = new();
        public float weakRatio = 1.2f, eventChance = .4f;
        public string[] rivals = Array.Empty<string>();
        public DiploAction[] actions = Array.Empty<DiploAction>();
        public DiploEvent[] events = Array.Empty<DiploEvent>();
    }

    /// <summary>Snapshot of both victory routes for the map strip and the diplomacy panel.</summary>
    internal readonly struct VictoryProgress
    {
        public readonly int Loyal, Subjects, Reputation, Held;
        public readonly DiploVictory Rules;
        public VictoryProgress(int loyal, int subjects, int reputation, int held, DiploVictory rules)
        {
            Loyal = loyal; Subjects = subjects; Reputation = reputation; Held = held; Rules = rules;
        }
        public bool Diplomatic => Loyal >= Rules.loyalNeeded && Subjects >= Rules.subjectNeeded && Reputation >= Rules.reputationNeeded;
        public bool Conquest => Held >= Rules.conquestNodes && Reputation >= Rules.conquestReputation;
    }

    internal static class Diplomacy
    {
        public const int Trade = 1, Pact = 2, Alliance = 4, Vassal = 8, Merged = 16, Awed = 32;
        private static DiplomacyData data;
        internal static DiplomacyData Data => data ??= M2Data.Load<DiplomacyData>("diplomacy");

        public static int Bit(string name) => name switch
        {
            "trade" => Trade, "pact" => Pact, "alliance" => Alliance, "vassal" => Vassal, "merged" => Merged, "awed" => Awed, _ => 0
        };

        /// <summary>jianghu (sect / clan / merchant), court (court, military, foreign) or bandit.</summary>
        public static string Group(MapFaction faction) =>
            faction.Outlaw ? "bandit" : Data.victory.jianghuTypes.Contains(faction.type) && !faction.special ? "jianghu" : "court";

        public static bool Jianghu(MapFaction faction) => Group(faction) == "jianghu";

        public static string GroupName(MapFaction faction) => Group(faction) switch
        {
            "jianghu" => "江湖勢力", "bandit" => "草莽山寨", _ => faction.type == "foreign" ? "外邦" : "朝廷官府"
        };

        public static int BaseAttitude(MapFaction faction) =>
            faction.stance == "friendly" ? 50 : faction.stance == "hostile" ? -50 : 0;

        public static string AttitudeLabel(int value) => value <= -60 ? "仇視" : value <= -25 ? "敵對" : value < 0 ? "冷淡" :
            value < 25 ? "中立" : value < 50 ? "友善" : value < 75 ? "親密" : "肝膽相照";

        public static Color AttitudeColor(int value) => value <= -25 ? MapUi.Red : value >= 25 ? new Color(.20f, .39f, .28f) : MapUi.Ink;

        /// <summary>Faction strength: its renown plus one point per twenty disciples.</summary>
        public static int Strength(MapFaction faction) => faction.reputation + faction.disciples / 20;

        public static IEnumerable<string> Rivals(string id)
        {
            foreach (string pair in Data.rivals ?? Array.Empty<string>())
            {
                var parts = (pair ?? "").Split('|');
                if (parts.Length != 2) continue;
                if (parts[0] == id) yield return parts[1];
                else if (parts[1] == id) yield return parts[0];
            }
        }

        public static string TieNames(int status)
        {
            var names = new List<string>();
            if ((status & Merged) != 0) names.Add("已併入");
            if ((status & Vassal) != 0) names.Add("附庸");
            if ((status & Alliance) != 0) names.Add("結義");
            if ((status & Pact) != 0) names.Add("共同防禦");
            if ((status & Trade) != 0) names.Add("通商");
            if ((status & Awed) != 0) names.Add("懾服");
            return names.Count == 0 ? "無" : string.Join("・", names);
        }

        public static string Fill(string text, MapFaction faction) => (text ?? "").Replace("{f}", faction?.Label ?? "");
    }

    /// <summary>
    /// Diplomacy on the save-backed state: attitude reuses the saved <c>relation</c> shift, ties live in
    /// <see cref="StoryProgress.ties"/>, and the month end runs AI drift, tribute, rivalry and event rolls.
    /// </summary>
    public sealed partial class GameState
    {
        internal int MonthStamp => Year * 12 + Month;

        internal static MapFaction FindFaction(string id) =>
            WorldMapData.Load().Factions.FirstOrDefault(faction => faction.id == id);

        internal int Attitude(MapFaction faction) =>
            Mathf.Clamp(Diplomacy.BaseAttitude(faction) + RelationShift(faction.id), -100, 100);

        private void ShiftAttitude(MapFaction faction, int delta)
        {
            if (faction == null || delta == 0) return;
            Story.relations ??= Array.Empty<StoryValue>();
            SetValue(ref Story.relations, faction.id, delta, true);
        }

        internal DiploTie Tie(string id, bool create = false)
        {
            Story.ties ??= Array.Empty<DiploTie>();
            var tie = Array.Find(Story.ties, entry => entry?.id == id);
            if (tie == null && create)
            {
                tie = new DiploTie { id = id };
                Story.ties = Story.ties.Append(tie).ToArray();
            }
            return tie;
        }

        internal int Ties(string id) => Tie(id)?.status ?? 0;
        internal bool HasTie(string id, int bit) => (Ties(id) & bit) != 0;

        private void SetTie(MapFaction faction, int bit, bool on)
        {
            var tie = Tie(faction.id, true);
            if (on)
            {
                if (bit == Diplomacy.Vassal && (tie.status & Diplomacy.Vassal) == 0) tie.since = MonthStamp;
                tie.status |= bit;
                if (bit == Diplomacy.Vassal) tie.status &= ~Diplomacy.Awed;
            }
            else tie.status &= ~bit;
        }

        /// <summary>Envoy eloquence: the best 魅力 + 悟性/2 among the head and the uninjured disciples.</summary>
        internal int Eloquence(out string envoy)
        {
            int Score(int[] attrs) => AttributeSystem.Value(attrs, "cha") + AttributeSystem.Value(attrs, "wit") / 2;
            int best = Score(AttributeSystem.Normalize(Protagonist.attrs));
            envoy = string.IsNullOrWhiteSpace(Protagonist.name) ? "掌門" : Protagonist.name;
            foreach (var disciple in Disciples.Where(member => member != null && member.injury < 3))
            {
                int score = Score(AttributeSystem.Normalize(disciple.attrs));
                if (score <= best) continue;
                best = score;
                envoy = disciple.name;
            }
            return best;
        }

        /// <summary>本派聲威: reputation, a fifth of the sect's martial power and five per sworn ally.</summary>
        internal int Prestige => Reputation + SectPower / 5 +
            5 * (Story.ties ?? Array.Empty<DiploTie>()).Count(tie => tie != null && (tie.status & Diplomacy.Alliance) != 0);

        /// <summary>+8% sect power per defence pact or sworn alliance, at most +40%.</summary>
        internal float DiplomacyPowerBonus => Mathf.Min(.4f, .08f * (Story.ties ?? Array.Empty<DiploTie>())
            .Count(tie => tie != null && (tie.status & (Diplomacy.Pact | Diplomacy.Alliance)) != 0));

        internal static DiploAction DiploActionById(string id) => Array.Find(Diplomacy.Data.actions ?? Array.Empty<DiploAction>(), a => a.id == id);

        /// <summary>Why an action is unavailable right now (null when it can be attempted).</summary>
        internal string DiploBlock(DiploAction action, MapFaction faction)
        {
            if (action == null || faction == null) return "無此行動";
            int ties = Ties(faction.id);
            if ((ties & Diplomacy.Merged) != 0) return "已併入本派";
            if (!IsFactionKnown(faction.id) && action.id != "envoy") return "尚未探明";
            string group = Diplomacy.Group(faction);
            if (action.groups != null && !action.groups.Contains(group))
                return group == "bandit" ? "草莽不講此禮" : group == "court" ? "朝廷外邦不可為" : "不適用";
            int bit = Diplomacy.Bit(action.forbid);
            if (bit != 0 && (ties & bit) != 0) return "已" + Diplomacy.TieNames(bit);
            if (!string.IsNullOrEmpty(action.requires))
            {
                int need = action.requires.Split('|').Sum(Diplomacy.Bit);
                if ((ties & need) == 0)
                    return "須先" + string.Join("或", action.requires.Split('|').Select(name => Diplomacy.TieNames(Diplomacy.Bit(name))));
            }
            if (action.vassalMonths > 0 && MonthStamp - (Tie(faction.id)?.since ?? MonthStamp) < action.vassalMonths)
                return $"附庸須滿 {action.vassalMonths} 個月";
            int attitude = Attitude(faction);
            bool awed = action.awedBypass && (ties & Diplomacy.Awed) != 0;
            if (attitude < action.minAttitude && !awed) return $"態度須 ≥ {action.minAttitude}" + (action.awedBypass ? "或已懾服" : "");
            if (attitude > action.maxAttitude) return action.id == "mediate" ? "並無仇怨" : "交情已深";
            if (action.needRatio > 0 && Prestige < Diplomacy.Strength(faction) * action.needRatio)
                return $"聲威須達 {Mathf.CeilToInt(Diplomacy.Strength(faction) * action.needRatio)}";
            if (ActionPoints < action.ap) return "行動力不足";
            if (Silver < action.silver) return "銀兩不足";
            if (Reputation < action.reputation) return "聲望不足";
            return null;
        }

        internal float DiploChance(DiploAction action, MapFaction faction)
        {
            int eloquence = Eloquence(out _);
            float gap = Mathf.Clamp((Prestige - Diplomacy.Strength(faction)) * .004f, -.2f, .2f);
            float chance = action.chance / 100f + (eloquence - 15) * .02f + Attitude(faction) * .003f + gap;
            return Mathf.Clamp(chance, .05f, .95f);
        }

        /// <summary>Attempts a diplomatic action, paying its costs; returns the narrated result.</summary>
        internal string DiploAct(DiploAction action, MapFaction faction)
        {
            string block = DiploBlock(action, faction);
            if (block != null) return $"{action?.name}：{block}。";
            float chance = DiploChance(action, faction);
            if (!TrySpendAp(action.ap, "diplomacy:" + action.id + ":" + faction.id)) return "行動力不足。";
            Silver -= action.silver;
            Reputation -= action.reputation;
            Eloquence(out string envoy);
            bool success = UnityEngine.Random.value < chance;
            if (!success)
            {
                ShiftAttitude(faction, action.fail);
                if (action.id == "intimidate") Reputation -= 2;
                return $"【{action.name}失敗】{Diplomacy.Fill(action.failure, faction)}\n（使者 {envoy}，成功率 {chance:P0}；態度 {action.fail:+0;-0}）";
            }
            int gain = action.gain;
            string extra = "";
            switch (action.id)
            {
                case "envoy":
                    SetFactionKnown(faction.id);
                    break;
                case "trade": SetTie(faction, Diplomacy.Trade, true); break;
                case "mediate":
                    gain = Mathf.Max(0, Mathf.Min(gain, 10 - Attitude(faction)));
                    ApplyEffects(new[] { new GameEffect { key = "reputation", value = 2 } });
                    extra = "聲望 +2。";
                    break;
                case "pact": SetTie(faction, Diplomacy.Pact, true); break;
                case "alliance":
                    SetTie(faction, Diplomacy.Alliance, true);
                    foreach (string rival in Diplomacy.Rivals(faction.id))
                    {
                        var enemy = FindFaction(rival);
                        if (enemy == null) continue;
                        ShiftAttitude(enemy, -10);
                        extra += $"{enemy.Label}視本派為敵，態度 −10。";
                    }
                    break;
                case "intimidate": SetTie(faction, Diplomacy.Awed, true); break;
                case "vassal": SetTie(faction, Diplomacy.Vassal, true); break;
                case "merge":
                    SetTie(faction, Diplomacy.Merged, true);
                    var nodes = WorldMapData.Load().Holdings(faction).Select(node => node.id).Where(id => !HoldsNode(id)).ToArray();
                    Story.heldNodes = (Story.heldNodes ?? Array.Empty<string>()).Concat(nodes).ToArray();
                    ApplyEffects(new[] { new GameEffect { key = "reputation", value = 5 } });
                    extra = $"新增據點 {nodes.Length} 處，聲望 +5。";
                    break;
            }
            ShiftAttitude(faction, gain);
            return $"【{action.name}成功】{Diplomacy.Fill(action.success, faction)}\n（使者 {envoy}；態度 {gain:+0;-0}）{extra}";
        }

        // ---- Victory ---------------------------------------------------------------------------

        internal VictoryProgress Victory
        {
            get
            {
                var rules = Diplomacy.Data.victory ?? new DiploVictory();
                int loyal = 0, subjects = 0;
                foreach (var faction in WorldMapData.Load().Factions.Where(Diplomacy.Jianghu))
                {
                    int ties = Ties(faction.id);
                    if ((ties & (Diplomacy.Vassal | Diplomacy.Merged)) != 0) { loyal++; subjects++; }
                    else if ((ties & Diplomacy.Alliance) != 0) loyal++;
                }
                return new VictoryProgress(loyal, subjects, Reputation, Story.heldNodes?.Length ?? 0, rules);
            }
        }

        internal int JianghuCount => WorldMapData.Load().Factions.Count(Diplomacy.Jianghu);

        /// <summary>True once, the first time the 江湖盟主 victory is reached (so the panel can proclaim it).</summary>
        internal bool ClaimDiplomaticVictory()
        {
            if (!Victory.Diplomatic || (Story.flags ?? Array.Empty<StoryValue>()).Any(flag => flag.id == "diplo_victory" && flag.value > 0))
                return false;
            SetValue(ref Story.flags, "diplo_victory", 1, false);
            return true;
        }

        // ---- Events ----------------------------------------------------------------------------

        internal DiploEvent PendingDiploEvent =>
            string.IsNullOrEmpty(Story.diploEvent) ? null : Array.Find(Diplomacy.Data.events ?? Array.Empty<DiploEvent>(), e => e.id == Story.diploEvent);

        internal MapFaction PendingDiploTarget => FindFaction(Story.diploEventTarget);

        private bool EventFits(DiploEvent diploEvent, MapFaction faction)
        {
            int ties = Ties(faction.id), attitude = Attitude(faction);
            if ((ties & Diplomacy.Merged) != 0) return false;
            switch (diploEvent.target)
            {
                case "friendly": return Diplomacy.Jianghu(faction) && attitude >= 25;
                case "proposal": return !faction.Outlaw && attitude >= 35 && (ties & Diplomacy.Pact) == 0;
                case "vassal": return (ties & Diplomacy.Vassal) != 0;
                case "trade": return (ties & Diplomacy.Trade) != 0;
                case "rival":
                    return Diplomacy.Rivals(faction.id).Any(id => (Ties(id) & (Diplomacy.Alliance | Diplomacy.Vassal | Diplomacy.Merged)) != 0);
                case "weak":
                    return Diplomacy.Group(faction) != "court" && attitude >= 20 && (ties & Diplomacy.Vassal) == 0 &&
                        Prestige >= Diplomacy.Strength(faction) * Diplomacy.Data.weakRatio;
                default: return false;
            }
        }

        /// <summary>Picks a weighted event whose target filter matches some known faction; stores it as pending.</summary>
        internal bool RollDiploEvent(string forceId = null)
        {
            var candidates = new List<(DiploEvent diploEvent, MapFaction faction)>();
            foreach (var diploEvent in Diplomacy.Data.events ?? Array.Empty<DiploEvent>())
            {
                if (forceId != null && diploEvent.id != forceId) continue;
                var targets = WorldMapData.Load().Factions.Where(f => IsFactionKnown(f.id) && EventFits(diploEvent, f)).ToArray();
                if (targets.Length == 0) continue;
                var pick = targets[UnityEngine.Random.Range(0, targets.Length)];
                for (int i = 0; i < Mathf.Max(1, diploEvent.weight); i++) candidates.Add((diploEvent, pick));
            }
            if (candidates.Count == 0) return false;
            var chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            Story.diploEvent = chosen.diploEvent.id;
            Story.diploEventTarget = chosen.faction.id;
            return true;
        }

        internal string DiploOptionBlock(DiploOption option)
        {
            if (option.ap > ActionPoints) return "行動力不足";
            if (option.silver > Silver) return "銀兩不足";
            return null;
        }

        internal string ResolveDiploEvent(int index)
        {
            var diploEvent = PendingDiploEvent;
            var faction = PendingDiploTarget;
            if (diploEvent == null || faction == null || index < 0 || index >= (diploEvent.options?.Length ?? 0))
            {
                Story.diploEvent = Story.diploEventTarget = null;
                return "急報已過時。";
            }
            var option = diploEvent.options[index];
            string block = DiploOptionBlock(option);
            if (block != null) return block + "。";
            TrySpendAp(option.ap, "diplo_event:" + diploEvent.id);
            Silver -= option.silver;
            var parts = new List<string>();
            foreach (var effect in option.effects ?? Array.Empty<GameEffect>())
            {
                int value = Mathf.RoundToInt(effect.value);
                switch (effect.key)
                {
                    case "attitude": ShiftAttitude(faction, value); parts.Add($"{faction.Label}態度 {value:+0;-0}"); break;
                    case "tie":
                        SetTie(faction, Diplomacy.Bit(effect.@ref), value > 0);
                        parts.Add(value > 0 ? "結成" + Diplomacy.TieNames(Diplomacy.Bit(effect.@ref)) : "解除" + Diplomacy.TieNames(Diplomacy.Bit(effect.@ref)));
                        break;
                    default:
                        ApplyEffects(new[] { effect });
                        if (effect.key == "reputation") parts.Add($"聲望 {value:+0;-0}");
                        break;
                }
            }
            if (option.silver > 0) parts.Insert(0, $"銀兩 −{option.silver}");
            if (option.ap > 0) parts.Insert(0, $"行動力 −{option.ap}");
            Story.diploEvent = Story.diploEventTarget = null;
            return Diplomacy.Fill(option.result, faction) + (parts.Count > 0 ? "\n（" + string.Join("，", parts) + "）" : "");
        }

        // ---- Month end AI ----------------------------------------------------------------------

        /// <summary>AI reactions at month end: drift, ties, tribute, vassal unrest, rival grudges, envy and event rolls.</summary>
        private void DiplomacyMonth(MonthlyReport report)
        {
            var map = WorldMapData.Load();
            int trade = 0, tribute = 0;
            var notes = new List<string>();
            int held = Story.heldNodes?.Length ?? 0;
            foreach (var faction in map.Factions)
            {
                int ties = Ties(faction.id);
                if ((ties & Diplomacy.Merged) != 0) continue;
                int shift = RelationShift(faction.id);
                int delta = 0;
                if ((ties & (Diplomacy.Trade | Diplomacy.Pact | Diplomacy.Alliance | Diplomacy.Vassal)) == 0)
                    delta += shift > 0 ? -1 : shift < 0 ? 1 : 0;
                if ((ties & Diplomacy.Trade) != 0)
                {
                    delta += 1;
                    trade += faction.type == "merchant" ? 12 : 6;
                }
                if ((ties & Diplomacy.Pact) != 0) delta += 1;
                if ((ties & Diplomacy.Alliance) != 0) delta += 1;
                if ((ties & Diplomacy.Vassal) != 0)
                {
                    delta -= 1;
                    tribute += Mathf.Clamp(faction.money / 600, 5, 30);
                    if (Attitude(faction) < 10 && UnityEngine.Random.value < .3f)
                    {
                        SetTie(faction, Diplomacy.Vassal, false);
                        delta -= 15;
                        notes.Add($"{faction.Label}不堪朝貢，叛離本派！");
                    }
                }
                foreach (string rival in Diplomacy.Rivals(faction.id))
                    if ((Ties(rival) & (Diplomacy.Alliance | Diplomacy.Vassal | Diplomacy.Merged)) != 0) delta -= 2;
                if (held >= 6 && Attitude(faction) < 0) delta -= 1;
                ShiftAttitude(faction, delta);
            }
            if (trade + tribute > 0)
            {
                Silver += trade + tribute;
                report.Lines.Add($"外交收入　通商 {trade}、附庸朝貢 {tribute}");
            }
            report.Lines.AddRange(notes);
            if (PendingDiploEvent == null && UnityEngine.Random.value < Diplomacy.Data.eventChance && RollDiploEvent())
                report.Lines.Add($"外交急報：{PendingDiploEvent.title}（{PendingDiploTarget?.Label}）——請至江湖地圖「外交」處理。");
        }

        /// <summary>Capture-only demo campaign: a mid-game sect with a few allies, a vassal and some outlaw holds.</summary>
        internal void SeedDiplomacyDemo()
        {
            Reputation = 48;
            Silver = 420;
            Year = 2;
            Month = 6;
            foreach (string id in WorldMapData.Load().Factions.Select(f => f.id)) SetFactionKnown(id);
            void Seed(string id, int shift, int status, int monthsAgo = 0)
            {
                var faction = FindFaction(id);
                if (faction == null) return;
                ShiftAttitude(faction, shift);
                var tie = Tie(id, true);
                tie.status = status;
                tie.since = MonthStamp - monthsAgo;
            }
            Seed("shaolin", 16, Diplomacy.Trade | Diplomacy.Pact | Diplomacy.Alliance);
            Seed("funiu", 14, Diplomacy.Vassal, 4);
            Seed("sute", 12, Diplomacy.Trade | Diplomacy.Alliance);
            Seed("huayue", -8, Diplomacy.Trade);
            Seed("emei", 34, Diplomacy.Pact);
            Seed("wulong", 18, Diplomacy.Awed);
            Seed("yanbang", -34, 0);
            Seed("taishan_xiangma", 60, Diplomacy.Vassal, 2);
            Story.heldNodes = new[] { "qinzhou", "lintao", "hanzhong" };
        }
    }
}
