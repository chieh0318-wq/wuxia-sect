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
                map.CampaignRequested += StartOffensiveCampaign;
                map.BattleRequested += StartMapAssaultBattle;
                map.TreasureRequested += EnterTreasure; // v1124 C16
                map.MasterChallengeRequested += StartMasterChallenge; // v1126b 決定 3
            }
            state.ChoosePatronEnvoy = (title, faction, send) => ui.ChoosePatronEnvoy(state, title, faction, send);
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
            // v1126d CF2 #21: the foot shows the summed month-end net instead of a placeholder.
            var net = reports.All(report => report.Net.HasValue)
                ? (reports.Sum(report => report.Net.Value.silver), reports.Sum(report => report.Net.Value.food),
                    reports.Sum(report => report.Net.Value.reputation))
                : ((int, int, int)?)null;
            ui.ShowMonthScroll("旅途中的門派月報", lines, net, () =>
            {
                if (state.FactionWorld.envoy != null) ShowFactionEnvoy();
                else ShowRequestedLeader();
            });
        }

        private bool HandleWorldMapInput()
        {
            bool open = map != null && map.IsOpen;
            if (ui.BattleOpen || campaignOpen)
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
            if (!capturing && state.ChooseMapParty != null)
            {
                state.ChooseMapParty("率眾攻討", party =>
                {
                    if (state.ActionPoints < 2 || !AssignmentSystem.Commit(state, party)) return;
                    encounter.expeditionParty = party;
                    StartAssignedAssault(encounter, done);
                });
                return;
            }
            StartAssignedAssault(encounter, done);
        }

        private void StartAssignedAssault(BattleRequest encounter, System.Action<bool> done)
        {
            encounter.onComplete = won =>
            {
                if (won)
                    foreach (string member in encounter.expeditionParty ?? System.Array.Empty<string>())
                        AssignmentSystem.Credit(state, member, 2);
                done?.Invoke(won);
                ui.Refresh(state);
            };
            ui.StartTurnBattle(state, encounter, this);
        }

        /// <summary>v1126b 決定 3 天下第一: the protagonist alone against a master, who fights with their own arts.</summary>
        private void StartMasterChallenge(MapFaction master, System.Action<string> done)
        {
            if (state == null || ui.BattleOpen || !state.BeginMasterChallenge(master)) return;
            ui.StartTurnBattle(state, new BattleRequest
            {
                encounter = BattleEncounter.Boss, ground = BattleGround.MountainGate,
                expeditionParty = new[] { GameState.HeroId }, enemyCount = 1,
                enemyLeadName = master.leader, enemyLeadPortrait = master.PortraitId, leadAsStandee = true,
                commanderDuel = true, difficulty = state.MasterDifficulty(master),
                onComplete = won =>
                {
                    string result = state.ResolveMasterChallenge(master, won);
                    done?.Invoke(result);
                    ui.Refresh(state);
                }
            }, this);
        }
    }
}
