using System.Linq;
using UnityEngine;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private WorldMapScreen map;

        private void ToggleWorldMap() => ToggleWorldMap(false);

        private void ToggleWorldMap(bool intel)
        {
            if (map != null && map.IsOpen)
            {
                map.Close();
                return;
            }
            if (ui.ModalOpen || !courtyardReady || state == null) return;
            if (choosingBuilding) CancelBuild();
            ui.CloseDialogue();
            ui.CloseBuilding();
            OpenWorldMap();
            if (intel) map.OpenIntelOverview();
        }

        private void OpenWorldMap()
        {
            if (map == null)
            {
                map = gameObject.AddComponent<WorldMapScreen>();
                map.Closed += OnWorldMapClosed;
                map.LeaderSelected += OpenMapLeader;
                map.BattleRequested += StartMapAssaultBattle;
                map.TreasureRequested += EnterTreasure; // v1124 C16
            }
            map.Open(state);
            controls.enabled = false;
        }

        private void OnWorldMapClosed()
        {
            if (controls != null) controls.enabled = true;
            if (state == null) return;
            ui.Refresh(state);
            ShowCommands();
            if (!capturing) state.EvaluateQuestlines();
            var reports = state.PendingReports.ToList();
            state.PendingReports.Clear();
            if (reports.Count == 0)
            {
                if (!capturing) state.Save();
                return;
            }
            // Travel crossed one or more month ends: show the combined month report(s), then autosave.
            AudioManager.Play("month");
            candidates = null;
            var lines = reports.SelectMany(report => report.Lines).ToList();
            if (!capturing && !state.Save())
                lines.Add("自動存檔失敗。");
            // v1125 #20: travel month-ends use the same report scroll.
            ui.ShowMonthScroll("旅途中的門派月報", lines, null, ShowRequestedLeader);
        }

        private bool HandleWorldMapInput()
        {
            bool open = map != null && map.IsOpen;
            if (ui.BattleOpen)
            {
                if (UiBack.BackPressed) UiBack.TryBack(); // v1125 #16: battle registers "cancel the armed move"
                return true;
            }
            if (Input.GetKeyDown(KeyCode.M) && (open || !ui.ModalOpen))
            {
                ToggleWorldMap();
                return true;
            }
            if (open && UiBack.BackPressed)
            {
                // v1125 #16/#19: back closes intel / diplomacy (intel opened from the toolbar returns to the sect),
                // then the open stronghold card, then the map itself.
                if (UiBack.TryBack()) return true;
                UiBack.Consume();
                map.Back();
                return true;
            }
            return open;
        }

        private void PlayBuildAudio()
        {
            AudioManager.Play("place");
            Invoke(nameof(PlayCompletionAudio), .36f);
        }

        private void PlayCompletionAudio() => AudioManager.Play("complete");

        private void StartSkirmishBattle()
        {
            if (state == null || ui.BattleOpen) return;
            if (choosingBuilding) CancelBuild();
            ui.CloseBuilding();
            ui.CloseDialogue();
            if (controls != null) controls.enabled = false;
            ui.StartTurnBattle(state, new BattleRequest
            {
                ground = BattleGround.MountainGate,
                enemyLeadName = "黑風山賊頭目",
                enemyCount = 3,
                difficulty = 0.95f,
                onComplete = won =>
                {
                    if (controls != null) controls.enabled = true;
                    if (won)
                    {
                        state.Apply(new Yield { silver = 18, reputation = 2 });
                        ui.Refresh(state);
                        ShowCommands();
                        ui.Message("山門演武凱旋！獲得 銀兩 +18、聲望 +2、先輩積分 +10。");
                    }
                    else
                    {
                        ui.Refresh(state);
                        ShowCommands();
                        ui.Message("切磋點到為止，眾弟子收劍回院休整。");
                    }
                }
            }, this);
        }

        private void StartMapAssaultBattle(BattleRequest encounter, System.Action<bool> done)
        {
            if (state == null || ui.BattleOpen) return;
            encounter.onComplete = won =>
            {
                done?.Invoke(won);
                ui.Refresh(state);
            };
            ui.StartTurnBattle(state, encounter, this);
        }
    }
}
