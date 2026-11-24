using System;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    /// <summary>Derived combat / adventure figures computed from the seven attributes and the skill lines.</summary>
    internal readonly struct DerivedStats
    {
        public readonly int Attack, Speed, MaxHp, MaxQi, QiPower;
        public readonly float Dodge, Fortune, Study;

        public DerivedStats(int attack, int speed, int maxHp, int maxQi, int qiPower, float dodge, float fortune, float study)
        {
            Attack = attack; Speed = speed; MaxHp = maxHp; MaxQi = maxQi; QiPower = qiPower;
            Dodge = dodge; Fortune = fortune; Study = study;
        }
    }

    /// <summary>
    /// The 7-attribute model (武力 身法 根骨 內力 悟性 魅力 幸運). Every attribute starts at <see cref="Base"/>,
    /// can be lowered to <see cref="Min"/> during creation (refunding points) and raised to <see cref="Max"/>.
    /// </summary>
    internal static class AttributeSystem
    {
        public const int Base = 10, Min = 5, Max = 20, Count = 7;
        public static readonly string[] Keys = { "str", "agi", "con", "qi", "wit", "cha", "luk" };
        public static readonly string[] Names = { "武力", "身法", "根骨", "內力", "悟性", "魅力", "幸運" };
        public static readonly string[] Glyphs = { "武", "身", "骨", "氣", "悟", "魅", "運" };

        public static readonly string[] Benefits =
        {
            "影響外功攻擊力與實戰傷害，並微幅提升武學修習。",
            "影響戰鬥速度／先手順序與閃避機率。",
            "影響體力（氣血上限）與療傷恢復速度。",
            "影響內力上限與內功招式威力、續戰力。",
            "影響你與弟子的武學修習進度。",
            "影響新入門弟子初始忠誠、門派聲望與交涉。",
            "影響探索、山道巡訪與江湖事件觸發奇遇的機率。"
        };

        public static readonly Color[] Tints =
        {
            new(.62f, .23f, .17f), new(.25f, .49f, .55f), new(.47f, .35f, .21f), new(.33f, .30f, .58f),
            new(.20f, .44f, .32f), new(.71f, .36f, .47f), new(.74f, .57f, .19f)
        };

        public static int Index(string key) => Array.IndexOf(Keys, key);

        /// <summary>
        /// Brings any stored attribute array up to the 7-attribute layout. Legacy 5-element arrays were
        /// [str, agi, con, wit, cha] on the old base-3 scale: they are lifted onto the base-10 scale and the
        /// new 內力 / 幸運 slots are padded with <see cref="Base"/>.
        /// </summary>
        public static int[] Normalize(int[] attrs)
        {
            if (attrs == null || attrs.Length == 0)
                return Enumerable.Repeat(Base, Count).ToArray();
            if (attrs.Length >= Count)
                return attrs.Length == Count ? attrs : attrs.Take(Count).ToArray();
            if (attrs.Length == 5)
            {
                int Lift(int value) => Mathf.Clamp(value + (Base - 3), 1, 30);
                return new[] { Lift(attrs[0]), Lift(attrs[1]), Lift(attrs[2]), Base, Lift(attrs[3]), Lift(attrs[4]), Base };
            }
            return Enumerable.Range(0, Count).Select(index => index < attrs.Length ? attrs[index] : Base).ToArray();
        }

        internal static int Allocated(int value) => Mathf.Clamp(value, 1, Max);

        internal static string FormatBonus(string key, int value) =>
            $"{(Index(key) is int slot && slot >= 0 ? Names[slot] : key)} {value:+0;-0;0}";

        internal static string Format(int basis, int bonus, string tint) =>
            bonus == 0 ? $"基礎 {basis}" : $"基礎 {basis} <color={tint}>{bonus:+0;-0}</color>";

        public static int Value(int[] attrs, string key)
        {
            int index = Index(key);
            return index < 0 || attrs == null || index >= attrs.Length ? Base : attrs[index];
        }

        /// <summary>Signed distance from the base value (negative when an attribute was lowered below 10).</summary>
        public static int Bonus(int[] attrs, string key) => Value(attrs, key) - Base;

        public static DerivedStats Derive(int[] attrs, SkillLine[] lines, int weaponDelta = 0)
        {
            attrs = Normalize(attrs);
            lines ??= Array.Empty<SkillLine>();
            float martial = RankSystem.Multiplier(lines.Length == 0 ? "C" : RankSystem.Martial(lines, weaponDelta: weaponDelta));
            int Tier(string id) => RankSystem.Index(Array.Find(lines, line => line?.id == id)?.rank);
            int str = attrs[0], agi = attrs[1], con = attrs[2], qi = attrs[3], wit = attrs[4], luk = attrs[6];
            int attack = Mathf.RoundToInt((8 + str * 3) * martial);
            int speed = agi * 2 + Tier("qinggong") * 3;
            float dodge = Mathf.Clamp(.05f + (agi - Base) * .015f + Tier("qinggong") * .01f, 0, .6f);
            int maxHp = 60 + con * 12;
            int maxQi = 30 + qi * 10 + Tier("neigong") * 10;
            int qiPower = Mathf.RoundToInt(qi * 2.5f * martial);
            float fortune = Mathf.Clamp(.05f + (luk - Base) * .012f + (agi - Base) * .003f, 0, .6f);
            float study = 1 + .05f * (wit - Base) + .02f * (str - Base);
            return new DerivedStats(attack, speed, maxHp, maxQi, qiPower, dodge, fortune, study);
        }
    }
}
