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

            // Close-up: protagonist (look from creation), a recruit-style random look and a story character.
            Vector3[] points = { new(-1.4f,0,-8.7f),new(0,0,-8.3f),new(1.4f,0,-8.7f) };
            var walkers = FindObjectsByType<DiscipleWalker>();
            foreach (var walker in walkers) walker.gameObject.SetActive(false);
            var looks = new[]
            {
                state.Disciples.Count > 0 ? CharacterFactory.ForDisciple(state.Disciples[0],0) : CharacterFactory.ForRecruit("女","入門弟子"),
                ProtagonistFigure().look,
                CharacterFactory.ForStory("master")
            };
            for (int i=0;i<looks.Length;i++)
            {
                var figure = CharacterFactory.Build(looks[i]);
                figure.transform.SetPositionAndRotation(points[i],Quaternion.Euler(0,(i-1)*17,0));
                figure.transform.localScale *= 1.42f;
            }
            view.transform.position = new Vector3(.05f,2.25f,-4.4f);
            view.transform.LookAt(new Vector3(0,1.28f,-8.6f));
            view.fieldOfView = 41;
            var bounce = new GameObject("Capture fill").AddComponent<Light>();
            bounce.type = LightType.Directional;
            bounce.color = new Color(1f,.93f,.84f);
            bounce.intensity = .50f;
            bounce.transform.rotation = Quaternion.Euler(23,156,0);
            yield return null;
            capture("23_characters_closeup.png");
            ui.Canvas.enabled = true;
        }
    }
}
