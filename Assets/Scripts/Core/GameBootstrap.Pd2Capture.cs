using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private SectCampaign captureCampaign;

        private (string filename, Action show)[] Pd2CaptureSteps()
        {
            GameState previous = null;
            MapNode target = null;
            int ap = 0, fame = 0, silver = 0, food = 0;
            int troops = 0;
            return new (string, Action)[]
            {
                ("PD2_00_attribute_hover.png", () =>
                {
                    if (map != null && map.IsOpen) map.Close();
                    CleanupV1125PaCapture();
                    EnsureCaptureCourtyard();
                    RequirePd2(!AudioManager.VoiceEnabled && !AudioManager.VoicePlaying, "voice defaults off");
                    CaptureFlow().CaptureAttributes();
                }),
                ("PD2_01_campaign_map.png", () =>
                {
                    ui.CloseModal(force: true);
                    // Set up an arrived party at a known outlaw seat. Combat uses live UI commands.
                    previous = state;
                    state = new GameState(data);
                    state.Protagonist.name = previous.Protagonist.name;
                    state.Protagonist.gender = previous.Protagonist.gender;
                    state.Protagonist.lookId = state.Protagonist.portrait = previous.Protagonist.portrait;
                    state.Protagonist.title = "代掌門";
                    state.Story.mapNode = previous.StartNode;
                    ui.Refresh(state);
                    state.RefillAp();
                    var worldMap = WorldMapData.Load();
                    var faction = worldMap.Factions.Where(foe => foe.Outlaw && worldMap.Nodes.ContainsKey(foe.seat)
                            && foe.seat != state.StartNode && !state.HoldsNode(foe.seat))
                        .OrderBy(foe => foe.disciples).First();
                    target = worldMap.Nodes[faction.seat];
                    state.PartyNode = target.id;
                    state.SetFactionKnown(faction.id);
                    ap = state.ActionPoints;
                    fame = state.Reputation;
                    silver = state.Silver;
                    food = state.Food;
                    OpenCaptureMap();
                    ui.Canvas.enabled = true;
                    map.CaptureStronghold(faction);
                }),
                ("PD2_02_campaign_deployment.png", () =>
                {
                    var attack = map.CaptureCanvas.GetComponentsInChildren<Button>()
                        .Last(button => button.name == "攻打" && button.interactable);
                    attack.onClick.Invoke();
                    var party = new[] { GameState.HeroId }.Concat(state.Disciples.Select(member => member.id))
                        .Where(id => AssignmentSystem.Block(state, id) == null).Take(4).ToArray();
                    foreach (string id in party)
                    {
                        // v1126c picker rows: the row button has an empty caption; the name is the first cell text.
                        string name = state.SparName(id);
                        var choice = ui.Canvas.GetComponentsInChildren<Button>().LastOrDefault(button =>
                            button.interactable && button.GetComponentsInChildren<Text>()
                                .Any(cell => cell.text.Split('\n').Contains(name)));
                        RequirePd2(choice != null, $"deployment picker lists {name}");
                        choice.onClick.Invoke();
                    }
                    ClickCaptureCaption("確認派遣");
                    RequirePd2(campaignOpen && captureCampaign.Allies.Length == party.Length, "map attack opened deployment");
                    ClickCaptureCaption(SectCampaign.FormationNames[(int)WarFormation.Circle]);
                    RequirePd2(captureCampaign.Formation == WarFormation.Circle, "defensive formation selected");
                    troops = TotalTroops();
                }),
                ("PD2_03_campaign_troop_damage.png", () =>
                {
                    ClickCaptureCaption("開戰　行動力 2");
                    ClickCaptureCaption("整軍");
                    int survivors = TotalTroops();
                    RequirePd2(state.ActionPoints == ap - 2 && captureCampaign.Round == 1 &&
                        survivors < troops && captureCampaign.CanDuel,
                        $"first round spent 2 AP and reduced combined troop count from {troops} to {survivors}");
                }),
                ("PD2_04_campaign_duel.png", () =>
                {
                    ClickCaptureCaption("主將對決 ›");
                    RequirePd2(ui.BattleOpen, "campaign opened its commander duel");
                    ClickCaptureCaption("x1 速度");
                    ClickCaptureCaption("x2 加速");
                    ClickCaptureCaption("自動");
                }),
                ("PD2_05_campaign_duel_result.png", () =>
                {
                    RequirePd2(!ui.BattleOpen && captureCampaign.DuelUsed && Math.Abs(captureCampaign.Morale) == 8,
                        $"duel returned to campaign with morale {captureCampaign.Morale:+0;-0}");
                }),
                ("PD2_06_campaign_last_round.png", () =>
                {
                    while (!captureCampaign.Finished && captureCampaign.Round < 30)
                        ClickCaptureCaption("強攻");
                    RequirePd2(captureCampaign.Finished,
                        $"campaign finished in round {captureCampaign.Round}; victory={captureCampaign.Won}; troops={TotalTroops()}");
                }),
                ("PD2_07_campaign_settlement.png", () =>
                {
                    bool won = captureCampaign.Won;
                    ClickCaptureCaption("收兵");
                    RequirePd2(!campaignOpen && state.HoldsNode(target.id) == won &&
                        state.Reputation == fame + (won ? 6 : -2) &&
                        state.Silver == silver + (won ? 30 : 0) &&
                        state.Food == food + (won ? 20 : 0),
                        $"map settlement verified for {target.Label}; victory={won}; AP={ap}->{state.ActionPoints}");
                }),
                ("PD2_08_route_progress.png", () =>
                {
                    map.Close();
                    Toast.ClearAll();
                    ui.ShowQuestPanel(state);
                    var routes = state.Routes().ToArray();
                    RequirePd2(routes.Length == 4 && routes.All(route => !string.IsNullOrEmpty(route.Progress)),
                        "four routes have live progress");
                }),
                ("PD2_09_xie_jin_arrival.png", () => CaptureFlow(female: true).CaptureFriendArrival(true)),
                ("PD2_10_xie_yun_arrival.png", () =>
                {
                    state = previous;
                    ui.Refresh(state);
                    ui.ShowQuestPanel(state);
                    captureCampaign = null;
                    CaptureFlow().CaptureFriendArrival(false);
                })
            };

            int TotalTroops() => captureCampaign.Allies.Concat(captureCampaign.Enemies).Sum(unit => unit.Troops);
        }

        private void RevealPd2AttributeTooltip()
        {
            Canvas.ForceUpdateCanvases();
            var hover = ui.Canvas.GetComponentsInChildren<EventTrigger>().Last(trigger =>
                trigger.name == AttributeSystem.Names[0]);
            hover.OnPointerEnter(new PointerEventData(EventSystem.current));
            var tooltip = ui.Canvas.GetComponentsInChildren<RectTransform>()
                .LastOrDefault(rect => rect.name == "屬性提示");
            RequirePd2(tooltip != null && tooltip.GetComponentInChildren<Text>()?.text.Contains("外功傷害 +5%") == true,
                "martial attribute tooltip lists its per-point attack bonus");
        }

        private static void RequirePd2(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("[PD2] " + description);
            Debug.Log("[PD2] PASS " + description);
        }
    }
}
