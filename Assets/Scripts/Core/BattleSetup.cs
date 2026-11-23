using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    internal sealed class BattleCombatant
    {
        public string name, spriteId, portraitId, gender;
        public Texture2D standee;
        public bool isPlayer;
        public int level, hp, maxHp, qi, maxQi, attack, speed;
        public float dodge;
        public int defense, phase = 1, intentRound;
        public string element, enemyProfile;
        public BattleSkill intent;
        public BattleCombatant intentTarget;
        /// <summary>v1124 C14: slot ("skill" / "ultimate") → battle_skills.json id from the arts this fighter knows.</summary>
        public Dictionary<string, string> moves = new();
        public readonly Dictionary<string, int> statuses = new();
        public bool Alive => hp > 0;
    }

    internal enum BattleEncounter
    {
        Normal,
        Boss
    }

    internal enum BattleGround { Road, MountainGate, Fortification }

    internal sealed class BattleRequest
    {
        public BattleEncounter encounter;
        public BattleGround ground;
        public string enemyLeadPortrait = "npc_am01";
        public string enemyLeadName = "山賊頭目";
        public int enemyCount = 3;
        public float difficulty = 1.0f;
        public Action<bool> onComplete;
        public bool IsBoss => encounter == BattleEncounter.Boss;
        public string MusicCue => IsBoss ? "boss" : "battle";

        public static BattleRequest MapAssault(string factionName, string leaderName, string leaderPortrait,
            int garrison, float guard) => new()
        {
            encounter = guard >= .9f ? BattleEncounter.Boss : BattleEncounter.Normal,
            enemyLeadName = guard >= .9f && !string.IsNullOrWhiteSpace(leaderName) ? leaderName : factionName + "頭目",
            enemyLeadPortrait = guard >= .9f ? leaderPortrait : "npc_am01",
            enemyCount = garrison >= 45 ? 4 : 3,
            difficulty = Mathf.Clamp(guard + .15f, .8f, 1.4f)
        };
    }

    // Encounter construction preserves the stats derived from each party member.
    internal static class BattleSetup
    {
        public static (List<BattleCombatant> party, List<BattleCombatant> enemies) Create(GameState state, BattleRequest req, BattleCatalog catalog)
        {
            var stats = state.HeroStats;
            var players = new List<BattleCombatant>
            {
                new()
                {
                    // v1124 B10/A5: the battle HUD names the protagonist, never 「三師兄／三師姐」.
                    name = !string.IsNullOrWhiteSpace(state.Protagonist.name) ? state.Protagonist.name
                        : state.Protagonist.title,
                    standee = CharacterFactory.HeroOriginStandee(state.Protagonist.gender,
                        state.Protagonist.lookId ?? state.Protagonist.portrait,
                        state.Protagonist.backgroundId, state.Protagonist.title),
                    portraitId = state.Protagonist.portrait ?? state.Protagonist.lookId,
                    gender = state.Protagonist.gender,
                    isPlayer = true,
                    level = 10 + state.Year * 2 + state.TechCount,
                    hp = stats.MaxHp, maxHp = stats.MaxHp,
                    qi = stats.MaxQi, maxQi = stats.MaxQi,
                    attack = Math.Max(28, stats.Attack),
                    speed = Math.Max(15, stats.Speed),
                    defense = Math.Max(8, stats.Attack / 3),
                    element = "木",
                    dodge = stats.Dodge,
                    moves = MartialArts.Moves(state.ProtagonistArts,
                        RankSystem.Major(state.Protagonist.lines, state.Protagonist.major)?.id, catalog.Skills.ContainsKey)
                }
            };
            foreach (var d in state.Disciples.Where(member => member != null).Take(2))
            {
                var lines = d.lines is { Length: > 0 }
                    ? d.lines
                    : RankSystem.Seed(null, d.rank, d.cap, RankSystem.BaseCap, RankSystem.BaseRank);
                var derived = AttributeSystem.Derive(d.attrs, lines, state.WeaponRankDelta(d.weapon));
                players.Add(new BattleCombatant
                {
                    name = d.name,
                    spriteId = null,
                    portraitId = PortraitService.IdOrDefault(d.portrait, d.id),
                    gender = d.gender,
                    isPlayer = true,
                    level = 8 + state.Year,
                    hp = derived.MaxHp, maxHp = derived.MaxHp, qi = derived.MaxQi, maxQi = derived.MaxQi,
                    attack = derived.Attack,
                    speed = derived.Speed,
                    defense = Math.Max(8, derived.Attack / 3),
                    element = "水",
                    dodge = derived.Dodge,
                    moves = MartialArts.Moves(state.ArtsOf(d), RankSystem.BestCombat(lines)?.id, catalog.Skills.ContainsKey)
                });
            }

            // Build enemy squad
            var enemies = new List<BattleCombatant>();
            float diff = Mathf.Clamp(req.difficulty, 0.7f, 1.8f);
            // Budget about five rounds of focused damage for the boss, plus its escorts.
            // Attacks stay moderate so the party can spend those rounds fighting.
            float partyHp = (float)players.Average(unit => unit.maxHp);
            int bossHp = req.IsBoss
                ? Mathf.RoundToInt(players.Sum(unit => unit.attack) * 7.9f * diff)
                : Mathf.RoundToInt(stats.MaxHp * 1.15f * diff);
            enemies.Add(new BattleCombatant
            {
                name = req.enemyLeadName,
                spriteId = "enemy_boss",
                portraitId = req.enemyLeadPortrait,
                gender = "男",
                isPlayer = false,
                level = 10 + Mathf.RoundToInt(diff * 5),
                hp = bossHp, maxHp = bossHp, qi = 100, maxQi = 100,
                attack = Mathf.RoundToInt((req.IsBoss ? partyHp * .135f : stats.Attack * .72f) * diff),
                speed = Math.Max(10, stats.Speed - 3),
                defense = Math.Max(8, (int)players.Average(unit => unit.attack) / 3),
                enemyProfile = req.IsBoss ? "boss" : "leader",
                dodge = 0.05f
            });
            string[] minionSprites = { "enemy_scout", "enemy_axe", "enemy_assassin" };
            string[] minionProfiles = { "scout", "axe", "assassin" };
            string[] minionNames = { "山賊斥候", "山寨悍匪", "黑衣刺客" };
            int count = Mathf.Clamp(req.enemyCount, 2, 4);
            for (int i = 1; i < count; i++)
            {
                int mHp = Mathf.RoundToInt(stats.MaxHp * 0.58f * diff);
                enemies.Add(new BattleCombatant
                {
                    name = minionNames[(i - 1) % minionNames.Length],
                    spriteId = minionSprites[(i - 1) % minionSprites.Length],
                    portraitId = "npc_am02",
                    gender = "男",
                    isPlayer = false,
                    level = 8 + Mathf.RoundToInt(diff * 4),
                    hp = mHp, maxHp = mHp, qi = 60, maxQi = 60,
                    attack = Mathf.RoundToInt((req.IsBoss ? partyHp * .055f : stats.Attack * .52f) * diff),
                    speed = Math.Max(10, stats.Speed - 4 + i),
                    defense = Math.Max(5, stats.Attack / 5),
                    enemyProfile = minionProfiles[i - 1],
                    dodge = 0.06f
                });
            }

            foreach (var enemy in enemies) enemy.element = catalog.Enemies[enemy.enemyProfile].element;
            return (players, enemies);
        }
    }
}
