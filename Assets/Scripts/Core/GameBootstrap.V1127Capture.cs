using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    /// <summary>
    /// v1127 N1 capture shots (muted, part of -wuxiaCapture; run alone with "-wuxiaCaptureOnly V1127_"): a 草創 save
    /// like the user's (starting compound + 伙房 + 山門茶寮 = 17 by the old footprint sum), written to disk and loaded
    /// back through the normal save path, then the build menu (田地／菜圃 must be offered) and a 田地 placed on an
    /// empty lot.
    /// </summary>
    public sealed partial class GameBootstrap
    {
        private (string filename, Action show)[] V1127CaptureSteps() => new (string, Action)[]
        {
            ("V1127_N1_01_build_menu.png", V1127LandSave),
            ("V1127_N1_02_field_placed.png", () =>
            {
                var field = state.Data.Buildings.First(building => building.id == "field");
                int anchor = Enumerable.Range(0, state.Buildings.Length)
                    .Where(index => state.CanPlaceInCells(index, field))
                    .OrderBy(index => index % state.Data.Start.slots.Length == 7 ? 0 : 1).First();
                buildChoice = field;
                CompleteConstruction(anchor, GameState.HeroId);
                if (state.Buildings[anchor] != field) throw new InvalidOperationException("田地 placement failed on an empty lot.");
                Debug.Log($"[Capture] V1127 N1 田地 placed at {anchor}; land {state.LandUsed}/{state.LandCapacity}");
                BeginBuild(data.Actions.First(action => action.type == "build"));
            }),
        };

        private void V1127LandSave()
        {
            CleanupV1125PaCapture();
            ui.CloseStoryBeat();
            ui.CloseModal(force: true);
            var next = new GameState(data);
            next.Protagonist.name = "蕭寒舟";
            int lots = data.Start.slots.Length;
            foreach (var (id, lot) in new[] { ("kitchen", 4), ("teahouse", 0) })
            {
                var building = data.Buildings.First(b => b.id == id);
                int anchor = Enumerable.Range(0, 4).Select(corner => lot + corner * lots).First(index => next.CanPlaceInCells(index, building));
                next.Buildings[anchor] = building;
            }
            int oldSum = next.Buildings.Where(b => b != null).Sum(GameState.Footprint);
            int oldCap = GameState.Scales[next.SectScale].land;
            string path = Path.Combine(Application.temporaryCachePath, "v1127_n1_save.json");
            if (!next.Save(path)) throw new InvalidOperationException("Could not write the N1 test save.");
            var loaded = GameState.LoadSave(data, path);
            if (loaded == null) throw new InvalidOperationException("Could not load the N1 test save.");
            loaded.Apply(new Yield { silver = 500, food = 200 });
            loaded.GrantAp(5);
            EnterCourtyard(loaded, false);
            controls.enabled = false;
            var field = data.Buildings.First(b => b.id == "field");
            var garden = data.Buildings.First(b => b.id == "vegetable");
            bool oldRule = oldSum + GameState.Footprint(field) <= oldCap;
            Debug.Log($"[Capture] V1127 N1 old count {oldSum}/{oldCap} (old rule allows 田地={oldRule}); " +
                $"new land {state.LandUsed}/{state.LandCapacity}; 田地={state.IsAvailable(field)} 菜圃={state.IsAvailable(garden)}");
            if (oldSum != 17 || !state.IsAvailable(field) || !state.IsAvailable(garden))
                throw new InvalidOperationException("N1 land fix not effective on the 17/17 save.");
            BeginBuild(data.Actions.First(action => action.type == "build"));
        }
    }
}
