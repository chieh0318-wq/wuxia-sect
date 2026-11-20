using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    public sealed partial class WorldMapScreen : MonoBehaviour
    {
        private const float ViewWidth = 1552, ViewHeight = 720;
        private const float MapWidth = 1190, MapHeight = 720;
        // Fill the viewport width at the widest zoom so no empty bars show beside the parchment.
        private const float MinZoom = ViewWidth/MapWidth;
        private WorldMapData data;
        private IWorldMapHost host;
        private Canvas canvas;
        private RectTransform frame, viewport, map, card, party, routeLayer;
        private Text ap, status;
        private IntelPanel intel;
        private readonly List<(MapNode node, Text label, Vector2 point)> labels = new();
        private readonly List<(RectTransform flag, Vector2 point, int seatOrder)> flags = new();
        private readonly List<Button> cardActions = new();
        private Vector2 pan, lastMouse;
        private float zoom = MinZoom;
        private bool traveling;
        private string selected;
        private Sprite halo;
        private Texture2D haloTexture;
        private bool changingScreen;
        private RectTransform focusMarker;
        private HashSet<string> seats;
        public bool IsOpen => canvas != null && canvas.gameObject.activeSelf;
        internal Canvas CaptureCanvas => canvas;
        public event Action Closed;
        internal bool CloseIntel()
        {
            if (intel == null || !intel.IsOpen) return false;
            intel.Close();
            return true;
        }

        public void Open(IWorldMapHost host)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            data ??= WorldMapData.Load();
            seats ??= new HashSet<string>(data.Factions.Select(f => f.seat));
            if (canvas == null) Build();
            canvas.gameObject.SetActive(true);
            intel.Close();
            if (!data.Nodes.ContainsKey(host.PartyNode ?? ""))
                host.PartyNode = data.Nodes.ContainsKey(host.StartNode ?? "") ? host.StartNode : data.DefaultNode;
            SyncParty();
            UpdateAp();
            CenterOn(host.PartyNode);
            if (!traveling) status.text = $"隊伍駐於{data.NodeLabel(host.PartyNode)}。點選地名查看路程，點選旗幟查看勢力。";
            ApplyView();
            AudioManager.Music("map");
        }

        public void Close()
        {
            if (!IsOpen) return;
            if (traveling) FinishTravel(selected);
            StopAllCoroutines();
            ClearCard();
            canvas.gameObject.SetActive(false);
            AudioManager.Music("courtyard");
            Closed?.Invoke();
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            if (canvas != null) Destroy(canvas.gameObject);
            Destroy(halo);
            Destroy(haloTexture);
        }

        private void Build()
        {
            var obj = new GameObject("World map canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            obj.transform.SetParent(transform,false);
            canvas = obj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = obj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600,900);
            scaler.matchWidthOrHeight = .5f;
            var backdrop = MapUi.Panel(canvas.transform,"Map backdrop",0,0,0,0,new Color(.05f,.09f,.10f));
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
            frame = MapUi.Kit(canvas.transform,"Map frame",0,0,1600,900,"Teal");
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f,.5f);
            frame.anchoredPosition = Vector2.zero;
            var top = MapUi.Kit(frame,"Map top bar",12,8,1576,64,"Wood");
            MapUi.Text(top,"江湖大地圖",26,4,330,56,34,MapUi.Paper);
            ap = MapUi.Text(top,"",470,8,560,48,22,MapUi.Paper,TextAnchor.MiddleRight,"Numbers");
            MapUi.Button(top,"情報總覽",1060,10,150,44,OpenIntel,MapUi.Red);
            MapUi.Button(top,"全圖",1220,10,96,44,() => { zoom = MinZoom; pan = Vector2.zero; ApplyView(); });
            MapUi.Button(top,"返回宗門　M",1326,10,232,44,Close,MapUi.Red);
            MapUi.Kit(frame,"Map border",14,71,ViewWidth+20,ViewHeight+12,"Wood",false);
            viewport = MapUi.Panel(frame,"Map viewport",24,77,ViewWidth,ViewHeight,new Color(.34f,.39f,.36f));
            viewport.gameObject.AddComponent<RectMask2D>();
            map = MapUi.Rect(viewport,"Projected map",0,0,MapWidth,MapHeight);
            map.pivot = new Vector2(.5f,.5f);
            var image = map.gameObject.AddComponent<RawImage>();
            image.texture = M2RuntimeAssets.Load()?.map;
            image.color = image.texture == null ? MapUi.Paper : Color.white;
            image.raycastTarget = false;
            var washes = MapUi.Rect(map,"Territory washes",0,0,MapWidth,MapHeight);
            var roads = MapUi.Rect(map,"Post roads",0,0,MapWidth,MapHeight);
            routeLayer = MapUi.Rect(map,"Travel route",0,0,MapWidth,MapHeight);
            foreach (var faction in data.Factions)
                foreach (string id in (faction.territory ?? Array.Empty<string>()).Append(faction.seat).Distinct())
                    if (data.Nodes.TryGetValue(id ?? "",out var node)) AddWash(washes,Position(node),faction.Tint);
            foreach (var road in data.Roads)
                Line(roads,Position(data.Nodes[road.a]),Position(data.Nodes[road.b]),new Color(.28f,.21f,.14f,.42f),1.25f,true);
            foreach (var node in data.Nodes.Values) AddNode(node);
            foreach (var group in data.Factions.GroupBy(f => f.seat))
            {
                if (!data.Nodes.TryGetValue(group.Key ?? "",out var node)) continue;
                Vector2 p = Position(node);
                int index = 0;
                foreach (var faction in group)
                {
                    int n = index++;
                    var flag = MapUi.Button(map,"",0,0,17,19,() => FocusFaction(faction),Color.clear);
                    var rect = (RectTransform)flag.transform;
                    rect.pivot = new Vector2(.5f,1);
                    flags.Add((rect,p,n));
                    MapUi.Panel(rect,"Pole",2,0,2,19,MapUi.Ink).GetComponent<Image>().raycastTarget = false;
                    MapUi.Panel(rect,"Banner",4,1,12,11,faction.Tint).GetComponent<Image>().raycastTarget = false;
                    MapUi.Panel(rect,"Trim",4,10,12,2,MapUi.Paper).GetComponent<Image>().raycastTarget = false;
                }
            }
            focusMarker = MapUi.Rect(map,"Selected seat",0,0,36,36);
            var focusText = MapUi.Text(focusMarker,"◯",0,0,36,36,32,MapUi.Red,TextAnchor.MiddleCenter);
            focusText.fontStyle = FontStyle.Bold;
            focusMarker.gameObject.SetActive(false);
            party = MapUi.Panel(map,"Party",0,0,23,23,MapUi.Red);
            party.GetComponent<Image>().raycastTarget = false;
            party.pivot = Vector2.one*.5f;
            MapUi.Text(party,"行",0,0,23,23,16,MapUi.Paper,TextAnchor.MiddleCenter);
            card = MapUi.Rect(frame,"Map card",1113,96,426,680);
            var footer = MapUi.Kit(frame,"Map status bar",12,808,1576,82,"Wood",false);
            status = MapUi.Text(footer,"",22,6,1530,38,22,MapUi.Paper,TextAnchor.MiddleLeft,"Narration");
            MapUi.Text(footer,"滾輪縮放　右鍵拖曳／WASD 移圖",
                22,44,630,30,17,MapUi.Gold);
            var legend = MapUi.Kit(footer,"Legend paper tag",750,13,805,55,"Paper",false);
            MapUi.Text(legend,"◎ 京城　● 州城　◇ 門派　◆ 關隘　▲ 名山　行 隊伍",
                16,0,773,55,17,MapUi.Ink,TextAnchor.MiddleCenter);
            if (data.Nodes.Count == 0) status.text = "地圖資料尚未載入。";
            intel = new IntelPanel(frame,data,FocusFaction);
            ApplyView();
        }

        private Vector2 Position(MapNode node)
        {
            Vector2 uv = data.Bounds.Uv(node);
            return new Vector2(uv.x*MapWidth,(1-uv.y)*MapHeight);
        }

        private void AddWash(Transform root,Vector2 p,Color color)
        {
            if (halo == null)
            {
                haloTexture = new Texture2D(64,64,TextureFormat.RGBA32,false);
                var pixels = new Color[4096];
                for (int y=0; y<64; y++)
                for (int x=0; x<64; x++)
                {
                    float t = Mathf.Clamp01(1 - Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f))/31.5f);
                    pixels[y*64+x] = new Color(1,1,1,t*t);
                }
                haloTexture.SetPixels(pixels);
                haloTexture.Apply();
                halo = Sprite.Create(haloTexture,new Rect(0,0,64,64),Vector2.one*.5f);
            }
            var wash = MapUi.Rect(root,"Faction wash",p.x-54,p.y-54,108,108).gameObject.AddComponent<Image>();
            wash.sprite = halo;
            color.a = .65f;
            wash.color = color;
            wash.raycastTarget = false;
        }

        private static void Line(Transform root,Vector2 a,Vector2 b,Color color,float width,bool dashed)
        {
            float distance = Vector2.Distance(a,b);
            if (distance < .1f) return;
            Vector2 direction = (b-a)/distance;
            float step = dashed ? 9 : distance;
            for (float offset=0; offset<distance; offset+=step)
            {
                var p = a + direction*offset;
                var segment = MapUi.Panel(root,"Road",p.x,p.y,Mathf.Min(dashed ? 5 : distance,distance-offset),width,color);
                segment.localRotation = Quaternion.Euler(0,0,-Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
                segment.GetComponent<Image>().raycastTarget = false;
            }
        }

        private void AddNode(MapNode node)
        {
            Vector2 p = Position(node);
            bool major = node.type is "capital" or "city" or "sect";
            string marker = node.type switch
            {
                "capital" => "◎", "city" => "●", "town" => "•", "pass" => "◆",
                "sect" => "◇", "mountain" => "▲", "outpost" => "▪", _ => "·"
            };
            var button = MapUi.Button(map,"",p.x-8,p.y-8,16,16,() => ShowNode(node),Color.clear,14);
            MapUi.Text(button.transform,marker,0,0,16,16,major ? 16 : 15,MapUi.Ink,TextAnchor.MiddleCenter);
            var label = MapUi.Text(map,node.Label,0,0,150,24,major ? 18 : 15,MapUi.Ink);
            if (major) label.fontStyle = FontStyle.Bold;
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(1,.92f,.75f,.73f);
            shadow.effectDistance = new Vector2(0,-.8f);
            label.raycastTarget = true;
            var labelButton = label.gameObject.AddComponent<Button>();
            labelButton.transition = Selectable.Transition.None;
            labelButton.onClick.AddListener(() => { UiSfx.Play("click"); ShowNode(node); });
            labels.Add((node,label,p));
        }

        private void Update()
        {
            if (!IsOpen || intel.IsOpen) return;
            Vector2 mouse = Input.mousePosition;
            bool overMap = RectTransformUtility.RectangleContainsScreenPoint(viewport,mouse,canvas.worldCamera);
            float wheel = Input.mouseScrollDelta.y;
            if (overMap && Mathf.Abs(wheel) > .01f)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport,mouse,canvas.worldCamera,out Vector2 point);
                Vector2 relative = point - new Vector2(ViewWidth/2,-ViewHeight/2);
                float next = Mathf.Clamp(zoom*Mathf.Pow(1.15f,wheel),MinZoom,3.25f);
                pan = relative + (pan-relative)*(next/zoom);
                zoom = next;
                changingScreen = true;
            }
            if (overMap && Input.GetMouseButton(1) && !Input.GetMouseButtonDown(1))
            {
                pan += (mouse-lastMouse)/canvas.scaleFactor;
                changingScreen = true;
            }
            lastMouse = mouse;
            Vector2 move = new((Input.GetKey(KeyCode.A)?1:0)-(Input.GetKey(KeyCode.D)?1:0),
                (Input.GetKey(KeyCode.S)?1:0)-(Input.GetKey(KeyCode.W)?1:0));
            if (move != Vector2.zero)
            {
                pan += move.normalized * Time.unscaledDeltaTime * 560;
                changingScreen = true;
            }
            if (changingScreen) ApplyView();
        }

        private void ApplyView()
        {
            float x = Mathf.Max(0,(MapWidth*zoom-ViewWidth)/2);
            float y = Mathf.Max(0,(MapHeight*zoom-ViewHeight)/2);
            pan = new Vector2(Mathf.Clamp(pan.x,-x,x),Mathf.Clamp(pan.y,-y,y));
            map.anchoredPosition = new Vector2(ViewWidth/2,-ViewHeight/2)+pan;
            map.localScale = Vector3.one*zoom;
            LayoutLabels();
            if (!traveling) SyncParty();
            changingScreen = false;
        }

        private void LayoutLabels()
        {
            var occupied = new List<Rect>();
            party.localScale = Vector3.one/zoom;
            focusMarker.localScale = Vector3.one/zoom;
            Vector2 offset = new((ViewWidth-MapWidth*zoom)/2+pan.x,(ViewHeight-MapHeight*zoom)/2-pan.y);
            if (party.gameObject.activeSelf && data.Nodes.TryGetValue(host?.PartyNode ?? "",out var partyNode))
                occupied.Add(new Rect(Position(partyNode)*zoom + offset + new Vector2(-30.5f,3.5f),new Vector2(23,23)));
            foreach (var (flag,point,seatOrder) in flags)
            {
                flag.localScale = Vector3.one/zoom;
                Vector2 marker = point*zoom + offset;
                Vector2 position = new(seatOrder % 3 * 20, -32 - seatOrder / 3 * 22);
                var bounds = new Rect(marker + position - new Vector2(10, 1), new Vector2(20, 21));
                while (occupied.Any(other => other.Overlaps(bounds)))
                {
                    position.y -= 22;
                    bounds.y -= 22;
                }
                Vector2 local = point + position/zoom;
                flag.anchoredPosition = new Vector2(local.x,-local.y);
                occupied.Add(bounds);
            }
            foreach (var (node,label,point) in labels.OrderBy(entry => entry.node.id == host?.PartyNode ? -1 : LabelPriority(entry.node)))
            {
                var rect = label.rectTransform;
                rect.localScale = Vector3.one/zoom;
                float width = Mathf.Min(150,Mathf.Ceil(label.preferredWidth)+4);
                rect.sizeDelta = new Vector2(width,24);
                Vector2 marker = point*zoom+offset;
                Vector2[] candidates = {
                    new(9,-11),new(-width-9,-11),new(-width/2,5),new(-width/2,-34)
                };
                bool shown = false;
                if (LabelPriority(node) < 3 || seats.Contains(node.id) || zoom >= 1.42f || node.id == host?.PartyNode)
                    foreach (var position in candidates)
                    {
                        var bounds = new Rect(marker+position,new Vector2(width,22));
                        if (bounds.xMin < 0 || bounds.xMax > ViewWidth || bounds.yMin < 0 ||
                            bounds.yMax > ViewHeight || occupied.Any(other => other.Overlaps(bounds))) continue;
                        Vector2 local = point + position/zoom;
                        rect.anchoredPosition = new Vector2(local.x,-local.y);
                        occupied.Add(bounds);
                        shown = true;
                        break;
                    }
                if (label.gameObject.activeSelf != shown) label.gameObject.SetActive(shown);
            }
        }

        private static int LabelPriority(MapNode node) => node.type switch
        {
            "capital" => 0,
            "city" => 1,
            "sect" => 2,
            _ => 3
        };

        private void ClearCard()
        {
            MapUi.Clear(card);
            cardActions.Clear();
            MapUi.Clear(routeLayer);
            focusMarker.gameObject.SetActive(false);
        }

        private RectTransform Card(string title,string subtitle)
        {
            ClearCard();
            var panel = MapUi.Kit(card,title,0,0,426,680,"Paper");
            MapUi.Text(panel,title,20,16,342,48,31,MapUi.Ink);
            MapUi.Text(panel,subtitle,20,66,382,32,19,MapUi.Red,TextAnchor.MiddleLeft,"Names");
            var divider = MapUi.Rect(panel,"Divider",20,100,386,8).gameObject.AddComponent<Image>();
            divider.sprite = UiKit.Sprite("Divider");
            divider.raycastTarget = false;
            if (divider.sprite == null) divider.color = MapUi.Gold;
            MapUi.Button(panel,"×",370,18,38,38,ClearCard,MapUi.Red,22);
            return panel;
        }

        private void ShowNode(MapNode node)
        {
            if (traveling) return;
            var panel = Card(node.Label,node.region ?? "");
            MapUi.Text(panel,node.desc ?? "",20,110,386,150,21,MapUi.Ink,TextAnchor.UpperLeft);
            var factions = data.Factions.Where(f => f.Controls(node.id)).ToArray();
            MapUi.Text(panel,"勢力　"+(factions.Length == 0 ? "—" : string.Join("、",factions.Select(f => f.Label))),20,266,386,60,19,MapUi.Ink);
            var route = data.Route(host.PartyNode,node.id);
            string message = node.id == host.PartyNode ? "隊伍在此" : route == null ? "尚無可行路線" : $"{route.Days} 日　行動力 {route.Ap}";
            MapUi.Text(panel,message,20,345,386,39,24,MapUi.Ink);
            if (route == null) return;
            MapUi.Text(panel,string.Join(" → ",route.Nodes.Select(data.NodeLabel)),20,393,386,154,19,MapUi.Ink,TextAnchor.UpperLeft);
            for (int i=1;i<route.Nodes.Count;i++)
                Line(routeLayer,Position(data.Nodes[route.Nodes[i-1]]),Position(data.Nodes[route.Nodes[i]]),MapUi.Red,3,false);
            var confirm = MapUi.Button(panel,"確認啟程",20,599,386,48,() => StartTravel(route,node.id),MapUi.Red);
            confirm.interactable = host.ActionPoints >= route.Ap;
            cardActions.Add(confirm);
        }

        internal void ShowFaction(MapFaction faction)
        {
            if (traveling) return;
            var panel = Card(faction.Label,$"{faction.TypeName}　{(faction.Court ? "朝廷／官府　" : "")}{data.NodeLabel(faction.seat)}");
            bool known = host.IsFactionKnown(faction.id);
            var stance = faction.stance == "friendly" ? new Color(.20f,.39f,.28f) :
                faction.stance == "hostile" ? MapUi.Red : MapUi.Ink;
            MapUi.Text(panel,"關係　"+faction.Relation,20,105,384,32,21,stance);
            if (!known)
            {
                MapUi.Text(panel,"情報不足　　最強者 ???",20,166,386,48,23,MapUi.Ink);
                var scout = MapUi.Button(panel,"派人打探　行動力 1",20,599,386,48,() =>
                {
                    if (!host.TrySpendAp(1,"scout:"+faction.id)) return;
                    host.SetFactionKnown(faction.id);
                    UpdateAp();
                    AudioManager.Play("page");
                    ShowFaction(faction);
                },MapUi.Red);
                scout.interactable = host.ActionPoints >= 1;
                return;
            }
            MapUi.Text(panel,$"首領　{faction.leader ?? "—"}",20,149,386,32,22,MapUi.Ink);
            MapUi.Text(panel,RankSystem.FormatKnown(faction.leaderRank,faction.leaderCap),20,183,386,30,24,RankSystem.Color(faction.leaderRank));
            if (!string.IsNullOrEmpty(faction.leaderWeapon))
                MapUi.Text(panel,"神兵　"+faction.WeaponLabel,155,183,249,30,19,MapUi.Red);
            MapUi.Text(panel,$"聲望 {faction.reputation}　弟子 {faction.disciples}　銀兩 {faction.money}",20,229,386,39,19,MapUi.Ink);
            MapUi.Text(panel,"絕學　"+faction.Arts,20,276,386,54,19,MapUi.Ink,TextAnchor.UpperLeft);
            bool hasLines = faction.leaderLines?.Any(line => line != null) == true;
            MapUi.Text(panel,faction.desc ?? "",20,337,386,hasLines ? 104 : 212,20,MapUi.Ink,TextAnchor.UpperLeft,"Narration");
            if (hasLines)
            {
                MapUi.Panel(panel,"Skill divider",20,444,386,1,new Color(.65f,.51f,.34f));
                DrawFamily(panel,faction,"戰鬥技能",MapSkillLine.Combat,20);
                DrawFamily(panel,faction,"生產技能",MapSkillLine.Production,222);
            }
            MapUi.Panel(panel,"Notables divider",20,553,386,1,new Color(.65f,.51f,.34f));
            int row = 0;
            foreach (var notable in (faction.notables ?? Array.Empty<FactionNotable>()).Where(n => n != null).Take(2))
            {
                int y = 562 + row++*51;
                MapUi.Text(panel,$"{notable.name}　{notable.title}",20,y,250,29,19,MapUi.Ink);
                MapUi.Text(panel,RankSystem.FormatKnown(notable.rank,notable.cap),273,y,135,29,19,RankSystem.Color(notable.rank));
                MapUi.Text(panel,notable.desc ?? "",20,y+26,386,25,15,MapUi.Ink);
            }
        }

        private static void DrawFamily(Transform panel,MapFaction faction,string title,
            IReadOnlyDictionary<string,string> names,int x)
        {
            MapUi.Text(panel,title,x,450,184,29,18,MapUi.Red);
            int row = 0;
            var lines = (faction.leaderLines ?? Array.Empty<MapSkillLine>())
                .Where(line => line != null && names.ContainsKey(line.id ?? ""))
                .OrderByDescending(line => RankSystem.Order(line.rank)).Take(2);
            foreach (var line in lines)
            {
                int y = 480 + row++*28;
                MapUi.Text(panel,names[line.id],x,y,77,21,17,MapUi.Ink);
                Color tier = RankSystem.Color(line.rank);
                MapUi.Text(panel,RankSystem.FormatKnown(line.rank,line.cap),x+77,y,102,21,17,tier);
                float cap = Mathf.Clamp01((RankSystem.Order(line.cap)+1)/7f);
                float rank = Mathf.Min(cap,Mathf.Clamp01((RankSystem.Order(line.rank)+1)/7f));
                if (cap > 0) MapUi.Panel(panel,"Rank cap",x,y+22,184*cap,3,new Color(.72f,.63f,.49f));
                if (rank > 0) MapUi.Panel(panel,"Current rank",x,y+22,184*rank,3,tier);
            }
            if (row == 0) MapUi.Text(panel,"—",x,478,185,26,17,MapUi.Ink);
        }

        private void OpenIntel()
        {
            if (traveling) return;
            AudioManager.Play("page");
            intel.Open(host);
        }

        internal void OpenIntelOverview() => OpenIntel();
        internal void CaptureIntel() { OpenIntel(); intel.CaptureSelection(); }

        internal void FocusFaction(MapFaction faction)
        {
            if (traveling) return;
            intel.Close();
            if (data.Nodes.TryGetValue(faction.seat ?? "",out var node))
            {
                zoom = 1.85f;
                Vector2 p = Position(node);
                pan = new Vector2((MapWidth/2-p.x)*zoom-150,(p.y-MapHeight/2)*zoom);
                ApplyView();
            }
            ShowFaction(faction);
            if (data.Nodes.TryGetValue(faction.seat ?? "",out var focused))
            {
                Vector2 p = Position(focused);
                focusMarker.anchoredPosition = new Vector2(p.x-18/zoom,-p.y+18/zoom);
                focusMarker.gameObject.SetActive(true);
            }
        }

        private void CenterOn(string id)
        {
            if (!data.Nodes.TryGetValue(id ?? "",out var node)) return;
            zoom = Mathf.Max(zoom,MinZoom);
            Vector2 p = Position(node);
            pan = new Vector2((MapWidth/2-p.x)*zoom,(p.y-MapHeight/2)*zoom);
        }

        private void UpdateAp() => ap.text = $"{host.DateText}　　行動力 {host.ActionPoints}";
        private void SyncParty()
        {
            bool found = data.Nodes.TryGetValue(host.PartyNode ?? "",out var node);
            party.gameObject.SetActive(found);
            if (found) MoveParty(Position(node));
        }
        private void MoveParty(Vector2 p) => party.anchoredPosition = new Vector2(p.x-19/zoom,-p.y-15/zoom);

        private void StartTravel(MapRoute route,string destination)
        {
            if (traveling || !host.TrySpendAp(route.Ap,"travel:"+destination)) return;
            selected = destination;
            traveling = true;
            host.AdvanceDays(route.Days);
            foreach (var button in cardActions) button.interactable = false;
            UpdateAp();
            status.text = "隊伍正在路上……";
            AudioManager.Play("departure");
            StartCoroutine(Animate(route,destination));
        }

        private IEnumerator Animate(MapRoute route,string destination)
        {
            for (int i=1;i<route.Nodes.Count;i++)
            {
                Vector2 a = Position(data.Nodes[route.Nodes[i-1]]);
                Vector2 b = Position(data.Nodes[route.Nodes[i]]);
                for (float t=0;t<1;t+=Time.unscaledDeltaTime/.46f)
                {
                    MoveParty(Vector2.Lerp(a,b,t));
                    yield return null;
                }
            }
            FinishTravel(destination);
            ShowNode(data.Nodes[destination]);
        }

        private void FinishTravel(string destination)
        {
            host.PartyNode = destination;
            traveling = false;
            SyncParty();
            AudioManager.Play("arrival");
            string[] events = { "行旅在茶棚交換沿途消息。","驛路旁傳來商隊的鈴聲。","暮色漸深，隨行弟子尋了處歇腳地。" };
            status.text = $"抵達{data.NodeLabel(destination)}。{events[UnityEngine.Random.Range(0,events.Length)]}";
        }
    }
}
