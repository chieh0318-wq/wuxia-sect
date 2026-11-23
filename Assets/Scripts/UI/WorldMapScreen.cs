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
        // The San10-style terrain painting is 16:9.
        private const float MapWidth = 1280, MapHeight = 720;
        // Fill the viewport width at the widest zoom so no empty bars show beside the terrain.
        private const float MinZoom = ViewWidth/MapWidth, MaxZoom = 3.25f;
        private WorldMapData data;
        private IWorldMapHost host;
        private Canvas canvas;
        private RectTransform frame, viewport, map, card, party, routeLayer, iconLayer, plateLayer;
        private Text ap, status;
        private IntelPanel intel;
        private readonly List<(MapNode node, RectTransform plate, Text label, Vector2 point)> labels = new();
        private readonly List<RectTransform> regionNames = new();
        private RectTransform openedCard;
        private readonly List<(RectTransform marker, float size)> nodeMarkers = new();
        private readonly List<(RectTransform flag, Vector2 point, int seatOrder)> flags = new();
        private readonly List<Button> cardActions = new();
        private Vector2 pan, lastMouse, targetPan;
        private float zoom = MinZoom, targetZoom = MinZoom;
        private bool traveling;
        private string selected;
        internal event Action<BattleRequest, Action<bool>> BattleRequested;
        private bool changingScreen, dragFromMap;
        private RectTransform focusMarker;
        private Vector2 focusedPoint;
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
            if (!traveling) status.text = $"隊伍駐於{data.NodeLabel(host.PartyNode)}。點選城寨查看守備與路線，點選旗幟查看勢力。";
            ApplyView();
            AudioManager.Music("jianghu_map");
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
            MapUi.Text(top,"江湖地圖",26,4,330,56,34,MapUi.Paper);
            ap = MapUi.Text(top,"",470,8,560,48,22,MapUi.Paper,TextAnchor.MiddleRight,"Numbers");
            MapUi.Button(top,"情報",1060,10,150,44,OpenIntel,MapUi.Red);
            MapUi.Button(top,"全圖",1220,10,96,44,() => { targetZoom = MinZoom; targetPan = Vector2.zero; });
            MapUi.Button(top,"返回宗門　M",1326,10,232,44,Close,MapUi.Red);
            MapUi.Kit(frame,"Map border",14,71,ViewWidth+20,ViewHeight+12,"Wood",false);
            viewport = MapUi.Panel(frame,"Map viewport",24,77,ViewWidth,ViewHeight,new Color(.34f,.39f,.36f));
            viewport.gameObject.AddComponent<RectMask2D>();
            map = MapUi.Rect(viewport,"Projected map",0,0,MapWidth,MapHeight);
            map.pivot = new Vector2(.5f,.5f);
            var image = map.gameObject.AddComponent<RawImage>();
            image.texture = Resources.Load<Texture2D>("WorldMap/world_map_san10") ?? M2RuntimeAssets.Load()?.map;
            image.color = image.texture == null ? MapUi.Paper : Color.white;
            image.raycastTarget = false;
            var regions = MapUi.Rect(map,"Region names",0,0,MapWidth,MapHeight);
            var roads = MapUi.Rect(map,"Post roads",0,0,MapWidth,MapHeight);
            routeLayer = MapUi.Rect(map,"Travel route",0,0,MapWidth,MapHeight);
            foreach (var region in data.Nodes.Values.GroupBy(node => node.region).Where(group => group.Count() >= 3))
            {
                var points = region.Select(Position).ToArray();
                Vector2 center = new(points.Average(point => point.x), points.Average(point => point.y));
                var name = MapUi.Text(regions, region.Key, center.x - 90, center.y - 48, 180, 30, 20,
                    new Color(1, .95f, .82f, .62f), TextAnchor.MiddleCenter, "Names");
                regionNames.Add(name.rectTransform);
                var outline = name.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(.10f, .08f, .05f, .55f);
                outline.effectDistance = new Vector2(1, -1);
            }
            // Subtle post roads: a soft earth-brown bed with a pale dashed track on top.
            foreach (var road in data.Roads)
            {
                var a = Position(data.Nodes[road.a]);
                var b = Position(data.Nodes[road.b]);
                Line(roads, a, b, new Color(.24f, .17f, .10f, .42f), 3f, false);
                Line(roads, a, b, new Color(.99f, .90f, .70f, .72f), 1.2f, true);
            }
            iconLayer = MapUi.Rect(map,"Stronghold icons",0,0,MapWidth,MapHeight);
            plateLayer = MapUi.Rect(map,"Nameplates",0,0,MapWidth,MapHeight);
            foreach (var node in data.Nodes.Values.OrderBy(node => Position(node).y)) AddNode(node);
            foreach (var group in data.Factions.GroupBy(f => f.seat))
            {
                if (!data.Nodes.TryGetValue(group.Key ?? "",out var node)) continue;
                Vector2 p = Position(node);
                int index = 0;
                foreach (var faction in group)
                {
                    int n = index++;
                    flags.Add((Banner(faction),p,n));
                }
            }
            focusMarker = MapUi.Rect(map,"Selected seat",0,0,36,36);
            var focusText = MapUi.Text(focusMarker,"◯",0,0,36,36,32,MapUi.Red,TextAnchor.MiddleCenter);
            focusText.fontStyle = FontStyle.Bold;
            focusMarker.gameObject.SetActive(false);
            party = MapUi.Panel(map,"Party",0,0,28,28,MapUi.Red);
            party.GetComponent<Image>().raycastTarget = false;
            party.pivot = Vector2.one*.5f;
            MapUi.Text(party,"行",0,0,28,28,18,MapUi.Paper,TextAnchor.MiddleCenter);
            card = MapUi.Rect(frame,"Map card",1113,96,426,680);
            var footer = MapUi.Kit(frame,"Map status bar",12,808,1576,82,"Wood",false);
            status = MapUi.Text(footer,"",22,6,1530,38,22,MapUi.Paper,TextAnchor.MiddleLeft,"Narration");
            MapUi.Text(footer,"滾輪縮放　右鍵或中鍵拖曳　WASD 平移",
                22,44,630,30,18,MapUi.Gold);
            var legend = MapUi.Kit(footer,"Legend paper tag",750,13,805,55,"Paper",false);
            MapUi.Text(legend,"大城 雄城重鎮　小城 縣鎮驛站　山門 門派名山　關隘 險要雄關　旗 勢力　行 隊伍",
                16,0,773,55,18,MapUi.Ink,TextAnchor.MiddleCenter);
            if (data.Nodes.Count == 0) status.text = "地圖資料尚未載入。";
            intel = new IntelPanel(frame,data,FocusFaction);
            ApplyView();
        }

        private Vector2 Position(MapNode node)
        {
            Vector2 uv = data.Bounds.Uv(node);
            return new Vector2(uv.x*MapWidth,(1-uv.y)*MapHeight);
        }

        private static readonly Dictionary<string, Texture2D> Icons = new();

        private static Texture2D Icon(string type)
        {
            string id = type switch
            {
                "capital" or "city" => "map_city_major",
                "sect" or "mountain" => "map_sect_peak",
                "pass" => "map_pass_gate",
                _ => "map_city_minor"
            };
            if (!Icons.TryGetValue(id, out var texture) || texture == null)
                Icons[id] = texture = Resources.Load<Texture2D>("WorldMap/" + id);
            return texture;
        }

        /// <summary>On-screen icon width per node type (before the gentle zoom growth).</summary>
        private static float IconWidth(string type) => type switch
        {
            "capital" => 78, "city" => 56, "sect" => 54, "mountain" => 44, "pass" => 48, _ => 42
        };

        private static string TypeName(string type) => type switch
        {
            "capital" => "京畿重鎮", "city" => "州城", "town" => "縣鎮", "outpost" => "邊鎮驛站",
            "sect" => "門派山門", "mountain" => "名山", "pass" => "關隘", _ => "據點"
        };

        /// <summary>Outlaw garrisons are thin: 80–160 men, fixed per stronghold.</summary>
        private static int Garrison(MapNode node)
        {
            int hash = 17;
            foreach (char c in node.id) hash = hash * 31 + c;
            return 80 + Mathf.Abs(hash % 81);
        }

        private static string OutlawGuard(MapNode node) => node.type is "capital" or "city" or "pass" ? "丙" : "丁";

        internal static BattleRequest OutlawEncounter(MapNode node, MapFaction faction)
        {
            bool fortified = OutlawGuard(node) == "丙";
            var request = BattleRequest.MapAssault(faction.Label, faction.leader, faction.PortraitId,
                Garrison(node), fortified ? .9f : .7f);
            request.ground = fortified ? BattleGround.Fortification
                : node.type == "sect" ? BattleGround.MountainGate : BattleGround.Road;
            return request;
        }

        private static string Defence(string type) => type switch
        {
            "capital" => "甲上", "pass" => "甲", "city" => "乙", "sect" => "乙", "outpost" => "丙", _ => "丁"
        };

        /// <summary>A small faction banner: ink pole, cloth in the faction colour and the faction's first glyph.</summary>
        private RectTransform Banner(MapFaction faction)
        {
            var button = MapUi.Button(map,"",0,0,30,34,() => FocusFaction(faction),Color.clear);
            var rect = (RectTransform)button.transform;
            rect.pivot = new Vector2(.5f,1);
            MapUi.Panel(rect,"Pole",2,0,2.5f,34,MapUi.Ink).GetComponent<Image>().raycastTarget = false;
            var cloth = MapUi.Panel(rect,"Banner",4.5f,1,24,24,faction.Tint);
            cloth.GetComponent<Image>().raycastTarget = false;
            var edge = cloth.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(.10f,.07f,.05f,.8f);
            edge.effectDistance = new Vector2(1,-1);
            var glyph = MapUi.Text(cloth,faction.Label.Substring(0,1),0,0,24,24,18,MapUi.Paper,TextAnchor.MiddleCenter,"Names");
            glyph.resizeTextForBestFit = false;
            MapUi.Panel(rect,"Fringe",4.5f,25,24,2,MapUi.Gold).GetComponent<Image>().raycastTarget = false;
            return rect;
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
            var texture = Icon(node.type);
            float width = IconWidth(node.type);
            float height = texture == null ? width * .6f : width * texture.height / texture.width;
            // The icon's foot (bottom-centre) stands on the node's map coordinate.
            var button = MapUi.Button(iconLayer, "", p.x, p.y, width, height, () => ShowNode(node), Color.clear);
            var marker = (RectTransform)button.transform;
            marker.pivot = new Vector2(.5f, .18f);
            nodeMarkers.Add((marker, width));
            var shade = MapUi.Rect(marker, "Ground shadow", width * .14f, height * .70f, width * .72f, height * .22f)
                .gameObject.AddComponent<Image>();
            shade.sprite = null;
            shade.color = new Color(0, 0, 0, .28f);
            shade.raycastTarget = false;
            var art = MapUi.Rect(marker, "據點圖示", 0, 0, width, height).gameObject.AddComponent<RawImage>();
            art.texture = texture;
            art.color = texture == null ? MapUi.Ink : Color.white;
            art.raycastTarget = true;

            bool major = node.type is "capital" or "city" or "sect";
            var plate = MapUi.Panel(plateLayer, "Nameplate", 0, 0, 80, 30, MapUi.Ink);
            plate.pivot = new Vector2(0, 1);
            var paper = MapUi.Panel(plate, "Plate paper", 1.5f, 1.5f, 77, 21,
                node.type == "capital" ? new Color(.62f, .22f, .15f) : new Color(.95f, .89f, .76f));
            paper.anchorMin = Vector2.zero;
            paper.anchorMax = Vector2.one;
            paper.offsetMin = new Vector2(1.5f, 1.5f);
            paper.offsetMax = new Vector2(-1.5f, -1.5f);
            paper.GetComponent<Image>().raycastTarget = false;
            var label = MapUi.Text(plate, node.Label, 0, 0, 80, 30, major ? 20 : 18,
                node.type == "capital" ? MapUi.Paper : MapUi.Ink, TextAnchor.MiddleCenter, "Names");
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            if (major) label.fontStyle = FontStyle.Bold;
            var plateButton = plate.gameObject.AddComponent<Button>();
            plateButton.transition = Selectable.Transition.None;
            plateButton.onClick.AddListener(() => { UiSfx.Play("click"); ShowNode(node); });
            labels.Add((node, plate, label, p));
        }

        private void Update()
        {
            if (!IsOpen || intel.IsOpen) return;
            Vector2 mouse = Input.mousePosition;
            // The node/faction card floats over the viewport: wheel and drags that start on it must not move the map.
            bool overMap = RectTransformUtility.RectangleContainsScreenPoint(viewport,mouse,canvas.worldCamera) && !OverCard(mouse);
            float wheel = Input.mouseScrollDelta.y;
            if (overMap && Mathf.Abs(wheel) > .01f)
            {
                // Zoom towards the cursor; the view eases to the new target below.
                RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport,mouse,canvas.worldCamera,out Vector2 point);
                Vector2 relative = point - new Vector2(ViewWidth/2,-ViewHeight/2);
                float next = Mathf.Clamp(targetZoom*Mathf.Pow(1.18f,wheel),MinZoom,MaxZoom);
                targetPan = relative + (targetPan-relative)*(next/targetZoom);
                targetZoom = next;
            }
            bool pressed = Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2) || Input.GetMouseButtonDown(0);
            if (pressed) dragFromMap = overMap;
            bool held = Input.GetMouseButton(1) || Input.GetMouseButton(2) || Input.GetMouseButton(0);
            if (!pressed && dragFromMap && held && mouse != lastMouse)
            {
                Vector2 delta = (mouse-lastMouse)/canvas.scaleFactor;
                pan += delta;
                targetPan += delta;
                changingScreen = true;
            }
            if (!held) dragFromMap = false;
            lastMouse = mouse;
            Vector2 move = new((Input.GetKey(KeyCode.A)?1:0)-(Input.GetKey(KeyCode.D)?1:0),
                (Input.GetKey(KeyCode.S)?1:0)-(Input.GetKey(KeyCode.W)?1:0));
            if (move != Vector2.zero)
                targetPan += move.normalized * Time.unscaledDeltaTime * 620;
            float ease = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 11);
            if (Mathf.Abs(targetZoom - zoom) > .0005f || (targetPan - pan).sqrMagnitude > .01f)
            {
                zoom = Mathf.Lerp(zoom, targetZoom, ease);
                pan = Vector2.Lerp(pan, targetPan, ease);
                changingScreen = true;
            }
            if (changingScreen) ApplyView();
        }

        private bool OverCard(Vector2 mouse)
        {
            for (int i=0;i<card.childCount;i++)
            {
                var child = (RectTransform)card.GetChild(i);
                if (child.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(child,mouse,canvas.worldCamera))
                    return true;
            }
            return false;
        }

        private void ApplyView()
        {
            float x = Mathf.Max(0,(MapWidth*zoom-ViewWidth)/2);
            float y = Mathf.Max(0,(MapHeight*zoom-ViewHeight)/2);
            pan = new Vector2(Mathf.Clamp(pan.x,-x,x),Mathf.Clamp(pan.y,-y,y));
            float tx = Mathf.Max(0,(MapWidth*targetZoom-ViewWidth)/2), ty = Mathf.Max(0,(MapHeight*targetZoom-ViewHeight)/2);
            targetPan = new Vector2(Mathf.Clamp(targetPan.x,-tx,tx),Mathf.Clamp(targetPan.y,-ty,ty));
            map.anchoredPosition = new Vector2(ViewWidth/2,-ViewHeight/2)+pan;
            map.localScale = Vector3.one*zoom;
            LayoutLabels();
            if (!traveling) SyncParty();
            changingScreen = false;
        }

        /// <summary>Icons grow gently as the map zooms in, but never balloon with the terrain.</summary>
        private float IconScale => Mathf.Lerp(.82f, 1.25f, Mathf.InverseLerp(MinZoom, MaxZoom, zoom));

        private void LayoutLabels()
        {
            var occupied = new List<Rect>();
            float iconScale = IconScale;
            foreach (var (marker, _) in nodeMarkers) marker.localScale = Vector3.one * iconScale / zoom;
            party.localScale = Vector3.one/zoom;
            focusMarker.localScale = Vector3.one/zoom;
            focusMarker.anchoredPosition = new Vector2(focusedPoint.x - 18 / zoom, -focusedPoint.y + 18 / zoom);
            Vector2 offset = new((ViewWidth-MapWidth*zoom)/2+pan.x,(ViewHeight-MapHeight*zoom)/2-pan.y);
            if (party.gameObject.activeSelf && data.Nodes.TryGetValue(host?.PartyNode ?? "",out var partyNode))
                occupied.Add(new Rect(Position(partyNode)*zoom + offset + new Vector2(-30.5f,3.5f),new Vector2(23,23)));
            foreach (var (flag,point,seatOrder) in flags)
            {
                flag.localScale = Vector3.one/zoom;
                Vector2 marker = point*zoom + offset;
                Vector2 position = new(14 + seatOrder % 3 * 22, -46 * iconScale - seatOrder / 3 * 30);
                var bounds = new Rect(marker + position - new Vector2(12, 1), new Vector2(24, 30));
                int guard = 0;
                while (occupied.Any(other => other.Overlaps(bounds)) && guard++ < 6)
                {
                    position.y -= 30;
                    bounds.y -= 30;
                }
                Vector2 local = point + position/zoom;
                flag.anchoredPosition = new Vector2(local.x,-local.y);
                occupied.Add(bounds);
            }
            Rect cardBounds = openedCard == null ? default :
                new Rect(card.anchoredPosition.x - viewport.anchoredPosition.x,
                    viewport.anchoredPosition.y - card.anchoredPosition.y, openedCard.rect.width, openedCard.rect.height);
            bool IsVisible(Rect bounds) => bounds.xMin >= 3 && bounds.xMax <= ViewWidth - 3 &&
                bounds.yMin >= 3 && bounds.yMax <= ViewHeight - 3 &&
                (openedCard == null || !cardBounds.Overlaps(bounds));

            foreach (var region in regionNames)
            {
                Vector2 topLeft = new(region.anchoredPosition.x, -region.anchoredPosition.y);
                region.gameObject.SetActive(IsVisible(new Rect(topLeft * zoom + offset, region.sizeDelta * zoom)));
            }

            foreach (var (node,plate,label,point) in labels.OrderBy(entry => entry.node.id == host?.PartyNode ? -1 : LabelPriority(entry.node)))
            {
                plate.localScale = Vector3.one/zoom;
                float width = Mathf.Clamp(label.text.Length * (label.fontSize + 1) + 16, 48, 180);
                plate.sizeDelta = new Vector2(width,30);
                Vector2 marker = point*zoom+offset;
                float icon = IconWidth(node.type) * iconScale;
                Vector2[] candidates = {
                    new(-width/2, 10), new(icon/2, -12), new(-width-icon/2, -12), new(-width/2, -icon*.62f - 26)
                };
                bool shown = false;
                if (LabelPriority(node) < 3 || seats.Contains(node.id) || zoom >= 1.7f || node.id == host?.PartyNode)
                    foreach (var position in candidates)
                    {
                        var bounds = new Rect(marker+position,new Vector2(width,30));
                        if (!IsVisible(bounds) || occupied.Any(other => other.Overlaps(bounds))) continue;
                        Vector2 local = point + position/zoom;
                        plate.anchoredPosition = new Vector2(local.x,-local.y);
                        occupied.Add(bounds);
                        shown = true;
                        break;
                    }
                if (plate.gameObject.activeSelf != shown) plate.gameObject.SetActive(shown);
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
            openedCard = null;
            LayoutLabels();
            cardActions.Clear();
            MapUi.Clear(routeLayer);
            focusMarker.gameObject.SetActive(false);
        }

        private RectTransform Card(string title,string subtitle)
        {
            ClearCard();
            var panel = MapUi.Kit(card,title,0,0,426,680,"Paper");
            openedCard = panel;
            LayoutLabels();
            MapUi.Text(panel,title,20,16,342,48,31,MapUi.Ink);
            MapUi.Text(panel,subtitle,20,66,382,32,19,MapUi.Red,TextAnchor.MiddleLeft,"Names");
            var divider = MapUi.Rect(panel,"Divider",20,100,386,8).gameObject.AddComponent<Image>();
            divider.sprite = UiKit.Sprite("Divider");
            divider.raycastTarget = false;
            if (divider.sprite == null) divider.color = MapUi.Gold;
            MapUi.Button(panel,"×",370,18,38,38,ClearCard,MapUi.Red,22);
            return panel;
        }

        /// <summary>San10-style stronghold command card: banner, leader, garrison, roads and scout / intel / march actions.</summary>
        private void ShowNode(MapNode node)
        {
            if (traveling) return;
            var factions = data.Controllers(node).ToArray();
            var faction = factions.FirstOrDefault();
            bool held = host.HoldsNode(node.id);
            bool outlaw = faction != null && faction.Outlaw && !held;
            var panel = Card(node.Label, $"{TypeName(node.type)}　{node.region ?? ""}");
            FocusMarker(node);
            bool known = faction != null && host.IsFactionKnown(faction.id);
            // Faction banner hanging from the card's top edge (the sect's own banner once taken).
            var banner = MapUi.Panel(panel, "勢力旗", 300, 0, 58, 92,
                held ? new Color(.55f, .16f, .12f) : faction?.Tint ?? new Color(.45f, .42f, .36f));
            banner.GetComponent<Image>().raycastTarget = false;
            MapUi.Panel(banner, "旗緣", 0, 86, 58, 6, MapUi.Gold).GetComponent<Image>().raycastTarget = false;
            MapUi.Text(banner, held ? host.SectLabel.Substring(0, 1) : faction == null ? "無" : faction.Label.Substring(0, 1),
                0, 8, 58, 70, 40, MapUi.Paper, TextAnchor.MiddleCenter, "Names");
            if (faction != null)
            {
                MapUi.Portrait(panel, known ? PortraitService.Resolve(faction.PortraitId) : null, 20, 116, 104, 138);
                if (!known) MapUi.Text(panel, "？", 20, 116, 104, 138, 54, MapUi.Paper, TextAnchor.MiddleCenter);
                MapUi.Text(panel, "控制勢力", 140, 112, 140, 24, 18, MapUi.Red);
                MapUi.Text(panel, faction.Label, 140, 134, 266, 34, 26, MapUi.Ink);
                MapUi.Text(panel, known ? $"首領　{faction.leader ?? "不詳"}" : "首領　尚待打探", 140, 168, 266, 26, 19, MapUi.Ink);
                string relation = faction.RelationWith(host.RelationShift(faction.id));
                MapUi.Text(panel, $"關係　{relation}", 140, 194, 266, 26, 19,
                    relation == "敵對" ? MapUi.Red : relation == "友好" ? MapUi.Jade : MapUi.Ink);
            }
            else
                MapUi.Text(panel, "此地沒有已知的控制勢力", 20, 120, 386, 34, 21, MapUi.Jade);
            if (held || outlaw)
                MapUi.Text(panel, held ? $"【本派據點・{host.SectLabel}】" : "【草莽據點・易攻】", 140, 222, 266, 30, 20,
                    held ? MapUi.Jade : MapUi.Red, TextAnchor.MiddleLeft, "Names");

            // Garrison / strength ledger.
            int y = faction == null ? 160 : 262;
            int garrison = outlaw ? Garrison(node) : 0;
            Stat(20, "守備", held ? "本派" : outlaw ? OutlawGuard(node) : Defence(node.type));
            Stat(150, "兵力", outlaw ? garrison.ToString() : known ? $"{Mathf.RoundToInt(faction.disciples * (seats.Contains(node.id) ? .6f : .2f)):N0}" : faction == null ? "—" : "？");
            Stat(280, "聲望", known ? faction.reputation.ToString() : faction == null ? "—" : "？");
            y += 58;
            if (factions.Length > 1)
            {
                MapUi.Text(panel, "另有勢力　" + string.Join("、", factions.Skip(1).Select(f => f.Label)), 20, y, 386, 24, 18, MapUi.Ink);
                y += 26;
            }
            MapUi.Text(panel, node.desc ?? "", 20, y, 386, 64, 18, MapUi.Ink, TextAnchor.UpperLeft, "Narration");
            y += 70;

            // Connected roads as chips; clicking one jumps to that stronghold.
            MapUi.Text(panel, "相通驛路", 20, y, 200, 24, 18, MapUi.Red);
            y += 28;
            var roads = data.Connections(node);
            if (roads.Count == 0) MapUi.Text(panel, "沒有相連道路", 20, y, 386, 26, 18, MapUi.Ink);
            int chip = 0;
            foreach (var road in roads.GroupBy(road => road.Other(node.id)).Select(group => group.First()).Take(6))
            {
                var other = data.Nodes[road.Other(node.id)];
                var button = MapUi.Button(panel, $"{other.Label}　{road.days}日", 20 + chip % 3 * 130, y + chip / 3 * 36, 124, 32,
                    () => ShowNode(other), null, 16);
                cardActions.Add(button);
                chip++;
            }
            y += Mathf.Max(1, (chip + 2) / 3) * 36 + 6;

            var route = data.Route(host.PartyNode, node.id);
            string message = node.id == host.PartyNode ? "隊伍就在此地" : route == null ? "沒有可通的道路" :
                $"行軍　{route.Days} 日　行動力 {host.TravelCost(route.Ap)}";
            if (outlaw) message += $"　戰力 {host.SectPower}・攻討 2 點";
            MapUi.Text(panel, message, 20, y, 386, 28, 20, MapUi.Jade);
            if (route != null)
            {
                MapUi.Text(panel, string.Join(" → ", route.Nodes.Select(data.NodeLabel)), 20, y + 28, 386, 40, 18,
                    MapUi.Ink, TextAnchor.UpperLeft);
                for (int i = 1; i < route.Nodes.Count; i++)
                    Line(routeLayer, Position(data.Nodes[route.Nodes[i - 1]]), Position(data.Nodes[route.Nodes[i]]), MapUi.Red, 3, false);
            }

            // Command row: scout (or 率眾攻討 on outlaw holds) / intel / march.
            if (outlaw)
            {
                float guard = OutlawGuard(node) == "丙" ? .9f : .7f;
                var assault = MapUi.Button(panel, $"率眾攻討\n勝算 {host.AssaultChance(garrison, guard):P0}", 20, 610, 124, 48, () =>
                {
                    if (BattleRequested != null)
                    {
                        var encounter = OutlawEncounter(node, faction);
                        BattleRequested.Invoke(encounter, won =>
                        {
                            if (won)
                            {
                                string result = host.Assault(node.id, faction.id, garrison, 0.01f);
                                status.text = result;
                            }
                            else
                            {
                                status.text = $"攻討{data.NodeLabel(node.id)}失利，暫退休整。";
                            }
                            UpdateAp();
                            ShowNode(node);
                        });
                        return;
                    }
                    string resultFallback = host.Assault(node.id, faction.id, garrison, guard);
                    AudioManager.Play(host.HoldsNode(node.id) ? "complete" : "page");
                    UpdateAp();
                    ShowNode(node);
                    status.text = resultFallback;
                }, MapUi.Red, 15);
                assault.interactable = host.ActionPoints >= 2;
                cardActions.Add(assault);
            }
            else if (held)
            {
                var own = MapUi.Button(panel, "本派據點", 20, 610, 124, 48, () => { }, null, 18);
                own.interactable = false;
                cardActions.Add(own);
            }
            else if (faction != null && !known)
            {
                var scout = MapUi.Button(panel, "派人打探　行動力 1", 20, 610, 124, 48, () =>
                {
                    if (!host.TrySpendAp(1, "scout:" + faction.id)) return;
                    host.SetFactionKnown(faction.id);
                    UpdateAp();
                    AudioManager.Play("page");
                    ShowNode(node);
                }, null, 16);
                scout.interactable = host.ActionPoints >= 1;
                cardActions.Add(scout);
            }
            else
            {
                var leader = MapUi.Button(panel, "拜會首領", 20, 610, 124, 48, () => LeaderSelected?.Invoke(faction), null, 18);
                leader.interactable = faction != null;
                cardActions.Add(leader);
            }
            var intelButton = MapUi.Button(panel, "勢力情報", 151, 610, 124, 48, () =>
            {
                if (faction != null) ShowFaction(faction);
                else OpenIntel();
            }, null, 18);
            cardActions.Add(intelButton);
            var confirm = MapUi.Button(panel, "確認啟程", 282, 610, 124, 48, () => StartTravel(route, node.id), MapUi.Red, 18);
            confirm.interactable = route != null && host.ActionPoints >= host.TravelCost(route.Ap);
            cardActions.Add(confirm);

            void Stat(int x, string caption, string value)
            {
                var box = MapUi.Kit(panel, caption, x, y, 120, 52, "Wood", false);
                MapUi.Text(box, caption, 10, 3, 60, 24, 18, MapUi.Gold).verticalOverflow = VerticalWrapMode.Overflow;
                MapUi.Text(box, value, 10, 20, 100, 30, 22, MapUi.Paper, TextAnchor.MiddleRight, "Numbers").verticalOverflow = VerticalWrapMode.Overflow;
            }
        }

        internal event Action<MapFaction> LeaderSelected;

        internal void ShowFaction(MapFaction faction)
        {
            if (traveling) return;
            var panel = Card(faction.Label,$"{faction.TypeName}　{(faction.Court ? "朝廷／官府　" : "")}{data.NodeLabel(faction.seat)}");
            bool known = host.IsFactionKnown(faction.id);
            int shift = host.RelationShift(faction.id);
            string current = faction.StanceWith(shift);
            var stance = current == "friendly" ? new Color(.20f,.39f,.28f) :
                current == "hostile" ? MapUi.Red : MapUi.Ink;
            MapUi.Text(panel,"關係　"+faction.RelationWith(shift),20,105,270,32,21,stance);
            if (!known)
            {
                MapUi.Text(panel,"尚待打探首領與兵力",20,166,386,48,23,MapUi.Ink);
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
            MapUi.Text(panel,$"首領　{faction.leader ?? "—"}",20,149,274,32,22,MapUi.Ink);
            MapUi.Button(panel,"掌門",303,105,103,34,() => LeaderSelected?.Invoke(faction),MapUi.Red);
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
                MapUi.Text(panel,notable.desc ?? "",20,y+26,386,25,18,MapUi.Ink);
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
                MapUi.Text(panel,names[line.id],x,y,77,21,18,MapUi.Ink);
                Color tier = RankSystem.Color(line.rank);
                MapUi.Text(panel,RankSystem.FormatKnown(line.rank,line.cap),x+77,y,102,21,18,tier);
                float cap = Mathf.Clamp01((RankSystem.Order(line.cap)+1)/7f);
                float rank = Mathf.Min(cap,Mathf.Clamp01((RankSystem.Order(line.rank)+1)/7f));
                if (cap > 0) MapUi.Panel(panel,"Rank cap",x,y+22,184*cap,3,new Color(.72f,.63f,.49f));
                if (rank > 0) MapUi.Panel(panel,"Current rank",x,y+22,184*rank,3,tier);
            }
            if (row == 0) MapUi.Text(panel,"—",x,478,185,26,18,MapUi.Ink);
        }

        private void OpenIntel()
        {
            if (traveling) return;
            AudioManager.Play("page");
            intel.Open(host);
        }

        internal void OpenIntelOverview() => OpenIntel();
        internal void CaptureIntel() => OpenIntel();
        internal void CapturePeopleIntel() => intel.CapturePeopleTab();

        internal void CaptureStronghold(MapFaction faction)
        {
            CenterOn(faction.seat);
            ApplyView();
            ShowNode(data.Nodes[faction.seat]);
        }

        internal void FocusFaction(MapFaction faction)
        {
            if (traveling) return;
            intel.Close();
            if (data.Nodes.TryGetValue(faction.seat ?? "",out var node))
            {
                zoom = targetZoom = 1.85f;
                Vector2 p = Position(node);
                pan = targetPan = new Vector2((MapWidth/2-p.x)*zoom-150,(p.y-MapHeight/2)*zoom);
                ApplyView();
            }
            ShowFaction(faction);
            if (data.Nodes.TryGetValue(faction.seat ?? "",out var focused))
                FocusMarker(focused);
        }

        private void FocusMarker(MapNode node)
        {
            focusedPoint = Position(node);
            focusMarker.anchoredPosition = new Vector2(focusedPoint.x - 18 / zoom, -focusedPoint.y + 18 / zoom);
            focusMarker.gameObject.SetActive(true);
        }

        private void CenterOn(string id)
        {
            if (!data.Nodes.TryGetValue(id ?? "",out var node)) return;
            zoom = targetZoom = Mathf.Max(zoom,MinZoom);
            Vector2 p = Position(node);
            pan = targetPan = new Vector2((MapWidth/2-p.x)*zoom,(p.y-MapHeight/2)*zoom);
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
            if (traveling || !host.TrySpendAp(host.TravelCost(route.Ap),"travel:"+destination)) return;
            selected = destination;
            traveling = true;
            host.AdvanceDays(route.Days);
            foreach (var button in cardActions) button.interactable = false;
            UpdateAp();
            status.text = "一行人正在趕路……";
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
            string fortune = host.TravelFortune();
            status.text = $"抵達{data.NodeLabel(destination)}。{fortune ?? events[UnityEngine.Random.Range(0,events.Length)]}";
        }
    }
}
