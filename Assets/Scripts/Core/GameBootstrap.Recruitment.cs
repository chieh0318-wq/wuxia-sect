using System;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Wuxia
{
    [Serializable] internal sealed class RecruitCatalog
    {
        public int baseApCost = 1, refreshSilver = 5, maxAcceptPerVisit = 2;
        public RecruitMethod[] methods;
        public string[] surnames, givenMale, givenFemale;
        public RecruitOrigin[] origins;
        public RecruitPersonality[] personalities;
        public Candidate[] specials;
    }

    [Serializable] internal sealed class RecruitMethod
    {
        public string id, name, desc;
        public int apCost = 1, silverCost, poolSize = 4, quality;
        public float rareChance = .05f, productionBias;
        public float[] rankWeights, capWeights;
    }

    [Serializable] internal sealed class RecruitOrigin { public string name; public string[] blurbs; }
    [Serializable] internal sealed class RecruitPersonality { public string name; public int loyalty; }
    [Serializable] internal sealed class RecruitStory
    {
        public string prompt, acceptText, rejectText, risk;
        public GameEffect[] acceptEffects;
    }
    [Serializable] public sealed class Aptitude { public int bone, insight; }
    [Serializable] internal sealed class Candidate : DiscipleData
    {
        public int silverCost;
        public RecruitStory story;
    }

    public sealed partial class GameBootstrap
    {
        private RecruitCatalog recruitment;
        private RecruitCatalog Recruitment => recruitment ??= M2Data.Load<RecruitCatalog>("recruit");
        private TalentCatalog recruitTalents;
        private TalentCatalog RecruitTalents => recruitTalents ??= M2Data.Load<TalentCatalog>("talents");
        private Candidate[] candidates;
        private RecruitMethod activeMethod;
        private int accepted;
        private ActionData recruitmentAction;

        private void OpenRecruitment(ActionData action)
        {
            recruitmentAction = action;
            if (candidates != null)
            {
                ShowCandidates();
                return;
            }
            var methods = Recruitment.methods ?? new[]
            {
                new RecruitMethod { id = "public", name = "公開招徒", desc = "張貼告示，請附近鄉民來投帖。" },
                new RecruitMethod { id = "search", name = "江湖尋訪", desc = "派人沿商道尋訪有心習武之人。", silverCost = 8, quality = 1, rareChance = .2f },
                new RecruitMethod { id = "orphans", name = "收養孤兒", desc = "接納無依的孩子，給他們立身的機會。", poolSize = 3 }
            };
            ui.ShowCreation("招收弟子", new[] { $"銀兩 {state.Silver}　行動力 {state.ActionPoints}／{state.ApMax}" },
                new[] { ("返回", (Action)(() => ui.CloseModal(force: true))) }, null);
            foreach (var method in methods)
            {
                bool tutorialFunds = tutorial?.CanFund("recruit") == true;
                bool affordable = (state.ActionPoints >= method.apCost && state.Silver >= method.silverCost) || tutorialFunds;
                string reason = affordable ? "" : "　資源不足";
                ui.CreationChoice($"{method.name}　行動力 {method.apCost}／銀 {method.silverCost}{reason}",
                    method.desc, null, affordable ? () => Visit(method) : null);
            }
        }

        private void Visit(RecruitMethod method)
        {
            var cost = new Cost { silver = Mathf.Max(0, method.silverCost) };
            tutorial?.Fund("recruit", cost);
            if (!state.CanPay(cost))
                return;
            if (!state.TrySpendAp(method.apCost, method.id) &&
                !(tutorial?.CanFund("recruit") == true && state.TrySpendAp(state.ActionPoints, method.id)))
                return;
            state.Pay(cost);
            activeMethod = method;
            accepted = 0;
            FillCandidates();
        }

        private void FillCandidates()
        {
            int count = Mathf.Clamp(activeMethod.poolSize + state.Story.recruitPoolSize, 3, 5);
            candidates = Enumerable.Range(0, count).Select(_ => RollCandidate(activeMethod)).ToArray();
            if (activeMethod.id != "public" && Recruitment.specials?.Length > 0 && Random.value < activeMethod.rareChance)
            {
                var unseen = Recruitment.specials.Where(special => !state.Disciples.Any(disciple => disciple.id == special.id)).ToArray();
                if (unseen.Length > 0)
                {
                    var special = JsonUtility.FromJson<Candidate>(JsonUtility.ToJson(Pick(unseen)));
                    special.lines = RankSystem.Seed(special.lines, special.rank, special.cap, "B");
                    var personality = Recruitment.personalities?.FirstOrDefault(entry => entry.name == special.personality);
                    special.loyalty = Mathf.Clamp(50 + (personality?.loyalty ?? 0) + state.Story.loyaltyBonus, 0, 100);
                    candidates[count - 1] = special;
                }
            }
            ui.Refresh(state);
            ShowCommands();
            ShowCandidates();
        }

        private Candidate RollCandidate(RecruitMethod method)
        {
            bool female = Random.value < .5f;
            string gender = female ? "女" : "男";
            var origin = Pick(Recruitment.origins);
            var personality = Pick(Recruitment.personalities);
            var rank = RollTier(method.rankWeights, "C");
            var cap = RollTier(method.capWeights, "B");
            var lines = RankSystem.Seed(null, rank, RankSystem.At(Math.Max(RankSystem.Index(rank), RankSystem.Index(cap))), "B");
            if (method.productionBias > Random.value)
            {
                var production = lines.Where(line => !RankSystem.IsCombat(line.id)).ToArray();
                RankSystem.Shift(Pick(production), 1, false);
            }
            var talents = (RecruitTalents.talents ?? Array.Empty<CreationOption>())
                .Where(talent => talent.rarity == "common" || Random.value < method.rareChance)
                .OrderBy(talent => -Mathf.Log(Mathf.Max(.00001f, Random.value)) / Math.Max(1, talent.weight))
                .Take(Random.Range(0, 3)).Select(talent => talent.id).ToArray();
            string name = (Pick(Recruitment.surnames) ?? Pick(new[] { "杜", "何", "葛", "林", "沈" })) +
                (Pick(female ? Recruitment.givenFemale : Recruitment.givenMale) ?? Pick(new[] { "青", "春山", "雲生", "照雨", "小滿" }));
            return new Candidate
            {
                id = "recruit_" + Guid.NewGuid().ToString("N"),
                name = name,
                gender = gender, age = Random.Range(12, 31), title = "入門弟子",
                origin = origin?.name ?? "山下鄉里", blurb = (Pick(origin?.blurbs) ?? "聞訊來到山門，願在宗門習藝。").Replace("{name}", name),
                portrait = $"recruit_{(female ? "f" : "m")}{Random.Range(1, 5)}",
                personality = personality?.name ?? "沉著", trait = personality?.name ?? "沉著",
                loyalty = Mathf.Clamp(50 + (personality?.loyalty ?? 0) + state.Story.loyaltyBonus, 0, 100),
                attrs = Enumerable.Range(0, 5).Select(_ => Mathf.Clamp(Random.Range(2, 7) + method.quality, 1, 10)).ToArray(),
                aptitude = new Aptitude { bone = Random.Range(1, 11), insight = Random.Range(1, 11) },
                lines = lines, talents = talents, silverCost = Mathf.Max(0, state.Data.Start.recruitCost + method.quality)
            };
        }

        private static T Pick<T>(T[] values) where T : class =>
            values?.Length > 0 ? values[Random.Range(0, values.Length)] : null;

        private static string RollTier(float[] weights, string fallback)
        {
            if (weights?.Length != 7 || weights.Sum() <= 0)
                return fallback;
            float roll = Random.value * weights.Sum();
            for (int tier = 0; tier < weights.Length; tier++)
                if ((roll -= weights[tier]) <= 0)
                    return RankSystem.At(tier);
            return fallback;
        }

        private int RecruitPrice(Candidate candidate) => Mathf.Max(0,
            Mathf.RoundToInt(candidate.silverCost * (1 + state.Story.recruitCostPct / 100f)));

        private string RecruitRestriction(Candidate candidate)
        {
            if (accepted >= Recruitment.maxAcceptPerVisit)
                return "本次招收名額已滿";
            int beds = state.Buildings.Count(building => building != null && building.actions.Contains("lodge")) * 4;
            if (beds > 0 && state.Disciples.Count >= beds && tutorial?.CanFund("recruit") != true)
                return "床位已滿";
            if (!state.CanPay(recruitmentAction.cost, RecruitPrice(candidate)) && tutorial?.CanFund("recruit") != true)
                return "銀兩或糧食不足";
            return "";
        }

        private void ShowCandidates()
        {
            var buttons = new System.Collections.Generic.List<(string, Action)>
            {
                ("返回", () => ui.CloseModal(force: true)),
                ("謝絕餘下人選", () => { candidates = null; ShowCommands(); ui.CloseModal(force: true); })
            };
            if (accepted < Recruitment.maxAcceptPerVisit && state.Silver >= Recruitment.refreshSilver)
                buttons.Add(($"再尋訪　銀 {Recruitment.refreshSilver}", () =>
                {
                    state.Pay(new Cost { silver = Recruitment.refreshSilver });
                    FillCandidates();
                }));
            ui.ShowCreation(activeMethod.name, new[] { $"銀兩 {state.Silver}　已收徒 {accepted}／{Recruitment.maxAcceptPerVisit}" }, buttons, null);
            var catalog = RecruitTalents.talents ?? Array.Empty<CreationOption>();
            foreach (var candidate in candidates)
            {
                string reason = RecruitRestriction(candidate);
                ui.RecruitCard(candidate, catalog, RecruitPrice(candidate), reason,
                    reason.Length == 0 ? () => ReviewCandidate(candidate) : null);
            }
        }

        private void ReviewCandidate(Candidate candidate)
        {
            if (candidate.story == null)
            {
                AcceptCandidate(candidate);
                return;
            }
            ui.ShowCreation(candidate.name, new[] { candidate.story.prompt }, new[]
            {
                (candidate.story.acceptText ?? "收為弟子", (Action)(() => AcceptCandidate(candidate))),
                (candidate.story.rejectText ?? "婉拒", (Action)ShowCandidates)
            }, null);
        }

        private void AcceptCandidate(Candidate candidate)
        {
            if (RecruitRestriction(candidate).Length > 0 || !candidates.Contains(candidate))
                return;
            Cost cost = recruitmentAction.cost;
            int silver = RecruitPrice(candidate);
            tutorial?.Fund("recruit", cost, silver);
            if (!state.CanPay(cost, silver))
                return;
            state.Pay(cost, silver);
            state.Apply(recruitmentAction.effect);
            candidate.title ??= "入門弟子";
            candidate.trait ??= candidate.personality;
            state.Disciples.Add(candidate);
            world.SpawnDisciple(candidate);
            if (candidate.story != null)
            {
                state.ApplyEffects(candidate.story.acceptEffects);
                if (!string.IsNullOrEmpty(candidate.story.risk))
                    state.ApplyEffects(new[] { new GameEffect { key = "flag", @ref = candidate.story.risk, value = 1 } });
            }
            accepted++;
            candidates = candidates.Where(entry => entry != candidate).ToArray();
            tutorial?.Record("recruit");
            ui.Refresh(state);
            ShowCommands();
            ShowCandidates();
        }

        private void CaptureRecruitment()
        {
            recruitmentAction = data.Actions.First(action => action.type == "recruit");
            activeMethod = Recruitment.methods?.FirstOrDefault() ?? new RecruitMethod { id = "public", name = "公開招徒" };
            accepted = 0;
            FillCandidates();
        }
    }
}
