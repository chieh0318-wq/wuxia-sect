using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    /// <summary>One tactical option on a secret-realm floor, tested against a protagonist attribute.</summary>
    internal sealed class RealmChoice
    {
        public string label, attr, text, success, failure;
    }

    internal sealed class RealmStage
    {
        public string title, text;
        public RealmChoice[] choices;
    }

    /// <summary>A themed three-floor 江湖秘境 (qi-yu dungeon) with its signature treasure slot and martial line.</summary>
    internal sealed class RealmData
    {
        public string id, name, intro, gearSlot, line;
        public RealmStage[] stages;
        /// <summary>v1124 C15: realms.json scene id (background) and the floor whose guardian can be fought for real.</summary>
        public string scene;
        public int bossStage = -1;
        public RealmBattle boss;
    }

    /// <summary>An in-progress delve: floor index, successful checks and the running log.</summary>
    internal sealed class RealmRun
    {
        public RealmData realm;
        public int stage, score;
        public readonly List<string> log = new();
    }

    internal static class SecretRealms
    {
        internal static readonly string[] StageTitles =
        {
            "第一層（初探秘境）", "第二層（機關／護法考驗）", "第三層（秘境深處・首領與秘寶）"
        };

        private static RealmChoice C(string label, string attr, string text, string success, string failure) =>
            new() { label = label, attr = attr, text = text, success = success, failure = failure };

        internal static readonly RealmData[] All =
        {
            new()
            {
                id = "sword_tomb", name = "《前朝藏劍古塚》", gearSlot = "武器", line = "sword", scene = "realm_sword_tomb", bossStage = 2,
                boss = new RealmBattle { lead = "披甲劍俑", portrait = "npc_am01", standee = "Battle/enemy_boss", count = 3, difficulty = 1.15f, boss = true, minions = new[] { "石俑", "石俑" } },
                intro = "崤函古道旁的荒塚，傳說埋著前朝劍聖的佩劍。塚口的石碑上刻著八個字：「劍在人在，劍亡人亡」。",
                stages = new[]
                {
                    new RealmStage { text = "墓道兩側的石俑手持銹劍，地上散著前人的白骨。甬道深處隱約有風聲，像是有人在低聲吟哦。",
                        choices = new[]
                        {
                            C("憑身法潛行", "agi", "貼著墓壁、踩著石俑的影子前進", "你如一縷輕煙穿過甬道，石俑始終沒有察覺。", "腳下踩碎一根枯骨，石俑齊齊轉頭，你帶著幾道劍痕狼狽衝過。"),
                            C("憑武力強闖", "str", "一劍劈開攔路的石俑", "劍光過處石屑紛飛，你硬生生殺出一條路。", "石俑比想像中堅硬，震得你虎口迸裂。")
                        } },
                    new RealmStage { text = "劍閣中央立著九柄劍，地面刻滿機關紋路。牆上題著：「九劍擇一，錯者萬箭穿心。」",
                        choices = new[]
                        {
                            C("參悟劍意擇劍", "wit", "觀察九劍上的銹跡與劍穗，推敲劍聖心意", "你取下最樸拙的那柄木劍——機關無聲退去。", "你選錯了劍，萬箭齊發，好在只擦傷了臂膀。"),
                            C("運內力震開機關", "qi", "以內力貫入地面紋路，強行壓住機關", "真氣沿紋路流轉，機關一處接一處熄滅。", "真氣逆衝，機關失控，你被氣浪掀翻在地。")
                        } },
                    new RealmStage { text = "古塚最深處，一具披甲劍俑緩緩站起，手中長劍寒光不減當年。劍俑胸口嵌著一塊玉牌：「勝我者得劍」。",
                        choices = new[]
                        {
                            C("以劍會劍・正面決鬥", "str", "拔劍迎上，以崤山劍法硬撼劍俑", "三十招後劍俑轟然倒地，古劍落入你手中。", "劍俑劍招精妙，你負傷退走，只搶下劍鞘裡的半卷劍譜。"),
                            C("尋其破綻・一擊制勝", "luk", "等待劍俑招式間那一瞬的停頓", "天意站在你這邊——劍俑收劍時露出空門，你一劍點中玉牌。", "破綻稍縱即逝，你失手被逼退，只撿到一些散落的陪葬。")
                        } }
                }
            },
            new()
            {
                id = "jade_cave", name = "《太白寒玉洞窟》", gearSlot = "飾品", line = "neigong", scene = "realm_ice_cave", bossStage = 1,
                boss = new RealmBattle { lead = "雪白冰蟒", portrait = "beast_ice_python", standee = "Realms/beast_ice_python", count = 1, difficulty = 1.1f, boss = true },
                intro = "太白山積雪終年不化，獵戶說山腹裡有一座冰洞，洞中寒玉能讓習武之人內力倍增，可進去的人多半沒再出來。",
                stages = new[]
                {
                    new RealmStage { text = "洞口寒氣逼人，冰壁光滑如鏡，腳下是看不見底的冰縫。",
                        choices = new[]
                        {
                            C("以根骨硬抗寒氣", "con", "運氣護住心脈，一步步走過冰縫", "你周身冒著白氣，穩穩走過了冰縫。", "寒氣入體，你凍得嘴唇發紫，好不容易才撐過去。"),
                            C("施展輕功飛渡", "agi", "踏冰借力，凌空掠過冰縫", "你足尖在冰柱上連點三下，輕巧落地。", "冰面一滑，你險些墜入冰縫，扭傷了腳踝。")
                        } },
                    new RealmStage { text = "冰洞深處盤踞著一條通體雪白的冰蟒，牠守著一汪寒潭，潭底隱約透出青光。",
                        choices = new[]
                        {
                            C("以內力逼退冰蟒", "qi", "掌心吐勁，以純陽真氣逼退冰蟒", "冰蟒畏懼你掌心的熱力，緩緩退入暗處。", "冰蟒吐出寒霧，你真氣一滯，被尾巴掃中。"),
                            C("以魅力安撫靈獸", "cha", "收起兵刃，輕聲細語靠近冰蟒", "冰蟒竟低下頭，任你從身旁走過。", "冰蟒並不領情，你只得狼狽閃避。")
                        } },
                    new RealmStage { text = "寒潭底部，一塊人頭大的寒玉散發著幽光。潭水冷得刺骨，水面還結著一層薄冰。",
                        choices = new[]
                        {
                            C("潛入寒潭取玉", "con", "憋一口長氣，直潛潭底", "你抱著寒玉破冰而出，一股清涼真氣湧入四肢百骸。", "潭水太冷，你只撬下了一角碎玉便不得不浮起。"),
                            C("參透寒玉紋理", "wit", "盤坐潭邊，觀寒玉光華流轉，悟其運氣之法", "你在潭邊坐了一夜，悟出寒玉真氣的運行路線，再取玉便不費吹灰之力。", "參悟不透，只記下了些許運氣口訣。")
                        } }
                }
            },
            new()
            {
                id = "black_wind", name = "《黑風嶺密庫》", gearSlot = "衣袍", line = "fist", scene = "realm_bandit_vault", bossStage = 2,
                boss = new RealmBattle { lead = "黑風老寨主", portrait = "npc_em06", count = 3, difficulty = 1.1f, boss = true, minions = new[] { "黑風殘匪", "黑風殘匪" } },
                intro = "黑風嶺的山賊被官軍剿滅之後，他們十年劫掠來的財寶卻始終沒找著。有人說，密庫就在嶺後的斷崖底下。",
                stages = new[]
                {
                    new RealmStage { text = "斷崖下的山洞外，還有三五個漏網的嘍囉在守著，篝火上烤著野兔。",
                        choices = new[]
                        {
                            C("趁夜色摸進去", "agi", "等嘍囉打盹時從背後掠過", "你悄無聲息地溜進洞中，嘍囉們還在打呼。", "一個嘍囉忽然醒來，你只得出手打暈他們，還挨了一刀。"),
                            C("亮出名號喝退", "cha", "報上門派名號，嚇退這群烏合之眾", "嘍囉們聽說是名門正派，嚇得四散奔逃。", "嘍囉們不吃這一套，你不得不動手，費了一番工夫。")
                        } },
                    new RealmStage { text = "密庫大門是一道千斤閘，閘旁的石壁上有一排銅鈕，旁邊還躺著一具被機關射死的盜墓賊。",
                        choices = new[]
                        {
                            C("憑武力抬起千斤閘", "str", "雙臂運勁，硬生生將千斤閘抬起", "你大喝一聲，千斤閘緩緩升起，塵土簌簌落下。", "閘門太重，你力竭鬆手，被壓傷了肩膀，好在擠了進去。"),
                            C("推敲銅鈕機關", "wit", "從盜墓賊的屍體推敲出錯誤的按法", "按下第三、第七、第一個銅鈕，閘門應聲而開。", "按錯了一個鈕，毒煙噴出，你掩鼻衝了進去。")
                        } },
                    new RealmStage { text = "密庫裡堆滿箱籠，黑風寨的老寨主竟還活著——他守著財寶十年，已經半瘋，一雙鐵掌練得黝黑發亮。",
                        choices = new[]
                        {
                            C("硬接鐵掌", "str", "以掌對掌，與老寨主拚個高下", "你一掌震退老寨主，他頹然坐倒，把財寶與一件寶甲都交給了你。", "鐵掌勢大力沉，你吐血退開，只搶到幾隻小箱子。"),
                            C("以巧勁周旋", "luk", "繞著箱籠遊走，引老寨主撞上機關", "老寨主一掌拍在自家機關上，被困在鐵籠裡。", "機關沒有觸發，你只得且戰且退。")
                        } }
                }
            },
            new()
            {
                id = "immortal_go", name = "《仙人弈棋殘局》", gearSlot = "冠帽", line = "qinggong", scene = "realm_immortal_go", bossStage = 1,
                boss = new RealmBattle { lead = "終南白猿", portrait = "beast_white_ape", standee = "Realms/beast_white_ape", count = 1, difficulty = 1.0f },
                intro = "終南山深處有一塊石坪，坪上刻著一局殘棋。樵夫說每逢月圓，便有兩位白衣老者在此對弈，破局者可得仙緣。",
                stages = new[]
                {
                    new RealmStage { text = "通往石坪的山路被雲霧遮蔽，岔路一條接一條，走著走著總會回到原處。",
                        choices = new[]
                        {
                            C("憑悟性辨識陣法", "wit", "觀察雲霧流向，找出迷陣的生門", "你看出這是一座八卦迷陣，從生門一路走了出去。", "陣法變化莫測，你繞了大半夜才摸到出口，累得筋疲力盡。"),
                            C("憑運氣隨意而行", "luk", "閉上眼睛，跟著直覺走", "也許真有仙緣——你閉眼走了三百步，睜眼時已站在石坪邊。", "直覺帶你走進了荊棘叢，渾身劃傷才繞出來。")
                        } },
                    new RealmStage { text = "石坪旁守著一隻白猿，牠擋在路中，比劃著要你與牠過招。",
                        choices = new[]
                        {
                            C("以輕功與白猿追逐", "agi", "與白猿在松枝間追逐騰挪", "你與白猿在樹梢追逐了一炷香，牠拍手大樂，讓開了路。", "白猿身法比你快得多，戲弄得你摔了好幾跤。"),
                            C("以根骨硬接猿拳", "con", "站穩馬步，任由白猿捶打", "你紋絲不動，白猿打累了，佩服地讓開。", "猿拳力道驚人，你被打得眼冒金星。")
                        } },
                    new RealmStage { text = "月上中天，兩位白衣老者果然出現。其中一位指著殘局：「後生，這一子，你下在哪裡？」",
                        choices = new[]
                        {
                            C("苦思破局", "wit", "凝神看棋，推演百步之後的變化", "你落下一子，殘局豁然開朗。老者大笑，將一頂星冠與一卷步法相贈。", "你想了一夜仍無頭緒，老者搖頭，只留下一枚棋子。"),
                            C("坦言不會下棋", "cha", "老老實實說自己不會下棋，只想請教武學", "老者撫鬚：「不欺心，便是破局。」竟將步法傾囊相授。", "老者嫌你無趣，拂袖而去，只留下半句口訣。")
                        } }
                }
            }
        };

        internal static RealmData Pick() => All[UnityEngine.Random.Range(0, All.Length)];

        internal static string AttrName(string key)
        {
            int index = AttributeSystem.Index(key);
            return index < 0 ? key : AttributeSystem.Names[index];
        }

        /// <summary>Check odds: higher attribute and a stronger best martial line help; deeper floors are harder.</summary>
        internal static float Chance(GameState state, RealmChoice choice, int stage)
        {
            int value = AttributeSystem.Value(state.Protagonist.attrs, choice.attr);
            int martial = RankSystem.Index(RankSystem.Martial(state.Protagonist.lines ?? Array.Empty<SkillLine>()));
            return Mathf.Clamp(.48f + (value - AttributeSystem.Base) * .045f + martial * .02f - stage * .06f +
                state.Story.insightChance, .1f, .95f);
        }

        /// <summary>Resolves a floor: success adds to the score, failure costs a wound.</summary>
        internal static bool Resolve(GameState state, RealmRun run, RealmChoice choice)
        {
            bool success = UnityEngine.Random.value < Chance(state, choice, run.stage);
            if (success) run.score++;
            else state.Injure(1);
            run.log.Add($"{StageTitles[run.stage]}　{choice.label}（{AttrName(choice.attr)}）{(success ? "成功" : "失手")}：" +
                (success ? choice.success : choice.failure));
            run.stage++;
            return success;
        }

        /// <summary>Grants the treasure: gear graded by the score, silver, reputation and martial progress.</summary>
        internal static List<string> Claim(GameState state, RealmRun run)
        {
            var lines = new List<string>();
            int score = run.score;
            var gear = GameState.MakeGear(run.realm.gearSlot, Mathf.Clamp(score, 0, 3));
            lines.Add("秘寶：" + state.EquipOrStore(gear));
            int silver = 15 + 15 * score, reputation = 2 + 2 * score;
            state.ApplyEffects(new[]
            {
                new GameEffect { key = "silver", value = silver }, new GameEffect { key = "reputation", value = reputation }
            });
            lines.Add($"銀兩 +{silver}、聲望 +{reputation}");
            var line = Array.Find(state.Protagonist.lines ?? Array.Empty<SkillLine>(), entry => entry.id == run.realm.line);
            string category = RankSystem.Categories.FirstOrDefault(entry => entry.id == run.realm.line)?.name ?? "武學";
            if (line != null && score >= 2)
            {
                RankSystem.Raise(line, 1, score >= 3 ? 1 : 0);
                lines.Add($"{category}精進：{line.rank}／{line.cap}" + (score >= 3 ? "（潛力上限亦提升）" : ""));
            }
            else if (line != null)
            {
                line.progress += 1.5f;
                lines.Add($"{category}略有所得（修習進度 +1.5）");
            }
            state.Story.realmsCleared++;
            return lines;
        }
    }
}
