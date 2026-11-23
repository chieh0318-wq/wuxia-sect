using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    internal sealed class LegacyItemDef
    {
        public string id, name, category, icon, desc, statsText;
        public int cost, maxQty;
        public GearItem gear;
        public int silver, food, reputation;
        public string skillId;
        public int skillRankSteps;
    }

    /// <summary>
    /// M9 Legacy Points (積分系統 — 先輩遺澤): persistent points carried across runs,
    /// starting at 100 pts (plus earned battle/realm points) to bring gear, elixirs, and manuals into a new game.
    /// </summary>
    internal static class LegacySystem
    {
        private const string PrefKey = "Wuxia_LegacyPoints_Bonus";
        internal const int BasePoints = 100;

        internal static int TotalPoints => BasePoints + Mathf.Max(0, PlayerPrefs.GetInt(PrefKey, 0));

        internal static void AwardPoints(int delta)
        {
            if (delta <= 0) return;
            PlayerPrefs.SetInt(PrefKey, Mathf.Max(0, PlayerPrefs.GetInt(PrefKey, 0)) + delta);
            PlayerPrefs.Save();
        }

        internal static readonly LegacyItemDef[] Catalog =
        {
            // ── 武器與裝備 ──
            new()
            {
                id = "leg_qingfeng", name = "青鋒長劍", category = "武器裝備", icon = "item_sword", cost = 20, maxQty = 1,
                desc = "崤山劍門先輩留下的精鋼長劍，劍脊如秋水，開局即佩於腰間。",
                statsText = "武器・攻擊 +12・先手 +3",
                gear = new GearItem { slot = "武器", name = "青鋒長劍", grade = "精良", attack = 12, speed = 3 }
            },
            new()
            {
                id = "leg_yanling", name = "雁翎寶刀", category = "武器裝備", icon = "item_blade", cost = 20, maxQty = 1,
                desc = "河西刀客遺落的弧背寶刀，剛猛無儔，破甲力強。",
                statsText = "武器・攻擊 +15・氣血 +10",
                gear = new GearItem { slot = "武器", name = "雁翎寶刀", grade = "精良", attack = 15, hp = 10 }
            },
            new()
            {
                id = "leg_bracer", name = "玄鐵護腕", category = "武器裝備", icon = "item_bracer", cost = 15, maxQty = 1,
                desc = "以關中玄鐵鑲皮製成的護腕，格擋兵刃時穩若磐石。",
                statsText = "護腕・攻擊 +6・氣血 +25",
                gear = new GearItem { slot = "護腕", name = "玄鐵護腕", grade = "精良", attack = 6, hp = 25 }
            },
            new()
            {
                id = "leg_robe", name = "雲紋道袍", category = "武器裝備", icon = "item_robe", cost = 20, maxQty = 1,
                desc = "蠶絲混織金線的唐式武袍，輕盈防風，兼有護體之效。",
                statsText = "衣袍・氣血 +40・內力 +20",
                gear = new GearItem { slot = "衣袍", name = "雲紋道袍", grade = "精良", hp = 40, qi = 20 }
            },
            new()
            {
                id = "leg_boots", name = "踏雲快靴", category = "武器裝備", icon = "item_boots", cost = 15, maxQty = 1,
                desc = "輕功名家縫製的軟底雲履，行步無聲，出招快人一步。",
                statsText = "履靴・先手 +8・閃避 +5%",
                gear = new GearItem { slot = "履靴", name = "踏雲快靴", grade = "精良", speed = 8, dodge = 0.05f }
            },
            new()
            {
                id = "leg_trinket", name = "羊脂玉佩", category = "武器裝備", icon = "item_trinket", cost = 20, maxQty = 1,
                desc = "前代掌門所賜玉佩，佩之神清氣爽，內息運轉更為流暢。",
                statsText = "飾品・內力 +35・閃避 +3%",
                gear = new GearItem { slot = "飾品", name = "羊脂玉佩", grade = "珍品", qi = 35, dodge = 0.03f }
            },

            // ── 丹藥與糧食 ──
            new()
            {
                id = "item_jinchuang", name = "金創靈藥", category = "丹藥糧食", icon = "act_item", cost = 10, maxQty = 5,
                desc = "崤山藥廬秘製外傷良藥，回合戰鬥中可立即回復大量氣血（+90 氣血）。",
                statsText = "戰鬥道具・回復 90 氣血"
            },
            new()
            {
                id = "item_jiuzhuan", name = "九轉回元丹", category = "丹藥糧食", icon = "item_pill_hp", cost = 15, maxQty = 3,
                desc = "以終南山靈芝煉就的丹藥，戰鬥中服用可同時回復氣血與內力（+120 氣血、+60 內力）。",
                statsText = "戰鬥道具・回復 120 氣血與 60 內力"
            },
            new()
            {
                id = "item_rations", name = "行軍乾糧袋", category = "丹藥糧食", icon = "item_rations", cost = 8, maxQty = 3,
                desc = "耐放的胡餅與肉乾，開局為門派多備 40 份糧食，度過荒年無憂。",
                statsText = "開局糧食 +40",
                food = 40
            },
            new()
            {
                id = "item_wine", name = "汾州青梅酒", category = "丹藥糧食", icon = "item_wine", cost = 10, maxQty = 3,
                desc = "江湖豪客最愛的陳年佳釀，開局帶入可提振門派聲望並添置銀兩。",
                statsText = "開局聲望 +8・銀兩 +35",
                reputation = 8, silver = 35
            },

            // ── 武學秘笈 ──
            new()
            {
                id = "book_sword", name = "崤山劍訣手抄", category = "武學秘笈", icon = "item_manual_sword", cost = 25, maxQty = 1,
                desc = "師父早年手書的劍理批註，開局即令劍法階位與上限各進一階。",
                statsText = "劍法階位 +1・劍法潛力上限 +1",
                skillId = "sword", skillRankSteps = 1
            },
            new()
            {
                id = "book_neigong", name = "周天吐納古卷", category = "武學秘笈", icon = "item_manual_qi", cost = 25, maxQty = 1,
                desc = "道門失傳的內息古卷，開局即令內功階位與上限各進一階。",
                statsText = "內功階位 +1・內功潛力上限 +1",
                skillId = "neigong", skillRankSteps = 1
            },
            new()
            {
                id = "book_medicine", name = "千金藥方殘本", category = "武學秘笈", icon = "skill_medicine", cost = 15, maxQty = 1,
                desc = "收錄十餘道外傷內傷古方，開局提升醫術階位與上限各一階。",
                statsText = "醫術階位 +1・醫術潛力上限 +1",
                skillId = "medicine", skillRankSteps = 1
            },
            new()
            {
                id = "book_silver", name = "長安櫃坊憑帖", category = "武學秘笈", icon = "hud_silver", cost = 15, maxQty = 2,
                desc = "可於兩京兌換的銀票憑帖，開局立即為山門注入 100 兩資金。",
                statsText = "開局銀兩 +100",
                silver = 100
            },
        };

        internal static void ApplyCarried(GameState state, Dictionary<string, int> counts)
        {
            if (state == null || counts == null) return;
            foreach (var kv in counts)
            {
                if (kv.Value <= 0) continue;
                var def = Catalog.FirstOrDefault(x => x.id == kv.Key);
                if (def == null) continue;
                for (int i = 0; i < kv.Value; i++)
                {
                    if (def.gear != null)
                    {
                        var copy = new GearItem
                        {
                            slot = def.gear.slot, name = def.gear.name, grade = def.gear.grade,
                            attack = def.gear.attack, speed = def.gear.speed, hp = def.gear.hp,
                            qi = def.gear.qi, dodge = def.gear.dodge
                        };
                        state.EquipOrStore(copy);
                    }
                    if (def.silver != 0 || def.food != 0 || def.reputation != 0)
                        state.Apply(new Yield { silver = def.silver, food = def.food, reputation = def.reputation });
                    if (!string.IsNullOrEmpty(def.skillId) && def.skillRankSteps > 0)
                    {
                        var line = Array.Find(state.Protagonist.lines ?? Array.Empty<SkillLine>(), x => x?.id == def.skillId);
                        if (line != null) RankSystem.Raise(line, def.skillRankSteps, def.skillRankSteps);
                    }
                    if (def.id is "item_jinchuang" or "item_jiuzhuan")
                        AddInventory(state.Story, def.id, 1);
                }
            }
        }

        internal static int GetInventory(StoryProgress story, string id)
        {
            if (story?.inventory == null) return 0;
            return Array.Find(story.inventory, x => x?.id == id)?.value ?? 0;
        }

        internal static void AddInventory(StoryProgress story, string id, int delta)
        {
            if (story == null || string.IsNullOrEmpty(id)) return;
            story.inventory ??= Array.Empty<StoryValue>();
            var entry = Array.Find(story.inventory, x => x?.id == id);
            if (entry != null)
            {
                entry.value = Mathf.Max(0, entry.value + delta);
                return;
            }
            if (delta <= 0) return;
            Array.Resize(ref story.inventory, story.inventory.Length + 1);
            story.inventory[^1] = new StoryValue { id = id, value = delta };
        }
    }
}
