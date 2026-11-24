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
        internal static bool MuteAudio { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void MuteAutomationAudio()
        {
            string[] args = Environment.GetCommandLineArgs();
            MuteAudio = Application.isBatchMode || args.Contains("-wuxiaCapture") ||
                args.Contains("-wuxiaCaptureRes") || args.Contains("-wuxiaQuitAfter") ||
                args.Contains("-wuxiaDiplomacyCapture") ||
                args.Contains("-runTests");
            if (MuteAudio)
            {
                AudioListener.volume = 0;
                AudioListener.pause = true;
            }
        }

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
            string[] args = Environment.GetCommandLineArgs();
            int capture = Array.IndexOf(args, "-wuxiaCapture");
            int diplomacyCapture = Array.IndexOf(args, "-wuxiaDiplomacyCapture");
            if (!Application.isBatchMode && capture < 0 && diplomacyCapture < 0)
            {
                int sysW = Display.main != null && Display.main.systemWidth >= 1600 ? Display.main.systemWidth : 1920;
                int sysH = Display.main != null && Display.main.systemHeight >= 900 ? Display.main.systemHeight : 1080;
                if (Screen.width < 1600 || Screen.height < 900)
                    Screen.SetResolution(sysW, sysH, FullScreenMode.FullScreenWindow);
            }
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
            if (capture >= 0 && capture + 1 < args.Length)
                StartCoroutine(Capture(args[capture + 1]));
            else if (diplomacyCapture >= 0 && diplomacyCapture + 1 < args.Length)
                StartCoroutine(CaptureDiplomacy(args[diplomacyCapture + 1]));
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
            ui.GoalsFromSystem = () => ui.ShowSectGoals(next, ui.ShowSystemMenu);
            if (!capturing)
            {
                state.ChooseEnvoy = (action, faction, selected) => ui.ChooseDiplomaticAssignment(state, action, faction, selected);
                state.ChooseMapParty = (title, selected) => ui.ChooseMapAssignment(state, title, selected);
            }
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
            ui.ShowMapButtons(ToggleWorldMap, () => ToggleWorldMap(true), ShowCharacterDirectory, OpenSystemMenu, ShowSectRoster);
            if (startTutorial)
                BootStep("tutorial", () => tutorial = new TutorialGuide(ui, state));
            // v1126 #8: the 任務 panel (short-term goal on top, the four routes pinned underneath).
            ui.TutorialQuest = () => tutorial?.Current;
            ui.ShowQuestPanel(state);
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
            world = new Courtyard(transform, ui, state.Data.Start.slots.TakeWhile(slot => slot.scale <= state.SectScale).ToArray(),
                state.SectScale);
            controls?.SetCourtyardScale(state.SectScale);
            for (int index = 0; index < state.Buildings.Length && state.PlotUnlocked(index); index++)
            {
                if (state.Buildings[index] != null)
                    world.PlaceBuilding(index, state.Buildings[index]);
            }
            var hero = state.Protagonist;
            world.SpawnDisciple(ProtagonistFigure(), CharacterFactory.HeroOriginStandee(hero.gender,
                hero.lookId ?? hero.portrait, hero.backgroundId, hero.title));
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
                portrait = PortraitService.IdOrDefault(hero.portrait, hero.lookId), look = look, age = hero.age
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
                cameraObject.AddComponent<AudioListener>().enabled = !RuntimeEntry.MuteAudio;
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

        private void OpenSystemMenu()
        {
            if (choosingBuilding) CancelBuild();
            ui.CloseDialogue();
            ui.CloseBuilding();
            ui.ShowSystemMenu();
        }

        private void LateUpdate()
        {
            if (!courtyardReady && ui != null)
            {
                ui.UpdateStoryBeat();
                ui.UpdateAppearance();
                if (!ui.StoryOpen && UiBack.BackPressed && !UiBack.TryBack())
                    ui.ModalBack(); // v1125 #16
                return;
            }
            if (ui == null || view == null || !courtyardReady)
                return;
            if (ui.StoryOpen)
            {
                ui.UpdateStoryBeat();
                return;
            }
            if (ui.SceneOpen)
            {
                if (UiBack.BackPressed) UiBack.TryBack(); // v1125 #16: scenes may register a back step
                return; // v1124 C15–C18: scene pages own the input
            }
            if (HandleWorldMapInput()) return; // M2-B map input
            if (!capturing && !ui.ModalOpen && !ui.DialogueOpen && !ui.TalkOpen && !choosingBuilding)
            {
                if (Input.GetKeyDown(KeyCode.C))
                {
                    ShowCharacterDirectory();
                    return;
                }
                if (Input.GetKeyDown(KeyCode.B))
                {
                    ShowSectRoster();
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
            Transform hovered = null;
            if (!capturing && !ui.ModalOpen && !choosingBuilding &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                Physics.SyncTransforms();
                if (Physics.Raycast(view.ScreenPointToRay(Input.mousePosition), out var hoverHit) &&
                    world.TryGetDisciple(hoverHit.collider, out _))
                    hovered = hoverHit.collider.transform.parent;
            }
            ui.PositionLabels(view, hovered, selectedCourtyardPerson);
            ui.PositionHighlight(tutorial?.Highlight, view, world);
            if (HandleBack()) // v1125 #16: right-click / Esc = back one page
                return;

            if (ui.ModalOpen)
                return;
            if (!Input.GetMouseButtonDown(0))
                return;

            Vector3 mouse = Input.mousePosition;
            if (ui.DialogueOpen && !ui.IsInDialogue(mouse))
                ui.CloseDialogue();
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            Physics.SyncTransforms();
            selectedCourtyardPerson = null;
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
                    selectedCourtyardPerson = hit.collider.transform.parent;
                    if (disciple.id == ProtagonistFigureId)
                        ShowProtagonist();
                    else
                        Speak(disciple);
                    return;
                }
            }

            ui.CloseBuilding();
        }

        private Transform selectedCourtyardPerson;
        private Material mountainSky;

        private void OnDestroy()
        {
            ui?.Destroy();
            world?.Destroy();
            Destroy(mountainSky);
        }

        private void ShowCommands()
        {
            bool maySpar = state.ActionPoints >= state.SparApCost && state.Food >= state.SparFoodCost;
            int halls = HallCatalog.Data.halls.Count(hall => state.HallLevel(hall.id) > 0);
            int ready = HallCatalog.Data.halls.Count(hall => state.HallLevel(hall.id) == 0 && state.HallUnlocked(hall));
            var categories = new (string name, string detail, (string label, string detail, System.Action run, bool selected)[] entries)[]
            {
                ("建造", "興建宗門設施", Matching(action => action.type == "build").Append(("擴建山門 ›", "查看規模與擴建條件", (System.Action)ShowSectExpansion, false)).Append(
                    ("山門堂口　›", $"已建 {halls}／{HallCatalog.Data.halls.Length}，{ready} 處可興建", (System.Action)OpenHalls, ready > 0)).ToArray()),
                ("人事", "招募弟子、查閱門派與主角資料", Matching(action => action.type == "recruit").Concat(new[]
                {
                    ("門派政務　›", $"收支、弟子與建築，可直接派人辦事　{HousingInfo()}", (System.Action)ShowSect, false),
                    ("外派名單・飛鴿傳書 ›", "查閱外派進度，或緊急召回隊伍", (System.Action)ShowFieldDispatches, state.Story.fieldDispatches.Length > 0),
                    ("門中位階 ›", "查閱貢獻並升階", (System.Action)(() => ui.ShowRankLadder(state, null)), false),
                    ("主角　›", $"{state.Protagonist.name}：換裝備、調主修、修練、用秘笈", (System.Action)ShowProtagonist, false)
                }).ToArray()),
                ("探險", "走訪山道，探尋秘境", Matching(action => action.id is "explore" or "secret_realm")),
                ("外交", "拜會各方勢力", new[]
                {
                    ("開啟江湖外交　›", "查看勢力關係，派使後依所選交涉扣除行動力",
                        (System.Action)(() => { ToggleWorldMap(); if (map != null && map.IsOpen) map.OpenDiplomacy(null); }), false)
                }),
                ("征伐", "前往江湖地圖調兵行軍", new[]
                {
                    ("山門軍情 ›", "查閱敵軍動向、指派守將與佈防", (System.Action)ShowRaidDesk, state.RaidReady),
                    ("開啟江湖地圖　›", "點選據點查看路線與出兵條件", (System.Action)ToggleWorldMap, false)
                }),
                ("修練", "研習藏經閣功法，或在山門切磋", new[]
                {
                    ("藏經閣科技樹　›", state.BuildingLevel("library") > 0
                        ? $"已研習 {state.TechCount}／{GameState.TechTree.Length}，研習資費標於各節點" : "須先興建藏經閣",
                        state.BuildingLevel("library") > 0 ? (System.Action)ShowTechTree : null, false),
                    (state.SparApCost > 0 ? $"山門演武　行動力 {state.SparApCost}" : "山門演武　›", "選派同門對練，提升武學與好感", maySpar ? (System.Action)StartSparring : null, false)
                })
            };
            ui.ShowCommandCategories(categories);

            (string label, string detail, System.Action run, bool selected)[] Matching(System.Func<ActionData, bool> filter) =>
                state.Data.Actions.Where(filter).Select(action =>
                    (ActionLabel(action), action.summary, CanUseAp(action) ? (System.Action)(() => RunAction(action)) : null, false)).ToArray();
        }

        private void ShowBuilding(BuildingData building)
        {
            openBuilding = building;
            var actions = new List<(string, string, System.Action)>();
            foreach (string id in building.actions)
            {
                var action = state.Data.Actions.FirstOrDefault(entry => entry.id == id);
                if (action != null)
                    actions.Add((ActionLabel(action), action.summary, CanUseAp(action) ? () => RunAction(action) : null));
            }
            if (building.id == "library")
                actions.Add(("開啟門派科技樹", $"已研習 {state.TechCount}／{GameState.TechTree.Length}：劍宗・外功・內功輕功・百工", ShowTechTree));
            int level = state.BuildingLevel(building.id);
            if (building.id == "council" && level < 3)
            {
                int silver = state.BuildSilver(18 * level);
                bool mayUpgrade = state.ActionPoints >= 1 && state.Silver >= silver;
                actions.Add(($"擴建　行動力 1／銀 {silver}", $"升為 {level + 1} 級：每月行動力上限再 +1", mayUpgrade ? () =>
                {
                    ui.ChooseAssignment(state, "擴建議事堂", "build", 1, party =>
                    {
                        state.UpgradeCouncil(party[0]);
                        ui.Refresh(state);
                        ShowCommands();
                        ShowBuilding(building);
                    });
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

            if (action.id == "lodge" && state.Story.lodgeMonth == state.Year * 12 + state.Month)
            {
                ui.Message($"本月已安頓過弟子。{HousingInfo()}");
                return;
            }
            bool expedition = action.id is "explore" or "secret_realm";
            ui.ChooseAssignment(state, action.name, action.id, expedition ? 3 : 1,
                party => {
                    if (action.id == "train") ChooseTraining(action, party);
                    else ExecuteAssignedAction(action, party);
                });
        }

        private void ExecuteAssignedAction(ActionData action, string[] party, string manual = null)
        {
            if (!state.CanPay(action.cost) || party.Any(id => AssignmentSystem.Block(state, id) != null) ||
                !SpendActionAp(action)) return;
            AssignmentSystem.Commit(state, party);
            state.Pay(action.cost);
            string worker = party[0];
            var prediction = AssignmentSystem.Predict(state, action.id, worker);
            foreach (string id in party) AssignmentSystem.Credit(state, id, 1);
            if (action.id is "explore" or "secret_realm") expeditionParty = party;
            if (action.id == "secret_realm")
            {
                state.Apply(action.effect);
                ui.Refresh(state);
                ShowCommands();
                EnterSecretRealm("循著江湖傳聞");
                return;
            }
            if (action.id == "explore")
            {
                // v1124 C15: 山道巡訪 plays as a scene (encounter, optional battle, rewards).
                ui.Refresh(state);
                ExploreRoad(action);
                return;
            }
            string breakthrough = action.id switch
            {
                "train" => state.TrainMartial(worker, manual),
                "feast" => state.Feast(),
                "forge_gear" => state.ForgeGear(worker),
                "decree" => state.Decree(),
                "lodge" => state.Lodge(),
                _ => ""
            };
            bool realm = action.id == "gather_rumors" && state.GatherRumors(worker);
            var output = new Yield
            {
                silver = Mathf.RoundToInt(action.effect.silver * prediction.Quality),
                food = Mathf.RoundToInt(action.effect.food * prediction.Quality),
                materials = Mathf.RoundToInt(action.effect.materials * prediction.Quality),
                reputation = Mathf.RoundToInt(action.effect.reputation * prediction.Quality)
            };
            state.Apply(output);
            if (output.silver > 0)
                AudioManager.Play("coin");
            if (action.id == "medicine")
            {
                state.RecoverInjuries(worker);
                if (state.HasTech("craft1"))
                {
                    state.RecoverInjuries(worker);
                    state.Apply(new Yield { reputation = 1 });
                }
            }
            if (action.id == "forge_gear")
                AudioManager.Play("complete");
            if (action.id == "council" && state.BuildingLevel("council") >= 2)
                state.ApplyEffects(new[] { new GameEffect { key = "flag", @ref = "sect_records", value = 1 } });
            ui.Refresh(state);
            ShowCommands();
            if (openBuilding != null && ui.BuildingOpen)
                ShowBuilding(openBuilding);
            string message = $"{state.SparName(worker)}完成{action.name}。{Changes(output)}";
            if (action.id == "lodge")
                message = $"{breakthrough}{HousingInfo()}".TrimEnd();
            else if (action.id == "gather_rumors")
                message = $"茶寮裡打聽到不少消息，下次奇遇機率 +25%（目前 +{state.Story.rumorBonus:P0}）。{Changes(action.effect)}";
            else if (!string.IsNullOrEmpty(breakthrough))
                message = $"{breakthrough}　{Changes(action.effect)}".TrimEnd();
            ui.Message(message);
            if (realm)
                OfferSecretRealm("茶客壓低聲音說起一處無人敢去的秘境，連路徑都畫給了你。");
        }

        private void BeginBuild(ActionData action)
        {
            if (!state.Buildings.Where((_, index) => state.PlotUnlocked(index)).Any(building => building == null))
            {
                ui.Message("院中已無空地。");
                return;
            }

            choosingBuilding = true;
            buildAction = action;
            // M6: pre-select the first available building and light the empty plots immediately.
            buildChoice = state.Data.Buildings.FirstOrDefault(state.IsAvailable);
            ui.CloseBuilding();
            ui.CloseDialogue();
            string aid = TutorialBuildAid();
            world.HighlightPlots(true);
            ShowBuildMenu();
            ui.Message(aid + (buildChoice == null ? "沒有可建的屋舍。" : $"已選【{buildChoice.name}】，點擊亮綠色空地動工"));
        }

        private Cost BuildTotal(BuildingData building) => new Cost
        {
            silver = state.BuildSilver(building.cost.silver + buildAction.cost.silver),
            food = building.cost.food + buildAction.cost.food
        };

        /// <summary>Build menu: every available building as a card (cost, purpose / yield), the current pick marked 【已選】.</summary>
        private void ShowBuildMenu()
        {
            var options = new List<(string, string, System.Action, bool)>
            {
                ("‹ 返回指令", "取消建造，回到門派事務（右鍵亦可）", CancelBuild, false)
            };
            options.Add(("擴建山門 ›", $"{GameState.Scales[state.SectScale].name}　用地 {state.LandUsed}／{state.LandCapacity}　材料 {state.Materials}",
                ShowSectExpansion, false));
            foreach (var building in state.Data.Buildings.OrderByDescending(building => building.resource))
            {
                var total = BuildTotal(building);
                string cost = $"銀 {total.silver}" + (total.food > 0 ? $"・糧 {total.food}" : "") +
                    (buildAction.apCost > 0 ? $"・行動力 {buildAction.apCost}" : ""); // v1125 #8: no 「行動力 0」
                bool picked = building == buildChoice;
                string kind = building.resource ? "資源" : "特殊";
                string plot = $"{kind}・占地 {GameState.Footprint(building)}・{building.size[0]:0.#} × {building.size[2]:0.#} 丈";
                var available = state.IsAvailable(building);
                string reason = available ? "" : building.scale > state.SectScale ? $"　須{GameState.Scales[building.scale].name}"
                    : state.BuildingCount(building) >= state.BuildingLimit(building) ? "　已達棟數上限" : "　用地不足";
                options.Add(($"{(picked ? "【已選】" : "")}{building.name}\n{cost}", $"{plot}{reason}\n{building.summary ?? building.description}", available ? (System.Action)(() =>
                {
                    buildChoice = building;
                    string aid = TutorialBuildAid();
                    world.HighlightPlots(true);
                    ShowBuildMenu();
                    ui.Message($"{aid}已選【{building.name}】，點擊亮綠色空地動工");
                }) : null, picked));
            }
            ui.ShowCommands("建造", options);
        }

        /// <summary>
        /// Tutorial softlock guard: while 「擴建院落」 (build_any) is pending, top AP up to the build cost and silver /
        /// food up to the chosen building's price.
        /// </summary>
        private string TutorialBuildAid()
        {
            if (tutorial?.CanFund("build_any") != true || buildAction == null)
                return "";
            int ap = Mathf.Max(0, buildAction.apCost - state.ActionPoints);
            var total = buildChoice == null ? new Cost() : BuildTotal(buildChoice);
            var grant = new Yield { silver = Mathf.Max(0, total.silver - state.Silver), food = Mathf.Max(0, total.food - state.Food) };
            if (ap == 0 && grant.silver == 0 && grant.food == 0)
                return "";
            state.GrantAp(ap);
            state.Apply(grant);
            ui.Refresh(state);
            return "【引導補助】門派撥付開山營造銀兩與行動力　";
        }

        private void PlaceBuilding(int index)
        {
            if (!state.PlotUnlocked(index) || state.Buildings[index] != null)
            {
                ui.Message("此處已有建築。請選擇綠色空地。");
                return;
            }
            if (!state.IsAvailable(buildChoice))
            {
                ui.Message("目前無法興建。請檢查占地、規模或棟數上限。");
                return;
            }

            if (capturing) { CompleteConstruction(index, GameState.HeroId); return; }
            ui.ChooseAssignment(state, $"興建{buildChoice.name}", "build", 1, party => CompleteConstruction(index, party[0]));
        }

        private void CompleteConstruction(int index, string worker)
        {
            if (buildChoice == null || state.Buildings[index] != null || !state.IsAvailable(buildChoice) ||
                AssignmentSystem.Block(state, worker) != null) return;
            string grant = TutorialBuildAid();
            var total = BuildTotal(buildChoice);
            float quality = AssignmentSystem.Predict(state, "build", worker).Quality;
            total.silver = Mathf.CeilToInt(total.silver / Mathf.Max(1, quality));
            if (!state.CanPay(total) || !SpendActionAp(buildAction)) return;
            state.Pay(total);
            AssignmentSystem.Commit(state, new[] { worker });
            AssignmentSystem.Credit(state, worker, 2);
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
            ledgerOpen = false;
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
            var castLines = CastDialogue.Find(disciple.id)?.daily?.Get(CastDialogue.Tier(CastDialogue.Score(state, disciple)));
            text = CastDialogue.Format(state, CastDialogue.Pick(castLines) ?? text, disciple.id);
            if (state.Story.delegateId == disciple.id)
                text = CastDialogue.CharacterSpeech(state, disciple.id, "門務暫由我料理。");
            // v1125 #4: courtyard chat uses the large-portrait talk instead of the old small box.
            ui.ShowTalk(new PortraitLine(disciple.name, $"{DiscipleTitle(disciple)}／{disciple.trait}",
                    PortraitService.IdOrDefault(disciple.portrait, disciple.id), disciple.gender, text),
                new (string, System.Action)[] { ("人物詳情　›", () => ShowDisciple(disciple)) });
            if (capturing) ui.RevealTalkForCapture();
            tutorial?.Record("talk_disciple");
        }

        private void ShowDisciple(DiscipleData disciple)
        {
            bool delegateRole = state.Story.delegateId == disciple.id;
            bool senior = disciple.age >= 20 && disciple.startInSect;
            // v1125 #5: 「委任總管」 (the hero already is 代掌門); the button spells out the trade-off.
            ui.ShowCharacter(disciple.name, DiscipleTitle(disciple), disciple.gender,
                PortraitService.IdOrDefault(disciple.portrait, disciple.id), disciple.attrs, disciple.lines,
                disciple.weapon, state.WeaponRankDelta(disciple.weapon),
                senior ? (delegateRole ? "解除總管\n行動力−1・恢復練武生產" : "委任總管\n行動力+1・不練武・不計生產", () =>
                {
                    state.ToggleDelegate(disciple);
                    ui.Refresh(state);
                    ShowDisciple(disciple);
                }) : null);
            DiscipleActions(disciple);
        }

        private void ShowProtagonist()
        {
            var hero = state.Protagonist;
            string storyWeapon = RankSystem.Catalog.breakthroughs?.FirstOrDefault(
                entry => entry.id == hero.weapon && entry.type == "weapon")?.name ?? hero.weapon;
            ui.ShowCharacter(hero.name, UiKit.HeroTitle(hero) == hero.name ? "" : UiKit.HeroTitle(hero), hero.gender, PortraitService.IdOrDefault(hero.portrait, hero.lookId), hero.attrs, hero.lines,
                hero.weapon, state.WeaponRankDelta(hero.weapon), gear: state.GearLabels(storyWeapon), stats: state.HeroStats, attributeBonuses: Enumerable.Range(0, AttributeSystem.Count).Select(state.HeroAttributeBonus).ToArray());
            HeroActions(); // v1125 #10: equip, 主修, training and manuals right on the hero page
        }

        private void ShowSect() => ShowSectLedgerPage(); // v1125 #9: GameBootstrap.V1125Ui.cs

        private string HousingInfo()
        {
            return $"床位 {state.Beds}　弟子 {state.Disciples.Count}";
        }

        private void EndMonth()
        {
            if (state == null)
                return;
            if (choosingBuilding)
                CancelBuild();
            candidates = null;
            var before = (state.Silver, state.Food, state.Reputation);
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
            ShowMonthReport("門派月報", report.Lines, before, ReadReport); // v1125 #20: scroll + 軍師 + new-month opener
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
            // v1125 #8: only actions that really spend action points show them; menus that open a page get 「›」.
            bool opensPage = action.type is "build" or "recruit";
            if (action.type == "recruit")
                return CanUseAp(action) ? $"{action.name}　›" : $"{action.name}　行動力不足";
            int silver = action.cost.silver;
            string resources = silver == 0 && action.cost.food == 0 ? "" : $"\n{CostText(silver, action.cost.food)}";
            string reason = CanUseAp(action) ? "" : "（行動力不足）";
            string ap = action.apCost > 0 ? $"　行動力 {action.apCost}" : "";
            return $"{action.name}{(opensPage ? "　›" : ap)}{reason}{resources}";
        }

        private static string CostText(int silver, int food) =>
            food == 0 ? $"銀\u00A0{silver}" : $"銀\u00A0{silver}／糧\u00A0{food}";

        internal static string Changes(Yield effect)
        {
            var changes = new List<string>();
            if (effect.silver != 0) changes.Add($"銀兩 {effect.silver:+0;-0}");
            if (effect.food != 0) changes.Add($"糧食 {effect.food:+0;-0}");
            if (effect.materials != 0) changes.Add($"材料 {effect.materials:+0;-0}");
            if (effect.reputation != 0) changes.Add($"聲望 {effect.reputation:+0;-0}");
            return string.Join("、", changes);
        }
    }
}
