using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    /// <summary>v1124 C17 山門演武: pick fellow disciples (and/or yourself), spar, see who won, who grew and whose favour moved.</summary>
    public sealed partial class GameBootstrap
    {
        private readonly List<string> sparPicks = new();
        private bool sparMelee;

        private void StartSparring()
        {
            if (state == null || ui.BattleOpen) return;
            LeaveCourtyardForScene();
            ui.CloseModal(force: true);
            sparPicks.Clear();
            ui.Scene.Begin("山門演武・同門切磋", $"消耗　行動力 {state.SparApCost}・糧食 {state.SparFoodCost}　{state.DateWithDay}",
                "sect_sparring");
            ShowSparPicker();
        }

        private void ShowSparPicker()
        {
            var cards = state.SparCandidates.Where(id => AssignmentSystem.Block(state, id) == null).Select(id =>
            {
                int order = sparPicks.IndexOf(id);
                var member = state.Disciples.FirstOrDefault(d => d.id == id);
                string note = id == GameState.HeroId
                    ? $"戰力 {state.SparPower(id):0}　傷勢 {state.Protagonist.injury}"
                    : $"戰力 {state.SparPower(id):0}　好感 {CastDialogue.Score(state, member)}　傷勢 {member?.injury ?? 0}";
                note += $"　餘 {AssignmentSystem.Remaining(state, id)} 次";
                if (sparPicks.Count == 1 && order < 0)
                    note += $"\n對{state.SparName(sparPicks[0])}勝率 {state.SparOdds(id, sparPicks[0]):P0}";
                return new SparCard
                {
                    id = id, name = state.SparName(id), selected = order >= 0,
                    title = id == GameState.HeroId ? state.Protagonist.title : member?.title,
                    line = state.SparLine(id), note = note,
                    badge = order < 0 ? null : sparMelee ? $"出場 {order + 1}" : order == 0 ? "甲方" : "乙方",
                    portrait = id == GameState.HeroId
                        ? PortraitService.Resolve(PortraitService.IdOrDefault(state.Protagonist.portrait, state.Protagonist.lookId), state.Protagonist.gender)
                        : PortraitService.Resolve(PortraitService.IdOrDefault(member?.portrait, id), member?.gender)
                };
            }).ToList();
            string block = state.SparBlock(sparPicks, sparMelee);
            string lineup = sparPicks.Count == 0 ? "點選出場的同門" : string.Join(sparMelee ? "、" : " 對 ", sparPicks.Select(state.SparName));
            if (!sparMelee && sparPicks.Count == 2)
                lineup += $"　（{state.SparName(sparPicks[0])}勝率 {state.SparOdds(sparPicks[0], sparPicks[1]):P0}）";
            string status = $"{lineup}　｜　行動力 {state.ActionPoints}／{state.ApMax}　糧食 {state.Food}";
            bool heroIn = sparPicks.Contains(GameState.HeroId);
            ui.ShowSparPicker(cards, sparMelee, status, block, ToggleSpar, melee =>
            {
                sparMelee = melee;
                while (sparPicks.Count > (melee ? 4 : 2)) sparPicks.RemoveAt(0);
                ShowSparPicker();
            }, RunSparSimulation, heroIn ? RunSparBattle : null, () => CloseScene());
        }

        private void ToggleSpar(string id)
        {
            if (!sparPicks.Remove(id))
            {
                sparPicks.Add(id);
                while (sparPicks.Count > (sparMelee ? 4 : 2)) sparPicks.RemoveAt(0);
            }
            UiSfx.Play("click");
            ShowSparPicker();
        }

        private void RunSparSimulation()
        {
            if (state.SparBlock(sparPicks, sparMelee) != null || !state.PaySpar(sparPicks)) return;
            var picks = sparPicks.ToList();
            var result = state.SimulateSpar(picks);
            PlaySparResult(picks, result);
        }

        /// <summary>The protagonist fights for real: their side is them (plus every other second pick), the rest oppose.</summary>
        private void RunSparBattle()
        {
            if (state.SparBlock(sparPicks, sparMelee) != null || !sparPicks.Contains(GameState.HeroId) || !state.PaySpar(sparPicks)) return;
            var picks = sparPicks.ToList();
            var others = picks.Where(id => id != GameState.HeroId).ToList();
            var party = new List<string> { GameState.HeroId };
            var opponents = new List<string>();
            for (int i = 0; i < others.Count; i++)
                (i % 2 == 1 && party.Count < opponents.Count + 1 ? party : opponents).Add(others[i]);
            var request = new BattleRequest
            {
                ground = BattleGround.MountainGate,
                enemyLeadName = state.SparName(opponents[0]),
                sparParty = party.ToArray(),
                sparOpponents = opponents.ToArray(),
                difficulty = 1
            };
            SceneBattle(request, won => PlaySparResult(picks, state.SettleBattle(party, opponents, won)));
        }

        private void PlaySparResult(IList<string> picks, SparResult result)
        {
            AudioManager.Play("complete");
            SceneLine Figure(string id)
            {
                if (id == GameState.HeroId) return HeroFigure();
                var member = state.Disciples.FirstOrDefault(d => d.id == id);
                return member == null ? null : new SceneLine
                {
                    name = member.name, subtitle = member.title, side = "right",
                    figure = CharacterFactory.StandeeTexture(PortraitService.IdOrDefault(member.portrait, member.id), member.gender, portraitFallback: true)
                };
            }
            // Our side on the left (the protagonist when present), the first other fighter on the right.
            string leftId = picks.Contains(GameState.HeroId) ? GameState.HeroId : picks[0];
            string rightId = picks.FirstOrDefault(id => id != leftId);
            var leftFigure = Figure(leftId);
            if (leftFigure != null) leftFigure.side = "left";
            ui.Scene.SetFigures(leftFigure, Figure(rightId));
            var lines = result.narration.Select(Narration).ToList();
            foreach (var (speaker, text) in result.reactions)
            {
                var figure = Figure(speaker);
                if (figure == null) continue;
                lines.Add(new SceneLine { name = figure.name, subtitle = figure.subtitle, side = "right", figure = figure.figure, text = text });
            }
            ui.Scene.Say(lines, () =>
            {
                var rows = result.standings.Select(entry => (entry.place == 1 ? "act_attack" : "hud_disciple",
                    $"第 {entry.place} 名　{entry.name}" + (picks.Count > 2 ? $"（{entry.wins} 勝）" : "") + $"　{entry.gains}")).ToList();
                var log = new List<string>
                {
                    $"消耗　行動力 {state.SparApCost}、糧食 {state.SparFoodCost}",
                    picks.Count > 2 ? "多人混戰：每兩人對打一場，勝場多者名次在前。" : "兩人對練：點到為止，勝者進境更多。"
                };
                ui.Scene.Result($"演武結果・{result.Winner}拔得頭籌", rows, log, "收劍回院",
                    () => CloseScene($"山門演武：{result.Winner}拔得頭籌。"), "再比一場", () =>
                    {
                        ui.Refresh(state);
                        ShowSparPicker();
                    });
            });
        }
    }
}
