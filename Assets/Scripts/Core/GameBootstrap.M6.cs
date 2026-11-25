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
                "　　點擊「可研習」即可研習；須依序研習，高重另需聲望、山門規模與主修品階。"
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
                    if (unlocked && !affordable && state.TechGate(node) == null)
                        status += state.Silver < node.silver ? "　銀兩不足" : state.ActionPoints < node.ap ? "　行動力不足" : "";
                    // v1126d CF2 #22: locked higher tiers also show their 聲望／山門／主修 gate up front.
                    if (!researched && !unlocked && GameState.TechGateSummary(node) is string gates)
                        status += $"\n另需{gates}";
                    return new TechCard
                    {
                        name = node.name, cost = $"銀 {node.silver}・行動力 {node.ap}", effect = node.effect,
                        status = status, researched = researched, unlocked = unlocked,
                        research = affordable ? () => ResearchTech(node) : null
                    };
                }).ToArray())).ToArray();
            ui.ShowTechTree(header, branches, CloseTechTree);
        }

        private void ResearchTech(TechNode node) =>
            ui.ChooseAssignment(state, node.name, "study", 1, party => ResearchTech(node, party[0]), ShowTechTree);

        private void ResearchTech(TechNode node, string worker)
        {
            techMessage = state.Research(node, worker);
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
