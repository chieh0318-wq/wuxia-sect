using System;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    /// <summary>
    /// v1126 PA capture shots P01–P22 (muted, last part of -wuxiaCapture; run alone with "-wuxiaCaptureOnly P"):
    /// 主修 picker and full 已選天賦 bar (#2/#5), per-point attributes (#4), nearby powers on the settlement map (#10),
    /// the friend's family support and 交給老三 (#0/#6), the 軍師's four routes and a faction intro with its tag
    /// (#8/#14), the 任務 panel with and without the guide (#8), dossiers for the hero / friend / master (#16/#18),
    /// 調整主修 and 門派志向, and the world map legend, typed flags and the attack button on three kinds of card (#12/#13/#15).
    /// </summary>
    public sealed partial class GameBootstrap
    {
        private (string filename, Action show)[] V1126PaCaptureSteps() => new (string, Action)[]
        {
            ("P01_talents_major.png", () =>
            {
                if (map != null && map.IsOpen) map.Close();
                ui.Canvas.enabled = true;
                if (ui.SceneOpen) ui.Scene.Close();
                EnsureCaptureCourtyard();
                CaptureFlow().CaptureTalents();
            }),
            ("P02_talents_full_bar.png", () => CaptureFlow().CaptureTalentsFull()),
            ("P03_attributes_per_point.png", () => CaptureFlow().CaptureAttributes()),
            ("P04_settlement_powers.png", () => CaptureFlow().CaptureLocation()),
            ("P05_friend_family.png", () => CaptureFlow(female: true).CaptureFriendFamily()),
            ("P06_master_laosan_female.png", () => CaptureFlow(female: true).CaptureMasterLeaves()),
            ("P07_advisor_routes.png", () => CaptureFlow(female: true).CaptureAdvisorOpening(6)),
            ("P08_advisor_faction_tag.png", () => CaptureFlow(female: true).CaptureAdvisorFaction()),
            ("P09_quest_panel_guide.png", () =>
            {
                ui.CloseStoryBeat();
                ui.CloseModal(force: true);
                EnsureCaptureCourtyard();
                ShowCommands();
                tutorial = new TutorialGuide(ui, state);
                tutorial.ChooseForCapture();
                ui.TutorialQuest = () => tutorial?.Current;
                ui.ShowQuestPanel(state);
            }),
            ("P10_quest_panel_stage.png", () =>
            {
                ui.HideObjective();
                tutorial = null;
                ui.RefreshQuestPanel();
            }),
            ("P11_dossier_hero.png", () =>
            {
                directoryFocus = HeroCharacterId;
                ShowSectRoster();
            }),
            ("P12_dossier_friend.png", () => ShowCharacterDirectory(0, state.FriendId)),
            ("P13_dossier_master.png", () => ShowCharacterDirectory(0, "master")),
            ("P14_dossier_disciple.png", () => ShowCharacterDirectory(0, state.Disciples.FirstOrDefault()?.id)),
            ("P15_hero_major_choice.png", () =>
            {
                ui.CloseModal(force: true);
                sectRosterMode = false;
                ShowMajorChoice();
            }),
            ("P16_sect_goals.png", () =>
            {
                ui.CloseModal(force: true);
                ui.ShowSectGoals(state, null);
            }),
            ("P17_world_map_legend.png", () =>
            {
                ui.CloseModal(force: true);
                OpenCaptureMap();
            }),
            ("P18_card_court.png", () => CaptureMapCard("tang")),
            ("P19_card_sect_neutral.png", () => CaptureMapCard("shaolin")),
            ("P20_card_outlaw.png", () => CaptureMapCard("heisong")),
            ("P21_card_foreign.png", () => CaptureMapCard("tubo")),
            ("P22_card_unknown.png", () => CaptureMapCard("louguan", known: false)),
        };

        private NewGameFlow CaptureFlow(bool female = false)
        {
            var preview = new GameState(data);
            if (female)
            {
                preview.Protagonist.gender = "女";
                preview.Protagonist.title = "三師姐";
                preview.Protagonist.name = "盧凝雪";
                preview.Protagonist.portrait = preview.Protagonist.lookId = "hero_f1";
            }
            return new NewGameFlow(ui, preview, _ => { });
        }

        private void OpenCaptureMap()
        {
            if (map == null || !map.IsOpen) OpenWorldMap();
            ui.Canvas.enabled = false;
            var overlay = map.CaptureCanvas;
            overlay.renderMode = RenderMode.ScreenSpaceCamera;
            overlay.worldCamera = view;
            overlay.planeDistance = view.nearClipPlane + .11f;
        }

        private void CaptureMapCard(string factionId, bool known = true)
        {
            OpenCaptureMap();
            var faction = GameState.FindFaction(factionId);
            if (faction == null) return;
            if (known) state.SetFactionKnown(faction.id);
            map.CaptureStronghold(faction);
        }
    }
}
