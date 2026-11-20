using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private NewGameFlow newGame;

        private void StartNewGame()
        {
            ui.CloseTitle();
            newGame = new NewGameFlow(ui, new GameState(data), next =>
            {
                newGame = null;
                EnterCourtyard(next, true);
            });
            newGame.Begin();
        }
    }

    internal sealed class NewGameFlow
    {
        private readonly SectUI ui;
        private readonly GameState state;
        private readonly Action<GameState> done;
        private readonly CharacterCatalog characters = M2Data.Load<CharacterCatalog>("characters");
        private readonly TalentCatalog talents = M2Data.Load<TalentCatalog>("talents");
        private readonly SkillCatalog skills = M2Data.Load<SkillCatalog>("skills");
        private readonly PrologueData story = M2Data.Load<PrologueData>("prologue");
        private readonly List<CreationOption> selected = new();
        private CreationOption[] offered;
        private int rerolls;
        private CreationOption background;
        private CharacterLook look;
        private string currentCg;
        private int freeAttributes;
        private int[] allocated = new int[5];
        private GameEffect[] talentEffects = Array.Empty<GameEffect>(), skillEffects = Array.Empty<GameEffect>();
        private static readonly string[] AttributeNames = { "武力", "身法", "根骨", "悟性", "魅力" };

        internal NewGameFlow(SectUI ui, GameState state, Action<GameState> done)
        {
            this.ui = ui;
            this.state = state;
            this.done = done;
            rerolls = talents.rerollFree;
            freeAttributes = characters.attrPoints;
            state.Protagonist.attrs = Enumerable.Repeat(characters.attrBase, 5).ToArray();
        }

        internal void CaptureAppearance() => ChooseAppearance();
        /// <summary>Capture: six offered talents with two picked, plus both skill families underneath.</summary>
        internal void CaptureTalents()
        {
            selected.Clear();
            RollTalents();
            offered = offered.Take(6).ToArray();
            foreach (var option in offered.Where(option => option.cost > 0).OrderByDescending(option => option.rarity == "rare").Take(2))
                if (CanPick(option, talents.budget, talents.maxPicks))
                    selected.Add(option);
            ShowTalents();
        }
        internal void CapturePrologue()
        {
            var opening = story.openingBeats ?? Array.Empty<StoryBeat>();
            int speaker = Array.FindIndex(opening, beat => !string.IsNullOrEmpty(beat.portrait));
            if (speaker < 0) { StartStory(); return; }
            currentCg = opening.Take(speaker + 1).LastOrDefault(beat => !string.IsNullOrEmpty(beat.image))?.image;
            Play(new[] { opening[speaker] }, ChooseDisaster, SkipStory);
        }
        internal void CaptureDisaster() => ChooseDisaster();
        internal void CaptureLocation() => ChooseLocation();

        internal void Begin()
        {
            AudioManager.Music("prologue");
            ui.ShowCreation("首徒稱謂", new[] { "你是師父最早收入門下的弟子。" },
                new[] { ("大師兄", (Action)(() => ChooseGender("男"))), ("大師姐", (Action)(() => ChooseGender("女"))) },
                ChooseAppearance);
        }

        private void ChooseGender(string gender)
        {
            state.Protagonist.gender = gender;
            state.Protagonist.title = gender == "女" ? "大師姐" : "大師兄";
            look = null;
            state.Protagonist.name = SuggestedName();
            ChooseAppearance();
        }

        private string SuggestedName()
        {
            var names = characters.nameSuggestions?.FirstOrDefault(entry => entry.gender == state.Protagonist.gender)?.names;
            return names?.Length > 0 ? names[UnityEngine.Random.Range(0, names.Length)] :
                state.Protagonist.gender == "女" ? "顧清霜" : "顧行舟";
        }

        private void ChooseAppearance()
        {
            var hero = state.Protagonist;
            var looks = characters.protagonistLooks?.Where(candidate => candidate.gender == hero.gender).ToArray();
            if (looks?.Length != 3)
                looks = Enumerable.Range(1, 3).Select(index => new CharacterLook
                {
                    id = $"hero_{(hero.gender == "女" ? "f" : "m")}{index}", gender = hero.gender,
                    label = new[] { "沉著", "英氣", "清雅" }[index - 1]
                }).ToArray();
            look ??= looks[0];
            hero.lookId = look.id;
            hero.look = look;
            hero.portrait = string.IsNullOrEmpty(look.portrait) ? look.id : look.portrait;
            ui.ShowCreation("形貌與姓名", new[] { hero.title }, null, ChooseBackground, ChooseBackground);
            ui.CreationName(hero.name, value => hero.name = string.IsNullOrWhiteSpace(value) ? SuggestedName() : value.Trim(), SuggestedName);
            ui.CreationPortraits(looks, look, candidate =>
            {
                look = candidate;
                ChooseAppearance();
            });
        }

        private void ChooseBackground()
        {
            var options = characters.backgrounds ?? Array.Empty<CreationOption>();
            background ??= options.FirstOrDefault();
            ui.ShowCreation("出身", Array.Empty<string>(), null, StartTalents, StartTalents);
            foreach (var option in options)
            {
                ui.CreationChoice((option == background ? "◆ " : "") + option.name,
                    Describe(option), option.image, () =>
                    {
                        background = option;
                        ChooseBackground();
                    });
            }
        }

        private void StartTalents()
        {
            selected.Clear();
            RollTalents();
            ShowTalents();
        }

        private void RollTalents()
        {
            var pool = (talents.talents ?? Array.Empty<CreationOption>()).Where(x => x != null).ToList();
            var draw = new List<CreationOption>();
            for (int index = 0; index < 12 && pool.Count > 0; index++)
            {
                int ticket = UnityEngine.Random.Range(0, pool.Sum(option => Math.Max(1, option.weight)));
                int picked = 0;
                while ((ticket -= Math.Max(1, pool[picked].weight)) >= 0)
                    picked++;
                draw.Add(pool[picked]);
                pool.RemoveAt(picked);
            }
            offered = draw.ToArray();
        }

        private void ShowTalents()
        {
            var actions = new List<(string, Action)>
            {
                ("隨機推薦", () =>
                {
                    selected.Clear();
                    foreach (var option in offered.OrderBy(_ => UnityEngine.Random.value))
                        if (CanPick(option, talents.budget, talents.maxPicks))
                            selected.Add(option);
                    ShowTalents();
                })
            };
            if (rerolls > 0)
                actions.Add(($"重抽 {rerolls}", () => { rerolls--; selected.Clear(); RollTalents(); ShowTalents(); }));
            ui.ShowCreation("天賦", new[] { $"餘點 {talents.budget - Spent}　已選 {selected.Count}／{talents.maxPicks}" },
                actions, CompleteTalents, CompleteTalents);
            if (offered.Length > 0)
                ui.CreationGrid(3, (offered.Length + 2) / 3, 136);
            foreach (var option in offered.OrderBy(x => x.rarity == "legendary" ? 0 : x.rarity == "rare" ? 1 : 2))
                ShowPick(option, RarityLabel(option.rarity), talents.budget, talents.maxPicks, ShowTalents);
            // Both skill families, reflecting rank/cap effects of the talents picked so far.
            ui.SkillColumns(PreviewLines(selected));
        }

        private SkillLine[] PreviewLines(IEnumerable<CreationOption> picks)
        {
            var preview = RankSystem.Seed(state.Protagonist.lines, null, null, "B");
            foreach (var effect in picks.SelectMany(option => option.effects ?? Array.Empty<GameEffect>()))
            {
                if (effect == null || effect.key is not ("rank" or "lineRank" or "cap"))
                    continue;
                var line = effect.@ref == "martial" ? RankSystem.BestCombat(preview)
                    : Array.Find(preview, entry => entry.id == effect.@ref);
                RankSystem.Shift(line, Mathf.RoundToInt(effect.value), effect.key == "cap");
            }
            return preview;
        }

        private int Spent => selected.Sum(option => option.cost);
        private bool CanPick(CreationOption option, int budget, int limit) =>
            selected.Count < limit && Spent + option.cost <= budget;

        private void ShowPick(CreationOption option, string group, int budget, int limit, Action redraw)
        {
            bool picked = selected.Contains(option);
            bool available = picked ? Spent - option.cost <= budget : CanPick(option, budget, limit);
            string label = $"{(picked ? "◆ " : "")}{option.name}　{group}　{option.cost} 點";
            ui.CreationChoice(label, Describe(option), null, available ? () =>
            {
                if (picked) selected.Remove(option);
                else selected.Add(option);
                redraw();
            } : null, option.rarity);
        }

        private static string Describe(CreationOption option)
        {
            string[] details = (option.effects ?? Array.Empty<GameEffect>()).Where(effect => effect != null).Select(effect =>
            {
                string caption = effect.key switch
                {
                    "silver" => "銀兩", "food" => "糧食", "reputation" => "聲望", "apMax" => "行動力上限",
                    "recruitPoolSize" => "招徒人選", "recruitCostPct" => "招徒費用", "buildCostPct" => "建造費用",
                    "trainBonusPct" => "修習進度", "insightChance" => "感悟機率", "str" => "武力", "agi" => "身法",
                    "con" => "根骨", "wit" => "悟性", "cha" => "魅力", "loyaltyAll" => "弟子忠誠",
                    "relation" => "勢力關係", "followers" => "追隨弟子", "cap" => "技能上限",
                    "rank" or "lineRank" => "技能階位", _ => ""
                };
                if (effect.key == "noGraveInjury") return "免受重傷與致命傷";
                if (caption.Length == 0) return "";
                if (effect.key is "rank" or "lineRank" or "cap")
                {
                    string line = RankSystem.Categories.FirstOrDefault(category => category.id == effect.@ref)?.name ?? "武學";
                    caption = line + caption;
                }
                string value = effect.key == "insightChance" ? $"{effect.value:+0%;-0%}" :
                    effect.key.EndsWith("Pct", StringComparison.Ordinal) ? $"{effect.value:+0;-0}%" : $"{effect.value:+0;-0}";
                return $"{caption} {value}";
            }).Where(line => line.Length > 0).ToArray();
            return details.Length == 0 ? option.desc : option.desc + "\n" + string.Join("、", details);
        }

        private static string RarityLabel(string rarity) => rarity switch
        {
            "rare" => "稀有", "legendary" => "傳奇", _ => "普通"
        };

        private void CompleteTalents()
        {
            state.Protagonist.talents = selected.Select(option => option.id).ToArray();
            talentEffects = selected.SelectMany(option => option.effects ?? Array.Empty<GameEffect>()).ToArray();
            selected.Clear();
            ShowSkills();
        }

        private void ShowSkills()
        {
            ui.ShowCreation("戰鬥技能與生產技能", new[] { $"餘點 {skills.budget - Spent}" }, null,
                CompleteSkills, CompleteSkills);
            var preview = RankSystem.Seed(state.Protagonist.lines, null, null, "B");
            foreach (var art in selected)
                RankSystem.Learn(preview, art);
            ui.SkillColumns(preview);
            foreach (var option in (skills.skills ?? Array.Empty<CreationOption>()).Where(option => option != null))
                ShowPick(option, RankSystem.Categories.FirstOrDefault(category => category.id == option.category)?.name, skills.budget, int.MaxValue, ShowSkills);
        }

        private void CompleteSkills()
        {
            state.Protagonist.skills = selected.Select(option => option.id).ToArray();
            skillEffects = selected.SelectMany(option => option.effects ?? Array.Empty<GameEffect>()).ToArray();
            ShowAttributes();
        }

        private void ShowAttributes()
        {
            ui.ShowCreation("屬性", new[] { $"餘點 {freeAttributes}　{state.Protagonist.name}", SectUI.Attributes(state.Protagonist.attrs) }, null, StartStory, StartStory);
            for (int index = 0; index < AttributeNames.Length; index++)
            {
                int slot = index;
                ui.CreationAttribute(AttributeNames[slot], state.Protagonist.attrs[slot],
                    allocated[slot] > 0 ? () => ChangeAttribute(slot, -1) : null,
                    freeAttributes > 0 && state.Protagonist.attrs[slot] < characters.attrMax
                        ? () => ChangeAttribute(slot, 1) : null);
            }
        }

        private void ChangeAttribute(int index, int delta)
        {
            allocated[index] += delta;
            freeAttributes -= delta;
            state.Protagonist.attrs[index] += delta;
            ShowAttributes();
        }

        private void StartStory()
        {
            state.Protagonist.backgroundId = background?.id;
            state.ApplyEffects(background?.effects);
            state.ApplyEffects(talentEffects);
            foreach (var art in selected)
                RankSystem.Learn(state.Protagonist.lines, art);
            state.ApplyEffects(skillEffects);
            Play(story.openingBeats, ChooseDisaster, SkipStory);
        }

        private void ChooseDisaster()
        {
            var options = story.disasters ?? Array.Empty<StoryDisaster>();
            if (options.Length == 0) { ChooseLocation(); return; }
            ui.ShowCreation("門派之變", new[] { FormatStory(story.disasterPrompt) }, null, SkipStory);
            ui.CreationGrid(2, (options.Length + 1) / 2, 208);
            foreach (var option in options)
                ui.CreationChoice(option.name, FormatStory(option.summary), option.image, () =>
                {
                    state.Story.disasterId = option.id;
                    state.ApplyEffects(option.effects);
                    Play(option.beats, () => ChooseResponse(option), SkipStory);
                });
        }

        private void ChooseResponse(StoryDisaster disaster)
        {
            var options = disaster.responses ?? Array.Empty<StoryResponse>();
            if (options.Length == 0) { ChooseLocation(); return; }
            ui.ShowCreation("去留之策", new[] { FormatStory(disaster.responsePrompt) }, null, SkipStory);
            ui.CreationGrid(2, (options.Length + 1) / 2, 208);
            foreach (var option in options)
                ui.CreationChoice(option.name, FormatStory(option.summary), option.image, () =>
                {
                    SetResponse(option);
                    Play(option.beats, ChooseLocation, SkipStory);
                });
        }

        private void SetResponse(StoryResponse option)
        {
            state.Story.responseId = option.id;
            state.ApplyEffects(option.effects);
        }

        private void ChooseLocation()
        {
            var options = story.locations ?? Array.Empty<StoryLocation>();
            if (options.Length == 0) { Play(story.closingBeats, Finish, Finish); return; }
            ui.ShowCreation("立足之地", new[] { FormatStory(story.locationPrompt) }, null, SkipStory);
            ui.CreationGrid(2, (options.Length + 1) / 2, 164);
            foreach (var option in options)
                ui.CreationChoice(option.name, $"{option.region}　{option.resources}\n{option.desc}\n{option.bonusText}", option.image, () =>
                {
                    SetLocation(option);
                    Play(story.closingBeats, Finish, Finish);
                });
        }

        private void SetLocation(StoryLocation option)
        {
            state.Story.locationId = option.id;
            state.Story.mapNode = option.node;
            state.ApplyEffects(option.effects);
        }

        private void SkipStory()
        {
            var disaster = story.disasters?.FirstOrDefault(entry => entry.id == state.Story.disasterId)
                ?? story.disasters?.FirstOrDefault();
            if (disaster != null && string.IsNullOrEmpty(state.Story.disasterId))
            {
                state.Story.disasterId = disaster.id;
                state.ApplyEffects(disaster.effects);
            }
            var responses = disaster?.responses;
            var response = responses?.FirstOrDefault(entry => entry.id == "independent") ?? responses?.FirstOrDefault();
            if (response != null && string.IsNullOrEmpty(state.Story.responseId))
                SetResponse(response);
            var location = story.locations?.FirstOrDefault();
            if (location != null && string.IsNullOrEmpty(state.Story.locationId))
                SetLocation(location);
            Finish();
        }

        private void Play(StoryBeat[] beats, Action next, Action skip)
        {
            int index = 0;
            ShowNext();
            void ShowNext()
            {
                if (index >= (beats?.Length ?? 0)) { next(); return; }
                var beat = beats[index++];
                var friend = Friend();
                string friendId = state.Protagonist.gender == "女" ? "friend_m" : "friend_f";
                // Story characters always use their own portrait id (never a recruit/fallback image).
                string portrait = beat.portrait == "hero" ? state.Protagonist.portrait ?? state.Protagonist.lookId :
                    beat.portrait == "friend" ? friendId : beat.portrait;
                if (!string.IsNullOrEmpty(beat.image))
                    currentCg = beat.image;
                ui.ShowStoryBeat(FormatStory(beat.speaker), FormatStory(beat.text), currentCg, portrait,
                    beat.portrait != "hero", ShowNext, skip);
            }
        }

        private StoryCharacter Friend()
        {
            string id = state.Protagonist.gender == "女" ? "friend_m" : "friend_f";
            return characters.storyCharacters?.FirstOrDefault(entry => entry.id == id);
        }

        private string FormatStory(string text) => (text ?? "").Replace("{title}", state.Protagonist.title)
            .Replace("{name}", state.Protagonist.name).Replace("{sect}", state.Data.Start.sectName)
            .Replace("{friend}", Friend()?.name ?? "故友").Replace("{he}", state.Protagonist.gender == "女" ? "她" : "他");

        private void Finish()
        {
            ui.CloseStoryBeat();
            ui.CloseModal(force: true);
            state.RefillAp();
            done(state);
        }
    }
}
