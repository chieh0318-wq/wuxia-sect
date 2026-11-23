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
            view.transform.position = new Vector3(0, 1.57f, -5.61f);
            view.transform.LookAt(new Vector3(0, .94f, -8.65f));
            view.fieldOfView = 42;
            var hero = state.Protagonist;
            var people = new[] {
                new DiscipleData { id = ProtagonistFigureId, portrait = hero.portrait ?? hero.lookId ?? (hero.gender == "女" ? "hero_f1" : "hero_m1"),
                    gender = hero.gender },
                new DiscipleData { id = "lin", gender = "男" },
                new DiscipleData { id = "xu", gender = "女" },
                new DiscipleData { id = "master", gender = "男" }
            };
            var figures = new GameObject[people.Length];
            for (int i = 0; i < people.Length; i++)
            {
                figures[i] = new GameObject("Close-up standee");
                figures[i].transform.position = world.Grounded(new Vector3((i - 1.5f) * 1.03f, 0, -8.65f));
                CharacterFactory.BuildStandee(people[i], figures[i].transform, out _, view.transform.rotation,
                    i == 0 ? CharacterFactory.HeroOriginStandee(hero.gender, hero.lookId ?? hero.portrait,
                        hero.backgroundId, hero.title) : null);
            }
            yield return null;
            capture("23_characters_closeup.png");
            foreach (var figure in figures) Destroy(figure);

            // Use the same fixed angle as gameplay, with the walkers passing through the frame.
            controls.Frame(new Vector2(0, -11.4f), 11f);
            view.fieldOfView = 45;
            var starts = new Vector3[walkers.Length];
            for (int i = 0; i < walkers.Length; i++)
            {
                var walker = walkers[i];
                walker.gameObject.SetActive(true);
                Vector3 start = new Vector3((i % 4 - 1.5f) * 2.45f + .3f, 0, -15.3f + i / 4 * 1.45f);
                float direction = i / 4 % 2 == 0 ? 1 : -1;
                walker.WalkForCapture(start, start + Vector3.right * direction * 2);
                starts[i] = walker.transform.position;
            }
            yield return new WaitForSecondsRealtime(.35f);
            ui.Canvas.enabled = true;
            capture("25_courtyard_people.png");
            int left = 0, right = 0;
            for (int i = 0; i < walkers.Length; i++)
            {
                float screenX = Vector3.Dot(walkers[i].transform.position - starts[i], view.transform.right);
                if (screenX < -.05f) left++;
                if (screenX > .05f) right++;
            }
            Debug.Log($"[Capture] Courtyard travel: left={left}, right={right}.");
            capture("67_courtyard_walk_facing.png");
        }
    }
}
