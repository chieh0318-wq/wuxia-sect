using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    /// <summary>
    /// v1124: turns data-driven <see cref="GameEffect"/> lists into the short 「影響」 line shown under story
    /// choices (「糧食 +60、銀兩 +30、河南府關係 +35」). Every number comes from the effect data itself, so changing
    /// prologue.json / questlines.json automatically changes what the player sees. Flags are hidden.
    /// </summary>
    internal static class EffectText
    {
        [Serializable] private sealed class DiscipleNames { public DiscipleData[] disciples; }

        private static Dictionary<string, string> people;

        /// <summary>Joins every visible effect: 「糧食 +60、銀兩 +30」. Empty when nothing is visible.</summary>
        internal static string Describe(IEnumerable<GameEffect> effects, GameState state = null, string separator = "、") =>
            string.Join(separator, (effects ?? Array.Empty<GameEffect>()).Select(effect => Describe(effect, state))
                .Where(text => !string.IsNullOrEmpty(text)));

        /// <summary>One effect as 「名稱 ±數值」, or "" for hidden bookkeeping such as story flags.</summary>
        internal static string Describe(GameEffect effect, GameState state = null)
        {
            if (effect == null) return "";
            float raw = effect.value;
            int value = Mathf.RoundToInt(raw);
            string signed = $"{value:+0;-0;0}";
            switch (effect.key)
            {
                case "silver": return $"銀兩 {signed}";
                case "food": return $"糧食 {signed}";
                case "reputation": return $"聲望 {signed}";
                case "apMax": return $"行動力上限 {signed}";
                case "recruitPoolSize": return $"招徒人選 {signed}";
                case "recruitCostPct": return $"招徒費用 {signed}%";
                case "buildCostPct": return $"建造費用 {signed}%";
                case "trainBonusPct": return $"修習進度 {signed}%";
                case "insightChance": return $"感悟機率 {raw * 100:+0;-0;0}%";
                case "loyaltyAll": return $"全門忠誠 {signed}";
                case "noGraveInjury": return value > 0 ? "不會重傷，也不致殞命" : "";
                case "relation": return $"{FactionName(effect.@ref)}關係 {signed}";
                case "charRelation": return $"{PersonName(effect.@ref, state)}好感 {signed}";
                case "rank":
                case "lineRank": return $"{LineName(effect.@ref)}階位 {signed}";
                case "cap": return $"{LineName(effect.@ref)}上限 {signed}";
                case "studyPct": return $"{StudyName(effect.@ref)}修習速度 {signed}%";
                case "art": return $"習得《{MartialArts.Find(effect.@ref)?.name ?? effect.@ref}》";
                case "followers":
                    string who = PersonName(effect.@ref, state);
                    return value > 0 ? $"{who}加入同門" : value < 0 ? $"{who}離開同門" : "";
                case "flag": return "";
                // v1124 C15–C18: realm / hall rewards.
                case "item": return $"{ItemName(effect.@ref)} ×{value}";
                case "gear": return $"{GameState.GearGrades[Mathf.Clamp(value, 0, GameState.GearGrades.Length - 1)]}{effect.@ref}一件";
                case "progress": return $"{LineName(effect.@ref)}修習進度 +{raw:0.#}";
                case "injury": return value > 0 ? $"主角傷勢 +{value}" : $"主角傷勢 {value}";
                case "heal": return "全門療傷";
                case "discipleProgress": return $"弟子武學進度 +{raw:0.#}";
            }
            int slot = AttributeSystem.Index(effect.key);
            return slot >= 0 ? $"{AttributeSystem.Names[slot]} {signed}" : "";
        }

        /// <summary>
        /// The 「影響」 line for a questline choice: its numeric effects, the change in the story character's
        /// favour, and — when nothing numeric changes — a 「劇情：……」 hint about where the story goes.
        /// </summary>
        internal static string Choice(QuestChoice choice, Questline line, GameState state = null)
        {
            if (choice == null) return "";
            var parts = new List<string>();
            string numbers = Describe(choice.effects, state);
            if (numbers.Length > 0) parts.Add(numbers);
            if (choice.relation != 0 && line != null)
                parts.Add($"{PersonName(line.character, state)}好感 {choice.relation:+0;-0}");
            if (parts.Count == 0 || !string.IsNullOrWhiteSpace(choice.hint))
                parts.Add("劇情：" + StoryHint(choice, line));
            return string.Join("、", parts);
        }

        private static string StoryHint(QuestChoice choice, Questline line)
        {
            if (!string.IsNullOrWhiteSpace(choice.hint)) return choice.hint.Trim();
            var next = line?.beats?.FirstOrDefault(beat => beat?.id == choice.next);
            if (!string.IsNullOrWhiteSpace(next?.title)) return $"引出「{next.title}」";
            return string.IsNullOrEmpty(choice.next) ? "此段故事告一段落" : "故事另有發展";
        }

        /// <summary>Data-driven difficulty badge for the four 門派之變 openings (1 = 易 … 4 = 極難).</summary>
        internal static string Difficulty(int level)
        {
            if (level <= 0) return "";
            level = Mathf.Clamp(level, 1, 4);
            string label = level switch { 1 => "易", 2 => "中", 3 => "難", _ => "極難" };
            return new string('★', level) + new string('☆', 4 - level) + "　" + label;
        }

        internal static string ItemName(string id) =>
            System.Array.Find(LegacySystem.Catalog, item => item.id == id)?.name ?? id switch
            {
                "item_jinchuang" => "金創靈藥", "item_jiuzhuan" => "九轉回元丹", _ => id
            };

        internal static string FactionName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "勢力";
            var faction = WorldMapData.Load().Factions.FirstOrDefault(entry => entry.id == id);
            return faction?.Label ?? id;
        }

        internal static string LineName(string id)
        {
            if (id == "martial") return "主修武學";
            return RankSystem.Categories.FirstOrDefault(category => category.id == id)?.name ?? "武學";
        }

        private static string StudyName(string id) => string.IsNullOrEmpty(id) ? "武學" : LineName(id);

        internal static string PersonName(string id, GameState state = null)
        {
            if (string.IsNullOrEmpty(id)) return "同門";
            if (id == "friend") id = state?.FriendId ?? "friend_f";
            var live = state?.Disciples?.FirstOrDefault(member => member?.id == id);
            if (live != null && !string.IsNullOrEmpty(live.name)) return live.name;
            if (people == null)
            {
                people = new Dictionary<string, string>();
                foreach (var person in M2Data.Load<CharacterCatalog>("characters").storyCharacters ?? Array.Empty<StoryCharacter>())
                    if (!string.IsNullOrEmpty(person?.id)) people[person.id] = person.name;
                foreach (var member in M2Data.Load<DiscipleNames>("disciples").disciples ?? Array.Empty<DiscipleData>())
                    if (!string.IsNullOrEmpty(member?.id)) people[member.id] = member.name;
            }
            if (id is "friend_m" or "friend_f" && state == null) return "故友";
            return people.TryGetValue(id, out var name) && !string.IsNullOrEmpty(name) ? name : id;
        }
    }
}
