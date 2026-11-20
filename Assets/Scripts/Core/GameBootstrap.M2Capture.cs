using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        /// <summary>Shots 20–23: world map, faction card, intel overview and a character close-up.</summary>
        private IEnumerator CaptureM2(Action<string> capture)
        {
            ui.Canvas.enabled = false;
            var cameraData = view.GetUniversalAdditionalCameraData();
            bool post = cameraData.renderPostProcessing;
            cameraData.renderPostProcessing = false;
            OpenWorldMap();
            var overlay = map.CaptureCanvas;
            overlay.renderMode = RenderMode.ScreenSpaceCamera;
            overlay.worldCamera = view;
            overlay.planeDistance = view.nearClipPlane + .11f;
            yield return null;
            capture("20_world_map.png");

            var mapData = WorldMapData.Load();
            var faction = mapData.Factions.Where(f => state.IsFactionKnown(f.id) && mapData.Nodes.ContainsKey(f.seat ?? ""))
                .OrderByDescending(f => f.reputation).FirstOrDefault();
            if (faction != null) map.FocusFaction(faction);
            yield return null;
            capture("21_world_map_card.png");

            map.CaptureIntel();
            yield return null;
            capture("22_intel_overview.png");

            // Exercise scouting and travel on a throwaway state so the real capture state stays untouched.
            var check = new GameState(data);
            check.Story.mapNode = state.Story.mapNode;
            yield return map.VerifyCaptureInteractions(check);
            map.Open(state);
            map.Close();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            controls.enabled = false;
            cameraData.renderPostProcessing = post;

            var walkers = FindObjectsByType<DiscipleWalker>();
            foreach (var walker in walkers) walker.gameObject.SetActive(false);
            var looks = new[]
            {
                CharacterFactory.ForStory("master"),
                new CharacterLook { gender = "男", robeColor = "#536775", sashColor = "#754339", hairStyle = "topknot" },
                new CharacterLook { gender = "女", hairStyle = "twinbuns",
                    model = new CharacterModelSpec { style = "ruqun", robe = "#8D817B", robe2 = "#61766D", sash = "#725458" } },
                CharacterFactory.ForStory("warlord")
            };
            var figures = new GameObject[looks.Length];
            for (int i = 0; i < looks.Length; i++)
            {
                figures[i] = CharacterFactory.Build(looks[i]);
                figures[i].transform.SetPositionAndRotation(new Vector3((i - 1.5f) * 1.03f, 0, -8.65f),
                    Quaternion.Euler(0, (1.5f - i) * 18, 0));
            }
            view.transform.position = new Vector3(0, 1.57f, -5.61f);
            view.transform.LookAt(new Vector3(0, .94f, -8.65f));
            view.fieldOfView = 42;
            var bounce = new GameObject("Capture fill").AddComponent<Light>();
            bounce.type = LightType.Directional;
            bounce.color = new Color(1f, .96f, .90f);
            bounce.intensity = .85f;
            bounce.transform.rotation = Quaternion.Euler(21, 158, 0);
            yield return null;
            capture("23_characters_closeup.png");
            foreach (var figure in figures) Destroy(figure);
            for (int i = 0; i < walkers.Length; i++)
            {
                var walker = walkers[i];
                walker.gameObject.SetActive(true);
                Vector3 start = new Vector3((i % 4 - 1.5f) * 1.55f, 0, -9.5f - i / 4 * 3.4f);
                walker.WalkForCapture(start, start + new Vector3(1.8f, 0, 6f));
            }
            view.transform.position = new Vector3(-.2f, 2.22f, -3.2f);
            view.transform.LookAt(new Vector3(.42f, 1.22f, -9.2f));
            view.fieldOfView = 45;
            yield return new WaitForSecondsRealtime(.52f);
            capture("25_courtyard_people.png");
            Destroy(bounce.gameObject);
            ui.Canvas.enabled = true;
        }
    }
}
