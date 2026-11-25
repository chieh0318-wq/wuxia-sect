using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private IEnumerable<(string filename, Action show)> Pb3CaptureSteps() => new (string, Action)[]
        {
            ("PB3_01_build_picker.png", () =>
            {
                VerifyPb3Progression();
                ui.CloseTitle();
                EnsureCaptureCourtyard();
                var sword = MartialArts.All.First(entry => entry.category == "sword" && entry.source == "creation");
                var inner = MartialArts.All.First(entry => entry.category == "neigong" && entry.source == "creation");
                state.Protagonist.skills = new[] { sword.id, inner.id };
                var line = state.MemberLines(GameState.HeroId).First(entry => entry.id == "sword");
                line.progress = GameState.MartialProgressMax * .34f;
                state.AssignStudy(GameState.HeroId, sword.id);
                ui.ChooseAssignment(state, "興建菜圃", "build_plot", 1, _ => { });
            }),
            ("PB3_01b_build_sort_desc.png", () => ClickCaptureCaption("品質")),
            ("PB3_01c_build_sort_asc.png", () => ClickCaptureCaption("品質 ↓")),
            ("PB3_02_patrol_odds.png", () => ui.ChooseAssignment(state, "山道巡訪", "explore", 3, _ => { })),
            ("PB3_03_study_order.png", () => OpenStudyOrders(GameState.HeroId, () => ui.CloseModal(force: true))),
            ("PB3_04_character_progress.png", ShowProtagonist),
            ("PB3_05_drill_result.png", () =>
            {
                ui.CloseModal(force: true);
                ExecuteAssignedAction(data.Actions.First(action => action.id == "train"), new[] { GameState.HeroId });
            }),
            ("PB3_06_spar_picker.png", StartSparring),
            ("PB3_07_build_quality.png", () =>
            {
                if (ui.SceneOpen) CloseScene();
                int slot = Array.FindIndex(state.Buildings, building => building != null);
                state.RecordConstruction(slot, state.Buildings[slot], GameState.ConstructionQuality(1.25f));
                ShowBuilding(state.Buildings[slot], slot);
            }),
            ("PB3_08_recruit_candidates.png", CaptureRecruitment),
            ("PB3_09_road_choice.png", () =>
            {
                ui.CloseModal(force: true);
                var road = RealmContent.Data.roadEvents.First(entry => entry.choices.Any(choice => choice.type == "check"));
                PlayRoadEvent(road, "", false);
                CaptureClickThrough(Math.Max(1, road.lines.Length - 1));
            })
        };

        private void VerifyPb3Progression()
        {
            var game = new GameState(data);
            string id = GameState.HeroId;
            var craft = game.MemberLines(id).First(line => line.id == "construction");
            int initial = RankSystem.Index(craft.cap);
            var life = BalanceV1126c.LifeSkill;
            for (int i = 0; i < life.capGainPerTasks; i++) game.PracticeWork(id, "construction");
            if (RankSystem.Index(craft.cap) != Mathf.Min(RankSystem.Index(RankSystem.Top), initial + Math.Min(1, life.capMaxAboveStart))
                || game.WorkPractice(id, "construction")?.tasks != life.capGainPerTasks)
                throw new InvalidOperationException("PB3 life-skill progression failed.");
            var art = MartialArts.All.First(entry => entry.Combat && entry.category == "sword");
            if (!game.KnowsArt(art.id) && !game.GrantManual(id, art.id))
                throw new InvalidOperationException("PB3 manual failed to teach its art.");
            var sword = game.MemberLines(id).First(line => line.id == art.category);
            int cap = RankSystem.Index(sword.cap);
            if (cap > 0)
            {
                sword.rank = RankSystem.At(cap - 1);
                if (!game.AssignStudy(id, art.id)) throw new InvalidOperationException("PB3 study order rejected.");
                game.PracticeMartial(id, GameState.MartialProgressMax);
                if (RankSystem.Index(sword.cap) != cap || RankSystem.Index(sword.rank) != cap ||
                    game.CurrentStudy(id, out bool assigned) != null && assigned)
                    throw new InvalidOperationException("PB3 training failed to finish the order at the cap.");
            }
            Debug.Log("[PB3] PASS life-skill cap, manual learning, martial cap and study order");
        }
    }
}
