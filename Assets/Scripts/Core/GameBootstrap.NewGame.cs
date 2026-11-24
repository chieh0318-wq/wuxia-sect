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
        private int appearancePage;
        private string currentCg;
        private int freeAttributes;
        private readonly int[] allocated = new int[AttributeSystem.Count];
        private GameEffect[] talentEffects = Array.Empty<GameEffect>(), skillEffects = Array.Empty<GameEffect>();
        private readonly Dictionary<string, int> legacyPicks = new();
        private LegacyItemDef legacyFocused;

        private string AttributeBenefit(int slot) =>
            characters.attributes?.FirstOrDefault(entry => entry?.id == AttributeSystem.Keys[slot])?.desc
            ?? AttributeSystem.Benefits[slot];

        internal NewGameFlow(SectUI ui, GameState state, Action<GameState> done)
        {
            this.ui = ui;
            this.state = state;
            this.done = done;
            rerolls = talents.rerollFree;
            freeAttributes = characters.attrPoints;
            state.Protagonist.attrs = Enumerable.Repeat(characters.attrBase, AttributeSystem.Count).ToArray();
        }

        internal void CaptureAppearance()
        {
            var hero = state.Protagonist;
            string gender = hero.gender == "女" ? "女" : "男";
            hero.gender = gender;
            if (string.IsNullOrWhiteSpace(hero.title)) hero.title = gender == "女" ? "三師姐" : "三師兄";
            if (string.IsNullOrWhiteSpace(hero.name)) hero.name = SuggestedName();
            ChooseAppearance();
        }
        internal void CaptureFemaleAppearance() => ChooseGender("女");
        internal void CaptureSkipPrologue() => SkipStory();
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
        /// <summary>v1126 #5: as many talents picked as the budget allows, to check the 已選天賦 bar against 繼續.</summary>
        internal void CaptureTalentsFull()
        {
            selected.Clear();
            RollTalents();
            foreach (var option in offered.Where(option => option.cost > 0).OrderBy(option => option.cost).ThenByDescending(option => option.name.Length))
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
            string clip = AudioManager.ClipFor(opening[speaker].text, state.Protagonist.gender == "女");
            int voiced = opening.Concat(story.closingBeats ?? Array.Empty<StoryBeat>()).Count(beat => AudioManager.ClipFor(beat.text) != null);
            Debug.Log($"[Capture] Voice: beat clip={clip ?? "none"} loaded={(clip != null && Resources.Load<AudioClip>("Audio/Voice/" + clip) != null)}" +
                $" voicedBeats={voiced} playlist={AudioManager.PlaylistSummary}");
        }
        internal void CaptureOpposingSpeaker()
        {
            var beats = story.disasters.First(entry => entry.id == "betrayal").beats;
            CaptureBeats(beats, Array.FindIndex(beats, beat => beat.portrait == "rival_head"));
        }

        internal void CaptureDisaster() => ChooseDisaster();
        internal void CaptureLocation() => ChooseLocation(previewFirst: true);
        internal void CaptureBackground() => ChooseBackground();
        internal void CaptureSkills() => CompleteTalents();
        internal void CaptureAttributes() => CompleteSkills();
        internal void CaptureLegacyShop()
        {
            RecommendLegacyPicks();
            Debug.Log($"[Capture] Legacy shop opened with {legacyPicks.Count} recommended selections.");
        }

        internal void CaptureResponse()
        {
            if (story.disasters?.FirstOrDefault(entry => entry.id == "plague") is StoryDisaster disaster)
                ChooseResponse(disaster);
        }

        internal void Begin()
        {
            AudioManager.Music("title");
            ui.ShowCreation("同門排行", new[] { "師父門下，你排行第三，卻是同輩中最出挑的一個。同門該怎麼稱呼你？" },
                null, ChooseAppearance);
            ui.CreationGrid(2, 1, 540);
            ui.CreationIdentity("男俠", "hero_m1", "男", () => ChooseGender("男"));
            ui.CreationIdentity("女俠", "hero_f1", "女", () => ChooseGender("女"));
        }

        private void ChooseGender(string gender)
        {
            // Coming back with the same gender keeps the chosen look and typed name.
            bool changed = look == null || state.Protagonist.gender != gender;
            state.Protagonist.gender = gender;
            state.Protagonist.title = gender == "女" ? "三師姐" : "三師兄";
            if (changed)
            {
                look = null;
                appearancePage = 0;
                state.Protagonist.name = SuggestedName();
            }
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
            var looks = (characters.protagonistLooks ?? Array.Empty<CharacterLook>())
                .Where(candidate => candidate != null && candidate.gender == hero.gender &&
                    !string.IsNullOrEmpty(candidate.id)).ToArray();
            if (looks.Length == 0)
                looks = new[] { new CharacterLook { id = hero.gender == "女" ? "hero_f1" : "hero_m1",
                    gender = hero.gender, label = "沉著" } };
            look = looks.FirstOrDefault(candidate => candidate.id == look?.id) ?? looks[0];
            hero.lookId = look.id;
            hero.look = look;
            hero.portrait = string.IsNullOrEmpty(look.portrait) ? look.id : look.portrait;

            var bgOptions = characters.backgrounds ?? Array.Empty<CreationOption>();
            background ??= bgOptions.FirstOrDefault();

            ui.ShowCreation("形貌與出身", Array.Empty<string>(), Back(Begin), StartTalents, StartTalents);
            ui.CreationSummary(Sheet(ChosenTalents, ChosenSkills, "出身"));
            ui.CreationNameAndGender(hero.name, value => hero.name = string.IsNullOrWhiteSpace(value) ? SuggestedName() : value.Trim(),
                SuggestedName, hero.gender, gender => { if (gender != hero.gender) ChooseGender(gender); });
            ui.CreationPortraitStrip(looks, look, background?.id, hero.title, candidate =>
            {
                look = candidate;
                ChooseAppearance();
            });
            if (bgOptions.Length > 0)
            {
                int columns = Mathf.Min(5, bgOptions.Length);
                ui.CreationGrid(columns, (bgOptions.Length + columns - 1) / columns, 264);
            }
            foreach (var option in bgOptions)
            {
                ui.CreationChoice(option.name, option.desc, option.image, () =>
                {
                    background = option;
                    ChooseAppearance();
                }, picked: option == background, tags: Details(option), wideDescription: true);
            }
        }

        private void ChooseBackground() => ChooseAppearance();

        /// <summary>Back button for the pre-story creation steps (effects are only applied in StartStory, so going back is safe).</summary>
        private static (string, Action)[] Back(Action previous) => new[] { ("上一步", previous) };

        private string[] SelectedIds => selected.Select(option => option.id).ToArray();

        private void Restore(IEnumerable<CreationOption> pool, string[] ids)
        {
            selected.Clear();
            selected.AddRange((pool ?? Array.Empty<CreationOption>()).Where(option => option != null && ids.Contains(option.id)));
        }

        private void StartTalents()
        {
            // Point-buy mode: list ALL talents directly without random drawing.
            offered = (talents.talents ?? Array.Empty<CreationOption>()).Where(x => x != null).ToArray();
            Restore(offered, state.Protagonist.talents);
            talentFilter = "";
            ShowTalents(1f);
        }

        private void RollTalents()
        {
            offered = (talents.talents ?? Array.Empty<CreationOption>()).Where(x => x != null).ToArray();
        }

        private string talentFilter = "";

        private void ShowTalents(float scrollPos = 1f)
        {
            var actions = new List<(string, Action)>
            {
                ("上一步", () =>
                {
                    state.Protagonist.talents = SelectedIds;
                    ChooseAppearance();
                }),
                ("隨機推薦", () =>
                {
                    selected.Clear();
                    foreach (var option in offered.OrderBy(_ => UnityEngine.Random.value))
                        if (CanPick(option, talents.budget, talents.maxPicks))
                            selected.Add(option);
                    ShowTalents(1f);
                }),
                ("清空重選", () =>
                {
                    selected.Clear();
                    ShowTalents(1f);
                })
            };
            ui.ShowCreation("天賦", Array.Empty<string>(), actions, CompleteTalents, CompleteTalents);
            var sheet = Sheet(selected, ChosenSkills, "天賦");
            sheet.picksHeading = $"已選天賦　{selected.Count}／{talents.maxPicks}";
            ui.CreationSummary(sheet, showPicks: false);
            ui.CreationSelectedTalents(sheet.picksHeading, selected.Select(option =>
                (option, Spent - option.cost <= talents.budget ? (Action)(() =>
                {
                    selected.Remove(option);
                    ShowTalents(ui.TalentScrollPosition);
                }) : null)).ToArray());
            // v1126 #2: 主修 is picked from the combat lines, with what it changes written beside it.
            ui.CreationMajor(MajorId, id =>
            {
                state.Protagonist.major = id;
                ShowTalents(ui.TalentScrollPosition);
            }, MajorExplanation(MajorId));
            // Pinned at the TOP (synchronized with 武學與技藝 so 14 skill bars are always visible while scrolling talents).
            ui.SkillTiles(PreviewLines(selected));
            ui.CreationTabs(new[] { "", "legendary", "rare", "common", "negative" }.Select(key =>
                (key.Length == 0 ? "全部" : key == "negative" ? "負面補點" : RarityLabel(key), key == talentFilter,
                    (Action)(() => { talentFilter = key; ShowTalents(1f); }))));
            var visible = offered.Where(option =>
                    talentFilter.Length == 0
                    || (talentFilter == "negative" ? option.cost < 0 : option.rarity == talentFilter && option.cost >= 0))
                .OrderBy(option => option.cost < 0 ? 3 : option.rarity == "legendary" ? 0 : option.rarity == "rare" ? 1 : 2)
                .ThenByDescending(option => option.cost).ToArray();
            ui.CreationTalentScroll(visible, scrollPos, option =>
                ShowPick(option, option.cost < 0 ? "負面" : RarityLabel(option.rarity), talents.budget, talents.maxPicks,
                    () => ShowTalents(ui.TalentScrollPosition)));
        }

        /// <summary>
        /// Skill lines as they will stand after creation: the base tiers (劍 C/B, others D/C), then the background
        /// and talent rank/cap effects, each stacking additively (applied for real only in StartStory).
        /// </summary>
        private SkillLine[] PreviewLines(IEnumerable<CreationOption> picks)
        {
            var preview = RankSystem.Seed(null, RankSystem.PrimaryRank, RankSystem.PrimaryCap, RankSystem.BaseCap, RankSystem.BaseRank);
            var effects = (background?.effects ?? Array.Empty<GameEffect>())
                .Concat(picks.SelectMany(option => option.effects ?? Array.Empty<GameEffect>()));
            foreach (var effect in effects)
            {
                if (effect == null || effect.key is not ("rank" or "lineRank" or "cap"))
                    continue;
                var line = effect.@ref == "martial" ? RankSystem.Major(preview, MajorId)
                    : Array.Find(preview, entry => entry.id == effect.@ref);
                int value = Mathf.RoundToInt(effect.value);
                if (effect.key == "cap") RankSystem.Raise(line, 0, value);
                else RankSystem.Raise(line, value, 0);
            }
            return preview;
        }

        private string MajorId => RankSystem.IsCombat(state.Protagonist.major) ? state.Protagonist.major : "sword";

        /// <summary>v1126 #2: what 主修 decides, in one line (shared with the hero page's 調整主修).</summary>
        internal static string MajorExplanation(string major)
        {
            string line = EffectText.LineName(major);
            return $"主修{line}：標「主修武學」的天賦，階位與潛力都加在{line}；每月修練{line}一路另有加成；" +
                $"與弟子切磋的心得記在{line}；上陣時以{line}招式為主。進遊戲後可在主角頁「調整主修」改換。";
        }

        private int Spent => selected.Sum(option => option.cost);
        private bool CanPick(CreationOption option, int budget, int limit) =>
            selected.Count < limit && Spent + option.cost <= budget;

        private void ShowPick(CreationOption option, string group, int budget, int limit, Action redraw)
        {
            bool picked = selected.Contains(option);
            bool available = picked ? Spent - option.cost <= budget : CanPick(option, budget, limit);
            string label = $"{option.name}　{group}";
            ui.CreationChoice(label, option.desc, null, available ? () =>
            {
                if (picked) selected.Remove(option);
                else selected.Add(option);
                redraw();
            } : null, option.rarity ?? "common", picked, option.cost, tags: Details(option),
                icon: UiKit.TalentIcon(option));
        }

        /// <summary>Gameplay effects of an option as a compact tag line (「武力 +2・身法 +2」).</summary>
        private string Details(CreationOption option)
        {
            // v1124: one shared formatter (EffectText) for creation tags and story-choice impact lines.
            // v1126 #2: 「主修武學」 bonuses name the line they land on.
            string major = EffectText.LineName(MajorId);
            var details = (option.effects ?? Array.Empty<GameEffect>())
                .Select(effect => effect?.@ref == "martial" && effect.key is "rank" or "lineRank" or "cap"
                    ? EffectText.Describe(effect).Replace("主修武學", $"主修（{major}）")
                    : EffectText.Describe(effect)).Where(line => line.Length > 0).ToList();
            if (!string.IsNullOrEmpty(option.category) && RankSystem.Categories.Any(entry => entry.id == option.category))
            {
                int steps = RankSystem.LearnSteps(option);
                details.Insert(0, $"習得階位 +{steps}・潛力上限 +{RankSystem.LearnCap(option)}");
            }
            return string.Join("・", details);
        }

        private IEnumerable<CreationOption> ChosenTalents => (talents.talents ?? Array.Empty<CreationOption>())
            .Where(option => option != null && state.Protagonist.talents.Contains(option.id));

        private IEnumerable<CreationOption> ChosenSkills => (skills.skills ?? Array.Empty<CreationOption>())
            .Where(option => option != null && state.Protagonist.skills.Contains(option.id));

        private IEnumerable<GameEffect> Bonuses(IEnumerable<CreationOption> talentPicks, IEnumerable<CreationOption> skillPicks) =>
            (background?.effects ?? Array.Empty<GameEffect>())
            .Concat(talentPicks.Concat(skillPicks).SelectMany(option => option.effects ?? Array.Empty<GameEffect>()))
            .Where(effect => effect != null);

        private static int[] EffectAttributes(IEnumerable<GameEffect> effects)
        {
            var totals = new int[AttributeSystem.Count];
            foreach (var effect in effects)
            {
                int slot = AttributeSystem.Index(effect.key);
                if (slot >= 0) totals[slot] += Mathf.RoundToInt(effect.value);
            }
            return totals;
        }

        /// <summary>The live summary card: allocated attributes plus background / talent / skill bonuses, budgets and picks.</summary>
        private CreationSheet Sheet(IEnumerable<CreationOption> talentPicks, IEnumerable<CreationOption> skillPicks, string step)
        {
            var talentList = talentPicks.ToList();
            var skillList = skillPicks.ToList();
            var hero = state.Protagonist;
            var bonuses = EffectAttributes(Bonuses(talentList, skillList));
            var attrs = AttributeSystem.Normalize(hero.attrs).Select((value, slot) => value + bonuses[slot]).ToArray();
            var lines = PreviewLines(talentList);
            foreach (var art in skillList)
                RankSystem.Learn(lines, art);
            var sheet = new CreationSheet
            {
                name = hero.name, title = UiKit.HeroTitle(hero) == hero.name ? "" : UiKit.HeroTitle(hero), portrait = hero.portrait ?? hero.lookId, gender = hero.gender,
                background = background?.name, attrs = attrs, lines = lines,
                picksHeading = step == "技藝" ? "已習技藝" : "已選天賦"
            };
            sheet.budgets.Add(("天賦點", talents.budget - talentList.Sum(option => option.cost), talents.budget));
            sheet.budgets.Add(("技藝點", skills.budget - skillList.Sum(option => option.cost), skills.budget));
            sheet.budgets.Add(("屬性點", freeAttributes, characters.attrPoints));
            sheet.picks.AddRange((step == "技藝" ? skillList : talentList).Select(option => option.name));
            return sheet;
        }

        private static string RarityLabel(string rarity) => rarity switch
        {
            "rare" => "稀有", "legendary" => "傳奇", _ => "普通"
        };

        private void CompleteTalents()
        {
            state.Protagonist.talents = SelectedIds;
            talentEffects = selected.SelectMany(option => option.effects ?? Array.Empty<GameEffect>()).ToArray();
            Restore(skills.skills, state.Protagonist.skills);
            ShowSkills();
        }

        private void ShowSkills()
            => ShowSkills(Vector2.one);

        private void ShowSkills(Vector2 scrollPosition)
        {
            ui.ShowCreation("武學與技藝", Array.Empty<string>(), Back(() =>
                {
                    state.Protagonist.skills = SelectedIds;
                    StartTalents();
                }), CompleteSkills, CompleteSkills);
            var sheet = Sheet(ChosenTalents, selected, "技藝");
            ui.CreationSummary(sheet);
            ui.SkillTiles(sheet.lines);
            var arts = (skills.skills ?? Array.Empty<CreationOption>()).Where(option => option != null).ToArray();
            var combat = arts.Where(art => RankSystem.IsCombat(art.category)).ToArray();
            var production = arts.Where(art => !RankSystem.IsCombat(art.category)).ToArray();
            ui.CreationArts(combat, production, scrollPosition, option =>
                ShowPick(option, RankSystem.Categories.FirstOrDefault(category => category.id == option.category)?.name,
                    skills.budget, int.MaxValue, () => ShowSkills(ui.ArtsScrollPosition)));
        }

        private void CompleteSkills()
        {
            state.Protagonist.skills = SelectedIds;
            skillEffects = selected.SelectMany(option => option.effects ?? Array.Empty<GameEffect>()).ToArray();
            ShowAttributes();
        }

        private void ShowAttributes()
        {
            var actions = new (string, Action)[]
            {
                ("上一步", () =>
                {
                    Restore(skills.skills, state.Protagonist.skills);
                    ShowSkills();
                }),
                ("隨機分配", RollAttributes)
            };
            ui.ShowCreation("屬性", new[] { $"可分配點數　{freeAttributes}／{characters.attrPoints}　　" +
                    $"每項基礎 {characters.attrBase}，最低 {characters.attrMin}（降低可退回點數），最高 {characters.attrMax}" },
                actions, ShowLegacyShop, ShowLegacyShop);
            ui.CreationSummary(Sheet(ChosenTalents, ChosenSkills, "屬性"));
            var bonuses = EffectAttributes(Bonuses(ChosenTalents, ChosenSkills));
            var values = state.Protagonist.attrs = AttributeSystem.Normalize(state.Protagonist.attrs);
            for (int index = 0; index < AttributeSystem.Count; index++)
            {
                int slot = index;
                int value = values[slot];
                ui.CreationAttribute(slot, value, characters.attrBase, characters.attrMin, characters.attrMax, bonuses[slot],
                    AttributeBenefit(slot),
                    value > characters.attrMin ? () => ChangeAttribute(slot, -1) : null,
                    freeAttributes > 0 && value < characters.attrMax ? () => ChangeAttribute(slot, 1) : null,
                    AttributePerPoint.Describe(slot));
            }
        }

        private void RollAttributes()
        {
            var values = state.Protagonist.attrs = AttributeSystem.Normalize(state.Protagonist.attrs);
            for (int slot = 0; slot < allocated.Length; slot++)
            {
                values[slot] -= allocated[slot];
                freeAttributes += allocated[slot];
                allocated[slot] = 0;
            }

            var available = Enumerable.Range(0, allocated.Length)
                .Where(slot => values[slot] < characters.attrMax).ToList();
            while (freeAttributes > 0 && available.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, available.Count);
                int slot = available[index];
                allocated[slot]++;
                freeAttributes--;
                if (++values[slot] == characters.attrMax)
                    available.RemoveAt(index);
            }
            ShowAttributes();
        }

        private void ChangeAttribute(int index, int delta)
        {
            allocated[index] += delta;
            freeAttributes -= delta;
            state.Protagonist.attrs[index] += delta;
            ShowAttributes();
        }

        private void ShowLegacyShop()
        {
            legacyFocused ??= LegacySystem.Catalog[0];
            int total = LegacySystem.TotalPoints;
            int spent = legacyPicks.Sum(kv =>
            {
                var def = LegacySystem.Catalog.FirstOrDefault(x => x.id == kv.Key);
                return def == null ? 0 : def.cost * kv.Value;
            });
            int left = total - spent;
            var actions = new (string, Action)[]
            {
                ("上一步", ShowAttributes),
                ("清空物資", () => { legacyPicks.Clear(); ShowLegacyShop(); }),
                ("推薦攜帶", RecommendLegacyPicks)
            };
            ui.ShowCreation("先輩遺澤", Array.Empty<string>(), actions, StartStory, StartStory);
            ui.CreationLegacyShop(total, left, legacyPicks, legacyFocused, (def, delta) =>
            {
                int cur = legacyPicks.TryGetValue(def.id, out var q) ? q : 0;
                int next = Mathf.Clamp(cur + delta, 0, def.maxQty);
                if (next == 0) legacyPicks.Remove(def.id);
                else legacyPicks[def.id] = next;
                ShowLegacyShop();
            }, def =>
            {
                legacyFocused = def;
            });
        }

        private void RecommendLegacyPicks()
        {
            legacyPicks.Clear();
            legacyPicks["leg_qingfeng"] = 1;
            legacyPicks["leg_robe"] = 1;
            legacyPicks["leg_boots"] = 1;
            legacyPicks["item_jinchuang"] = 2;
            legacyPicks["book_sword"] = 1;
            ShowLegacyShop();
        }

        private void StartStory()
        {
            state.Protagonist.backgroundId = background?.id;
            state.ApplyEffects(background?.effects);
            state.ApplyEffects(talentEffects);
            foreach (var art in (skills.skills ?? Array.Empty<CreationOption>())
                         .Where(option => option != null && state.Protagonist.skills.Contains(option.id)))
                RankSystem.Learn(state.Protagonist.lines, art);
            state.ApplyEffects(skillEffects);
            LegacySystem.ApplyCarried(state, legacyPicks);
            AudioManager.Music("title");
            Play(story.openingBeats, PlaySuccession, SkipStory);
        }

        /// <summary>v1124 B10: the sect asks the protagonist to stand in as 代掌門; then B9, the friend arrives.</summary>
        private void PlaySuccession() => Play(story.successionBeats, () =>
        {
            TakeActingTitle();
            Play(story.friendBeats, IntroduceSectGoals, SkipStory);
        }, SkipStory);

        /// <summary>
        /// v1126 #8 / #14: after taking over, the 軍師 (the friend of the opposite sex) lays out the four routes in
        /// person and says the choice is the hero's, then speaks of the neighbouring powers in plain words — the
        /// relation itself is a tag on screen, never part of the line (quests.json).
        /// </summary>
        private void IntroduceSectGoals() => IntroduceSectGoals(false);

        private void IntroduceSectGoals(bool factionsOnly, int skip = 0)
        {
            var board = QuestBoardData.Data;
            var opening = (factionsOnly ? Array.Empty<string>() : board.opening ?? Array.Empty<string>()).Where(text => !string.IsNullOrWhiteSpace(text))
                .Select(text => new StoryBeat { speaker = "{friend}", portrait = "friend", text = text }).ToList();
            foreach (var intro in board.factionIntro ?? Array.Empty<QuestIntro>())
            {
                var faction = intro == null ? null : GameState.FindFaction(intro.id);
                if (faction == null || string.IsNullOrWhiteSpace(intro.text)) continue;
                var beat = new StoryBeat { speaker = "{friend}", portrait = "friend", text = intro.text };
                beatTags[beat] = $"{faction.Label}・{faction.Relation}";
                opening.Add(beat);
            }
            if (opening.Count == 0) { ui.ShowSectGoals(state, ChooseDisaster); return; }
            Play(opening.Skip(Mathf.Clamp(skip, 0, opening.Count - 1)).ToArray(), ChooseDisaster, SkipStory);
        }

        /// <summary>v1126 #14: on-screen tags (e.g. 「少林寺・友好」) for beats whose line must not spell them out.</summary>
        private readonly Dictionary<StoryBeat, string> beatTags = new();

        internal void CaptureAdvisorOpening(int skip = 0) => IntroduceSectGoals(false, skip);

        /// <summary>v1126 capture: the friend's beat about the Xie family's support (#0).</summary>
        internal void CaptureFriendFamily()
        {
            var beats = story.friendBeats ?? Array.Empty<StoryBeat>();
            CaptureBeats(beats, Math.Max(0, Array.FindIndex(beats, beat => beat.text?.Contains("空手") == true)));
        }

        /// <summary>v1126 capture: 「門中事務，交給老三……替{he}看著」 (#6).</summary>
        internal void CaptureMasterLeaves()
        {
            var beats = story.openingBeats ?? Array.Empty<StoryBeat>();
            CaptureBeats(beats, Math.Max(0, Array.FindIndex(beats, beat => beat.text?.Contains("交給老三") == true)));
        }
        internal void CaptureAdvisorFaction() => IntroduceSectGoals(true);

        private void TakeActingTitle()
        {
            if (string.IsNullOrWhiteSpace(story.actingTitle)) return;
            state.Protagonist.title = story.actingTitle;
            state.ApplyEffects(new[] { new GameEffect { key = "flag", @ref = "acting_head", value = 1 } });
        }

        internal void CaptureSuccession() => CaptureBeats(story.successionBeats, 1);

        internal void CaptureFriendArrival()
        {
            state.Protagonist.gender = "女";
            state.Protagonist.name = "盧凝雪";
            state.Protagonist.portrait = state.Protagonist.lookId = "hero_f1";
            CaptureBeats(story.friendBeats, 2);
        }

        private void CaptureBeats(StoryBeat[] beats, int index)
        {
            if (beats == null || beats.Length == 0) { ChooseDisaster(); return; }
            index = Mathf.Clamp(index, 0, beats.Length - 1);
            currentCg = beats.Take(index + 1).LastOrDefault(beat => !string.IsNullOrEmpty(beat.image) &&
                PortraitService.Story(beat.image) != null)?.image ?? currentCg;
            Play(beats.Skip(index).ToArray(), ChooseDisaster, SkipStory);
        }

        private void ChooseDisaster()
        {
            var options = story.disasters ?? Array.Empty<StoryDisaster>();
            if (options.Length == 0) { ChooseLocation(); return; }
            ui.ShowCreation("門派之變", new[] { FormatStory(story.disasterPrompt) }, null, SkipStory);
            ui.CreationGrid(2, (options.Length + 1) / 2, 262);
            foreach (var option in options)
                // v1124 B6: difficulty badge from prologue.json and the real effects generated from the data.
                ui.CreationChoice(ChoiceTitle(option.name, EffectText.Difficulty(option.difficulty)),
                    FormatStory(option.summary), option.image, () =>
                {
                    state.Story.disasterId = option.id;
                    state.ApplyEffects(option.effects);
                    AudioManager.Music(option.id == "treasure" ? "battle" : "sorrow");
                    Play(option.beats, () => ChooseResponse(option), SkipStory);
                }, effect: EffectText.Describe(option.effects, state));
        }

        private void ChooseResponse(StoryDisaster disaster)
        {
            var options = disaster.responses ?? Array.Empty<StoryResponse>();
            if (options.Length == 0) { ChooseLocation(); return; }
            ui.ShowCreation("去留之策", new[] { FormatStory(disaster.responsePrompt) }, null, SkipStory);
            // v1124 B7: taller cards so the 「影響」 line and the full summary both fit.
            ui.CreationGrid(3, (options.Length + 2) / 3, 600);
            foreach (var option in options)
                ui.CreationChoice(option.name, FormatStory(option.summary),
                    PortraitService.ResponseArt(disaster.id, option.id, option.image
                        ?? option.beats?.FirstOrDefault(beat => !string.IsNullOrEmpty(beat.image))?.image), () =>
                {
                    SetResponse(option);
                    if (option.id == "independent")
                    {
                        Play(option.beats, ChooseLocation, SkipStory);
                    }
                    else
                    {
                        AssignShelterLocation(disaster?.id, option.id);
                        Play(option.beats, () => Play(story.closingBeats, Finish, Finish), SkipStory);
                    }
                }, banner: true, effect: EffectText.Describe(option.effects, state));
        }

        private static string ChoiceTitle(string name, string badge) =>
            string.IsNullOrEmpty(badge) ? name : $"{name}　{badge}";

        private void AssignShelterLocation(string disasterId, string responseId)
        {
            string node = (disasterId, responseId) switch
            {
                ("treasure", "submit") => "shanzhou",
                ("treasure", "ally") => "huashan",
                ("court", "submit") => "luoyang",
                ("court", "ally") => "shaolin",
                ("betrayal", "submit") => "funiu",
                ("betrayal", "ally") => "huashan",
                ("plague", "submit") => "luoyang",
                ("plague", "ally") => "liangzhou",
                _ => "huashan"
            };
            state.Story.locationId = $"shelter_{disasterId}_{responseId}";
            state.Story.mapNode = node;
        }

        private void SetResponse(StoryResponse option)
        {
            state.Story.responseId = option.id;
            state.ApplyEffects(option.effects);
        }

        private void ChooseLocation() => ChooseLocation(previewFirst: false);

        private void ChooseLocation(bool previewFirst)
        {
            AudioManager.Music("jianghu_map");
            var options = story.locations ?? Array.Empty<StoryLocation>();
            if (options.Length == 0) { Play(story.closingBeats, Finish, Finish); return; }
            ui.ShowSettlementMap(state, options, FormatStory(story.locationPrompt), option =>
            {
                SetLocation(option);
                Play(story.closingBeats, Finish, Finish);
            }, SkipStory, previewFirst);
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
                string friendId = state.FriendId;
                // Story characters always use their own portrait id (never a recruit/fallback image).
                string portrait = beat.portrait == "hero" ? state.Protagonist.portrait ?? state.Protagonist.lookId :
                    PortraitService.StoryId(beat.portrait, friendId == "friend_m" ? "男" : "女");
                // A CG still waiting for art (listed in v1124_art_todo.md) keeps the previous illustration.
                if (!string.IsNullOrEmpty(beat.image) && PortraitService.Story(beat.image) != null)
                    currentCg = beat.image;
                ui.ShowStoryBeat(FormatStory(beat.speaker), FormatStory(beat.text), currentCg, portrait,
                    beat.portrait != "hero", ShowNext, skip, voiceText: beat.text, femaleHero: state.Protagonist.gender == "女",
                    speakerOnRight: !string.IsNullOrEmpty(portrait)
                        && beat.portrait is not ("hero" or "lin" or "xu" or "master")
                        && !portrait.StartsWith("friend_", StringComparison.Ordinal),
                    tag: beatTags.TryGetValue(beat, out var tag) ? tag : null);
            }
        }

        private StoryCharacter Friend()
        {
            return characters.storyCharacters?.FirstOrDefault(entry => entry.id == state.FriendId);
        }

        private string FormatStory(string text) => CastDialogue.Format(state, text).Replace("{title}", UiKit.HeroTitle(state.Protagonist))
            .Replace("{name}", state.Protagonist.name)
            .Replace("{friend}", Friend()?.name ?? "故友").Replace("{he}", state.Protagonist.gender == "女" ? "她" : "他")
            .Replace("{guestSect}", QuestBoardData.Data.guestSect);

        private void Finish()
        {
            // Skipped stories still end with the protagonist standing in as 代掌門 (B10).
            if (state.Protagonist.title is "三師兄" or "三師姐")
                TakeActingTitle();
            ui.CloseStoryBeat();
            ui.CloseModal(force: true);
            state.RefillAp();
            done(state);
        }
    }
}
