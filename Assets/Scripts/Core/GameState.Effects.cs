using System;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    public sealed partial class GameState
    {
        public ProtagonistData Protagonist { get; private set; } = new();
        public StoryProgress Story { get; private set; } = new();

        internal int ReputationEffect(int value) => value > 0
            ? Mathf.Max(1, Mathf.RoundToInt(value * BalanceV1126c.Reputation.gainScalePct / 100f *
                (1 + AttributeSystem.ReputationPerPoint * AttributeBonus("cha"))))
            : value;

        public void ApplyEffects(GameEffect[] effects)
        {
            foreach (var effect in effects ?? Array.Empty<GameEffect>())
            {
                if (effect == null)
                    continue;
                int value = Mathf.RoundToInt(effect.value);
                switch (effect.key)
                {
                    case "silver": Silver = Mathf.Max(0, Silver + value); break;
                    case "food": Food = Mathf.Max(0, Food + value); break;
                    // 魅力 widens reputation gains (never losses).
                    case "reputation":
                        // v1126c PA3: reputation.gainScalePct (balance_v1126c.json) scales every event / quest / diplomacy gain.
                        Reputation += ReputationEffect(value);
                        break;
                    case "monthlyTerms": Story.monthlyTermsLeft = value; break;
                    case "monthlyFood": Story.monthlyFood += value; break;
                    case "monthlySilver": Story.monthlySilver += value; break;
                    case "siteFood": Story.siteFood += value; break; // v1126c PC3 #8: site yield, every month
                    case "siteSilver": Story.siteSilver += value; break;
                    case "apMax": Story.apBonus += value; break;
                    case "recruitPoolSize": Story.recruitPoolSize += value; break;
                    case "recruitCostPct": Story.recruitCostPct += value; break;
                    case "buildCostPct": Story.buildCostPct += value; break;
                    case "trainBonusPct": Story.trainBonusPct += value; break;
                    case "insightChance": Story.insightChance += effect.value; break;
                    case "loyaltyAll":
                        Story.loyaltyBonus += value;
                        foreach (var disciple in Disciples)
                            disciple.loyalty = Mathf.Clamp(disciple.loyalty + value, 0, 100);
                        break;
                    case "noGraveInjury": Protagonist.noGraveInjury = value > 0; break;
                    case "str":
                    case "agi":
                    case "con":
                    case "qi":
                    case "wit":
                    case "cha":
                    case "luk":
                        Protagonist.attributeBonuses ??= new int[AttributeSystem.Count];
                        Protagonist.attributeBonuses[AttributeSystem.Index(effect.key)] += value;
                        break;
                    case "knownFaction": SetFactionKnown(effect.@ref); break;
                    case "flag": SetValue(ref Story.flags, effect.@ref, value, false); break;
                    case "charRelation": SetValue(ref Story.characterRelations, CharacterId(effect.@ref), value, true); break;
                    case "relation": SetValue(ref Story.relations, effect.@ref, value, true); break;
                    case "rank":
                    case "lineRank":
                    case "cap":
                        // v1124 C12: 「主修武學」 effects follow the player's chosen major, not whatever line is highest.
                        var line = effect.@ref == "martial" ? RankSystem.Major(Protagonist.lines, Protagonist.major)
                            : Array.Find(Protagonist.lines, entry => entry.id == effect.@ref);
                        // Additive: rank effects carry the cap up with them, cap effects only widen potential.
                        if (effect.key == "cap") RankSystem.Raise(line, 0, value);
                        else RankSystem.Raise(line, value, 0);
                        break;
                    case "studyPct":
                        SetValue(ref Protagonist.study, StudyKey(effect.@ref), value, true);
                        break;
                    case "art": LearnArt(effect.@ref); break;
                    case "followers":
                        var member = Disciples.Find(d => d.id == effect.@ref);
                        if (value < 0 && member != null)
                            Disciples.Remove(member);
                        if (value > 0 && member == null)
                        {
                            var seed = Data.Disciples.FirstOrDefault(d => d.id == effect.@ref);
                            if (seed != null)
                            {
                                member = CloneDisciple(seed);
                                member.loyalty = Mathf.Clamp(member.loyalty + Story.loyaltyBonus, 0, 100);
                                EnsurePortrait(member);
                                Disciples.Add(member);
                            }
                        }
                        break;
                    // v1124 C15–C18: realm / hall rewards.
                    case "item": LegacySystem.AddInventory(Story, effect.@ref, value); break;
                    case "gear": EquipOrStore(MakeGear(effect.@ref, value)); break;
                    case "progress": StudyLine(effect.@ref, effect.value); break;
                    case "injury":
                        if (value > 0) Injure(value);
                        else Protagonist.injury = Mathf.Max(0, Protagonist.injury + value);
                        break;
                    case "heal": for (int i = 0; i < Mathf.Max(1, value); i++) RecoverInjuries(); break;
                    case "discipleProgress":
                        foreach (var pupil in Disciples) PracticeMartial(pupil.id, effect.value);
                        break;
                    default: Debug.LogWarning($"[Effects] Unknown key {effect.key}"); break;
                }
            }
            ActionPoints = Mathf.Min(ActionPoints, ApMax);
        }

        /// <summary>The authored sect disciples, whose own portrait always wins over any pooled recruit art.</summary>
        internal static readonly string[] NamedDisciples =
            { "lin", "xu", "luo", "du", "han", "kang", "pei", "shen", "song", "wei", "cui", "qiao" };

        internal bool IsNamedDisciple(string id) =>
            !string.IsNullOrEmpty(id) && (Array.IndexOf(NamedDisciples, id) >= 0 || Data.Disciples.Any(seed => seed.id == id));

        internal void EnsurePortrait(DiscipleData person)
        {
            // Old saves may carry a stale recruit_* or superseded id; a named disciple always uses its own art.
            if (IsNamedDisciple(person.id) && PortraitService.FindPortrait(person.id) != null)
            {
                person.portrait = person.id;
                return;
            }
            if (string.IsNullOrEmpty(person.portrait) && PortraitService.FindPortrait(person.id) == null)
                person.portrait = PortraitPool.Assign(Story, person);
        }

        internal void InjureWorker(string id)
        {
            if (id == HeroId) Injure(1);
            else if (Disciples.Find(member => member.id == id) is DiscipleData member)
                member.injury = Mathf.Min(9, member.injury + 1);
        }

        internal int[] DiscipleAttributes(DiscipleData member)
        {
            int rank = member == null ? 0 : AssignmentSystem.RankOf(this, member.id).Ability;
            return AttributeSystem.Normalize(member?.attrs).Select(AttributeSystem.Allocated)
                .Select(value => value + rank).ToArray();
        }

        public void RefillAp() => ActionPoints = ApMax;

        private static DiscipleData CloneDisciple(DiscipleData seed)
        {
            var copy = JsonUtility.FromJson<DiscipleData>(JsonUtility.ToJson(seed));
            copy.lines = RankSystem.Seed(copy.lines, copy.rank, copy.cap, RankSystem.BaseCap, RankSystem.BaseRank);
            copy.attrs = AttributeSystem.Normalize(copy.attrs);
            return copy;
        }

        private static void SetValue(ref StoryValue[] values, string id, int value, bool accumulate)
        {
            if (string.IsNullOrEmpty(id))
                return;
            var existing = Array.Find(values, entry => entry.id == id);
            if (existing == null)
            {
                existing = new StoryValue { id = id };
                Array.Resize(ref values, values.Length + 1);
                values[values.Length - 1] = existing;
            }
            existing.value = accumulate ? Mathf.Clamp(existing.value + value, -100, 100) : value;
        }
    }

    public static class InjuryService
    {
        public static bool CanSufferGraveInjury(ProtagonistData character) => !character.noGraveInjury;

        public static bool TryInjure(ProtagonistData character, int severity, bool grave)
        {
            if (severity <= 0 || (grave && !CanSufferGraveInjury(character)))
                return false;
            character.injury = Mathf.Max(severity, character.injury);
            return true;
        }
    }
}
