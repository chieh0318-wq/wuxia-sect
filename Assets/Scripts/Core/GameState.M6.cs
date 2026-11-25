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
        public int tier, silver, ap, minReputation, minScale;
        /// <summary>v1126d: protagonist major martial grade required (RankSystem tier index; 0 = no gate).</summary>
        public int minHeroTier;
        /// <summary>Effect magnitudes from balance_v1126c.json (see <see cref="GameState.TechEffectText"/>).</summary>
        public float value, value2, value3;
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

        /// <summary>
        /// v1126c PA3: names and branch/tier are fixed here; cost, gates and effect magnitudes come from
        /// balance_v1126c.json (techTree) — the defaults below mirror that file. The effect text is generated from the
        /// same numbers that <see cref="Research"/>, <see cref="TechStats"/> and <see cref="M6MonthIncome"/> apply.
        /// Args: id, branch, tier, name, silver, ap, minReputation, minScale, value, value2, value3.
        /// </summary>
        internal static readonly TechNode[] TechTree =
        {
            Node("sword1", "sword", 1, "崤山入門劍訣", 30, 1, 0, 0, 5, 3),
            Node("sword2", "sword", 2, "太白流雲劍譜", 90, 1, 40, 0, 6, 2),
            Node("sword3", "sword", 3, "兩儀化形劍陣", 200, 2, 110, 1, 6, 2),
            Node("sword4", "sword", 4, "崤山劍經全本", 420, 2, 260, 1, 10),
            Node("outer1", "outer", 1, "開碑拳譜精要", 30, 1, 0, 0, 5, 10),
            Node("outer2", "outer", 2, "邊軍破陣刀槍", 90, 1, 40, 0, 6),
            Node("outer3", "outer", 3, "八方風雨暗器錄", 180, 2, 110, 1, 3, 1),
            Node("outer4", "outer", 4, "萬兵總綱", 380, 2, 360, 1, 4),
            Node("inner1", "inner", 1, "崤山吐納心法", 30, 1, 0, 0, 15),
            Node("inner2", "inner", 2, "龜息周天功", 90, 1, 40, 0, 20, 25),
            Node("inner3", "inner", 3, "踏雪無痕輕功", 180, 2, 110, 1, 3),
            Node("inner4", "inner", 4, "紫府玄元無上功", 420, 2, 360, 1, 1, 60, 50),
            Node("craft1", "craft", 1, "百草金創藥方", 30, 1, 0, 0, 2, 1),
            Node("craft2", "craft", 2, "百煉精鋼鍛法", 90, 1, 40, 0, 3, 1.5f),
            Node("craft3", "craft", 3, "山門營造法式", 180, 2, 110, 1, 8, 1),
            Node("craft4", "craft", 4, "奇門經商總策", 360, 2, 360, 1, 8, 1)
        };

        private static TechNode Node(string id, string branch, int tier, string name, int silver, int ap, int minReputation,
            int minScale, float value, float value2 = 0, float value3 = 0)
        {
            var node = new TechNode
            {
                id = id, branch = branch, tier = tier, name = name, silver = silver, ap = ap, minReputation = minReputation,
                minScale = minScale, value = value, value2 = value2, value3 = value3,
                requires = tier > 1 ? branch + (tier - 1) : null
            };
            if (BalanceV1126c.Tech(id) is { } tuned)
            {
                node.silver = tuned.silver; node.ap = tuned.ap; node.minReputation = tuned.minReputation;
                node.minScale = tuned.minScale; node.value = tuned.value; node.value2 = tuned.value2; node.value3 = tuned.value3;
                if (!string.IsNullOrEmpty(tuned.requires)) node.requires = tuned.requires;
                if (!string.IsNullOrEmpty(tuned.minHeroRank)) node.minHeroTier = RankSystem.Index(tuned.minHeroRank);
            }
            node.effect = TechEffectText(node);
            return node;
        }

        /// <summary>Player-facing effect line built from the node's own numbers.</summary>
        internal static string TechEffectText(TechNode n)
        {
            static string V(float x) => x.ToString("0.#");
            return n.id switch
            {
                "sword1" => $"全武學修習速度 +{V(n.value)}%，主角攻擊 +{V(n.value2)}",
                "sword2" => $"主角劍法上限 +1 階，攻擊 +{V(n.value)}，先手 +{V(n.value2)}",
                "sword3" => $"全門派攻討戰力 +{V(n.value)}%，主角閃避 +{V(n.value2)}%",
                "sword4" => $"主角劍法上限 +1 階（至多 SSS），攻擊 +{V(n.value)}，解鎖鎮派絕學",
                "outer1" => $"全武學修習速度 +{V(n.value)}%，主角氣血 +{V(n.value2)}",
                "outer2" => $"主角刀法與槍棍上限 +1 階，攻擊 +{V(n.value)}",
                "outer3" => $"主角暗器上限 +1 階，先手 +{V(n.value)}，奇遇感悟 +{V(n.value2)}%",
                "outer4" => $"主角拳掌／刀／暗器／槍棍上限各 +1 階，全門派攻討戰力 +{V(n.value)}%",
                "inner1" => $"主角內力 +{V(n.value)}，月結時多療傷一次",
                "inner2" => $"主角內功上限 +1 階，氣血 +{V(n.value)}，內力 +{V(n.value2)}",
                "inner3" => $"主角輕功上限 +1 階，大地圖行軍行動力 −1（最低 1），閃避 +{V(n.value)}%",
                "inner4" => $"主角內力與根骨屬性各 +{V(n.value)}，內力 +{V(n.value2)}，氣血 +{V(n.value3)}",
                "craft1" => $"有藥廬時每月銀 +{V(n.value)}，施藥義診另得聲望 +{V(n.value2)}，月結時多療傷一次",
                "craft2" => $"每座兵器坊每月銀 +{V(n.value)}，鍛造精品／神兵機率 ×{V(n.value2)}",
                "craft3" => $"興建與擴建屋舍耗銀 −{V(n.value)}%，每座弟子廂房床位 +{V(n.value2)}",
                "craft4" => $"建築每月銀兩與糧食產出 +{V(n.value)}%，每月行動力上限 +{V(n.value2)}",
                _ => ""
            };
        }

        /// <summary>Why a node whose prerequisite is learned is still closed by 聲望 / 山門規模 / 主修武學 (null when open).</summary>
        internal string TechGate(TechNode node)
        {
            string scale = Scales[Mathf.Clamp(node.minScale, 0, Scales.Length - 1)].name;
            var needs = new List<string>(3);
            if (Reputation < node.minReputation) needs.Add($"需聲望 {node.minReputation}");
            if (SectScale < node.minScale) needs.Add($"需山門{scale}");
            if (node.minHeroTier > 0 && HeroMajorTier < node.minHeroTier) needs.Add($"需主修達 {RankSystem.At(node.minHeroTier)}");
            return needs.Count == 0 ? null : string.Join("、", needs).Replace("、需", "、");
        }

        /// <summary>v1126d CF2: every 聲望 / 山門規模 / 主修 requirement of a node, met or not (null when none).</summary>
        internal static string TechGateSummary(TechNode node)
        {
            var needs = new List<string>(3);
            if (node.minReputation > 0) needs.Add($"聲望{node.minReputation}");
            if (node.minScale > 0) needs.Add(Scales[Mathf.Clamp(node.minScale, 0, Scales.Length - 1)].name);
            if (node.minHeroTier > 0) needs.Add($"主修{RankSystem.At(node.minHeroTier)}");
            return needs.Count == 0 ? null : string.Join("・", needs);
        }

        private float TechValue(string id, Func<TechNode, float> pick) => HasTech(id) && Tech(id) is { } node ? pick(node) : 0;

        internal static TechNode Tech(string id) => Array.Find(TechTree, node => node.id == id);

        internal bool HasTech(string id) => (Story.flags ?? Array.Empty<StoryValue>()).Any(flag => flag.id == "tech_" + id && flag.value > 0);

        internal int TechCount => TechTree.Count(node => HasTech(node.id));

        /// <summary>"[已研習]" / "[可研習]" / "[需前置：…]" for a node card.</summary>
        internal string TechStatus(TechNode node)
        {
            if (HasTech(node.id)) return "[已研習]";
            if (node.requires != null && !HasTech(node.requires)) return $"[需前置：{Tech(node.requires)?.name}]";
            if (TechGate(node) is string gate) return $"[{gate}]";
            return "[可研習]";
        }

        internal string CampaignBlock(MapNode node, MapFaction foe)
        {
            if (node == null || foe == null) return "此處沒有可攻打的據點。";
            if (node.id == StartNode || HoldsNode(node.id)) return "此處由本門守備。";
            if (!IsFactionKnown(foe.id)) return "先打探此地守軍。";
            if (Patron?.id == foe.id || HasTie(foe.id, Diplomacy.Alliance | Diplomacy.Pact) || Protected(HeroId, foe.id))
                return $"仍與{foe.Label}有約，請先解除盟約。";
            if (!foe.Outlaw && Attitude(foe) > -25)
                return foe.Court ? $"攻打將與{foe.Label}為敵，須先在外交交惡。"
                    : $"與{foe.Label}尚未敵對，須先在外交交惡。";
            if (PartyNode != node.id) return "須先率隊抵達此地。";
            if (ActionPoints < 2) return "攻打需要 2 點行動力。";
            return null;
        }

        internal bool CanResearch(TechNode node) => !HasTech(node.id) && (node.requires == null || HasTech(node.requires)) &&
            TechGate(node) == null && BuildingLevel("library") > 0 && Silver >= node.silver && ActionPoints >= node.ap;

        /// <summary>Researches a node, applying its permanent effects; returns the result line.</summary>
        internal string Research(TechNode node, string worker = null)
        {
            if (node == null || HasTech(node.id)) return "此功法已經研習過了。";
            if (BuildingLevel("library") <= 0) return "須先有藏經閣，才能研習功法。";
            if (node.requires != null && !HasTech(node.requires)) return $"需先研習「{Tech(node.requires)?.name}」。";
            if (TechGate(node) is string gate) return gate + "，方可研習此重。";
            if (worker != null && IsGuestOfficer(worker) && node.branch == "sword")
                return "客將不可由本門傳授崤山武學。";
            if (worker != null && AssignmentSystem.Block(this, worker) is string unavailable) return unavailable;
            float quality = worker == null ? 1 : AssignmentSystem.Predict(this, "study", worker).Quality;
            int silver = Mathf.CeilToInt(node.silver / Mathf.Max(1, quality));
            if (Silver < silver) return "銀兩不足，買不起燈油紙墨。";
            if (!TrySpendAp(node.ap, "tech:" + node.id)) return "行動力不足。";
            Silver -= silver;
            if (worker != null)
            {
                AssignmentSystem.Commit(this, new[] { worker }, "study");
                AssignmentSystem.Credit(this, worker, 1);
            }
            SetValue(ref Story.flags, "tech_" + node.id, 1, false);
            SkillLine Line(string id) => Array.Find(Protagonist.lines ?? Array.Empty<SkillLine>(), entry => entry.id == id);
            switch (node.id)
            {
                case "sword1": case "outer1": Story.trainBonusPct += Mathf.RoundToInt(node.value); break;
                case "sword2": RankSystem.Raise(Line("sword"), 0, 1); break;
                case "sword4":
                    // v1126c: one more cap tier (at most SSS) instead of jumping straight to SSS.
                    RankSystem.Raise(Line("sword"), 0, 1);
                    SetValue(ref Story.flags, "sect_ultimate", 1, false);
                    break;
                case "outer2": RankSystem.Raise(Line("blade"), 0, 1); RankSystem.Raise(Line("polearm"), 0, 1); break;
                case "outer3": RankSystem.Raise(Line("hidden"), 0, 1); Story.insightChance += node.value2 / 100f; break;
                case "outer4":
                    foreach (string id in new[] { "fist", "blade", "hidden", "polearm" }) RankSystem.Raise(Line(id), 0, 1);
                    break;
                case "inner2": RankSystem.Raise(Line("neigong"), 0, 1); break;
                case "inner3": RankSystem.Raise(Line("qinggong"), 0, 1); break;
                case "inner4":
                    int points = Mathf.RoundToInt(node.value);
                    ApplyEffects(new[] { new GameEffect { key = "qi", value = points }, new GameEffect { key = "con", value = points } });
                    break;
                case "craft3": Story.buildCostPct -= Mathf.RoundToInt(node.value); break;
                case "craft4": Story.apBonus += Mathf.RoundToInt(node.value2); break;
            }
            return $"研習「{node.name}」功成：{node.effect}";
        }

        // ---- Derived bonuses ---------------------------------------------------------------

        private (int attack, int speed, int hp, int qi, float dodge) TechStats()
        {
            int attack = 0, speed = 0, hp = 0, qi = 0;
            float dodge = 0;
            int I(string id, Func<TechNode, float> pick) => Mathf.RoundToInt(TechValue(id, pick));
            attack += I("sword1", n => n.value2) + I("sword2", n => n.value) + I("sword4", n => n.value) + I("outer2", n => n.value);
            speed += I("sword2", n => n.value2) + I("outer3", n => n.value);
            dodge += (TechValue("sword3", n => n.value2) + TechValue("inner3", n => n.value)) / 100f;
            hp += I("outer1", n => n.value2) + I("inner2", n => n.value) + I("inner4", n => n.value3);
            qi += I("inner1", n => n.value) + I("inner2", n => n.value2) + I("inner4", n => n.value2);
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
                Mathf.Clamp(raw.Dodge + tech.dodge + gear.Sum(item => item?.dodge ?? 0), 0, .75f), raw.Fortune, raw.Study,
                raw.Hit, raw.Crit, raw.QiRegen);
        }

        /// <summary>Beds across all 弟子廂房 (sect.bedsPerQuarters each, plus 山門營造法式's bonus).</summary>
        internal int Beds => Buildings.Count(building => building != null && building.actions.Contains("lodge")) *
            (BalanceV1126c.Sect.bedsPerQuarters + Mathf.RoundToInt(TechValue("craft3", n => n.value2)));

        /// <summary>Month-end extras: tech building income, the 奇門經商總策 boost, held strongholds, faster healing.</summary>
        private void M6MonthIncome(ref int silver, ref int food, MonthlyReport report)
        {
            if (HasTech("craft1") && BuildingLevel("infirmary") > 0) silver += Mathf.RoundToInt(TechValue("craft1", n => n.value));
            if (HasTech("craft2"))
                silver += Mathf.RoundToInt(TechValue("craft2", n => n.value)) * Buildings.Count(building => building?.id == "smithy");
            if (HasTech("craft4"))
            {
                float boost = 1 + TechValue("craft4", n => n.value) / 100f;
                silver = Mathf.RoundToInt(silver * boost);
                food = Mathf.RoundToInt(food * boost);
            }
            int held = Story.heldNodes?.Length ?? 0;
            if (held > 0)
            {
                var (holdingSilver, holdingFood) = MonthlyHoldingIncome;
                silver += holdingSilver;
                food += holdingFood;
                report?.Lines.Add($"轄下據點 {held} 處　上繳銀兩 {holdingSilver}、糧食 {holdingFood}");
            }
            if (report != null && (HasTech("inner1") || HasTech("craft1")))
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
            item.attribute = new[] { "str", "con", "qi", "wit", "con", "agi" }[index];
            item.attributeBonus = grade;
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
        internal string ForgeGear(string worker = HeroId)
        {
            var odds = ForgeGearOdds(worker);
            float roll = UnityEngine.Random.value;
            int grade = roll < odds[3] ? 3 : roll < odds[3] + odds[2] ? 2 : roll < 1 - odds[0] ? 1 : 0;
            var item = MakeGear(GearSlots[UnityEngine.Random.Range(0, GearSlots.Length)], grade);
            return "爐火熊熊，鍛成一件" + EquipOrStore(item);
        }

        // ---- Building actions ---------------------------------------------------------------

        internal string Feast()
        {
            ApplyEffects(new[] { new GameEffect { key = "loyaltyAll", value = FeastLoyalty } });
            RecoverInjuries();
            Protagonist.injury = Mathf.Max(0, Protagonist.injury - FeastExtraHealing);
            return $"大鍋燉肉香飄滿院，全體弟子忠誠 +{FeastLoyalty}，傷勢加速恢復。";
        }

        internal string Decree()
        {
            ApplyEffects(new[] { new GameEffect { key = "loyaltyAll", value = DecreeLoyalty } });
            return $"門規張貼於祖師殿前，全門忠誠 +{DecreeLoyalty}。";
        }

        internal string Lodge()
        {
            int month = Year * 12 + Month;
            if (Story.lodgeMonth == month)
                return "本月已安頓過弟子。";
            Story.lodgeMonth = month;
            foreach (var disciple in Disciples)
            {
                disciple.loyalty = Mathf.Clamp(disciple.loyalty + LodgeLoyalty, 0, 100);
                disciple.injury = Mathf.Max(0, disciple.injury - LodgeHealing);
            }
            return $"被褥曬過、床板修好，全門弟子忠誠 +{LodgeLoyalty}、傷勢 −{LodgeHealing}。";
        }

        /// <summary>江湖聽風: raises the next qi-yu chance; sometimes the rumour leads straight to a secret realm.</summary>
        internal bool GatherRumors(string worker = HeroId)
        {
            Story.rumorBonus = NextRumorBonus;
            return UnityEngine.Random.value < RumorDiscoveryChance(worker);
        }

        /// <summary>Chance that 山道巡訪 stumbles on a secret realm (幸運 / 身法 and gathered rumours help).</summary>
        internal float RealmChance => Mathf.Clamp((.22f + Story.rumorBonus) * AdventureFactor, .05f, .9f);

        internal float ConsumeRumors()
        {
            float bonus = Story.rumorBonus;
            Story.rumorBonus = 0;
            return bonus;
        }

        internal void GrantAp(int amount)
        {
            int cap = ApMax;
            ActionPoints = Mathf.Min(ActionPoints, cap);
            ActionPoints += Mathf.Clamp(amount, 0, cap - ActionPoints);
        }

        internal void Injure(int amount)
        {
            // v1126b 決定 1: 幸運 can soften a grave wound (2+ levels) into a light one.
            if (amount >= 2 && UnityEngine.Random.value < AttributeSystem.GraveToLight(AttributeValue("luk"))) amount = 1;
            Protagonist.injury = Mathf.Clamp(Protagonist.injury + amount, 0, 9);
        }

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
                power *= 1 + (TechValue("sword3", n => n.value) + TechValue("outer4", n => n.value)) / 100f + DiplomacyPowerBonus;
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
                    ? "　繳獲：" + StoreInventoryGear(MakeGear(GearSlots[UnityEngine.Random.Range(0, GearSlots.Length)], 1)) : "";
                return $"大捷！{SectLabel}攻破{place}，插上本派旗號（每月上繳銀兩 4、糧食 2）。" +
                    $"繳獲銀兩 {silver}、糧食 {food}，聲望 +3。{loot}";
            }
            Injure(1);
            ApplyEffects(new[] { new GameEffect { key = "reputation", value = -1 } });
            return $"攻討{place}失利，寨中守備比想像中頑強。主角負傷，聲望 −1。（勝算 {chance:P0}）";
        }
    }
}
