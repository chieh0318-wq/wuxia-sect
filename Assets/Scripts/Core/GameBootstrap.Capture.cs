using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private bool capturing;

        private IEnumerator Capture(string directory)
        {
            capturing = true;
            yield return new WaitForSecondsRealtime(2);
            Directory.CreateDirectory(directory);
            int width = 1920, height = 1080;
            string[] args = System.Environment.GetCommandLineArgs();
            int option = System.Array.IndexOf(args, "-wuxiaCaptureRes");
            if (option >= 0 && option + 1 < args.Length)
            {
                string[] dimensions = args[option + 1].Split('x');
                if (dimensions.Length == 2 && int.TryParse(dimensions[0], out int w) &&
                    int.TryParse(dimensions[1], out int h) && w > 0 && h > 0 &&
                    w <= SystemInfo.maxTextureSize && h <= SystemInfo.maxTextureSize)
                {
                    width = w;
                    height = h;
                }
            }
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            var canvas = ui.Canvas;
            RenderTexture previous = RenderTexture.active;
            bool shotsOk = true;
            try
            {
                controls.enabled = false;
                view.targetTexture = target;
                view.aspect = width / (float)height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = view;
                canvas.planeDistance = view.nearClipPlane + .1f;

                var creation = new NewGameFlow(ui, new GameState(data), _ => { });
                var steps = new (string filename, System.Action show)[]
                {
                    ("01_title.png", () => { }),
                    ("18_gender.png", () => { ui.CloseTitle(); creation.Begin(); }),
                    ("02_appearance.png", () => { ui.CloseTitle(); creation.CaptureAppearance(); }),
                    ("02b_appearance_female.png", creation.CaptureFemaleAppearance),
                    ("24_creation_preview.png", () =>
                    {
                        var preview = new GameState(data);
                        preview.Protagonist.gender = "女";
                        preview.Protagonist.title = "三師姐";
                        preview.Protagonist.name = "顧清霜";
                        new NewGameFlow(ui, preview, _ => { }).CaptureAppearance();
                    }),
                    ("03_talents.png", creation.CaptureTalents),
                    ("13_skills.png", creation.CaptureSkills),
                    ("14_attributes.png", creation.CaptureAttributes),
                    ("47_legacy_shop.png", creation.CaptureLegacyShop),
                    ("15_background.png", creation.CaptureBackground),
                    ("04_prologue_beat.png", creation.CapturePrologue),
                    ("04b_succession.png", creation.CaptureSuccession),
                    ("04c_friend_arrival.png", creation.CaptureFriendArrival),
                    ("05_disaster_choice.png", creation.CaptureDisaster),
                    ("16_response_choice.png", creation.CaptureResponse),
                    ("06_location_choice.png", creation.CaptureLocation),
                    ("07_courtyard_hud_ap.png", () =>
                    {
                        ui.CloseStoryBeat();
                        ui.CloseModal(force: true);
                        // Same defaults as skipping the prologue: first starting site, chosen look.
                        var next = new GameState(data);
                        var site = M2Data.Load<PrologueData>("prologue").locations?.FirstOrDefault();
                        if (site != null)
                        {
                            next.Story.locationId = site.id;
                            next.Story.mapNode = site.node;
                        }
                        EnterCourtyard(next, true);
                        controls.enabled = false;
                        tutorial?.ChooseForCapture();
                        tutorial?.Record("camera_move");
                        tutorial?.Record("camera_zoom");
                    }),
                    ("07b_commands_expanded.png", () => ui.OpenCommandCategoryForCapture("探險")),
                    ("08_building_panel.png", () =>
                    {
                        var hall = System.Array.Find(state.Buildings, building => building?.id == "hall");
                        if (hall != null)
                        {
                            tutorial?.Record("click_building", hall.id);
                            ShowBuilding(hall);
                        }
                    }),
                    ("09_dialogue_portrait.png", () =>
                    {
                        if (state.Disciples.Count > 0)
                            Speak(state.Disciples[0]);
                    }),
                    ("17_recruit_methods.png", () =>
                    {
                        ui.CloseDialogue();
                        OpenRecruitment(data.Actions.First(action => action.type == "recruit"));
                    }),
                    ("10_recruit.png", CaptureRecruitment),
                    ("19_special_recruit.png", () =>
                    {
                        var special = Recruitment.specials?.FirstOrDefault(candidate => candidate.story != null);
                        if (special != null) ReviewCandidate(special);
                    }),
                    ("11_options_audio.png", ui.ShowAudioOptions),
                    ("29_system_menu.png", ui.ShowSystemMenu),
                    ("30_exit_confirmation.png", () => ui.ConfirmExit(ui.ShowSystemMenu)),
                    ("12_protagonist_panel.png", ShowProtagonist),
                    ("26_characters_panel.png", ShowCharacterDirectory),
                    ("26b_characters_sect.png", () => ShowCharacterDirectory(1, "lin")),
                    ("26c_characters_leaders.png", () => ShowCharacterDirectory(5, null)),
                    ("27_character_detail.png", () =>
                    {
                        var person = StoryCharacters.FirstOrDefault(entry => entry.id == state.FriendId)
                            ?? StoryCharacters.FirstOrDefault();
                        if (person != null) ShowCharacterDetail(person, ShowCharacterDirectory);
                    })
                }.Concat(M6CaptureSteps()).Concat(V1124CaptureSteps()).ToArray();
                // "-wuxiaCaptureOnly 8" runs just the shots whose file names start with that prefix (v1124 P1B-2: 80–89).
                int only = System.Array.IndexOf(args, "-wuxiaCaptureOnly");
                string prefix = only >= 0 && only + 1 < args.Length ? args[only + 1] : null;
                if (prefix != null)
                    steps = steps.Where(step => step.filename.StartsWith(prefix, System.StringComparison.Ordinal)).ToArray();

                foreach (var step in steps)
                {
                    step.show();
                    yield return new WaitForSecondsRealtime(step.filename == "04_prologue_beat.png" ? 2.8f : .15f);
                    if (!SaveShot(step.filename))
                        yield break;
                    if (step.filename == "30_exit_confirmation.png" &&
                        (!ui.ActivateMenuAction("取消") || !ui.ActivateMenuAction("繼續遊戲") || ui.ModalOpen))
                    {
                        Debug.LogError("[Capture] Exit cancellation did not return to the system menu.");
                        Application.Quit(1);
                        yield break;
                    }
                    if (step.filename == "27_character_detail.png" && !ui.VerifyCharacterProfileScroll())
                    {
                        Debug.LogError("[Capture] Character profile wheel did not reach the bottom.");
                        Application.Quit(1);
                        yield break;
                    }
                }
                if (ui.SceneOpen) ui.Scene.Close(); // v1124: leave no scene page over the later shots
                if (prefix != null)
                {
                    Debug.Log($"[Capture] only '{prefix}': {steps.Length} shots done.");
                    Application.Quit(0);
                    yield break;
                }
                if (CaptureQuestline())
                {
                    yield return new WaitForSecondsRealtime(2.8f);
                    if (!SaveShot("28_questline_beat.png")) yield break;
                    ui.CloseStoryBeat();
                    // v1124 B8: the choice screen with its 「影響」 lines.
                    PlayQuest(capturedQuest.line, capturedQuest.beat, choicesOnly: true);
                    yield return new WaitForSecondsRealtime(.3f);
                    if (!SaveShot("28b_questline_choices.png")) yield break;
                }
                ui.CloseModal(force: true);
                yield return CaptureM2(file => shotsOk &= SaveShot(file));
                if (shotsOk)
                    yield return CaptureBattle();
                if (!shotsOk)
                    yield break;
                yield return CaptureHeroStandees();
                if (!shotsOk)
                    yield break;
                yield return CaptureOriginCourtyard();
                if (!shotsOk) yield break;
                if (!VerifyCapturedGame(directory))
                {
                    Application.Quit(1);
                    yield break;
                }
                ui.ConfirmExit(ui.ShowSystemMenu);
                if (!ui.ActivateMenuAction("確認退出"))
                {
                    Debug.LogError("[Capture] Exit confirmation action was unavailable.");
                    Application.Quit(1);
                }
                else
                    Debug.Log("[Capture] System-menu cancellation and normal exit exercised.");
            }
            finally
            {
                RenderTexture.active = previous;
                view.targetTexture = null;
                view.ResetAspect();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                Destroy(pixels);
                Destroy(target);
            }

            bool SaveShot(string file)
            {
                try
                {
                    // A shared capture directory may already contain the original P3 UI evidence.
                    if (file.StartsWith("6", System.StringComparison.Ordinal) && file.Length > 3 &&
                        file[1] >= '0' && file[1] <= '5' && file[2] == '_' &&
                        File.Exists(Path.Combine(directory, file)))
                        return true;
                    WritePng(file);
                    return true;
                }
                catch (System.Exception error)
                {
                    Debug.LogError($"[Capture] {error}");
                    Application.Quit(1);
                    return false;
                }
            }

            void WritePng(string file)
            {
                Canvas.ForceUpdateCanvases();
                ui.PositionLabels(view);
                ui.PositionHighlight(tutorial?.Highlight, view, world);
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(view, new UniversalRenderPipeline.SingleCameraRequest
                {
                    destination = target
                });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                string path = Path.Combine(directory, file);
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                Debug.Log($"[Capture] wrote {path}");
            }

            IEnumerator CaptureOriginCourtyard()
            {
                var walkers = FindObjectsByType<DiscipleWalker>();
                foreach (var walker in walkers) walker.gameObject.SetActive(false);
                controls.Frame(new Vector2(0, -12.3f), 5.6f);
                view.fieldOfView = 45;
                var examples = new[]
                {
                    ("女", "hero_f1", "fubing_orphan", "三師姐", true, "77_courtyard_p4_orphan.png"),
                    ("男", "hero_m9", "temple_foundling", "三師兄", false, "78_courtyard_p4_temple.png")
                };
                try
                {
                    foreach (var (gender, look, origin, title, faceRight, file) in examples)
                    {
                        var figure = new GameObject("Origin outfit capture");
                        try
                        {
                            figure.transform.position = world.Grounded(new Vector3(0, 0, -12.3f));
                            var outfit = CharacterFactory.HeroOriginStandee(gender, look, origin, title);
                            var body = CharacterFactory.BuildStandee(new DiscipleData { id = look, gender = gender },
                                figure.transform, out _, standee: outfit);
                            var renderer = body.GetComponentInChildren<SpriteRenderer>();
                            renderer.sprite = CharacterFactory.WalkingStandee(renderer.sprite);
                            renderer.flipX = CharacterFactory.FlipStandee(renderer.sprite.texture, faceRight);
                            yield return null;
                            shotsOk = SaveShot(file);
                            if (!shotsOk) yield break;
                        }
                        finally
                        {
                            Destroy(figure);
                        }
                    }
                }
                finally
                {
                    foreach (var walker in walkers)
                        if (walker != null) walker.gameObject.SetActive(true);
                }
            }

            IEnumerator CaptureHeroStandees()
            {
                var hero = state.Protagonist;
                var original = (hero.gender, hero.lookId, hero.backgroundId, hero.title, hero.name, hero.portrait);
                var examples = new[]
                {
                    ("女", "hero_f1", "fubing_orphan", "三師姐", "三師姐", "68_hero_female_dashijie.png"),
                    ("男", "hero_m1", "scholar_family", "少掌門", "少掌門", "69_hero_male_shaozhangmen.png"),
                    ("女", "hero_f6", "mountain_hunter", "三師姐", "山野獵戶", "70_hero_female_mountain_hunter.png"),
                    ("男", "hero_m9", "temple_foundling", "三師兄", "寺院棄嬰", "71_hero_male_temple_foundling.png")
                };
                try
                {
                    foreach (var (gender, look, background, title, name, file) in examples)
                    {
                        (hero.gender, hero.lookId, hero.backgroundId, hero.title, hero.name, hero.portrait) =
                            (gender, look, background, title, name, look);
                        var standee = CharacterFactory.HeroOriginStandee(gender, look, background, title);
                        Debug.Log($"[Capture] Hero {look}/{background}/{title}: {standee.name}.");
                        ui.StartTurnBattle(state, new BattleRequest(), this);
                        yield return null;
                        shotsOk = SaveShot(file);
                        if (!shotsOk) yield break;
                        ui.CloseBattle();
                    }
                }
                finally
                {
                    ui.CloseBattle();
                    (hero.gender, hero.lookId, hero.backgroundId, hero.title, hero.name, hero.portrait) = original;
                    AudioManager.Music("courtyard");
                }
            }

            IEnumerator CaptureBattle()
            {
                System.Func<IEnumerator> round = null;
                System.Action<string> inspect = null;
                bool hitShot = false, critShot = false, bossShot = false;
                ui.StartTurnBattle(state, new BattleRequest(), this, routine => round = routine, action => inspect = action,
                    CaptureImpact, captureCommand: _ => "attack", captureSeed: 89);
                try
                {
                    yield return null;
                    Debug.Log("[Capture] Battle start: both sides ready for round one.");
                    shotsOk = SaveShot("72_battle_p4_facing.png");
                    if (!shotsOk) yield break;
                    inspect("interrupt");
                    shotsOk = SaveShot("60_battle_p3_command_tooltip.png") && SaveShot("61_battle_p3_turn_order.png");
                    if (!shotsOk)
                        yield break;

                    yield return round();
                    if (!ui.BattleOpen)
                    {
                        shotsOk = false;
                        Debug.LogError("[Capture] Battle ended before the mid-battle shot.");
                        Application.Quit(1);
                        yield break;
                    }

                    yield return new WaitForSecondsRealtime(.7f);
                    Debug.Log("[Capture] Battle mid: round one resolved, action buttons visible.");
                    shotsOk = SaveShot("62_battle_p3_statuses.png");
                    if (!shotsOk) yield break;
                    ui.CloseBattle();

                    ui.StartTurnBattle(state, new BattleRequest { ground = BattleGround.MountainGate },
                        this, routine => round = routine);
                    yield return null;
                    shotsOk = SaveShot("63_battle_p3_mountain_gate.png");
                    if (!shotsOk) yield break;
                    ui.CloseBattle();

                    var worldMap = WorldMapData.Load();
                    var hangu = worldMap.Nodes["hangu"];
                    var faction = worldMap.Controllers(hangu).First(entry => entry.Outlaw);
                    var boss = WorldMapScreen.OutlawEncounter(hangu, faction);
                    bool? won = null;
                    boss.onComplete = result => won = result;
                    var randomState = Random.state;
                    try
                    {
                        Random.InitState(318);
                        ui.StartTurnBattle(state, boss, this, routine => round = routine, captureImpact: CaptureImpact,
                            captureCommand: turn => turn == 2 ? "defend" : null);
                        yield return null;
                        string track = AudioManager.CurrentTrack;
                        if (!boss.IsBoss || (track != "boss_a_g1" && track != "boss_b_g1"))
                        {
                            shotsOk = false;
                            Debug.LogError($"[Capture] Expected a stronghold boss track, selected '{track}'.");
                            Application.Quit(1);
                            yield break;
                        }
                        Debug.Log($"[Capture] Stronghold boss {boss.enemyLeadName}, cue={boss.MusicCue}, track={track}.");
                        shotsOk = SaveShot("64_battle_p3_boss.png");
                        if (!shotsOk) yield break;

                        int rounds = 0;
                        while (!won.HasValue && rounds < 10)
                        {
                            rounds++;
                            yield return round();
                            if (rounds == 1)
                            {
                                shotsOk = SaveShot("65_battle_p3_enemy_intents.png");
                                if (!shotsOk) yield break;
                            }
                        }
                        Debug.Log($"[Capture] Boss pacing: {rounds} rounds, victory={won}.");
                    }
                    finally
                    {
                        Random.state = randomState;
                    }
                }
                finally
                {
                    ui.CloseBattle();
                    AudioManager.Music("courtyard");
                }

                if (shotsOk && !(hitShot && critShot && bossShot))
                {
                    shotsOk = false;
                    Debug.LogError($"[Capture] Impact evidence missing: hit={hitShot}, crit={critShot}, boss={bossShot}.");
                    Application.Quit(1);
                }

                void CaptureImpact(BattleEvent entry)
                {
                    if (!hitShot && !entry.Critical && entry.Actor.isPlayer && shotsOk)
                        shotsOk = hitShot = SaveShot("73_battle_p4_hit.png");
                    if (!critShot && entry.Critical && shotsOk)
                        shotsOk = critShot = SaveShot("74_battle_p4_crit.png");
                    if (!bossShot && entry.Actor.enemyProfile == "boss" && entry.Skill?.telegraph == true && shotsOk)
                        shotsOk = bossShot = SaveShot("75_battle_p4_boss_move.png");
                }
            }
        }

        private (Questline line, QuestBeat beat) capturedQuest;

        private bool CaptureQuestline()
        {
            var fresh = new GameState(data);
            new NewGameFlow(ui, fresh, _ => { }).CaptureSkipPrologue();
            var seen = new System.Collections.Generic.List<(Questline line, QuestBeat beat)>();
            for (int month = 1; month <= 3; month++)
            {
                fresh.EvaluateQuestlines();
                var quest = fresh.PendingQuest();
                if (quest.beat != null)
                {
                    seen.Add(quest);
                    Debug.Log($"[Capture] Early quest month {month}: {quest.line.character}/{quest.beat.id}");
                    fresh.CompleteQuest(quest.line, quest.beat, quest.beat.choices?.FirstOrDefault());
                }
                if (month < 3) fresh.EndMonth();
            }
            var sample = seen.FirstOrDefault(quest => quest.beat.scene.Any(
                part => part?.portrait?.Contains("__") == true));
            if (sample.beat == null) sample = seen.FirstOrDefault();
            if (sample.beat == null)
            {
                Debug.LogWarning("[Capture] 28_questline_beat skipped. No playable beat triggered in months 1–3.");
                return false;
            }
            capturedQuest = sample;
            PlayQuest(sample.line, sample.beat, preferExpression: true);
            return true;
        }

        private bool VerifyCapturedGame(string directory)
        {
            string path = Path.Combine(directory, "capture-check-" + System.Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var build = System.Array.Find(data.Actions, action => action.type == "build");
                var plot = System.Array.IndexOf(state.Buildings, null);
                if (build == null || plot < 0)
                    throw new System.InvalidOperationException("The tutorial requires a build action and an empty plot.");
                ui.CloseModal(force: true);
                BeginBuild(build);
                buildChoice = System.Array.Find(data.Buildings, building => building.id == "council");
                if (buildChoice == null)
                    throw new System.InvalidOperationException("Council configuration is unavailable.");
                PlaceBuilding(plot);
                if (state.Buildings[plot]?.id != "council" || state.TrySpendAp(state.ApMax + 1, "capture-check"))
                    throw new System.InvalidOperationException("AP spending or building placement failed.");

                state.TrySpendAp(state.ActionPoints, "capture-check");
                var method = activeMethod;
                candidates = null;
                Visit(method);
                int before = state.Disciples.Count;
                if (candidates == null || candidates.Length == 0)
                    throw new System.InvalidOperationException("The tutorial could not open recruitment with zero AP.");
                var acceptedCandidate = candidates[0];
                AcceptCandidate(acceptedCandidate);
                AcceptCandidate(acceptedCandidate);
                if (state.Disciples.Count != before + 1 || !candidates.Contains(acceptedCandidate) ||
                    RecruitRestriction(acceptedCandidate) != Recruited)
                    throw new System.InvalidOperationException("The tutorial could not accept a recruit.");
                ui.CloseModal(force: true);
                state.EndMonth();
                tutorial?.Record("end_month");
                tutorial?.Record("read_report");
                var recruit = state.Disciples[before];
                if (tutorial?.Active == true || state.ActionPoints != state.ApMax || !state.Save(path))
                    throw new System.InvalidOperationException("The tutorial, AP refill, or save failed.");
                var restored = GameState.LoadSave(data, path);
                if (restored == null || restored.ActionPoints != state.ActionPoints || restored.Month != state.Month ||
                    !restored.Disciples.Exists(member => member.id == recruit.id && member.aptitude?.bone == recruit.aptitude?.bone))
                    throw new System.InvalidOperationException("The save did not restore the generated recruit, date, and AP.");
                Debug.Log("[Capture] AP, tutorial completion and generated recruit save/load smoke passed.");
                return true;
            }
            catch (System.Exception error)
            {
                Debug.LogError($"[Capture] Smoke failed: {error}");
                return false;
            }
            finally
            {
                File.Delete(path);
                File.Delete(path + ".tmp");
            }
        }
    }
}
