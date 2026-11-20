using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    public sealed partial class SectUI
    {
        internal void ShowDirectory(string[] filters, int selected, Action<int> filter,
            StoryCharacter[] people, Action<StoryCharacter> open)
        {
            ShowCreation("江湖人物", Array.Empty<string>(),
                new[] { ("關閉", (Action)(() => CloseModal(force: true))) }, null);
            var tabs = characterDecoration = Rect("人物分類", modalCard);
            Anchor(tabs, Vector2.up, Vector2.one, new(36, -159), new(-36, -111));
            var tabsLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabsLayout.spacing = 10;
            tabsLayout.childControlWidth = tabsLayout.childControlHeight = true;
            for (int index = 0; index < filters.Length; index++)
            {
                int category = index;
                var tab = Button(tabs, filters[index], () => filter(category));
                UiKit.Skin(tab.GetComponent<Image>(), index == selected ? "Selected" : "Wood");
            }
            // The tabs sit above the scroll viewport and remain available while browsing.
            Anchor((RectTransform)modalList.parent, Vector2.zero, Vector2.one, new(36, 88), new(-36, -172));
            if (people.Length == 0)
            {
                AddLine(modalList, "此類人物暫無紀錄。");
                return;
            }
            CreationGrid(3, (people.Length + 2) / 3, 125);
            foreach (var person in people)
            {
                var card = Button(creationGrid, $"{person.name}\n{person.title ?? person.role}", () => open(person));
                UiKit.Skin(card.GetComponent<Image>(), "Wood");
                var art = Rect("人物肖像", card);
                Anchor(art, Vector2.zero, Vector2.up, new(10, 10), new(89, -10));
                AddImage(art, PortraitService.Resolve(person.portrait ?? person.id, person.gender));
                var caption = card.GetComponentInChildren<Text>();
                caption.alignment = TextAnchor.MiddleLeft;
                Anchor(caption.rectTransform, Vector2.zero, Vector2.one, new(105, 36), new(-14, -4));
                var rank = Label(card, RankSystem.FormatKnown(person.rank, person.cap), 22);
                rank.color = RankTextColor(person.rank, dark: true);
                Anchor(rank.rectTransform, Vector2.zero, Vector2.right, new(105, 6), new(-14, 36));
            }
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
