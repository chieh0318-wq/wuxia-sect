using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private static Vector2Int CaptureResolution()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args,"-wuxiaCaptureRes");
            return i >= 0 && i+1 < args.Length && args[i+1] == "1280x720"
                ? new Vector2Int(1280,720) : new Vector2Int(1920,1080);
        }

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

            var data = WorldMapData.Load();
            var faction = data.Factions.Where(f => mapHost.IsFactionKnown(f.id) && data.Nodes.ContainsKey(f.seat ?? ""))
                .OrderByDescending(f => f.reputation).FirstOrDefault();
            if (faction != null) map.FocusFaction(faction);
            yield return null;
            capture("21_world_map_card.png");

            map.CaptureIntel();
            yield return null;
            capture("22_intel_overview.png");
            yield return map.VerifyCaptureInteractions(new LocalWorldMapHost());
            map.Close();
            controls.enabled = false;
            cameraData.renderPostProcessing = post;

            Vector3[] points = { new(-1.4f,0,-8.7f),new(0,0,-8.3f),new(1.4f,0,-8.7f) };
            var walkers = FindObjectsByType<DiscipleWalker>(FindObjectsSortMode.None);
            foreach (var walker in walkers) walker.gameObject.SetActive(false);
            for (int i=0;i<Mathf.Min(3,state.Disciples.Count);i++)
            {
                var figure = CharacterFactory.Build(CharacterFactory.ForDisciple(state.Disciples[i],i));
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
        }
    }
}
