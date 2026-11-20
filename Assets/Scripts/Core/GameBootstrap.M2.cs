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
            ui.ShowModal("旅途中的門派月報", lines, closed: ShowRequestedLeader);
        }

        private bool HandleWorldMapInput()
        {
            bool open = map != null && map.IsOpen;
            if (Input.GetKeyDown(KeyCode.M) && (open || !ui.ModalOpen))
            {
                ToggleWorldMap();
                return true;
            }
            if (open && Input.GetKeyDown(KeyCode.Escape))
            {
                if (!map.CloseIntel()) map.Close();
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
    }
}
