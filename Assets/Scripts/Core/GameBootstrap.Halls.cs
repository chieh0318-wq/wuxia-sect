using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    /// <summary>v1124 C18 山門堂口: found, upgrade and use the halls; unlocks follow reputation, resources and story.</summary>
    public sealed partial class GameBootstrap
    {
        private string hallMessage;

        private void OpenHalls()
        {
            if (state == null) return;
            LeaveCourtyardForScene();
            ui.CloseModal(force: true);
            hallMessage = null;
            ui.Scene.Begin("山門堂口", $"{state.SectLabel}　{state.DateWithDay}", "sect_halls");
            ShowHalls();
        }

        private void ShowHalls()
        {
            var cards = HallCatalog.Data.halls.Select(hall =>
            {
                int level = state.HallLevel(hall.id);
                var card = new HallCard
                {
                    id = hall.id, name = hall.name, icon = hall.icon, art = hall.art, desc = hall.desc, summary = hall.summary,
                    founded = level > 0,
                    level = level > 0 ? $"{level}／{hall.maxLevel} 級" : state.HallUnlocked(hall) ? "可興建" : "未解鎖",
                    requirements = state.HallRequirements(hall).ToList()
                };
                if (level > 0)
                    foreach (var function in hall.functions)
                    {
                        string block = state.HallFunctionBlock(function);
                        string cost = string.Join("・", new[]
                        {
                            function.ap > 0 ? $"行動力 {function.ap}" : null, function.silver > 0 ? $"銀 {function.silver}" : null,
                            function.food > 0 ? $"糧 {function.food}" : null
                        }.Where(part => part != null));
                        card.functions.Add(($"{function.name}　{cost}", block ?? function.desc,
                            block == null ? () => UseHall(hall, function) : null));
                    }
                if (level < hall.maxLevel)
                {
                    var price = state.HallCost(hall);
                    string block = state.HallBuildBlock(hall);
                    string cost = $"銀 {price.silver}" + (price.food > 0 ? $"・糧 {price.food}" : "") + $"・行動力 {hall.ap}";
                    card.build = (level > 0 ? $"擴建為 {level + 1} 級" : $"興建{hall.name}", block == null ? cost : $"{cost}　{block}",
                        block == null ? () => BuildHall(hall) : null);
                }
                return card;
            }).ToList();
            int founded = HallCatalog.Data.halls.Count(hall => state.HallLevel(hall.id) > 0);
            string header = $"已建堂口 {founded}／{HallCatalog.Data.halls.Length}　聲望 {state.Reputation}　銀兩 {state.Silver}　糧食 {state.Food}" +
                $"　行動力 {state.ActionPoints}／{state.ApMax}　　堂口隨聲望、資源與劇情逐步開放；每月結算時依等級生效。";
            ui.ShowHalls(cards, header, hallMessage, () => CloseScene(hallMessage));
        }

        private void BuildHall(HallData hall) =>
            ui.ChooseAssignment(state, hall.name, "build", 1, party => BuildHall(hall, party[0]), ShowHalls);

        private void BuildHall(HallData hall, string worker)
        {
            hallMessage = state.BuildHall(hall, worker);
            AudioManager.Play(state.HallLevel(hall.id) > 0 ? "complete" : "page");
            ui.Refresh(state);
            ShowHalls();
        }

        private void UseHall(HallData hall, HallFunction function)
        {
            switch (function.target)
            {
                case "person":
                    ui.ShowHallTargets(function.name, function.desc, state.SparCandidates.Where(id => AssignmentSystem.Block(state, id) == null).Select(id =>
                    {
                        var member = state.Disciples.FirstOrDefault(d => d.id == id);
                        var image = id == GameState.HeroId
                            ? PortraitService.Resolve(PortraitService.IdOrDefault(state.Protagonist.portrait, state.Protagonist.lookId), state.Protagonist.gender)
                            : PortraitService.Resolve(PortraitService.IdOrDefault(member?.portrait, id), member?.gender);
                        return (id, state.SparName(id), state.SparLine(id), image);
                    }).ToList(), id => RunHall(hall, function, id), ShowHalls);
                    return;
                case "slot":
                    ui.ShowHallTargets(function.name, function.desc, HallCatalog.Data.slots.Select(slot =>
                    {
                        var worn = state.Equipped(slot);
                        return (slot, slot, worn == null ? "目前未裝備" : $"現有：【{worn.grade}】{worn.name}", UiKit.Icon(UiKit.SlotIcon(slot)));
                    }).ToList(), slot => RunHall(hall, function, slot), ShowHalls);
                    return;
                default:
                    RunHall(hall, function, null);
                    return;
            }
        }

        private void RunHall(HallData hall, HallFunction function, string target)
        {
            if (function.target != "person")
            {
                ui.ChooseAssignment(state, function.name, function.id, 1,
                    party => CompleteHallFunction(hall, function, target, party[0]), ShowHalls);
                return;
            }
            CompleteHallFunction(hall, function, target, target);
        }

        private void CompleteHallFunction(HallData hall, HallFunction function, string target, string worker)
        {
            hallMessage = state.RunHallFunction(hall, function, target, worker);
            AudioManager.Play(function.id == "forge" ? "complete" : "page");
            ui.Refresh(state);
            ShowHalls();
        }
    }
}
