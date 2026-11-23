using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    public sealed partial class SectUI
    {
        private sealed class BattleUnitView
        {
            public RectTransform Root;
            public RawImage Art;
            public Image HpFill;
            public Text HpText, TargetArrow;
            public Vector2 StandingPosition;
        }

        private RectTransform battleScreen;
        internal bool BattleOpen => battleScreen != null;

        internal void StartTurnBattle(GameState state, BattleRequest req, MonoBehaviour runner,
            Action<Func<IEnumerator>> captureRound = null)
        {
            CloseBattle();
            AudioManager.Music(req.MusicCue);

            var (players, enemies) = BattleSetup.Create(state, req);
            var views = new Dictionary<BattleCombatant, BattleUnitView>();

            battleScreen = Panel("TurnBattle", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            battleScreen.GetComponent<Image>().color = Color.black;

            // Scenic arena background
            var bgTex = Resources.Load<Texture2D>("WorldMap/battle_arena_bg") ?? UiKit.Backdrop();
            var bgRect = Rect("ArenaBg", battleScreen);
            Fill(bgRect);
            AddImage(bgRect, bgTex);

            // ── TOP HUD: Left Player Lead | Center Round Medallion | Right Enemy Lead ──
            var lead = players[0];
            var enemyLead = enemies[0];
            (Image pHp, Text pHpTxt, Image pQi, Text pQiTxt) = BuildLeadHud(battleScreen, lead, false);
            (Image eHp, Text eHpTxt, Image eQi, Text eQiTxt) = BuildLeadHud(battleScreen, enemyLead, true, req.IsBoss);

            var roundBadge = Panel("RoundBadge", battleScreen, new(.5f, 1), new(.5f, 1), new(-120, -78), new(120, -12), false);
            UiKit.Skin(roundBadge.GetComponent<Image>(), "Jade");
            var roundLabel = Label(roundBadge, "第 1 回合", 26, TextAnchor.MiddleCenter);
            roundLabel.color = UiKit.HeaderGold;
            roundLabel.fontStyle = FontStyle.Bold;
            Fill(roundLabel.rectTransform);

            // ── CENTER STAGE: Left Player Party & Right Enemy Squad ──
            var stage = Rect("Stage", battleScreen);
            Anchor(stage, Vector2.zero, Vector2.one, new(0, 140), new(0, -110));

            bool busy = false;
            int targetIdx = 0;
            Vector2[] pPositions = { new(-270, -54), new(-490, 18), new(-710, -38) };
            for (int i = 0; i < players.Count; i++)
                views.Add(players[i], SpawnBattleSprite(stage, players[i], pPositions[i], null));

            Vector2[] ePositions = { new(175, -50), new(390, 25), new(605, -42), new(820, 35) };
            for (int i = 0; i < enemies.Count; i++)
            {
                int idx = i;
                views.Add(enemies[i], SpawnBattleSprite(stage, enemies[i], ePositions[i], () =>
                {
                    if (busy || !enemies[idx].Alive) return;
                    targetIdx = idx;
                    RefreshTargetArrows();
                }));
            }

            var logLines = new List<string>
            {
                $"【系統】遭遇 {req.enemyLeadName} 一夥！請點擊下方指令出招。",
                $"【系統】點擊右側敵人可切換集火目標。"
            };

            // ── BOTTOM-LEFT COMBAT LOG ──
            var logBox = Panel("CombatLog", battleScreen, Vector2.zero, Vector2.zero, new(20, 18), new(540, 152));
            UiKit.Skin(logBox.GetComponent<Image>(), "Lacquer");
            var viewport = Rect("Viewport", logBox);
            Fill(viewport, 12);
            viewport.gameObject.AddComponent<RectMask2D>();
            var logText = Label(viewport, "", 18, TextAnchor.UpperLeft);
            Anchor(logText.rectTransform, Vector2.up, Vector2.one, Vector2.zero, Vector2.zero);
            logText.rectTransform.pivot = new(.5f, 1);
            var scroll = logBox.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = logText.rectTransform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            logText.color = CardText;
            logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            logText.resizeTextForBestFit = false;
            UpdateLog();

            // ── LEFT VERTICAL TOOLBAR (背包 / x2加速 / 自動) ──
            bool fastMode = false, autoMode = false;
            string selectedAction = "attack";
            int roundNum = 1;
            var actButtons = new Dictionary<string, (RectTransform rt, Outline ol)>();
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

            RefreshTargetArrows();
            RefreshLeadHuds();
            captureRound?.Invoke(ExecuteRound);

            void PushLog(string line)
            {
                logLines.Add(line);
                if (logLines.Count > 40) logLines.RemoveAt(0);
                UpdateLog();
            }

            void UpdateLog()
            {
                logText.text = string.Join("\n", logLines);
                logText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, logText.preferredHeight + 4);
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = 0;
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

            void RefreshTargetArrows()
            {
                if (targetIdx < 0 || targetIdx >= enemies.Count || !enemies[targetIdx].Alive)
                    targetIdx = enemies.FindIndex(e => e.Alive);
                for (int i = 0; i < enemies.Count; i++)
                    views[enemies[i]].TargetArrow.enabled = i == targetIdx && enemies[i].Alive;
            }

            void RefreshLeadHuds()
            {
                pHp.fillAmount = Mathf.Clamp01(lead.hp / (float)Math.Max(1, lead.maxHp));
                pHpTxt.text = $"{Math.Max(0, lead.hp)}/{lead.maxHp}";
                pQi.fillAmount = Mathf.Clamp01(lead.qi / (float)Math.Max(1, lead.maxQi));
                pQiTxt.text = $"{Math.Max(0, lead.qi)}/{lead.maxQi}";
                eHp.fillAmount = Mathf.Clamp01(enemyLead.hp / (float)Math.Max(1, enemyLead.maxHp));
                eHpTxt.text = $"{Math.Max(0, enemyLead.hp)}/{enemyLead.maxHp}";
                eQi.fillAmount = Mathf.Clamp01(enemyLead.qi / (float)Math.Max(1, enemyLead.maxQi));
                eQiTxt.text = $"{Math.Max(0, enemyLead.qi)}/{enemyLead.maxQi}";
                foreach (var u in players.Concat(enemies))
                {
                    var unitView = views[u];
                    unitView.HpFill.fillAmount = (float)u.hp / u.maxHp;
                    unitView.HpText.text = $"{u.name}  {u.hp}/{u.maxHp}";
                    unitView.Art.color = u.Alive ? Color.white : new Color(.4f, .4f, .4f, .25f);
                }
            }

            void ShowFloatText(BattleCombatant u, string msg, Color color)
            {
                if (u == null || views[u].Root == null) return;
                var ft = Label(views[u].Root, msg, 30, TextAnchor.MiddleCenter);
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

            IEnumerator Lunge(BattleCombatant attacker, BattleCombatant defender)
            {
                float step = fastMode ? 0.08f : 0.16f;
                var moving = views[attacker];
                var hit = defender == null ? null : views[defender];
                if (moving.Root != null)
                    moving.Root.anchoredPosition = moving.StandingPosition + Vector2.right * (attacker.isPlayer ? 64f : -26f);
                if (hit?.Art != null) hit.Art.color = new Color(1f, .45f, .45f);
                yield return new WaitForSecondsRealtime(step);
                if (moving.Root != null) moving.Root.anchoredPosition = moving.StandingPosition;
                if (hit?.Art != null && defender.Alive) hit.Art.color = Color.white;
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
                            RefreshTargetArrows();
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
                    RefreshTargetArrows();
                    yield return new WaitForSecondsRealtime(wait);
                }

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

                busy = false;
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
                    if (captureRound == null) LegacySystem.AwardPoints(10);
                }
                req.onComplete?.Invoke(won);
            }
        }

        private (Image hpFill, Text hpText, Image qiFill, Text qiText) BuildLeadHud(RectTransform parent, BattleCombatant unit, bool right, bool boss = false)
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
            var nm = Label(box, $"{(boss ? "強敵・" : "")}{unit.name}　Lv.{unit.level}", 21, right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
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

        private BattleUnitView SpawnBattleSprite(RectTransform stage, BattleCombatant unit, Vector2 pos, Action onClick)
        {
            var rt = Rect("Unit_" + unit.name, stage);
            Anchor(rt, new(.5f, .5f), new(.5f, .5f), pos - new Vector2(95, 150), pos + new Vector2(95, 150));

            var spr = Rect("Sprite", rt);
            Anchor(spr, Vector2.zero, Vector2.one, new(0, 18), new(0, -34));
            var tex = unit.spriteId == null
                ? CharacterFactory.StandeeTexture(unit.portraitId, unit.gender, portraitFallback: true)
                : Resources.Load<Texture2D>("Battle/" + unit.spriteId);
            var raw = AddImage(spr, tex);
            if (unit.spriteId == null && tex != null)
            {
                float scale = Mathf.Min(190f / tex.width, 248f / tex.height);
                float halfWidth = tex.width * scale * .5f;
                Anchor(spr, new(.5f, 0), new(.5f, 0), new(-halfWidth, 18), new(halfWidth, 18 + tex.height * scale));
            }

            if (onClick != null)
            {
                var btn = rt.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => onClick());
                raw.raycastTarget = true;
            }

            // Overhead HP bar & nameplate
            var plate = Panel("Nameplate", rt, Vector2.up, Vector2.one, new(0, -37), new(0, 2), onClick != null);
            UiKit.Skin(plate.GetComponent<Image>(), "Lacquer");
            var barBg = Rect("HpBar", plate);
            Anchor(barBg, new(.5f, 1), new(.5f, 1), new(-78, -35), new(78, -27));
            barBg.gameObject.AddComponent<Image>().color = new Color(.08f, .06f, .05f, .85f);
            var fill = Rect("Fill", barBg);
            Fill(fill, 2);
            var fImg = fill.gameObject.AddComponent<Image>();
            fImg.color = unit.isPlayer ? new Color(.35f, .82f, .38f) : new Color(.86f, .26f, .22f);
            fImg.type = Image.Type.Filled;
            fImg.fillMethod = Image.FillMethod.Horizontal;

            var nm = Label(plate, "", 18, TextAnchor.MiddleCenter);
            nm.color = Color.white;
            nm.verticalOverflow = VerticalWrapMode.Truncate;
            Anchor(nm.rectTransform, Vector2.up, Vector2.one, new(2, -26), new(-2, 0));

            Text arrow = null;
            if (onClick != null)
            {
                arrow = Label(rt, "▼", 30, TextAnchor.MiddleCenter);
                arrow.color = UiKit.HeaderGold;
                arrow.resizeTextForBestFit = false;
                var outline = arrow.gameObject.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(2, -2);
                Anchor(arrow.rectTransform, new(.5f, 1), new(.5f, 1), new(-20, 0), new(20, 38));
                arrow.enabled = false;
            }

            return new BattleUnitView
            {
                Root = rt, Art = raw, HpFill = fImg, HpText = nm,
                TargetArrow = arrow, StandingPosition = rt.anchoredPosition
            };
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
