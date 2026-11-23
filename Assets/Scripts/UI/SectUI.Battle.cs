using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    internal sealed class BattleUnit
    {
        public string name, spriteId, portraitId, gender;
        public bool isPlayer;
        public int level, hp, maxHp, qi, maxQi, attack, speed;
        public float dodge;
        public bool defending;
        public bool Alive => hp > 0;
        public RectTransform root;
        public RawImage spriteImage;
        public Image hpFill;
        public Text hpText;
        public Image targetRing;
        public Vector2 basePos;
    }

    internal sealed class BattleRequest
    {
        public string title = "山寨伏擊";
        public string enemyLeadName = "山賊頭目";
        public int enemyCount = 3;
        public float difficulty = 1.0f;
        public Action<bool> onComplete;
    }

    public sealed partial class SectUI
    {
        private RectTransform battleScreen;
        internal bool BattleOpen => battleScreen != null;

        internal void StartTurnBattle(GameState state, BattleRequest req, MonoBehaviour runner,
            Action<IEnumerator> captureRound = null)
        {
            CloseBattle();
            AudioManager.Music("battle");

            var stats = state.HeroStats;
            var players = new List<BattleUnit>
            {
                new()
                {
                    name = state.Protagonist.name,
                    spriteId = state.Protagonist.gender == "女" ? "hero_f" : "hero_m",
                    portraitId = state.Protagonist.portrait ?? state.Protagonist.lookId,
                    gender = state.Protagonist.gender,
                    isPlayer = true,
                    level = 10 + state.Year * 2 + state.TechCount,
                    hp = stats.MaxHp, maxHp = stats.MaxHp,
                    qi = stats.MaxQi, maxQi = stats.MaxQi,
                    attack = Math.Max(28, stats.Attack),
                    speed = Math.Max(15, stats.Speed),
                    dodge = stats.Dodge
                }
            };
            // Bring up to 2 disciples into the party
            var allies = (state.Disciples ?? new List<DiscipleData>()).Take(2).ToArray();
            for (int i = 0; i < allies.Length; i++)
            {
                var d = allies[i];
                int dHp = Mathf.RoundToInt(stats.MaxHp * 0.72f);
                int dQi = Mathf.RoundToInt(stats.MaxQi * 0.7f);
                players.Add(new BattleUnit
                {
                    name = d.name,
                    spriteId = d.gender == "女" ? "ally_f" : "ally_m",
                    portraitId = d.portrait,
                    gender = d.gender,
                    isPlayer = true,
                    level = 8 + state.Year,
                    hp = dHp, maxHp = dHp, qi = dQi, maxQi = dQi,
                    attack = Math.Max(22, Mathf.RoundToInt(stats.Attack * 0.75f)),
                    speed = Math.Max(12, stats.Speed - 2),
                    dodge = 0.08f
                });
            }

            // Build enemy squad
            var enemies = new List<BattleUnit>();
            float diff = Mathf.Clamp(req.difficulty, 0.7f, 1.8f);
            int bossHp = Mathf.RoundToInt(stats.MaxHp * 1.15f * diff);
            enemies.Add(new BattleUnit
            {
                name = req.enemyLeadName,
                spriteId = "enemy_boss",
                portraitId = "npc_am01",
                gender = "男",
                isPlayer = false,
                level = 10 + Mathf.RoundToInt(diff * 5),
                hp = bossHp, maxHp = bossHp, qi = 100, maxQi = 100,
                attack = Mathf.RoundToInt(stats.Attack * 0.72f * diff),
                speed = Math.Max(10, stats.Speed - 3),
                dodge = 0.05f
            });
            string[] minionSprites = { "enemy_scout", "enemy_axe", "enemy_assassin" };
            string[] minionNames = { "山賊斥候", "山寨悍匪", "黑衣刺客" };
            int count = Mathf.Clamp(req.enemyCount, 2, 4);
            for (int i = 1; i < count; i++)
            {
                int mHp = Mathf.RoundToInt(stats.MaxHp * 0.58f * diff);
                enemies.Add(new BattleUnit
                {
                    name = minionNames[(i - 1) % minionNames.Length],
                    spriteId = minionSprites[(i - 1) % minionSprites.Length],
                    portraitId = "npc_am02",
                    gender = "男",
                    isPlayer = false,
                    level = 8 + Mathf.RoundToInt(diff * 4),
                    hp = mHp, maxHp = mHp, qi = 60, maxQi = 60,
                    attack = Mathf.RoundToInt(stats.Attack * 0.52f * diff),
                    speed = Math.Max(10, stats.Speed - 4 + i),
                    dodge = 0.06f
                });
            }

            battleScreen = Panel("TurnBattle", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            battleScreen.GetComponent<Image>().color = Color.black;

            // Scenic arena background
            var bgTex = Resources.Load<Texture2D>("WorldMap/battle_arena_bg") ?? UiKit.Backdrop();
            var bgRect = Rect("ArenaBg", battleScreen);
            Fill(bgRect);
            AddImage(bgRect, bgTex);

            // ── TOP HUD: Left Player Lead | Center Round Medallion | Right Enemy Lead ──
            var lead = players[0];
            var boss = enemies[0];
            (Image pHp, Text pHpTxt, Image pQi, Text pQiTxt) = BuildLeadHud(battleScreen, lead, false);
            (Image eHp, Text eHpTxt, Image eQi, Text eQiTxt) = BuildLeadHud(battleScreen, boss, true);

            var roundBadge = Panel("RoundBadge", battleScreen, new(.5f, 1), new(.5f, 1), new(-120, -78), new(120, -12), false);
            UiKit.Skin(roundBadge.GetComponent<Image>(), "Jade");
            var roundLabel = Label(roundBadge, "第 1 回合", 26, TextAnchor.MiddleCenter);
            roundLabel.color = UiKit.HeaderGold;
            roundLabel.fontStyle = FontStyle.Bold;
            Fill(roundLabel.rectTransform);

            // ── CENTER STAGE: Left Player Party & Right Enemy Squad ──
            var stage = Rect("Stage", battleScreen);
            Anchor(stage, Vector2.zero, Vector2.one, new(0, 140), new(0, -110));

            int targetIdx = 0;
            Vector2[] pPositions = { new(-340, -55), new(-520, -12), new(-660, 48) };
            for (int i = 0; i < players.Count; i++)
                SpawnBattleSprite(stage, players[i], pPositions[i % pPositions.Length], null);

            Vector2[] ePositions = { new(430, 12), new(260, -58), new(210, 42), new(610, -45) };
            for (int i = 0; i < enemies.Count; i++)
            {
                int idx = i;
                SpawnBattleSprite(stage, enemies[i], ePositions[i % ePositions.Length], () =>
                {
                    if (!enemies[idx].Alive) return;
                    targetIdx = idx;
                    RefreshTargetRings();
                });
            }

            // ── LEFT VERTICAL TOOLBAR (背包 / x2加速 / 自動) ──
            bool fastMode = false, autoMode = false, busy = false;
            string selectedAction = "attack";
            int roundNum = 1;
            var actButtons = new Dictionary<string, (RectTransform rt, Outline ol)>();
            var logLines = new List<string>
            {
                $"【系統】遭遇 {req.enemyLeadName} 一夥！請點擊下方指令出招。",
                $"【系統】點擊右側敵人可切換集火目標。"
            };
            Text logText = null;

            var sideBar = Panel("SideTools", battleScreen, new(0, .5f), new(0, .5f), new(20, -135), new(106, 135), false);
            UiKit.Skin(sideBar.GetComponent<Image>(), "Lacquer");
            var sideV = sideBar.gameObject.AddComponent<VerticalLayoutGroup>();
            sideV.padding = new RectOffset(8, 8, 10, 10);
            sideV.spacing = 10;
            sideV.childControlWidth = sideV.childControlHeight = true;
            sideV.childForceExpandWidth = sideV.childForceExpandHeight = true;

            Text speedLbl = null;
            RectTransform speedBtn = null, autoBtn = null;
            var bagBtn = Button(sideBar, "背包", () => { selectedAction = "item"; RefreshActionButtons(); });
            UiKit.Skin(bagBtn.GetComponent<Image>(), "Wood");
            speedBtn = Button(sideBar, "x1 速度", () =>
            {
                fastMode = !fastMode;
                if (speedLbl != null) speedLbl.text = fastMode ? "x2 加速" : "x1 速度";
                UiKit.Skin(speedBtn.GetComponent<Image>(), fastMode ? "Selected" : "Wood");
            });
            UiKit.Skin(speedBtn.GetComponent<Image>(), "Wood");
            speedLbl = speedBtn.GetComponentInChildren<Text>();

            autoBtn = Button(sideBar, "自動", () =>
            {
                autoMode = !autoMode;
                UiKit.Skin(autoBtn.GetComponent<Image>(), autoMode ? "Selected" : "Wood");
                if (autoMode && !busy) runner.StartCoroutine(ExecuteRound());
            });
            UiKit.Skin(autoBtn.GetComponent<Image>(), "Wood");

            // ── BOTTOM-LEFT COMBAT LOG ──
            var logBox = Panel("CombatLog", battleScreen, Vector2.zero, Vector2.zero, new(20, 18), new(540, 152), false);
            UiKit.Skin(logBox.GetComponent<Image>(), "Lacquer");
            logText = Label(logBox, string.Join("\n", logLines), 18, TextAnchor.UpperLeft);
            logText.color = CardText;
            logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            logText.resizeTextForBestFit = false;
            Fill(logText.rectTransform, 12);

            // ── BOTTOM-CENTER 5 CIRCULAR ICON BUTTONS + GOLDEN EXECUTE BUTTON ──
            var actionDefs = new[]
            {
                ("attack", "攻擊", "act_attack", "普攻 +15內"),
                ("skill", "技能", "act_skill", "25內・重擊"),
                ("ultimate", "特技", "act_ultimate", "50內・群攻"),
                ("defend", "防禦", "act_defend", "減傷+回氣"),
                ("item", "道具", "act_item", "靈藥療傷"),
            };
            for (int i = 0; i < actionDefs.Length; i++)
            {
                var (id, lbl, iconName, tip) = actionDefs[i];
                var btn = Button(battleScreen, "", () =>
                {
                    if (busy) return;
                    selectedAction = id;
                    RefreshActionButtons();
                });
                btn.GetComponentInChildren<Text>().text = "";
                float x0 = 580 + i * 134;
                Anchor(btn, Vector2.zero, Vector2.zero, new(x0, 26), new(x0 + 114, 146));
                UiKit.Skin(btn.GetComponent<Image>(), "Wood");
                var ol = btn.gameObject.AddComponent<Outline>();
                ol.effectColor = UiKit.HeaderGold;
                ol.effectDistance = new Vector2(3, 3);
                ol.enabled = id == selectedAction;

                var ic = Rect("ActIcon", btn);
                Anchor(ic, new(.5f, 1), new(.5f, 1), new(-36, -80), new(36, -8));
                var icTex = UiKit.Icon(iconName);
                if (icTex != null) AddImage(ic, icTex);

                var nameLbl = Label(btn, lbl, 20, TextAnchor.MiddleCenter);
                nameLbl.color = UiKit.HeaderGold;
                nameLbl.fontStyle = FontStyle.Bold;
                Anchor(nameLbl.rectTransform, Vector2.zero, Vector2.right, new(2, 22), new(-2, 46));

                var subLbl = Label(btn, tip, 18, TextAnchor.MiddleCenter);
                subLbl.color = CardMuted;
                Anchor(subLbl.rectTransform, Vector2.zero, Vector2.right, new(2, 2), new(-2, 24));
                actButtons[id] = (btn, ol);
            }

            // Prominent Golden Action Medallion (開始戰鬥 / 出招)
            var goBtn = Button(battleScreen, "開始\n出招", () =>
            {
                if (!busy) runner.StartCoroutine(ExecuteRound());
            });
            Anchor(goBtn, Vector2.right, Vector2.right, new(-206, 22), new(-24, 154));
            UiKit.Skin(goBtn.GetComponent<Image>(), "Selected");
            var goOutline = goBtn.gameObject.AddComponent<Outline>();
            goOutline.effectColor = UiKit.HeaderGold;
            goOutline.effectDistance = new Vector2(3, 3);
            var goTxt = goBtn.GetComponentInChildren<Text>();
            goTxt.fontSize = goTxt.resizeTextMaxSize = 30;
            goTxt.color = UiKit.HeaderGold;
            goTxt.fontStyle = FontStyle.Bold;

            // Retreat button (top-right below enemy HUD)
            var fleeBtn = Button(battleScreen, "撤退", () =>
            {
                if (busy) return;
                FinishBattle(false);
            });
            Anchor(fleeBtn, Vector2.one, Vector2.one, new(-124, -148), new(-20, -104));
            UiKit.Skin(fleeBtn.GetComponent<Image>(), "Wood");

            RefreshTargetRings();
            RefreshLeadHuds();
            captureRound?.Invoke(ExecuteRound());

            void PushLog(string line)
            {
                logLines.Add(line);
                if (logLines.Count > 5) logLines.RemoveAt(0);
                if (logText != null) logText.text = string.Join("\n", logLines);
            }

            void RefreshActionButtons()
            {
                foreach (var kv in actButtons)
                {
                    bool active = kv.Key == selectedAction;
                    UiKit.Skin(kv.Value.rt.GetComponent<Image>(), active ? "Selected" : "Wood");
                    kv.Value.ol.enabled = active;
                }
            }

            void RefreshTargetRings()
            {
                if (targetIdx < 0 || targetIdx >= enemies.Count || !enemies[targetIdx].Alive)
                    targetIdx = enemies.FindIndex(e => e.Alive);
                for (int i = 0; i < enemies.Count; i++)
                    if (enemies[i].targetRing != null)
                        enemies[i].targetRing.enabled = i == targetIdx && enemies[i].Alive;
            }

            void RefreshLeadHuds()
            {
                pHp.fillAmount = Mathf.Clamp01(lead.hp / (float)Math.Max(1, lead.maxHp));
                pHpTxt.text = $"{Math.Max(0, lead.hp)}/{lead.maxHp}";
                pQi.fillAmount = Mathf.Clamp01(lead.qi / (float)Math.Max(1, lead.maxQi));
                pQiTxt.text = $"{Math.Max(0, lead.qi)}/{lead.maxQi}";
                eHp.fillAmount = Mathf.Clamp01(boss.hp / (float)Math.Max(1, boss.maxHp));
                eHpTxt.text = $"{Math.Max(0, boss.hp)}/{boss.maxHp}";
                eQi.fillAmount = Mathf.Clamp01(boss.qi / (float)Math.Max(1, boss.maxQi));
                eQiTxt.text = $"{Math.Max(0, boss.qi)}/{boss.maxQi}";
                foreach (var u in players.Concat(enemies))
                {
                    if (u.hpFill != null) u.hpFill.fillAmount = Mathf.Clamp01(u.hp / (float)Math.Max(1, u.maxHp));
                    if (u.hpText != null) u.hpText.text = $"{u.name} ({Math.Max(0, u.hp)})";
                    if (u.spriteImage != null) u.spriteImage.color = u.Alive ? Color.white : new Color(.4f, .4f, .4f, .25f);
                }
            }

            void ShowFloatText(BattleUnit u, string msg, Color color)
            {
                if (u?.root == null) return;
                var ft = Label(u.root, msg, 30, TextAnchor.MiddleCenter);
                ft.color = color;
                ft.fontStyle = FontStyle.Bold;
                var sh = ft.gameObject.AddComponent<Shadow>();
                sh.effectColor = Color.black;
                sh.effectDistance = new Vector2(2, -2);
                Anchor(ft.rectTransform, new(.5f, 1), new(.5f, 1), new(-90, -20), new(90, 30));
                runner.StartCoroutine(FloatAndDestroy(ft.rectTransform, fastMode ? 0.35f : 0.65f));
            }

            IEnumerator FloatAndDestroy(RectTransform rt, float dur)
            {
                float t = 0;
                Vector2 start = rt.anchoredPosition;
                while (t < dur && rt != null)
                {
                    t += Time.unscaledDeltaTime;
                    rt.anchoredPosition = start + Vector2.up * (54f * (t / dur));
                    yield return null;
                }
                if (rt != null) UnityEngine.Object.Destroy(rt.gameObject);
            }

            IEnumerator Lunge(BattleUnit attacker, BattleUnit defender)
            {
                float step = fastMode ? 0.08f : 0.16f;
                Vector2 dir = attacker.isPlayer ? Vector2.right * 90f : Vector2.left * 90f;
                if (attacker.root != null) attacker.root.anchoredPosition = attacker.basePos + dir;
                if (defender?.spriteImage != null) defender.spriteImage.color = new Color(1f, .45f, .45f);
                yield return new WaitForSecondsRealtime(step);
                if (attacker.root != null) attacker.root.anchoredPosition = attacker.basePos;
                if (defender?.spriteImage != null && defender.Alive) defender.spriteImage.color = Color.white;
            }

            IEnumerator ExecuteRound()
            {
                busy = true;
                float wait = fastMode ? 0.14f : 0.30f;
                foreach (var u in players.Concat(enemies)) u.defending = false;

                var order = players.Concat(enemies).Where(u => u.Alive).OrderByDescending(u => u.speed).ToList();
                foreach (var actor in order)
                {
                    if (!actor.Alive) continue;
                    if (!players.Any(p => p.Alive) || !enemies.Any(e => e.Alive)) break;

                    if (actor.isPlayer)
                    {
                        string act = actor == lead ? (autoMode ? (actor.qi >= 50 ? "ultimate" : actor.qi >= 25 ? "skill" : "attack") : selectedAction)
                            : (actor.qi >= 25 ? "skill" : "attack");

                        if (act == "defend")
                        {
                            actor.defending = true;
                            int heal = Mathf.RoundToInt(actor.maxHp * 0.08f);
                            actor.hp = Mathf.Min(actor.maxHp, actor.hp + heal);
                            actor.qi = Mathf.Min(actor.maxQi, actor.qi + 25);
                            PushLog($"【防禦】{actor.name} 運氣護體，回氣 +25、回血 +{heal}！");
                            ShowFloatText(actor, $"防禦 +{heal}", new Color(.55f, .92f, 1f));
                            AudioManager.Play("page");
                        }
                        else if (act == "item")
                        {
                            int jz = LegacySystem.GetInventory(state.Story, "item_jiuzhuan");
                            int jc = LegacySystem.GetInventory(state.Story, "item_jinchuang");
                            if (jz > 0)
                            {
                                LegacySystem.AddInventory(state.Story, "item_jiuzhuan", -1);
                                actor.hp = Mathf.Min(actor.maxHp, actor.hp + 120);
                                actor.qi = Mathf.Min(actor.maxQi, actor.qi + 60);
                                PushLog($"【道具】{actor.name} 服下九轉回元丹（剩 {jz - 1}），氣血 +120、內力 +60！");
                                ShowFloatText(actor, "+120 氣血", new Color(.45f, 1f, .55f));
                            }
                            else if (jc > 0)
                            {
                                LegacySystem.AddInventory(state.Story, "item_jinchuang", -1);
                                actor.hp = Mathf.Min(actor.maxHp, actor.hp + 90);
                                PushLog($"【道具】{actor.name} 敷上金創靈藥（剩 {jc - 1}），氣血 +90！");
                                ShowFloatText(actor, "+90 氣血", new Color(.45f, 1f, .55f));
                            }
                            else
                            {
                                int heal = 45;
                                actor.hp = Mathf.Min(actor.maxHp, actor.hp + heal);
                                PushLog($"【調息】背包靈藥已盡，{actor.name} 運功調息回復 {heal} 氣血。");
                                ShowFloatText(actor, $"+{heal} 氣血", new Color(.45f, 1f, .55f));
                            }
                            AudioManager.Play("complete");
                        }
                        else if (act == "ultimate" && actor.qi >= 50)
                        {
                            actor.qi -= 50;
                            var targets = enemies.Where(e => e.Alive).ToList();
                            yield return runner.StartCoroutine(Lunge(actor, targets.FirstOrDefault()));
                            int totalDmg = 0;
                            foreach (var t in targets)
                            {
                                int dmg = Mathf.RoundToInt(actor.attack * UnityEngine.Random.Range(1.25f, 1.50f));
                                t.hp = Math.Max(0, t.hp - dmg);
                                totalDmg += dmg;
                                ShowFloatText(t, $"-{dmg}", new Color(1f, .78f, .25f));
                            }
                            PushLog($"【特技】{actor.name} 施展《崤山劍陣》，橫掃全場共造成 {totalDmg} 點傷害！");
                            AudioManager.Play("complete");
                        }
                        else
                        {
                            RefreshTargetRings();
                            var target = targetIdx >= 0 && targetIdx < enemies.Count && enemies[targetIdx].Alive
                                ? enemies[targetIdx] : enemies.FirstOrDefault(e => e.Alive);
                            if (target != null)
                            {
                                bool useSkill = act == "skill" && actor.qi >= 25;
                                if (useSkill) actor.qi -= 25;
                                else actor.qi = Mathf.Min(actor.maxQi, actor.qi + 15);
                                float mult = useSkill ? UnityEngine.Random.Range(1.55f, 1.80f) : UnityEngine.Random.Range(0.95f, 1.15f);
                                int dmg = Math.Max(8, Mathf.RoundToInt(actor.attack * mult));
                                target.hp = Math.Max(0, target.hp - dmg);
                                yield return runner.StartCoroutine(Lunge(actor, target));
                                ShowFloatText(target, $"-{dmg}", useSkill ? new Color(1f, .62f, .22f) : new Color(1f, .92f, .70f));
                                PushLog(useSkill
                                    ? $"【技能】{actor.name} 施展精妙劍招重創 {target.name}，造成 {dmg} 點傷害！"
                                    : $"【攻擊】{actor.name} 揮劍斬向 {target.name}，造成 {dmg} 點傷害。");
                                AudioManager.Play("page");
                            }
                        }
                    }
                    else
                    {
                        var alivePlayers = players.Where(p => p.Alive).ToList();
                        var target = alivePlayers[UnityEngine.Random.Range(0, alivePlayers.Count)];
                        bool dodged = UnityEngine.Random.value < target.dodge;
                        yield return runner.StartCoroutine(Lunge(actor, dodged ? null : target));
                        if (dodged)
                        {
                            ShowFloatText(target, "閃避！", new Color(.65f, .95f, 1f));
                            PushLog($"【閃避】{target.name} 身形一晃，靈巧避開了 {actor.name} 的攻勢！");
                        }
                        else
                        {
                            int raw = Mathf.RoundToInt(actor.attack * UnityEngine.Random.Range(0.85f, 1.12f));
                            if (target.defending) raw = Mathf.Max(4, raw / 2);
                            target.hp = Math.Max(0, target.hp - raw);
                            ShowFloatText(target, $"-{raw}", new Color(1f, .38f, .35f));
                            PushLog($"【敵襲】{actor.name} 猛攻 {target.name}，造成 {raw} 點傷害。");
                        }
                    }

                    RefreshLeadHuds();
                    RefreshTargetRings();
                    yield return new WaitForSecondsRealtime(wait);
                }

                busy = false;
                if (!enemies.Any(e => e.Alive))
                {
                    PushLog("【凱旋】敵寇盡數伏誅，此戰大獲全勝！");
                    yield return new WaitForSecondsRealtime(0.55f);
                    FinishBattle(true);
                    yield break;
                }
                if (!players.Any(p => p.Alive))
                {
                    PushLog("【失利】我方力竭負傷，暫且退回山門重整旗鼓。");
                    yield return new WaitForSecondsRealtime(0.55f);
                    FinishBattle(false);
                    yield break;
                }

                roundNum++;
                if (roundLabel != null) roundLabel.text = $"第 {roundNum} 回合";
                if (autoMode && battleScreen != null)
                    runner.StartCoroutine(ExecuteRound());
            }

            void FinishBattle(bool won)
            {
                CloseBattle();
                AudioManager.Music("courtyard");
                if (won)
                {
                    AudioManager.Jingle("victory");
                    LegacySystem.AwardPoints(10);
                }
                req.onComplete?.Invoke(won);
            }
        }

        private (Image hpFill, Text hpText, Image qiFill, Text qiText) BuildLeadHud(RectTransform parent, BattleUnit unit, bool right)
        {
            var box = Panel(right ? "EnemyHud" : "PlayerHud", parent,
                right ? Vector2.one : Vector2.up, right ? Vector2.one : Vector2.up,
                right ? new(-470, -104) : new(20, -104), right ? new(-20, -14) : new(470, -14), false);
            UiKit.Skin(box.GetComponent<Image>(), "Lacquer");

            var faceFrame = Panel("Portrait", box, right ? Vector2.one : Vector2.up, right ? Vector2.one : Vector2.up,
                right ? new(-80, -82) : new(10, -82), right ? new(-10, -8) : new(80, -8), false);
            faceFrame.GetComponent<Image>().color = UiKit.AntiqueGold;
            var face = Rect("Face", faceFrame);
            Fill(face, 3);
            var tex = PortraitService.Resolve(unit.portraitId, unit.gender);
            var raw = AddImage(face, tex);
            FitCover(raw, tex, 64f / 68f);

            int xL = right ? 16 : 92;
            int xR = right ? -92 : -16;
            var nm = Label(box, $"{unit.name}　Lv.{unit.level}", 21, right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
            nm.color = UiKit.HeaderGold;
            nm.fontStyle = FontStyle.Bold;
            Anchor(nm.rectTransform, Vector2.up, Vector2.one, new(xL, -34), new(xR, -6));

            var (hpFill, hpTxt) = BattleBar(box, xL, -58, xR, -38, new Color(.82f, .24f, .20f));
            var (qiFill, qiTxt) = BattleBar(box, xL, -82, xR, -62, new Color(.22f, .54f, .86f));
            return (hpFill, hpTxt, qiFill, qiTxt);
        }

        private (Image fill, Text txt) BattleBar(RectTransform parent, float xL, float yB, float xR, float yT, Color color)
        {
            var bg = Rect("BarBg", parent);
            Anchor(bg, Vector2.up, Vector2.one, new(xL, yB), new(xR, yT));
            bg.gameObject.AddComponent<Image>().color = new Color(.08f, .06f, .05f, .85f);
            var f = Rect("Fill", bg);
            Fill(f, 2);
            var img = f.gameObject.AddComponent<Image>();
            img.color = color;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillAmount = 1f;
            var t = Label(bg, "", 18, TextAnchor.MiddleCenter);
            t.color = Color.white;
            t.fontStyle = FontStyle.Bold;
            Fill(t.rectTransform);
            return (img, t);
        }

        private void SpawnBattleSprite(RectTransform stage, BattleUnit unit, Vector2 pos, Action onClick)
        {
            var rt = Rect("Unit_" + unit.name, stage);
            Anchor(rt, new(.5f, .5f), new(.5f, .5f), pos - new Vector2(130, 150), pos + new Vector2(130, 150));
            unit.root = rt;
            unit.basePos = rt.anchoredPosition;

            // Target selection ring underneath enemy
            var ring = Rect("TargetRing", rt);
            Anchor(ring, new(.5f, 0), new(.5f, 0), new(-72, 12), new(72, 34));
            var ringImg = ring.gameObject.AddComponent<Image>();
            ringImg.color = new Color(1f, .82f, .28f, .70f);
            ringImg.enabled = false;
            unit.targetRing = ringImg;

            var spr = Rect("Sprite", rt);
            Anchor(spr, Vector2.zero, Vector2.one, new(0, 18), new(0, -34));
            var tex = Resources.Load<Texture2D>("Battle/" + unit.spriteId);
            var raw = AddImage(spr, tex);
            if (tex != null) raw.color = Color.white;
            unit.spriteImage = raw;

            if (onClick != null)
            {
                var btn = rt.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => onClick());
                raw.raycastTarget = true;
            }

            // Overhead HP bar & nameplate
            var barBg = Rect("HpBar", rt);
            Anchor(barBg, new(.5f, 1), new(.5f, 1), new(-74, -26), new(74, -14));
            barBg.gameObject.AddComponent<Image>().color = new Color(.08f, .06f, .05f, .85f);
            var fill = Rect("Fill", barBg);
            Fill(fill, 2);
            var fImg = fill.gameObject.AddComponent<Image>();
            fImg.color = unit.isPlayer ? new Color(.35f, .82f, .38f) : new Color(.86f, .26f, .22f);
            fImg.type = Image.Type.Filled;
            fImg.fillMethod = Image.FillMethod.Horizontal;
            unit.hpFill = fImg;

            var nm = Label(rt, $"{unit.name} ({unit.hp})", 18, TextAnchor.MiddleCenter);
            nm.color = Color.white;
            var sh = nm.gameObject.AddComponent<Shadow>();
            sh.effectColor = Color.black;
            Anchor(nm.rectTransform, new(.5f, 1), new(.5f, 1), new(-110, -12), new(110, 14));
            unit.hpText = nm;
        }

        internal void CloseBattle()
        {
            if (battleScreen == null) return;
            battleScreen.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(battleScreen.gameObject);
            battleScreen = null;
        }
    }
}
