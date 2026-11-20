using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    public sealed class IntelPanel
    {
        private static readonly string[] Headers = { "勢力","類型","據點","最強者","聲望","弟子數","絕學","金錢","關係" };
        private static readonly float[] Widths = { 219,137,166,140,110,115,335,115,138 };
        private static readonly string[] Types = { "全部","門派","朝廷官府","其他" };
        private static readonly string[] Relations = { "全部","友好","中立","敵對" };
        private readonly WorldMapData data;
        private readonly Action<MapFaction> focus;
        private readonly RectTransform root, rows;
        private readonly Text count;
        private readonly Button[] typeButtons = new Button[Types.Length], relationButtons = new Button[Relations.Length];
        private readonly Text[] heading = new Text[Headers.Length], arrows = new Text[Headers.Length];
        private readonly ScrollRect scroll;
        private IWorldMapHost host;
        private int type, relation, column;
        private bool descending;
        public bool IsOpen => root.gameObject.activeSelf;

        internal IntelPanel(Transform parent,WorldMapData data,Action<MapFaction> focus)
        {
            this.data = data;
            this.focus = focus;
            root = MapUi.Kit(parent,"Intel overview",0,0,1600,900,"Teal");
            var masthead = MapUi.Kit(root,"Masthead",12,8,1576,80,"Wood");
            MapUi.Text(masthead,"情報總覽",26,6,420,68,36,MapUi.Paper);
            MapUi.Text(masthead,"江湖各方勢力　未打探者以「情報不足」示之",300,14,700,52,19,MapUi.Gold,TextAnchor.MiddleLeft,"Narration");
            MapUi.Button(masthead,"返回地圖",1376,16,176,48,Close,MapUi.Red);
            MapUi.Kit(root,"Ledger paper",14,96,1572,794,"Paper",false);
            MapUi.Text(root,"類型",31,110,65,46,20,MapUi.Ink);
            MapUi.Text(root,"關係",637,110,65,46,20,MapUi.Ink);
            for (int i=0;i<Types.Length;i++)
            {
                int choice = i;
                typeButtons[i] = MapUi.Button(root,Types[i],96+i*120,110,114,46,() => { type=choice; Render(); });
            }
            for (int i=0;i<Relations.Length;i++)
            {
                int choice = i;
                relationButtons[i] = MapUi.Button(root,Relations[i],701+i*89,110,83,46,() => { relation=choice; Render(); });
            }
            count = MapUi.Text(root,"",1100,109,452,46,20,MapUi.Ink,TextAnchor.MiddleRight);
            float x = 30;
            for (int i=0;i<Headers.Length;i++)
            {
                int index = i;
                var button = MapUi.Button(root,"",x,174,Widths[i]-2,47,() => Sort(index));
                heading[i] = MapUi.Text(button.transform,Headers[i],10,0,Widths[i]-40,47,19,MapUi.Paper);
                // Sort glyphs are outside the font subsets, so they get their own (OS-fallback) text.
                arrows[i] = MapUi.Text(button.transform,"▼▲",Widths[i]-34,0,26,47,17,MapUi.Gold,TextAnchor.MiddleCenter);
                x+=Widths[i];
            }
            var viewport = MapUi.Panel(root,"Faction rows",30,223,1475,592,new Color(.87f,.80f,.66f,.55f));
            viewport.gameObject.AddComponent<RectMask2D>();
            rows = MapUi.Rect(viewport,"Scrollable rows",0,0,1475,0);
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = rows;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 43;
            var rail = MapUi.Panel(root,"Scroll track",1518,223,16,592,new Color(.66f,.56f,.42f,.6f));
            var slide = MapUi.Rect(rail,"Slide area",0,0,16,592);
            var handle = MapUi.Panel(slide,"Grip",0,0,16,46,MapUi.Red);
            handle.offsetMin = handle.offsetMax = Vector2.zero;
            var bar = rail.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handle;
            bar.targetGraphic = handle.GetComponent<Image>();
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            MapUi.Text(root,"點選欄名即可排序；點選勢力，地圖便會標出其據點",31,834,1340,37,20,MapUi.Ink);
            Close();
        }

        public void Open(IWorldMapHost host)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            root.gameObject.SetActive(true);
            Render();
        }
        public void Close() => root.gameObject.SetActive(false);

        internal void CaptureSelection()
        {
            type = data.Factions.Any(f => f.type == "sect" && !f.Court) ? 1 : 2;
            column = 4;
            descending = true;
            Render();
        }

        private void Sort(int index)
        {
            descending = index == column ? !descending : index is 3 or 4 or 5 or 7;
            column = index;
            Render();
        }

        private bool InType(MapFaction f) => type switch
        {
            1 => f.type == "sect" && !f.Court,
            2 => f.Court,
            3 => f.type != "sect" && !f.Court,
            _ => true
        };

        private bool IsPrivate(MapFaction faction) =>
            column is 3 or 4 or 5 or 6 or 7 && !host.IsFactionKnown(faction.id);

        private IComparable Key(MapFaction faction) => IsPrivate(faction) ? null : column switch
        {
            0 => faction.Label, 1 => faction.TypeName, 2 => data.NodeLabel(faction.seat),
            3 => RankSystem.Order(faction.leaderRank), 4 => faction.reputation, 5 => faction.disciples,
            6 => faction.Arts, 7 => faction.money, _ => RelationOf(faction)
        };

        private string RelationOf(MapFaction faction) => faction.RelationWith(host.RelationShift(faction.id));

        private void Render()
        {
            MapUi.Clear(rows);
            for (int i=0;i<typeButtons.Length;i++)
                MapUi.Select(typeButtons[i],i == type);
            for (int i=0;i<relationButtons.Length;i++)
                MapUi.Select(relationButtons[i],i == relation);
            for (int i=0;i<heading.Length;i++)
                arrows[i].text = i == column ? descending ? "▼" : "▲" : "";
            var matching = data.Factions.Where(f => InType(f) && (relation == 0 || RelationOf(f) == Relations[relation]));
            var grouped = matching.OrderBy(IsPrivate);
            var sorted = (descending ? grouped.ThenByDescending(Key) : grouped.ThenBy(Key)).ThenBy(f=>f.Label).ToArray();
            count.text = $"共 {sorted.Length} 股勢力";
            rows.sizeDelta = new Vector2(1475,Mathf.Max(592,sorted.Length*48));
            for (int i=0;i<sorted.Length;i++) DrawRow(sorted[i],i);
            if (sorted.Length == 0)
                MapUi.Text(rows,"目前沒有符合條件的勢力。",16,0,1000,55,21,MapUi.Ink);
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1;
        }

        private void DrawRow(MapFaction faction,int row)
        {
            bool known = host.IsFactionKnown(faction.id);
            string[] values =
            {
                faction.Label,faction.TypeName,data.NodeLabel(faction.seat),
                known ? RankSystem.FormatKnown(faction.leaderRank,faction.leaderCap) : "???",
                known ? faction.reputation.ToString() : "—",known ? faction.disciples.ToString() : "—",
                known ? (string.IsNullOrWhiteSpace(faction.Arts) ? "—" : faction.Arts) : "情報不足",
                known ? faction.money.ToString() : "—",RelationOf(faction)
            };
            string stance = faction.StanceWith(host.RelationShift(faction.id));
            var rect = MapUi.Panel(rows,faction.Label,0,row*48,1475,46,
                row%2 == 0 ? new Color(.97f,.92f,.80f,.55f) : new Color(.80f,.70f,.54f,.30f));
            MapUi.Panel(rect,"Ruled line",0,45,1475,1,new Color(.36f,.27f,.18f,.35f)).GetComponent<Image>().raycastTarget = false;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(() => { UiSfx.Play("click"); focus(faction); });
            rect.gameObject.AddComponent<UiHoverSound>();
            float x=0;
            for (int i=0;i<values.Length;i++)
            {
                if (i > 0)
                    MapUi.Panel(rect,"Ledger column rule",x-2,0,1,46,new Color(.36f,.27f,.18f,.16f))
                        .GetComponent<Image>().raycastTarget = false;
                Color color = MapUi.Ink;
                if (i == 3 && known) color=Color.Lerp(RankSystem.Color(faction.leaderRank),MapUi.Ink,.30f);
                if (i == 8 && stance == "hostile") color=MapUi.Red;
                if (i == 8 && stance == "friendly") color=new Color(.20f,.39f,.28f);
                MapUi.Text(rect,values[i],x+8,0,Widths[i]-18,46,20,color);
                x+=Widths[i];
            }
        }
    }
}
