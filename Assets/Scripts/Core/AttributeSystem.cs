using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    /// <summary>Derived combat / adventure figures computed from the seven attributes and the skill lines.</summary>
    internal readonly struct DerivedStats
    {
        public readonly int Attack, Speed, MaxHp, MaxQi, QiPower, QiRegen;
        public readonly float Dodge, Fortune, Study, Hit, Crit;

        public DerivedStats(int attack, int speed, int maxHp, int maxQi, int qiPower, float dodge, float fortune, float study,
            float hit = 0, float crit = AttributeSystem.BaseCrit, int qiRegen = 0)
        {
            Attack = attack; Speed = speed; MaxHp = maxHp; MaxQi = maxQi; QiPower = qiPower;
            Dodge = dodge; Fortune = fortune; Study = study; Hit = hit; Crit = crit; QiRegen = qiRegen;
        }
    }

    /// <summary>
    /// The 7-attribute model (武力 身法 根骨 內力 悟性 魅力 幸運). Every attribute starts at <see cref="Base"/>,
    /// can be lowered to <see cref="Min"/> during creation (refunding points) and raised to <see cref="Max"/>.
    /// v1126b 決定 1 (Claude 案): one point ≈ +5% on an attribute's main use and +3% on its secondary uses. All
    /// per-point figures live here so the creation screen and the character page can read them back.
    /// </summary>
    internal static class AttributeSystem
    {
        public const int Base = 10, Min = 5, Max = 20, Count = 7;
        public static readonly string[] Keys = { "str", "agi", "con", "qi", "wit", "cha", "luk" };
        public static readonly string[] Names = { "武力", "身法", "根骨", "內力", "悟性", "魅力", "幸運" };
        public static readonly string[] Glyphs = { "武", "身", "骨", "氣", "悟", "魅", "運" };

        // ── per-point table (v1126b 決定 1) ──
        /// <summary>Main uses: +5% of the base-10 figure per point.</summary>
        public const float OuterPerPoint = .05f, SpeedPerPoint = .05f, HpPerPoint = .05f, QiPerPoint = .05f, QiPowerPerPoint = .05f;
        /// <summary>身法: dodge and (new) hit, in percentage points.</summary>
        public const float DodgePerPoint = .01f, DodgeCap = .40f, HitPerPoint = .01f;
        /// <summary>悟性: every kind of training +4% per point; 頓悟 (breakthrough) +0.3 points.</summary>
        public const float StudyPerPoint = .04f, InsightPerPoint = .003f;
        /// <summary>Secondary uses: dispatched-work quality, realm / defence checks, +3% per point.</summary>
        public const float CraftPerPoint = .03f, CheckPerPoint = .03f;
        /// <summary>魅力: diplomacy success per point of 口才 (魅力 + 悟性/2), new-disciple loyalty, reputation gains.</summary>
        public const float DiplomacyPerPoint = .025f, ReputationPerPoint = .03f;
        public const int LoyaltyPerPoint = 2;
        /// <summary>幸運: qi-yu odds ×1.04 per point (relative), crit +0.8 points, grave → light wound +2 points.</summary>
        public const float FortuneGrowth = 1.04f, BaseFortune = .05f, BaseCrit = .08f, CritPerPoint = .008f, GraveToLightPerPoint = .02f;
        /// <summary>Base-10 combat figures (attack 38, speed 20, hp 180, qi 130, qi power 25).</summary>
        public const int BaseAttack = 38, BaseSpeed = 20, BaseHp = 180, BaseQi = 130, BaseQiPower = 25;

        public static readonly string[] Benefits =
        {
            "外功傷害為主；兼及鍛造品質與破門強闖。",
            "速度、閃避與命中；兼及攔截、探查。",
            "氣血與療傷；兼及耕作品質與守山。",
            "內力上限與內功威力；兼及閉關修練與每回合回氣。",
            "所有修練的進度；兼及煉丹、醫術、建造、教導與頓悟。",
            "外交成功、新弟子忠誠；兼及聲望、經商、招募與請援收買。",
            "奇遇機率與暴擊；兼及秘境判定、寶物品質與化險為夷。"
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

        internal static string FormatBonus(string key, int value)
        {
            int slot = Index(key);
            return $"{(slot >= 0 ? Names[slot] : key)} {value:+0;-0;0}";
        }

        internal static string Format(int basis, int bonus, string tint) =>
            bonus == 0 ? $"基礎 {basis}" : $"基礎 {basis} <color={tint}>{bonus:+0;-0}</color>";

        public static int Value(int[] attrs, string key)
        {
            int index = Index(key);
            return index < 0 || attrs == null || index >= attrs.Length ? Base : attrs[index];
        }

        /// <summary>Signed distance from the base value (negative when an attribute was lowered below 10).</summary>
        public static int Bonus(int[] attrs, string key) => Value(attrs, key) - Base;

        /// <summary>1 + rate × (value − 10), never below a quarter.</summary>
        internal static float Scale(int value, float rate) => Mathf.Max(.25f, 1 + rate * (value - Base));

        /// <summary>幸運: relative qi-yu factor, ×1.04 per point above (÷ below) the base.</summary>
        internal static float FortuneFactor(int luck) => Mathf.Pow(FortuneGrowth, luck - Base);

        /// <summary>Training speed from 悟性 (the only attribute that speeds up training).</summary>
        internal static float StudyFactor(int wit) => Mathf.Max(.5f, 1 + StudyPerPoint * (wit - Base));

        /// <summary>Quality of dispatched work: skill rank plus the deciding attribute (口才 is centred on 15).</summary>
        internal static float WorkQuality(int rank, int value, int basis) =>
            Mathf.Clamp(.85f + .09f * rank + (value - basis) * CraftPerPoint, .6f, 1.8f);

        /// <summary>Realm / defence / interception checks: +3 points per attribute point.</summary>
        internal static float CheckBonus(int value) => (value - Base) * CheckPerPoint;

        /// <summary>Chance that a grave wound (2+ levels) is softened to a light one.</summary>
        internal static float GraveToLight(int luck) => Mathf.Clamp((luck - Base) * GraveToLightPerPoint, 0, .5f);

        public static DerivedStats Derive(int[] attrs, SkillLine[] lines, int weaponDelta = 0)
        {
            attrs = Normalize(attrs);
            lines ??= Array.Empty<SkillLine>();
            float martial = RankSystem.Multiplier(lines.Length == 0 ? "C" : RankSystem.Martial(lines, weaponDelta: weaponDelta));
            int Tier(string id) => RankSystem.Index(Array.Find(lines, line => line?.id == id)?.rank);
            int str = attrs[0], agi = attrs[1], con = attrs[2], qi = attrs[3], wit = attrs[4], luk = attrs[6];
            int attack = Mathf.RoundToInt(BaseAttack * Scale(str, OuterPerPoint) * martial);
            int speed = Mathf.RoundToInt(BaseSpeed * Scale(agi, SpeedPerPoint)) + Tier("qinggong") * 3;
            float dodge = Mathf.Clamp(.05f + (agi - Base) * DodgePerPoint + Tier("qinggong") * .01f, 0, DodgeCap);
            float hit = (agi - Base) * HitPerPoint;
            int maxHp = Mathf.RoundToInt(BaseHp * Scale(con, HpPerPoint));
            int maxQi = Mathf.RoundToInt(BaseQi * Scale(qi, QiPerPoint)) + Tier("neigong") * 10;
            int qiPower = Mathf.RoundToInt(BaseQiPower * Scale(qi, QiPowerPerPoint) * martial);
            int qiRegen = Mathf.Max(0, qi - Base);
            float fortune = Mathf.Clamp(BaseFortune * FortuneFactor(luk), 0, .6f);
            float crit = Mathf.Clamp(BaseCrit + (luk - Base) * CritPerPoint, .02f, .5f);
            float study = StudyFactor(wit);
            return new DerivedStats(attack, speed, maxHp, maxQi, qiPower, dodge, fortune, study, hit, crit, qiRegen);
        }

        /// <summary>
        /// Everything one more point of <paramref name="slot"/> buys, read from <see cref="Derive"/> (combat figures,
        /// one point above base with every other attribute at base) and the per-point constants above.
        /// </summary>
        internal static List<string> PerPoint(int slot)
        {
            var baseAttrs = Enumerable.Repeat(Base, Count).ToArray();
            var raised = (int[])baseAttrs.Clone();
            raised[slot]++;
            var lines = RankSystem.Seed(null, RankSystem.PrimaryRank, RankSystem.PrimaryCap, RankSystem.BaseCap, RankSystem.BaseRank);
            var before = Derive(baseAttrs, lines);
            var after = Derive(raised, lines);
            var parts = new List<string>();
            Rel("外功傷害", after.Attack, before.Attack, OuterPerPoint);
            Rel("速度", after.Speed, before.Speed, SpeedPerPoint);
            Pts("閃避", after.Dodge - before.Dodge);
            Pts("命中", after.Hit - before.Hit);
            Rel("氣血", after.MaxHp, before.MaxHp, HpPerPoint);
            Rel("內力上限", after.MaxQi, before.MaxQi, QiPerPoint);
            Rel("內功威力", after.QiPower, before.QiPower, QiPowerPerPoint);
            if (after.QiRegen != before.QiRegen) parts.Add($"每回合回內力 {after.QiRegen - before.QiRegen:+0}");
            if (Mathf.Abs(after.Study - before.Study) > .0005f) parts.Add($"所有修練 {(after.Study - before.Study) * 100:+0.#}%");
            if (Mathf.Abs(after.Fortune / before.Fortune - 1) > .0005f) parts.Add($"奇遇機率 ×{after.Fortune / before.Fortune:0.00}");
            Pts("暴擊", after.Crit - before.Crit);
            string craft = $"{CraftPerPoint * 100:+0}%", check = $"{CheckPerPoint * 100:+0}%";
            switch (Keys[slot])
            {
                case "str": parts.Add($"鍛造品質 {craft}"); parts.Add($"破門強闖判定 {check}"); break;
                case "agi": parts.Add($"攔截、探查判定 {check}"); parts.Add($"潛行、飛渡判定 {check}"); break;
                case "con":
                    parts.Add("療傷：每 3 點多癒 1 級"); parts.Add($"耕作品質 {craft}"); parts.Add($"守山判定 {check}");
                    break;
                case "qi": parts.Add($"閉關修練品質 {craft}"); parts.Add($"運功判定 {check}"); break;
                case "wit":
                    parts.Add($"煉丹、醫術、建造、教導品質 {craft}"); parts.Add($"頓悟機率 +{InsightPerPoint * 100:0.0}%");
                    parts.Add("口才 +0.5"); parts.Add($"參悟判定 {check}");
                    break;
                case "cha":
                    parts.Add($"外交成功 +{DiplomacyPerPoint * 100:0.#}%"); parts.Add($"新弟子忠誠 +{LoyaltyPerPoint}");
                    parts.Add($"聲望收穫 +{ReputationPerPoint * 100:0}%"); parts.Add($"經商、招募品質 {craft}");
                    parts.Add($"請援、收買判定 {check}");
                    break;
                case "luk":
                    parts.Add($"秘境判定 {check}"); parts.Add($"探索、寶物品質 {craft}");
                    parts.Add($"重傷化輕傷 +{GraveToLightPerPoint * 100:0}%");
                    break;
            }
            return parts;

            // Relative figures show the formula's rate (the rounded integers would read 4.6% / 5.3%).
            void Rel(string name, int now, int was, float rate)
            {
                if (now != was) parts.Add($"{name} +{rate * 100:0.#}%");
            }
            void Pts(string name, float delta)
            {
                if (Mathf.Abs(delta) >= .0005f) parts.Add($"{name} {delta * 100:+0.#;-0.#}%");
            }
        }
    }
}
