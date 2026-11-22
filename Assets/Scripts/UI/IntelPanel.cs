using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    public sealed class IntelPanel
    {
        private const int ListWidth = 1480, ListHeight = 656, Columns = 2, Pitch = 746, CardHeight = 194;
        private readonly WorldMapData data;
        private readonly Action<MapFaction> focus;
        private readonly RectTransform root, rows;
        private readonly Text count;
        private readonly Button factionsTab, peopleTab;
        private readonly ScrollRect scroll;
        private readonly StoryCharacter[] roster;
        private IWorldMapHost host;
        private bool people;
        public bool IsOpen => root.gameObject.activeSelf;

        internal IntelPanel(Transform parent, WorldMapData data, Action<MapFaction> focus)
        {
            this.data = data;
            this.focus = focus;
            roster = M2Data.Load<CharacterCatalog>("characters").storyCharacters ?? Array.Empty<StoryCharacter>();
            root = MapUi.Kit(parent, "江湖情報", 0, 0, 1600, 900, "Teal");
            var masthead = MapUi.Kit(root, "刊首", 12, 8, 1576, 80, "Wood");
            MapUi.Text(masthead, "江湖情報", 27, 6, 420, 68, 36, MapUi.Paper);
            MapUi.Button(masthead, "返回地圖", 1376, 16, 176, 48, Close, MapUi.Red);
            MapUi.Kit(root, "情報卷冊", 14, 99, 1572, 784, "Paper", false);
            factionsTab = MapUi.Button(root, "勢力情報", 34, 117, 196, 48, () => Select(false));
            peopleTab = MapUi.Button(root, "江湖俠客", 244, 117, 196, 48, () => Select(true));
            count = MapUi.Text(root, "", 1095, 117, 448, 48, 21, MapUi.Jade, TextAnchor.MiddleRight);
            var viewport = MapUi.Panel(root, "卷冊視窗", 34, 192, ListWidth, ListHeight, new Color(.66f, .55f, .38f, .11f));
            viewport.gameObject.AddComponent<RectMask2D>();
            rows = MapUi.Rect(viewport, "條目", 0, 0, ListWidth, 0);
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = rows;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 65;
            var rail = MapUi.Panel(root, "卷軌", 1531, 192, 14, ListHeight, new Color(.66f, .56f, .42f, .6f));
            var slide = MapUi.Rect(rail, "滑動區", 0, 0, 14, ListHeight);
            var handle = MapUi.Panel(slide, "捲動把手", 0, 0, 14, 46, MapUi.Red);
            var bar = rail.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handle;
            bar.targetGraphic = handle.GetComponent<Image>();
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            Close();
        }

        public void Open(IWorldMapHost host)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            root.gameObject.SetActive(true);
            Select(false);
        }

        public void Close() => root.gameObject.SetActive(false);
        internal void CapturePeopleTab() => peopleTab.onClick.Invoke();

        private void Select(bool showPeople)
        {
            people = showPeople;
            Render();
        }

        private void Render()
        {
            MapUi.Clear(rows);
            MapUi.Select(factionsTab, !people);
            MapUi.Select(peopleTab, people);
            int entries;
            if (people)
            {
                var list = People().ToArray();
                entries = list.Length;
                for (int i = 0; i < entries; i++) DrawPerson(list[i], i);
                count.text = $"共 {entries} 位俠客";
            }
            else
            {
                var list = data.Factions.OrderByDescending(faction => host.IsFactionKnown(faction.id))
                    .ThenByDescending(faction => data.Holdings(faction).Count()).ThenBy(faction => faction.Label).ToArray();
                entries = list.Length;
                for (int i = 0; i < entries; i++) DrawFaction(list[i], i);
                count.text = $"共 {entries} 股勢力";
            }
            if (entries == 0)
                MapUi.Text(rows, "目前沒有可查閱的情報。", 20, 10, 700, 52, 24, MapUi.Ink);
            rows.sizeDelta = new Vector2(ListWidth, Mathf.Max(ListHeight, (entries + 1) / Columns * CardHeight));
            scroll.StopMovement();
            rows.anchoredPosition = Vector2.zero;
            scroll.verticalNormalizedPosition = 1;
        }

        private RectTransform Card(string label, int i, Color color)
        {
            var card = MapUi.Kit(rows, label, i % Columns * Pitch, i / Columns * CardHeight,
                Pitch - 12, CardHeight - 10, "Paper");
            MapUi.Panel(card, "標色", 0, 0, 6, CardHeight - 10, color).GetComponent<Image>().raycastTarget = false;
            return card;
        }

        private void DrawFaction(MapFaction faction, int i)
        {
            bool known = host.IsFactionKnown(faction.id);
            int shift = host.RelationShift(faction.id);
            Color attitude = faction.StanceWith(shift) == "hostile" ? MapUi.Red : MapUi.Jade;
            var card = Card(faction.Label, i, faction.Tint);
            MapUi.Portrait(card, known ? PortraitService.Resolve(faction.PortraitId) : null, 15, 13, 116, 154);
            MapUi.Text(card, faction.Label, 148, 11, 355, 38, 28, MapUi.Ink);
            MapUi.Text(card, faction.RelationWith(shift), 635, 12, 77, 35, 20, attitude, TextAnchor.MiddleRight);
            MapUi.Text(card, known ? faction.leader ?? "首領不詳" : "首領尚待打探", 148, 50, 565, 31, 21, MapUi.Ink);
            string strength = known ? $"人數 {faction.disciples:N0}　聲望 {faction.reputation}" : "兵力尚待打探";
            MapUi.Text(card, $"{faction.TypeName}　據點 {data.Holdings(faction).Count()} 處　{strength}",
                148, 82, 565, 28, 19, MapUi.Jade);
            MapUi.Text(card, known ? faction.desc ?? "" : $"據於{data.NodeLabel(faction.seat)}。可派人查明近況。",
                148, 115, 438, 62, 18, MapUi.Ink, TextAnchor.UpperLeft);
            MapUi.Button(card, known ? "查看據點" : "查探勢力", 594, 132, 118, 37, () => focus(faction), MapUi.Red, 17);
        }

        private IEnumerable<(StoryCharacter person, MapFaction faction)> People()
        {
            var names = new HashSet<string>();
            foreach (var person in roster.Where(person => person != null && !string.IsNullOrEmpty(person.name))
                .OrderByDescending(person => person.id is "master" or "elder_villain" or "warlord" or "official"))
            {
                if (person.id is "friend_m" or "friend_f" && host is GameState game && person.id != game.FriendId)
                    continue;
                var faction = data.Factions.FirstOrDefault(f => f.PortraitId == person.id || f.id == person.faction || f.name == person.faction);
                if (faction != null && faction.PortraitId == person.id && !host.IsFactionKnown(faction.id)) continue;
                if (names.Add(person.name)) yield return (person, faction);
            }
            foreach (var faction in data.Factions.Where(f => host.IsFactionKnown(f.id)))
            foreach (var notable in faction.notables ?? Array.Empty<FactionNotable>())
            {
                if (string.IsNullOrWhiteSpace(notable?.name) || !names.Add(notable.name)) continue;
                yield return (new StoryCharacter
                {
                    name = notable.name, title = notable.title, rank = notable.rank, cap = notable.cap,
                    desc = notable.desc
                }, faction);
            }
        }

        private void DrawPerson((StoryCharacter person, MapFaction faction) entry, int i)
        {
            var (person, faction) = entry;
            var card = Card(person.name, i, faction?.Tint ?? MapUi.Jade);
            MapUi.Portrait(card, PortraitService.Resolve(PortraitService.IdOrDefault(person.portrait, person.id), person.gender),
                15, 13, 116, 154);
            MapUi.Text(card, person.name, 148, 11, 380, 39, 29, MapUi.Ink);
            MapUi.Text(card, RankSystem.FormatKnown(person.rank, person.cap), 554, 14, 158, 35, 20,
                MapUi.Red, TextAnchor.MiddleRight);
            string affiliation = faction?.Label ?? person.faction;
            if (string.IsNullOrWhiteSpace(affiliation))
                affiliation = person.id switch
                {
                    "master" or "uncle_schemer" => "崤山劍門",
                    "friend_m" or "friend_f" => "洛陽謝家",
                    _ => "所屬未明"
                };
            MapUi.Text(card, string.Join("　", new[] { person.title, affiliation }.Where(label => !string.IsNullOrWhiteSpace(label))),
                148, 52, 555, 29, 20, MapUi.Ink);
            string location = faction != null ? data.NodeLabel(faction.seat) : person.id switch
            {
                "uncle_schemer" => "崤山後山",
                "friend_m" or "friend_f" => "洛陽",
                _ => "行蹤未明"
            };
            MapUi.Text(card, "目前線索　" + location, 148, 82, 555, 28, 19, MapUi.Jade);
            MapUi.Text(card, person.personality ?? person.desc ?? person.background ?? "尚無更多消息",
                148, 112, 560, 66, 18, MapUi.Ink, TextAnchor.UpperLeft);
        }
    }
}
