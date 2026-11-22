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

        private void EnterSecretRealm(string source)
        {
            if (choosingBuilding) CancelBuild();
            ui.CloseBuilding();
            ui.CloseDialogue();
            realmRun = new RealmRun { realm = SecretRealms.Pick() };
            realmRun.log.Clear();
            AudioManager.Play("page");
            ShowRealmStage(source);
        }

        private void ShowRealmStage(string source = null)
        {
            var run = realmRun;
            if (run == null) return;
            var stage = run.realm.stages[run.stage];
            var lines = new List<string>();
            if (run.stage == 0)
                lines.Add($"{source}，你來到{run.realm.name}。{run.realm.intro}");
            lines.Add(stage.text);
            lines.Insert(0, $"【{SecretRealms.StageTitles[run.stage]}】");
            lines.Add($"闖關成績 {run.score}／{run.stage}　主角傷勢 {state.Protagonist.injury}　（成功越多，秘寶品級越高）");
            ui.ShowRealmStage(run.realm.name, lines,
                stage.choices.Select(choice =>
                {
                    int value = AttributeSystem.Value(state.Protagonist.attrs, choice.attr);
                    float chance = SecretRealms.Chance(state, choice, run.stage);
                    return (choice.label, choice.text,
                        $"{SecretRealms.AttrName(choice.attr)} {value}・成功率 {chance:P0}", (Action)(() => ResolveRealm(choice)));
                }), LeaveRealm);
        }

        private void ResolveRealm(RealmChoice choice)
        {
            var run = realmRun;
            if (run == null) return;
            bool success = SecretRealms.Resolve(state, run, choice);
            AudioManager.Play(success ? "complete" : "page");
            string title = run.realm.name;
            var lines = new[] { run.log.Last(), success ? "旗開得勝！" : "雖然失手負傷，仍咬牙繼續深入。" };
            if (run.stage >= run.realm.stages.Length)
                ui.ShowRealmResult(title, lines, "開啟秘寶", ClaimRealm);
            else
                ui.ShowRealmResult(title, lines, "深入下一層", () => ShowRealmStage());
        }

        private void ClaimRealm()
        {
            var run = realmRun;
            if (run == null) return;
            var rewards = SecretRealms.Claim(state, run);
            var lines = new List<string> { $"三層闖關成績　{run.score}／3" };
            lines.AddRange(run.log);
            lines.Add("—— 秘境所得 ——");
            lines.AddRange(rewards);
            realmRun = null;
            AudioManager.Play("coin");
            ui.ShowRealmResult("秘境凱旋", lines.Prepend(run.realm.name), "收下秘寶", () =>
            {
                ui.CloseModal(force: true);
                ui.Refresh(state);
                ShowCommands();
                ui.Message($"自{run.realm.name}滿載而歸。{rewards.FirstOrDefault()}");
            });
        }

        private void LeaveRealm()
        {
            var run = realmRun;
            realmRun = null;
            ui.CloseModal(force: true);
            ui.Refresh(state);
            ShowCommands();
            ui.Message(run == null ? "" : $"你撤出了{run.realm.name}，改日再來。");
        }
    }
}
