using System;
using System.Linq;

namespace Wuxia
{
    public sealed partial class GameState
    {
        /// <summary>
        /// v1126e #6: the 行動力 breakdown names each talent that raises the cap (「王佐之才 +1」); whatever else sits in
        /// Story.apBonus (story choices, quest rewards, 奇門經商總策) stays under 「際遇」.
        /// </summary>
        private string ApBonusDescription()
        {
            var catalog = M2Data.Load<TalentCatalog>("talents").talents ?? Array.Empty<CreationOption>();
            var talentAp = catalog.Where(option => option != null && (Protagonist.talents ?? Array.Empty<string>()).Contains(option.id))
                .Select(option => (option.name, amount: (option.effects ?? Array.Empty<GameEffect>())
                    .Where(effect => effect?.key == "apMax").Sum(effect => UnityEngine.Mathf.RoundToInt(effect.value))))
                .Where(entry => entry.amount != 0).ToArray();
            int rest = Story.apBonus - talentAp.Sum(entry => entry.amount);
            return string.Concat(talentAp.Select(entry => $"\n天賦・{entry.name} {entry.amount:+0;-0}")) +
                (rest != 0 ? $"\n際遇 {rest:+0;-0}" : "");
        }

        /// <summary>
        /// v1126e #5: whether any monthly action can still be afforded (AP and its silver / food). The 月底結算 guide step
        /// and the 「結束本月」 confirmation wait on this.
        /// </summary>
        internal bool HasAffordableAction => ActionPoints > 0 && (Data.Actions ?? Array.Empty<ActionData>())
            .Any(action => action != null && action.apCost > 0 && action.apCost <= ActionPoints &&
                (action.cost == null || CanPay(action.cost)));

#if UNITY_EDITOR
        /// <summary>v1126e #6 audit: the key a 「studyPct」 effect is stored under.</summary>
        internal string StudyKeyForAudit(string id) => StudyKey(id);
#endif
    }
}
