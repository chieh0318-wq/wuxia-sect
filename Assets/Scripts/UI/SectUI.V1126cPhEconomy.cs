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
                .Concat(index == 4 ? new[] { resourceState.ApDescription } : System.Array.Empty<string>());
            ShowCreation($"{MonthlyResourceLabels[index]}・月底收支", lines.ToArray(),
                new (string, System.Action)[] { ("關閉", () => CloseModal(force: true)) }, null);
            foreach (var row in rows)
                CreationChoice(row.source, $"{row.value:+0;-0;0}", null, null);
        }
    }
}
