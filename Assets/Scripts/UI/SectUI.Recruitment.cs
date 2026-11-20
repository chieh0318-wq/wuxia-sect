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
                recruitGrid = CreationRow("候選弟子", 580);
                var grid = recruitGrid.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(328, 284);
                grid.spacing = new Vector2(12, 12);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 3;
            }
            var card = Panel(candidate.name, recruitGrid, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var portrait = Rect("Portrait", card);
            Anchor(portrait, Vector2.up, Vector2.up, new(10, -120), new(86, -22));
            AddImage(portrait, PortraitService.Resolve(candidate.portrait, candidate.gender));
            string best = RankSystem.Martial(candidate.lines);
            var heading = Label(card, $"{candidate.name}　{candidate.gender} {candidate.age}\n{candidate.origin}\n武學 {RankSystem.Format(best, RankSystem.Martial(candidate.lines, true))}", 21);
            Anchor(heading.rectTransform, Vector2.up, Vector2.one, new(98, -110), new(-12, -18));

            string combat = TopLines(true);
            string production = TopLines(false);
            string talentNames = string.Join("、", (candidate.talents ?? Array.Empty<string>()).Select(id =>
                catalog.FirstOrDefault(talent => talent.id == id)?.name ?? id));
            var body = Label(card, $"{Attributes(candidate.attrs)}\n根骨 {candidate.aptitude?.bone}／悟性 {candidate.aptitude?.insight}　忠誠 {candidate.loyalty}　{candidate.personality}\n戰鬥　{combat}\n生產　{production}\n{(talentNames.Length > 0 ? talentNames : candidate.blurb)}", 15);
            Anchor(body.rectTransform, Vector2.zero, Vector2.one, new(12, 56), new(-12, -120));
            var acceptButton = Button(card, accept == null ? reason : $"收為弟子　銀 {silver}", accept);
            Anchor(acceptButton, Vector2.zero, Vector2.right, new(10, 8), new(-10, 49));
            acceptButton.GetComponent<Button>().interactable = accept != null;

            string TopLines(bool martial) => string.Join("・", candidate.lines.Where(line => RankSystem.IsCombat(line.id) == martial)
                .OrderByDescending(line => RankSystem.Index(line.rank)).ThenByDescending(line => RankSystem.Index(line.cap)).Take(2)
                .Select(line => $"{RankSystem.Categories.First(category => category.id == line.id).name} {RankSystem.Format(line.rank, line.cap)}"));
        }
    }
}
