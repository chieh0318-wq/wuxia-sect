using System;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Wuxia
{
    internal static class RuntimeEntry
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (UnityEngine.Object.FindAnyObjectByType<GameBootstrap>() == null)
                new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
        }
    }

    public sealed partial class GameBootstrap : MonoBehaviour
    {
        private GameState state;
        private SectUI ui;
        private Courtyard world;
        private Camera view;
        private ActionData buildAction;
        private BuildingData buildChoice;
        private bool choosingBuilding;
        private TutorialGuide tutorial;
        private CameraController controls;
        private bool courtyardReady;

        private void Awake()
        {
            BootStep("scene", ConfigureScene);
            BootStep("HUD", () => ui = new SectUI(EndMonth));
            BootStep("data", () => state = new GameState(GameData.Load()));
            if (ui == null || state == null)
                return;
            ShowCommands();
            ui.Refresh(state);
            courtyardReady = BootStep("courtyard", BuildCourtyard);
            if (!courtyardReady)
            {
                ui.Message("庭院載入失敗，請查看遊戲記錄檔。");
                return;
            }
            BootStep("tutorial", () => tutorial = new TutorialGuide(ui, state));
            if (controls != null)
                controls.Changed += trigger => tutorial?.Record(trigger);
            string[] args = Environment.GetCommandLineArgs();
            int capture = Array.IndexOf(args, "-wuxiaCapture");
            if (capture >= 0 && capture + 1 < args.Length)
                StartCoroutine(Capture(args[capture + 1]));
        }

        private static bool BootStep(string name, Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception error)
            {
                Debug.LogError($"[Boot] {name} failed: {error}");
                return false;
            }
        }

        private void BuildCourtyard()
        {
            world = new Courtyard(transform, ui, state.Data.Start.slots);
            for (int index = 0; index < state.Buildings.Length; index++)
            {
                if (state.Buildings[index] != null)
                    world.PlaceBuilding(index, state.Buildings[index]);
            }
            foreach (var disciple in state.Disciples)
                world.SpawnDisciple(disciple);
        }

        private void ConfigureScene()
        {
            view = Camera.main;
            if (view == null)
                view = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (view == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                view = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(.66f, .73f, .77f);
            var skyShader = Resources.Load<Shader>("MountainSky");
            if (skyShader != null)
            {
                mountainSky = new Material(skyShader);
                RenderSettings.skybox = mountainSky;
                view.clearFlags = CameraClearFlags.Skybox;
            }
            view.orthographic = false;
            view.fieldOfView = 54f;
            view.allowHDR = true;
            controls = view.GetComponent<CameraController>();
            if (controls == null)
                controls = view.gameObject.AddComponent<CameraController>();
            var cameraData = view.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.55f, .60f, .67f);
            RenderSettings.ambientEquatorColor = new Color(.45f, .50f, .57f);
            RenderSettings.ambientGroundColor = new Color(.31f, .32f, .34f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 55;
            RenderSettings.fogEndDistance = 190;
            RenderSettings.fogColor = view.backgroundColor;
            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = Resources.Load<VolumeProfile>("CourtyardVolume");

            if (UnityEngine.Object.FindAnyObjectByType<Light>() == null)
            {
                var sunObject = new GameObject("Courtyard Sun");
                sunObject.transform.SetParent(transform, false);
                var sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = new Color(1f, 0.94f, 0.81f);
                sun.intensity = 1.35f;
                RenderSettings.sun = sun;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = .78f;
                sun.shadowBias = .025f;
                sun.shadowNormalBias = .22f;
                sun.transform.rotation = Quaternion.Euler(47f, -37f, 0f);
            }

            var events = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (events == null)
            {
                var eventObject = new GameObject("EventSystem");
                eventObject.transform.SetParent(transform, false);
                events = eventObject.AddComponent<EventSystem>();
            }
            if (events.GetComponent<StandaloneInputModule>() == null)
                events.gameObject.AddComponent<StandaloneInputModule>();
        }

        private void LateUpdate()
        {
            if (ui == null || view == null || !courtyardReady)
                return;
            ui.PositionLabels(view);
            ui.PositionHighlight(tutorial?.Highlight, view, world);
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (ui.ModalOpen)
                    ui.CloseModal();
                else if (choosingBuilding)
                    CancelBuild();
                else
                {
                    ui.CloseDialogue();
                    ui.CloseBuilding();
                }
                return;
            }

            if (ui.ModalOpen)
                return;
            if (choosingBuilding && Input.GetMouseButtonDown(1))
            {
                CancelBuild();
                return;
            }
            if (!Input.GetMouseButtonDown(0))
                return;

            Vector3 mouse = Input.mousePosition;
            if (ui.DialogueOpen && !ui.IsInDialogue(mouse))
                ui.CloseDialogue();
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            Physics.SyncTransforms();
            if (Physics.Raycast(view.ScreenPointToRay(mouse), out var hit))
            {
                if (world.TryGetSlot(hit.collider, out int index))
                {
                    if (buildChoice != null)
                        PlaceBuilding(index);
                    else if (state.Buildings[index] != null)
                    {
                        ShowBuilding(state.Buildings[index]);
                        tutorial?.Record("click_building", state.Buildings[index].id);
                    }
                    else
                        ui.CloseBuilding();
                    return;
                }
                if (!choosingBuilding && world.TryGetDisciple(hit.collider, out var disciple))
                {
                    Speak(disciple);
                    return;
                }
            }

            ui.CloseBuilding();
        }

        private Material mountainSky;

        private void OnDestroy()
        {
            ui?.Destroy();
            world?.Destroy();
            Destroy(mountainSky);
        }

        private void ShowCommands()
        {
            var commands = new List<(string, System.Action)>();
            foreach (string type in new[] { "build", "recruit" })
            {
                var action = state.Data.Actions.FirstOrDefault(entry => entry.type == type);
                if (action != null)
                    commands.Add((ActionLabel(action), () => RunAction(action)));
            }
            commands.Add(("查看門派", ShowSect));
            ui.ShowCommands("指令", commands);
        }

        private void ShowBuilding(BuildingData building)
        {
            var actions = new List<(string, System.Action)>();
            foreach (string id in building.actions)
            {
                var action = state.Data.Actions.FirstOrDefault(entry => entry.id == id);
                if (action != null)
                    actions.Add((ActionLabel(action), () => RunAction(action)));
            }
            ui.ShowBuilding(building, actions);
        }

        private void RunAction(ActionData action)
        {
            if (action.type == "build")
            {
                if (courtyardReady)
                    BeginBuild(action);
                return;
            }

            int extraSilver = action.type == "recruit" ? state.Data.Start.recruitCost : 0;
            string grant = "";

            DiscipleData recruit = null;
            if (action.type == "recruit")
            {
                recruit = state.Data.Disciples.FirstOrDefault(d => !state.Disciples.Contains(d));
                if (recruit == null)
                {
                    ui.Message("目前沒有尚未入門的弟子。");
                    return;
                }
            }

            if (recruit != null)
                grant = tutorial?.Fund("recruit", action.cost, extraSilver) ?? "";
            if (!state.CanPay(action.cost, extraSilver))
            {
                ui.Message("銀兩或糧食不足。");
                return;
            }

            state.Pay(action.cost, extraSilver);
            state.Apply(action.effect);
            if (recruit != null)
            {
                state.Disciples.Add(recruit);
                world?.SpawnDisciple(recruit);
            }
            ui.Refresh(state);
            string message = $"{action.name}完成。{Changes(action.effect)}";
            if (recruit != null)
                message = $"{recruit.name}已加入{state.Data.Start.sectName}。{Changes(action.effect)}";
            if (action.id == "lodge")
                message = $"已安頓弟子。{HousingInfo()}　{Changes(action.effect)}".TrimEnd();
            ui.Message(grant + message);
            if (recruit != null)
                tutorial?.Record("recruit");
        }

        private void BeginBuild(ActionData action)
        {
            if (state.Buildings.All(building => building != null))
            {
                ui.Message("院中已無空地。");
                return;
            }

            choosingBuilding = true;
            buildAction = action;
            buildChoice = null;
            world.HighlightPlots(false);
            ui.CloseBuilding();
            ui.CloseDialogue();
            ui.Message("選一種建築，再選院中的空地。");

            var options = new List<(string, System.Action)> { ("返回指令", CancelBuild) };
            foreach (var building in state.Data.Buildings.Where(state.IsAvailable))
            {
                string cost = CostText(building.cost.silver + action.cost.silver,
                    building.cost.food + action.cost.food);
                options.Add(($"{building.name}　{cost}", () =>
                {
                    buildChoice = building;
                    world.HighlightPlots(true);
                    ui.Message($"準備建造{building.name}。請選擇綠色空地。");
                }));
            }
            ui.ShowCommands("建造", options);
        }

        private void PlaceBuilding(int index)
        {
            if (state.Buildings[index] != null)
            {
                ui.Message("此處已有建築。請選擇綠色空地。");
                return;
            }
            if (!state.IsAvailable(buildChoice))
            {
                ui.Message("此建築只能建造一棟。");
                return;
            }

            var total = new Cost
            {
                silver = buildChoice.cost.silver + buildAction.cost.silver,
                food = buildChoice.cost.food + buildAction.cost.food
            };
            string grant = tutorial?.Fund("build_any", total) ?? "";
            if (!state.CanPay(total))
            {
                ui.Message("銀兩或糧食不足。");
                return;
            }

            state.Pay(total);
            state.Apply(buildAction.effect);
            state.Buildings[index] = buildChoice;
            world.PlaceBuilding(index, buildChoice);
            string result = $"{buildChoice.name}已落成。{Changes(buildAction.effect)}";
            CancelBuild();
            ui.Refresh(state);
            ui.Message(grant + result);
            tutorial?.Record("build_any");
        }

        private void CancelBuild()
        {
            choosingBuilding = false;
            buildChoice = null;
            buildAction = null;
            world.HighlightPlots(false);
            ui.Message("");
            ShowCommands();
        }

        private void Speak(DiscipleData disciple)
        {
            ui.CloseBuilding();
            var lines = (disciple.lines ?? System.Array.Empty<string>())
                .Concat(state.Data.GeneralLines).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
            string text = lines.Length == 0 ? "請吩咐。" :
                lines[UnityEngine.Random.Range(0, lines.Length)];
            ui.ShowDialogue(disciple, text);
            tutorial?.Record("talk_disciple");
        }

        private void ShowSect()
        {
            ui.ShowModal(state.Data.Start.sectName, new[]
            {
                state.Date, HousingInfo(),
                $"院中建築　{state.Buildings.Count(b => b != null)}／{state.Buildings.Length}",
                $"弟子　{string.Join("、", state.Disciples.Select(d => d.name))}"
            });
        }

        private string HousingInfo()
        {
            int rooms = state.Buildings.Count(b => b != null && b.actions.Contains("lodge"));
            return $"床位 {rooms * 4}　弟子 {state.Disciples.Count}";
        }

        private void EndMonth()
        {
            if (state == null)
                return;
            if (choosingBuilding)
                CancelBuild();
            var report = state.EndMonth();
            ui.Refresh(state);
            tutorial?.Record("end_month");
            ui.ShowModal("門派月報", report.Lines, closed: () => tutorial?.Record("read_report"));
        }

        private string ActionLabel(ActionData action)
        {
            int silver = action.cost.silver + (action.type == "recruit" ? state.Data.Start.recruitCost : 0);
            return silver == 0 && action.cost.food == 0 ? action.name :
                $"{action.name}　{CostText(silver, action.cost.food)}";
        }

        private static string CostText(int silver, int food) =>
            food == 0 ? $"銀 {silver}" : $"銀 {silver}／糧 {food}";

        internal static string Changes(Yield effect)
        {
            var changes = new List<string>();
            if (effect.silver != 0) changes.Add($"銀兩 {effect.silver:+0;-0}");
            if (effect.food != 0) changes.Add($"糧食 {effect.food:+0;-0}");
            if (effect.reputation != 0) changes.Add($"聲望 {effect.reputation:+0;-0}");
            return string.Join("、", changes);
        }
    }
}
