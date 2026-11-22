using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    /// <summary>One research node of the Sutra Pavilion tech tree (four branches, four tiers each).</summary>
    internal sealed class TechNode
    {
        public string id, branch, name, effect, requires;
        public int tier, silver, ap;
    }

    /// <summary>
    /// M6 systems on the save-backed state: the 藏經閣 tech tree (flags <c>tech_&lt;id&gt;</c>), protagonist gear and the
    /// sect armoury, the kitchen / smithy / teahouse / council actions, and taking outlaw strongholds on the world map.
    /// </summary>
    public sealed partial class GameState
    {
        internal static readonly (string id, string name, string motto)[] TechBranches =
        {
            ("sword", "【劍宗一脈】", "崤山劍門立派之本"),
            ("outer", "【外功百兵】", "拳掌刀槍暗器"),
            ("inner", "【內功輕功】", "吐納周天、提縱騰挪"),
            ("craft", "【百工經世】", "醫藥鍛造營造經商")
        };

        internal static readonly TechNode[] TechTree =
        {
            Node("sword1", "sword", 1, "崤山入門劍訣", 6, 1, "主角與弟子劍法修習速度 +15%，攻擊 +8"),
            Node("sword2", "sword", 2, "太白流雲劍譜", 12, 1, "劍法潛力上限 +1 階，先手 +4，攻擊 +15"),
            Node("sword3", "sword", 3, "兩儀化形劍陣", 20, 1, "全門派攻討戰力 +20%，閃避 +5%"),
            Node("sword4", "sword", 4, "崤山劍經全本", 32, 2, "劍法上限突破至 SSS，攻擊 +30，解鎖鎮派絕學"),
            Node("outer1", "outer", 1, "開碑拳譜精要", 6, 1, "拳掌／刀法／槍棍修習 +15%，氣血 +20"),
            Node("outer2", "outer", 2, "邊軍破陣刀槍", 12, 1, "刀法與槍棍潛力上限 +1 階，攻擊 +15"),
            Node("outer3", "outer", 3, "八方風雨暗器錄", 18, 1, "暗器上限 +1 階，先手 +6，奇遇檢定加成"),
            Node("outer4", "outer", 4, "萬兵總綱", 30, 2, "所有外功百兵階位立即 +1 階，全門派守備提升"),
            Node("inner1", "inner", 1, "崤山吐納心法", 6, 1, "內力上限 +30，每月自動療傷 +1"),
            Node("inner2", "inner", 2, "龜息周天功", 12, 1, "內力功法上限 +1 階，氣血 +40，內力 +50"),
            Node("inner3", "inner", 3, "踏雪無痕輕功", 18, 1, "輕功上限 +1 階，大地圖行軍行動力消耗 -1（最低 1），閃避 +6%"),
            Node("inner4", "inner", 4, "紫府玄元無上功", 32, 2, "內力與根骨各 +3，內力上限 +120，氣血 +100"),
            Node("craft1", "craft", 1, "百草金創藥方", 6, 1, "藥廬收入 +4，義診聲望 +1，療傷效率翻倍"),
            Node("craft2", "craft", 2, "百煉精鋼鍛法", 12, 1, "兵器坊收入 +6，鍛造出高階神兵機率翻倍"),
            Node("craft3", "craft", 3, "山門營造法式", 16, 1, "興建與擴建屋舍銀兩消耗永久 -25%，廂房容量 +2"),
            Node("craft4", "craft", 4, "奇門經商總策", 26, 1, "所有建築每月銀兩與糧食產出永久 +35%，每月行動力上限 +1")
        };

        private static TechNode Node(string id, string branch, int tier, string name, int silver, int ap, string effect) => new()
        {
            id = id, branch = branch, tier = tier, name = name, silver = silver, ap = ap, effect = effect,
            requires = tier > 1 ? branch + (tier - 1) : null
        };

        internal static TechNode Tech(string id) => Array.Find(TechTree, node => node.id == id);

        internal bool HasTech(string id) => (Story.flags ?? Array.Empty<StoryValue>()).Any(flag => flag.id == "tech_" + id && flag.value > 0);

        internal int TechCount => TechTree.Count(node => HasTech(node.id));

        /// <summary>"[已研習]" / "[可研習]" / "[需前置：…]" for a node card.</summary>
        internal string TechStatus(TechNode node)
        {
            if (HasTech(node.id)) return "[已研習]";
            if (node.requires != null && !HasTech(node.requires)) return $"[需前置：{Tech(node.requires)?.name}]";
            return "[可研習]";
        }

        internal bool CanResearch(TechNode node) => !HasTech(node.id) && (node.requires == null || HasTech(node.requires)) &&
            BuildingLevel("library") > 0 && Silver >= node.silver && ActionPoints >= node.ap;

        /// <summary>Researches a node, applying its permanent effects; returns the result line.</summary>
        internal string Research(TechNode node)
        {
            if (node == null || HasTech(node.id)) return "此功法已經研習過了。";
            if (BuildingLevel("library") <= 0) return "須先有藏經閣，才能研習功法。";
            if (node.requires != null && !HasTech(node.requires)) return $"需先研習「{Tech(node.requires)?.name}」。";
            if (Silver < node.silver) return "銀兩不足，買不起燈油紙墨。";
            if (!TrySpendAp(node.ap, "tech:" + node.id)) return "行動力不足。";
            Silver -= node.silver;
            SetValue(ref Story.flags, "tech_" + node.id, 1, false);
            SkillLine Line(string id) => Array.Find(Protagonist.lines ?? Array.Empty<SkillLine>(), entry => entry.id == id);
            switch (node.id)
            {
                case "sword1": case "outer1": Story.trainBonusPct += 15; break;
                case "sword2": RankSystem.Raise(Line("sword"), 0, 1); break;
                case "sword4":
                    var sword = Line("sword");
                    if (sword != null) sword.cap = RankSystem.Top;
                    SetValue(ref Story.flags, "sect_ultimate", 1, false);
                    break;
                case "outer2": RankSystem.Raise(Line("blade"), 0, 1); RankSystem.Raise(Line("polearm"), 0, 1); break;
                case "outer3": RankSystem.Raise(Line("hidden"), 0, 1); Story.insightChance += .03f; break;
                case "outer4":
                    foreach (string id in new[] { "fist", "blade", "hidden", "polearm" }) RankSystem.Raise(Line(id), 1, 1);
                    break;
                case "inner2": RankSystem.Raise(Line("neigong"), 0, 1); break;
                case "inner3": RankSystem.Raise(Line("qinggong"), 0, 1); break;
                case "inner4":
                    ApplyEffects(new[] { new GameEffect { key = "qi", value = 3 }, new GameEffect { key = "con", value = 3 } });
                    break;
                case "craft3": Story.buildCostPct -= 25; break;
                case "craft4": Story.apBonus += 1; break;
            }
            return $"研習「{node.name}」功成：{node.effect}";
        }

        // ---- Derived bonuses ---------------------------------------------------------------

        private (int attack, int speed, int hp, int qi, float dodge) TechStats()
        {
            int attack = 0, speed = 0, hp = 0, qi = 0;
            float dodge = 0;
            if (HasTech("sword1")) attack += 8;
            if (HasTech("sword2")) { attack += 15; speed += 4; }
            if (HasTech("sword3")) dodge += .05f;
            if (HasTech("sword4")) attack += 30;
            if (HasTech("outer1")) hp += 20;
            if (HasTech("outer2")) attack += 15;
            if (HasTech("outer3")) speed += 6;
            if (HasTech("inner1")) qi += 30;
            if (HasTech("inner2")) { hp += 40; qi += 50; }
            if (HasTech("inner3")) dodge += .06f;
            if (HasTech("inner4")) { hp += 100; qi += 120; }
            return (attack, speed, hp, qi, dodge);
        }

        /// <summary>Hero figures with worn gear and researched tech folded in.</summary>
        private DerivedStats WithBonuses(DerivedStats raw)
        {
            var tech = TechStats();
            var gear = Protagonist.gear ?? Array.Empty<GearItem>();
            return new DerivedStats(raw.Attack + tech.attack + gear.Sum(item => item?.attack ?? 0),
                raw.Speed + tech.speed + gear.Sum(item => item?.speed ?? 0),
                raw.MaxHp + tech.hp + gear.Sum(item => item?.hp ?? 0),
                raw.MaxQi + tech.qi + gear.Sum(item => item?.qi ?? 0), raw.QiPower,
                Mathf.Clamp(raw.Dodge + tech.dodge + gear.Sum(item => item?.dodge ?? 0), 0, .75f), raw.Fortune, raw.Study);
        }

        /// <summary>Beds across all 弟子廂房 (4 each, 6 once 山門營造法式 is researched).</summary>
        internal int Beds => Buildings.Count(building => building != null && building.actions.Contains("lodge")) *
            (4 + (HasTech("craft3") ? 2 : 0));

        /// <summary>Month-end extras: tech building income, the 35% commerce boost, held strongholds, faster healing.</summary>
        private void M6MonthIncome(ref int silver, ref int food, MonthlyReport report)
        {
            if (HasTech("craft1") && BuildingLevel("infirmary") > 0) silver += 4;
            if (HasTech("craft2")) silver += 6 * Buildings.Count(building => building?.id == "smithy");
            if (HasTech("craft4"))
            {
                silver = Mathf.RoundToInt(silver * 1.35f);
                food = Mathf.RoundToInt(food * 1.35f);
            }
            int held = Story.heldNodes?.Length ?? 0;
            if (held > 0)
            {
                silver += held * 4;
                food += held * 2;
                report.Lines.Add($"轄下據點 {held} 處　上繳銀兩 {held * 4}、糧食 {held * 2}");
            }
            if (HasTech("inner1") || HasTech("craft1"))
                RecoverInjuries();
        }

        // ---- Gear ---------------------------------------------------------------------------

        internal static readonly string[] GearSlots = { "武器", "護腕", "飾品", "冠帽", "衣袍", "履靴" };
        internal static readonly string[] GearGrades = { "凡品", "良品", "精品", "神兵" };
        private static readonly string[][] GearNames =
        {
            new[] { "青鋼長劍", "秋水劍", "龍吟寶劍", "太阿古劍" },
            new[] { "牛皮護腕", "鐵線護腕", "烏金護腕", "玄蠶臂鞲" },
            new[] { "平安玉珮", "辟邪香囊", "碧血玉玦", "鳳凰銜珠佩" },
            new[] { "青布幞頭", "束髮銀冠", "紫金冠", "天蠶星冠" },
            new[] { "粗布勁裝", "青衫錦袍", "軟甲戰袍", "金絲寶甲" },
            new[] { "麻鞋", "快靴", "踏雲靴", "凌波仙履" }
        };

        internal static GearItem MakeGear(string slot, int grade)
        {
            int index = Mathf.Max(0, Array.IndexOf(GearSlots, slot));
            grade = Mathf.Clamp(grade, 0, GearGrades.Length - 1);
            int scale = grade + 1;
            var item = new GearItem { slot = GearSlots[index], name = GearNames[index][grade], grade = GearGrades[grade] };
            switch (index)
            {
                case 0: item.attack = 6 * scale + (grade == 3 ? 8 : 0); break;
                case 1: item.attack = 2 * scale; item.hp = 10 * scale; break;
                case 2: item.qi = 15 * scale; item.dodge = .01f * scale; break;
                case 3: item.hp = 12 * scale; item.qi = 8 * scale; break;
                case 4: item.hp = 20 * scale; break;
                default: item.speed = 3 * scale; item.dodge = .01f * scale; break;
            }
            return item;
        }

        internal GearItem Equipped(string slot) => (Protagonist.gear ?? Array.Empty<GearItem>()).FirstOrDefault(item => item?.slot == slot);

        /// <summary>Equips the item when its slot is empty or it beats what is worn (the old piece goes to the armoury).</summary>
        internal string EquipOrStore(GearItem item)
        {
            var worn = Equipped(item.slot);
            string label = $"【{item.grade}】{item.name}（{item.slot}・{item.Stats}）";
            if (worn != null && worn.Power >= item.Power)
            {
                Story.armoury = (Story.armoury ?? Array.Empty<GearItem>()).Append(item).ToArray();
                return $"{label}已存入門派裝備庫。";
            }
            var gear = (Protagonist.gear ?? Array.Empty<GearItem>()).Where(entry => entry != null && entry.slot != item.slot).ToList();
            gear.Add(item);
            Protagonist.gear = gear.ToArray();
            if (worn != null)
                Story.armoury = (Story.armoury ?? Array.Empty<GearItem>()).Append(worn).ToArray();
            return $"主角裝備{label}{(worn != null ? $"，換下的{worn.name}收入裝備庫" : "")}。";
        }

        /// <summary>Six slot names for the equipment panel (gear first, then a story weapon in the 武器 slot).</summary>
        internal string[] GearLabels(string storyWeapon) => GearSlots.Select(slot =>
        {
            var item = Equipped(slot);
            if (item != null) return $"{item.name}\n{item.Stats}";
            return slot == "武器" ? storyWeapon : null;
        }).ToArray();

        /// <summary>鍛造兵甲: forging rank and 百煉精鋼鍛法 decide the grade.</summary>
        internal string ForgeGear()
        {
            int forging = RankSystem.Index(Array.Find(Protagonist.lines ?? Array.Empty<SkillLine>(), line => line.id == "forging")?.rank);
            float boost = HasTech("craft2") ? 2 : 1;
            float legendary = (.03f + .015f * forging) * boost, fine = (.12f + .03f * forging) * boost, good = .40f;
            float roll = UnityEngine.Random.value;
            int grade = roll < legendary ? 3 : roll < legendary + fine ? 2 : roll < legendary + fine + good ? 1 : 0;
            var item = MakeGear(GearSlots[UnityEngine.Random.Range(0, GearSlots.Length)], grade);
            return "爐火熊熊，鍛成一件" + EquipOrStore(item);
        }

        // ---- Building actions ---------------------------------------------------------------

        internal string Feast()
        {
            ApplyEffects(new[] { new GameEffect { key = "loyaltyAll", value = 6 } });
            RecoverInjuries();
            Protagonist.injury = Mathf.Max(0, Protagonist.injury - 1);
            return "大鍋燉肉香飄滿院，全體弟子忠誠 +6，傷勢加速恢復。";
        }

        internal string Decree()
        {
            ApplyEffects(new[] { new GameEffect { key = "loyaltyAll", value = 3 } });
            return "門規張貼於祖師殿前，全門忠誠 +3。";
        }

        internal string Lodge()
        {
            int month = Year * 12 + Month;
            if (Story.lodgeMonth == month)
                return "本月已安頓過弟子。";
            Story.lodgeMonth = month;
            foreach (var disciple in Disciples)
            {
                disciple.loyalty = Mathf.Clamp(disciple.loyalty + 2, 0, 100);
                disciple.injury = Mathf.Max(0, disciple.injury - 1);
            }
            return "被褥曬過、床板修好，全門弟子忠誠 +2、傷勢 −1。";
        }

        /// <summary>江湖聽風: raises the next qi-yu chance; sometimes the rumour leads straight to a secret realm.</summary>
        internal bool GatherRumors()
        {
            Story.rumorBonus = Mathf.Min(.75f, Story.rumorBonus + .25f);
            return UnityEngine.Random.value < .35f + AdventureBonus;
        }

        /// <summary>Chance that 山道巡訪 stumbles on a secret realm (幸運 / 身法 and gathered rumours help).</summary>
        internal float RealmChance => Mathf.Clamp(.22f + 2 * AdventureBonus + Story.rumorBonus, .05f, .9f);

        internal float ConsumeRumors()
        {
            float bonus = Story.rumorBonus;
            Story.rumorBonus = 0;
            return bonus;
        }

        internal void GrantAp(int amount) => ActionPoints += Mathf.Max(0, amount);

        internal void Injure(int amount) => Protagonist.injury = Mathf.Clamp(Protagonist.injury + amount, 0, 9);

        // ---- World map strongholds ------------------------------------------------------------

        public string SectLabel => Data.Start.sectName;

        public bool HoldsNode(string id) => (Story.heldNodes ?? Array.Empty<string>()).Contains(id);

        public int TravelCost(int ap) => HasTech("inner3") ? Mathf.Max(Mathf.Min(ap, 1), ap - 1) : ap;

        /// <summary>Sect martial power for 率眾攻討: the hero's figures, every disciple's rank, tech formations.</summary>
        public int SectPower
        {
            get
            {
                var hero = HeroStats;
                float power = hero.Attack + hero.MaxHp / 10f + hero.Speed;
                foreach (var disciple in Disciples.Where(member => member.injury < 3))
                    power += 14 * RankSystem.Multiplier(RankSystem.Martial(disciple.lines ?? Array.Empty<SkillLine>()));
                power *= 1 + (HasTech("sword3") ? .2f : 0) + (HasTech("outer4") ? .1f : 0);
                return Mathf.RoundToInt(power);
            }
        }

        public float AssaultChance(int garrison, float guard)
        {
            float strength = garrison * guard;
            return Mathf.Clamp(SectPower / (SectPower + strength) + .1f, .1f, .95f);
        }

        /// <summary>率眾攻討 (2 AP): win and the stronghold flies the sect banner with its loot; lose and the hero is hurt.</summary>
        public string Assault(string nodeId, string factionId, int garrison, float guard)
        {
            if (HoldsNode(nodeId)) return "此地已插上本派旗號。";
            if (!TrySpendAp(2, "assault:" + nodeId)) return "行動力不足（率眾攻討需 2 點）。";
            float chance = AssaultChance(garrison, guard);
            string place = WorldMapData.Load().NodeLabel(nodeId);
            if (UnityEngine.Random.value < chance)
            {
                Story.heldNodes = (Story.heldNodes ?? Array.Empty<string>()).Append(nodeId).ToArray();
                int silver = 20 + garrison / 4, food = 10 + garrison / 10;
                ApplyEffects(new[]
                {
                    new GameEffect { key = "silver", value = silver }, new GameEffect { key = "food", value = food },
                    new GameEffect { key = "reputation", value = 3 },
                    new GameEffect { key = "relation", @ref = factionId, value = -10 }
                });
                string loot = UnityEngine.Random.value < .35f
                    ? "　繳獲：" + EquipOrStore(MakeGear(GearSlots[UnityEngine.Random.Range(0, GearSlots.Length)], 1)) : "";
                return $"大捷！{SectLabel}攻破{place}，插上本派旗號（每月上繳銀兩 4、糧食 2）。" +
                    $"繳獲銀兩 {silver}、糧食 {food}，聲望 +3。{loot}";
            }
            Injure(1);
            ApplyEffects(new[] { new GameEffect { key = "reputation", value = -1 } });
            return $"攻討{place}失利，寨中守備比想像中頑強。主角負傷，聲望 −1。（勝算 {chance:P0}）";
        }
    }
}
