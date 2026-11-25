using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    public sealed partial class SectUI
    {
        private GameState resourceState;
        private static readonly string[] MonthlyResourceLabels = { "銀兩", "糧食", "聲望", "門人", "行動力" };

        private void BindResourceBreakdown(RectTransform cell, int index)
        {
            var button = cell.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => ShowResourceBreakdown(index));
        }

        internal void ShowResourceBreakdown(int index)
        {
            if (resourceState == null) return;
            var rows = resourceState.MonthlyResourceBreakdown(index);
            var lines = new[] { $"預估淨變動　{rows.Sum(row => row.value):+0;-0;0}" }
                .Concat(rows.Select(row => $"{row.source}　{row.value:+0;-0;0}"))
                .Concat(index == 4 ? new[] { resourceState.ApDescription } : System.Array.Empty<string>());
            ShowModal($"{MonthlyResourceLabels[index]}・月底收支", lines,
                new (string, System.Action)[] { ("關閉", () => CloseModal(force: true)) });
            SizeModal(820, 665);
        }
    }
}
