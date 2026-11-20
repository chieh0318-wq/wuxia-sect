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
        internal string Highlight => Active ? data.steps[step].highlight : null;

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
                objective = "查看本月還能辦多少事。",
                hint = "將游標移到上方行動力，查看上限來源。",
                trigger = "inspect_ap", target = "", highlight = "ui:ap"
            });
            data.steps = steps.ToArray();
        }

        private string Format(string text) => (text ?? "").Replace("{title}", title ?? "首徒")
            .Replace("{sect}", state.Data.Start.sectName);

        private void ShowIntro()
        {
            if (intro < data.intro.Length)
            {
                var line = data.intro[intro++];
                var speaker = state.Data.Disciples.FirstOrDefault(member => !string.IsNullOrEmpty(member.name) && (line.speaker ?? "").EndsWith(member.name, StringComparison.Ordinal));
                ui.ShowModal(Format(line.speaker), new[] { Format(line.text) },
                    new[] { ("繼續", (Action)ShowIntro), ("略過", (Action)(() => Choose(title))) }, dismissible: false);
                if (speaker != null)
                    ui.ModalPortrait(speaker.portrait ?? speaker.id, speaker.gender);
                return;
            }
            Choose(title);
        }

        private void Choose(string option)
        {
            title = option;
            introComplete = true;
            ui.CloseModal(force: true);
            Advance();
        }

        internal void ChooseForCapture() => Choose(data.titles[0]);

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
            while (step < data.steps.Length &&
                actions.Contains((data.steps[step].trigger, data.steps[step].target ?? "")))
                step++;
            if (step < data.steps.Length)
            {
                var current = data.steps[step];
                ui.ShowObjective(Format(current.title), Format(current.objective), Format(current.hint), ConfirmSkip);
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
