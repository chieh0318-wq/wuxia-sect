using System;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        /// <summary>
        /// Shots 40–45: lit build plots with the rich build menu, the command cards, the tech tree, a secret-realm floor,
        /// its treasure page and the protagonist wearing the loot. Each step also checks the system it shows.
        /// </summary>
        /// <summary>Creation base tiers and additive skill stacking: 劍 C/B → +1 → B/A → +2 → A+/S.</summary>
        private static void VerifyRankStacking()
        {
            var lines = RankSystem.Seed(null, RankSystem.PrimaryRank, RankSystem.PrimaryCap, RankSystem.BaseCap, RankSystem.BaseRank);
            var sword = Array.Find(lines, line => line.id == "sword");
            var fist = Array.Find(lines, line => line.id == "fist");
            string start = $"{sword.rank}/{sword.cap} {fist.rank}/{fist.cap}";
            RankSystem.Learn(lines, new CreationOption { category = "sword", cost = 1 });
            string one = $"{sword.rank}/{sword.cap}";
            RankSystem.Learn(lines, new CreationOption { category = "sword", cost = 2 });
            string three = $"{sword.rank}/{sword.cap}";
            RankSystem.Learn(lines, new CreationOption { category = "sword", cost = 3 });
            string six = $"{sword.rank}/{sword.cap}";
            bool ok = start == "C/B D/C" && one == "B/A" && three == "A+/S" && six == "SSS/SSS";
            string report = $"[Capture] M6 rank stacking: base {start} → +1 {one} → +2 {three} → +3 {six}";
            if (ok) Debug.Log(report);
            else Debug.LogError(report + " (unexpected)");
        }

        private (string filename, Action show)[] M6CaptureSteps() => new (string, Action)[]
        {
            ("40_m6_build_plots.png", () =>
            {
                VerifyRankStacking();
                ui.CloseModal(force: true);
                var build = data.Actions.First(action => action.type == "build");
                state.TrySpendAp(state.ActionPoints, "capture-softlock");
                BeginBuild(build);
                if (!world.PlotsHighlighted || buildChoice == null || state.ActionPoints < build.apCost)
                    Debug.LogError("[Capture] M6 build: plots not lit, nothing pre-selected, or tutorial AP aid missing.");
                else
                    Debug.Log($"[Capture] M6 build: plots lit, pre-selected {buildChoice.name}, tutorial AP {state.ActionPoints} (was 0).");
                controls.Frame(new Vector2(1.1f, 0f), 34f);
            }),
            ("41_m6_commands.png", () =>
            {
                CancelBuild();
                var library = Array.Find(state.Buildings, building => building?.id == "library");
                if (library != null) ShowBuilding(library);
            }),
            ("42_m6_tech_tree.png", () =>
            {
                ui.CloseBuilding();
                state.Apply(new Yield { silver = 120 });
                state.GrantAp(8);
                foreach (string id in new[] { "sword1", "sword2", "inner1", "craft1" })
                    state.Research(GameState.Tech(id));
                if (!state.HasTech("sword2") || state.TechStatus(GameState.Tech("sword3")) != "[可研習]" ||
                    !state.TechStatus(GameState.Tech("outer2")).StartsWith("[需前置", StringComparison.Ordinal))
                    Debug.LogError("[Capture] M6 tech tree statuses are wrong.");
                else
                    Debug.Log($"[Capture] M6 tech tree: {state.TechCount} researched, statuses verified.");
                ShowTechTree();
            }),
            ("43_m6_secret_realm.png", () =>
            {
                CloseTechTree();
                EnterSecretRealm("循著江湖傳聞");
            }),
            ("44_m6_realm_treasure.png", () =>
            {
                var run = realmRun;
                while (run != null && run.stage < run.realm.stages.Length)
                    SecretRealms.Resolve(state, run, run.realm.stages[run.stage].choices[0]);
                int gear = state.Protagonist.gear?.Length ?? 0;
                ClaimRealm();
                if ((state.Protagonist.gear?.Length ?? 0) + (state.Story.armoury?.Length ?? 0) == 0 || state.Story.realmsCleared != 1)
                    Debug.LogError("[Capture] M6 secret realm granted no gear.");
                else
                    Debug.Log($"[Capture] M6 secret realm cleared (score {run?.score}), gear worn {state.Protagonist.gear.Length} (was {gear}).");
            }),
            ("45_m6_hero_gear.png", () =>
            {
                ui.CloseModal(force: true);
                state.Apply(new Yield { silver = 40 });
                state.GrantAp(2);
                Debug.Log("[Capture] M6 forge: " + state.ForgeGear());
                foreach (string slot in GameState.GearSlots)
                    if (state.Equipped(slot) == null)
                        state.EquipOrStore(GameState.MakeGear(slot, 0));
                int equipped = GameState.GearSlots.Count(slot => state.Equipped(slot) != null);
                Debug.Log($"[Capture] Hero gear: {equipped}/{GameState.GearSlots.Length} slots equipped.");
                ShowProtagonist();
            })
        };
    }
}
