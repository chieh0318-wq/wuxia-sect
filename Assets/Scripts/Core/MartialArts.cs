using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    /// <summary>One entry of martial_arts.json (v1124 C14). Creation arts share their id with skills.json.</summary>
    [Serializable] internal sealed class MartialArt
    {
        public string id, name, category, tier, source, how, slot, desc, move;
        public string[] holders;
        public string attribute;
        public int attributeBonus;
        internal bool Combat => RankSystem.IsCombat(category);
    }

    [Serializable] internal sealed class MartialArtCatalog { public string note; public MartialArt[] arts; }

    /// <summary>
    /// v1124 C14: every martial art in the game — the creation picks plus the far larger set only reachable through
    /// 師承、秘笈、奇遇、秘境 and faction masters. Also picks the battle moves a character fights with.
    /// </summary>
    internal static class MartialArts
    {
        private static MartialArt[] all;
        internal static MartialArt[] All => all ??= (M2Data.Load<MartialArtCatalog>("martial_arts").arts ?? Array.Empty<MartialArt>())
            .Where(art => art != null && !string.IsNullOrEmpty(art.id)).ToArray();

        internal static MartialArt Find(string idOrName) => string.IsNullOrEmpty(idOrName) ? null :
            All.FirstOrDefault(art => art.id == idOrName) ?? All.FirstOrDefault(art => art.name == idOrName);

        /// <summary>
        /// Battle slot overrides ("skill" / "ultimate" → battle_skills.json id) for a set of known arts: the
        /// highest-tier art on the preferred line wins each slot, then the highest-tier art on any combat line.
        /// </summary>
        internal static Dictionary<string, string> Moves(IEnumerable<MartialArt> known, string preferredLine,
            Func<string, bool> exists)
        {
            var moves = new Dictionary<string, string>();
            var usable = (known ?? Array.Empty<MartialArt>()).Where(art => art != null && art.Combat &&
                !string.IsNullOrEmpty(art.move) && exists(art.move)).ToArray();
            foreach (string slot in new[] { "skill", "ultimate" })
            {
                var pick = usable.Where(art => (art.slot ?? "skill") == slot)
                    .OrderByDescending(art => art.category == preferredLine)
                    .ThenByDescending(art => RankSystem.Order(art.tier)).FirstOrDefault();
                if (pick != null) moves[slot] = pick.move;
            }
            return moves;
        }
    }

    public sealed partial class GameState
    {
        /// <summary>Arts the protagonist knows: creation picks plus everything learned in play.</summary>
        internal IEnumerable<MartialArt> ProtagonistArts =>
            (Protagonist.skills ?? Array.Empty<string>()).Concat(Protagonist.arts ?? Array.Empty<string>())
            .Distinct().Select(MartialArts.Find).Where(art => art != null);

        /// <summary>A disciple's arts; authored disciples always use the current data (old saves keep stale lists).</summary>
        internal IEnumerable<MartialArt> ArtsOf(DiscipleData member) =>
            (Data.Disciples.FirstOrDefault(seed => seed.id == member?.id)?.signatureSkills ?? member?.signatureSkills
                ?? Array.Empty<string>()).Select(MartialArts.Find).Where(art => art != null);

        internal bool KnowsArt(string id) => ProtagonistArts.Any(art => art.id == id);

        /// <summary>Adds an art taught by a breakthrough (the breakthrough already moved the cap): 「，習得《…》」.</summary>
        internal string RecordArt(string id)
        {
            var art = MartialArts.Find(id);
            if (art == null || KnowsArt(art.id)) return "";
            Protagonist.arts = (Protagonist.arts ?? Array.Empty<string>()).Append(art.id).ToArray();
            return $"，習得《{art.name}》";
        }

        /// <summary>
        /// v1124 C14/C13: learns an art in play (秘笈、奇遇、秘境、師承). The art's line gains a rank and its cap may
        /// rise by one tier, including when the current potential already matches the art's tier.
        /// Returns a result line, or "" when the art is unknown or already learned.
        /// </summary>
        internal string LearnArt(string id)
        {
            var art = MartialArts.Find(id);
            if (art == null || KnowsArt(art.id)) return "";
            Protagonist.arts = (Protagonist.arts ?? Array.Empty<string>()).Append(art.id).ToArray();
            var line = Array.Find(Protagonist.lines ?? Array.Empty<SkillLine>(), entry => entry?.id == art.category);
            if (line == null) return $"習得《{art.name}》";
            RankSystem.Shift(line, 1, true);
            if (RankSystem.Index(line.rank) < RankSystem.Index(line.cap))
                RankSystem.Shift(line, 1, false);
            else
                line.progress += 1f;
            return $"習得《{art.name}》：{EffectText.LineName(art.category)} {line.rank}／{line.cap}";
        }
    }
}
