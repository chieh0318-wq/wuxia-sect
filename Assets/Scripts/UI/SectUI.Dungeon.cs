using System;
using System.Collections.Generic;
using System.Linq;

namespace Wuxia
{
    public sealed partial class SectUI
    {
        /// <summary>A secret-realm floor: the scene text, then one card per tactical choice (attribute and odds as tags).</summary>
        internal void ShowRealmStage(string title, IEnumerable<string> lines,
            IEnumerable<(string label, string desc, string tags, Action run)> choices, Action leave)
        {
            ShowCreation(title, lines, new[] { ("撤出秘境", leave) }, null);
            var options = choices.ToList();
            CreationGrid(Math.Max(1, options.Count), 1, 196);
            foreach (var option in options)
                CreationChoice(option.label, option.desc, null, option.run, "rare", tags: option.tags);
        }

        /// <summary>Outcome / reward page of a secret realm with a single continue button.</summary>
        internal void ShowRealmResult(string title, IEnumerable<string> lines, string nextLabel, Action next) =>
            ShowCreation(title, lines, new[] { (nextLabel, next) }, null);
    }
}
