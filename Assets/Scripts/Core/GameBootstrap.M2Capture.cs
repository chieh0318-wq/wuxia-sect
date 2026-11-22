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

            map.CapturePeopleIntel();
            yield return null;
            capture("32_intel_martial_artists.png");

            if (faction != null)
            {
                map.CloseIntel();
                map.CaptureStronghold(faction);
                yield return null;
                capture("31_stronghold.png");
            }

            // M6: an outlaw stronghold card with its 【草莽據點・易攻】 badge and 率眾攻討 command.
            if (map.CaptureOutlaw())
            {
                yield return null;
                capture("46_m6_outlaw_stronghold.png");
            }

            // Exercise scouting and travel on a throwaway state so the real capture state stays untouched.
            var check = new GameState(data);
            check.Story.mapNode = state.Story.mapNode;
            map.VerifyM6Map(check);
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
            Destroy(bounce.gameObject);

            // Use the same fixed angle as gameplay, with the walkers passing through the frame.
            controls.Frame(new Vector2(0, -9.9f), 9.4f);
            view.fieldOfView = 45;
            for (int i = 0; i < walkers.Length; i++)
            {
                var walker = walkers[i];
                walker.gameObject.SetActive(true);
                Vector3 start = new Vector3((i % 4 - 1.5f) * 2.25f, 0, -12.5f + i / 4 * 3.4f);
                walker.WalkForCapture(start, start + new Vector3(.65f, 0, 3f));
            }
            yield return new WaitForSecondsRealtime(.35f);
            ui.Canvas.enabled = true;
            capture("25_courtyard_people.png");
        }
    }
}
