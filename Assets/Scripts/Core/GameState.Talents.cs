using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    internal static class TalentRules
    {
        private static readonly Dictionary<string, CreationOption> Options =
            (M2Data.Load<TalentCatalog>("talents").talents ?? Array.Empty<CreationOption>())
            .Where(option => option != null).ToDictionary(option => option.id);

        internal static bool Compatible(CreationOption option, IEnumerable<CreationOption> selected) =>
            !selected.Any(previous =>
                (option.rarity == "legendary" && previous.rarity == "legendary") ||
                (!string.IsNullOrEmpty(option.exclusive) && option.exclusive == previous.exclusive));

        internal static float Sum(IEnumerable<string> ids, string key, float cap) => Mathf.Min(cap,
            ids.Distinct().SelectMany(id => Options.TryGetValue(id, out var option) ?
                option.modifiers ?? Array.Empty<GameEffect>() : Array.Empty<GameEffect>())
                .Where(effect => effect.key == key).Sum(effect => effect.value));

        internal static readonly string[] Innate = { "late_bloomer", "perfect_recall", "handy_builder" };

        private static readonly Dictionary<string, string[]> Signature = new()
        {
            ["master"] = new[] { "diamond_body", "patient_teacher" },
            ["uncle_schemer"] = new[] { "keen_eye", "broad_qi" },
            ["lin"] = new[] { "heavy_force", "iron_shirt" },
            ["xu"] = new[] { "patient_teacher", "trade_skill" },
            ["friend_m"] = new[] { "needle_gap", "sure_promise", "travel_light" },
            ["friend_f"] = new[] { "needle_gap", "sure_promise", "travel_light" },
            ["luo"] = new[] { "swift_feet", "inspect_iron" },
            ["du"] = new[] { "stone_body", "handy_builder" },
            ["han"] = new[] { "weighted_strike", "read_harvest" },
            ["kang"] = new[] { "trade_skill" },
            ["pei"] = new[] { "keen_eye" },
            ["shen"] = new[] { "side_step" },
            ["song"] = new[] { "broad_qi" },
            ["wei"] = new[] { "brute_force" },
            ["cui"] = new[] { "sure_promise" },
            ["qiao"] = new[] { "pure_forge" }
        };

        private static readonly string[] Temperaments = { "keen_eye", "side_step", "swift_feet",
            "stone_body", "weighted_strike", "heavy_force", "wind_speed", "needle_gap", "iron_shirt", "broad_qi" };

        internal static string[] Fixed(string id)
        {
            if (string.IsNullOrEmpty(id) || id.StartsWith("recruit_", StringComparison.Ordinal))
                return Array.Empty<string>();
            if (Signature.TryGetValue(id, out string[] talents)) return talents;
            uint index = 0;
            foreach (char letter in id) index = unchecked(index * 31 + letter);
            return new[] { Temperaments[index % Temperaments.Length] };
        }
    }

    public sealed partial class GameState
    {
        internal string[] TalentsOf(string id)
        {
            if (id == HeroId)
                return (Protagonist.talents ?? Array.Empty<string>())
                    .Concat(TalentRules.Innate).Distinct().ToArray();
            var acquired = Disciples.Find(member => member.id == id)?.talents ?? Array.Empty<string>();
            return acquired.Concat(TalentRules.Fixed(id)).Distinct().ToArray();
        }

        internal float TalentValue(string id, string key, float cap) => TalentRules.Sum(TalentsOf(id), key, cap);

        internal BattleCombatant ApplyBattleTalents(string id, BattleCombatant unit)
        {
            unit.attack = Mathf.RoundToInt((unit.attack + TalentValue(id, "attack", 12)) *
                (1 + TalentValue(id, "attackPct", 20) / 100f));
            unit.speed = Mathf.RoundToInt((unit.speed + TalentValue(id, "speed", 8)) *
                (1 + TalentValue(id, "speedPct", 20) / 100f));
            unit.hp = unit.maxHp += Mathf.RoundToInt(TalentValue(id, "hp", 40));
            unit.qi = unit.maxQi = Mathf.RoundToInt(unit.maxQi * (1 + TalentValue(id, "qiPct", 20) / 100f));
            unit.defense += Mathf.RoundToInt(TalentValue(id, "defense", 10));
            unit.dodge += TalentValue(id, "dodge", 12) / 100f;
            unit.talentHit = TalentValue(id, "hit", 12) / 100f;
            unit.talentCrit = TalentValue(id, "crit", 12) / 100f;
            unit.talentReduction = TalentValue(id, "reduction", 20) / 100f;
            unit.talentBasicAttack = Mathf.RoundToInt(TalentValue(id, "basicAttack", 8));
            return unit;
        }
    }
}
