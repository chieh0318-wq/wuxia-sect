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
            public Text HpText, TargetArrow, Status;
            public Vector2 StandingPosition;
        }

        private RectTransform battleScreen;
        internal bool BattleOpen => battleScreen != null;

        internal void StartTurnBattle(GameState state, BattleRequest req, MonoBehaviour runner,
            Action<Func<IEnumerator>> captureRound = null)
        {
            CloseBattle();
            AudioManager.Music(req.MusicCue);

            var catalog = new BattleCatalog();
            var (players, enemies) = BattleSetup.Create(state, req, catalog);
            var model = new BattleModel(players, enemies, catalog, UnityEngine.Random.Range(0, int.MaxValue),
                () => MedicineCount("item_jiuzhuan") > 0 || MedicineCount("item_jinchuang") > 0,
                UseMedicine);
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
            Action onTargetChanged = null;
            Vector2[] pPositions = { new(-270, -54), new(-490, 18), new(-710, -38) };
            for (int i = 0; i < players.Count; i++)
                views.Add(players[i], SpawnBattleSprite(stage, players[i], pPositions[i], null));

            Vector2[] ePositions = { new(175, -50), new(390, 25), new(605, -42), new(805, 35) };
            for (int i = 0; i < enemies.Count; i++)
            {
                int idx = i;
                views.Add(enemies[i], SpawnBattleSprite(stage, enemies[i], ePositions[i], () =>
                {
                    if (busy || !enemies[idx].Alive) return;
                    targetIdx = idx;
                    onTargetChanged?.Invoke();
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

            bool fastMode = false, autoMode = false;
            string selectedAction = "attack";
            var actButtons = new Dictionary<string, (RectTransform rt, Outline outline, Text detail, Text title)>();
            var sideBar = Panel("SideTools", battleScreen, new(0, .5f), new(0, .5f), new(20, -135), new(106, 135), false);
            UiKit.Skin(sideBar.GetComponent<Image>(), "Lacquer");
            var sideV = sideBar.gameObject.AddComponent<VerticalLayoutGroup>();
            sideV.padding = new RectOffset(8, 8, 10, 10);
            sideV.spacing = 10;
            sideV.childControlWidth = sideV.childControlHeight = true;
            sideV.childForceExpandWidth = sideV.childForceExpandHeight = true;

            Action<string> choose = null;
            Action startAuto = null;
            var bagBtn = Button(sideBar, "背包", () => choose?.Invoke("item"));
            UiKit.Skin(bagBtn.GetComponent<Image>(), "Wood");
            RectTransform speedBtn = null, autoBtn = null;
            speedBtn = Button(sideBar, "x1 速度", () =>
            {
                fastMode = !fastMode;
                speedBtn.GetComponentInChildren<Text>().text = fastMode ? "x2 加速" : "x1 速度";
                UiKit.Skin(speedBtn.GetComponent<Image>(), fastMode ? "Selected" : "Wood");
            });
            UiKit.Skin(speedBtn.GetComponent<Image>(), "Wood");
            autoBtn = Button(sideBar, "自動", () =>
            {
                autoMode = !autoMode;
                UiKit.Skin(autoBtn.GetComponent<Image>(), autoMode ? "Selected" : "Wood");
                if (autoMode && !busy) startAuto?.Invoke();
            });
            UiKit.Skin(autoBtn.GetComponent<Image>(), "Wood");

            var turn = Panel("CurrentActor", battleScreen, Vector2.zero, Vector2.zero, new(570, 151), new(1502, 193), false);
            UiKit.Skin(turn.GetComponent<Image>(), "Lacquer");
            var actorLabel = Label(turn, "", 23, TextAnchor.MiddleCenter);
            actorLabel.color = UiKit.HeaderGold;
            Fill(actorLabel.rectTransform, 3);

            var warning = Panel("EnemyWindup", battleScreen, new(.5f, 1), new(.5f, 1), new(-470, -218), new(470, -88), false);
            UiKit.Skin(warning.GetComponent<Image>(), "Lacquer");
            var warningText = Label(warning, "", 23, TextAnchor.MiddleCenter);
            warningText.color = new Color(1f, .69f, .45f);
            Fill(warningText.rectTransform, 5);

            var actionDefs = new[]
            {
                ("attack", "act_attack", "+15 內"), ("skill", "act_skill", "25 內・流血"),
                ("ultimate", "act_ultimate", "50 內・全體"), ("interrupt", "act_skill", "15 內・打斷"),
                ("defend", "act_defend", "+25 內・減傷"), ("dodge", "act_defend", "+10 內・避一擊"),
                ("focus", "act_skill", "+15 內・專注"), ("item", "act_item", "靈藥療傷")
            };
            for (int i = 0; i < actionDefs.Length; i++)
            {
                var (id, icon, tip) = actionDefs[i];
                var move = id == "item" ? null : model.Skill(id);
                var btn = Button(battleScreen, "", () => choose?.Invoke(id));
                float x0 = 570 + i * 141;
                Anchor(btn, Vector2.zero, Vector2.zero, new(x0, 26), new(x0 + 132, 146));
                var outline = btn.gameObject.AddComponent<Outline>();
                outline.effectColor = UiKit.HeaderGold;
                outline.effectDistance = new Vector2(3, 3);

                var ic = Rect("ActIcon", btn);
                Anchor(ic, new(.5f, 1), new(.5f, 1), new(-28, -63), new(28, -7));
                var texture = UiKit.Icon(icon);
                if (texture != null) AddImage(ic, texture);

                string element = string.IsNullOrEmpty(move?.element) ? "" : $"〔{move.element}〕";
                var title = Label(btn, (move?.label ?? "道具") + element, 19, TextAnchor.MiddleCenter);
                title.fontStyle = FontStyle.Bold;
                Anchor(title.rectTransform, Vector2.zero, Vector2.right, new(2, 24), new(-2, 53));
                var detail = Label(btn, tip, 17, TextAnchor.MiddleCenter);
                Anchor(detail.rectTransform, Vector2.zero, Vector2.right, new(2, 2), new(-2, 25));
                actButtons[id] = (btn, outline, detail, title);
            }

            Action confirm = null;
            var goBtn = Button(battleScreen, "確認\n出招", () =>
            {
                confirm?.Invoke();
            });
            Anchor(goBtn, Vector2.right, Vector2.right, new(-202, 22), new(-24, 154));
            UiKit.Skin(goBtn.GetComponent<Image>(), "Selected");
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

            choose = SelectAction;
            startAuto = () => runner.StartCoroutine(Advance());
            confirm = () =>
            {
                if (!busy && model.Reason(selectedAction, Target()) == "")
                    runner.StartCoroutine(Advance(selectedAction));
            };
            onTargetChanged = () =>
            {
                if (model.Reason(selectedAction, Target()) != "") selectedAction = "attack";
                RefreshBattle();
            };
            RefreshBattle();
            captureRound?.Invoke(() => Advance(stopAfterRound: true));
            if (captureRound == null && !model.NeedsCommand) runner.StartCoroutine(Advance());

            void SelectAction(string id)
            {
                if (busy || model.Reason(id, Target()) != "") return;
                selectedAction = id;
                RefreshActionButtons();
            }

            BattleCombatant Target() => targetIdx < 0 ? null : enemies[targetIdx];

            int MedicineCount(string id) => LegacySystem.GetInventory(state.Story, id);

            (string name, int hp, int qi) UseMedicine()
            {
                if (MedicineCount("item_jiuzhuan") > 0)
                {
                    LegacySystem.AddInventory(state.Story, "item_jiuzhuan", -1);
                    return ("九轉回元丹", 120, 60);
                }

                LegacySystem.AddInventory(state.Story, "item_jinchuang", -1);
                return ("金創靈藥", 90, 0);
            }


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
                foreach (var (id, _, tip) in actionDefs)
                {
                    var (rt, outline, detail, title) = actButtons[id];
                    string reason = model.Reason(id, Target());
                    bool usable = !busy && reason == "";
                    bool active = usable && id == selectedAction;
                    rt.GetComponent<Button>().interactable = usable;
                    UiKit.Skin(rt.GetComponent<Image>(), active ? "Selected" : "Wood");
                    rt.GetComponent<Image>().color = reason == "" ? Color.white : new Color(.52f, .52f, .52f);
                    outline.enabled = active;
                    title.color = reason == "" ? UiKit.HeaderGold : new Color(.72f, .72f, .72f);
                    detail.text = reason == "" ? tip : reason;
                    detail.color = reason == "" ? CardMuted : new Color(.76f, .76f, .76f);
                }
                goBtn.GetComponent<Button>().interactable = !busy && model.Reason(selectedAction, Target()) == "";
            }

            void RefreshBattle()
            {
                RefreshTargetArrows();
                RefreshLeadHuds();
                RefreshActionButtons();
                roundLabel.text = $"第 {model.Round} 回合";
                var actor = model.Actor;
                actorLabel.text = !busy && model.NeedsCommand
                    ? $"{actor.name} 下指令　內力 {actor.qi}/{actor.maxQi}"
                    : "戰況結算";
                warningText.text = model.Warning;
                warning.gameObject.SetActive(warningText.text.Length > 0);
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
                    unitView.HpText.text = $"{u.name}〔{u.element}〕\n{u.hp}/{u.maxHp}";
                    unitView.Status.text = model.StatusText(u);
                    unitView.Status.transform.parent.gameObject.SetActive(unitView.Status.text.Length > 0);
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

            IEnumerator Advance(string command = null, bool stopAfterRound = false)
            {
                busy = true;
                var screen = battleScreen;
                int startingRound = model.Round;
                while (battleScreen == screen && !model.Finished)
                {
                    var target = Target();
                    if (model.NeedsCommand && command == null)
                    {
                        if (!autoMode && !stopAfterRound) break;
                        (command, target) = model.AutoCommand(target);
                    }

                    var events = model.Step(command, target);
                    command = null;
                    foreach (var entry in events)
                    {
                        if (battleScreen != screen) yield break;
                        if (entry.Actor != null)
                            yield return Lunge(entry.Actor, entry.Hit ? entry.Target : null);
                        if (battleScreen != screen) yield break;
                        PushLog(entry.Text);
                        if (entry.Float != null)
                            ShowFloatText(entry.Target, entry.Float, entry.Hit ? UiKit.HeaderGold : new Color(.65f, .95f, 1f));
                        if (entry.Cue != null) AudioManager.Play(entry.Cue);
                    }
                    RefreshBattle();
                    if (events.Count > 0) yield return new WaitForSecondsRealtime(fastMode || stopAfterRound ? .08f : .24f);
                    if (stopAfterRound && model.Round != startingRound) break;
                }

                if (battleScreen != screen) yield break;
                if (model.Finished)
                {
                    PushLog(model.Won ? "【凱旋】敵寇盡數伏誅，此戰大獲全勝！" : "【失利】我方力竭負傷，退回山門。");
                    yield return new WaitForSecondsRealtime(.55f);
                    if (battleScreen == screen) FinishBattle(model.Won);
                    yield break;
                }

                busy = false;
                selectedAction = "attack";
                RefreshBattle();
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
            var bossStandee = !unit.isPlayer && unit.portraitId == "leader_heisong"
                ? Resources.Load<Texture2D>("Standees/leader_heisong") : null;
            var tex = bossStandee != null ? bossStandee : unit.spriteId == null
                ? CharacterFactory.StandeeTexture(unit.portraitId, unit.gender, portraitFallback: true)
                : Resources.Load<Texture2D>("Battle/" + unit.spriteId);
            var raw = AddImage(spr, tex);
            if ((unit.spriteId == null || bossStandee != null) && tex != null)
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
            var plate = Panel("Nameplate", rt, Vector2.up, Vector2.one, new(0, -52), new(0, 2), onClick != null);
            UiKit.Skin(plate.GetComponent<Image>(), "Lacquer");
            var barBg = Rect("HpBar", plate);
            Anchor(barBg, new(.5f, 1), new(.5f, 1), new(-78, -49), new(78, -43));
            barBg.gameObject.AddComponent<Image>().color = new Color(.08f, .06f, .05f, .85f);
            var fill = Rect("Fill", barBg);
            Fill(fill, 2);
            var fImg = fill.gameObject.AddComponent<Image>();
            fImg.color = unit.isPlayer ? new Color(.35f, .82f, .38f) : new Color(.86f, .26f, .22f);
            fImg.type = Image.Type.Filled;
            fImg.fillMethod = Image.FillMethod.Horizontal;

            var nm = Label(plate, "", 18, TextAnchor.MiddleCenter);
            nm.color = Color.white;
            nm.resizeTextForBestFit = false;
            Anchor(nm.rectTransform, Vector2.up, Vector2.one, new(2, -42), new(-2, 0));

            var statusPlate = Panel("Statuses", rt, Vector2.zero, Vector2.right, new(1, -42), new(-1, 12), false);
            UiKit.Skin(statusPlate.GetComponent<Image>(), "Lacquer");
            var status = Label(statusPlate, "", 17, TextAnchor.MiddleCenter);
            status.color = new Color(.82f, .97f, .8f);
            status.resizeTextMinSize = 13;
            Fill(status.rectTransform, 3);

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
                Root = rt, Art = raw, HpFill = fImg, HpText = nm, Status = status,
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
