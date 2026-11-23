using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>
    /// 外交折衝: a full-screen scroll over the world map with the faction roster, the selected faction's
    /// attitude / ties / nine diplomatic actions, the two victory routes side by side and the 外交急報 pop-up.
    /// </summary>
    public sealed class DiplomacyPanel
    {
        private const int RowHeight = 66, ListWidth = 450, ListHeight = 700;
        private static readonly Color Soft = new(.66f, .55f, .38f, .14f), Jade = new(.20f, .39f, .28f);
        private readonly WorldMapData data;
        private readonly Action changed;
        private readonly RectTransform root, listView, rows, detail, side, wide, overlay;
        private readonly Text header;
        private readonly Button factionsTab, victoryTab;
        private GameState state;
        private MapFaction selected;
        private string result = "";
        private bool victoryView;
        private DiplomacyTalk talk;
        private readonly Dictionary<string, string> lastDissent = new();
        public bool IsOpen => root.gameObject.activeSelf;
        public bool EventOpen => overlay.gameObject.activeSelf;
        internal bool TalkCommitted => talk?.Committed == true;
        internal event Action Closed;

        internal DiplomacyPanel(Transform parent, WorldMapData data, Action changed)
        {
            this.data = data;
            this.changed = changed;
            root = MapUi.Kit(parent, "外交折衝", 0, 0, 1600, 900, "Teal");
            var masthead = MapUi.Kit(root, "刊首", 12, 8, 1576, 80, "Wood");
            MapUi.Text(masthead, "外交折衝", 27, 6, 250, 68, 36, MapUi.Paper);
            header = MapUi.Text(masthead, "", 290, 14, 1066, 52, 20, MapUi.Paper, TextAnchor.MiddleLeft, "Numbers");
            MapUi.Button(masthead, "返回地圖", 1376, 16, 176, 48, Close, MapUi.Red);
            MapUi.Kit(root, "外交卷冊", 14, 99, 1572, 784, "Paper", false);
            factionsTab = MapUi.Button(root, "諸勢力", 34, 113, 180, 44, () => { victoryView = false; Refresh(); });
            victoryTab = MapUi.Button(root, "勝利之路", 224, 113, 180, 44, () => { victoryView = true; Refresh(); });

            listView = MapUi.Panel(root, "名冊視窗", 34, 166, ListWidth, ListHeight, Soft);
            listView.gameObject.AddComponent<RectMask2D>();
            rows = MapUi.Rect(listView, "名冊", 0, 0, ListWidth, 0);
            var scroll = listView.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = listView;
            scroll.content = rows;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 65;

            detail = MapUi.Rect(root, "勢力詳情", 506, 113, 630, 760);
            side = MapUi.Rect(root, "勝利摘要", 1156, 113, 410, 760);
            wide = MapUi.Rect(root, "勝利之路", 34, 166, 1532, 700);
            overlay = MapUi.Panel(root, "外交急報", 0, 0, 1600, 900, new Color(.02f, .02f, .02f, .62f));
            overlay.gameObject.SetActive(false);
            root.gameObject.SetActive(false);
        }

        internal void Open(GameState state, MapFaction focus = null)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            selected = focus ?? selected ?? Roster().FirstOrDefault();
            victoryView = false;
            result = "";
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            overlay.gameObject.SetActive(false);
            Refresh();
            if (state.ClaimDiplomaticVictory()) ShowVictoryProclamation();
        }

        public void Close()
        {
            if (!IsOpen || talk?.Committed == true) return;
            talk = null;
            overlay.gameObject.SetActive(false);
            root.gameObject.SetActive(false);
            Closed?.Invoke();
        }

        internal void ShowVictoryTab()
        {
            victoryView = true;
            Refresh();
        }

        internal void Select(MapFaction faction)
        {
            selected = faction;
            result = "";
            victoryView = false;
            Refresh();
        }

        /// <summary>Runs one action on the selected faction (also used by the capture checks).</summary>
        internal string Act(string id)
        {
            var action = GameState.DiploActionById(id);
            if (action == null || selected == null || state == null) return null;
            result = state.DiploAct(action, selected);
            AudioManager.Play(result.Contains("成功") ? "complete" : "page");
            changed?.Invoke();
            Refresh();
            if (state.ClaimDiplomaticVictory()) ShowVictoryProclamation();
            return result;
        }

        private void BeginTalk(DiploAction action)
        {
            if (talk != null || state.DiploBlock(action, selected) != null) return;
            var scene = DiplomacyDialogue.Data.actions.FirstOrDefault(entry => entry.id == action.id && entry.options.Length > 0);
            if (scene == null) { Act(action.id); return; }
            OpenTalk(scene, selected, action, null);
        }

        private void OpenTalk(TalkScene scene, MapFaction faction, DiploAction action, DiploEvent incident)
        {
            lastDissent.TryGetValue(faction.id, out string previous);
            talk = new DiplomacyTalk(overlay, state, data, faction, scene, action, incident, previous, role =>
            {
                if (role != null) lastDissent[faction.id] = role;
            }, text =>
            {
                talk = null;
                overlay.gameObject.SetActive(false);
                if (text != null)
                {
                    result = text;
                    changed?.Invoke();
                }
                Refresh();
                if (state.ClaimDiplomaticVictory()) ShowVictoryProclamation();
            });
        }

        private IEnumerable<MapFaction> Roster() => data.Factions
            .OrderBy(f => Diplomacy.Group(f) switch { "jianghu" => 0, "court" => 1, _ => 2 })
            .ThenByDescending(f => state?.IsFactionKnown(f.id) ?? true)
            .ThenByDescending(f => state?.Attitude(f) ?? 0);

        private void Refresh()
        {
            if (state == null) return;
            int eloquence = state.Eloquence(out string envoy);
            header.text = $"銀兩 {state.Silver}　聲望 {state.Reputation}　行動力 {state.ActionPoints}／{state.ApMax}　" +
                $"本派聲威 {state.Prestige}　使者 {envoy}（口才 {eloquence}）";
            MapUi.Select(factionsTab, !victoryView);
            MapUi.Select(victoryTab, victoryView);
            listView.gameObject.SetActive(!victoryView);
            detail.gameObject.SetActive(!victoryView);
            side.gameObject.SetActive(!victoryView);
            wide.gameObject.SetActive(victoryView);
            if (victoryView)
            {
                DrawVictory();
                return;
            }
            DrawRoster();
            DrawDetail();
            DrawSide();
        }

        // ---- Roster ----------------------------------------------------------------------------

        private void DrawRoster()
        {
            MapUi.Clear(rows);
            int index = 0;
            foreach (var faction in Roster())
            {
                bool known = state.IsFactionKnown(faction.id);
                int attitude = state.Attitude(faction);
                int ties = state.Ties(faction.id);
                bool chosen = faction == selected;
                var row = MapUi.Button(rows, "", 0, index * RowHeight, ListWidth - 14, RowHeight - 6, () => Select(faction),
                    chosen ? new Color(.61f, .24f, .17f, .22f) : new Color(1, .98f, .92f, .35f));
                row.name = "勢力:" + faction.id;
                var rect = (RectTransform)row.transform;
                MapUi.Panel(rect, "旗色", 6, 8, 8, RowHeight - 22, faction.Tint).GetComponent<Image>().raycastTarget = false;
                MapUi.Text(rect, faction.Label, 22, 2, 196, 30, 21, MapUi.Ink, TextAnchor.MiddleLeft, "Names");
                string tags = ties != 0 ? "・" + string.Join("・", Diplomacy.TieNames(ties).Split('・').Take(2)) : "";
                MapUi.Text(rect, (known ? Diplomacy.GroupName(faction) : "未探明") + tags, 22, 32, 230, 26, 18,
                    (ties & (Diplomacy.Alliance | Diplomacy.Vassal | Diplomacy.Merged)) != 0 ? Jade : MapUi.Red);
                MapUi.Text(rect, known ? $"{Diplomacy.AttitudeLabel(attitude)} {attitude:+0;-0;0}" : "態度 ？", 238, 2, 190, 30, 19,
                    known ? Diplomacy.AttitudeColor(attitude) : MapUi.Ink, TextAnchor.MiddleRight, "Numbers");
                if (known) Bar(rect, 252, 42, 176, 8, attitude);
                index++;
            }
            rows.sizeDelta = new Vector2(ListWidth, index * RowHeight);
        }

        /// <summary>A centred attitude bar: −100 fills left in red, +100 fills right in jade.</summary>
        private static void Bar(Transform parent, float x, float y, float width, float height, int attitude)
        {
            MapUi.Panel(parent, "態度槽", x, y, width, height, new Color(.45f, .37f, .27f, .35f)).GetComponent<Image>().raycastTarget = false;
            float half = width / 2, fill = half * Mathf.Abs(attitude) / 100f;
            var bar = MapUi.Panel(parent, "態度", attitude >= 0 ? x + half : x + half - fill, y, fill, height,
                attitude >= 0 ? Jade : MapUi.Red);
            bar.GetComponent<Image>().raycastTarget = false;
            MapUi.Panel(parent, "中線", x + half - 1, y - 3, 2, height + 6, MapUi.Ink).GetComponent<Image>().raycastTarget = false;
        }

        private static void Progress(Transform parent, float x, float y, float width, float height, float ratio, bool done)
        {
            MapUi.Panel(parent, "進度槽", x, y, width, height, new Color(.45f, .37f, .27f, .35f)).GetComponent<Image>().raycastTarget = false;
            if (ratio > 0)
                MapUi.Panel(parent, "進度", x, y, width * Mathf.Clamp01(ratio), height, done ? Jade : MapUi.Red)
                    .GetComponent<Image>().raycastTarget = false;
        }

        // ---- Selected faction ------------------------------------------------------------------

        private void DrawDetail()
        {
            MapUi.Clear(detail);
            if (selected == null) return;
            var faction = selected;
            bool known = state.IsFactionKnown(faction.id);
            int attitude = state.Attitude(faction), ties = state.Ties(faction.id);
            MapUi.Portrait(detail, known ? PortraitService.Resolve(faction.PortraitId) : null, 0, 4, 120, 156);
            if (!known) MapUi.Text(detail, "？", 0, 4, 120, 156, 54, MapUi.Paper, TextAnchor.MiddleCenter);
            MapUi.Text(detail, faction.Label, 136, 0, 360, 46, 31, MapUi.Ink, TextAnchor.MiddleLeft, "Names");
            var flag = MapUi.Panel(detail, "勢力旗", 574, 0, 50, 70, faction.Tint);
            flag.GetComponent<Image>().raycastTarget = false;
            MapUi.Text(flag, faction.Label.Substring(0, 1), 0, 4, 50, 56, 32, MapUi.Paper, TextAnchor.MiddleCenter, "Names");
            MapUi.Text(detail, $"{Diplomacy.GroupName(faction)}・{faction.TypeName}　{data.NodeLabel(faction.seat)}", 136, 48, 430, 28, 19, MapUi.Red);
            MapUi.Text(detail, known ? $"首領　{faction.leader ?? "不詳"}" : "首領　尚待打探（可遣使拜訪）", 136, 78, 494, 28, 19, MapUi.Ink);
            MapUi.Text(detail, "態度", 136, 110, 70, 30, 20, MapUi.Ink);
            MapUi.Text(detail, known ? $"{Diplomacy.AttitudeLabel(attitude)}　{attitude:+0;-0;0}" : "？", 206, 110, 260, 30, 22,
                known ? Diplomacy.AttitudeColor(attitude) : MapUi.Ink, TextAnchor.MiddleLeft, "Numbers");
            if (known) Bar(detail, 136, 146, 494, 12, attitude);

            int strength = Diplomacy.Strength(faction), prestige = state.Prestige;
            float ratio = Diplomacy.Data.weakRatio;
            string weak = Diplomacy.Group(faction) == "court" ? "朝廷外邦，只可交好" :
                prestige >= strength * ratio ? "衰弱門派，已可收為附庸" : prestige >= strength ? "可以聲望威懾" : "實力尚在本派之上";
            MapUi.Text(detail, $"實力 {strength}　本派聲威 {prestige}　{weak}", 0, 170, 630, 28, 19, MapUi.Ink);
            var tie = state.Tie(faction.id);
            string tribute = (ties & Diplomacy.Vassal) != 0
                ? $"　朝貢每月 {Mathf.Clamp(faction.money / 600, 5, 30)} 兩・附庸 {state.MonthStamp - (tie?.since ?? state.MonthStamp)} 個月" : "";
            MapUi.Text(detail, "盟約　" + Diplomacy.TieNames(ties) + tribute, 0, 200, 630, 28, 19, ties != 0 ? Jade : MapUi.Ink);
            var rivals = Diplomacy.Rivals(faction.id).Select(GameState.FindFaction).Where(f => f != null).Select(f => f.Label).ToArray();
            MapUi.Text(detail, rivals.Length > 0 ? "宿敵　" + string.Join("、", rivals) + "（與其交好會招宿敵記恨）" : "宿敵　無",
                0, 228, 630, 28, 18, MapUi.Ink);
            MapUi.Panel(detail, "分隔", 0, 262, 630, 2, new Color(.65f, .51f, .34f)).GetComponent<Image>().raycastTarget = false;

            var actions = Diplomacy.Data.actions ?? Array.Empty<DiploAction>();
            for (int i = 0; i < actions.Length && i < 9; i++)
            {
                var action = actions[i];
                float x = i % 3 * 214, y = 272 + i / 3 * 124;
                string block = state.DiploBlock(action, faction);
                var button = MapUi.Button(detail, action.name, x, y, 202, 48, () => BeginTalk(action), block == null ? MapUi.Red : null, 21);
                button.name = "外交:" + action.id;
                button.interactable = block == null;
                string cost = $"行動力{action.ap}" + (action.silver > 0 ? $"・銀兩{action.silver}" : "") +
                    (action.reputation > 0 ? $"・聲望{action.reputation}" : "");
                MapUi.Text(detail, cost, x, y + 52, 202, 26, 18, MapUi.Ink, TextAnchor.MiddleCenter, "Numbers");
                MapUi.Text(detail, block == null ? $"成功率 {state.DiploChance(action, faction):P0}" : block, x, y + 80, 202, 28, 18,
                    block == null ? Jade : MapUi.Red, TextAnchor.MiddleCenter);
            }
            string note = string.IsNullOrEmpty(result)
                ? "點選行動按鈕執行。成功率 = 基礎 ＋ 使者口才 ＋ 態度 ＋ 聲威差（5%～95%）；失敗會降低態度。" : result;
            MapUi.Kit(detail, "結果", 0, 650, 630, 104, "Wood", false);
            MapUi.Text(detail, note, 16, 658, 600, 92, 19, MapUi.Paper, TextAnchor.UpperLeft, "Narration");
        }

        // ---- Victory summary -------------------------------------------------------------------

        private void DrawSide()
        {
            MapUi.Clear(side);
            var v = state.Victory;
            var rules = v.Rules;
            MapUi.Panel(side, "底", 0, 0, 410, 760, Soft).GetComponent<Image>().raycastTarget = false;
            MapUi.Text(side, "勝利之路", 16, 4, 380, 42, 27, MapUi.Ink);
            int y = 50;
            Section("武力征服・以武立威", v.Conquest);
            Row($"轄下據點 {v.Held}／{rules.conquestNodes}", v.Held / (float)rules.conquestNodes, v.Held >= rules.conquestNodes);
            Row($"聲望 {v.Reputation}／{rules.conquestReputation}", v.Reputation / (float)rules.conquestReputation, v.Reputation >= rules.conquestReputation);
            y += 8;
            Section("外交勝利・江湖盟主", v.Diplomatic);
            Row($"歸心勢力 {v.Loyal}／{rules.loyalNeeded}（結義＋附庸＋併入）", v.Loyal / (float)rules.loyalNeeded, v.Loyal >= rules.loyalNeeded);
            Row($"附庸／併入 {v.Subjects}／{rules.subjectNeeded}", v.Subjects / (float)rules.subjectNeeded, v.Subjects >= rules.subjectNeeded);
            Row($"聲望 {v.Reputation}／{rules.reputationNeeded}", v.Reputation / (float)rules.reputationNeeded, v.Reputation >= rules.reputationNeeded);
            var loyal = data.Factions.Where(Diplomacy.Jianghu)
                .Where(f => (state.Ties(f.id) & (Diplomacy.Alliance | Diplomacy.Vassal | Diplomacy.Merged)) != 0).Select(f => f.Label).ToArray();
            MapUi.Text(side, "已歸心：" + (loyal.Length > 0 ? string.Join("、", loyal) : "尚無"), 16, y, 380, 56, 18, MapUi.Ink, TextAnchor.UpperLeft);
            y += 64;
            MapUi.Panel(side, "分隔", 16, y, 378, 2, new Color(.65f, .51f, .34f)).GetComponent<Image>().raycastTarget = false;
            y += 10;
            MapUi.Text(side, "外交急報", 16, y, 380, 32, 22, MapUi.Red);
            y += 36;
            var pending = state.PendingDiploEvent;
            if (pending != null)
            {
                MapUi.Text(side, $"{pending.title}——{state.PendingDiploTarget?.Label}", 16, y, 380, 30, 20, MapUi.Ink, TextAnchor.MiddleLeft, "Names");
                var open = MapUi.Button(side, "處理急報", 16, y + 36, 378, 46, ShowEvent, MapUi.Red, 21);
                open.name = "處理急報";
            }
            else
                MapUi.Text(side, "目前沒有待決急報。\n每月結算時可能傳來壽宴、求援或來投。", 16, y, 380, 80, 18, MapUi.Ink, TextAnchor.UpperLeft);

            void Section(string title, bool done)
            {
                MapUi.Text(side, title, 16, y, 270, 32, 22, MapUi.Red, TextAnchor.MiddleLeft, "Names");
                MapUi.Text(side, done ? "已達成" : "進行中", 286, y, 108, 32, 19, done ? Jade : MapUi.Ink, TextAnchor.MiddleRight);
                y += 36;
            }
            void Row(string label, float ratio, bool done)
            {
                MapUi.Text(side, label, 16, y, 380, 26, 18, MapUi.Ink, TextAnchor.MiddleLeft, "Numbers");
                Progress(side, 16, y + 28, 378, 10, ratio, done);
                y += 46;
            }
        }

        /// <summary>勝利之路 tab: the conquest and diplomatic routes as two equal columns.</summary>
        private void DrawVictory()
        {
            MapUi.Clear(wide);
            var v = state.Victory;
            var rules = v.Rules;
            Column(0, "武力征服", "以武立威", v.Conquest,
                $"率眾攻討草莽據點、或併入其他門派，讓轄下據點達到 {rules.conquestNodes} 處，且聲望 ≥ {rules.conquestReputation}。",
                new[]
                {
                    ($"轄下據點　{v.Held}／{rules.conquestNodes}", v.Held / (float)rules.conquestNodes, v.Held >= rules.conquestNodes),
                    ($"本派聲望　{v.Reputation}／{rules.conquestReputation}", v.Reputation / (float)rules.conquestReputation, v.Reputation >= rules.conquestReputation)
                }, (state.Story.heldNodes ?? Array.Empty<string>()).Select(id => (data.NodeLabel(id), "本派旗號", true)).ToArray(),
                "代價：每次攻討 2 行動力並有負傷風險；轄下據點 ≥ 6 時，冷淡的勢力會更忌憚本派。");
            Column(778, "外交勝利", "江湖盟主", v.Diplomatic,
                $"{state.JianghuCount} 個江湖勢力中，結義＋附庸＋併入 ≥ {rules.loyalNeeded}（其中附庸／併入 ≥ {rules.subjectNeeded}），且聲望 ≥ {rules.reputationNeeded}。",
                new[]
                {
                    ($"歸心勢力　{v.Loyal}／{rules.loyalNeeded}", v.Loyal / (float)rules.loyalNeeded, v.Loyal >= rules.loyalNeeded),
                    ($"附庸／併入　{v.Subjects}／{rules.subjectNeeded}", v.Subjects / (float)rules.subjectNeeded, v.Subjects >= rules.subjectNeeded),
                    ($"本派聲望　{v.Reputation}／{rules.reputationNeeded}", v.Reputation / (float)rules.reputationNeeded, v.Reputation >= rules.reputationNeeded)
                }, data.Factions.Where(Diplomacy.Jianghu).Select(f =>
                {
                    int ties = state.Ties(f.id);
                    bool loyal = (ties & (Diplomacy.Alliance | Diplomacy.Vassal | Diplomacy.Merged)) != 0;
                    string label = (ties & Diplomacy.Merged) != 0 ? "已併入" : (ties & Diplomacy.Vassal) != 0 ? "附庸" :
                        (ties & Diplomacy.Alliance) != 0 ? "結義" : Diplomacy.AttitudeLabel(state.Attitude(f));
                    return (f.Label, label, loyal);
                }).ToArray(),
                "代價：銀兩與行動力；盟友使本派戰力每約 +8%，但與一方交好會讓其宿敵記恨。");
        }

        private void Column(float x, string route, string title, bool done, string rule, (string label, float ratio, bool done)[] bars,
            (string name, string tag, bool good)[] chips, string note)
        {
            var box = MapUi.Kit(wide, route, x, 0, 754, 700, "Wood", false);
            MapUi.Text(box, route, 28, 14, 300, 50, 34, MapUi.Gold);
            MapUi.Text(box, "「" + title + "」", 230, 14, 300, 50, 30, MapUi.Paper, TextAnchor.MiddleLeft, "Names");
            MapUi.Text(box, done ? "已達成" : "進行中", 560, 18, 166, 44, 24, done ? new Color(.55f, .85f, .62f) : MapUi.Gold, TextAnchor.MiddleRight);
            MapUi.Text(box, rule, 28, 72, 698, 60, 19, MapUi.Paper, TextAnchor.UpperLeft);
            float y = 140;
            foreach (var (label, ratio, ok) in bars)
            {
                MapUi.Text(box, label, 28, y, 698, 32, 22, MapUi.Paper, TextAnchor.MiddleLeft, "Numbers");
                MapUi.Panel(box, "進度槽", 28, y + 36, 698, 16, new Color(.93f, .86f, .73f, .22f)).GetComponent<Image>().raycastTarget = false;
                if (ratio > 0)
                    MapUi.Panel(box, "進度", 28, y + 36, 698 * Mathf.Clamp01(ratio), 16, ok ? new Color(.36f, .66f, .45f) : MapUi.Red)
                        .GetComponent<Image>().raycastTarget = false;
                y += 66;
            }
            y += 6;
            int index = 0;
            foreach (var (name, tag, good) in chips.Take(15))
            {
                float cx = 28 + index % 3 * 234, cy = y + index / 3 * 50;
                var chip = MapUi.Panel(box, name, cx, cy, 224, 42, good ? new Color(.20f, .39f, .28f, .85f) : new Color(.93f, .86f, .73f, .16f));
                chip.GetComponent<Image>().raycastTarget = false;
                MapUi.Text(chip, name, 10, 0, 130, 42, 19, MapUi.Paper, TextAnchor.MiddleLeft, "Names");
                MapUi.Text(chip, tag, 130, 0, 86, 42, 18, good ? new Color(.85f, .95f, .80f) : MapUi.Gold, TextAnchor.MiddleRight);
                index++;
            }
            if (chips.Length == 0) MapUi.Text(box, "尚無轄下據點", 28, y, 698, 40, 19, MapUi.Gold);
            MapUi.Text(box, note, 28, 636, 698, 56, 18, MapUi.Gold, TextAnchor.UpperLeft);
        }

        // ---- Events ----------------------------------------------------------------------------

        private RectTransform Popup(string title, string subtitle, Texture2D portrait)
        {
            MapUi.Clear(overlay);
            overlay.gameObject.SetActive(true);
            overlay.SetAsLastSibling();
            var scroll = MapUi.Kit(overlay, title, 360, 140, 880, 620, "Paper");
            MapUi.Text(scroll, title, 40, 24, 560, 56, 36, MapUi.Ink);
            MapUi.Text(scroll, subtitle, 40, 82, 800, 30, 20, MapUi.Red, TextAnchor.MiddleLeft, "Names");
            var divider = MapUi.Rect(scroll, "Divider", 40, 116, 800, 8).gameObject.AddComponent<Image>();
            divider.sprite = UiKit.Sprite("Divider");
            divider.raycastTarget = false;
            if (divider.sprite == null) divider.color = MapUi.Gold;
            MapUi.Portrait(scroll, portrait, 40, 140, 160, 210);
            return scroll;
        }

        /// <summary>Shows the pending 外交急報 with its priced options.</summary>
        internal void ShowEvent()
        {
            var diploEvent = state?.PendingDiploEvent;
            var faction = state?.PendingDiploTarget;
            if (diploEvent == null || faction == null) return;
            if (talk != null) return;
            var scene = DiplomacyDialogue.Data.events.FirstOrDefault(entry => entry.id == diploEvent.id && entry.Covers(diploEvent));
            if (scene != null) { OpenTalk(scene, faction, null, diploEvent); return; }
            AudioManager.Play("page");
            var scroll = Popup("外交急報・" + diploEvent.title,
                $"{faction.Label}　{Diplomacy.GroupName(faction)}　態度 {Diplomacy.AttitudeLabel(state.Attitude(faction))} {state.Attitude(faction):+0;-0;0}",
                PortraitService.Resolve(faction.PortraitId));
            MapUi.Text(scroll, Diplomacy.Fill(diploEvent.text, faction), 224, 140, 616, 210, 21, MapUi.Ink, TextAnchor.UpperLeft, "Narration");
            var options = diploEvent.options ?? Array.Empty<DiploOption>();
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                var option = options[i];
                string block = state.DiploOptionBlock(option);
                var costs = new List<string>();
                if (option.ap > 0) costs.Add($"行動力 {option.ap}");
                if (option.silver > 0) costs.Add($"銀兩 {option.silver}");
                string label = $"{option.label}　（{(costs.Count > 0 ? string.Join("・", costs) : "不需花費")}）" + (block != null ? "　" + block : "");
                var button = MapUi.Button(scroll, label, 40, 372 + i * 72, 800, 60, () => Resolve(index), i == 0 ? MapUi.Red : null, 22);
                button.name = "急報選項:" + i;
                button.interactable = block == null;
            }
        }

        internal string Resolve(int index)
        {
            var faction = state.PendingDiploTarget;
            string title = state.PendingDiploEvent?.title ?? "外交急報";
            string text = state.ResolveDiploEvent(index);
            changed?.Invoke();
            Refresh();
            var scroll = Popup(title + "・結果", faction?.Label ?? "", faction == null ? null : PortraitService.Resolve(faction.PortraitId));
            MapUi.Text(scroll, text, 224, 140, 616, 210, 22, MapUi.Ink, TextAnchor.UpperLeft, "Narration");
            var ok = MapUi.Button(scroll, "知道了", 300, 520, 280, 60, () =>
            {
                overlay.gameObject.SetActive(false);
                Refresh();
                if (state.ClaimDiplomaticVictory()) ShowVictoryProclamation();
            }, MapUi.Red, 24);
            ok.name = "知道了";
            result = text;
            return text;
        }

        private void ShowVictoryProclamation()
        {
            AudioManager.Jingle("victory");
            var v = state.Victory;
            var scroll = Popup("江湖盟主", "外交勝利", null);
            MapUi.Text(scroll, $"{state.SectLabel}不動干戈，令江湖 {v.Loyal} 家勢力歸心、{v.Subjects} 家奉為上宗。" +
                "群雄齊聚山門，共推本派掌門為江湖盟主。\n\n（遊戲可繼續進行。）", 224, 140, 616, 260, 23, MapUi.Ink, TextAnchor.UpperLeft, "Narration");
            MapUi.Button(scroll, "受此重任", 300, 520, 280, 60, () => { overlay.gameObject.SetActive(false); Refresh(); }, MapUi.Red, 24);
        }
    }
}
