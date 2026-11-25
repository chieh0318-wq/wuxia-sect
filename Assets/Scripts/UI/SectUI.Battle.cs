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
            public RawImage Art, Shadow;
            public Image HpFill;
            public Text HpText, TargetArrow;
            public RectTransform Statuses;
            public string StatusKey;
            public Vector2 StandingPosition;
        }

        private RectTransform battleScreen;
        private int battlePreviousCanvasOrder;
        internal bool BattleOpen => battleScreen != null;

        internal void StartTurnBattle(GameState state, BattleRequest req, MonoBehaviour runner,
            Action<Func<IEnumerator>> captureRound = null, Action<Action<string>> captureInspect = null, Action<BattleEvent> captureImpact = null, Func<int, string> captureCommand = null, int? captureSeed = null)
        {
            CloseBattle();
            AudioManager.Music(req.MusicCue);

            var catalog = new BattleCatalog();
            var (players, enemies) = BattleSetup.Create(state, req, catalog);
            int seed = captureSeed ?? UnityEngine.Random.Range(0, int.MaxValue);
            var model = new BattleModel(players, enemies, catalog, seed,
                () => MedicineCount("item_jiuzhuan") > 0 || MedicineCount("item_jinchuang") > 0,
                UseMedicine);
            var views = new Dictionary<BattleCombatant, BattleUnitView>();

            var battleCanvas = canvas.GetComponent<Canvas>();
            battlePreviousCanvasOrder = battleCanvas.sortingOrder;
            battleCanvas.sortingOrder = Mathf.Max(70, battlePreviousCanvasOrder);
            battleScreen = Panel("TurnBattle", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            battleScreen.GetComponent<Image>().color = Color.black;

            string backdrop = req.ground switch
            {
                BattleGround.MountainGate => "battle_ground_mountain_gate",
                BattleGround.Fortification => "battle_ground_fortification",
                _ => "battle_ground_road"
            };
            var ground = Resources.Load<Texture2D>("Battle/" + backdrop);
            var bgRect = Rect("ArenaBg", battleScreen);
            Fill(bgRect);
            var background = AddImage(bgRect, ground ?? Resources.Load<Texture2D>("WorldMap/battle_arena_bg"));
            if (ground == null)
            {
                // Frame the original courtyard so its paving reaches both rows.
                background.uvRect = new Rect(.10f, 0, .80f, .80f);
                background.color = new Color(.82f, .79f, .75f);
            }

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
            Fill(stage);
            var floatLayer = Rect("FloatingBattleFeedback", battleScreen);
            Fill(floatLayer);
            var playedBigMoves = new HashSet<string>();
            var floatingFeedback = new Dictionary<BattleCombatant, RectTransform>();

            bool busy = false;
            int targetIdx = 0;
            Action onTargetChanged = null;
            Vector2[] pPositions = { new(-270, .275f), new(-490, .325f), new(-710, .275f) };
            for (int i = 0; i < players.Count; i++)
                views.Add(players[i], SpawnBattleSprite(stage, players[i], pPositions[i], null));

            Vector2[] ePositions = { new(175, .275f), new(390, .325f), new(605, .275f), new(805, .325f) };
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

            foreach (var view in views.Values.OrderByDescending(view => view.Root.anchorMin.y))
                view.Root.SetAsLastSibling();

            var logLines = new List<string>
            {
                $"【系統】遭遇 {req.enemyLeadName} 一夥！請點擊下方指令出招。",
                $"【系統】點擊右側敵人可切換集火目標。"
            };

            // ── BOTTOM-LEFT COMBAT LOG ──
            var logBox = Panel("CombatLog", battleScreen, Vector2.zero, Vector2.zero, new(20, 18), new(386, 211));
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

            int speed = 1;
            bool autoMode = false;
            string selectedAction = "attack";
            string inspectedAction = selectedAction;
            int activeTab = 0, movePage = 0;
            var sideBar = Panel("SideTools", battleScreen, new(0, .5f), new(0, .5f), new(20, -135), new(106, 135), false);
            UiKit.Skin(sideBar.GetComponent<Image>(), "Lacquer");
            var sideV = sideBar.gameObject.AddComponent<VerticalLayoutGroup>();
            sideV.padding = new RectOffset(8, 8, 10, 10);
            sideV.spacing = 10;
            sideV.childControlWidth = sideV.childControlHeight = true;
            sideV.childForceExpandWidth = sideV.childForceExpandHeight = true;

            Action<string> choose = null;
            Action<int> changeTab = null;
            Action startAuto = null;
            var bagBtn = Button(sideBar, "背包", () => choose?.Invoke("item"));
            UiKit.Skin(bagBtn.GetComponent<Image>(), "Wood");
            RectTransform speedBtn = null, autoBtn = null;
            speedBtn = Button(sideBar, "x1 速度", () =>
            {
                speed = speed % 3 + 1;
                speedBtn.GetComponentInChildren<Text>().text = speed == 1 ? "x1 速度" : $"x{speed} 加速";
                UiKit.Skin(speedBtn.GetComponent<Image>(), speed > 1 ? "Selected" : "Wood");
            });
            UiKit.Skin(speedBtn.GetComponent<Image>(), "Wood");
            autoBtn = Button(sideBar, "自動", () =>
            {
                autoMode = !autoMode;
                UiKit.Skin(autoBtn.GetComponent<Image>(), autoMode ? "Selected" : "Wood");
                if (autoMode && !busy) startAuto?.Invoke();
            });
            UiKit.Skin(autoBtn.GetComponent<Image>(), "Wood");

            var commandPanel = Panel("Commands", battleScreen, Vector2.zero, Vector2.zero,
                new(398, 18), new(1900, 294), false);
            UiKit.Skin(commandPanel.GetComponent<Image>(), "Lacquer");
            var actorLabel = BattleLabel(commandPanel, "", 21, TextAnchor.MiddleLeft,
                new(18, 230), new(508, 268));
            actorLabel.color = UiKit.HeaderGold;

            var description = BattleLabel(commandPanel, "", 22, TextAnchor.MiddleLeft,
                new(785, 20), new(1349, 220));
            description.color = CardText;

            var tabNames = new[] { "攻擊", "招式", "防禦", "道具", "逃跑" };
            var tabs = new RectTransform[tabNames.Length];
            for (int i = 0; i < tabs.Length; i++)
            {
                int tab = i;
                tabs[i] = Button(commandPanel, tabNames[i], () => changeTab?.Invoke(tab));
                float y = 184 - 42 * i;
                Anchor(tabs[i], Vector2.zero, Vector2.zero, new(18, y), new(166, y + 38));
            }

            var actionDefs = new (string id, int tab, int slot, string seal)[]
            {
                ("attack", 0, 0, "普"), ("skill", 1, 0, "藝"),
                ("ultimate", 1, 1, "藝"), ("interrupt", 1, 2, "拆"),
                ("item", 3, 0, "藥"), ("focus", 2, 2, "氣"),
                ("defend", 2, 0, "護"), ("dodge", 2, 1, "避")
            };
            var actButtons = new Dictionary<string, (RectTransform root, Outline border, Text seal, Text title, Text detail)>();
            foreach (var (id, _, slot, seal) in actionDefs)
            {
                var btn = Button(commandPanel, "", () => choose?.Invoke(id));
                btn.GetComponent<Button>().transition = Selectable.Transition.None;
                float y = 165 - slot * 56;
                Anchor(btn, Vector2.zero, Vector2.zero, new(190, y), new(760, y + 49));
                var hover = btn.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                var enter = new UnityEngine.EventSystems.EventTrigger.Entry
                    { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ =>
                {
                    string move = ResolveMove(id);
                    if (move != null) DescribeMove(move);
                });
                hover.triggers.Add(enter);
                var exit = new UnityEngine.EventSystems.EventTrigger.Entry
                    { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
                exit.callback.AddListener(_ => DescribeMove(inspectedAction));
                hover.triggers.Add(exit);
                var border = btn.gameObject.AddComponent<Outline>();
                border.effectColor = UiKit.HeaderGold;
                border.effectDistance = new Vector2(2, 2);
                var glyph = BattleLabel(btn, seal, 29, TextAnchor.MiddleCenter, new(8, 4), new(50, 45));
                // v1126d: 49px row = title (25–48) over a 20px detail line (1–25) that stays inside the row.
                var title = BattleLabel(btn, ActionName(id), 22, TextAnchor.MiddleLeft, new(60, 25), new(550, 48));
                var detail = BattleLabel(btn, "", 20, TextAnchor.MiddleLeft, new(60, 1), new(550, 25));
                UnityEngine.Object.DestroyImmediate(detail.GetComponent<CjkKinsoku>()); // one-line cell: no line-break guard
                actButtons[id] = (btn, border, glyph, title, detail);
            }

            Action<int> pageMoves = null;
            var previousMoves = Button(commandPanel, "‹", () => pageMoves?.Invoke(-1));
            Anchor(previousMoves, Vector2.zero, Vector2.zero, new(190, 14), new(238, 48));
            var nextMoves = Button(commandPanel, "›", () => pageMoves?.Invoke(1));
            Anchor(nextMoves, Vector2.zero, Vector2.zero, new(336, 14), new(384, 48));
            var pageLabel = BattleLabel(commandPanel, "", 19, TextAnchor.MiddleCenter, new(240, 14), new(334, 48));

            var timeline = Panel("TurnOrder", battleScreen, Vector2.up, Vector2.one,
                new(20, -159), new(-148, -109), false);
            UiKit.Skin(timeline.GetComponent<Image>(), "Lacquer");
            var orderTitle = BattleLabel(timeline, "行動序", 22, TextAnchor.MiddleCenter, new(4, 0), new(110, 50));
            orderTitle.color = UiKit.HeaderGold;
            var orderLabels = new Text[players.Count + enemies.Count];
            for (int i = 0; i < orderLabels.Length; i++)
            {
                float x = 116 + i * 228;
                var card = Panel("Turn", timeline, Vector2.zero, Vector2.zero, new(x, 6), new(x + 220, 44), false);
                orderLabels[i] = Label(card, "", 20, TextAnchor.MiddleCenter);
                Fill(orderLabels[i].rectTransform, 3);
            }

            var intents = Panel("EnemyIntents", battleScreen, Vector2.up, Vector2.one,
                new(20, -245), new(-148, -165), false);
            UiKit.Skin(intents.GetComponent<Image>(), "Lacquer");
            var intentTitle = BattleLabel(intents, "敵勢", 22, TextAnchor.MiddleCenter, new(4, 0), new(110, 80));
            intentTitle.color = UiKit.HeaderGold;
            var intentLabels = new Text[enemies.Count];
            for (int i = 0; i < intentLabels.Length; i++)
            {
                float x = 116 + i * 407;
                var card = Panel("Intent", intents, Vector2.zero, Vector2.zero, new(x, 7), new(x + 397, 73), false);
                intentLabels[i] = Label(card, "", 22, TextAnchor.MiddleCenter);
                Fill(intentLabels[i].rectTransform, 3);
            }

            Action confirm = null;
            var goBtn = Button(battleScreen, "確認\n出招", () =>
            {
                confirm?.Invoke();
            });
            Anchor(goBtn, Vector2.right, Vector2.right, new(-154, 34), new(-32, 195));
            UiKit.Skin(goBtn.GetComponent<Image>(), "Selected");
            var goTxt = goBtn.GetComponentInChildren<Text>();
            goTxt.fontSize = goTxt.resizeTextMaxSize = 30;
            goTxt.color = UiKit.HeaderGold;
            goTxt.fontStyle = FontStyle.Bold;


            pageMoves = PageMoves;
            choose = SelectAction;
            changeTab = ChangeTab;
            startAuto = () => runner.StartCoroutine(Advance());
            confirm = () =>
            {
                if (selectedAction != null && CommandReason(selectedAction) == "")
                    runner.StartCoroutine(Advance(selectedAction));
            };
            onTargetChanged = () =>
            {
                if (selectedAction != null) selectedAction = CommandReason(inspectedAction) == "" ? inspectedAction : null;
                RefreshBattle();
            };
            RefreshBattle();
            captureRound?.Invoke(() => Advance(stopAfterRound: true));
            captureInspect?.Invoke(SelectAction);
            void CancelMove()
            {
                inspectedAction = "attack";
                selectedAction = null;
                RefreshActionButtons();
            }

            void PageMoves(int direction)
            {
                movePage = Mathf.Clamp(movePage + direction, 0, Mathf.Max(0, (model.KnownMoves.Length - 1) / 2));
                inspectedAction = "attack";
                selectedAction = null;
                RefreshActionButtons();
            }

            string ResolveMove(string id) => id switch
            {
                "skill" => model.KnownMoves.ElementAtOrDefault(movePage * 2),
                "ultimate" => model.KnownMoves.ElementAtOrDefault(movePage * 2 + 1),
                _ => id
            };
            if (captureRound == null && !model.NeedsCommand) runner.StartCoroutine(Advance());

            void ChangeTab(int tab)
            {
                if (tab == 4)
                {
                    if (!busy) FinishBattle(false);
                    return;
                }
                activeTab = tab;
                string id = actionDefs.First(entry => entry.tab == tab).id;
                if (ResolveMove(id) == null) id = "interrupt";
                SelectAction(id);
            }

            void SelectAction(string id)
            {
                activeTab = actionDefs.First(entry => entry.id == id).tab;
                string move = ResolveMove(id);
                if (move == null) return;
                inspectedAction = move;
                selectedAction = CommandReason(move) == "" ? move : null;
                RefreshActionButtons();
            }

            string ActionName(string id) => id == "item" ? "道具" : model.Skill(id).label;
            string CommandReason(string id)
            {
                string reason = model.Reason(id, Target());
                return busy && reason.Length == 0 ? "戰況結算中" : reason;
            }

            BattleCombatant Target() => targetIdx < 0 ? null : enemies[targetIdx];

            int MedicineCount(string id) => LegacySystem.GetInventory(state.Story, id);

            (string name, int hp, int qi) UseMedicine()
            {
                var medicine = GameState.SectMedicines.First(entry => MedicineCount(entry.id) > 0);
                LegacySystem.AddInventory(state.Story, medicine.id, -1);
                return (EffectText.ItemName(medicine.id), medicine.hp, medicine.qi);
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
                var screen = battleScreen;
                UiBack.Pop(screen);
                if (selectedAction != null && screen != null)
                    UiBack.Push(CancelMove, () => battleScreen == screen && !busy, screen);

                for (int i = 0; i < tabs.Length; i++)
                    UiKit.Skin(tabs[i].GetComponent<Image>(), i == activeTab ? "Selected" : "Wood");

                int pages = (model.KnownMoves.Length + 1) / 2;
                previousMoves.gameObject.SetActive(activeTab == 1 && pages > 1);
                nextMoves.gameObject.SetActive(activeTab == 1 && pages > 1);
                pageLabel.text = activeTab == 1 && pages > 1 ? $"{movePage + 1} / {pages}" : "";
                int visibleRow = 0;
                foreach (var (id, tab, _, _) in actionDefs)
                {
                    var (root, border, seal, title, detail) = actButtons[id];
                    string move = ResolveMove(id);
                    bool shown = tab == activeTab && move != null;
                    root.gameObject.SetActive(shown);
                    if (!shown) continue;
                    float rowY = 165 - visibleRow++ * 56;
                    Anchor(root, Vector2.zero, Vector2.zero, new(190, rowY), new(760, rowY + 49));
                    title.text = ActionName(move);
                    if (id is "skill" or "ultimate") seal.text = "藝";
                    string reason = CommandReason(move);
                    bool usable = reason.Length == 0;
                    bool inspected = move == inspectedAction;
                    var image = root.GetComponent<Image>();
                    if (usable)
                        UiKit.Skin(image, inspected ? "Selected" : "Wood");
                    else
                    {
                        image.sprite = null;
                        image.type = Image.Type.Simple;
                        image.color = new Color(.19f, .19f, .19f);
                    }

                    border.enabled = inspected;
                    border.effectColor = usable ? UiKit.HeaderGold : new Color(.62f, .62f, .62f);
                    seal.color = title.color = usable ? UiKit.HeaderGold : new Color(.65f, .65f, .65f);
                    string effect = ActionEffect(move).Split('。', '\n')[0];
                    detail.text = usable ? $"{ActionCost(move)}　{effect}" : $"{ActionCost(move)}　{reason}";
                    detail.color = usable ? CardText : new Color(.86f, .86f, .86f);
                }

                DescribeMove(inspectedAction);
                goBtn.GetComponent<Button>().interactable =
                    selectedAction != null && CommandReason(selectedAction) == "";
            }

            void DescribeMove(string move)
            {
                string reason = CommandReason(move);
                description.text = $"{ActionName(move)}　｜　{ActionCost(move)}\n" +
                    $"五行 {ActionElement(move)}　・　冷卻 無\n{ActionEffect(move)}" + StrikeLine(move) +
                    (reason.Length == 0 ? "" : $"\n{reason}");
            }

            // v1126e #1: which figure a strike uses and which defence it meets (外功 → 外防, 內功 → 內防).
            string StrikeLine(string move)
            {
                if (move == "item" || model.Finished || model.Actor == null || model.Skill(move).power <= 0) return "";
                var actor = model.Actor;
                bool inner = model.IsInnerMove(move);
                int power = inner && actor.internalAttack > 0 ? actor.internalAttack : actor.attack;
                if (move == "attack") power += actor.talentBasicAttack;
                var target = Target();
                string against = target == null ? (inner ? "內防" : "外防")
                    : $"{target.name}{(inner ? "內防" : "外防")} {(inner ? target.innerDefense : target.outerDefense)}";
                return $"\n{(inner ? "內功" : "外功")} {power} 對 {against}";
            }

            string ActionCost(string id)
            {
                if (id == "item") return "消耗庫房丹藥 1 份";
                var skill = model.Skill(id);
                if (skill.qi > 0) return $"消耗 {skill.qi} 內力";
                return skill.restoreQi > 0 ? $"回復 {skill.restoreQi} 內力" : "內力消耗 0";
            }

            string ActionElement(string id)
            {
                string element = id == "item" ? null : model.Skill(id).element;
                return string.IsNullOrEmpty(element) ? "無" : element;
            }

            string ActionEffect(string id)
            {
                if (id == "item")
                {
                    var medicine = GameState.SectMedicines.FirstOrDefault(entry => MedicineCount(entry.id) > 0);
                    return medicine.id == null ? "庫房已無可用丹藥。" :
                        $"{EffectText.ItemName(medicine.id)}：氣血 +{medicine.hp}、內力 +{medicine.qi}。";
                }
                if (id == "interrupt") return "單體傷害 ×" + model.Skill(id).power.ToString("0.00") + "，打斷蓄勢。";
                var move = model.Skill(id);
                string damage = move.power > 0 ? $"{(move.all ? "全體" : "單體")}傷害 ×{move.power:0.00}" : "";
                if (string.IsNullOrEmpty(move.status)) return damage;
                var status = model.Status(move.status);
                if (move.power > 0) return $"{damage}，附加{status.label}{status.turns}回。";
                string effect = id switch
                {
                    "defend" => $"所受傷害降低 {(1 - status.incoming) * 100:0}%",
                    "dodge" => "閃避下一次攻擊",
                    _ => $"攻擊提高 {(status.attack - 1) * 100:0}%，回合開始回復 {status.qi} 內力"
                };
                return $"{status.label}{status.turns}回，{effect}。";
            }

            void RefreshBattle()
            {
                RefreshTargetArrows();
                RefreshLeadHuds();
                RefreshActionButtons();
                roundLabel.text = $"第 {model.Round} 回合";
                var actor = model.Finished ? null : model.Actor;
                actorLabel.text = !busy && model.NeedsCommand
                    ? $"{actor.name} 下指令　內力 {actor.qi}/{actor.maxQi}"
                    : "戰況結算";
                RefreshTurnOrder();
                RefreshIntents();
            }

            void RefreshTurnOrder()
            {
                var remaining = model.Finished ? Array.Empty<BattleCombatant>() : model.RemainingTurns.ToArray();
                for (int i = 0; i < orderLabels.Length; i++)
                {
                    var label = orderLabels[i];
                    label.transform.parent.gameObject.SetActive(i < remaining.Length);
                    if (i >= remaining.Length) continue;
                    var unit = remaining[i];
                    label.text = $"{(i == 0 ? "當前 " : "")}{(unit.isPlayer ? "我" : "敵")}・{unit.name}";
                    label.color = i == 0 ? UiKit.HeaderGold : Color.white;
                    label.transform.parent.GetComponent<Image>().color = unit.isPlayer
                        ? new Color(.13f, .35f, .28f) : new Color(.43f, .23f, .20f);
                    label.fontStyle = i == 0 ? FontStyle.Bold : FontStyle.Normal;
                }
            }

            void RefreshIntents()
            {
                int i = 0;
                foreach (var enemy in enemies.Where(unit => unit.Alive))
                {
                    var label = intentLabels[i++];
                    var (move, target, unavailable) = model.PreviewIntent(enemy);
                    bool charging = enemy.intent != null;
                    bool danger = move?.all == true && unavailable == null;
                    string phase = charging ? "蓄勢" : "預計";
                    string focus = move?.all == true ? "全隊危險" : target?.name;
                    string action = unavailable ?? (move.power == 0
                        ? $"{phase}・{move.label} → 自身" : $"{phase}・{move.label} → {focus}");
                    label.text = $"{enemy.name}\n{action}";
                    label.color = danger || charging ? new Color(1f, .80f, .65f) : CardText;
                    label.transform.parent.GetComponent<Image>().color = danger
                        ? new Color(.49f, .16f, .13f) : new Color(.19f, .17f, .15f);
                    label.transform.parent.gameObject.SetActive(true);
                }
                for (; i < intentLabels.Length; i++)
                    intentLabels[i].transform.parent.gameObject.SetActive(false);
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
                    RefreshStatuses(unitView, u, model);
                    unitView.Art.color = u.Alive ? Color.white : new Color(.4f, .4f, .4f, .25f);
                    unitView.Shadow.color = new Color(.17f, .13f, .10f, u.Alive ? .84f : .19f);
                }
            }

            void ShowFloatText(BattleCombatant u, string msg, Color color, bool critical)
            {
                if (u == null || views[u].Root == null) return;
                if (floatingFeedback.TryGetValue(u, out var previous) && previous != null)
                    UnityEngine.Object.Destroy(previous.gameObject);
                var unitView = views[u];
                var anchor = new Vector2(.5f, unitView.Root.anchorMin.y);
                var position = unitView.StandingPosition + Vector2.up * 315;
                var ft = Label(floatLayer, critical ? "暴擊 " + msg : msg, critical ? 49 : 36, TextAnchor.MiddleCenter);
                ft.raycastTarget = false;
                ft.color = color;
                ft.fontStyle = FontStyle.Bold;
                var sh = ft.gameObject.AddComponent<Shadow>();
                sh.effectColor = Color.black;
                sh.effectDistance = new Vector2(2, -2);
                Anchor(ft.rectTransform, anchor, anchor, position + new Vector2(-160, -30),
                    position + new Vector2(160, 30));
                floatingFeedback[u] = ft.rectTransform;
                runner.StartCoroutine(FloatAndDestroy(ft.rectTransform, .70f / Mathf.Sqrt(speed)));
            }

            IEnumerator FloatAndDestroy(RectTransform rt, float dur)
            {
                float t = 0;
                Vector2 start = rt.anchoredPosition;
                while (t < dur && rt != null)
                {
                    rt.anchoredPosition = start + Vector2.up * (54f * Mathf.Clamp01(t / dur));
                    yield return null;
                    t += Mathf.Min(Time.unscaledDeltaTime, .05f);
                }
                if (rt != null) UnityEngine.Object.Destroy(rt.gameObject);
            }

            IEnumerator Lunge(BattleCombatant attacker)
            {
                float step = .16f / speed;
                var moving = views[attacker];
                if (moving.Root != null)
                    moving.Root.anchoredPosition = moving.StandingPosition + Vector2.right * (attacker.isPlayer ? 64f : -26f);
                yield return new WaitForSecondsRealtime(step);
                if (moving.Root != null) moving.Root.anchoredPosition = moving.StandingPosition;
            }

            IEnumerator Advance(string command = null, bool stopAfterRound = false)
            {
                busy = true;
                RefreshActionButtons();
                var screen = battleScreen;
                int startingRound = model.Round;
                while (battleScreen == screen && !model.Finished)
                {
                    var target = Target();
                    if (model.NeedsCommand && command == null)
                    {
                        if (!autoMode && !stopAfterRound) break;
                        command = captureCommand?.Invoke(model.Round);
                        if (command == null || model.Reason(command, target) != "")
                            (command, target) = model.AutoCommand(target);
                    }

                    var events = model.Step(command, target);
                    command = null;
                    foreach (var entry in events)
                    {
                        if (battleScreen != screen) yield break;
                        PushLog(entry.Text);
                        if (entry.Float != null && !entry.Hit)
                            ShowFloatText(entry.Target, entry.Float, new Color(.65f, .95f, 1f), false);
                        if (entry.Cue != null) AudioManager.Play(entry.Cue);
                        if (entry.Hit)
                        {
                            bool firstBigMove = (entry.Skill?.all == true || entry.Skill?.telegraph == true)
                                && playedBigMoves.Add(entry.Skill.id);
                            yield return PlayImpact(stage, views[entry.Actor], views[entry.Target], entry,
                                speed, firstBigMove, captureImpact, ShowDamage);

                            void ShowDamage()
                            {
                                ShowFloatText(entry.Target, entry.Float, entry.Critical
                                    ? new Color(1f, .51f, .26f) : UiKit.HeaderGold, entry.Critical);
                            }
                        }
                        else if (entry.Actor != null)
                            yield return Lunge(entry.Actor);
                        else if (entry.Recovery)
                            yield return PlayRecovery(views[entry.Target]);
                    }
                    RefreshBattle();
                    if (events.Count > 0) yield return new WaitForSecondsRealtime(stopAfterRound ? .08f : .24f / speed);
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
                activeTab = 0;
                movePage = 0;
                inspectedAction = "attack";
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

            var (hpFill, hpTxt) = BattleBar(box, xL, -58, xR, -38, new Color(.82f, .24f, .20f), "hud_health");
            var (qiFill, qiTxt) = BattleBar(box, xL, -82, xR, -62, new Color(.22f, .54f, .86f), "attr_qi");
            return (hpFill, hpTxt, qiFill, qiTxt);
        }

        private (Image fill, Text txt) BattleBar(RectTransform parent, float xL, float yB, float xR, float yT, Color color, string icon)
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
            var symbol = Rect("Meter icon", bg);
            Anchor(symbol, Vector2.zero, Vector2.up, new(4, 0), new(24, 0));
            AddImage(symbol, UiKit.Icon(icon));
            var t = Label(bg, "", 18, TextAnchor.MiddleCenter);
            t.color = Color.white;
            t.fontStyle = FontStyle.Bold;
            Fill(t.rectTransform);
            return (img, t);
        }

        private BattleUnitView SpawnBattleSprite(RectTransform stage, BattleCombatant unit, Vector2 pos, Action onClick)
        {
            var rt = Rect("Unit_" + unit.name, stage);
            var groundAnchor = new Vector2(.5f, pos.y);
            Anchor(rt, groundAnchor, groundAnchor, new(pos.x - 95, 0), new(pos.x + 95, 282));
            rt.pivot = new Vector2(.5f, 0);
            rt.anchoredPosition = new Vector2(pos.x, 0);
            rt.localScale = Vector3.one * (pos.y > .3f ? .91f : 1f);

            var shadowRect = Rect("ContactShadow", rt);
            var feet = new Vector2(.5f, 0);
            Anchor(shadowRect, feet, feet, new(-83, -16), new(83, 13));
            var shadow = AddImage(shadowRect, CharacterFactory.StandeeShadow.texture);

            var spr = Rect("Sprite", rt);
            var bossStandee = !unit.isPlayer && unit.portraitId == "leader_heisong"
                ? Resources.Load<Texture2D>("Standees/leader_heisong") : null;
            var tex = unit.standee ?? bossStandee ?? (unit.spriteId == null
                ? CharacterFactory.StandeeTexture(unit.portraitId, unit.gender, portraitFallback: true)
                : Resources.Load<Texture2D>("Battle/" + unit.spriteId));
            var (pose, nativeRight) = CharacterFactory.BattlePose(tex,
                unit.spriteId != null && bossStandee == null ? "Battle/" : "Standees/",
                unit.gender, unit.isPlayer, unit.standee != null);
            tex = pose;
            float width = 222, height = 248;
            if (tex != null)
            {
                float scale = Mathf.Min(width / tex.width, height / tex.height);
                width = tex.width * scale;
                height = tex.height * scale;
            }
            const float padding = 2f;
            Anchor(spr, feet, feet, new(-width / 2, -padding), new(width / 2, height - padding));
            var raw = AddImage(spr, tex);
            if (nativeRight != unit.isPlayer)
                raw.uvRect = new Rect(1, 0, -1, 1);

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
            nm.fontSize = 17;
            nm.resizeTextForBestFit = false;
            Anchor(nm.rectTransform, Vector2.up, Vector2.one, new(2, -46), new(-2, -7));

            var statuses = Rect("Statuses", rt);
            Anchor(statuses, Vector2.zero, Vector2.right, new(-9, -63), new(9, -13));

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
                Root = rt, Art = raw, Shadow = shadow, HpFill = fImg, HpText = nm, Statuses = statuses,
                TargetArrow = arrow, StandingPosition = rt.anchoredPosition
            };
        }

        private Text BattleLabel(RectTransform parent, string content, int size, TextAnchor alignment,
            Vector2 min, Vector2 max)
        {
            var label = Label(parent, content, size, alignment);
            Anchor(label.rectTransform, Vector2.zero, Vector2.zero, min, max);
            return label;
        }

        private void RefreshStatuses(BattleUnitView view, BattleCombatant unit, BattleModel model)
        {
            var statuses = unit.statuses;
            string key = unit.Alive ? string.Join(",", statuses.Select(entry => $"{entry.Key}:{entry.Value}")) : "";
            if (key == view.StatusKey) return;
            view.StatusKey = key;

            // Keep the foot anchor intact while replacing the old seals.
            foreach (Transform child in view.Statuses)
            {
                child.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
            if (!unit.Alive) return;

            int i = 0;
            foreach (var (id, turns) in statuses)
            {
                var status = model.Status(id);
                bool boon = status.attack > 1 || status.incoming < 1 || status.speed > 0 || status.evade;
                float x = (i % 2) * 104;
                float y = 25 * (i / 2) + 26;
                var tag = Panel("Status_" + id, view.Statuses, Vector2.zero, Vector2.zero,
                    new(x, y - 24), new(x + 101, y), false);
                tag.GetComponent<Image>().color = new Color(.13f, .12f, .10f, .98f);
                var seal = Panel("Seal", tag, Vector2.zero, Vector2.zero,
                    new(2, 2), new(22, 22), false);
                seal.GetComponent<Image>().color = boon ? new Color(.18f, .43f, .32f) : new Color(.55f, .23f, .21f);
                var icon = Label(seal, status.label.Substring(0, 1), 18, TextAnchor.MiddleCenter);
                icon.color = Color.white;
                Fill(icon.rectTransform);
                var caption = BattleLabel(tag, $"{status.label}{turns}回", 18, TextAnchor.MiddleCenter,
                    new(23, 0), new(101, 24));
                caption.color = CardText;
                i++;
            }
        }

        internal void CloseBattle()
        {
            if (battleScreen == null) return;
            battleScreen.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(battleScreen.gameObject);
            battleScreen = null;
            canvas.GetComponent<Canvas>().sortingOrder = battlePreviousCanvasOrder;
        }
    }
}
