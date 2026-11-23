using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private StoryCharacter[] storyCharacters;
        private MapFaction requestedLeader;
        private StoryCharacter[] StoryCharacters => storyCharacters ??=
            (M2Data.Load<CharacterCatalog>("characters").storyCharacters ?? Array.Empty<StoryCharacter>())
            .Where(person => person != null && !string.IsNullOrEmpty(person.id)).ToArray();

        private StoryCharacter FactionLeader(MapFaction faction) => StoryCharacters.FirstOrDefault(person =>
            person.id == faction.PortraitId) ?? new StoryCharacter
            {
                id = faction.PortraitId, name = faction.leader ?? faction.Label, title = "掌門",
                faction = faction.id, portrait = faction.PortraitId, rank = faction.leaderRank,
                cap = faction.leaderCap, lines = faction.leaderLines?.Where(line => line != null)
                    .Select(line => new SkillLine { id = line.id, rank = line.rank, cap = line.cap }).ToArray()
            };

        private void OpenMapLeader(MapFaction faction)
        {
            requestedLeader = faction;
            map.Close();
            if (!ui.ModalOpen) ShowRequestedLeader();
        }

        private void ShowRequestedLeader()
        {
            if (requestedLeader == null) return;
            var person = FactionLeader(requestedLeader);
            requestedLeader = null;
            ShowCharacterDetail(person, () => { ui.CloseModal(force: true); OpenWorldMap(); });
        }

        private string directoryFocus;

        private void ShowCharacterDirectory()
        {
            if (choosingBuilding) CancelBuild();
            ShowCharacterDirectory(0);
        }

        private void ShowCharacterDirectory(int filter) => ShowCharacterDirectory(filter, directoryFocus);

        private void ShowCharacterDirectory(int filter, string focusId)
        {
            ui.CloseBuilding();
            ui.CloseDialogue();
            var factions = WorldMapData.Load().Factions;
            var characters = StoryCharacters.Concat(state.Data.Disciples.Select(seed =>
            {
                var current = state.Disciples.FirstOrDefault(person => person.id == seed.id) ?? seed;
                return new StoryCharacter
                {
                    id = seed.id, name = current.name, gender = current.gender, age = current.age,
                    title = current.title, faction = state.Data.Start.sectName,
                    portrait = PortraitService.FindPortrait(seed.id) != null ? seed.id : current.portrait ?? seed.id,
                    rank = current.rank ?? (current.lines?.Length > 0 ? RankSystem.Martial(current.lines) : null),
                    cap = current.cap ?? (current.lines?.Length > 0 ? RankSystem.Martial(current.lines, true) : null), lines = current.lines,
                    personality = seed.personality ?? seed.trait, background = seed.background,
                    relation = seed.relation, signatureSkills = seed.signatureSkills, questline = seed.questline,
                    desc = seed.blurb, attrs = current.attrs
                };
            })).Concat(factions.Where(faction => state.IsFactionKnown(faction.id)).Select(FactionLeader))
                .GroupBy(person => person.id).Select(group => group.First());
            string[] filters = { "全部", "師門", "青梅", "敵對", "朝廷", "各派掌門", "新秀" };
            var visible = characters.Where(person =>
            {
                var faction = FactionOf(person, factions);
                bool leader = faction != null && faction.PortraitId == person.id;
                if (leader && !state.IsFactionKnown(faction.id)) return false;
                if (person.id is "friend_m" or "friend_f" && person.id != state.FriendId) return false;
                return filter switch
                {
                    1 => IsSectPerson(person),
                    2 => person.id == state.FriendId,
                    3 => IsHostile(person, faction),
                    4 => faction?.Court == true || person.id == "official",
                    5 => leader || (person.id?.StartsWith("leader_", StringComparison.Ordinal) == true),
                    6 => person.id?.StartsWith("prodigy_", StringComparison.Ordinal) == true,
                    _ => true
                };
            }).ToArray();
            int focus = Math.Max(0, Array.FindIndex(visible, person => person.id == focusId));
            directoryFocus = visible.Length > 0 ? visible[focus].id : null;
            int[] kinds = visible.Select(person => PersonKind(person, FactionOf(person, factions))).ToArray();
            string[] factionNames = visible.Select(person => FactionOf(person, factions)?.Label ?? person.faction).ToArray();
            ui.ShowDirectory(filters, filter, next => ShowCharacterDirectory(next, directoryFocus), visible, kinds, factionNames,
                visible.Length == 0 ? -1 : focus, index => ShowCharacterDirectory(filter, visible[index].id),
                visible.Length == 0 ? null : BuildDossier(visible[focus], factions, filter));
        }

        private static MapFaction FactionOf(StoryCharacter person, IEnumerable<MapFaction> factions) =>
            factions.FirstOrDefault(entry => entry.id == person.faction || entry.name == person.faction ||
                entry.PortraitId == person.id);

        private bool IsSectPerson(StoryCharacter person) =>
            person.id is "master" or "uncle_schemer" || person.faction == state.Data.Start.sectName;

        private bool IsHostile(StoryCharacter person, MapFaction faction) =>
            person.id is "elder_villain" or "warlord" or "rival_head" or "official" ||
            faction?.StanceWith(state.RelationShift(faction.id)) == "hostile";

        private int PersonKind(StoryCharacter person, MapFaction faction) =>
            IsSectPerson(person) || person.id == state.FriendId ? 0 : IsHostile(person, faction) ? 1 : 2;

        /// <summary>Collects the right-hand dossier for the selected character, including its context actions.</summary>
        private CharacterDossier BuildDossier(StoryCharacter person, IEnumerable<MapFaction> factions, int filter)
        {
            var faction = FactionOf(person, factions);
            int kind = PersonKind(person, faction);
            var disciple = state.Disciples.FirstOrDefault(entry => entry.id == person.id);
            bool sect = IsSectPerson(person);
            string stance = sect ? "本門" : person.id == state.FriendId ? "青梅故交" :
                faction != null ? faction.RelationWith(state.RelationShift(faction.id)) : kind == 1 ? "敵對" : "江湖散人";
            var lines = (person.lines ?? Array.Empty<SkillLine>()).Where(line => line != null && !string.IsNullOrEmpty(line.id))
                .OrderByDescending(line => RankSystem.Index(line.rank)).Take(4)
                .Select(line => (RankSystem.Categories.FirstOrDefault(category => category.id == line.id)?.name ?? line.id) +
                    " " + RankSystem.FormatKnown(line.rank, line.cap));
            string arts = string.Join("　", (person.signatureSkills ?? Array.Empty<string>()).Select(skill => "【" + skill + "】"));
            string tiers = string.Join("　", lines);
            if (!string.IsNullOrEmpty(tiers)) arts = string.IsNullOrEmpty(arts) ? tiers : arts + "\n" + tiers;
            string biography = string.Join("\n", new[]
            {
                string.IsNullOrWhiteSpace(person.personality) ? null : "性情　" + person.personality,
                person.background ?? person.desc
            }.Where(text => !string.IsNullOrWhiteSpace(text)));
            int relation = state.CharacterRelation(person.id);
            string bond = string.Join("\n", new[]
            {
                string.IsNullOrWhiteSpace(person.relation) ? null : FormatQuestText(person.relation),
                faction == null ? null : faction.Label + "與本門：" + faction.RelationWith(state.RelationShift(faction.id)),
                relation == 0 ? null : $"好感 {relation:+0;-0}"
            }.Where(text => !string.IsNullOrWhiteSpace(text)));

            Action back = () => ShowCharacterDirectory(filter, person.id);
            var actions = new List<(string, Action)>();
            if (sect)
            {
                actions.Add(("「門中問敘（交談）」", disciple != null ? () => { ui.CloseModal(force: true); Speak(disciple); }
                    : () => ShowCharacterDetail(person, back)));
                actions.Add(("「指點武學」", disciple != null ? () => { ui.CloseModal(force: true); ShowDisciple(disciple); }
                    : null));
            }
            else
            {
                bool seat = faction != null && state.IsFactionKnown(faction.id) && !string.IsNullOrEmpty(faction.seat);
                actions.Add(("「查看據點」", seat ? () =>
                {
                    ui.CloseModal(force: true);
                    OpenWorldMap();
                    map.FocusFaction(faction);
                } : null));
            }
            actions.Add(("「列傳詳情」", () => ShowCharacterDetail(person, back)));
            return new CharacterDossier
            {
                person = person, faction = faction?.Label ?? person.faction, stance = stance, kind = kind,
                relation = relation, attrs = person.attrs ?? disciple?.attrs ?? EstimateAttributes(person),
                estimated = person.attrs == null && disciple?.attrs == null, arts = arts, biography = biography,
                bond = bond, actions = actions.ToArray()
            };
        }

        /// <summary>Rumoured attributes for story characters without authored ones, scaled by martial tier.</summary>
        private static int[] EstimateAttributes(StoryCharacter person)
        {
            int tier = Math.Max(0, RankSystem.Index(person.rank));
            int seed = 17;
            foreach (char ch in person.id ?? "") seed = unchecked(seed * 31 + ch);
            var values = new int[AttributeSystem.Count];
            for (int index = 0; index < values.Length; index++)
            {
                int jitter = Math.Abs((seed >> (index * 3)) % 7) - 3;
                values[index] = Mathf.Clamp(8 + tier + jitter + (index == 0 || index == 3 ? tier / 2 : 0),
                    AttributeSystem.Min, AttributeSystem.Max);
            }
            return values;
        }

        private void ShowCharacterDetail(StoryCharacter person, Action back)
        {
            var faction = WorldMapData.Load().Factions.FirstOrDefault(entry =>
                entry.id == person.faction || entry.PortraitId == person.id);
            var quests = state.Questlines.Where(line => line.id == person.questline ||
                state.CharacterId(line.character) == person.id).ToArray();
            ui.ShowCharacterProfile(person, faction?.Label ?? person.faction, state.CharacterRelation(person.id),
                quests, state.QuestStatus, back);
        }

        private void PlayQuest(Questline line, QuestBeat beat, bool preferExpression = false)
        {
            var scene = beat.scene.Where(part => !string.IsNullOrWhiteSpace(part?.text)).ToArray();
            var choices = (beat.choices ?? Array.Empty<QuestChoice>()).Where(choice =>
                !string.IsNullOrWhiteSpace(choice?.text)).ToArray();
            string art = null;
            int index = preferExpression ? Array.FindIndex(scene,
                part => part.portrait?.Contains("__") == true) : 0;
            if (index < 0) index = 0;
            for (int prior = 0; prior < index; prior++)
                if (!string.IsNullOrEmpty(scene[prior].image)) art = scene[prior].image;
            ShowNext();

            void ShowNext()
            {
                if (index == scene.Length) { ShowChoices(); return; }
                var part = scene[index++];
                if (!string.IsNullOrEmpty(part.image)) art = part.image;
                if (art == "cg_friend_vow" && state.FriendId == "friend_m") art = "cg_friend_vow_m";
                var hero = state.Protagonist;
                string portrait = part.portrait == "hero" ? hero.portrait ?? hero.lookId :
                    PortraitService.StoryId(part.portrait, state.FriendId == "friend_m" ? "男" : "女");
                string speakerId = state.Disciples.FirstOrDefault(c =>
                    c.id == part.portrait?.Split(new[] { "__" }, StringSplitOptions.None)[0] || c.name == part.speaker)?.id;
                string speech = CastDialogue.CharacterSpeech(state, speakerId, FormatQuestText(part.text));
                ui.ShowStoryBeat(FormatQuestText(part.speaker), speech, art, portrait,
                    part.portrait != "hero", ShowNext, ShowChoices, "前往抉擇", part.text, hero.gender == "女");
            }

            void ShowChoices()
            {
                ui.ShowCreation(beat.title ?? line.title ?? "人物逸事", new[] { FormatQuestText(beat.summary) },
                    null, null, choices.Length == 0 ? () => Complete(null) : null);
                foreach (var choice in choices)
                    // v1124 B8: every questline choice shows its real effects (or a 「劇情：……」 hint).
                    ui.CreationChoice(FormatQuestText(choice.text), "影響：" + EffectText.Choice(choice, line, state),
                        null, () => Complete(choice));
            }

            void Complete(QuestChoice choice)
            {
                var roster = state.Disciples.ToArray();
                state.CompleteQuest(line, beat, choice);
                foreach (var disciple in state.Disciples.Except(roster))
                    world.SpawnDisciple(disciple);
                foreach (var disciple in roster.Except(state.Disciples))
                    world.RemoveDisciple(disciple);
                ui.CloseModal(force: true);
                ui.Refresh(state);
                ShowCommands();
                if (!capturing && !state.Save()) ui.Message("自動存檔失敗。");
            }
        }

        private string FormatQuestText(string text)
        {
            var hero = state.Protagonist;
            var friend = StoryCharacters.FirstOrDefault(person => person.id == state.FriendId);
            return (text ?? "").Replace("{title}", hero.title).Replace("{name}", hero.name)
                .Replace("{sect}", state.Data.Start.sectName).Replace("{friend}", friend?.name ?? "故友")
                .Replace("{he}", hero.gender == "女" ? "她" : "他");
        }
    }
}
