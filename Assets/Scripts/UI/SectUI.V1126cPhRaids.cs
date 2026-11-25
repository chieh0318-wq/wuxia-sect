using System;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    public sealed partial class SectUI
    {
        private RectTransform raidAlert;
        private Text raidAlertLabel;

        internal void BindRaidAlert(Action open)
        {
            if (raidAlert != null) return;
            raidAlert = Button(canvas, "", open);
            Anchor(raidAlert, new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(-320, -160), new Vector2(320, -110));
            UiKit.Skin(raidAlert.GetComponent<Image>(), "Selected");
            raidAlertLabel = raidAlert.GetComponentInChildren<Text>();
        }

        private void RefreshRaidAlert(GameState state)
        {
            if (raidAlert == null) return;
            var raid = state.Story.sectRaid;
            raidAlert.gameObject.SetActive(raid != null);
            if (raid == null) return;
            string foe = GameState.FindFaction(raid.faction)?.Label ?? raid.faction;
            string eta = state.RaidReady ? "已抵山門" : $"距抵達尚有 {raid.due - state.MonthStamp} 月";
            string next = state.RaidReady ? "迎戰" : raid.guards?.Length > 0 ? "查看守備" : "指派守將";
            raidAlertLabel.text = $"軍情｜{foe}・兵力 {raid.troops}・{eta}　{next} ›";
        }
    }
}
