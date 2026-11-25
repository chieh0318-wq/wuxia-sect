using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    [Serializable] internal sealed class HallRequirement
    {
        public int reputation, year, knownFactions, realmsCleared, treasures, scale;
        public string building, flag;
    }

    [Serializable] internal sealed class HallFunction
    {
        public string id, name, target, desc;
        public int ap, food, silver;
    }

    [Serializable] internal sealed class HallData
    {
        public string id, name, icon, art, desc, summary;
        public HallRequirement requires;
        public Cost cost, levelCost;
        public int ap = 1, maxLevel = 1, levelReputation;
        public GameEffect[] monthly, found;
        public HallFunction[] functions;
    }

    [Serializable] internal sealed class HallCatalog
    {
        public HallData[] halls;
        public string[] slots;
        private static HallCatalog data;
        internal static HallCatalog Data => data ??= Normalize(M2Data.Load<HallCatalog>("halls"));

        private static HallCatalog Normalize(HallCatalog catalog)
        {
            catalog.halls = (catalog.halls ?? Array.Empty<HallData>()).Where(hall => hall != null && !string.IsNullOrEmpty(hall.id)).ToArray();
            foreach (var hall in catalog.halls)
            {
                hall.requires ??= new HallRequirement();
                hall.cost ??= new Cost();
                hall.levelCost ??= new Cost();
                hall.monthly ??= Array.Empty<GameEffect>();
                hall.found ??= Array.Empty<GameEffect>();
                hall.functions ??= Array.Empty<HallFunction>();
                hall.maxLevel = Mathf.Max(1, hall.maxLevel);
            }
            catalog.slots ??= GameState.GearSlots;
            return catalog;
        }
    }

    /// <summary>
    /// v1124 C18 山門堂口: halls beyond the courtyard plots (練武堂、鑄造堂、藥堂、客堂、傳功閣) unlocked step by step by
    /// reputation, resources and story progress. Levels live in story flags ("hall_&lt;id&gt;"), so old saves simply start
    /// with none founded.
    /// </summary>
    public sealed partial class GameState
    {
        internal static HallData Hall(string id) => HallCatalog.Data.halls.FirstOrDefault(hall => hall.id == id);

        internal int HallLevel(string id) => ValueOf(Story.flags ?? Array.Empty<StoryValue>(), "hall_" + id);

        /// <summary>Each unlock condition with whether it is met, e.g. ("聲望 12（目前 8）", false).</summary>
        internal IEnumerable<(string text, bool met)> HallRequirements(HallData hall)
        {
            var need = hall.requires;
            if (need.scale > 0 && need.scale < Scales.Length) yield return ($"山門規模須達{Scales[need.scale].name}", SectScale >= need.scale);
            if (need.reputation > 0) yield return ($"聲望 {need.reputation}（目前 {Reputation}）", Reputation >= need.reputation);
            if (need.year > 0) yield return ($"開元{need.year}年以後", Year >= need.year);
            if (!string.IsNullOrEmpty(need.building))
            {
                string name = Data.Buildings.FirstOrDefault(building => building.id == need.building)?.name ?? need.building;
                yield return ($"院中已建{name}", BuildingLevel(need.building) > 0);
            }
            if (need.knownFactions > 0)
            {
                int known = WorldMapData.Load().Factions.Count(faction => IsFactionKnown(faction.id));
                yield return ($"結識 {need.knownFactions} 個勢力（目前 {known}）", known >= need.knownFactions);
            }
            if (need.realmsCleared > 0)
                yield return ($"闖過 {need.realmsCleared} 處秘境（目前 {Story.realmsCleared}）", Story.realmsCleared >= need.realmsCleared);
            if (need.treasures > 0)
                yield return ($"取得 {need.treasures} 處寶物秘境（目前 {TreasuresClaimed}）", TreasuresClaimed >= need.treasures);
            if (!string.IsNullOrEmpty(need.flag))
                yield return ("劇情推進後開放", ValueOf(Story.flags ?? Array.Empty<StoryValue>(), need.flag) > 0);
        }

        internal bool HallUnlocked(HallData hall) => HallRequirements(hall).All(entry => entry.met);

        /// <summary>Cost of founding (level 0) or of the next level.</summary>
        internal Cost HallCost(HallData hall) => HallLevel(hall.id) <= 0 ? hall.cost : new Cost
        {
            silver = BuildSilver(hall.levelCost.silver * HallLevel(hall.id)), food = hall.levelCost.food
        };

        /// <summary>Why the hall cannot be founded / upgraded now, or null.</summary>
        internal string HallBuildBlock(HallData hall)
        {
            int level = HallLevel(hall.id);
            if (level >= hall.maxLevel) return "已達最高等級";
            if (level <= 0 && !HallUnlocked(hall)) return "條件未達";
            if (level > 0 && Reputation < hall.levelReputation * level) return $"擴建須聲望 {hall.levelReputation * level}";
            var cost = HallCost(hall);
            if (!CanPay(cost)) return "銀兩或糧食不足";
            if (ActionPoints < hall.ap) return $"行動力不足（需 {hall.ap}）";
            return null;
        }

        internal string BuildHall(HallData hall, string worker = HeroId)
        {
            if (AssignmentSystem.Block(this, worker) is string unavailable) return unavailable;
            string block = HallBuildBlock(hall);
            if (block != null) return block;
            var cost = HallCost(hall);
            float quality = AssignmentSystem.Predict(this, "build", worker).Quality;
            cost = new Cost { silver = Mathf.CeilToInt(cost.silver / Mathf.Max(1, quality)), food = cost.food };
            if (!TrySpendAp(hall.ap, "hall:" + hall.id)) return "行動力不足";
            Pay(cost);
            AssignmentSystem.Commit(this, new[] { worker }, "build");
            AssignmentSystem.Credit(this, worker, 2);
            int level = HallLevel(hall.id) + 1;
            SetValue(ref Story.flags, "hall_" + hall.id, level, false);
            if (level == 1) ApplyEffects(hall.found);
            return level == 1 ? $"{hall.name}落成！{hall.summary}" : $"{hall.name}擴建為 {level} 級。";
        }

        internal string HallFunctionBlock(HallFunction function)
        {
            if (ActionPoints < function.ap) return $"行動力不足（需 {function.ap}）";
            if (Silver < function.silver || Food < function.food) return "銀兩或糧食不足";
            return null;
        }

        /// <summary>Runs a hall command. target is a person id ("hero" / disciple) or a gear slot when the command needs one.</summary>
        internal string RunHallFunction(HallData hall, HallFunction function, string target, string worker)
        {
            if (AssignmentSystem.Block(this, worker) is string unavailable) return unavailable;
            float quality = AssignmentSystem.Predict(this, function.id, worker).Quality;
            string block = HallFunctionBlock(function);
            if (block != null) return block;
            if (!TrySpendAp(function.ap, "hall:" + function.id)) return "行動力不足";
            var forgeOdds = function.id == "forge" ? ForgeGearOdds(worker, hall: true) : null;
            Pay(new Cost { silver = function.silver, food = function.food });
            AssignmentSystem.Commit(this, new[] { worker }, function.id);
            AssignmentSystem.Credit(this, worker, 1);
            int level = Mathf.Max(1, HallLevel(hall.id));
            switch (function.id)
            {
                case "seclusion":
                {
                    float amount = (2.5f + (level - 1)) * quality;
                    if (target == HeroId || string.IsNullOrEmpty(target))
                    {
                        var line = RankSystem.Major(Protagonist.lines, Protagonist.major);
                        StudyLine("martial", amount);
                        return $"{Protagonist.name}閉關苦修，{EffectText.LineName(line?.id)}進度 +{amount * StudyMultiplier(line?.id ?? ""):0.0}" +
                            (line == null ? "" : $"（{line.rank}／{line.cap}）");
                    }
                    var member = Disciples.FirstOrDefault(d => d.id == target);
                    var best = RankSystem.BestCombat(member?.lines);
                    TrainLine(best, amount);
                    return member == null ? "無人閉關。" :
                        $"{member.name}閉關苦修，{EffectText.LineName(best?.id)}進度 +{amount:0.0}" + (best == null ? "" : $"（{best.rank}／{best.cap}）");
                }
                case "forge":
                {
                    float roll = UnityEngine.Random.value;
                    int grade = roll < forgeOdds[3] ? 3 : roll < forgeOdds[3] + forgeOdds[2] ? 2 : 1;
                    string slot = Array.IndexOf(GearSlots, target) >= 0 ? target : GearSlots[0];
                    return "鑄造堂爐火通紅，打成一件" + EquipOrStore(MakeGear(slot, grade));
                }
                case "alchemy":
                {
                    int portions = Mathf.Clamp(Mathf.RoundToInt(2 * quality), 1, 4);
                    LegacySystem.AddInventory(Story, "item_jinchuang", portions);
                    bool pill = level >= 2 || AssignmentSystem.Skill(this, worker, "alchemy") >= RankSystem.Index("B");
                    if (pill) LegacySystem.AddInventory(Story, "item_jiuzhuan", 1);
                    return $"丹爐開了七日七夜，煉成金創靈藥 ×{portions}" + (pill ? "、九轉回元丹 ×1" : "") + "。";
                }
                case "treat":
                    for (int i = 0; i < 2; i++) RecoverInjuries(worker);
                    Protagonist.injury = Mathf.Max(0, Protagonist.injury - 1);
                    return "藥堂延醫煎藥，主角與全門弟子傷勢減輕。";
                case "host":
                {
                    Story.rumorBonus = Mathf.Min(.75f, Story.rumorBonus + .25f);
                    ApplyEffects(new[] { new GameEffect { key = "reputation", value = 2 } });
                    var known = WorldMapData.Load().Factions.Where(faction => IsFactionKnown(faction.id) && !faction.Court).ToArray();
                    if (known.Length == 0) return "客堂裡茶香不斷，打聽到不少江湖消息。聲望 +2、下次奇遇機率 +25%。";
                    var guest = known[UnityEngine.Random.Range(0, known.Length)];
                    ApplyEffects(new[] { new GameEffect { key = "relation", @ref = guest.id, value = 4 } });
                    return $"{guest.Label}的朋友在客堂作客數日，賓主盡歡。聲望 +2、{guest.Label}關係 +4、下次奇遇機率 +25%。";
                }
                case "lecture":
                    foreach (var member in Disciples)
                    {
                        TrainLine(RankSystem.BestCombat(member.lines), 1.2f * quality);
                        member.loyalty = Mathf.Clamp(member.loyalty + 2, 0, 100);
                    }
                    return $"{SparName(worker)}開壇傳功，全門弟子武學進度 +{1.2f * quality:0.0}、忠誠 +2。";
            }
            return $"{function.name}完成。";
        }

        /// <summary>Month-end: each founded hall applies its monthly effects once per level.</summary>
        private System.Collections.Generic.IEnumerable<(HallData hall, int level, GameEffect[] effects)> MonthlyHallEffects()
        {
            foreach (var hall in HallCatalog.Data.halls)
            {
                int level = HallLevel(hall.id);
                if (level <= 0 || hall.monthly.Length == 0) continue;
                var effects = hall.monthly.Select(effect => new GameEffect { key = effect.key, @ref = effect.@ref, value = effect.value * level })
                    .ToArray();
                // Healing repeats rather than scales.
                foreach (var effect in effects.Where(effect => effect.key == "heal")) effect.value = level;
                yield return (hall, level, effects);
            }
        }

        internal void HallsMonth(MonthlyReport report)
        {
            foreach (var (hall, level, effects) in MonthlyHallEffects())
            {
                ApplyEffects(effects);
                string text = EffectText.Describe(effects, this);
                if (!string.IsNullOrEmpty(text)) report?.Lines.Add($"{hall.name}（{level} 級）：{text}");
            }
        }
    }
}
