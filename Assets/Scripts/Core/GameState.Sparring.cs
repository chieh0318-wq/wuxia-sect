using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    [Serializable] internal sealed class SparReaction { public string id; public string[] win, lose; }

    /// <summary>sparring.json (v1124 C17): costs, narration templates and per-disciple reactions.</summary>
    [Serializable] internal sealed class SparCatalog
    {
        public int apCost = 1, foodCost = 1;
        public string[] openers, exchanges, finishers;
        public SparReaction[] reactions;
        private static SparCatalog data;
        internal static SparCatalog Data => data ??= M2Data.Load<SparCatalog>("sparring");
    }

    /// <summary>One fighter's line in the sparring result: placing, growth and favour change.</summary>
    internal sealed class SparStanding
    {
        public string id, name, gains;
        public int wins, place;
    }

    internal sealed class SparResult
    {
        public readonly List<string> narration = new();
        public readonly List<SparStanding> standings = new();
        public readonly List<(string speaker, string text)> reactions = new();
        public string Winner => standings.FirstOrDefault()?.name;
    }

    /// <summary>
    /// v1124 C17 山門演武: fellow disciples spar with each other (two-person drill or a three-to-four person round robin).
    /// Costs AP and a little food; winners grow a bit more, everyone grows a little, and favour shifts.
    /// </summary>
    public sealed partial class GameState
    {
        internal const string HeroId = "hero";

        /// <summary>Everyone fit to spar: the protagonist and every disciple not badly hurt.</summary>
        internal IEnumerable<string> SparCandidates =>
            new[] { HeroId }.Concat(Disciples.Where(member => member != null && member.injury < 3).Select(member => member.id));

        internal string SparName(string id) => id == HeroId ? Protagonist.name : Disciples.FirstOrDefault(d => d.id == id)?.name ?? id;

        internal DerivedStats SparStats(string id)
        {
            if (id == HeroId) return HeroStats;
            var member = Disciples.FirstOrDefault(d => d.id == id);
            if (member == null) return default;
            var lines = member.lines is { Length: > 0 } ? member.lines
                : RankSystem.Seed(null, member.rank, member.cap, RankSystem.BaseCap, RankSystem.BaseRank);
            return AttributeSystem.Derive(DiscipleAttributes(member), lines, WeaponRankDelta(member.weapon));
        }

        /// <summary>A single strength score used for simulated bouts and the odds shown on the picker.</summary>
        internal float SparPower(string id)
        {
            var stats = SparStats(id);
            // v1126e #1: the stronger of 外功／內功 plus half of 外防＋內防 (each about 攻擊 ÷ 3 at base).
            return Mathf.Max(1, stats.Offense + (stats.OuterDefense + stats.InnerDefense) * .25f + stats.MaxHp * .2f +
                stats.MaxQi * .1f + stats.Speed * 1.5f + stats.Dodge * 150);
        }

        /// <summary>The best martial line of a fighter, as 「劍 B／A」.</summary>
        internal string SparLine(string id)
        {
            var line = id == HeroId ? RankSystem.Major(Protagonist.lines, Protagonist.major)
                : RankSystem.BestCombat(Disciples.FirstOrDefault(d => d.id == id)?.lines);
            if (line == null) return "—";
            string name = RankSystem.Categories.FirstOrDefault(category => category.id == line.id)?.name ?? "武學";
            return $"{name} {line.rank}／{line.cap}";
        }

        /// <summary>The art a fighter shows off in the narration.</summary>
        private string SparArt(string id)
        {
            var arts = (id == HeroId ? ProtagonistArts : ArtsOf(Disciples.FirstOrDefault(d => d.id == id)))
                .Where(art => art.Combat).ToArray();
            if (arts.Length == 0) return "崤山劍法";
            return arts[UnityEngine.Random.Range(0, arts.Length)].name;
        }

        internal int SparApCost => Mathf.Max(0, SparCatalog.Data.apCost);
        internal int SparFoodCost => Mathf.Max(0, SparCatalog.Data.foodCost);

        /// <summary>Why this line-up cannot spar now, or null.</summary>
        internal string SparBlock(IList<string> ids, bool melee)
        {
            if (melee ? ids.Count is < 3 or > 4 : ids.Count != 2)
                return melee ? "多人混戰須選 3–4 人。" : "兩人對練須選 2 人。";
            if (ActionPoints < SparApCost) return $"行動力不足（需 {SparApCost} 點）。";
            if (Food < SparFoodCost) return $"糧食不足（需 {SparFoodCost}）。";
            return null;
        }

        /// <summary>Pays the AP and food for one sparring session.</summary>
        internal bool PaySpar(IList<string> ids = null)
        {
            if (ids?.Any(id => AssignmentSystem.Block(this, id) != null) == true) return false;
            if (Food < SparFoodCost || !TrySpendAp(SparApCost, "sparring")) return false;
            Pay(new Cost { food = SparFoodCost });
            if (ids != null) AssignmentSystem.Commit(this, ids);
            return true;
        }

        /// <summary>Win odds of a against b in a simulated bout.</summary>
        internal float SparOdds(string a, string b)
        {
            float pa = SparPower(a), pb = SparPower(b);
            return Mathf.Clamp(pa * pa / (pa * pa + pb * pb), .1f, .9f);
        }

        /// <summary>Simulates the session: a duel, or a round robin where every pair fights once.</summary>
        internal SparResult SimulateSpar(IList<string> ids)
        {
            var wins = ids.ToDictionary(id => id, _ => 0);
            var narration = new List<string>();
            var catalog = SparCatalog.Data;
            for (int i = 0; i < ids.Count; i++)
                for (int j = i + 1; j < ids.Count; j++)
                {
                    string a = ids[i], b = ids[j];
                    bool aWins = UnityEngine.Random.value < SparOdds(a, b);
                    wins[aWins ? a : b]++;
                    if (ids.Count == 2 || narration.Count < 6)
                        narration.AddRange(Bout(catalog, a, b, aWins ? a : b, ids.Count == 2 ? 2 : 1));
                }
            var order = ids.OrderByDescending(id => wins[id]).ThenByDescending(SparPower).ToList();
            var result = Settle(order, wins);
            result.narration.InsertRange(0, narration);
            return result;
        }

        /// <summary>Records a duel fought for real on the battle screen (the protagonist's side won or lost).</summary>
        internal SparResult SettleBattle(IList<string> party, IList<string> opponents, bool won)
        {
            var winners = won ? party : opponents;
            var wins = party.Concat(opponents).ToDictionary(id => id, id => winners.Contains(id) ? 1 : 0);
            var order = winners.Concat(won ? opponents : party).ToList();
            var result = Settle(order, wins);
            result.narration.Add(won ? $"{string.Join("、", party.Select(SparName))}在回合實戰中勝出。"
                : $"{string.Join("、", opponents.Select(SparName))}技高一籌，贏下了這場實戰。");
            return result;
        }

        private IEnumerable<string> Bout(SparCatalog catalog, string a, string b, string winner, int exchanges)
        {
            string loser = winner == a ? b : a;
            string Fill(string text) => (text ?? "").Replace("{a}", SparName(a)).Replace("{b}", SparName(b))
                .Replace("{artA}", SparArt(a)).Replace("{artB}", SparArt(b))
                .Replace("{w}", SparName(winner)).Replace("{l}", SparName(loser));
            yield return Fill(CastDialogue.Pick(catalog.openers) ?? "{a}與{b}相對而立。");
            for (int i = 0; i < exchanges; i++)
                yield return Fill(CastDialogue.Pick(catalog.exchanges) ?? "{a}與{b}你來我往，鬥了數十合。");
            yield return Fill(CastDialogue.Pick(catalog.finishers) ?? "{w}勝了一招。");
        }

        /// <summary>Applies growth and favour for the final order (first = champion) and builds the result.</summary>
        internal static float SparPractice(int place, float multiplier = 1) => BalanceV1126c.Martial.practiceProgress *
            (place == 0 ? 1.2f : Mathf.Max(.5f, .9f - .1f * (place - 1))) * multiplier;
        internal const float SparInjuryChance = .15f;

        private SparResult Settle(IList<string> order, IDictionary<string, int> wins)
        {
            var result = new SparResult();
            bool heroIn = order.Contains(HeroId);
            bool heroWon = heroIn && order[0] == HeroId;
            float exp = Mathf.Max(.5f, 1 + Story.trainBonusPct / 100f);
            for (int place = 0; place < order.Count; place++)
            {
                string id = order[place];
                AssignmentSystem.Credit(this, id, place == 0 ? 2 : 1);
                float gain = SparPractice(place, exp);
                var gains = new List<string>();
                if (id == HeroId)
                {
                    var line = CurrentStudyLine(id);
                    gains.Add(PracticeMartial(id, gain * StudyMultiplier(line?.id ?? "")));
                }
                else
                {
                    var member = Disciples.FirstOrDefault(d => d.id == id);
                    if (member == null) continue;
                    gains.Add(PracticeMartial(id, gain));
                    if (heroIn)
                    {
                        // Favour: sparring with the head of the sect brings them closer; 林照 respects being beaten.
                        int favour = member.id switch
                        {
                            "lin" => heroWon ? 3 : 1,
                            "xu" => 3,
                            "luo" => place == 0 ? 3 : 2,
                            _ => 2
                        };
                        ApplyEffects(new[] { new GameEffect { key = "charRelation", @ref = member.id, value = favour } });
                        gains.Add($"好感 +{favour}");
                    }
                    else
                    {
                        member.loyalty = Mathf.Clamp(member.loyalty + 1, 0, 100);
                        gains.Add("忠誠 +1");
                    }
                    if (place == order.Count - 1 && order.Count > 1 && UnityEngine.Random.value < SparInjuryChance)
                    {
                        member.injury = Mathf.Min(9, member.injury + 1);
                        gains.Add("輕傷 +1");
                    }
                }
                result.standings.Add(new SparStanding
                {
                    id = id, name = SparName(id), wins = wins.TryGetValue(id, out int w) ? w : 0, place = place + 1,
                    gains = string.Join("、", gains)
                });
            }
            if (heroIn)
                foreach (var standing in result.standings.Where(entry => entry.id != HeroId).Take(2))
                {
                    bool memberWon = order.IndexOf(standing.id) < order.IndexOf(HeroId);
                    var pool = SparCatalog.Data.reactions ?? Array.Empty<SparReaction>();
                    var reaction = pool.FirstOrDefault(entry => entry.id == standing.id) ?? pool.FirstOrDefault(entry => entry.id == "generic");
                    string line = CastDialogue.Pick(memberWon ? reaction?.win : reaction?.lose);
                    if (string.IsNullOrEmpty(line)) continue;
                    string winner = memberWon ? standing.name : Protagonist.name, loser = memberWon ? Protagonist.name : standing.name;
                    result.reactions.Add((standing.id, line.Replace("{hero}", Protagonist.name).Replace("{other}", Protagonist.name)
                        .Replace("{w}", winner).Replace("{l}", loser)));
                }
            return result;
        }
    }
}
