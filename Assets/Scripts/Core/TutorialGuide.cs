using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    [Serializable] internal sealed class TutorialLine { public string speaker, text; }
    [Serializable] internal sealed class TutorialStep
    {
        public string id, title, objective, hint, trigger, target, highlight;
        public Yield reward; // v1126c PC3 #15: a small reward when the step is done
    }
    [Serializable] internal sealed class TutorialData
    {
        public TutorialLine[] intro;
        public string choosePrompt, completeText;
        public string[] titles;
        public TutorialStep[] steps;
        public Yield reward;
    }

    internal sealed class TutorialGuide
    {
        private readonly SectUI ui;
        private readonly GameState state;
        private readonly TutorialData data;
        private readonly HashSet<(string trigger, string target)> actions = new();
        private int step;
        private int intro;
        private string title;
        private bool finished, introComplete;

        internal bool Active => !finished && introComplete;
        internal string Highlight
        {
            get
            {
                if (!Active || step >= data.steps.Length) return null;
                // v1126c PC3 #11: inside the build menu the guide points at the recommended blueprint itself.
                if (data.steps[step].trigger == "build_any" && ui.TargetActive("blueprint")) return "ui:blueprint";
                // v1126e #5: while points are left the guide points at 行動力, not at 「結束本月」.
                if (WaitingForAp) return "ui:ap";
                return data.steps[step].highlight;
            }
        }

        /// <summary>v1126c PC3 #11: the building the 擴建院落 step recommends (null when the step is not pending).</summary>
        internal string RecommendedBuilding =>
            CanFund("build_any") ? TutorialAdvice.Recommend(state)?.building.name : null;

        /// <summary>v1126 #8: the running guide step for the 任務 panel (read-only), or null once it is over.</summary>
        internal (string title, string objective)? Current =>
            Active && step < data.steps.Length ? (Format(Shown.title), Format(Shown.objective)) : null;

        /// <summary>
        /// v1126e #5: 「月底結算：本月事務已畢」 only once the month's points are spent (or nothing affordable is left);
        /// until then the card asks the player to use them.
        /// </summary>
        private bool WaitingForAp => Active && step < data.steps.Length && data.steps[step].trigger == "end_month" &&
            state.HasAffordableAction;

        private static readonly TutorialStep SpendApStep = new()
        {
            id = "month_spend_ap", title = "用完行動力", objective = "本月尚餘行動力 {ap}／{apMax}，先把事情辦完。",
            hint = "常用指令：{actionCosts}。行動力用完（或已沒有辦得起的事）時，再點右上角「結束本月」。" +
                   "行動力有剩時按「結束本月」，會先問你是否確定。",
            trigger = "end_month", target = "", highlight = "ui:ap"
        };

        private TutorialStep Shown => WaitingForAp ? SpendApStep : data.steps[step];

        internal TutorialGuide(SectUI ui, GameState state)
        {
            this.ui = ui;
            this.state = state;
            string path = Path.Combine(Application.streamingAssetsPath, "Data", "tutorial.json");
            data = JsonUtility.FromJson<TutorialData>(File.ReadAllText(path));
            if (data?.intro == null || data.intro.Any(line => line == null) ||
                data.titles == null || data.titles.Length != 2 || data.titles.Any(string.IsNullOrWhiteSpace) ||
                data.steps == null || data.steps.Any(step => step == null || string.IsNullOrWhiteSpace(step.trigger)) ||
                data.reward == null)
                throw new InvalidDataException("Tutorial data is incomplete.");
            AddApStep();
            title = state.Protagonist.title;
            ShowIntro();
        }

        private void AddApStep()
        {
            int buildStep = Array.FindIndex(data.steps, entry => entry.trigger == "build_any");
            if (buildStep < 0 || data.steps.Any(entry => entry.trigger == "inspect_ap"))
                return;
            var steps = data.steps.ToList();
            steps.Insert(buildStep + 1, new TutorialStep
            {
                id = "action_points", title = "清點行動力",
                objective = "看看本月還能辦幾件事。",
                hint = "{apLine}\n常用指令：{actionCosts}。月底補滿，用剩的不留到下月。將游標移到上方行動力（箭頭所指）看明細。",
                trigger = "inspect_ap", target = "", highlight = "ui:ap"
            });
            data.steps = steps.ToArray();
        }

        private string Format(string text)
        {
            text = (text ?? "").Replace("{title}", title ?? "掌門").Replace("{sect}", state.Data.Start.sectName);
            return text.Contains("{") ? Live(text) : text;
        }

        /// <summary>v1126c PC3 #11 / #15: live numbers in step text, read from the running game state.</summary>
        private string Live(string text)
        {
            if (text.Contains("{build"))
            {
                var pick = TutorialAdvice.Recommend(state);
                if (pick.HasValue)
                {
                    var total = TutorialAdvice.BuildTotal(state, pick.Value.building);
                    text = text.Replace("{buildPick}", pick.Value.building.name)
                        .Replace("{buildYield}", TutorialAdvice.Yield(state, pick.Value.building))
                        .Replace("{buildReason}", pick.Value.reason)
                        .Replace("{buildCost}", total.silver + (total.food > 0 ? $"・糧 {total.food}" : ""));
                }
                else
                    text = text.Replace("{buildPick}", "任一屋舍").Replace("{buildYield}", "可增產出").Replace("{buildReason}", "")
                        .Replace("{buildCost}", "—");
            }
            var (foodIn, eaten) = TutorialAdvice.FoodPerMonth(state);
            var (silverIn, upkeep) = TutorialAdvice.SilverPerMonth(state);
            text = text.Replace("{apLine}", TutorialAdvice.ApLine(state)).Replace("{apMax}", state.ApMax.ToString())
                .Replace("{ap}", state.ActionPoints.ToString()).Replace("{actionCosts}", TutorialAdvice.ActionCosts(state))
                .Replace("{foodIn}", foodIn.ToString()).Replace("{eaten}", eaten.ToString())
                .Replace("{silverIn}", silverIn.ToString()).Replace("{upkeep}", upkeep.ToString())
                .Replace("{foodPer}", state.Data.Start.foodPerDisciple.ToString());
            int at;
            while ((at = text.IndexOf("{cost:", StringComparison.Ordinal)) >= 0)
            {
                int end = text.IndexOf('}', at);
                if (end < 0) break;
                string type = text.Substring(at + 6, end - at - 6);
                text = text.Substring(0, at) + TutorialAdvice.Cost(TutorialAdvice.Action(state, type)) + text.Substring(end + 1);
            }
            return text;
        }

        private float nextTick;
        private string shownText;

        /// <summary>v1126c PC3 #15: keeps the step card's numbers current (AP spent, silver paid) twice a second.</summary>
        internal void Tick()
        {
            if (!Active || step >= data.steps.Length || Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + .5f;
            ShowStep(false);
        }

        private void ShowStep(bool force)
        {
            var current = Shown;
            string hint = Format(current.hint);
            if (current.reward != null && GameBootstrap.Changes(current.reward) is string gift && gift.Length > 0)
                hint += $"\n完成可得：{gift}";
            string body = Format(current.objective);
            string key = current.id + body + hint;
            if (!force && key == shownText) return;
            shownText = key;
            ui.ShowObjective(Format(current.title), body, hint, ConfirmSkip);
        }

        private void ShowIntro()
        {
            if (intro < data.intro.Length)
            {
                var line = data.intro[intro++];
                var speaker = state.Data.Disciples.FirstOrDefault(member => !string.IsNullOrEmpty(member.name) && (line.speaker ?? "").EndsWith(member.name, StringComparison.Ordinal));
                // v1125 #4: the opening guide talks with the same large-portrait box as the rest of the game.
                string name = Format(line.speaker);
                int split = name.IndexOf('　');
                ui.ShowTalk(new PortraitLine(split > 0 ? name.Substring(split + 1) : name, split > 0 ? name.Substring(0, split) : "",
                        speaker == null ? null : speaker.portrait ?? speaker.id, speaker?.gender, Format(line.text)),
                    new[] { ("略過介紹", (Action)(() => Choose(title))) }, ShowIntro);
                return;
            }
            Choose(title);
        }

        private void Choose(string option)
        {
            title = option;
            introComplete = true;
            ui.CloseModal(force: true);
            ui.CloseTalk(false);
            Advance();
        }

        internal void ChooseForCapture() => Choose(data.titles[0]);

        /// <summary>v1126e capture: redraws the step card now (Tick does it twice a second in play).</summary>
        internal void RefreshForCapture() => ShowStep(true);

        internal void Record(string trigger, string target = "")
        {
            // Camera events arrive every frame while moving; only re-evaluate on a new action.
            if (trigger == "recruit" || trigger == "end_month")
                actions.Add(("inspect_ap", ""));
            if (finished || !actions.Add((trigger, target ?? "")))
                return;
            if (Active)
                Advance();
        }

        private void Advance()
        {
            var gifts = new List<string>();
            while (step < data.steps.Length &&
                actions.Contains((data.steps[step].trigger, data.steps[step].target ?? "")))
            {
                // v1126c PC3 #15: each finished step pays its small reward once.
                var done = data.steps[step];
                if (done.reward != null && GameBootstrap.Changes(done.reward) is string gift && gift.Length > 0)
                {
                    state.Apply(done.reward);
                    gifts.Add($"「{Format(done.title)}」{gift}");
                }
                step++;
            }
            if (gifts.Count > 0)
            {
                ui.Refresh(state);
                ui.Message("引導獎勵　" + string.Join("　", gifts));
            }
            if (step < data.steps.Length)
            {
                ShowStep(true);
                return;
            }

            Finish();
            state.Apply(data.reward);
            ui.Refresh(state);
            ui.Message($"{Format(data.completeText)}　{GameBootstrap.Changes(data.reward)}");
        }

        private void ConfirmSkip()
        {
            ui.ShowModal("略過引導", new[] { "確定略過新手引導？略過後仍可照常經營門派。" },
                new[] { ("繼續引導", (Action)(() => ui.CloseModal())),
                    ("略過引導", (Action)(() => { ui.CloseModal(); Finish(); })) });
        }

        private void Finish()
        {
            finished = true;
            ui.HideObjective();
        }

        internal bool CanFund(string trigger) => Active && !actions.Contains((trigger, ""));

        internal string Fund(string trigger, Cost cost, int extraSilver = 0)
        {
            if (!Active || actions.Contains((trigger, "")))
                return "";
            var grant = new Yield
            {
                silver = Mathf.Max(0, cost.silver + extraSilver - state.Silver),
                food = Mathf.Max(0, cost.food - state.Food)
            };
            if (grant.silver == 0 && grant.food == 0)
                return "";
            state.Apply(grant);
            return $"動用師門積蓄（{GameBootstrap.Changes(grant)}）。";
        }
    }
}
