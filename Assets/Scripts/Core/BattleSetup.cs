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
        public float dodge, hit, crit = AttributeSystem.BaseCrit;
        public float talentHit, talentCrit, talentReduction;
        public int talentBasicAttack, qiRegen, internalAttack;
        /// <summary>v1126e #1: 外防 takes outer strikes, 內防 takes inner-power strikes (BattleCatalog.IsInner).</summary>
        public int outerDefense, innerDefense, phase = 1, intentRound;
        /// <summary>Both defences at once (enemies and the sect-war roster read or set them together).</summary>
        public int defense
        {
            get => (outerDefense + innerDefense) / 2;
            set => outerDefense = innerDefense = value;
        }
        /// <summary>v1126e #1: the stronger of 外功攻擊 / 內功威力.</summary>
        public int Offense => Math.Max(attack, internalAttack);
        public string element, enemyProfile;
        public BattleSkill intent;
        public BattleCombatant intentTarget;
        /// <summary>v1124 C14: slot ("skill" / "ultimate") → battle_skills.json id from the arts this fighter knows.</summary>
        public Dictionary<string, string> moves = new();
        public string[] knownMoves = Array.Empty<string>();
        public bool personalArts;
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
        /// <summary>
        /// v1124 C15/C16: a named guardian fights as its own portrait standee; a beast names a Resources standee path
        /// (falls back to the generic boss art until P2 paints it). Escorts can carry their own names.
        /// </summary>
        public bool leadAsStandee, commanderDuel;
        public string enemyLeadStandee;
        public string[] minionNames;
        /// <summary>
        /// v1124 C17 同門切磋: the player side is exactly these fighters ("hero" or disciple ids) and the opposing side
        /// is built from <see cref="sparOpponents"/> with the same stat derivation — no bandits involved.
        /// </summary>
        public string[] sparParty, sparOpponents, expeditionParty;
        public bool IsSparring => sparParty is { Length: > 0 } && sparOpponents is { Length: > 0 };
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
        // v1126e #1: 內功威力 now shares 外功攻擊's base, so the sheet figure is the battle figure. Only the v1126d
        // before/after simulation still converts the old base-25 figure.
        private static int InnerAttack(int power) => AttributeSystem.LegacyV1126d
            ? Mathf.RoundToInt(power * (AttributeSystem.BaseAttack / (float)AttributeSystem.LegacyBaseQiPower))
            : power;

        /// <summary>
        /// v1126e #1: enemies scale from 外功 plus half of any 內功 surplus. v1126d read 外功 only, so a 內功 build met
        /// bosses with ~30% less 氣血 (BattleSimV1126e: 6.0 rounds against 7.2 for 外功); scaling on the full 內功 swung
        /// it the other way (8.6), because the inner arts carry no area or armour-breaking moves. A quarter (sweep 0 / .25 / .5)
        /// keeps every build within about ±10% win rate and ±1 round of v1126d without either pure build pulling ahead.
        /// </summary>
        internal static float InnerSurplusScaling = .25f;
        private static int Scaled(int attack, int inner) => AttributeSystem.LegacyV1126d ? attack
            : attack + Mathf.RoundToInt(InnerSurplusScaling * Math.Max(0, inner - attack));
        private static int Offense(BattleCombatant unit) => Scaled(unit.attack, unit.internalAttack);
        private static int Offense(DerivedStats stats) => Scaled(stats.Attack, stats.QiPower);

        public static (List<BattleCombatant> party, List<BattleCombatant> enemies) Create(GameState state, BattleRequest req, BattleCatalog catalog)
        {
            if (req.IsSparring)
                return Sparring(state, req, catalog);
            var stats = state.HeroStats;
            var players = new List<BattleCombatant>();
            var ids = req.expeditionParty ?? new[] { GameState.HeroId }
                .Concat(state.Disciples.Where(member => member != null).Take(2).Select(member => member.id)).ToArray();
            foreach (string id in ids)
            {
                if (id == GameState.HeroId) players.Add(Hero(state, catalog));
                else if (state.Disciples.Find(member => member?.id == id) is DiscipleData member)
                    players.Add(Member(state, member, catalog, true));
            }
            if (players.Count == 0) players.Add(Hero(state, catalog));

            // Build enemy squad
            var enemies = new List<BattleCombatant>();
            float diff = Mathf.Clamp(req.difficulty, 0.7f, 1.8f);
            // Budget about five rounds of focused damage for the boss, plus its escorts.
            // Attacks stay moderate so the party can spend those rounds fighting.
            float partyHp = (float)players.Average(unit => unit.maxHp);
            int bossHp = req.IsBoss
                ? Mathf.RoundToInt(players.Sum(Offense) * 7.9f * diff)
                : Mathf.RoundToInt(stats.MaxHp * 1.15f * diff);
            var leadStandee = string.IsNullOrEmpty(req.enemyLeadStandee) ? null : Resources.Load<Texture2D>(req.enemyLeadStandee);
            enemies.Add(new BattleCombatant
            {
                name = req.enemyLeadName,
                spriteId = leadStandee != null || req.leadAsStandee ? null : "enemy_boss",
                standee = leadStandee,
                portraitId = req.enemyLeadPortrait,
                gender = "男",
                isPlayer = false,
                level = 10 + Mathf.RoundToInt(diff * 5),
                hp = bossHp, maxHp = bossHp, qi = 100, maxQi = 100,
                attack = Mathf.RoundToInt((req.IsBoss ? partyHp * .135f : Offense(stats) * .72f) * diff),
                speed = Math.Max(10, stats.Speed - 3),
                defense = Math.Max(AttributeSystem.MinDefense, (int)players.Average(Offense) / 3),
                enemyProfile = req.IsBoss ? "boss" : "leader",
                dodge = 0.05f
            });
            string[] minionSprites = { "enemy_scout", "enemy_axe", "enemy_assassin" };
            string[] minionProfiles = { "scout", "axe", "assassin" };
            string[] minionNames = req.minionNames is { Length: > 0 } ? req.minionNames : new[] { "山賊斥候", "山寨悍匪", "黑衣刺客" };
            int count = Mathf.Clamp(req.enemyCount, 1, 4);
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
                    attack = Mathf.RoundToInt((req.IsBoss ? partyHp * .055f : Offense(stats) * .52f) * diff),
                    speed = Math.Max(10, stats.Speed - 4 + i),
                    defense = Math.Max(5, Offense(stats) / 5),
                    enemyProfile = minionProfiles[i - 1],
                    dodge = 0.06f
                });
            }

            foreach (var enemy in enemies) enemy.element = catalog.Enemies[enemy.enemyProfile].element;
            var leader = WorldMapData.Load().Factions.FirstOrDefault(faction => faction.PortraitId == req.enemyLeadPortrait);
            var known = (leader?.martialArts ?? Array.Empty<string>()).Select(MartialArts.Find).Where(art => art != null);
            enemies[0].knownMoves = MartialArts.KnownMoves(known, null, catalog.Skills.ContainsKey);
            enemies[0].personalArts = req.commanderDuel && enemies[0].knownMoves.Length > 0;
            return (players, enemies);
        }

        /// <summary>v1124 C17: a bout between fellow disciples — both sides use their own derived stats and arts.</summary>
        private static (List<BattleCombatant>, List<BattleCombatant>) Sparring(GameState state, BattleRequest req,
            BattleCatalog catalog)
        {
            BattleCombatant Build(string id, bool ally)
            {
                if (id == "hero")
                {
                    var hero = Hero(state, catalog);
                    hero.isPlayer = ally;
                    return hero;
                }
                var member = state.Disciples.FirstOrDefault(d => d?.id == id);
                return member == null ? null : Member(state, member, catalog, ally);
            }
            var players = req.sparParty.Select(id => Build(id, true)).Where(unit => unit != null).ToList();
            var enemies = req.sparOpponents.Select(id => Build(id, false)).Where(unit => unit != null).ToList();
            string[] profiles = { "leader", "scout", "axe", "assassin" };
            for (int i = 0; i < enemies.Count; i++)
            {
                enemies[i].enemyProfile = profiles[i % profiles.Length];
                enemies[i].element = catalog.Enemies[enemies[i].enemyProfile].element;
            }
            return (players, enemies);
        }

        private static BattleCombatant Hero(GameState state, BattleCatalog catalog)
        {
            var stats = state.HeroStats;
            return state.ApplyBattleTalents(GameState.HeroId, new BattleCombatant
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
                outerDefense = stats.OuterDefense, innerDefense = stats.InnerDefense,
                element = "木",
                dodge = stats.Dodge,
                hit = stats.Hit, crit = stats.Crit, qiRegen = stats.QiRegen,
                internalAttack = InnerAttack(stats.QiPower),
                personalArts = true,
                knownMoves = MartialArts.KnownMoves(state.ProtagonistArts, state.Protagonist.major, catalog.Skills.ContainsKey),
                moves = MartialArts.Moves(state.ProtagonistArts,
                    RankSystem.Major(state.Protagonist.lines, state.Protagonist.major)?.id, catalog.Skills.ContainsKey)
            });
        }

        private static BattleCombatant Member(GameState state, DiscipleData d, BattleCatalog catalog, bool ally)
        {
            var lines = d.lines is { Length: > 0 }
                ? d.lines
                : RankSystem.Seed(null, d.rank, d.cap, RankSystem.BaseCap, RankSystem.BaseRank);
            var derived = AttributeSystem.Derive(state.DiscipleAttributes(d), lines, state.WeaponRankDelta(d.weapon));
            return state.ApplyBattleTalents(d.id, new BattleCombatant
            {
                name = d.name,
                spriteId = null,
                portraitId = PortraitService.IdOrDefault(d.portrait, d.id),
                gender = d.gender,
                isPlayer = ally,
                level = 8 + state.Year,
                hp = derived.MaxHp, maxHp = derived.MaxHp, qi = derived.MaxQi, maxQi = derived.MaxQi,
                attack = derived.Attack,
                speed = derived.Speed,
                outerDefense = derived.OuterDefense, innerDefense = derived.InnerDefense,
                element = "水",
                dodge = derived.Dodge,
                hit = derived.Hit, crit = derived.Crit, qiRegen = derived.QiRegen,
                internalAttack = InnerAttack(derived.QiPower),
                personalArts = true,
                knownMoves = MartialArts.KnownMoves(state.ArtsOf(d), RankSystem.BestCombat(lines)?.id, catalog.Skills.ContainsKey),
                moves = MartialArts.Moves(state.ArtsOf(d), RankSystem.BestCombat(lines)?.id, catalog.Skills.ContainsKey)
            });
        }
    }
}
