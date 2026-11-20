using System;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    public sealed partial class GameState
    {
        public ProtagonistData Protagonist { get; private set; } = new();
        public StoryProgress Story { get; private set; } = new();

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
                    case "reputation": Reputation += value; break;
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
                    case "str": Protagonist.attrs[0] += value; break;
                    case "agi": Protagonist.attrs[1] += value; break;
                    case "con": Protagonist.attrs[2] += value; break;
                    case "wit": Protagonist.attrs[3] += value; break;
                    case "cha": Protagonist.attrs[4] += value; break;
                    case "flag": SetValue(ref Story.flags, effect.@ref, value, false); break;
                    case "relation": SetValue(ref Story.relations, effect.@ref, value, true); break;
                    case "rank":
                    case "lineRank":
                    case "cap":
                        var line = effect.@ref == "martial" ? RankSystem.BestCombat(Protagonist.lines)
                            : Array.Find(Protagonist.lines, entry => entry.id == effect.@ref);
                        RankSystem.Shift(line, value, effect.key == "cap");
                        break;
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
                    default: Debug.LogWarning($"[Effects] Unknown key {effect.key}"); break;
                }
            }
            ActionPoints = Mathf.Min(ActionPoints, ApMax);
        }

        internal void EnsurePortrait(DiscipleData person)
        {
            if (string.IsNullOrEmpty(person.portrait) && PortraitService.FindPortrait(person.id) == null)
                person.portrait = PortraitPool.Assign(Story, person);
        }

        public void RefillAp() => ActionPoints = ApMax;

        private static DiscipleData CloneDisciple(DiscipleData seed)
        {
            var copy = JsonUtility.FromJson<DiscipleData>(JsonUtility.ToJson(seed));
            copy.lines = RankSystem.Seed(copy.lines, copy.rank, copy.cap, "C");
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
