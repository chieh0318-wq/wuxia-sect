using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private (string filename, Action show)[] V1125PdCaptureSteps() => new (string, Action)[]
        {
            ("D01_assignment.png", () =>
            {
                CleanupV1125PaCapture();
                EnsureCaptureCourtyard();
                state.RefillAp();
                state.Apply(new Yield { silver = 100, food = 50, materials = 50 });
                var forge = data.Actions.First(action => action.id == "forge_gear");
                RunAction(forge);
                if (!ui.ModalOpen) throw new InvalidOperationException("Forge did not open assignment.");
            }),
            ("D02_expedition_party.png", () =>
            {
                ui.CloseModal(force: true);
                RunAction(data.Actions.First(action => action.id == "explore"));
                if (!ui.ModalOpen) throw new InvalidOperationException("Expedition did not open assignment.");
            }),
            ("D03_rank_ladder.png", () => ui.ShowRankLadder(state, null)),
            ("D04_sect_expansion.png", ShowSectExpansion),
            ("D05_sect_goals.png", () => ui.ShowSectGoals(state, null)),
            ("D06_attributes_with_gear.png", () =>
            {
                state.Protagonist.attrs[AttributeSystem.Index("con")] = AttributeSystem.Max;
                state.EquipOrStore(GameState.MakeGear("護腕", 3));
                ShowProtagonist();
            }),
            ("D07_personal_moves.png", () =>
            {
                ui.CloseModal(force: true);
                foreach (string id in new[] { "kaibei_shou", "hangu_ziqi", "qingping" })
                    state.LearnArt(id);
                ui.StartTurnBattle(state, new BattleRequest { expeditionParty = new[] { GameState.HeroId } },
                    this, captureRound: _ => { });
                ClickCaptureCaption("›");
            }),
            ("D08_raid_desk.png", () =>
            {
                ui.CloseBattle();
                state.Story.sectRaid = new SectRaid
                {
                    faction = "tubo", target = state.StartNode, due = state.MonthStamp + 1,
                    troops = 610, detected = true, preparation = 9, aid = 350,
                    guards = new[] { GameState.HeroId }.Concat(state.Disciples.Take(3).Select(d => d.id)).ToArray()
                };
                ShowRaidDesk();
            }),
            ("D09_campaign_deploy.png", () =>
            {
                state.Story.sectRaid.due = state.MonthStamp;
                StartDefensiveCampaign();
            }),
            ("D10_campaign_battle.png", () =>
            {
                ClickCaptureCaption("開戰");
            }),
            ("D11_campaign_duel.png", () =>
            {
                ClickCaptureCaption("整軍");
                ClickCaptureCaption("主將對決 ›");
            }),
            ("D12_special_faction.png", () =>
            {
                ui.CloseBattle();
                ui.Canvas.transform.Find("SectCampaign").gameObject.SetActive(true);
                ClickCaptureCaption("撤兵");
                Toast.ClearAll();
                OpenWorldMap();
                var overlay = map.CaptureCanvas;
                overlay.renderMode = RenderMode.ScreenSpaceCamera;
                overlay.worldCamera = view;
                overlay.planeDistance = view.nearClipPlane + .11f;
                map.OpenDiplomacy(GameState.FindFaction("tang"));
            }),
        };

        private void ClickCaptureCaption(string caption)
        {
            var button = ui.Canvas.GetComponentsInChildren<Button>().LastOrDefault(candidate =>
                candidate.interactable && candidate.GetComponentInChildren<Text>()?.text == caption);
            if (button == null) throw new InvalidOperationException($"Capture button '{caption}' is unavailable.");
            button.onClick.Invoke();
        }
    }
}
