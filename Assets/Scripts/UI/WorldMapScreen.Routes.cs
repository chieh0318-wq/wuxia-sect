using System;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>
    /// v1126b 決定 3 (PC2): the 天下第一 route's 「登門挑戰」 button on a master's faction card, and the four-route strip
    /// in the map's top bar.
    /// </summary>
    public sealed partial class WorldMapScreen
    {
        /// <summary>Raised when the player challenges a master; the callback receives the result line for the status bar.</summary>
        internal event Action<MapFaction, Action<string>> MasterChallengeRequested;

        private void MasterChallengeButton(Transform panel, MapFaction faction)
        {
            if (host is not GameState game || !GameState.IsMaster(faction)) return;
            bool beaten = game.MasterDefeated(faction.id);
            string block = game.MasterBlock(faction);
            var rules = RouteRules.Data.champion;
            var button = MapUi.Button(panel, beaten ? "已勝此人" : "登門挑戰", 303, 149, 103, 32, () =>
            {
                if (game.MasterBlock(faction) != null || MasterChallengeRequested == null) return;
                MasterChallengeRequested.Invoke(faction, result =>
                {
                    UpdateAp();
                    ShowFaction(faction);
                    if (!string.IsNullOrEmpty(result)) status.text = result;
                });
            }, MapUi.Red, 16);
            button.name = "登門挑戰";
            button.interactable = block == null && MasterChallengeRequested != null;
            Hover(button.gameObject, () =>
            {
                string reason = game.MasterBlock(faction);
                string head = $"天下第一之路：與{faction.leader}單打獨鬥。\n行動力 {rules.apCost}、銀兩 {rules.silverCost}；" +
                    $"勝則記一位成名高手（{game.MastersDefeated}／{rules.masters}），敗則負傷、{rules.retryMonths} 個月後再訪。";
                return reason == null ? head : head + "\n不可挑戰：" + reason;
            });
        }

        /// <summary>Compact live progress of the four routes for the map's top bar.</summary>
        private static string RouteStrip(GameState game)
        {
            var v = game.Victory;
            var r = RouteRules.Data;
            return $"破關　盟 {v.Loyal}/{r.diplomacy.loyal}　據 {v.Held}/{r.conquest.held}　" +
                $"商 {game.SettledTradeRoutes}/{r.wealth.routes}　勝 {game.MastersDefeated}/{r.champion.masters}";
        }
    }
}
