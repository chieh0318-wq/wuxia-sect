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
        private readonly Text[] heading = new Text[Headers.Length];
        private readonly ScrollRect scroll;
        private IWorldMapHost host;
        private int type, relation, column;
        private bool descending;
        public bool IsOpen => root.gameObject.activeSelf;

        internal IntelPanel(Transform parent,WorldMapData data,Action<MapFaction> focus)
        {
            this.data = data;
            this.focus = focus;
            root = MapUi.Panel(parent,"Intel overview",0,0,1600,900,MapUi.Paper);
            MapUi.Panel(root,"Masthead",0,0,1600,91,MapUi.Wood);
            MapUi.Text(root,"情報總覽",31,11,620,68,38,MapUi.Paper);
            MapUi.Button(root,"返回地圖",1384,22,176,47,Close,MapUi.Red);
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
                heading[i] = MapUi.Text(button.transform,Headers[i],8,0,Widths[i]-18,47,19,MapUi.Paper);
                x+=Widths[i];
            }
            var viewport = MapUi.Panel(root,"Faction rows",30,223,1475,592,new Color(.81f,.74f,.62f));
            viewport.gameObject.AddComponent<RectMask2D>();
            rows = MapUi.Rect(viewport,"Scrollable rows",0,0,1475,0);
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = rows;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 43;
            var rail = MapUi.Panel(root,"Scroll track",1518,223,16,592,new Color(.73f,.66f,.55f));
            var slide = MapUi.Rect(rail,"Slide area",0,0,16,592);
            var handle = MapUi.Panel(slide,"Grip",0,0,16,46,MapUi.Red);
            handle.offsetMin = handle.offsetMax = Vector2.zero;
            var bar = rail.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handle;
            bar.targetGraphic = handle.GetComponent<Image>();
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            MapUi.Text(root,"點選欄名排序　點選勢力定位其據點",31,834,1340,37,20,MapUi.Ink);
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
            3 => RankFormat.Order(faction.leaderRank), 4 => faction.reputation, 5 => faction.disciples,
            6 => faction.Arts, 7 => faction.money, _ => faction.Relation
        };

        private void Render()
        {
            MapUi.Clear(rows);
            for (int i=0;i<typeButtons.Length;i++)
                typeButtons[i].targetGraphic.color = i == type ? MapUi.Red : MapUi.Wood;
            for (int i=0;i<relationButtons.Length;i++)
                relationButtons[i].targetGraphic.color = i == relation ? MapUi.Red : MapUi.Wood;
            for (int i=0;i<heading.Length;i++)
                heading[i].text = Headers[i]+(i == column ? descending ? " ▼" : " ▲" : "");
            var matching = data.Factions.Where(f => InType(f) && (relation == 0 || f.Relation == Relations[relation]));
            var grouped = matching.OrderBy(IsPrivate);
            var sorted = (descending ? grouped.ThenByDescending(Key) : grouped.ThenBy(Key)).ThenBy(f=>f.Label).ToArray();
            count.text = $"{sorted.Length} 股勢力";
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
                known ? RankFormat.Format(faction.leaderRank,faction.leaderCap) : "???",
                known ? faction.reputation.ToString() : "—",known ? faction.disciples.ToString() : "—",
                known ? (string.IsNullOrWhiteSpace(faction.Arts) ? "—" : faction.Arts) : "情報不足",
                known ? faction.money.ToString() : "—",faction.Relation
            };
            var rect = MapUi.Panel(rows,faction.Label,0,row*48,1475,46,
                row%2 == 0 ? new Color(.95f,.89f,.77f) : new Color(.89f,.82f,.70f));
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(() => { AudioManager.Play("click"); focus(faction); });
            rect.gameObject.AddComponent<M2ButtonAudio>();
            float x=0;
            for (int i=0;i<values.Length;i++)
            {
                Color color = MapUi.Ink;
                if (i == 3 && known) color=RankFormat.Color(faction.leaderRank);
                if (i == 8 && faction.stance == "hostile") color=MapUi.Red;
                if (i == 8 && faction.stance == "friendly") color=new Color(.20f,.39f,.28f);
                MapUi.Text(rect,values[i],x+8,0,Widths[i]-18,46,20,color);
                x+=Widths[i];
            }
        }
    }
}
