using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private bool campaignOpen;

        private void ChooseTraining(ActionData action, string[] party)
        {
            if (AssignmentSystem.Block(state, party[0]) is string blocked)
            {
                ui.Message(blocked);
                return;
            }
            ui.ShowCreation("選擇修練秘笈", new[] { "主修一路額外獲得兩成修練經驗；弟子的主修以最強武學為準。" },
                new[] { ("自動修練", (Action)(() => Select(null))), ("返回", (Action)(() => ui.CloseModal(force: true))) }, null);
            foreach (var art in state.TrainingManuals(party[0]))
                ui.CreationChoice(art.name, EffectText.LineName(art.category), null, () => Select(art.id));

            void Select(string manual)
            {
                ui.CloseModal(force: true);
                ExecuteAssignedAction(action, party, manual);
            }
        }

        private void ShowRaidDesk()
        {
            var raid = state.Story.sectRaid;
            var lines = new List<string>();
            var buttons = new List<(string, Action)> { ("關閉", () => ui.CloseModal(force: true)) };
            if (state.FactionWorld.envoy != null) buttons.Add(("來使 ›", ShowFactionEnvoy));
            if (raid == null)
                lines.Add("目前山道平靜。加蓋城牆、哨塔和候訊房，可加強守備並及早掌握敵軍。");
            else
            {
                if (raid.detected)
                {
                    var foe = GameState.FindFaction(raid.faction);
                    int month = (raid.due - 1) % 12 + 1;
                    lines.Add($"{foe.Label}從{state.WarNode(foe.seat)}出兵，預計{month}月抵達{state.WarNode(raid.target)}，兵力 {raid.troops}。");
                    lines.Add($"守備加成 {raid.preparation}　援兵 {raid.aid}　{(raid.counter == "disrupted" ? "敵方斥候失聯" : "")}");
                }
                else lines.Add(state.RaidReady ? "敵軍突然現身，守軍來不及選陣。" : "山道出現陌生人馬，敵軍來向與兵力未明。");
                if (state.RaidReady) buttons.Add(("迎戰 ›", StartDefensiveCampaign));
                else
                {
                    Add("遣細作", RaidOrder.Scout, 1);
                    Add("指派守將", RaidOrder.Garrison, 10);
                    Add("預先佈防", RaidOrder.Prepare, 1);
                    Add("調請援兵", RaidOrder.Reinforce, 1);
                    Add("散布假消息", RaidOrder.Rumor, 1);
                    Add("刺殺敵探", RaidOrder.Intercept, 1);
                    Add("收買內線・銀兩 20", RaidOrder.Bribe, 1);
                }
            }
            ui.ShowCreation("山門軍情", lines.ToArray(), buttons.Take((state.RaidReady ? 2 : 1) + (state.FactionWorld.envoy != null ? 1 : 0)).ToArray(), null);
            foreach (var option in buttons.Skip((state.RaidReady ? 2 : 1) + (state.FactionWorld.envoy != null ? 1 : 0)))
                ui.CreationChoice(option.Item1, "派人後使用當月出勤次數。", null, option.Item2);

            void Add(string label, RaidOrder order, int limit)
            {
                string blocked = state.RaidBlock(order);
                if (blocked != null)
                {
                    lines.Add($"{label}｜{blocked}");
                    return;
                }
                buttons.Add((label + " ›", () => ui.ChooseAssignment(state, label, "campaign", limit, party =>
                {
                    string result = state.RunRaidOrder(order, party);
                    ui.Refresh(state);
                    ShowRaidDesk();
                    ui.Message(result);
                }, ShowRaidDesk, id => state.RaidChance(order, id))));
            }
        }

        private void StartDefensiveCampaign()
        {
            if (!state.RaidReady || campaignOpen) return;
            var raid = state.Story.sectRaid;
            var foe = GameState.FindFaction(raid.faction);
            var guards = (raid.guards ?? Array.Empty<string>()).Where(id => id == GameState.HeroId
                ? state.Protagonist.injury < 3
                : state.Disciples.Any(member => member.id == id && member.injury < 3 && id != state.Story.delegateId)).ToArray();
            guards = guards.Where(id => !state.IsDispatched(id)).ToArray();
            if (guards.Length == 0)
                guards = (state.Protagonist.injury < 3 ? new[] { GameState.HeroId } : Array.Empty<string>())
                    .Concat(state.Disciples.Where(member => member.injury < 3
                    && member.id != state.Story.delegateId).Select(member => member.id))
                    .Where(id => !state.IsDispatched(id)).Take(4).ToArray();
            if (guards.Length == 0) { ui.Message(state.FinishRaid(false, "山中無將可出戰。")); ShowRaidDesk(); return; }
            ui.CloseModal(force: true);
            OpenCampaign(new SectCampaign(state, guards, foe, raid.troops, raid),
                $"門派戰役　{state.WarNode(raid.target)}守備", foe, 0, () => state.RaidReady, won =>
                {
                    if (won) foreach (string id in guards) AssignmentSystem.Credit(state, id, 2);
                    ui.Message(state.FinishRaid(won));
                    if (!capturing) state.Save();
                }, ShowRaidDesk);
        }

        private void StartOffensiveCampaign(MapNode node, MapFaction foe, Action<string> finished)
        {
            if (campaignOpen) return;
            if (state.CampaignBlock(node, foe) is string blocked) { finished(blocked); return; }
            ui.ChooseCampaignAssignment(state, $"調兵攻取{node.Label}", party =>
            {
                int troops = Mathf.Clamp(foe.disciples / 2, 200, 3200);
                OpenCampaign(new SectCampaign(state, party, foe, troops, support: state.CampaignSupport),
                    $"門派戰役　{node.Label}攻防", foe, 2,
                    () => state.CampaignBlock(node, foe) == null
                        && AssignmentSystem.Commit(state, party) && state.TrySpendAp(2, "campaign:" + node.id), won =>
                    {
                        if (won)
                        {
                            state.Story.heldNodes = (state.Story.heldNodes ?? Array.Empty<string>()).Append(node.id).ToArray();
                            state.ApplyEffects(new[] { new GameEffect { key = "relation", @ref = foe.id, value = -25 } });
                            state.Apply(new Yield { reputation = 6, silver = 30, food = 20 });
                            foreach (string id in party) AssignmentSystem.Credit(state, id, 3);
                        }
                        else state.Apply(new Yield { reputation = -2 });
                        finished(won ? $"攻取{node.Label}。銀兩＋30、糧食＋20、聲望＋6。" : $"攻取{node.Label}失利，聲望 −2。");
                    }, null);
            });
        }

        private void OpenCampaign(SectCampaign battle, string title, MapFaction faction, int apCost, Func<bool> begin,
            Action<bool> finish, Action back)
        {
            campaignOpen = true;
            controls.enabled = false;
            void Close()
            {
                campaignOpen = false;
                controls.enabled = map == null || !map.IsOpen;
            }
            var layer = ui.Canvas;
            int previousOrder = layer.sortingOrder;
            layer.sortingOrder = 60;
            ui.ShowCampaign(battle, title, this, state, faction, apCost, begin, won =>
            {
                layer.sortingOrder = previousOrder;
                Close();
                finish(won);
                ui.Refresh(state);
                ShowCommands();
            }, () => { layer.sortingOrder = previousOrder; Close(); back?.Invoke(); });
        }
    }
}
