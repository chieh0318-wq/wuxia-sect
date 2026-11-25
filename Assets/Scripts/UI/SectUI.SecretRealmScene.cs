using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>One spoken line in a realm scene. side: "left" (our side), "right" (the other party) or "narrator".</summary>
    internal sealed class SceneLine
    {
        public string name, subtitle, text, side = "narrator";
        public Texture2D figure;
    }

    /// <summary>One answer card: label, flavour, tags (attribute / odds / cost) and the 「影響」 line.</summary>
    internal sealed class SceneChoice
    {
        public string label, desc, tags, impact, icon;
        public Action run;
        public string block;
    }

    /// <summary>
    /// v1124 C15–C18 scene stage: a full-screen page on the sect canvas with a painted background, large half-body
    /// standees on the left (our side) and right (the other party), a wide dialogue box with a name plate, answer cards
    /// that carry an 「影響」 line, and a reward page. Used by 山道巡訪, 秘境探尋, the fixed 寶物秘境, 山門演武 and 堂口.
    /// Layout is in the sect canvas' 1920×1080 reference space; P2 restyles it with the A3 dialogue kit.
    /// </summary>
    internal sealed class SceneStage
    {
        private const float W = 1920, H = 1080;
        private readonly RectTransform root;
        private RectTransform page;
        private string title, subtitle, sceneId;
        private SceneLine left, right;

        internal bool IsOpen => root.gameObject.activeSelf;
        internal RectTransform Page => page;

        internal SceneStage(Canvas canvas)
        {
            root = MapUi.Rect(canvas.transform, "Realm scene", 0, 0, 0, 0);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.gameObject.AddComponent<Image>().color = Color.black;
            root.gameObject.SetActive(false);
        }

        /// <summary>Starts a scene: header text, background id (realms.json scenes) and the two standing figures.</summary>
        internal void Begin(string title, string subtitle, string scene, SceneLine leftFigure = null, SceneLine rightFigure = null)
        {
            this.title = title;
            this.subtitle = subtitle;
            sceneId = scene;
            left = leftFigure;
            right = rightFigure;
        }

        internal void SetFigures(SceneLine leftFigure, SceneLine rightFigure)
        {
            left = leftFigure;
            right = rightFigure;
        }

        internal void Hide() => root.gameObject.SetActive(false);

        internal void Close()
        {
            MapUi.Clear(root);
            page = null;
            root.gameObject.SetActive(false);
        }

        /// <summary>A fresh page: background, header; figures drawn with the speaking side lit.</summary>
        internal RectTransform NewPage(string speakingSide = null, bool figures = true)
        {
            MapUi.Clear(root);
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            page = MapUi.Rect(root, "Scene page", 0, 0, W, H);
            page.anchorMin = page.anchorMax = page.pivot = new Vector2(.5f, .5f);
            page.anchoredPosition = Vector2.zero;
            var backdrop = MapUi.Rect(page, "場景背景", 0, 0, W, H).gameObject.AddComponent<RawImage>();
            var texture = RealmContent.Background(sceneId);
            backdrop.texture = texture;
            backdrop.raycastTarget = true;
            if (texture != null)
            {
                float crop = W * texture.height / (H * texture.width);
                backdrop.uvRect = crop <= 1 ? new Rect((1 - crop) / 2, 0, crop, 1) : new Rect(0, (1 - 1 / crop) / 2, 1, 1 / crop);
                backdrop.color = new Color(.80f, .78f, .74f);
            }
            else
                backdrop.color = new Color(.16f, .18f, .17f);
            var head = MapUi.Kit(page, "刊首", 0, 0, W, 86, "Wood");
            MapUi.Text(head, title ?? "", 36, 0, 1100, 86, 40, MapUi.Paper, TextAnchor.MiddleLeft, "Titles");
            MapUi.Text(head, subtitle ?? "", 1100, 0, 784, 86, 24, MapUi.Gold, TextAnchor.MiddleRight);
            if (figures)
            {
                Figure(left, false, speakingSide == null || speakingSide == "left");
                Figure(right, true, speakingSide == null || speakingSide == "right");
            }
            return page;
        }

        private void Figure(SceneLine who, bool onRight, bool lit)
        {
            if (who?.figure == null) return;
            var texture = who.figure;
            float boxW = 560, boxH = 860;
            float scale = Mathf.Min(boxW / texture.width, boxH / texture.height);
            float w = texture.width * scale, h = texture.height * scale;
            float x = onRight ? W - 40 - w : 40;
            var image = MapUi.Rect(page, onRight ? "右側立繪" : "左側立繪", x, H - 70 - h, w, h).gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            // Figures face the centre of the stage.
            bool faceRight = !onRight;
            if (CharacterFactory.FlipStandee(texture, faceRight))
                image.uvRect = new Rect(1, 0, -1, 1);
            image.color = lit ? Color.white : new Color(.48f, .46f, .44f);
        }

        /// <summary>Plays lines one by one (click the box or 繼續), then runs next.</summary>
        internal void Say(IList<SceneLine> lines, Action next, string lastLabel = "繼續")
        {
            var queue = (lines ?? Array.Empty<SceneLine>()).Where(line => line != null && !string.IsNullOrWhiteSpace(line.text)).ToList();
            void Draw(int index)
            {
                if (index >= queue.Count) { next?.Invoke(); return; }
                var line = queue[index];
                // A speaker that brings its own figure takes over that side of the stage.
                if (line.figure != null && line.side == "right") right = line;
                else if (line.figure != null && line.side == "left") left = line;
                NewPage(line.side);
                DialogueBox(line);
                var advance = MapUi.Button(page, index + 1 < queue.Count ? "繼續" : lastLabel, 1640, 990, 240, 64, () => Draw(index + 1),
                    MapUi.Red, 26);
                advance.name = "場景繼續";
            }
            Draw(0);
        }

        private void DialogueBox(SceneLine line, float y = 708, float height = 268)
        {
            var paper = MapUi.Kit(page, "對白", 300, y, 1320, height, "Lacquer");
            // v1126c PC3 #5 / #25: every speaker, 旁白 included, gets the shared compact plate and upright 32 px body.
            bool narrator = line.side == "narrator" || string.IsNullOrEmpty(line.name);
            string name = narrator ? "旁白" : line.name;
            string subtitle = narrator ? null : line.subtitle;
            float plateWidth = DialogueStyle.PlateWidth(name, subtitle);
            DialogueStyle.MapPlate(page, name, line.side == "right" && !narrator ? 1590 - plateWidth : 330,
                y - DialogueStyle.PlateLift, subtitle);
            float top = string.IsNullOrEmpty(subtitle) ? 44 : 60;
            var text = MapUi.Text(paper, DialogueStyle.Clean(line.text), 44, top, 1232, height - top - 16, DialogueStyle.BodySize,
                MapUi.Paper, TextAnchor.UpperLeft, DialogueStyle.BodyFont);
            text.lineSpacing = 1.1f;
            text.fontStyle = FontStyle.Normal;
        }

        /// <summary>
        /// The prompt stays in the dialogue box; answer cards stack in the middle, each with its tags and a 「影響」 line.
        /// notes are short status lines (score, injury) shown above the cards.
        /// </summary>
        internal void Choose(SceneLine prompt, IList<string> notes, IList<SceneChoice> choices, string leaveLabel, Action leave)
        {
            NewPage(prompt?.side);
            int count = choices?.Count ?? 0;
            float cardH = count > 3 ? 118 : 138, gap = 14;
            float top = 110;
            if (notes != null && notes.Count > 0)
            {
                var strip = MapUi.Kit(page, "狀態", 600, top, 720, 44, "Wood", false);
                MapUi.Text(strip, string.Join("　", notes), 16, 0, 688, 44, 21, MapUi.Gold, TextAnchor.MiddleCenter);
                top += 56;
            }
            for (int i = 0; i < count; i++)
            {
                var choice = choices[i];
                var button = MapUi.Button(page, "", 600, top + i * (cardH + gap), 720, cardH, () => choice.run?.Invoke(),
                    i == 0 ? MapUi.Red : null);
                button.name = "場景選項:" + choice.label;
                button.interactable = string.IsNullOrEmpty(choice.block) && choice.run != null;
                float x = 20;
                var icon = UiKit.Icon(choice.icon ?? "realm_explore");
                if (icon != null)
                {
                    var image = MapUi.Rect(button.transform, "icon", 18, 18, 56, 56).gameObject.AddComponent<RawImage>();
                    image.texture = icon;
                    image.raycastTarget = false;
                    x = 86;
                }
                MapUi.Text(button.transform, choice.label, x, 6, 700 - x, 40, 29, MapUi.Paper, TextAnchor.MiddleLeft, "Names");
                string detail = string.Join("　", new[] { choice.desc, choice.tags, choice.block }.Where(part => !string.IsNullOrEmpty(part)));
                MapUi.Text(button.transform, detail, x, 44, 700 - x, 34, 19, MapUi.Gold);
                if (!string.IsNullOrEmpty(choice.impact))
                    MapUi.Text(button.transform, "影響：" + choice.impact, x, 78, 700 - x, cardH - 84, 19,
                        new Color(.80f, .93f, .82f), TextAnchor.UpperLeft);
            }
            if (prompt != null)
                DialogueBox(prompt, 760, 216);
            if (leave != null)
                MapUi.Button(page, leaveLabel ?? "離開", 1640, 990, 240, 64, leave, null, 24).name = "場景離開";
        }

        /// <summary>Outcome / reward page: icon rows for what was gained, the log of what happened, and up to two buttons.</summary>
        internal void Result(string heading, IList<(string icon, string text)> rows, IList<string> log,
            string label, Action next, string label2 = null, Action next2 = null)
        {
            NewPage(null, figures: false);
            var sheet = MapUi.Docket(page, "結果", 360, 110, 1200, 860);
            MapUi.Text(sheet, heading, 40, 26, 1120, 60, 38, MapUi.Red, TextAnchor.MiddleCenter, "Titles");
            float y = 100;
            if (rows != null && rows.Count > 0)
            {
                MapUi.Text(sheet, "—— 所得 ——", 40, y, 1120, 34, 22, MapUi.Jade, TextAnchor.MiddleCenter);
                y += 40;
                foreach (var (iconName, text) in rows.Take(9))
                {
                    var icon = UiKit.Icon(iconName);
                    if (icon != null)
                    {
                        var image = MapUi.Rect(sheet, "icon", 120, y, 42, 42).gameObject.AddComponent<RawImage>();
                        image.texture = icon;
                        image.raycastTarget = false;
                    }
                    MapUi.Text(sheet, text, 176, y, 920, 42, 25, MapUi.Ink);
                    y += 48;
                }
                y += 8;
            }
            if (log != null && log.Count > 0)
            {
                var body = MapUi.Text(sheet, string.Join("\n", log), 80, y, 1040, 760 - y - 10, 22, new Color(.30f, .26f, .21f),
                    TextAnchor.UpperLeft, "Narration");
                body.resizeTextForBestFit = true;
                body.resizeTextMinSize = 16;
                body.resizeTextMaxSize = 22;
                body.verticalOverflow = VerticalWrapMode.Truncate;
            }
            if (label2 != null)
                MapUi.Button(page, label2, 1160, 990, 240, 64, next2, null, 24).name = "結果次選";
            MapUi.Button(page, label, 1420, 990, 240, 64, next, MapUi.Red, 26).name = "結果確定";
        }

        /// <summary>Icon name for a reward effect line.</summary>
        internal static string RewardIcon(GameEffect effect) => effect?.key switch
        {
            "silver" => "hud_silver", "food" => "hud_food", "reputation" => "hud_rep",
            "art" => (MartialArts.Find(effect.@ref)?.category) switch
            {
                "sword" => "item_manual_sword", "neigong" => "item_manual_qi", var c when !string.IsNullOrEmpty(c) => UiKit.SkillIcon(c),
                _ => "item_manual_sword"
            },
            "gear" => UiKit.SlotIcon(effect.@ref),
            "item" => effect.@ref == "item_jiuzhuan" ? "item_pill_hp" : "act_item",
            "cap" or "rank" or "progress" => "item_manual_qi",
            "relation" or "charRelation" => "hud_disciple",
            "str" => "attr_str", "agi" => "attr_agi", "con" => "attr_con", "qi" => "attr_qi", "wit" => "attr_wit",
            "cha" => "attr_cha", "luk" => "attr_luk",
            "injury" => "act_defend",
            _ => "act_item"
        };
    }

    public sealed partial class SectUI
    {
        private SceneStage sceneStage;
        /// <summary>v1124 C15–C18: the shared realm / sparring / hall scene page (created on first use).</summary>
        internal SceneStage Scene => sceneStage ??= new SceneStage(Canvas);
        internal bool SceneOpen => sceneStage != null && sceneStage.IsOpen;
    }
}
