using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    public sealed partial class SectUI
    {
        private RectTransform recruitGrid;

        internal void RecruitCard(Candidate candidate, CreationOption[] catalog, int silver, string reason, Action accept)
        {
            if (recruitGrid == null || !recruitGrid.gameObject.activeSelf)
            {
                recruitGrid = CreationRow("候選弟子", 0);
                var grid = recruitGrid.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2((creationListWidth - 32) / 3, 574);
                grid.spacing = new Vector2(16, 16);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 3;
            }
            var card = Panel(candidate.name, recruitGrid, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            recruitGrid.GetComponent<LayoutElement>().preferredHeight = 574;
            var portrait = Rect("Portrait", card);
            Anchor(portrait, Vector2.up, Vector2.up, new(18, -210), new(162, -18));
            var pTex = PortraitService.Resolve(candidate.portrait, candidate.gender);
            var pRaw = AddImage(portrait, pTex);
            FitCover(pRaw, pTex, 144f / 192f);
            string best = RankSystem.Martial(candidate.lines);
            CardText($"{candidate.name}　{candidate.gender} {candidate.age}歲", 26, 186, 18, 18, 40);
            CardText(candidate.origin ?? "", 18, 186, 18, 56, 26); // ends above the 武學 badge (top 83)
            var badge = Panel("武學", card, Vector2.up, Vector2.up, new(186, -118), new(326, -83));
            UiKit.Skin(badge.GetComponent<Image>(), "Wood");
            var rank = Label(badge, $"武學 {RankSystem.Format(best, RankSystem.Martial(candidate.lines, true))}",
                19, TextAnchor.MiddleCenter);
            rank.color = Color.Lerp(RankSystem.Color(best), new Color(1, .92f, .75f), .46f);
            Fill(rank.rectTransform, 2);
            Aptitude("根骨", candidate.aptitude?.bone ?? 0, 129);
            Aptitude("悟性", candidate.aptitude?.insight ?? 0, 167);
            var attributes = AttributeSystem.Normalize(candidate.attrs);
            for (int index = 0; index < AttributeSystem.Count; index++)
            {
                var cell = Label(card, $"{AttributeSystem.Glyphs[index]} {attributes[index]}", 19, TextAnchor.MiddleCenter);
                cell.color = attributes[index] >= 14 ? new Color(.55f, .30f, .10f) : Ink;
                Anchor(cell.rectTransform, new(index / 7f, 1), new((index + 1) / 7f, 1),
                    new(4, -250), new(-4, -220));
            }
            CardText($"戰鬥　{TopLines(true)}", 19, 20, 18, 255, 28);
            string craft = TopLines(false);
            CardText($"生產　{(string.IsNullOrWhiteSpace(craft) ? "尚無專長" : craft)}", 19, 20, 18, 286, 28);
            CardText($"{candidate.personality}　忠誠 {candidate.loyalty}", 19, 20, 18, 322, 29);
            var chips = Rect("天賦", card);
            Anchor(chips, Vector2.up, Vector2.one, new(190, -351), new(-18, -322));
            var chipLayout = chips.gameObject.AddComponent<HorizontalLayoutGroup>();
            chipLayout.spacing = 6;
            chipLayout.childControlWidth = chipLayout.childControlHeight = true;
            foreach (var id in candidate.talents ?? Array.Empty<string>())
            {
                var talent = catalog.FirstOrDefault(option => option.id == id);
                var chip = Panel("天賦", chips, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                UiKit.Skin(chip.GetComponent<Image>(), "Wood");
                var text = Label(chip, talent?.name ?? id, 18, TextAnchor.MiddleCenter);
                text.color = talent?.rarity switch
                {
                    "legendary" => new Color(1, .71f, .37f),
                    "rare" => new Color(.64f, .88f, .79f),
                    _ => new Color(.97f, .91f, .78f)
                };
                Fill(text.rectTransform, 2);
                string capturedId = id;
                AttachTalentTip(chip, () => TalentTipText(talent, capturedId));
            }
            var biography = ScrollList(card, new Vector2(20, 59), new Vector2(-20, -361));
            AddLine(biography, candidate.blurb ?? "", 20);
            var acceptButton = Button(card, accept == null ? reason : $"收為弟子　銀 {silver}", accept);
            Anchor(acceptButton, Vector2.zero, Vector2.right, new(18, 12), new(-18, 53));
            acceptButton.GetComponent<Button>().interactable = accept != null;

            void CardText(string content, int size, int left, int right, int top, int height)
            {
                var text = Label(card, content, size, TextAnchor.MiddleLeft);
                text.resizeTextForBestFit = false;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                Anchor(text.rectTransform, Vector2.up, Vector2.one, new(left, -top - height), new(-right, -top));
            }

            void Aptitude(string caption, int value, int top)
            {
                CardText($"{caption} {value}", 19, 186, 18, top - 2, 29);
                var track = Rect("Aptitude track", card);
                Anchor(track, Vector2.up, Vector2.one, new(186, -top - 31), new(-24, -top - 26));
                track.gameObject.AddComponent<Image>().color = new Color(.32f, .27f, .22f, .27f);
                var fill = Rect("Aptitude value", track);
                Anchor(fill, Vector2.zero, new(Mathf.Clamp01(value / 10f), 1), Vector2.zero, Vector2.zero);
                fill.gameObject.AddComponent<Image>().color = new Color(.26f, .44f, .36f);
            }

            string TopLines(bool martial) => string.Join("・", candidate.lines.Where(line => RankSystem.IsCombat(line.id) == martial)
                .OrderByDescending(line => RankSystem.Index(line.rank)).ThenByDescending(line => RankSystem.Index(line.cap)).Take(2)
                .Select(line => $"{RankSystem.Categories.First(category => category.id == line.id).name} {RankSystem.Format(line.rank, line.cap)}"));
        }
    }
}
