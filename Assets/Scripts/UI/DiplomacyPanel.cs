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
        private static readonly Color Soft = new(.83f, .72f, .55f), Jade = new(.20f, .39f, .28f);
        private readonly WorldMapData data;
        private readonly Action changed;
        private readonly RectTransform root, listView, rows, detail, side, wide, overlay;
        private readonly Text header;
        private readonly Button factionsTab, specialTab, victoryTab;
        private GameState state;
        private MapFaction selected;
        private DiploAction selectedAction;
        private string result = "";
        private bool victoryView, specialView;
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
            root = MapUi.Docket(parent, "外交折衝", 0, 0, 1600, 900, false);
            var masthead = MapUi.Kit(root, "刊首", 12, 8, 1576, 80, "Wood");
            MapUi.Text(masthead, "外交折衝", 27, 6, 250, 68, 36, MapUi.Paper);
            header = MapUi.Text(masthead, "", 290, 14, 1066, 52, 20, MapUi.Paper, TextAnchor.MiddleLeft, "Numbers");
            MapUi.Button(masthead, "返回地圖", 1376, 16, 176, 48, Close, MapUi.Red);
            MapUi.Docket(root, "外交卷冊", 14, 99, 1572, 784);
            factionsTab = MapUi.Button(root, "江湖勢力", 34, 113, 144, 44, () => SelectRoster(false));
            specialTab = MapUi.Button(root, "特殊勢力", 187, 113, 144, 44, () => SelectRoster(true));
            victoryTab = MapUi.Button(root, "勝利之路", 340, 113, 144, 44, ShowVictoryTab);

            MapUi.Kit(root, "名冊", 26, 158, 466, 716, "Wood", false);
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
            selected = focus ?? selected;
            specialView = selected?.Court == true;
            selected ??= Roster().FirstOrDefault();
            victoryView = false;
            result = "";
            selectedAction = GameState.DiploActionById("gift");
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            overlay.gameObject.SetActive(false);
            Refresh();
            if (state.ClaimDiplomaticVictory()) ShowVictoryProclamation();
        }

        public void Close()
        {
            if (!IsOpen || talk?.Committed == true) return;
            state.EnvoyId = null;
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
            specialView = faction.Court;
            result = "";
            victoryView = false;
            Refresh();
        }

        private void BeginTalk(DiploAction action)
        {
            if (talk != null || state.DiploBlock(action, selected) != null) return;
            var scene = DiplomacyDialogue.Data.actions.FirstOrDefault(entry => entry.id == action.id && entry.options.Length > 0);
            if (scene == null) return;
            var faction = selected;
            if (state.ChooseEnvoy == null) OpenTalk(scene, faction, action, null);
            else state.ChooseEnvoy(action, faction, id => { state.EnvoyId = id; OpenTalk(scene, faction, action, null); });
        }

        private void OpenTalk(TalkScene scene, MapFaction faction, DiploAction action, DiploEvent incident)
        {
            lastDissent.TryGetValue(faction.id, out string previous);
            talk = new DiplomacyTalk(overlay, state, data, faction, scene, action, incident, previous, role =>
            {
                if (role != null) lastDissent[faction.id] = role;
            }, text =>
            {
                state.EnvoyId = null;
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

        private void SelectRoster(bool special)
        {
            specialView = special;
            victoryView = false;
            if (selected?.Court != special)
            {
                selected = Roster().FirstOrDefault();
                result = "";
            }
            Refresh();
        }

        private IEnumerable<MapFaction> Roster() => data.Factions.Where(faction => faction.Court == specialView)
            .OrderBy(f => Diplomacy.Group(f) switch { "jianghu" => 0, "court" => 1, _ => 2 })
            .ThenByDescending(f => state?.IsFactionKnown(f.id) ?? true)
            .ThenByDescending(f => state?.Attitude(f) ?? 0);

        private void Refresh()
        {
            if (state == null) return;
            int eloquence = state.Eloquence(out string envoy);
            header.text = $"銀兩 {state.Silver}　聲望 {state.Reputation}　行動力 {state.ActionPoints}／{state.ApMax}　" +
                $"本派聲威 {state.Prestige}　使者 {envoy}（口才 {eloquence}）";
            MapUi.Select(factionsTab, !victoryView && !specialView);
            MapUi.Select(specialTab, !victoryView && specialView);
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
                    chosen ? new Color(.89f, .68f, .48f) : index % 2 == 0 ? MapUi.Paper : new Color(.90f, .82f, .68f));
                row.name = "勢力:" + faction.id;
                var rect = (RectTransform)row.transform;
                MapUi.Portrait(rect, known ? PortraitService.Resolve(faction.PortraitId) : null, 17, 5, 45, 50);
                MapUi.Panel(rect, "旗色", 6, 8, 8, RowHeight - 22, faction.Tint).GetComponent<Image>().raycastTarget = false;
                MapUi.Text(rect, faction.Label, 70, 2, 164, 30, 21, chosen ? MapUi.Red : MapUi.Ink, TextAnchor.MiddleLeft, "Names");
                string tags = ties != 0 ? "・" + string.Join("・", Diplomacy.TieNames(ties).Split('・').Take(2)) : "";
                MapUi.Text(rect, (known ? Diplomacy.GroupName(faction) : "未探明") + tags, 70, 32, 176, 26, 18,
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
            string weak = Diplomacy.Group(faction) == "court" ? "可遣使議約" :
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

            MapUi.Kit(detail, "行動卷", -8, 266, 646, 365, "Wood", false);
            var actions = Diplomacy.Data.actions ?? Array.Empty<DiploAction>();
            for (int i = 0; i < actions.Length && i < 9; i++)
            {
                var action = actions[i];
                float x = i % 3 * 214, y = 276 + i / 3 * 114;
                string block = state.DiploBlock(action, faction);
                var button = MapUi.Button(detail, action.name, x, y, 202, 48, () => SelectAction(action), action == selectedAction ? MapUi.Red : null, 21);
                button.name = "外交:" + action.id;
                string cost = $"行動力{action.ap}" + (action.silver > 0 ? $"・銀兩{action.silver}" : "") +
                    (action.reputation > 0 ? $"・聲望{action.reputation}" : "");
                MapUi.Text(detail, cost, x, y + 52, 202, 26, 18, MapUi.Paper, TextAnchor.MiddleCenter, "Numbers");
                MapUi.Text(detail, block == null ? $"成功率 {state.DiploChance(action, faction):P0}" : block, x, y + 80, 202, 28, 18,
                    block == null ? MapUi.Gold : new Color(.96f, .62f, .50f), TextAnchor.MiddleCenter);
            }
            string note = string.IsNullOrEmpty(result)
                ? selectedAction?.desc ?? "" : result;
            MapUi.Kit(detail, "結果", 0, 650, 630, 104, "Wood", false);
            MapUi.Text(detail, note, 16, 660, selectedAction == null ? 594 : 420, 85, 19, MapUi.Paper, TextAnchor.UpperLeft, "Narration");
            if (selectedAction != null)
            {
                var enter = MapUi.Button(detail, "入席議事", 454, 675, 160, 52, () => BeginTalk(selectedAction), MapUi.Red);
                enter.interactable = state.DiploBlock(selectedAction, faction) == null;
            }
        }

        // ---- Victory summary -------------------------------------------------------------------

        private void SelectAction(DiploAction action)
        {
            selectedAction = action;
            result = "";
            DrawDetail();
            DrawSide();
        }

        private void DrawSide()
        {
            MapUi.Clear(side);
            var docket = MapUi.Docket(side, "席前進言", 0, 0, 410, 516, false);
            MapUi.Text(docket, "席前進言　／　" + selectedAction?.name, 20, 13, 370, 52, 27, MapUi.Gold);
            if (selected != null && state.IsFactionKnown(selected.id) && GameState.Patronage(selected) is PatronageTerms terms)
            {
                var faction = selected;
                bool active = state.Patron?.id == faction.id;
                MapUi.Text(docket, terms.label + (active ? "・已立約" : ""),
                    20, 82, 365, 48, 27, MapUi.Gold);
                MapUi.Text(docket, "所得　" + terms.Benefit, 20, 146, 365, 64, 20, MapUi.Paper, TextAnchor.UpperLeft);
                MapUi.Text(docket, "代價　" + terms.Cost, 20, 225, 365, 64, 20, MapUi.Paper, TextAnchor.UpperLeft);
                string block = active ? null : state.PatronBlock(faction);
                MapUi.Text(docket, block ?? (active ? "解除後，對方態度 −10。" : "派使議約需行動力 1。"),
                    20, 314, 365, 60, 19, MapUi.Gold, TextAnchor.UpperLeft);
                var patronButton = MapUi.Button(docket, active ? "解除約書" : "遣使議約 ›", 20, 401, 365, 51, () =>
                {
                    if (active) ApplyResult(state.ReleasePatron());
                    else state.ChoosePatronEnvoy?.Invoke(terms.label, id => ApplyResult(state.SeekPatron(faction, id)));
                }, MapUi.Red);
                patronButton.interactable = block == null;
            }
            else if (selected != null && selectedAction != null)
            {
                var available = state.Disciples.Where(d => d.injury < 3).ToArray();
                var disciple = available.Where(d => CastDialogue.Find(d.id)?.role?.StartsWith("disciple_pro_", StringComparison.Ordinal) == true)
                    .OrderBy(d => d.id != "kang").FirstOrDefault();
                int y = 70;
                foreach (var id in new[] { "lin", "xu", disciple?.id })
                {
                    var member = available.FirstOrDefault(d => d.id == id);
                    if (member == null) continue;
                    var paper = MapUi.Kit(docket, member.name, 12, y, 386, 138, "Xuan", false);
                    MapUi.Portrait(paper, PortraitService.Resolve(PortraitService.IdOrDefault(member.portrait, member.id), member.gender),
                        10, 12, 76, 112);
                    MapUi.Text(paper, member.name + "　" + member.title, 98, 10, 274, 30, 22, MapUi.Red);
                    MapUi.Text(paper, Preview(member), 98, 46, 274, 82, 18, MapUi.Ink, TextAnchor.UpperLeft, "Narration");
                    y += 145;
                }
            }

            var report = MapUi.Docket(side, "急報與局勢", 0, 528, 410, 232);
            var v = state.Victory;
            MapUi.Text(report, "勝利之路", 20, 12, 368, 34, 23, MapUi.Red);
            MapUi.Text(report, $"據點 {v.Held}／{v.Rules.conquestNodes}　歸心 {v.Loyal}／{v.Rules.loyalNeeded}",
                20, 50, 370, 34, 19, MapUi.Ink);
            MapUi.Text(report, "外交急報", 20, 96, 368, 34, 23, MapUi.Red);
            var pending = state.PendingDiploEvent;
            if (pending == null) MapUi.Text(report, "目前沒有待決急報。", 20, 136, 368, 40, 19, MapUi.Ink);
            else
            {
                MapUi.Text(report, $"{pending.title}・{state.PendingDiploTarget?.Label}", 20, 130, 368, 34, 19, MapUi.Ink);
                MapUi.Button(report, "處理急報", 20, 173, 370, 42, ShowEvent, MapUi.Red);
            }
        }

        private void ApplyResult(string text)
        {
            result = text;
            changed?.Invoke();
            Refresh();
        }

        private string Preview(DiscipleData member)
        {
            var cast = CastDialogue.Find(member.id);
            string tier = CastDialogue.Tier(CastDialogue.Score(state, member));
            int attitude = state.Attitude(selected);
            string band = attitude < 0 ? "cold" : attitude < 25 ? "neutral" : "warm";
            var scene = DiplomacyDialogue.Data.actions.FirstOrDefault(entry => entry.id == selectedAction.id);
            var advice = scene?.advisors.Where(a => a.role == cast?.role && (a.tier == tier || a.tier == "any")
                && (a.band == band || a.band == "any")).OrderByDescending(a => (a.tier == tier ? 2 : 0) + (a.band == band ? 1 : 0))
                .FirstOrDefault();
            string line = tier == "frosty" ? $"「{selectedAction.name}」？" + cast?.advisorVoice?.frosty?.advisor : advice?.text;
            if (string.IsNullOrWhiteSpace(line))
                line = "先看清對方的條件，再決定要不要遣使。";
            return CastDialogue.Format(state, Diplomacy.Fill(line, selected), member.id)
                .Replace("{faction}", selected.Label).Replace("{silver}", selectedAction.silver.ToString())
                .Replace("{seat}", data.NodeLabel(selected.seat));
        }

        /// <summary>Detailed military and diplomatic progress alongside the quest panel’s four routes.</summary>
        private void DrawVictory()
        {
            MapUi.Clear(wide);
            var v = state.Victory;
            var rules = v.Rules;
            Column(0, "武力征服", "以武立威", state.Route("conquest").Met,
                $"率眾攻討草莽據點、或併入其他門派，讓轄下據點達到 {rules.conquestNodes} 處，且聲望 ≥ {rules.conquestReputation}，守穩 {RouteRules.Data.conquest.holdMonths} 個月。",
                new[]
                {
                    ($"轄下據點　{v.Held}／{rules.conquestNodes}", v.Held / (float)rules.conquestNodes, v.Held >= rules.conquestNodes),
                    ($"本派聲望　{v.Reputation}／{rules.conquestReputation}", v.Reputation / (float)rules.conquestReputation, v.Reputation >= rules.conquestReputation)
                }, (state.Story.heldNodes ?? Array.Empty<string>()).Select(id => (data.NodeLabel(id), "本派旗號", true)).ToArray(),
                "代價：每次攻討 2 行動力並有負傷風險；轄下據點 ≥ 6 時，冷淡的勢力會更忌憚本派。");
            Column(778, "外交勝利", "江湖盟主", state.Route("diplomacy").Met,
                $"{state.JianghuCount} 個江湖勢力中，結義＋附庸＋併入 ≥ {rules.loyalNeeded}（其中附庸／併入 ≥ {rules.subjectNeeded}），且聲望 ≥ {rules.reputationNeeded}，維持 {RouteRules.Data.diplomacy.holdMonths} 個月。",
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
            var box = MapUi.Docket(wide, route, x, 0, 754, 700, false);
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
                var chip = MapUi.Kit(box, name, cx, cy, 224, 42, good ? "Jade" : "Wood", false);
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
            var scroll = MapUi.Docket(overlay, title, 360, 140, 880, 620);
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
