using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    public sealed partial class SectUI
    {
        internal void ChooseAssignment(GameState state, string title, string actionId, int limit,
            Action<string[]> onConfirm, Action onBack = null, Func<string, float> chance = null)
        {
            var picked = new List<string>();
            void Back()
            {
                AssignmentBack = null;
                CloseModal(force: true);
                onBack?.Invoke();
            }
            void Draw()
            {
                var buttons = new List<(string, Action)> { ("返回", Back) };
                if (picked.Count > 0) buttons.Add(("確認派遣", () =>
                {
                    AssignmentBack = null;
                    CloseModal(force: true);
                    onConfirm(picked.ToArray());
                }));
                ShowCreation(title, new[] { limit == 1 ? "挑選一位執行此事。"
                    : $"可派 1～{limit} 人。首選者領隊，戰鬥與途中判定使用此次隊伍。" }, buttons, null);
                foreach (string id in new[] { GameState.HeroId }.Concat(state.Disciples.Select(member => member.id)))
                {
                    var rank = AssignmentSystem.RankOf(state, id);
                    var prediction = AssignmentSystem.Predict(state, actionId, id);
                    string block = AssignmentSystem.Block(state, id);
                    int index = picked.IndexOf(id);
                    string mark = index < 0 ? "" : index == 0 ? "【領隊】" : "【同行】";
                    string condition = limit > 1 && actionId == "explore" ? "（幸運判定參考）" : "";
                    string preview = actionId == "campaign" && chance == null
                        ? $"位階帶兵基數 {rank.Troops}・統率依悟性與魅力計算" : $"{prediction.Detail}\n預估成功率 {chance?.Invoke(id) ?? prediction.Success:P0}{condition}　預估品質 {prediction.Quality:P0}";
                    CreationChoice($"{mark}{state.SparName(id)}　{rank.Name}　餘 {AssignmentSystem.Remaining(state, id)}／{rank.Monthly} 次",
                        block ?? preview, null, block == null ? (Action)(() =>
                        {
                            if (index >= 0) picked.RemoveAt(index);
                            else if (limit == 1) { picked.Clear(); picked.Add(id); }
                            else if (picked.Count < limit) picked.Add(id);
                            Draw();
                        }) : null, picked: index >= 0);
                }
            }
            Draw();
            AssignmentBack = Back;
        }

        internal void ChooseDiplomaticAssignment(GameState state, DiploAction action, MapFaction faction, Action<string> selected)
        {
            ChooseAboveMap(state, action.name, "diplomacy", 1,
                party => selected(party[0]), id => state.DiploChance(action, faction, id));
        }

        internal void ChoosePatronEnvoy(GameState state, string title, Action<string> send) =>
            ChooseAboveMap(state, title, "diplomacy", 1, party => send(party[0]), state.PatronChance);

        internal void ChooseCampaignAssignment(GameState state, string title, Action<string[]> selected) =>
            ChooseAboveMap(state, title, "campaign", 10, selected);

        internal void ChooseMapAssignment(GameState state, string title, Action<string[]> selected) =>
            ChooseAboveMap(state, title, "explore", 3, selected);

        private void ChooseAboveMap(GameState state, string title, string actionId, int limit,
            Action<string[]> selected, Func<string, float> chance = null)
        {
            var layer = canvas.GetComponent<Canvas>();
            int order = layer.sortingOrder;
            layer.sortingOrder = 60;
            void Restore() => layer.sortingOrder = order;
            ChooseAssignment(state, title, actionId, limit,
                party => { Restore(); selected(party); }, Restore, chance);
        }

        private Action AssignmentBack
        {
            set
            {
                UiBack.Pop(assignmentBackOwner);
                if (value != null) UiBack.Push(value, () => ModalOpen, assignmentBackOwner);
            }
        }

        private readonly object assignmentBackOwner = new();

        internal void TrackPanelBack(Action back) => AssignmentBack = back;
    }
}
