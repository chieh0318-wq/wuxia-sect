using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    // ---- realms.json (v1124 C15 / C16) ---------------------------------------------------------------

    [Serializable] internal sealed class RealmScene { public string id, fallback, caption; }

    [Serializable] internal sealed class RealmSpeaker { public string name, subtitle, portrait, gender, standee; }

    [Serializable] internal sealed class RealmLine { public string speaker, text; }

    /// <summary>A battle that a realm choice or a treasure guardian can start (built into a <see cref="BattleRequest"/>).</summary>
    [Serializable] internal sealed class RealmBattle
    {
        public string lead, portrait, standee;
        public string[] minions;
        public int count = 3;
        public float difficulty = 1;
        public bool boss;
    }

    /// <summary>One answer in a road encounter: an attribute check, a battle, or a plain choice.</summary>
    [Serializable] internal sealed class RoadChoice
    {
        public string label, type, attr, text, success, failure;
        public Cost cost;
        public RealmBattle battle;
        public GameEffect[] successEffects, failureEffects;
    }

    [Serializable] internal sealed class RoadEvent
    {
        public string id, title, scene;
        public int weight = 10;
        public RealmSpeaker speaker;
        public RealmLine[] lines;
        public RoadChoice[] choices;
    }

    [Serializable] internal sealed class TreasureGuardian
    {
        public string name, subtitle, portrait, standee, gender, win, lose;
        public string[] minions;
        public int count = 2;
        public float difficulty = 1.3f;
        public bool boss = true;
        public RealmLine[] lines;
    }

    /// <summary>A fixed, one-time 寶物秘境 on the world map (C16).</summary>
    [Serializable] internal sealed class TreasureSite
    {
        public string id, name, node, kind, treasureType, treasure, scene, intro;
        public float dx = 24, dy = -30;
        public RealmStage[] stages;
        public TreasureGuardian guardian;
        public GameEffect[] rewards;
        internal bool Guarded => kind == "guarded" && guardian != null;
        internal string KindLabel => Guarded ? "高手鎮守" : "探索";
        internal string TypeLabel => treasureType switch
        {
            "manual" => "秘笈", "weapon" => "神兵", "pill" => "丹藥", "chance" => "突破機緣", _ => "寶物"
        };
        internal string TypeIcon => treasureType switch
        {
            "manual" => "item_manual_sword", "weapon" => "item_sword", "pill" => "item_pill_hp", "chance" => "item_manual_qi",
            _ => "item_trinket"
        };
    }

    [Serializable] internal sealed class RealmCatalog
    {
        public RealmScene[] scenes;
        public RoadEvent[] roadEvents;
        public TreasureSite[] treasures;
    }

    internal static class RealmContent
    {
        private static RealmCatalog data;
        internal static RealmCatalog Data => data ??= Normalize(M2Data.Load<RealmCatalog>("realms"));

        private static RealmCatalog Normalize(RealmCatalog catalog)
        {
            catalog.scenes ??= Array.Empty<RealmScene>();
            catalog.roadEvents = (catalog.roadEvents ?? Array.Empty<RoadEvent>())
                .Where(entry => entry != null && entry.choices is { Length: > 0 }).ToArray();
            catalog.treasures = (catalog.treasures ?? Array.Empty<TreasureSite>())
                .Where(entry => entry != null && !string.IsNullOrEmpty(entry.id)).ToArray();
            foreach (var site in catalog.treasures)
            {
                site.stages ??= Array.Empty<RealmStage>();
                site.rewards ??= Array.Empty<GameEffect>();
            }
            return catalog;
        }

        internal static RealmScene Scene(string id) =>
            Data.scenes.FirstOrDefault(scene => scene.id == id) ?? new RealmScene { id = id, fallback = "Story/cg_ruins" };

        /// <summary>Background for a scene: the painted Resources/Realms/&lt;id&gt; when present, else the stand-in.</summary>
        internal static Texture2D Background(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var scene = Scene(id);
            return Resources.Load<Texture2D>("Realms/" + id) ??
                (string.IsNullOrEmpty(scene.fallback) ? null : Resources.Load<Texture2D>(scene.fallback));
        }

        internal static TreasureSite Treasure(string id) => Data.treasures.FirstOrDefault(site => site.id == id);

        internal static RoadEvent PickRoadEvent()
        {
            var pool = Data.roadEvents;
            if (pool.Length == 0) return null;
            int total = pool.Sum(entry => Mathf.Max(1, entry.weight));
            int roll = UnityEngine.Random.Range(0, total);
            foreach (var entry in pool)
            {
                roll -= Mathf.Max(1, entry.weight);
                if (roll < 0) return entry;
            }
            return pool[0];
        }

        /// <summary>The battle request a realm fight starts: named lead and escorts, optional beast standee.</summary>
        internal static BattleRequest Battle(RealmBattle fight, BattleGround ground) => new()
        {
            encounter = fight.boss ? BattleEncounter.Boss : BattleEncounter.Normal,
            ground = ground,
            enemyLeadName = fight.lead,
            enemyLeadPortrait = string.IsNullOrEmpty(fight.portrait) ? "npc_am01" : fight.portrait,
            enemyLeadStandee = fight.standee,
            leadAsStandee = string.IsNullOrEmpty(fight.standee),
            minionNames = fight.minions,
            enemyCount = Mathf.Clamp(fight.count, 1, 4),
            difficulty = fight.difficulty
        };

        internal static BattleRequest Battle(TreasureGuardian guardian) => Battle(new RealmBattle
        {
            lead = guardian.name, portrait = guardian.portrait, standee = guardian.standee, minions = guardian.minions,
            count = guardian.count, difficulty = guardian.difficulty, boss = guardian.boss
        }, BattleGround.MountainGate);
    }

    /// <summary>An in-progress visit to a fixed treasure realm.</summary>
    internal sealed class TreasureRun
    {
        public TreasureSite site;
        public int stage, score;
        public readonly List<string> log = new();
    }

    public sealed partial class GameState
    {
        // ---- Treasure realms (C16) ------------------------------------------------------------------

        private static string TreasureFlag(string id) => "treasure_" + id;

        internal bool TreasureClaimed(string id) => ValueOf(Story.flags ?? Array.Empty<StoryValue>(), TreasureFlag(id)) > 0;

        internal int TreasuresClaimed => RealmContent.Data.treasures.Count(site => TreasureClaimed(site.id));

        /// <summary>Why the party cannot enter a treasure realm right now, or null when it can (1 AP).</summary>
        internal string TreasureBlock(TreasureSite site)
        {
            if (site == null) return "此處沒有秘境。";
            if (TreasureClaimed(site.id)) return "秘寶已經取走。";
            if (WorldMapData.Load().Route(StartNode, site.node) == null && StartNode != site.node) return "沒有可通的道路。";
            if (ActionPoints < 1) return "行動力不足（需 1 點）。";
            if (Protagonist.injury >= 6) return "主角傷勢太重，先回山養傷。";
            return null;
        }

        /// <summary>Scene check odds for a treasure-realm floor (same curve as the random secret realms).</summary>
        internal float TreasureChance(RealmChoice choice, int stage, string worker = HeroId) => SecretRealms.Chance(this, choice, stage, worker);

        internal bool ResolveTreasureStage(TreasureRun run, RealmChoice choice, string worker = HeroId)
        {
            bool success = UnityEngine.Random.value < TreasureChance(choice, run.stage, worker);
            if (success) run.score++;
            else InjureWorker(worker);
            run.log.Add($"{choice.label}（{SecretRealms.AttrName(choice.attr)}）{(success ? "成功" : "失手")}：" +
                (success ? choice.success : choice.failure));
            run.stage++;
            return success;
        }

        /// <summary>Marks the site taken and grants its rewards; returns the reward lines.</summary>
        internal List<string> ClaimTreasure(TreasureSite site, string worker = HeroId)
        {
            var lines = GrantRealmRewards(site.rewards, worker);
            SetValue(ref Story.flags, TreasureFlag(site.id), 1, false);
            Story.realmsCleared++;
            return lines;
        }

        // ---- Road encounters (C15 山道巡訪) ------------------------------------------------------------

        internal string RoadChoiceBlock(RoadChoice choice) =>
            choice.cost != null && !CanPay(choice.cost) ? "銀兩或糧食不足" : null;

        /// <summary>Odds of a check choice (attribute based, like a first-floor realm check).</summary>
        internal float RoadChance(RoadChoice choice, string worker = HeroId) =>
            SecretRealms.Chance(this, new RealmChoice { attr = choice.attr }, 0, worker);

        /// <summary>The 「影響」 line for a road choice: costs, success effects and (for checks / fights) the failure.</summary>
        internal string RoadImpact(RoadChoice choice)
        {
            var parts = new List<string>();
            if (choice.cost != null && (choice.cost.silver > 0 || choice.cost.food > 0))
                parts.Add("花費 " + string.Join("、", new[]
                {
                    choice.cost.silver > 0 ? $"銀兩 {choice.cost.silver}" : null, choice.cost.food > 0 ? $"糧食 {choice.cost.food}" : null
                }.Where(text => text != null)));
            string win = EffectText.Describe(choice.successEffects, this);
            string lose = EffectText.Describe(choice.failureEffects, this);
            if (choice.type == "plain")
                parts.Add(win.Length > 0 ? win : "劇情：無數值變化");
            else
            {
                parts.Add((choice.type == "battle" ? "勝：" : "成：") + (win.Length > 0 ? win : "無"));
                parts.Add((choice.type == "battle" ? "敗：" : "敗：") + (lose.Length > 0 ? lose : "無"));
            }
            return string.Join("　", parts);
        }

        /// <summary>Applies a road outcome (cost already paid) and returns the reward lines.</summary>
        internal List<string> ResolveRoad(RoadChoice choice, bool success, string worker = HeroId) =>
            GrantRealmRewards(success ? choice.successEffects : choice.failureEffects, worker);

        /// <summary>Applies reward effects one by one and returns one readable line per effect.</summary>
        internal List<string> GrantRealmRewards(IEnumerable<GameEffect> effects, string worker = HeroId)
        {
            var lines = new List<string>();
            foreach (var effect in effects ?? Array.Empty<GameEffect>())
            {
                if (effect == null) continue;
                string text = effect.key switch
                {
                    "art" => worker == HeroId ? LearnArt(effect.@ref)
                        : LearnDiscipleArt(Disciples.Find(member => member.id == worker), effect.@ref, sectInstruction: false),
                    "gear" => EquipOrStore(MakeGear(effect.@ref, Mathf.RoundToInt(effect.value))),
                    "cap" or "rank" or "lineRank" => CapLine(effect),
                    _ => EffectText.Describe(effect, this)
                };
                if (effect.key == "injury" && worker != HeroId &&
                    Disciples.Find(member => member.id == worker) is DiscipleData member)
                    member.injury = Mathf.Clamp(member.injury + Mathf.RoundToInt(effect.value), 0, 9);
                else if (effect.key is not ("gear" or "art" or "cap" or "rank" or "lineRank"))
                    ApplyEffects(new[] { effect });
                if (effect.key == "art" && worker == HeroId && string.IsNullOrEmpty(text))
                {
                    // Already known: the manual still deepens its line.
                    StudyLine(MartialArts.Find(effect.@ref)?.category, 1.5f);
                    text = $"《{MartialArts.Find(effect.@ref)?.name ?? effect.@ref}》早已習得，溫故知新：修習進度 +1.5";
                }
                if (!string.IsNullOrEmpty(text)) lines.Add(text);
            }
            return lines;
        }

        private string CapLine(GameEffect effect)
        {
            ApplyEffects(new[] { effect });
            var line = effect.@ref == "martial" ? RankSystem.Major(Protagonist.lines, Protagonist.major)
                : Array.Find(Protagonist.lines ?? Array.Empty<SkillLine>(), entry => entry.id == effect.@ref);
            string name = line == null ? EffectText.LineName(effect.@ref)
                : RankSystem.Categories.FirstOrDefault(category => category.id == line.id)?.name ?? EffectText.LineName(line.id);
            return line == null ? EffectText.Describe(effect, this)
                : $"突破機緣：{name}{(effect.key == "cap" ? "上限" : "階位")} +{Mathf.RoundToInt(effect.value)}（{line.rank}／{line.cap}）";
        }

        /// <summary>Study progress (2 progress = one rank, never past the cap) on a protagonist line.</summary>
        internal void StudyLine(string id, float amount)
        {
            var line = id == "martial" || string.IsNullOrEmpty(id) ? RankSystem.Major(Protagonist.lines, Protagonist.major)
                : Array.Find(Protagonist.lines ?? Array.Empty<SkillLine>(), entry => entry.id == id);
            TrainLine(line, amount * (line == null ? 1 : StudyMultiplier(line.id)));
        }
    }
}
