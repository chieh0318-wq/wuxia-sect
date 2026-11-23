using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Wuxia
{
    [Serializable] internal sealed class TalkBands
    {
        public string cold, neutral, warm;
        public string Get(string band) => band == "cold" ? cold : band == "warm" ? warm : neutral ?? warm;
    }

    [Serializable] internal sealed class Affinity<T>
    {
        public T frosty, low, mid, high;
        public T Get(string tier) => tier == "frosty" ? frosty : tier == "low" ? low : tier == "high" ? high : mid;
    }

    [Serializable] internal sealed class TalkLine
    {
        public string speaker, text, role, tier;
    }

    [Serializable] internal sealed class TalkAdvice
    {
        public string role, tier, band, text, recommends;
    }

    [Serializable] internal sealed class TalkPersona
    {
        public string id;
        public string[] factions;
        public TalkBands greet;
        public TalkHints hint;
    }

    [Serializable] internal sealed class TalkHints
    {
        public string dashixiong, shijie;
        public string Get(string role) => role == "dashixiong" ? dashixiong : role == "shijie" ? shijie : null;
    }

    [Serializable] internal sealed class TalkScene
    {
        public string id;
        public TalkAdvice[] advisors = Array.Empty<TalkAdvice>();
        public TalkBands opening;
        public TalkOpening[] openingByPersona = Array.Empty<TalkOpening>();
        public TalkChoice[] options = Array.Empty<TalkChoice>();
        public TalkLine[] arrive, success, failure;

        internal bool Covers(DiploEvent incident) => incident.options != null &&
            options.Select(option => option.index).Distinct().OrderBy(index => index)
                .SequenceEqual(Enumerable.Range(0, incident.options.Length));
    }

    [Serializable] internal sealed class TalkOpening { public string id; public TalkBands lines; }
    [Serializable] internal sealed class TalkText { public string id, text; }
    [Serializable] internal sealed class TalkLines { public string id; public string[] lines; }
    [Serializable] internal sealed class TalkNumber { public string id; public int value; }
    [Serializable] internal sealed class TieModifier { public string id; public TalkModifierValues mods; }

    [Serializable] internal sealed class TalkChoice
    {
        public string id, label, line;
        public int index;
        public TalkBands reaction;
        public TalkText[] personaReaction = Array.Empty<TalkText>();
        public TalkLine aftermath;
        public TalkModifiers mods;
        public GameEffect[] effects;
    }

    [Serializable] internal class TalkModifierValues
    {
        public int chance, silver, ap, reputation, gain, fail;
        public TalkNumber[] persona = Array.Empty<TalkNumber>(), band = Array.Empty<TalkNumber>();
        public TalkTerms terms;
    }

    [Serializable] internal sealed class TalkModifiers : TalkModifierValues
    {
        public string requires;
        public string[] hideForPersona = Array.Empty<string>();
        public TieModifier[] ifTie = Array.Empty<TieModifier>();
        public GameEffect[] effects;

        public bool Visible(GameState state, MapFaction target, string personality, string attitude) =>
            !hideForPersona.Contains(personality) && (string.IsNullOrEmpty(requires) ||
                requires.Split('|').Any(id => id == attitude || state.HasTie(target.id, Diplomacy.Bit(id))));

        public TalkModifiers Effective(GameState state, MapFaction target, string personality, string attitude)
        {
            var total = new TalkModifiers { terms = new TalkTerms() };
            Add(this);
            foreach (var entry in ifTie)
                if (state.HasTie(target.id, Diplomacy.Bit(entry.id))) Add(entry.mods);
            return total;

            void Add(TalkModifierValues value)
            {
                total.chance += value.chance + (value.persona.FirstOrDefault(x => x.id == personality)?.value ?? 0)
                    + (value.band.FirstOrDefault(x => x.id == attitude)?.value ?? 0);
                total.silver += value.silver;
                total.ap += value.ap;
                total.reputation += value.reputation;
                total.gain += value.gain;
                total.fail += value.fail;
                if (value.terms == null) return;
                total.terms.tradeIncome += value.terms.tradeIncome;
                total.terms.tributePct += value.terms.tributePct;
                total.terms.pactPower += value.terms.pactPower;
                total.terms.rivalShift += value.terms.rivalShift;
                total.terms.repOnSuccess += value.terms.repOnSuccess;
                total.terms.monthlyAttitude += value.terms.monthlyAttitude;
            }
        }
    }

    [Serializable] internal sealed class TalkTerms
    {
        public int tradeIncome, tributePct, pactPower, rivalShift, repOnSuccess, monthlyAttitude;
    }

    [Serializable] internal sealed class TalkEnvoy { public TalkBands arrive, arriveSelf; }

    [Serializable] internal sealed class DiplomacyDialogue
    {
        public TalkScene[] actions = Array.Empty<TalkScene>(), events = Array.Empty<TalkScene>();
        public TalkPersona[] personas = Array.Empty<TalkPersona>();
        public TalkEnvoy envoy;
        private static DiplomacyDialogue data;
        internal static DiplomacyDialogue Data => data ??= M2Data.Load<DiplomacyDialogue>("diplomacy_dialogue");
    }

    [Serializable] internal sealed class CastMember
    {
        public string id, role, camp;
        public int startLoyalty;
        public Affinity<string> address;
        public Affinity<string[]> daily;
        public CastVoice advisorVoice;
        public CastAftermath aftermath;
    }

    [Serializable] internal sealed class CastVoice { public CastFrosty frosty; }
    [Serializable] internal sealed class CastFrosty { public string advisor; }
    [Serializable] internal sealed class CastAftermath { public Affinity<string> success, failure; }
    [Serializable] internal sealed class CastFeedback { public int adoptedSuccess = 2, opposedFailure = -2; }

    [Serializable] internal sealed class CastDialogue
    {
        public CastMember[] cast = Array.Empty<CastMember>();
        public TalkLines[] interjections = Array.Empty<TalkLines>();
        public CastFeedback adviceFeedback = new();
        private static CastDialogue data;
        internal static CastDialogue Data => data ??= M2Data.Load<CastDialogue>("cast_dialogue");
        internal static CastMember Find(string id) => Array.Find(Data.cast, member => member.id == id);
        internal static int Score(GameState state, DiscipleData member) => Mathf.Clamp(member.loyalty + state.CharacterRelation(member.id), 0, 100);
        internal static string Tier(int score) => score < 15 ? "frosty" : score < 40 ? "low" : score < 70 ? "mid" : "high";
        internal static T Pick<T>(T[] values) => values == null || values.Length == 0 ? default : values[UnityEngine.Random.Range(0, values.Length)];

        private static string Address(GameState state, string id)
        {
            var cast = Find(id);
            var disciple = state.Disciples.FirstOrDefault(member => member.id == id);
            return cast?.address?.Get(Tier(disciple == null ? cast.startLoyalty : Score(state, disciple))) ?? state.Protagonist.title;
        }

        internal static string Format(GameState state, string text, string speaker = null)
        {
            var hero = state.Protagonist;
            bool female = hero.gender == "女";
            return Regex.Replace(text ?? "", @"\{call:([^}]+)\}", match => Address(state, match.Groups[1].Value))
                .Replace("{call}", Address(state, speaker)).Replace("{player}", hero.name)
                .Replace("{sect}", state.SectLabel).Replace("{兄姐}", female ? "姐" : "兄").Replace("{弟妹}", female ? "妹" : "弟");
        }
    }
}
