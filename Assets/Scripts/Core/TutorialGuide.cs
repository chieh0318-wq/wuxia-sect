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
        private bool finished;

        internal bool Active => !finished && title != null;
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
            AudioManager.Music("prologue"); // M2-B opening music
            ShowIntro();
        }

        private string Format(string text) => (text ?? "").Replace("{title}", title ?? "首徒")
            .Replace("{sect}", state.Data.Start.sectName);

        private void ShowIntro()
        {
            if (intro < data.intro.Length)
            {
                var line = data.intro[intro++];
                ui.ShowModal(Format(line.speaker), new[] { Format(line.text) },
                    new[] { ("繼續", (Action)ShowIntro) }, dismissible: false);
                return;
            }
            var choices = Array.ConvertAll(data.titles, option => (option, (Action)(() => Choose(option))));
            ui.ShowModal("首徒稱謂",new[] { Format(data.choosePrompt) }, choices, dismissible: false);
        }

        private void Choose(string option)
        {
            title = option;
            AudioManager.Music("courtyard"); // M2-B return to courtyard music
            ui.CloseModal(force: true);
            Advance();
        }

        internal void ChooseForCapture() => Choose(data.titles[0]);

        internal void Record(string trigger, string target = "")
        {
            // Camera events arrive every frame while moving; only re-evaluate on a new action.
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
