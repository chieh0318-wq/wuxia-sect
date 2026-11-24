using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    /// <summary>
    /// v1124 C15 / C16: 山道巡訪 and 秘境探尋 as scenes (background, encounter dialogue, optional turn battle, reward page)
    /// and the fixed one-time 寶物秘境 entered from the world map.
    /// </summary>
    public sealed partial class GameBootstrap
    {
        private TreasureRun treasureRun;

        // ---- Shared scene helpers ------------------------------------------------------------------

        private SceneLine HeroFigure(string text = null) => new()
        {
            name = state.Protagonist.name,
            subtitle = $"{state.SectLabel}　{state.Protagonist.title}",
            side = "left",
            text = Fill(text),
            figure = CharacterFactory.HeroOriginStandee(state.Protagonist.gender,
                state.Protagonist.lookId ?? state.Protagonist.portrait, state.Protagonist.backgroundId, state.Protagonist.title)
        };

        private static Texture2D FigureArt(string portrait, string gender, string standee)
        {
            if (!string.IsNullOrEmpty(standee)) return Resources.Load<Texture2D>(standee);
            if (string.IsNullOrEmpty(portrait) || portrait.StartsWith("beast_", StringComparison.Ordinal)) return null;
            return CharacterFactory.StandeeTexture(portrait, gender, portraitFallback: true);
        }

        private SceneLine NpcFigure(string name, string subtitle, string portrait, string gender, string standee, string text = null) => new()
        {
            name = name, subtitle = subtitle, side = "right", text = Fill(text), figure = FigureArt(portrait, gender, standee)
        };

        private static SceneLine Narration(string text) => new() { side = "narrator", text = text };

        /// <summary>Realm dialogue addresses the expedition leader.</summary>
        private string Fill(string text) => text?.Replace("{俠}", (ExpeditionMember?.gender ?? state.Protagonist.gender) == "女" ? "女俠" : "少俠")
            .Replace("{hero}", state.SparName(ExpeditionLeader));

        private SceneLine Speak(RealmLine line, SceneLine npc)
        {
            switch (line?.speaker)
            {
                case "hero": return ExpeditionFigure(line.text);
                case "npc":
                case "guardian":
                    if (npc == null) return Narration(Fill(line.text));
                    return new SceneLine { name = npc.name, subtitle = npc.subtitle, side = "right", figure = npc.figure, text = Fill(line.text) };
                default: return Narration(Fill(line?.text));
            }
        }

        private void LeaveCourtyardForScene()
        {
            if (choosingBuilding) CancelBuild();
            ui.CloseBuilding();
            ui.CloseDialogue();
            if (controls != null) controls.enabled = false;
        }

        private string[] expeditionParty;
        private string ExpeditionLeader => expeditionParty?.FirstOrDefault() ?? GameState.HeroId;
        private DiscipleData ExpeditionMember => state.Disciples.Find(member => member.id == ExpeditionLeader);
        private string ExpeditionInjury => $"{state.SparName(ExpeditionLeader)}傷勢 {ExpeditionMember?.injury ?? state.Protagonist.injury}";

        private SceneLine ExpeditionFigure(string text = null)
        {
            var member = ExpeditionMember;
            if (member == null) return HeroFigure(text);
            return new SceneLine
            {
                name = member.name, subtitle = AssignmentSystem.RankOf(state, member.id).Name,
                side = "left", text = Fill(text),
                figure = CharacterFactory.StandeeTexture(PortraitService.IdOrDefault(member.portrait, member.id), member.gender,
                    portraitFallback: true)
            };
        }

        /// <summary>Hides the scene, fights on the battle screen, then returns to the scene with the result.</summary>
        private void SceneBattle(BattleRequest request, Action<bool> after)
        {
            request.expeditionParty = expeditionParty;
            ui.Scene.Hide();
            request.onComplete = won =>
            {
                if (controls != null) controls.enabled = false;
                ui.Refresh(state);
                if (won && request.expeditionParty != null)
                    foreach (string id in request.expeditionParty) AssignmentSystem.Credit(state, id, 2);
                after(won);
            };
            ui.StartTurnBattle(state, request, this);
        }

        private void CloseScene(string message = null)
        {
            expeditionParty = null;
            ui.Scene.Close();
            if (controls != null) controls.enabled = true;
            ui.Refresh(state);
            ShowCommands();
            if (!string.IsNullOrEmpty(message)) ui.Message(message);
            if (!capturing) state.Save();
        }

        private static List<(string, string)> Rows(IEnumerable<GameEffect> effects, IList<string> texts)
        {
            var list = (effects ?? Array.Empty<GameEffect>()).Where(effect => effect != null).ToList();
            var rows = new List<(string, string)>();
            for (int i = 0; i < texts.Count; i++)
                rows.Add((i < list.Count ? SceneStage.RewardIcon(list[i]) : "act_item", texts[i]));
            return rows;
        }

        private string AttrTag(GameState state, string attr, float chance) =>
            $"{SecretRealms.AttrName(attr)} {AssignmentSystem.Attribute(state, ExpeditionLeader, attr)}・成功率 {chance:P0}";

        // ---- 山道巡訪 (C15) --------------------------------------------------------------------------

        /// <summary>Road patrol scene. AP / cost are already paid. Keeps the old 奇遇 breakthrough and realm discovery rolls.</summary>
        private void ExploreRoad(ActionData action)
        {
            float fortune = .012f * (AssignmentSystem.Attribute(state, ExpeditionLeader, "luk") - AttributeSystem.Base)
                + .003f * (AssignmentSystem.Attribute(state, ExpeditionLeader, "agi") - AttributeSystem.Base);
            float rumor = state.ConsumeRumors();
            string breakthrough = state.TryBreakthrough("qiyu_cliff_cave", state.Story.insightChance + fortune + rumor);
            bool realm = UnityEngine.Random.value < Mathf.Clamp(.22f + 2 * fortune + rumor, .05f, .9f);
            state.Apply(action.effect);
            var road = RealmContent.PickRoadEvent();
            if (road == null)
            {
                ui.Refresh(state);
                ShowCommands();
                ui.Message($"{action.name}完成。{breakthrough}");
                if (realm) OfferSecretRealm("山道盡頭的藤蔓後，竟藏著一道幽深的石門——是一處江湖秘境的入口！");
                return;
            }
            PlayRoadEvent(road, breakthrough, realm);
        }

        private void PlayRoadEvent(RoadEvent road, string breakthrough, bool realm)
        {
            LeaveCourtyardForScene();
            var npc = road.speaker == null ? null
                : NpcFigure(road.speaker.name, road.speaker.subtitle, road.speaker.portrait, road.speaker.gender, road.speaker.standee);
            ui.Scene.Begin(road.title, $"山道巡訪　{state.DateWithDay}", road.scene, ExpeditionFigure(), npc);
            AudioManager.Play("page");
            var lines = (road.lines ?? Array.Empty<RealmLine>()).Select(line => Speak(line, npc)).ToList();
            var prompt = lines.LastOrDefault();
            if (lines.Count > 1) lines.RemoveAt(lines.Count - 1);
            else lines.Clear();
            ui.Scene.Say(lines, () => RoadChoices(road, npc, prompt, breakthrough, realm));
        }

        private void RoadChoices(RoadEvent road, SceneLine npc, SceneLine prompt, string breakthrough, bool realm)
        {
            var cards = road.choices.Select(choice =>
            {
                string tags = choice.type switch
                {
                    "check" => AttrTag(state, choice.attr, state.RoadChance(choice, ExpeditionLeader)),
                    "battle" => $"回合實戰：{choice.battle?.lead}",
                    _ => null
                };
                return new SceneChoice
                {
                    label = choice.label, desc = choice.text, tags = tags, impact = state.RoadImpact(choice),
                    icon = choice.type == "battle" ? "act_attack" : choice.type == "check" ? UiKit.AttrIcon(AttributeSystem.Index(choice.attr)) : "act_item",
                    block = state.RoadChoiceBlock(choice),
                    run = () => ResolveRoadChoice(road, npc, choice, breakthrough, realm)
                };
            }).ToList();
            ui.Scene.Choose(prompt, new[] { ExpeditionInjury, $"行動力 {state.ActionPoints}／{state.ApMax}" }, cards,
                null, null);
        }

        private void ResolveRoadChoice(RoadEvent road, SceneLine npc, RoadChoice choice, string breakthrough, bool realm)
        {
            if (choice.cost != null && (choice.cost.silver > 0 || choice.cost.food > 0))
            {
                if (!state.CanPay(choice.cost)) return;
                state.Pay(choice.cost);
            }
            void Finish(bool success)
            {
                AudioManager.Play(success ? "complete" : "page");
                var effects = success ? choice.successEffects : choice.failureEffects;
                var gains = state.ResolveRoad(choice, success, ExpeditionLeader);
                string story = Fill(success ? choice.success : choice.failure);
                ui.Scene.Say(new[] { Narration(story) }, () =>
                {
                    var log = new List<string> { $"【{road.title}】{choice.label}：{(choice.type == "plain" ? "" : success ? "成功。" : "失手。")}{story}" };
                    if (!string.IsNullOrEmpty(breakthrough)) log.Add("奇遇　" + breakthrough);
                    if (realm) log.Add("山道盡頭的藤蔓後，竟藏著一道幽深的石門——是一處江湖秘境的入口！");
                    var rows = Rows(effects, gains);
                    if (rows.Count == 0) rows.Add(("act_item", "沒有額外所得"));
                    string summary = string.Join("、", gains.Take(2));
                    if (realm)
                        ui.Scene.Result(road.title, rows, log, "深入秘境", () => EnterSecretRealm("巡山途中意外發現"), "改日再探",
                            () => CloseScene($"{road.title}：{summary}"));
                    else
                        ui.Scene.Result(road.title, rows, log, "回山", () => CloseScene($"{road.title}：{summary}"));
                });
            }
            switch (choice.type)
            {
                case "check":
                    Finish(UnityEngine.Random.value < state.RoadChance(choice, ExpeditionLeader));
                    break;
                case "battle" when choice.battle != null:
                    SceneBattle(RealmContent.Battle(choice.battle, BattleGround.Road), Finish);
                    break;
                default:
                    Finish(true);
                    break;
            }
        }

        // ---- 秘境探尋 (C15): the three-floor random realms as scenes --------------------------------------

        private void EnterSecretRealm(string source)
        {
            if (expeditionParty == null && !capturing)
            {
                ui.ChooseAssignment(state, "秘境探尋", "secret_realm", 3, party =>
                {
                    if (!AssignmentSystem.Commit(state, party)) return;
                    expeditionParty = party;
                    foreach (string id in party) AssignmentSystem.Credit(state, id, 1);
                    StartSecretRealm(source);
                });
                return;
            }
            StartSecretRealm(source);
        }

        private void StartSecretRealm(string source)
        {
            LeaveCourtyardForScene();
            ui.CloseModal(force: true);
            realmRun = new RealmRun { realm = SecretRealms.Pick() };
            realmRun.log.Clear();
            ui.Scene.Begin(realmRun.realm.name, "江湖秘境", realmRun.realm.scene, ExpeditionFigure(), null);
            AudioManager.Play("page");
            ui.Scene.Say(new[] { Narration($"{source}，你來到{realmRun.realm.name}。{realmRun.realm.intro}") }, () => ShowRealmStage());
        }

        private void ShowRealmStage(string source = null)
        {
            var run = realmRun;
            if (run == null) return;
            var stage = run.realm.stages[run.stage];
            bool bossFloor = run.realm.bossStage == run.stage && run.realm.boss != null;
            var boss = bossFloor ? run.realm.boss : null;
            ui.Scene.SetFigures(ExpeditionFigure(), boss == null ? null : NpcFigure(boss.lead, "秘境守護", boss.portrait, "男", boss.standee));
            var cards = stage.choices.Select(choice => new SceneChoice
            {
                label = choice.label, desc = choice.text, icon = UiKit.AttrIcon(AttributeSystem.Index(choice.attr)),
                tags = AttrTag(state, choice.attr, SecretRealms.Chance(state, choice, run.stage, ExpeditionLeader)),
                impact = "成：闖關成績 +1　敗：主角傷勢 +1",
                run = () => ResolveRealm(choice)
            }).ToList();
            if (boss != null)
                cards.Add(new SceneChoice
                {
                    label = $"拔劍一戰：{boss.lead}", desc = "不取巧，正面擊敗守護者。", tags = "回合實戰", icon = "act_attack",
                    impact = "勝：闖關成績 +2（秘寶品級更高）　敗：主角傷勢 +1",
                    run = () => SceneBattle(RealmContent.Battle(boss, BattleGround.MountainGate), won =>
                    {
                        var dummy = new RealmChoice { label = $"與{boss.lead}正面交鋒", attr = "str",
                            success = $"{boss.lead}轟然倒下，前路再無阻攔。", failure = $"{boss.lead}太過強悍，你負傷突圍。" };
                        if (won) run.score++;
                        RecordRealm(run, dummy, won);
                    })
                });
            ui.Scene.Choose(Narration($"【{SecretRealms.StageTitles[run.stage]}】{stage.text}"),
                new[] { $"闖關成績 {run.score}／{run.stage}", ExpeditionInjury, "成功越多，秘寶品級越高" },
                cards, "撤出秘境", LeaveRealm);
        }

        private void ResolveRealm(RealmChoice choice)
        {
            var run = realmRun;
            if (run == null) return;
            bool success = SecretRealms.Resolve(state, run, choice, ExpeditionLeader);
            AfterRealmFloor(run, success);
        }

        /// <summary>A battle on a realm floor: same bookkeeping as an attribute check.</summary>
        private void RecordRealm(RealmRun run, RealmChoice choice, bool success)
        {
            if (success) run.score++;
            else state.InjureWorker(ExpeditionLeader);
            run.log.Add($"{SecretRealms.StageTitles[run.stage]}　{choice.label}{(success ? "得勝" : "失利")}：" +
                (success ? choice.success : choice.failure));
            run.stage++;
            AfterRealmFloor(run, success);
        }

        private void AfterRealmFloor(RealmRun run, bool success)
        {
            AudioManager.Play(success ? "complete" : "page");
            var line = Narration(run.log.Last() + (success ? "　旗開得勝！" : "　雖然失手負傷，仍咬牙繼續深入。"));
            if (run.stage >= run.realm.stages.Length)
                ui.Scene.Say(new[] { line }, ClaimRealm, "開啟秘寶");
            else
                ui.Scene.Say(new[] { line }, () => ShowRealmStage(), "深入下一層");
        }

        private void ClaimRealm()
        {
            var run = realmRun;
            if (run == null) return;
            var rewards = SecretRealms.Claim(state, run);
            realmRun = null;
            AudioManager.Play("coin");
            var rows = rewards.Select(text => (text.StartsWith("秘寶", StringComparison.Ordinal) ? UiKit.SlotIcon(run.realm.gearSlot)
                : text.StartsWith("銀兩", StringComparison.Ordinal) ? "hud_silver" : UiKit.SkillIcon(run.realm.line), text)).ToList();
            var log = new List<string> { $"三層闖關成績　{Mathf.Min(run.score, 3)}／3" };
            log.AddRange(run.log);
            ui.Scene.Result($"秘境凱旋・{run.realm.name}", rows, log, "收下秘寶", () =>
                CloseScene($"自{run.realm.name}滿載而歸。{rewards.FirstOrDefault()}"));
        }

        private void LeaveRealm()
        {
            var run = realmRun;
            realmRun = null;
            CloseScene(run == null ? "" : $"你撤出了{run.realm.name}，改日再來。");
        }

        // ---- 寶物秘境 (C16) ---------------------------------------------------------------------------

        /// <summary>Entered from the world map card: closes the map, spends 1 AP and plays the site.</summary>
        private void EnterTreasure(TreasureSite site)
        {
            if (capturing) { StartTreasure(site, new[] { GameState.HeroId }); return; }
            if (map != null && map.IsOpen)
            {
                ui.ChooseMapAssignment(state, site.name, party => StartTreasure(site, party));
                return;
            }
            ui.ChooseAssignment(state, site.name, "secret_realm", 3, party => StartTreasure(site, party));
        }

        private void StartTreasure(TreasureSite site, string[] party)
        {
            string block = state.TreasureBlock(site);
            if (block != null)
            {
                ui.Message(block);
                return;
            }
            if (!capturing && site.node != state.StartNode)
            {
                string result = state.SendTreasureDispatch(site, party);
                if (map != null && map.IsOpen) map.Close();
                ui.Refresh(state);
                ui.Message(result);
                return;
            }
            if (map != null && map.IsOpen) map.Close();
            if (party.Any(id => AssignmentSystem.Block(state, id) != null) ||
                !state.TrySpendAp(1, "treasure:" + site.id)) return;
            AssignmentSystem.Commit(state, party);
            BeginTreasureScene(site, party);
        }

        private void BeginTreasureScene(TreasureSite site, string[] party)
        {
            expeditionParty = party;
            foreach (string id in party) AssignmentSystem.Credit(state, id, 1);
            LeaveCourtyardForScene();
            treasureRun = new TreasureRun { site = site };
            var guardian = site.Guarded ? GuardianFigure(site.guardian) : null;
            ui.Scene.Begin(site.name, $"寶物秘境・{WorldMapData.Load().NodeLabel(site.node)}　{site.KindLabel}　{site.TypeLabel}：{site.treasure}",
                site.scene, ExpeditionFigure(), null);
            AudioManager.Play("page");
            ui.Scene.Say(new[] { Narration(site.intro) }, () => TreasureStage(guardian));
        }

        private SceneLine GuardianFigure(TreasureGuardian guardian) =>
            NpcFigure(guardian.name, guardian.subtitle, guardian.portrait, guardian.gender, guardian.standee);

        private void TreasureStage(SceneLine guardian)
        {
            var run = treasureRun;
            if (run == null) return;
            if (run.stage >= run.site.stages.Length)
            {
                if (run.site.Guarded) FaceGuardian(guardian);
                else ClaimTreasure();
                return;
            }
            var stage = run.site.stages[run.stage];
            ui.Scene.SetFigures(ExpeditionFigure(), null);
            var cards = stage.choices.Select(choice => new SceneChoice
            {
                label = choice.label, desc = choice.text, icon = UiKit.AttrIcon(AttributeSystem.Index(choice.attr)),
                tags = AttrTag(state, choice.attr, state.TreasureChance(choice, run.stage, ExpeditionLeader)),
                impact = "成：順利前進　敗：主角傷勢 +1（仍可前進）",
                run = () =>
                {
                    bool success = state.ResolveTreasureStage(run, choice, ExpeditionLeader);
                    AudioManager.Play(success ? "complete" : "page");
                    ui.Scene.Say(new[] { Narration(run.log.Last()) }, () => TreasureStage(guardian), "繼續深入");
                }
            }).ToList();
            ui.Scene.Choose(Narration(stage.text),
                new[] { $"第 {run.stage + 1}／{run.site.stages.Length} 關", ExpeditionInjury, $"秘寶：{run.site.treasure}" },
                cards, "撤出秘境", () => LeaveTreasure(false));
        }

        private void FaceGuardian(SceneLine guardian)
        {
            var run = treasureRun;
            var keeper = run.site.guardian;
            ui.Scene.SetFigures(ExpeditionFigure(), guardian);
            var lines = (keeper.lines ?? Array.Empty<RealmLine>()).Select(line => Speak(line, guardian)).ToList();
            string rewards = EffectText.Describe(run.site.rewards, state);
            ui.Scene.Say(lines, () => ui.Scene.Choose(
                new SceneLine { name = keeper.name, subtitle = keeper.subtitle, side = "right", figure = guardian?.figure,
                    text = $"{keeper.name}擋在秘寶之前。要取{run.site.treasure}，就得擊敗他。" },
                new[] { ExpeditionInjury, "鎮守者極強，可先回山休整" },
                new[]
                {
                    new SceneChoice
                    {
                        label = $"迎戰{keeper.name}", desc = "回合實戰，勝者取寶。", tags = keeper.boss ? "首領戰" : "實戰", icon = "act_attack",
                        impact = $"勝：{rewards}　敗：主角傷勢 +1，可改日再來",
                        run = () => SceneBattle(RealmContent.Battle(keeper), won =>
                        {
                            AudioManager.Play(won ? "complete" : "page");
                            if (won)
                                ui.Scene.Say(new[] { Narration(Fill(keeper.win)) }, ClaimTreasure, "取寶");
                            else
                            {
                                state.InjureWorker(ExpeditionLeader);
                                ui.Scene.Say(new[] { Narration(Fill(keeper.lose)) }, () => LeaveTreasure(true), "撤出");
                            }
                        })
                    },
                    new SceneChoice
                    {
                        label = "暫且退去", desc = "養好傷、練好武功再來。", icon = "act_defend", impact = "秘寶留在原處，地圖上仍標示",
                        run = () => LeaveTreasure(false)
                    }
                }, null, null));
        }

        private void ClaimTreasure()
        {
            var run = treasureRun;
            if (run == null) return;
            treasureRun = null;
            var rewards = state.ClaimTreasure(run.site);
            AudioManager.Play("coin");
            var log = new List<string>(run.log) { $"{run.site.name}的秘寶已歸{state.SectLabel}所有，地圖上的標記將改為「已取」。" };
            ui.Scene.Result($"取得秘寶・{run.site.treasure}", Rows(run.site.rewards, rewards), log, "收下秘寶", () =>
                CloseScene($"自{run.site.name}取得{run.site.treasure}。"));
        }

        private void LeaveTreasure(bool defeated)
        {
            var run = treasureRun;
            treasureRun = null;
            var log = run?.log.ToList() ?? new List<string>();
            log.Add(defeated ? "鎮守者太強，你負傷退出。秘寶仍在原處等著你。" : "你撤出了秘境，秘寶仍在原處。");
            ui.Scene.Result(run?.site.name ?? "寶物秘境", new[] { ("act_defend", ExpeditionInjury) }, log, "回山",
                () => CloseScene(run == null ? null : $"你退出了{run.site.name}。"));
        }
    }
}
