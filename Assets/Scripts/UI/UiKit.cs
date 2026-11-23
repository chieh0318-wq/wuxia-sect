using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    internal static class UiKit
    {
        internal static readonly Color WarmPaper = new(.93f, .86f, .73f);
        internal static readonly Color JadeInk = new(.20f, .39f, .28f);

        private static Texture2D backdrop;
        private static Texture2D vignette;

        internal static Texture2D Vignette()
        {
            if (vignette != null) return vignette;
            const int size = 64;
            vignette = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var offset = new Vector2(x, y) / (size - 1) * 2 - Vector2.one;
                float shade = Mathf.SmoothStep(0, .86f, Mathf.InverseLerp(.46f, 1.25f, offset.magnitude));
                vignette.SetPixel(x, y, new Color(.025f, .035f, .04f, shade));
            }
            vignette.Apply(false, true);
            return vignette;
        }

        internal static Texture2D Backdrop()
        {
            if (backdrop != null)
                return backdrop;
            backdrop = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < backdrop.height; y++)
                backdrop.SetPixel(0, y, Color.Lerp(new Color(.04f, .08f, .09f), new Color(.16f, .27f, .27f), y / 63f));
            backdrop.Apply(false, true);
            return backdrop;
        }

        // ── M7 Koei / Zongmen-Origin theme: dark lacquer, antique gold rules, aged xuan paper, crimson seals ──
        internal static readonly Color Lacquer = Hex("#1B1714");
        internal static readonly Color LacquerDeep = Hex("#1A1714");
        internal static readonly Color AntiqueGold = Hex("#C6A25A");
        internal static readonly Color TitleGold = Hex("#C8A86B");
        internal static readonly Color HeaderGold = Hex("#E6C98A");
        internal static readonly Color JadeHeader = Hex("#162629");
        internal static readonly Color Crimson = Hex("#A8382B");
        internal static readonly Color JadeAccent = Hex("#3B7A6E");
        internal static readonly Color GoldAccent = Hex("#C69B4B");
        internal static readonly Color Ivory = Hex("#F3E7CC");

        /// <summary>Readability floor: body text never renders below 18 reference pixels.</summary>
        internal const int MinBody = 18, MinTitle = 22;
        internal static int Readable(int size) => Mathf.Max(MinBody, size);

        internal static Color Hex(string html) => ColorUtility.TryParseHtmlString(html, out var color) ? color : Color.magenta;

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> themed = new();
        private static readonly System.Collections.Generic.Dictionary<string, Texture2D> iconTexCache = new();
        private static readonly System.Collections.Generic.Dictionary<string, Sprite> iconSpriteCache = new();

        internal static Texture2D Icon(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (iconTexCache.TryGetValue(name, out var tex)) return tex;
            tex = Resources.Load<Texture2D>("Icons/" + name);
            iconTexCache[name] = tex;
            return tex;
        }

        internal static Sprite IconSprite(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (iconSpriteCache.TryGetValue(name, out var sp) && sp != null) return sp;
            var tex = Icon(name);
            if (tex == null) return null;
            sp = UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * .5f, 100);
            iconSpriteCache[name] = sp;
            return sp;
        }

        internal static string AttrIcon(int slot) => slot switch
        {
            0 => "attr_str", 1 => "attr_agi", 2 => "attr_con", 3 => "attr_qi",
            4 => "attr_wit", 5 => "attr_cha", 6 => "attr_luk", _ => "attr_str"
        };

        internal static string SkillIcon(string id) => id switch
        {
            "fist" => "skill_fist", "blade" => "skill_blade", "sword" => "skill_sword",
            "hidden" => "skill_hidden", "qinggong" => "skill_qinggong", "neigong" => "skill_neigong",
            "polearm" => "skill_polearm", "medicine" => "skill_medicine", "alchemy" => "skill_alchemy",
            "forging" => "skill_forging", "commerce" => "skill_commerce", "farming" => "skill_farming",
            "construction" => "skill_construction", "teaching" => "skill_teaching", _ => "skill_sword"
        };

        internal static string FactionIcon(MapFaction faction) => Diplomacy.Group(faction) switch
        {
            "jianghu" => "faction_sect",
            "bandit" => "faction_bandit",
            _ => "faction_court"
        };

        internal static string TechIcon(string branch, int index)
        {
            string[] icons = branch.Contains("劍") ? new[] { "skill_sword", "item_manual_sword", "act_attack", "act_ultimate" }
                : branch.Contains("外功") ? new[] { "skill_fist", "skill_blade", "skill_hidden", "skill_polearm" }
                : branch.Contains("內功") ? new[] { "skill_neigong", "skill_qinggong", "item_manual_qi", "attr_qi" }
                : new[] { "skill_construction", "skill_farming", "skill_medicine", "skill_forging" };
            return icons[Mathf.Clamp(index, 0, icons.Length - 1)];
        }

        internal static string HeroTitle(ProtagonistData hero) =>
            string.IsNullOrWhiteSpace(hero.title) || hero.title is "三師兄" or "三師姐" ? hero.name : hero.title;

        internal static string TalentIcon(CreationOption option)
        {
            var effect = option.effects != null && option.effects.Length > 0 ? option.effects[0] : null;
            if (effect == null) return "act_skill";
            return effect.key switch
            {
                "str" => "attr_str", "agi" => "attr_agi", "con" => "attr_con", "qi" => "attr_qi",
                "wit" => "attr_wit", "cha" => "attr_cha", "luk" => "attr_luk",
                "silver" or "buildCostPct" or "recruitCostPct" => "hud_silver",
                "loyaltyAll" => "hud_loyalty", "relation" => "hud_affinity",
                "food" => "hud_food", "apMax" => "hud_ap", "reputation" => "hud_rep",
                "recruitPoolSize" or "followers" => "hud_disciple",
                "rank" or "lineRank" or "cap" => SkillIcon(effect.@ref),
                "noGraveInjury" => "skill_medicine",
                _ => "act_skill"
            };
        }

        internal static string SlotIcon(string slot) => slot switch
        {
            "武器" => "slot_weapon", "護腕" => "slot_bracer", "飾品" => "slot_trinket",
            "冠帽" => "slot_hat", "衣袍" => "slot_robe", "履靴" => "slot_boots", _ => "slot_weapon"
        };

        /// <summary>
        /// Theme sprites are generated at runtime so every screen picks up the lacquer/gold/xuan look without
        /// re-importing art. Names that are not themed fall back to the InkUi resources (icons, dividers).
        /// </summary>
        internal static Sprite Sprite(string name)
        {
            if (themed.TryGetValue(name, out var cached) && cached != null) return cached;
            var made = name switch
            {
                "Paper" => Frame(64, 12, PaperPixel),
                "Xuan" => Frame(96, 26, XuanPixel),
                "Lacquer" => Frame(96, 26, LacquerPixel),
                "Wood" or "Card" => Frame(64, 12, CardPixel),
                "Teal" or "Jade" or "Plaque" => Frame(64, 12, JadePixel),
                "Vermilion" => Frame(64, 12, (x, y, n) => ButtonPixel(x, y, n, Hex("#7E2A20"), Hex("#A8382B"))),
                "Selected" => Frame(64, 12, (x, y, n) => ButtonPixel(x, y, n, Hex("#6B2219"), Hex("#93372A"), true)),
                "TitleMenu" => Frame(96, 26, TitleMenuPixel, new Vector4(30, 14, 14, 14)),
                "Seal" => Frame(64, 12, SealPixel),
                _ => null
            };
            if (made != null)
            {
                themed[name] = made;
                return made;
            }
            return Resources.Load<Sprite>("InkUi/" + name);
        }

        private static Sprite Frame(int size, int border, System.Func<int, int, int, Color> pixel, Vector4? borders = null)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "M7 theme"
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = pixel(x, y, size);
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return UnityEngine.Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 100, 0,
                SpriteMeshType.FullRect, borders ?? Vector4.one * border);
        }

        private static int Edge(int x, int y, int n) => Mathf.Min(Mathf.Min(x, y), Mathf.Min(n - 1 - x, n - 1 - y));
        private static int EdgeX(int x, int n) => Mathf.Min(x, n - 1 - x);
        private static int EdgeY(int y, int n) => Mathf.Min(y, n - 1 - y);
        private static float Grain(int x, int y, float fx, float fy) => Mathf.PerlinNoise(x * fx + 3.1f, y * fy + 7.7f);

        /// <summary>Aged xuan paper inside a lacquer rim with double antique-gold rules and corner brackets.</summary>
        private static Color XuanPixel(int x, int y, int n)
        {
            int edge = Edge(x, y, n), ex = EdgeX(x, n), ey = EdgeY(y, n);
            if (edge <= 5) return Color.Lerp(Lacquer, Hex("#2A221B"), Grain(x, y, .4f, .4f) * .6f);
            if (edge == 6 || edge == 7) return AntiqueGold;
            if (edge == 8 || edge == 9) return Color.Lerp(Lacquer, Hex("#3A2E22"), .4f);
            if (edge == 10) return Color.Lerp(AntiqueGold, Hex("#8C6A37"), .35f);
            if (ex < 26 && ey < 26 && ex >= 14 && ey >= 14 && (ex == 14 || ey == 14)) return AntiqueGold;
            if (ex >= 17 && ex <= 20 && ey >= 17 && ey <= 20) return Crimson;
            float fibre = Grain(x, y, .09f, .55f) * .55f + Grain(x, y, .37f, .21f) * .45f;
            var paper = Color.Lerp(Hex("#E3D3B0"), Hex("#F1E6CD"), fibre);
            float age = Mathf.InverseLerp(26, 11, edge);
            return Color.Lerp(paper, Hex("#C9B188"), age * .45f);
        }

        /// <summary>Small xuan-paper tag: thin lacquer rim and a single gold rule.</summary>
        private static Color PaperPixel(int x, int y, int n)
        {
            int edge = Edge(x, y, n);
            if (edge <= 1) return Lacquer;
            if (edge == 3) return AntiqueGold;
            if (edge == 2) return Color.Lerp(Lacquer, Hex("#3A2E22"), .5f);
            float fibre = Grain(x, y, .09f, .55f) * .55f + Grain(x, y, .37f, .21f) * .45f;
            return Color.Lerp(Color.Lerp(Hex("#E3D3B0"), Hex("#F1E6CD"), fibre), Hex("#CDB68D"), Mathf.InverseLerp(9, 4, edge) * .5f);
        }

        /// <summary>Obsidian lacquer window frame with double gold rules and corner ornaments.</summary>
        private static Color LacquerPixel(int x, int y, int n)
        {
            int edge = Edge(x, y, n), ex = EdgeX(x, n), ey = EdgeY(y, n);
            if (edge <= 1) return Hex("#0E0B09");
            if (edge == 2 || edge == 3) return AntiqueGold;
            if (edge == 7) return Color.Lerp(AntiqueGold, Lacquer, .25f);
            if (ex < 24 && ey < 24 && ((ex == 11 && ey >= 11) || (ey == 11 && ex >= 11))) return AntiqueGold;
            if (Mathf.Abs(ex - 16) + Mathf.Abs(ey - 16) <= 2) return Crimson;
            return Color.Lerp(Lacquer, Hex("#271F19"), Grain(x, y, .05f, .3f) * .7f);
        }

        /// <summary>Dark card body with a thin double gold rule (left accent stripes are added per card).</summary>
        private static Color CardPixel(int x, int y, int n)
        {
            int edge = Edge(x, y, n);
            if (edge == 0) return Hex("#0F0C0A");
            if (edge == 1) return Color.Lerp(AntiqueGold, Lacquer, .15f);
            if (edge == 4) return Color.Lerp(AntiqueGold, Lacquer, .62f);
            return Color.Lerp(Hex("#211B16"), Hex("#2E261F"), Grain(x, y, .06f, .25f) * .8f + (1 - y / (float)n) * .2f);
        }

        /// <summary>Dark-jade ink header bar with gold rules top and bottom.</summary>
        private static Color JadePixel(int x, int y, int n)
        {
            int edge = Edge(x, y, n);
            if (edge == 0) return Hex("#0B1314");
            if (edge == 1 || edge == 2) return AntiqueGold;
            if (edge == 5) return Color.Lerp(AntiqueGold, JadeHeader, .5f);
            return Color.Lerp(JadeHeader, Hex("#20393B"), y / (float)n * .7f + Grain(x, y, .07f, .3f) * .3f);
        }

        private static Color ButtonPixel(int x, int y, int n, Color low, Color high, bool selected = false)
        {
            int edge = Edge(x, y, n);
            if (edge == 0) return Hex("#120B08");
            if (edge == 1 || (selected && edge == 2)) return selected ? HeaderGold : AntiqueGold;
            if (edge == 4) return Color.Lerp(AntiqueGold, low, .55f);
            return Color.Lerp(low, high, y / (float)n * .75f + Grain(x, y, .08f, .3f) * .25f);
        }

        /// <summary>Grand title-menu plaque: dark lacquer, gold border, crimson seal accent on the left.</summary>
        private static Color TitleMenuPixel(int x, int y, int n)
        {
            int edge = Edge(x, y, n);
            if (edge == 0) return Hex("#0A0807");
            if (edge == 1 || edge == 2) return TitleGold;
            if (edge == 5) return Color.Lerp(TitleGold, LacquerDeep, .55f);
            if (x >= 9 && x <= 21 && y >= 9 && y <= n - 10)
                return (x == 9 || x == 21 || y == 9 || y == n - 10) ? Hex("#E0B979") : Crimson;
            return Color.Lerp(LacquerDeep, Hex("#2A221B"), y / (float)n * .5f + Grain(x, y, .06f, .3f) * .3f);
        }

        private static Color SealPixel(int x, int y, int n)
        {
            int edge = Edge(x, y, n);
            if (edge <= 1) return Color.clear;
            if (edge == 4) return Hex("#E8C38A");
            return Color.Lerp(Hex("#8E2A1F"), Crimson, Grain(x, y, .2f, .2f));
        }

        /// <summary>Vertical category stripe on the left edge of a card (crimson / jade / gold).</summary>
        internal static Image AccentStripe(RectTransform card, Color color, float width = 7)
        {
            var stripe = new GameObject("Accent stripe", typeof(RectTransform)).GetComponent<RectTransform>();
            stripe.SetParent(card, false);
            stripe.anchorMin = Vector2.zero;
            stripe.anchorMax = Vector2.up;
            stripe.offsetMin = new Vector2(4, 5);
            stripe.offsetMax = new Vector2(4 + width, -5);
            var image = stripe.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        internal static Font Font(string role, Font fallback, string text = "")
        {
            var font = Resources.Load<Font>("Fonts/" + role);
            if (font == null)
                return fallback;
            return System.Linq.Enumerable.Any(text, ch => !char.IsWhiteSpace(ch) && !font.HasCharacter(ch)) ? fallback : font;
        }

        internal static void Skin(Image image, string name)
        {
            var sprite = Sprite(name);
            if (sprite == null)
                return;
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
    }

    internal sealed class InkPanelMotion : MonoBehaviour
    {
        private CanvasGroup group;
        private float start;
        private RectTransform panel;
        private Vector2 restingPosition;

        private void OnEnable()
        {
            if (group == null)
                group = gameObject.AddComponent<CanvasGroup>();
            panel = (RectTransform)transform;
            restingPosition = panel.anchoredPosition;
            start = Time.unscaledTime;
            group.alpha = .65f;
            panel.anchoredPosition = restingPosition + Vector2.down * 8;
        }

        private void Update()
        {
            float progress = Mathf.Clamp01((Time.unscaledTime - start) / .12f);
            if (progress >= 1 && group.alpha >= 1)
                return;
            group.alpha = Mathf.Lerp(.65f, 1, progress);
            panel.anchoredPosition = restingPosition + Vector2.down * (8 * (1 - progress));
        }
        private void OnDisable()
        {
            if (panel != null && group.alpha < 1)
                panel.anchoredPosition = restingPosition;
        }
    }
}

namespace Wuxia
{
    /// <summary>Warm gold hover glow for grand menu plaques (title menu, dossier actions).</summary>
    internal sealed class HoverGlow : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler,
        UnityEngine.EventSystems.IPointerExitHandler
    {
        private Image glow;
        private Text caption;
        private Color restingText;
        private float target, level;

        internal void Initialize(Image image, Text text)
        {
            glow = image;
            caption = text;
            if (caption != null) restingText = caption.color;
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData) => target = 1;
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData) => target = 0;

        private void Update()
        {
            if (glow == null || Mathf.Approximately(level, target)) return;
            level = Mathf.MoveTowards(level, target, Time.unscaledDeltaTime * 6);
            var color = glow.color;
            color.a = .26f * level;
            glow.color = color;
            if (caption != null) caption.color = Color.Lerp(restingText, new Color(1, .86f, .52f), level);
        }
    }
}
