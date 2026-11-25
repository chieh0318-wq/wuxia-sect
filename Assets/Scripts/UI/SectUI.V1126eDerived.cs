using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>
    /// v1126e #1: the derived-figure panel with 外功／內功 on one shared base and 外防／內防 split. Each cell explains its
    /// formula with the live coefficients from <see cref="AttributeSystem"/>; the other figures (先手 氣血 內力 閃避 奇遇)
    /// keep the v1126c PC3 #3 text from <see cref="DerivedStatTips"/>.
    /// </summary>
    internal static class DerivedStatsV1126e
    {
        internal static readonly string[] Keys = { "外功", "內功", "外防", "內防", "先手", "氣血", "內力", "閃避", "奇遇" };

        internal static (string key, string value)[] Cells(DerivedStats stats) => new[]
        {
            ("外功", stats.Attack.ToString()), ("內功", stats.QiPower.ToString()),
            ("外防", stats.OuterDefense.ToString()), ("內防", stats.InnerDefense.ToString()),
            ("先手", stats.Speed.ToString()), ("氣血", stats.MaxHp.ToString()), ("內力", stats.MaxQi.ToString()),
            ("閃避", $"{stats.Dodge:P0}"), ("奇遇", $"{stats.Fortune:P0}")
        };

        /// <summary>
        /// The tooltip of one derived figure. <paramref name="total"/> is the figure as the game uses it (gear, tech) and
        /// <paramref name="battle"/> the figure after battle talents, when the caller knows them.
        /// </summary>
        internal static string Tip(string key, int[] attrs, SkillLine[] lines, int weaponDelta = 0,
            DerivedStats? total = null, BattleCombatant battle = null)
        {
            attrs = AttributeSystem.Normalize(attrs);
            lines ??= Array.Empty<SkillLine>();
            var stats = AttributeSystem.Derive(attrs, lines, weaponDelta);
            int B = AttributeSystem.Base;
            int str = attrs[0], con = attrs[2], qi = attrs[3];
            string martialRank = lines.Length == 0 ? "C" : RankSystem.Martial(lines, weaponDelta: weaponDelta);
            float martial = RankSystem.Multiplier(martialRank);
            string Pct(float rate) => $"{rate * 100:0.#}%";
            string rank = $"武學品階 {martialRank}（×{martial:0.##}）";
            const string damage = "傷害 ≈ 招式倍率 × 攻² ÷（攻 + 防）";
            string main = Pct(AttributeSystem.DefensePerPoint), minor = Pct(AttributeSystem.DefenseSecondaryPerPoint);
            string text;
            switch (key)
            {
                case "外功":
                case "攻擊":
                    text = $"外功 {stats.Attack}\n內功一路以外所有招式的傷害底數，打在對手的外防上；普攻取外功、內功較高者。\n" +
                           $"公式：{AttributeSystem.BaseAttack} ×（1 + {Pct(AttributeSystem.OuterPerPoint)} ×（武力 − {B}））× 武學品階倍率\n" +
                           $"目前：武力 {str}・{rank}\n來源：武力、最強兩門武學的品階（與內功同底 {AttributeSystem.BaseAttack}、每點同 +{Pct(AttributeSystem.OuterPerPoint)}）";
                    return text + Extra(total?.Attack, stats.Attack, battle?.attack, "外功");
                case "內功":
                    text = $"內功 {stats.QiPower}\n內功一路招式（及內功高於外功時的普攻）的傷害底數，打在對手的內防上。\n" +
                           $"公式：{AttributeSystem.BaseQiPower} ×（1 + {Pct(AttributeSystem.QiPowerPerPoint)} ×（內力 − {B}））× 武學品階倍率\n" +
                           $"目前：內力 {qi}・{rank}\n來源：內力屬性、最強兩門武學的品階（與外功同底、同每點加成）";
                    return text + Extra(total?.QiPower, stats.QiPower, battle?.internalAttack, "內功");
                case "外防":
                    text = $"外防 {stats.OuterDefense}\n抵擋普攻、外功招式與兵刃重擊。{damage}。\n" +
                           $"公式：{AttributeSystem.BaseDefense} ×（1 + {main} ×（根骨 − {B}）+ {minor} ×（武力 − {B}））× 武學品階倍率，至少 {AttributeSystem.MinDefense}\n" +
                           $"目前：根骨 {con}・武力 {str}・{rank}\n來源：根骨為主、武力為輔（與內防同底 {AttributeSystem.BaseDefense}）";
                    return text + Extra(total?.OuterDefense, stats.OuterDefense, battle?.outerDefense, "外防");
                case "內防":
                    text = $"內防 {stats.InnerDefense}\n抵擋內功招式與截脈手、黑松崩山等內勁。{damage}。\n" +
                           $"公式：{AttributeSystem.BaseDefense} ×（1 + {main} ×（內力 − {B}）+ {minor} ×（根骨 − {B}））× 武學品階倍率，至少 {AttributeSystem.MinDefense}\n" +
                           $"目前：內力 {qi}・根骨 {con}・{rank}\n來源：內力為主、根骨為輔（與外防同底 {AttributeSystem.BaseDefense}）";
                    return text + Extra(total?.InnerDefense, stats.InnerDefense, battle?.innerDefense, "內防");
                case "內力":
                    // v1126e #1: 內功威力 has its own cell now; drop the old trailing 「內功威力 N」.
                    text = DerivedStatTips.Tip("內力", attrs, lines);
                    int cut = text.LastIndexOf("；內功威力", StringComparison.Ordinal);
                    return cut > 0 ? text.Substring(0, cut) : text;
                default:
                    return DerivedStatTips.Tip(key, attrs, lines);
            }
        }

        private static string Extra(int? total, int raw, int? battle, string name)
        {
            string text = "";
            if (total.HasValue && total.Value != raw) text += $"\n連同裝備與門派科技：{name} {total.Value}";
            if (battle != null && battle.Value != (total ?? raw)) text += $"\n上陣時（含天賦）：{name} {battle.Value}";
            return text;
        }
    }

    public sealed partial class GameState
    {
        /// <summary>
        /// v1126e #1: a fighter's battle figures exactly as <see cref="BattleSetup"/> builds them (derived stats, then
        /// <see cref="ApplyBattleTalents"/>), so the character page can quote the numbers the fight will use.
        /// </summary>
        internal BattleCombatant BattleFigures(string id)
        {
            var stats = SparStats(id);
            if (stats.MaxHp <= 0) return null;
            return ApplyBattleTalents(id, new BattleCombatant
            {
                attack = id == HeroId ? Math.Max(28, stats.Attack) : stats.Attack,
                internalAttack = stats.QiPower, speed = id == HeroId ? Math.Max(15, stats.Speed) : stats.Speed,
                hp = stats.MaxHp, maxHp = stats.MaxHp, qi = stats.MaxQi, maxQi = stats.MaxQi,
                outerDefense = stats.OuterDefense, innerDefense = stats.InnerDefense, dodge = stats.Dodge
            });
        }
    }

    public sealed partial class SectUI
    {
        /// <summary>
        /// v1126e #1: the derived figures of a character page as a row of hoverable cells (外功 內功 外防 內防 先手 氣血 內力
        /// 閃避 命中 暴擊 奇遇) — replaces the v1126c one-line summary.
        /// </summary>
        internal void DerivedStrip(RectTransform parent, int[] attrs, SkillLine[] lines, int weaponDelta, DerivedStats shown,
            BattleCombatant battle)
        {
            var strip = Rect("衍生數值", parent);
            Anchor(strip, Vector2.zero, Vector2.right, new(8, 4), new(-8, 35));
            var cells = new List<(string key, string value)>(DerivedStatsV1126e.Cells(shown));
            cells.Insert(8, ("命中", $"{shown.Hit:+0%;-0%;0%}"));
            cells.Insert(9, ("暴擊", $"{shown.Crit:P0}"));
            for (int i = 0; i < cells.Count; i++)
            {
                var (key, value) = cells[i];
                var cell = Rect("衍生_" + key, strip);
                Anchor(cell, new(i / (float)cells.Count, 0), new((i + 1) / (float)cells.Count, 1), new(2, 0), new(-2, 0));
                var label = Label(cell, $"<color=#b9a98a>{key}</color> {value}", 19, TextAnchor.MiddleCenter);
                label.supportRichText = true;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 14;
                label.resizeTextMaxSize = 19;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.color = CardText;
                label.raycastTarget = false;
                Fill(label.rectTransform);
                string tipKey = key;
                DerivedHover(cell, () => tipKey switch
                {
                    "命中" => $"命中 {shown.Hit:+0%;-0%;0%}\n抵銷對手閃避的機率點數。\n公式：{AttributeSystem.HitPerPoint * 100:0.#}% ×（身法 − {AttributeSystem.Base}）\n來源：身法（天賦另計）",
                    "暴擊" => $"暴擊 {shown.Crit:P0}\n出手造成 1.5 倍傷害的機率。\n公式：{AttributeSystem.BaseCrit * 100:0.#}% + {AttributeSystem.CritPerPoint * 100:0.#}% ×（幸運 − {AttributeSystem.Base}）\n來源：幸運（天賦另計）",
                    _ => DerivedStatsV1126e.Tip(tipKey, attrs, lines, weaponDelta, shown, battle)
                });
            }
        }
    }
}
