using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>Everything the 江湖人物 dossier shows for the selected character.</summary>
    internal sealed class CharacterDossier
    {
        public StoryCharacter person;
        public string faction, stance, arts, biography, bond, talents, study;
        public string[] talentIds;
        public int relation, kind = 2;
        public int[] attrs;
        public bool estimated;
        public (string label, System.Action run)[] actions;
    }

    public sealed partial class SectUI
    {
        private void CharacterEquipment(string portrait, string gender, string weapon, string[] gear = null)
        {
            var row = CreationRow("裝備", 312);
            var frame = Rect("肖像裱框", row);
            // Exact 3:4 portrait frame (228 x 304) — eliminates squishing & dark letterboxing
            Anchor(frame, new(.5f, .5f), new(.5f, .5f), new(-114, -152), new(114, 152));
            var backing = frame.gameObject.AddComponent<Image>();
            backing.color = UiKit.AntiqueGold;
            backing.raycastTarget = false;
            var inset = Rect("肖像內框", frame);
            Fill(inset, 4);
            var art = Rect("人物肖像", inset);
            Fill(art);
            var texture = PortraitService.Resolve(portrait, gender);
            var raw = AddImage(art, texture);
            FitCover(raw, texture, 220f / 296f);

            string weaponName = RankSystem.Catalog.breakthroughs?.FirstOrDefault(
                entry => entry.id == weapon && entry.type == "weapon")?.name ?? weapon;
            string Worn(int index) => gear != null && index < gear.Length ? gear[index] : null;
            Slot("武器", Worn(0) ?? weaponName, false, 0);
            Slot("護腕", Worn(1), false, 1);
            Slot("飾品", Worn(2), false, 2);
            Slot("冠帽", Worn(3), true, 0);
            Slot("衣袍", Worn(4), true, 1);
            Slot("履靴", Worn(5), true, 2);

            void Slot(string label, string item, bool right, int index)
            {
                float top = -8 - 99 * index;
                var slot = Rect(label, row);
                if (right)
                    Anchor(slot, new(.5f, 1), Vector2.one, new(134, top - 94), new(-18, top));
                else
                    Anchor(slot, Vector2.up, new(.5f, 1), new(18, top - 94), new(-134, top));
                UiKit.Skin(slot.gameObject.AddComponent<Image>(), "Wood");
                var slotTex = UiKit.Icon(UiKit.SlotIcon(label));
                if (slotTex != null)
                {
                    var ic = Rect("SlotIcon", slot);
                    Anchor(ic, new(0, .5f), new(0, .5f), new(14, -34), new(82, 34));
                    AddImage(ic, slotTex);
                }
                int textLeft = slotTex != null ? 94 : 8;
                var heading = Label(slot, label, 24, slotTex != null ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
                heading.color = new Color(.96f, .87f, .69f);
                Anchor(heading.rectTransform, Vector2.up, Vector2.one, new(textLeft, -46), new(-12, -10));
                var value = Label(slot, string.IsNullOrEmpty(item) ? "未裝備" : item, 20, slotTex != null ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
                value.color = string.IsNullOrEmpty(item) ? new Color(.86f, .76f, .61f) : new Color(.99f, .86f, .52f);
                value.resizeTextMinSize = 18;
                Anchor(value.rectTransform, Vector2.zero, Vector2.right, new(textLeft, 10), new(-12, 49));
            }
        }

        private float rosterScroll = 1;
        private int rosterFilter = -1;

        /// <summary>
        /// 江湖人物 — Koei-style master/detail view: category tabs on top, a scrollable two-column roster on the
        /// left (44%) and an always-visible dossier of the selected character on the right (56%).
        /// </summary>
        internal void ShowDirectory(string[] filters, int selected, Action<int> filter,
            StoryCharacter[] people, int[] kinds, string[] factionNames, int focus, Action<int> choose, CharacterDossier dossier,
            string heading = "江湖人物", string[] lifeSkills = null, Action closed = null)
        {
            bool sameFilter = rosterFilter == selected && rosterHeading == heading;
            float keepScroll = sameFilter ? rosterScroll : 1;
            rosterFilter = selected;
            rosterHeading = heading;
            ShowCreation(heading, Array.Empty<string>(),
                new[] { ("關閉", (Action)(() => { rosterFilter = -1; closed?.Invoke(); CloseModal(force: true); })) }, null);
            Anchor(modalCard, new(.03f, .025f), new(.97f, .975f), Vector2.zero, Vector2.zero);
            characterDecoration = Rect("人物總覽", modalCard);
            Fill(characterDecoration);
            var tabs = Rect("人物分類", characterDecoration);
            Anchor(tabs, Vector2.up, Vector2.one, new(36, -162), new(-36, -108));
            var tabsLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabsLayout.spacing = 10;
            tabsLayout.childControlWidth = tabsLayout.childControlHeight = true;
            tabsLayout.childForceExpandWidth = tabsLayout.childForceExpandHeight = true;
            for (int index = 0; index < filters.Length; index++)
            {
                int category = index;
                var tab = Button(tabs, filters[index], () => filter(category));
                bool active = index == selected;
                UiKit.Skin(tab.GetComponent<Image>(), active ? "Selected" : "Wood");
                var caption = tab.GetComponentInChildren<Text>();
                caption.fontSize = caption.resizeTextMaxSize = 20;
                caption.color = active ? UiKit.HeaderGold : new Color(.88f, .80f, .66f);
                if (active) caption.fontStyle = FontStyle.Bold;
            }

            float inner = modalCard.rect.width - 72;
            float rosterWidth = inner * .44f - 26;
            var viewport = (RectTransform)modalList.parent;
            Anchor(viewport, Vector2.zero, new(.44f, 1), new(36, 88), new(-8, -176));
            AttachScrollbar(viewport, characterDecoration, new(.44f, 0), new(-4, 88), new(8, -176));
            creationListWidth = rosterWidth;
            if (people.Length == 0)
                AddLine(modalList, "此類人物暫無紀錄。");
            else
            {
                int columns = kinds?.Any(kind => kind is 0 or 3) == true ? 1 : 2;
                CreationGrid(columns, (people.Length + columns - 1) / columns, columns == 1 ? 216 : 150);
                for (int index = 0; index < people.Length; index++)
                    RosterCard(people[index], kinds != null && index < kinds.Length ? kinds[index] : 2, index == focus, index,
                        factionNames != null && index < factionNames.Length ? factionNames[index] : people[index].faction,
                        lifeSkills != null && index < lifeSkills.Length ? lifeSkills[index] : null);
                Canvas.ForceUpdateCanvases();
                viewport.GetComponent<ScrollRect>().verticalNormalizedPosition = Mathf.Clamp01(keepScroll);
            }
            var sheet = Panel("人物卷宗", characterDecoration, new(.44f, 0), Vector2.one, new(22, 88), new(-36, -176), false);
            UiKit.Skin(sheet.GetComponent<Image>(), "Lacquer");
            if (dossier?.person == null)
            {
                var none = Label(sheet, "請於左側名冊點選人物。", 24, TextAnchor.MiddleCenter);
                none.color = CardMuted;
                Fill(none.rectTransform, 30);
                return;
            }
            Dossier(sheet, dossier);

            void RosterCard(StoryCharacter person, int kind, bool active, int index, string factionName, string life)
            {
                var card = Button(creationGrid, "", () =>
                {
                    rosterScroll = viewport.GetComponent<ScrollRect>().verticalNormalizedPosition;
                    choose(index);
                });
                card.name = person.name ?? person.id;
                UiKit.Skin(card.GetComponent<Image>(), active ? "Selected" : "Wood");
                if (active)
                {
                    var glow = card.gameObject.AddComponent<Outline>();
                    glow.effectColor = new Color(.93f, .76f, .40f, .95f);
                    glow.effectDistance = new Vector2(3, 3);
                }
                UiKit.AccentStripe(card, KindColor(kind));
                if (kind == 3)
                {
                    // v1125 #13: the hero's own card wears a double gold frame so it stands out in 師門 / 本門.
                    var frame = card.gameObject.GetComponent<Outline>() ?? card.gameObject.AddComponent<Outline>();
                    frame.effectColor = new Color(1f, .82f, .36f, 1f);
                    frame.effectDistance = new Vector2(4, 4);
                    GoldFrame(card, 3, 2, new Color(1f, .84f, .42f));
                }
                var texture = PortraitService.Resolve(person.portrait ?? person.id, person.gender);
                MapUi.Portrait(card, texture, 18, 12, 84, 108);
                var name = Label(card, person.name ?? person.id, 22, TextAnchor.MiddleLeft);
                name.font = UiKit.Font("Names", font, name.text);
                name.fontStyle = FontStyle.Bold;
                name.color = active ? UiKit.HeaderGold : CardText;
                Anchor(name.rectTransform, Vector2.up, Vector2.one, new(114, -46), new(-82, -10));
                string role = person.title ?? person.role ?? "";
                var badge = Panel("身分", card, Vector2.up, Vector2.one, new(114, -84), new(-12, -52), false);
                UiKit.Skin(badge.GetComponent<Image>(), "Jade");
                var roleText = Label(badge, string.IsNullOrEmpty(role) ? "江湖人士" : role, 18, TextAnchor.MiddleLeft);
                roleText.color = new Color(.86f, .93f, .86f);
                Anchor(roleText.rectTransform, Vector2.zero, Vector2.one, new(10, 0), new(-6, 0));
                // The full martial and life lists wrap below the portrait.
                bool own = kind is 0 or 3 && !string.IsNullOrEmpty(life);
                var faction = Label(card, own ? life : factionName ?? "", own ? 17 : 18, own ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft);
                faction.color = own ? new Color(.78f, .88f, .74f) : CardMuted;
                faction.resizeTextMinSize = 15;
                Anchor(faction.rectTransform, Vector2.zero, Vector2.right, new(own ? 18 : 114, 8), new(-12, own ? 92 : 40));
                RankSeal(card, person.rank, person.cap, new(-76, -48), new(-10, -10), 22);
                card.GetComponentInChildren<Text>().text = "";
            }
        }

        private string rosterHeading;

        private static Color KindColor(int kind) => kind switch
        {
            3 => UiKit.HeaderGold,
            0 => UiKit.JadeAccent,
            1 => UiKit.Crimson,
            _ => UiKit.GoldAccent
        };

        /// <summary>Colored martial rank seal (e.g. "SS") anchored to the top-right of <paramref name="parent"/>.</summary>
        private void RankSeal(RectTransform parent, string rank, string cap, Vector2 min, Vector2 max, int size)
        {
            bool known = RankSystem.Order(rank) >= 0;
            var seal = Rect("武學印", parent);
            Anchor(seal, Vector2.one, Vector2.one, min, max);
            var ink = seal.gameObject.AddComponent<Image>();
            var tint = known ? RankSystem.Color(rank) : new Color(.45f, .40f, .34f);
            ink.color = Color.Lerp(tint, Color.black, .45f);
            ink.raycastTarget = false;
            var rim = seal.gameObject.AddComponent<Outline>();
            rim.effectColor = Color.Lerp(tint, Color.white, .25f);
            rim.effectDistance = new Vector2(2, 2);
            var text = Label(seal, known ? rank : "？", size, TextAnchor.MiddleCenter);
            text.color = Color.Lerp(tint, Color.white, .55f);
            text.font = UiKit.Font("Numbers", font, text.text);
            text.fontStyle = FontStyle.Bold;
            Fill(text.rectTransform, 2);
        }

        private static void AttachScrollbar(RectTransform viewport, RectTransform parent, Vector2 anchor, Vector2 min, Vector2 max)
        {
            var track = Rect("名冊捲軸", parent);
            Anchor(track, anchor, new(anchor.x, 1), min, max);
            track.gameObject.AddComponent<Image>().color = new Color(.36f, .27f, .18f, .30f);
            var slide = Rect("捲軸範圍", track);
            Fill(slide);
            var thumb = Rect("捲軸滑塊", slide);
            Fill(thumb);
            var grip = thumb.gameObject.AddComponent<Image>();
            grip.color = UiKit.Crimson;
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.targetGraphic = grip;
            bar.handleRect = thumb;
            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        /// <summary>Right-hand dossier: large framed portrait, identity, seven attributes, arts, biography and actions.</summary>
        private void Dossier(RectTransform sheet, CharacterDossier info)
        {
            var person = info.person;
            const float pad = 30, portraitW = 240, portraitH = 320;
            var frame = Rect("卷宗肖像框", sheet);
            Anchor(frame, Vector2.up, Vector2.up, new(pad - 6, -pad - portraitH - 6), new(pad + portraitW + 6, -pad + 6));
            frame.gameObject.AddComponent<Image>().color = UiKit.AntiqueGold;
            var texture = PortraitService.Resolve(person.portrait ?? person.id, person.gender);
            MapUi.Portrait(frame, texture, 3, 3, portraitW + 6, portraitH + 6);

            float x = pad + portraitW + 28;
            float y = -pad + 2;
            var name = Label(sheet, person.name ?? person.id, 32, TextAnchor.MiddleLeft);
            name.font = UiKit.Font("Names", font, name.text);
            name.fontStyle = FontStyle.Bold;
            name.color = UiKit.HeaderGold;
            Anchor(name.rectTransform, Vector2.up, Vector2.one, new(x, y - 44), new(-150, y));
            RankSeal(sheet, person.rank, person.cap, new(-136, -pad - 50), new(-pad, -pad - 2), 26);
            var capLabel = Label(sheet, "武學印　潛力 " + (RankSystem.Order(person.cap) < 0 ? "？" : person.cap), 18, TextAnchor.MiddleRight);
            capLabel.color = CardMuted;
            Anchor(capLabel.rectTransform, Vector2.one, Vector2.one, new(-300, -pad - 84), new(-pad, -pad - 56));
            y -= 50;
            string courtesy = string.Join("　", new[]
            {
                string.IsNullOrEmpty(person.courtesy) ? null : "字 " + person.courtesy, person.title ?? person.role
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
            Line(string.IsNullOrEmpty(courtesy) ? "江湖人士" : courtesy, 22, CardGold, 32);
            Line(string.Join("　", new[]
            {
                person.gender, person.age > 0 ? $"{person.age} 歲" : "年歲不詳", info.relation == 0 ? null : $"好感 {info.relation:+0;-0}"
            }.Where(value => !string.IsNullOrWhiteSpace(value))), 18, CardText, 28);
            var badges = Rect("勢力立場", sheet);
            Anchor(badges, Vector2.up, Vector2.one, new(x, y - 36), new(-pad, y));
            var badgeRow = badges.gameObject.AddComponent<HorizontalLayoutGroup>();
            badgeRow.spacing = 10;
            badgeRow.childControlWidth = badgeRow.childControlHeight = true;
            badgeRow.childForceExpandWidth = false;
            badgeRow.childForceExpandHeight = true;
            Pill(badges, string.IsNullOrEmpty(info.faction) ? "無門無派" : info.faction, UiKit.GoldAccent);
            if (!string.IsNullOrEmpty(info.stance)) Pill(badges, info.stance, KindColor(info.kind));
            y -= 50;

            // Seven-attribute bar grid in two columns beside the portrait.
            var attrs = AttributeSystem.Normalize(info.attrs);
            for (int index = 0; index < AttributeSystem.Count; index++)
            {
                int column = index % 2, attrRow = index / 2;
                var cell = Rect(AttributeSystem.Names[index], sheet);
                float top = y - attrRow * 34;
                Anchor(cell, Vector2.up, Vector2.one, new(x, top - 30), new(-pad, top));
                var half = Rect("欄", cell);
                Anchor(half, new(column * .5f, 0), new((column + 1) * .5f, 1), new(column == 0 ? 0 : 10, 0), new(column == 0 ? -10 : 0, 0));
                var label = Label(half, AttributeSystem.Names[index], 18, TextAnchor.MiddleLeft);
                label.color = CardMuted;
                Anchor(label.rectTransform, Vector2.zero, new(0, 1), Vector2.zero, new(48, 0));
                var value = Label(half, attrs[index].ToString(), 20, TextAnchor.MiddleRight);
                value.color = attrs[index] >= 15 ? CardGold : attrs[index] < AttributeSystem.Base ? CardLow : CardText;
                value.font = UiKit.Font("Numbers", font, value.text);
                Anchor(value.rectTransform, new(1, 0), Vector2.one, new(-34, 0), Vector2.zero);
                CardBar(half, 54, -11, -40, 9, attrs[index] / (float)AttributeSystem.Max,
                    Color.Lerp(AttributeSystem.Tints[index], Color.white, .2f));
                // v1126b 決定 1: hovering lists every per-point effect (a clear image catches the pointer).
                half.gameObject.AddComponent<Image>().color = Color.clear;
                int slot = index, shown = attrs[index];
                AttributeHover(half, slot, () => shown);
            }
            if (info.estimated)
            {
                var note = Label(sheet, "屬性係據江湖傳聞推估", 18, TextAnchor.MiddleRight);
                note.color = new Color(.62f, .55f, .44f);
                float noteTop = y - 4 * 34 + 4;
                Anchor(note.rectTransform, Vector2.up, Vector2.one, new(x, noteTop - 28), new(-pad, noteTop));
            }

            // Lower half (v1126 #18): one scrolling column — arts and life skills (all of them), talents, biography
            // and the tie to the sect — so a long skill list can never sit on top of the 生平列傳 heading.
            float lower = -pad - portraitH - 20;
            Rule(lower - 2);
            var bio = ScrollList(sheet, new Vector2(pad, 84), new Vector2(-pad - 14, lower - 8));
            if (!string.IsNullOrEmpty(info.study)) BioLine(info.study, 21, CardGold);
            BioLine("拿手武學與技藝", 22, CardGold);
            BioLine(string.IsNullOrEmpty(info.arts) ? "尚無記載" : info.arts, 18, CardText);
            BioLine("天賦", 22, CardGold);
            if (info.talentIds != null && info.talentIds.Length > 0)
                AddInteractiveTalentChips(bio, info.talentIds);
            BioLine(string.IsNullOrEmpty(info.talents) ? "尚無天賦記載。" : info.talents, 18, CardText);
            BioLine("生平列傳", 22, CardGold);
            BioLine(string.IsNullOrWhiteSpace(info.biography) ? "生平未詳。" : info.biography, 18, CardText);
            BioLine("與本門淵源", 22, CardGold);
            BioLine(string.IsNullOrWhiteSpace(info.bond) ? "與本門尚無往來。" : info.bond, 18, CardText);

            var actions = Rect("卷宗行動", sheet);
            Anchor(actions, Vector2.zero, Vector2.right, new(pad, 18), new(-pad, 70));
            var row = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 14;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            foreach (var (label, run) in info.actions ?? Array.Empty<(string, Action)>())
            {
                var button = Button(actions, label, run);
                button.GetComponent<Button>().interactable = run != null;
                var caption = button.GetComponentInChildren<Text>();
                caption.fontSize = caption.resizeTextMaxSize = 20;
                caption.color = UiKit.HeaderGold;
            }

            void Line(string text, int size, Color color, float height)
            {
                var line = Label(sheet, text, size, TextAnchor.MiddleLeft);
                line.color = color;
                Anchor(line.rectTransform, Vector2.up, Vector2.one, new(x, y - height), new(-pad, y));
                y -= height + 2;
            }
            void Rule(float top)
            {
                var rule = Rect("Rule", sheet);
                Anchor(rule, Vector2.up, Vector2.one, new(pad, top - 1), new(-pad, top));
                var ink = rule.gameObject.AddComponent<Image>();
                ink.color = new Color(.78f, .63f, .35f, .55f);
                ink.raycastTarget = false;
            }
            void BioLine(string text, int size, Color color)
            {
                var line = Label(bio, text, size);
                line.color = color;
                line.gameObject.AddComponent<LayoutElement>().minHeight = size + 8;
                line.horizontalOverflow = HorizontalWrapMode.Wrap;
                line.verticalOverflow = VerticalWrapMode.Overflow;
                line.resizeTextForBestFit = false;
                line.lineSpacing = 1.15f;
            }
        }

        private void Pill(RectTransform parent, string text, Color accent)
        {
            var pill = Rect(text, parent);
            var image = pill.gameObject.AddComponent<Image>();
            image.color = Color.Lerp(accent, Color.black, .55f);
            image.raycastTarget = false;
            var rim = pill.gameObject.AddComponent<Outline>();
            rim.effectColor = accent;
            rim.effectDistance = new Vector2(1.5f, 1.5f);
            var label = Label(pill, text, 18, TextAnchor.MiddleCenter);
            label.color = Color.Lerp(accent, Color.white, .6f);
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            Fill(label.rectTransform);
            pill.gameObject.AddComponent<LayoutElement>().preferredWidth = text.Length * 19 + 28;
        }

        internal void ShowCharacterProfile(StoryCharacter person, string faction, int relation,
            Questline[] quests, Func<string, QuestProgress> progress, Action back)
        {
            ShowCreation("人物小傳", Array.Empty<string>(), new[] { ("返回", back),
                ("關閉", (Action)(() => CloseModal(force: true))) }, null);
            characterDecoration = Rect("人物小傳裝飾", modalCard);
            Fill(characterDecoration);
            var portraitCard = Panel("人物肖像", characterDecoration, Vector2.zero, Vector2.up,
                new(36, 89), new(410, -112));
            var art = Rect("人物肖像", portraitCard);
            Anchor(art, Vector2.up, Vector2.one, new(21, -463), new(-21, -21));
            AddImage(art, PortraitService.Resolve(person.portrait ?? person.id, person.gender));
            var rank = Label(portraitCard, "武學　" + RankSystem.FormatKnown(person.rank, person.cap),
                28, TextAnchor.MiddleCenter);
            rank.color = RankTextColor(person.rank, dark: false);
            Anchor(rank.rectTransform, Vector2.up, Vector2.one, new(12, -530), new(-12, -475));

            var viewport = (RectTransform)modalList.parent;
            Anchor(viewport, Vector2.zero, Vector2.one, new(440, 88), new(-62, -112));
            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.StopMovement();
            var track = Rect("人物小傳捲軸", characterDecoration);
            Anchor(track, Vector2.right, Vector2.one, new(-55, 88), new(-41, -112));
            track.gameObject.AddComponent<Image>().color = new Color(.36f, .27f, .18f, .24f);
            var slide = Rect("捲軸範圍", track);
            Fill(slide);
            var thumb = Rect("捲軸滑塊", slide);
            Fill(thumb);
            var grip = thumb.gameObject.AddComponent<Image>();
            grip.color = new Color(.56f, .26f, .19f);
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.targetGraphic = grip;
            bar.handleRect = thumb;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            AddLine(modalList, string.IsNullOrEmpty(person.courtesy) ? person.name :
                $"{person.name}　字 {person.courtesy}", 36);
            Info(string.Join("　", new[] { person.title ?? person.role, faction, person.gender,
                person.age > 0 ? $"{person.age} 歲" : "" }.Where(value => !string.IsNullOrWhiteSpace(value))));
            Field("性情", person.personality);
            Field("身世", person.background ?? person.desc);
            Field("與主角的關係", person.relation);
            Field("好感", $"{relation:+0;-0;0}");
            if (person.signatureSkills?.Length > 0)
                Field("拿手絕藝", string.Join("、", person.signatureSkills));
            foreach (var quest in quests)
            {
                AddLine(modalList, quest.title ?? "人物逸事", 28);
                Info(quest.summary);
                var status = progress(quest.id);
                foreach (var beat in quest.beats)
                {
                    string state = status?.completed?.Contains(beat.id) == true ? "已完成" :
                        !beat.HasScene ? "後續待續" : status?.next == beat.id || status == null && beat == quest.beats[0]
                            ? "待觸發" : "未解鎖";
                    AddLine(modalList, $"{state}　{beat.title ?? beat.id}");
                    Info(beat.HasScene ? beat.summary : beat.outline ?? beat.summary);
                }
            }

            if (person.lines?.Any(line => line != null) == true)
                SkillColumns(person.lines, knownOnly: true);

            void Info(string text)
            {
                if (!string.IsNullOrWhiteSpace(text)) AddLine(modalList, text);
            }
            void Field(string heading, string text)
            {
                if (!string.IsNullOrWhiteSpace(text)) Info($"{heading}　{text}");
            }
        }

        internal bool VerifyCharacterProfileScroll()
        {
            var scroll = modalList.parent.GetComponent<ScrollRect>();
            Canvas.ForceUpdateCanvases();
            if (scroll.verticalScrollbar == null || !scroll.verticalScrollbar.gameObject.activeInHierarchy ||
                scroll.verticalNormalizedPosition < .99f)
                return false;
            scroll.OnScroll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            {
                scrollDelta = Vector2.down * 100
            });
            bool reachedBottom = scroll.verticalNormalizedPosition <= .01f;
            scroll.verticalNormalizedPosition = 1;
            if (reachedBottom) Debug.Log("[Capture] Character profile wheel reached the bottom.");
            return reachedBottom;
        }

        private static Color RankTextColor(string rank, bool dark) =>
            Color.Lerp(RankSystem.Color(rank), dark ? new Color(1, .9f, .72f) : Ink, .35f);
    }
}
