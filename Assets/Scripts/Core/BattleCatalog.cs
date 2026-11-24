using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    internal sealed class BattleCatalog
    {
        [Serializable]
        private sealed class SkillTable { public BattleSkill[] entries; }
        [Serializable]
        private sealed class StatusTable { public BattleStatus[] entries; }
        [Serializable]
        private sealed class EnemyTable { public BattleEnemy[] entries; }

        public readonly Dictionary<string, BattleSkill> Skills =
            Load<SkillTable>("battle_skills").entries.ToDictionary(entry => entry.id);
        public readonly Dictionary<string, BattleStatus> Statuses =
            Load<StatusTable>("battle_statuses").entries.ToDictionary(entry => entry.id);
        public readonly Dictionary<string, BattleEnemy> Enemies =
            Load<EnemyTable>("battle_enemies").entries.ToDictionary(entry => entry.id);

        internal readonly HashSet<string> InternalMoves = new(MartialArts.KnownMoves(
            MartialArts.All.Where(art => art.category == "neigong"), "neigong", _ => true));

        private static T Load<T>(string name) =>
            JsonUtility.FromJson<T>(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Data", name + ".json")));
    }
}
