using System;
using System.Linq;

namespace Wuxia
{
    /// <summary>v1126 PA (介面與台詞): the 客將 label, the 與本門淵源 texts and the talent list on the dossier.</summary>
    public sealed partial class GameBootstrap
    {
        /// <summary>v1126 #16: the friend keeps their own sect; the name is one data field (quests.json guestSect).</summary>
        private static string GuestSect => QuestBoardData.Data.guestSect;
        private static string GuestLabel => $"{QuestBoardData.Data.guestSect}・{QuestBoardData.Data.guestRole}";

        /// <summary>Known martial lines, best first (「劍 B　拳掌 C」), for the roster cards.</summary>
        private static string CombatSummary(SkillLine[] lines, int count)
        {
            var known = (lines ?? Array.Empty<SkillLine>()).Where(line => line != null && !string.IsNullOrEmpty(line.id) &&
                    RankSystem.IsCombat(line.id) && RankSystem.Index(line.rank) >= 0)
                .OrderByDescending(line => RankSystem.Index(line.rank)).Take(count)
                .Select(line => (RankSystem.Categories.FirstOrDefault(category => category.id == line.id)?.name ?? line.id) +
                    " " + line.rank).ToArray();
            return known.Length == 0 ? "武學未習" : "武　" + string.Join("　", known);
        }

        /// <summary>
        /// v1126 #18 (T-3 #14/#15/#18): the real tie to the sect for the hero, the friend, the elders and our own
        /// disciples (quests.json bonds). Null for outsiders, who keep their faction line.
        /// </summary>
        private string SectBond(StoryCharacter person)
        {
            var bonds = QuestBoardData.Data.bonds ?? new QuestBonds();
            if (person.id == HeroCharacterId)
            {
                string background = M2Data.Load<CharacterCatalog>("characters").backgrounds?
                    .FirstOrDefault(option => option?.id == state.Protagonist.backgroundId)?.name;
                return string.IsNullOrEmpty(background) ? bonds.heroNoBackground : (bonds.hero ?? "").Replace("{background}", background);
            }
            if (person.id == state.FriendId)
                return (bonds.friend ?? "").Replace("{friend_he}", person.gender == "女" ? "她" : "他")
                    .Replace("{guestSect}", GuestSect).Replace("{friend}", person.name);
            if (!string.IsNullOrWhiteSpace(person.relation)) return null;
            if (person.id == "master") return bonds.master;
            if (person.id == "uncle_schemer") return bonds.uncle_schemer;
            if (state.Disciples.Any(member => member.id == person.id))
                return state.Data.Disciples.Any(seed => seed.id == person.id) ? bonds.disciple : bonds.recruit;
            return null;
        }

        /// <summary>v1126 #18: talents by name with what they do (「【根骨奇佳】根骨 +2」), one per line.</summary>
        private static string TalentSummary(string[] ids)
        {
            var catalog = M2Data.Load<TalentCatalog>("talents").talents ?? Array.Empty<CreationOption>();
            var lines = (ids ?? Array.Empty<string>()).Distinct()
                .Select(id => catalog.FirstOrDefault(option => option?.id == id)).Where(option => option != null)
                .Select(option =>
                {
                    string effect = option.modifiers?.Length > 0 ? option.desc : EffectText.Describe(option.effects);
                    return string.IsNullOrEmpty(effect) ? $"【{option.name}】" : $"【{option.name}】{effect}";
                }).ToArray();
            return lines.Length == 0 ? null : string.Join("\n", lines);
        }
    }
}
