using System;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    /// <summary>
    /// v1125 PA capture shots A01–A16 (muted, part of -wuxiaCapture; run alone with "-wuxiaCaptureOnly A"):
    /// talent cards that grow with their text, the portrait courtyard talk, the operable 門派政務 / disciple /
    /// hero pages, the 本門 roster with the hero and life skills, absent elders, the month report scroll with the
    /// 軍師's summary and the new-month opener, stacked toasts, the two-level tutorial highlight and the
    /// 門派事務 sub-menu without 「行動力 0」. The world-map 本門 marker shows in 20_world_map.png.
    /// </summary>
    public sealed partial class GameBootstrap
    {
        private (string filename, Action show)[] V1125PaCaptureSteps() => new (string, Action)[]
        {
            ("A01_talent_cards.png", () =>
            {
                EnsureCaptureCourtyard();
                new NewGameFlow(ui, new GameState(data), _ => { }).CaptureTalents();
            }),
            ("A02_courtyard_portrait_talk.png", () =>
            {
                EnsureCaptureCourtyard();
                if (state.Disciples.Count > 0) Speak(state.Disciples[0]);
                ui.RevealTalkForCapture();
            }),
            ("A03_sect_ledger.png", () =>
            {
                ui.CloseTalk(false);
                ShowSect();
            }),
            ("A04_disciple_page_actions.png", () =>
            {
                if (state.Disciples.Count > 0) ShowDisciple(state.Disciples[0]);
            }),
            ("A05_hero_page_actions.png", () =>
            {
                ui.CloseModal(force: true);
                if ((state.Story.armoury?.Length ?? 0) == 0)
                    state.Story.armoury = new[] { GameState.MakeGear("護腕", 1), GameState.MakeGear("履靴", 2) };
                ShowProtagonist();
            }),
            ("A06_hero_armoury.png", ShowArmoury),
            ("A07_sect_roster.png", () =>
            {
                ui.CloseModal(force: true);
                directoryFocus = HeroCharacterId;
                ShowSectRoster();
            }),
            ("A08_shimen_tab_hero.png", () =>
            {
                ui.CloseModal(force: true);
                sectRosterMode = false;
                ShowCharacterDirectory(1, HeroCharacterId);
            }),
            ("A09_master_absent.png", () => ShowCharacterDirectory(1, "master")),
            ("A10_commands_submenu.png", () =>
            {
                ui.CloseModal(force: true);
                ShowCommands();
                ui.OpenCommandCategoryForCapture("人事");
            }),
            ("A11_build_menu.png", () =>
            {
                ShowCommands();
                ui.OpenCommandCategoryForCapture("建造");
                var build = state.Data.Actions.FirstOrDefault(action => action.type == "build");
                if (build != null && state.ActionPoints < build.apCost) state.GrantAp(build.apCost);
                if (build != null) RunAction(build);
            }),
            ("A12_month_report_scroll.png", () =>
            {
                if (choosingBuilding) CancelBuild();
                // Never EndMonth() here: it autosaves over the player's slot. Settle the month without saving.
                var before = (state.Silver, state.Food, state.Reputation);
                var report = state.EndMonth();
                ui.Refresh(state);
                ShowMonthReport("門派月報", report.Lines, before, null);
            }),
            ("A13_advisor_summary.png", () =>
            {
                ui.CloseModal(force: true);
                PlayMonthTalks((-12, 8, 1), null);
                ui.RevealTalkForCapture();
            }),
            ("A14_new_month_opener.png", () =>
            {
                var opener = NewMonthOpener();
                if (opener.HasValue) ui.ShowTalk(opener.Value);
                ui.RevealTalkForCapture();
            }),
            ("A15_toasts.png", () =>
            {
                ui.CloseTalk(false);
                ui.Message("鍛成一件【精品】玄鐵護腕（護腕・攻擊 +6・氣血 +30），已存入門派裝備庫。這段提示文字特意寫得很長，用來檢查提示框會依內容自動長高、不會超出邊框。");
                ui.Message("茶寮裡打聽到不少消息，下次奇遇機率 +25%。銀兩 −3");
                ui.Message("糧食不足。", "hud_food");
            }),
            ("A16_tutorial_two_level.png", () =>
            {
                ShowCommands();
                tutorial ??= new TutorialGuide(ui, state);
                tutorial.ChooseForCapture();
                foreach (string trigger in new[] { "camera_move", "camera_zoom", "talk_disciple" })
                    tutorial.Record(trigger);
                tutorial.Record("click_building", "hall");
                ui.PositionHighlight(tutorial.Highlight, view, world);
            }),
        };

        /// <summary>Leaves no tutorial panel or talk from the A shots over the later map / battle captures.</summary>
        private void CleanupV1125PaCapture()
        {
            ui.CloseTalk(false);
            ui.CloseModal(force: true);
            Toast.ClearAll();
            if (tutorial != null)
            {
                ui.HideObjective();
                tutorial = null;
            }
        }
    }
}
