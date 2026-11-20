using UnityEngine;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private WorldMapScreen map;
        private IWorldMapHost mapHost;

        private void ToggleWorldMap()
        {
            if (map != null && map.IsOpen)
            {
                map.Close();
                return;
            }
            if (ui.ModalOpen || !courtyardReady) return;
            OpenWorldMap();
        }

        private void OpenWorldMap()
        {
            if (map == null)
            {
                map = gameObject.AddComponent<WorldMapScreen>();
                map.Closed += () => controls.enabled = true;
            }
            mapHost ??= new LocalWorldMapHost();
            map.Open(mapHost);
            controls.enabled = false;
        }

        private bool HandleWorldMapInput()
        {
            if (Input.GetKeyDown(KeyCode.M) || (map != null && map.IsOpen && Input.GetKeyDown(KeyCode.Escape)))
            {
                if (Input.GetKeyDown(KeyCode.M) || !(map?.CloseIntel() ?? false)) ToggleWorldMap();
                return true;
            }
            return map != null && map.IsOpen;
        }

        private void PlayBuildAudio()
        {
            AudioManager.Play("place");
            Invoke(nameof(PlayCompletionAudio), .36f);
        }

        private void PlayCompletionAudio() => AudioManager.Play("complete");
    }
}
