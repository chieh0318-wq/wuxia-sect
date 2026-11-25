using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    public sealed partial class SectUI
    {
        internal sealed class PickerSort
        {
            internal int column = -1;
            internal bool ascending;
            internal void Select(int next)
            {
                ascending = next == column && !ascending;
                column = next;
            }
        }

        private sealed class PickerRow
        {
            internal string id;
            internal string[] cells;
            internal IComparable[] keys;
            internal bool selected;
            internal Action toggle;
        }

        private static readonly string[] PickerColumns = {
            "姓名", "身分", "餘次", "相關技能", "相關屬性", "成功率", "品質", "相關專長"
        };
        private static readonly float[] PickerShares = { .17f, .09f, .07f, .155f, .105f, .09f, .215f, .105f };

        private float PickerWidth()
        {
            Canvas.ForceUpdateCanvases();
            return ((RectTransform)modalList.parent).rect.width - 12;
        }

        private static string PickerBuildQuality(int quality)
        {
            var (upkeep, output, months) = GameState.QualityTerms(quality);
            return $"{quality}%・{months} 月整修\n維護 {upkeep:+0;-0;0}%・產出 {output:+0;-0;0}%";
        }

        private static PickerRow PickerMember(GameState state, string id, string action, string prefix,
            string blocked, float? chance, Action toggle)
        {
            var (skill, attribute) = AssignmentSystem.Relevance(action);
            var prediction = AssignmentSystem.Predict(state, action, id);
            var rank = AssignmentSystem.RankOf(state, id);
            int remaining = AssignmentSystem.Remaining(state, id);
            bool spar = action == "sparring";
            bool campaign = action == "campaign";
            var line = spar ? state.CurrentStudyLine(id) : state.MemberLines(id).FirstOrDefault(entry => entry?.id == skill);
            int grade = RankSystem.Order(line?.rank);
            int attr = attribute == "cha" ? AssignmentSystem.Eloquence(state, id) : AssignmentSystem.Attribute(state, id, attribute);
            string attrName = attribute == "cha" ? "口才" : AttributeSystem.Names[AttributeSystem.Index(attribute)];
            string talentKey = action == "diplomacy" ? "diplomacy" : skill == "forging" ? "forge" : skill;
            bool specialist = (talentKey != null && state.TalentValue(id, talentKey, 20) > 0) || grade >= RankSystem.Index("B");
            int buildQuality = GameState.ConstructionQuality(prediction.Quality);
            float success = chance ?? prediction.Success;
            string quality = action == "build_plot" ? PickerBuildQuality(buildQuality)
                : spar ? state.StudyLabel(id, compact: true)
                : campaign ? $"統領 {SectCampaign.CommandCapacity(state, id)} 兵"
                : $"{prediction.Quality:P0}";
            string skillText = line == null ? "—" : $"{EffectText.LineName(line.id)} {line.rank}";
            if (!spar && line != null && !RankSystem.IsCombat(line.id))
                skillText += $"\n{state.WorkPractice(id, line.id)?.xp ?? 0}／{GameState.LifeProgressMax}";
            bool variableOdds = chance.HasValue || action is "explore" or "gather_rumors";
            return new PickerRow
            {
                id = id,
                cells = new[] { (prefix.Length > 0 ? prefix + "\n" : "") + state.SparName(id) +
                    (blocked == null ? "" : "\n" + blocked), rank.Name, $"{remaining}／{rank.Monthly}",
                    skillText, spar ? $"戰力 {state.SparPower(id):0}" : $"{attrName} {attr}",
                    blocked != null || (spar || campaign || action == "secret_realm") && !variableOdds ? "—" : $"{success:P0}",
                    quality, specialist ? "○" : "—" },
                keys = new IComparable[] { state.SparName(id), AssignmentSystem.Grade(state, id), remaining,
                    grade, spar ? state.SparPower(id) : attr, success, spar ? line?.progress ?? -1 :
                        campaign ? SectCampaign.CommandCapacity(state, id) : action == "build_plot" ? buildQuality : prediction.Quality, specialist },
                selected = prefix.Length > 0,
                toggle = blocked == null ? toggle : null
            };
        }

        private static IEnumerable<PickerRow> SortRows(IEnumerable<PickerRow> rows, PickerSort sort)
        {
            if (sort.column < 0) return rows;
            return sort.ascending ? rows.OrderBy(row => row.keys[sort.column]) :
                rows.OrderByDescending(row => row.keys[sort.column]);
        }

        private static void PickerHeader(RectTransform parent, float width, PickerSort sort, Action redraw, bool spar, string[] headings = null)
        {
            float x = 0;
            for (int column = 0; column < PickerColumns.Length; column++)
            {
                int index = column;
                float cellWidth = width * PickerShares[column];
                string label = headings?[column] ?? (spar && column == 6 ? "修習中" : PickerColumns[column]);
                if (sort.column == column) label += sort.ascending ? " ↑" : " ↓";
                MapUi.Button(parent, label, x + 2, 0, cellWidth - 4, 38, () => { sort.Select(index); redraw(); },
                    sort.column == column ? MapUi.Red : null, 21);
                x += cellWidth;
            }
        }

        private static Button PickerBody(RectTransform parent, float width, float height, PickerRow row)
        {
            var button = MapUi.Button(parent, "", 2, 1, width - 4, height - 2,
                row.toggle ?? (() => { }), row.selected ? MapUi.Red : null);
            button.interactable = row.toggle != null;
            float x = 0;
            for (int column = 0; column < row.cells.Length; column++)
            {
                float cellWidth = width * PickerShares[column];
                MapUi.Text(button.transform, row.cells[column], x + 2, 2, cellWidth - 7, height - 4,
                    19, column == 7 || row.selected ? MapUi.Gold : MapUi.Paper, TextAnchor.MiddleCenter);
                x += cellWidth;
            }
            return button;
        }

        internal void RecruitPicker(IEnumerable<Candidate> candidates, PickerSort sort, Func<Candidate, int> price,
            Func<Candidate, string> restriction, Action redraw, Action<Candidate> review)
        {
            float width = PickerWidth();
            PickerHeader(CreationRow("候選門人欄目", 42), width, sort, redraw, false,
                new[] { "姓名", "來歷", "年齡", "武學／上限", "根骨／悟性", "束脩", "入門忠誠", "天賦" });
            var rows = candidates.Select(candidate =>
            {
                string martial = RankSystem.Martial(candidate.lines), cap = RankSystem.Martial(candidate.lines, true);
                string status = restriction(candidate);
                int fee = price(candidate);
                int talentCount = candidate.talents?.Length ?? 0;
                int bone = candidate.aptitude?.bone ?? 0, wit = candidate.aptitude?.insight ?? 0;
                return new PickerRow
                {
                    cells = new[] { candidate.name + (status.Length == 0 ? "" : "\n" + status),
                        candidate.origin ?? "—", $"{candidate.age} 歲", $"{martial}／{cap}",
                        $"{bone}／{wit}", $"銀 {fee}", $"{candidate.loyalty}／100", talentCount.ToString() },
                    keys = new IComparable[] { candidate.name, candidate.origin ?? "", candidate.age,
                        RankSystem.Index(martial), bone + wit, fee, candidate.loyalty, talentCount },
                    toggle = () => review(candidate)
                };
            });
            foreach (var row in SortRows(rows, sort))
                PickerBody(CreationRow("候選門人", 62), width, 62, row);
        }

        private void AssignmentOutcomeTable(GameState state, string action, string member,
            (string Label, float Chance, string Success, string Failure)[] outcomes)
        {
            outcomes ??= state.AssignmentOutcomes(action, member);
            if (outcomes.Length == 0) return;
            float w = PickerWidth();
            string note = action switch
            {
                "explore" => "兩項分開判定，落空仍會巡山遇事。行動力、出勤與已有傳聞照常耗用。",
                "gather_rumors" => "無論有無探得秘境，均記下江湖傳聞。",
                "diplomacy" => "以下為交涉前估算，席間所選說辭可改變成敗與條款。",
                _ => "以下為各品級的成品機率，每次必得 1 件。"
            };
            var box = CreationRow($"判定結果・{state.SparName(member)}", 82 + outcomes.Length * 40);
            MapUi.Text(box, $"判定結果・{state.SparName(member)}　{note}", 10, 0, w - 20, 40, 20, Ink);
            string[] labels = { "判定", "成／敗", "成功所得", "失手後果" };
            for (int col = 0; col < labels.Length; col++)
                MapUi.Text(box, labels[col], col * w / 4 + 10, 40, w / 4 - 20, 34, 19, Ink);
            for (int i = 0; i < outcomes.Length; i++)
            {
                var outcome = outcomes[i];
                string[] items = { outcome.Label, $"{outcome.Chance:P0}／{1 - outcome.Chance:P0}", outcome.Success, outcome.Failure };
                var strip = MapUi.Kit(box, "判定", 0, 76 + i * 40, w, 38, "Wood", false);
                for (int col = 0; col < items.Length; col++)
                    MapUi.Text(strip, items[col], col * w / 4 + 10, 0, w / 4 - 20, 38, 19, MapUi.Paper);
            }
        }
    }
}
