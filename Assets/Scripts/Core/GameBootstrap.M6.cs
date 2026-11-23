using System;
using System.Collections.Generic;
using System.Linq;

namespace Wuxia
{
    /// <summary>M6 flows: the 藏經閣 tech-tree window and the three-floor 江湖秘境 (qi-yu dungeon).</summary>
    public sealed partial class GameBootstrap
    {
        private RealmRun realmRun;
        private string techMessage;

        // ---- Tech tree --------------------------------------------------------------------------

        private void ShowTechTree()
        {
            if (state == null) return;
            if (choosingBuilding) CancelBuild();
            ui.CloseBuilding();
            ui.CloseDialogue();
            var header = new List<string>
            {
                $"銀兩 {state.Silver}　行動力 {state.ActionPoints}／{state.ApMax}　已研習 {state.TechCount}／{GameState.TechTree.Length}" +
                "　　點擊「可研習」的功法即可研習；每脈須由第一重依序研習。"
            };
            if (!string.IsNullOrEmpty(techMessage))
                header.Add(techMessage);
            var branches = GameState.TechBranches.Select(branch => (branch.name, branch.motto,
                GameState.TechTree.Where(node => node.branch == branch.id).OrderBy(node => node.tier).Select(node =>
                {
                    bool researched = state.HasTech(node.id);
                    bool unlocked = !researched && (node.requires == null || state.HasTech(node.requires));
                    bool affordable = state.CanResearch(node);
                    string status = state.TechStatus(node);
                    if (unlocked && !affordable)
                        status += state.Silver < node.silver ? "　銀兩不足" : "　行動力不足";
                    return new TechCard
                    {
                        name = node.name, cost = $"銀 {node.silver}・行動力 {node.ap}", effect = node.effect,
                        status = status, researched = researched, unlocked = unlocked,
                        research = affordable ? () => ResearchTech(node) : null
                    };
                }).ToArray())).ToArray();
            ui.ShowTechTree(header, branches, CloseTechTree);
        }

        private void ResearchTech(TechNode node)
        {
            techMessage = state.Research(node);
            AudioManager.Play(state.HasTech(node.id) ? "complete" : "page");
            ui.Refresh(state);
            ShowCommands();
            ShowTechTree();
        }

        private void CloseTechTree()
        {
            techMessage = null;
            ui.CloseModal(force: true);
            ui.Refresh(state);
            ShowCommands();
        }

        // ---- Secret realm -----------------------------------------------------------------------

        private void OfferSecretRealm(string discovery)
        {
            ui.ShowModal("發現江湖秘境", new[] { discovery, "要立即深入探尋嗎？（不另耗行動力）" }, new (string, Action)[]
            {
                ("改日再探", () => ui.CloseModal()),
                ("立即深入", () =>
                {
                    ui.CloseModal(force: true);
                    EnterSecretRealm("意外發現");
                })
            });
        }

        // v1124 C15: EnterSecretRealm / floors / ClaimRealm / LeaveRealm now play as scenes (GameBootstrap.Realms.cs).
    }
}
