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
        private GameData data;
        private SectUI ui;
        private Courtyard world;
        private Camera view;
        private ActionData buildAction;
        private BuildingData buildChoice;
        private bool choosingBuilding;
        private TutorialGuide tutorial;
        private CameraController controls;
        private bool courtyardReady;
        private BuildingData openBuilding;

        private void Awake()
        {
            BootStep("scene", ConfigureScene);
            AudioManager.Initialize(); // M2-B audio
            BootStep("HUD", () => ui = new SectUI(EndMonth));
            BootStep("data", () => data = GameData.Load());
            if (ui == null || data == null)
                return;
            if (controls != null)
            {
                controls.Changed += trigger => tutorial?.Record(trigger);
                controls.enabled = false;
            }
            ui.ApInspected += () => tutorial?.Record("inspect_ap");
            ShowTitle();
            string[] args = Environment.GetCommandLineArgs();
            int capture = Array.IndexOf(args, "-wuxiaCapture");
            if (capture >= 0 && capture + 1 < args.Length)
                StartCoroutine(Capture(args[capture + 1]));
            // Smoke tests: "-wuxiaQuitAfter 20" exits cleanly after N seconds so no process has to be killed.
            int quit = Array.IndexOf(args, "-wuxiaQuitAfter");
            if (quit >= 0 && quit + 1 < args.Length && float.TryParse(args[quit + 1],
                    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float seconds))
                StartCoroutine(QuitAfter(seconds));
        }

        private static System.Collections.IEnumerator QuitAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(1, seconds));
            Debug.Log($"[Smoke] Quitting after {seconds:0} s.");
            Application.Quit(0);
        }

        private void ShowTitle()
        {
            ui.ShowTitle(data.Start.sectName, StartNewGame,
                GameState.HasSave ? ContinueGame : null);
        }

        private void ContinueGame()
        {
            GameState saved = GameState.LoadSave(data);
            if (saved == null)
            {
                ui.ShowModal("讀取存檔", new[] { "存檔已損毀或版本不符，無法讀取。" }, closed: ShowTitle);
                return;
            }
            EnterCourtyard(saved, false);
        }

        private void EnterCourtyard(GameState next, bool startTutorial)
        {
            state = next;
            ui.CloseTitle();
            ShowCommands();
            ui.Refresh(state);
            courtyardReady = BootStep("courtyard", BuildCourtyard);
            if (!courtyardReady)
            {
                ui.Message("庭院載入失敗，請查看遊戲記錄檔。");
                return;
            }
            if (controls != null)
                controls.enabled = true;
            AudioManager.Music("courtyard");
            ui.ShowMapButtons(ToggleWorldMap, () => ToggleWorldMap(true), ShowCharacterDirectory);
            if (startTutorial)
                BootStep("tutorial", () => tutorial = new TutorialGuide(ui, state));
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
            world.SpawnDisciple(ProtagonistFigure());
            foreach (var disciple in state.Disciples)
                world.SpawnDisciple(disciple);
        }

        private const string ProtagonistFigureId = "hero";

        /// <summary>A courtyard stand-in for the protagonist, wearing the look chosen at creation.</summary>
        private DiscipleData ProtagonistFigure()
        {
            var hero = state.Protagonist;
            var look = hero.look != null && (hero.look.model?.HasOverrides == true || !string.IsNullOrEmpty(hero.look.hairStyle)) ? hero.look :
                M2Data.Load<CharacterCatalog>("characters").protagonistLooks?.FirstOrDefault(entry => entry.id == hero.lookId);
            look ??= CharacterFactory.ForRecruit(hero.gender, hero.title);
            look.role = hero.title;
            return new DiscipleData
            {
                id = ProtagonistFigureId, name = hero.name, gender = hero.gender, title = hero.title,
                portrait = hero.portrait ?? hero.lookId, look = look, age = hero.age
            };
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
            if (!courtyardReady && ui != null)
            {
                ui.UpdateStoryBeat();
                ui.UpdateAppearance();
                if (Input.GetKeyDown(KeyCode.Escape) && ui.ModalOpen)
                    ui.CloseModal();
                return;
            }
            if (ui == null || view == null || !courtyardReady)
                return;
            if (ui.StoryOpen)
            {
                ui.UpdateStoryBeat();
                return;
            }
            if (HandleWorldMapInput()) return; // M2-B map input
            if (!capturing && !ui.ModalOpen && !ui.DialogueOpen && !choosingBuilding)
            {
                if (Input.GetKeyDown(KeyCode.C))
                {
                    ShowCharacterDirectory();
                    return;
                }
                state.EvaluateQuestlines();
                var quest = state.PendingQuest();
                if (quest.beat != null)
                {
                    PlayQuest(quest.line, quest.beat);
                    return;
                }
            }
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
                    ui.ShowAudioOptions();
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
                    if (disciple.id == ProtagonistFigureId)
                        ShowProtagonist();
                    else
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
                    commands.Add((ActionLabel(action), CanUseAp(action) ? () => RunAction(action) : null));
            }
            commands.Add(("查看門派", ShowSect));
            commands.Add(("主角", ShowProtagonist));
            var exploration = state.Data.Actions.FirstOrDefault(entry => entry.id == "explore");
            if (exploration != null)
                commands.Add((ActionLabel(exploration), CanUseAp(exploration) ? () => RunAction(exploration) : null));
            ui.ShowCommands("指令", commands);
        }

        private void ShowBuilding(BuildingData building)
        {
            openBuilding = building;
            var actions = new List<(string, System.Action)>();
            foreach (string id in building.actions)
            {
                var action = state.Data.Actions.FirstOrDefault(entry => entry.id == id);
                if (action != null)
                    actions.Add((ActionLabel(action), CanUseAp(action) ? () => RunAction(action) : null));
            }
            int level = state.BuildingLevel(building.id);
            if (building.id == "council" && level < 3)
            {
                int silver = state.BuildSilver(18 * level);
                bool mayUpgrade = state.ActionPoints >= 1 && state.Silver >= silver;
                actions.Add(($"擴建　行動力 1／銀 {silver}", mayUpgrade ? () =>
                {
                    state.UpgradeCouncil();
                    ui.Refresh(state);
                    ShowCommands();
                    ShowBuilding(building);
                } : null));
            }
            ui.ShowBuilding(building, actions, level);
        }

        private void RunAction(ActionData action)
        {
            if (!CanUseAp(action))
                return;
            if (action.type == "build")
            {
                if (courtyardReady)
                    BeginBuild(action);
                return;
            }

            if (action.type == "recruit")
            {
                OpenRecruitment(action);
                return;
            }

            if (!state.CanPay(action.cost))
            {
                ui.Message("銀兩或糧食不足。");
                return;
            }

            if (!SpendActionAp(action))
                return;
            state.Pay(action.cost);
            string breakthrough = action.id switch
            {
                "train" => state.TrainMartial(),
                "explore" => state.TryBreakthrough("qiyu_cliff_cave", state.Story.insightChance),
                _ => ""
            };
            state.Apply(action.effect);
            if (action.effect.silver > 0)
                AudioManager.Play("coin");
            if (action.id == "medicine")
                state.RecoverInjuries();
            if (action.id == "council" && state.BuildingLevel("council") >= 2)
                state.ApplyEffects(new[] { new GameEffect { key = "flag", @ref = "sect_records", value = 1 } });
            ui.Refresh(state);
            ShowCommands();
            if (openBuilding != null && ui.BuildingOpen)
                ShowBuilding(openBuilding);
            string message = $"{action.name}完成。{Changes(action.effect)}";
            if (action.id == "lodge")
                message = $"已安頓弟子。{HousingInfo()}　{Changes(action.effect)}".TrimEnd();
            ui.Message(string.IsNullOrEmpty(breakthrough) ? message : breakthrough);
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
                string cost = CostText(state.BuildSilver(building.cost.silver + action.cost.silver),
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
                silver = state.BuildSilver(buildChoice.cost.silver + buildAction.cost.silver),
                food = buildChoice.cost.food + buildAction.cost.food
            };
            string grant = tutorial?.Fund("build_any", total) ?? "";
            if (!state.CanPay(total))
            {
                ui.Message("銀兩或糧食不足。");
                return;
            }

            if (!SpendActionAp(buildAction))
                return;
            state.Pay(total);
            state.Apply(buildAction.effect);
            state.Buildings[index] = buildChoice;
            world.PlaceBuilding(index, buildChoice);
            PlayBuildAudio(); // M2-B placement and immediate M1 completion
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
            var action = state.Data.Actions.FirstOrDefault(entry => entry.id == "talk");
            if (action != null && !state.TrySpendAp(action.apCost, action.id))
            {
                ui.Message("行動力不足。");
                return;
            }
            if (action?.apCost > 0)
            {
                ShowCommands();
                ui.Refresh(state);
            }
            ui.CloseBuilding();
            var lines = (disciple.dialogue ?? System.Array.Empty<string>())
                .Concat(state.Data.GeneralLines).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
            string text = lines.Length == 0 ? "請吩咐。" :
                lines[UnityEngine.Random.Range(0, lines.Length)];
            if (state.Story.delegateId == disciple.id)
                text = "正在代行掌門門務。";
            ui.ShowDialogue(disciple, text, () => ShowDisciple(disciple));
            tutorial?.Record("talk_disciple");
        }

        private void ShowDisciple(DiscipleData disciple)
        {
            bool delegateRole = state.Story.delegateId == disciple.id;
            bool senior = disciple.age >= 20 && disciple.startInSect;
            ui.ShowCharacter(disciple.name, delegateRole ? "代掌門" : disciple.title, disciple.gender,
                disciple.portrait ?? disciple.id, disciple.attrs, disciple.lines, state.WeaponRankDelta(disciple.weapon),
                senior ? (delegateRole ? "解除代掌門" : "委任代掌門", () =>
                {
                    state.ToggleDelegate(disciple);
                    ui.Refresh(state);
                    ShowDisciple(disciple);
                }) : null);
        }

        private void ShowProtagonist()
        {
            var hero = state.Protagonist;
            ui.ShowCharacter(hero.name, hero.title, hero.gender, hero.portrait ?? hero.lookId, hero.attrs, hero.lines,
                state.WeaponRankDelta(hero.weapon));
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
            candidates = null;
            var report = state.EndMonth();
            ShowCommands();
            if (openBuilding != null && ui.BuildingOpen)
                ShowBuilding(openBuilding);
            AudioManager.Play("month");
            ui.Refresh(state);
            tutorial?.Record("end_month");
            if (!state.Save())
                report.Lines.Add("自動存檔失敗。");
            void ReadReport()
            {
                tutorial?.Record("read_report");
                if (!state.Save())
                    ui.Message("自動存檔失敗。");
            }
            ui.ShowModal("門派月報", report.Lines, closed: ReadReport);
        }

        private bool TutorialApGrant(ActionData action)
        {
            string trigger = action.type == "build" ? "build_any" : action.type;
            return tutorial?.CanFund(trigger) == true && (trigger == "build_any" || trigger == "recruit");
        }

        private bool CanUseAp(ActionData action) => state.ActionPoints >= action.apCost ||
            (action.type == "recruit" && candidates != null) || TutorialApGrant(action);

        private bool SpendActionAp(ActionData action)
        {
            if (state.TrySpendAp(action.apCost, action.id))
                return true;
            return TutorialApGrant(action) && state.TrySpendAp(state.ActionPoints, action.id);
        }

        private string ActionLabel(ActionData action)
        {
            if (action.type == "recruit")
                return CanUseAp(action) ? $"{action.name}　行動力 {action.apCost} 起" : $"{action.name}　行動力不足";
            int silver = action.cost.silver;
            string resources = silver == 0 && action.cost.food == 0 ? "" : $"　{CostText(silver, action.cost.food)}";
            string reason = CanUseAp(action) ? "" : "（行動力不足）";
            return $"{action.name}　行動力 {action.apCost}{resources}{reason}";
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
