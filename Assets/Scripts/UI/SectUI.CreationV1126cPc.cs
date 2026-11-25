using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>
    /// v1126c PC3 #3: what each derived figure on the creation card (攻擊 先手 氣血 內力 閃避 奇遇) means, its formula with
    /// the live coefficients from <see cref="AttributeSystem"/>, and the attributes / skill lines that feed it.
    /// </summary>
    internal static class DerivedStatTips
    {
        internal static readonly string[] Keys = { "攻擊", "先手", "氣血", "內力", "閃避", "奇遇" };

        internal static string Tip(string key, int[] attrs, SkillLine[] lines)
        {
            attrs = AttributeSystem.Normalize(attrs);
            lines ??= Array.Empty<SkillLine>();
            var stats = AttributeSystem.Derive(attrs, lines);
            int B = AttributeSystem.Base;
            int str = attrs[0], agi = attrs[1], con = attrs[2], qi = attrs[3], luk = attrs[6];
            string martialRank = lines.Length == 0 ? "C" : RankSystem.Martial(lines);
            float martial = RankSystem.Multiplier(martialRank);
            string Line(string id) => RankSystem.At(RankSystem.Index(Array.Find(lines, line => line?.id == id)?.rank));
            int Tier(string id) => RankSystem.Index(Array.Find(lines, line => line?.id == id)?.rank);
            string Pct(float rate) => $"{rate * 100:0.#}%";
            switch (key)
            {
                case "攻擊":
                    return $"攻擊 {stats.Attack}\n每次出手的基本傷害（外功招式以此為底）。\n" +
                           $"公式：{AttributeSystem.BaseAttack} ×（1 + {Pct(AttributeSystem.OuterPerPoint)} ×（武力 − {B}））× 武學品階倍率\n" +
                           $"目前：武力 {str}・武學品階 {martialRank}（×{martial:0.##}）\n" +
                           "來源：武力、最強兩門武學的品階（兩門皆 A 以上且相差一階內，再升一階）";
                case "先手":
                    return $"先手 {stats.Speed}\n戰鬥中出手的先後；數值高者先動。\n" +
                           $"公式：{AttributeSystem.BaseSpeed} ×（1 + {Pct(AttributeSystem.SpeedPerPoint)} ×（身法 − {B}））+ 輕功品階 × {AttributeSystem.QinggongSpeedPerTier}\n" +
                           $"目前：身法 {agi}・輕功 {Line("qinggong")}（第 {Tier("qinggong")} 階，C 為 0）\n" +
                           "來源：身法、輕功";
                case "氣血":
                    return $"氣血 {stats.MaxHp}\n能承受的傷害；歸零即重傷倒地。\n" +
                           $"公式：{AttributeSystem.BaseHp} ×（1 + {Pct(AttributeSystem.HpPerPoint)} ×（根骨 − {B}））\n" +
                           $"目前：根骨 {con}\n來源：根骨（另每 3 點根骨療傷多癒 1 級）";
                case "內力":
                    return $"內力 {stats.MaxQi}\n施展招式所耗的真氣上限；每回合回氣 {stats.QiRegen}。\n" +
                           $"公式：{AttributeSystem.BaseQi} ×（1 + {Pct(AttributeSystem.QiPerPoint)} ×（內力 − {B}））+ 內功品階 × {AttributeSystem.NeigongQiPerTier}\n" +
                           $"目前：內力 {qi}・內功 {Line("neigong")}（第 {Tier("neigong")} 階）・每回合回氣 = 內力 − {B}（不低於 0）\n" +
                           $"來源：內力屬性、內功；內功威力 {stats.QiPower}";
                case "閃避":
                    return $"閃避 {stats.Dodge:P0}\n敵方攻擊落空的機率。\n" +
                           $"公式：{Pct(AttributeSystem.BaseDodge)} + {Pct(AttributeSystem.DodgePerPoint)} ×（身法 − {B}）+ 輕功品階 × {Pct(AttributeSystem.QinggongDodgePerTier)}，上限 {Pct(AttributeSystem.DodgeCap)}\n" +
                           $"目前：身法 {agi}・輕功 {Line("qinggong")}\n來源：身法、輕功";
                case "奇遇":
                    return $"奇遇 {stats.Fortune:P0}\n山道巡訪、行旅途中遇上奇遇（秘笈、異人、寶物）的機率。\n" +
                           $"公式：{Pct(AttributeSystem.BaseFortune)} × {AttributeSystem.FortuneGrowth:0.00}^（幸運 − {B}），上限 {Pct(AttributeSystem.FortuneCap)}\n" +
                           $"目前：幸運 {luk}（×{AttributeSystem.FortuneFactor(luk):0.00}）・暴擊 {stats.Crit:P0}\n來源：幸運";
            }
            return "";
        }
    }

    /// <summary>v1126c PC3 #4: right-click on a picked starting-gear tile hands one back (and refunds its points).</summary>
    internal sealed class RightClickRefund : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        public Action refund;

        public void OnPointerDown(PointerEventData eventData)
        {
            // Claim the right click on press, so the release does not also count as 「上一步」 (UiBack).
            if (eventData.button == PointerEventData.InputButton.Right && refund != null) UiBack.SwallowRightClick();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right) refund?.Invoke();
        }
    }

    internal static class SceneStageCapture
    {
        /// <summary>Capture helper: presses the scene page's 繼續 (next line).</summary>
        internal static void AdvanceForCapture(this SceneStage stage)
        {
            var next = stage.Page == null ? null : stage.Page.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "場景繼續");
            next?.onClick.Invoke();
        }
    }

    public sealed partial class SectUI
    {
        /// <summary>v1126c PC3 #3: hovering a derived figure shows <see cref="DerivedStatTips.Tip"/>.</summary>
        internal void DerivedHover(RectTransform target, Func<string> text)
        {
            if (target == null) return;
            var surface = target.GetComponent<Graphic>() ?? target.gameObject.AddComponent<Image>();
            if (surface is Image image && image.sprite == null && image.color.a > .99f && target.GetComponent<Button>() == null)
                image.color = new Color(0, 0, 0, 0);
            surface.raycastTarget = true;
            var trigger = target.gameObject.GetComponent<EventTrigger>() ?? target.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => ShowAttributeTip(target, text()));
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => HideAttributeTip());
            trigger.triggers.Add(enter);
            trigger.triggers.Add(exit);
        }

        /// <summary>Capture helper: shows the derived-stat tooltip for <paramref name="key"/> on the open creation card.</summary>
        internal bool RevealDerivedTipForCapture(string key)
        {
            var cell = creationSummary == null ? null :
                creationSummary.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(rect => rect.name == "衍生_" + key);
            if (cell == null) return false;
            var trigger = cell.GetComponent<EventTrigger>();
            var enter = trigger?.triggers.FirstOrDefault(entry => entry.eventID == EventTriggerType.PointerEnter);
            enter?.callback.Invoke(new PointerEventData(EventSystem.current));
            return enter != null;
        }

        /// <summary>Capture helper: shows the hover tip of the creation attribute row <paramref name="slot"/>.</summary>
        internal bool RevealAttributeRowTipForCapture(int slot)
        {
            var row = modalList.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(rect => rect.name == AttributeSystem.Names[slot] && rect.GetComponent<EventTrigger>() != null);
            var enter = row?.GetComponent<EventTrigger>().triggers.FirstOrDefault(entry => entry.eventID == EventTriggerType.PointerEnter);
            enter?.callback.Invoke(new PointerEventData(EventSystem.current));
            return enter != null;
        }

        /// <summary>Capture helper: the 先輩遺澤 point balance and picks as shown.</summary>
        internal string LegacyShopSummary()
        {
            var points = creationSummary == null ? null : creationSummary.GetComponentsInChildren<Text>(true)
                .FirstOrDefault(text => text.text.StartsWith("剩餘", StringComparison.Ordinal));
            int picked = modalList.GetComponentsInChildren<RightClickRefund>(true).Length;
            return $"{points?.text ?? "?"}・已選 {picked} 種";
        }

        /// <summary>Capture helper: right-clicks the first picked 先輩遺澤 tile (same handler as a real right click).</summary>
        internal string RightClickFirstLegacyPick()
        {
            var tile = modalList.GetComponentsInChildren<RightClickRefund>(true).FirstOrDefault();
            if (tile == null) return "no picked tile";
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right };
            tile.OnPointerDown(data);
            tile.OnPointerClick(data);
            return "clicked";
        }

        /// <summary>Capture helper: hovers the settlement-map flag of the seat holding <paramref name="factionId"/>.</summary>
        internal bool RevealSettlementTipForCapture(string factionId)
        {
            var data = WorldMapData.Load();
            var seat = data.Factions.FirstOrDefault(f => f.id == factionId)?.seat;
            var flag = modalList.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(rect =>
                rect.name.StartsWith("附近勢力_", StringComparison.Ordinal) &&
                data.Factions.FirstOrDefault(f => f.id == rect.name.Substring(5))?.seat == seat);
            var enter = flag?.GetComponent<EventTrigger>()?.triggers.FirstOrDefault(entry => entry.eventID == EventTriggerType.PointerEnter);
            enter?.callback.Invoke(new PointerEventData(EventSystem.current));
            return enter != null;
        }

        /// <summary>Capture/check helper: every creation attribute row lies fully inside the visible list viewport.</summary>
        internal string CreationAttributeRowsVisible()
        {
            var view = (RectTransform)modalList.parent;
            var corners = new Vector3[4];
            view.GetWorldCorners(corners);
            float bottom = corners[0].y, top = corners[1].y;
            var problems = AttributeSystem.Names.Where(name =>
            {
                var row = modalList.Cast<Transform>().FirstOrDefault(child => child.name == name) as RectTransform;
                if (row == null) return true;
                var rc = new Vector3[4];
                row.GetWorldCorners(rc);
                return rc[0].y < bottom - .5f || rc[1].y > top + .5f;
            }).ToArray();
            return problems.Length == 0 ? "ok (7/7 rows visible)" : "hidden: " + string.Join("、", problems);
        }
    }
}
