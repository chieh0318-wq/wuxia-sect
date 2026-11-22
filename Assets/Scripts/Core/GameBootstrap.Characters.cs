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

        private void ShowCharacterDirectory()
        {
            if (choosingBuilding) CancelBuild();
            ShowCharacterDirectory(0);
        }

        private void ShowCharacterDirectory(int filter)
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
                    portrait = current.portrait ?? seed.id, rank = current.rank ?? (current.lines?.Length > 0 ? RankSystem.Martial(current.lines) : null),
                    cap = current.cap ?? (current.lines?.Length > 0 ? RankSystem.Martial(current.lines, true) : null), lines = current.lines,
                    personality = seed.personality ?? seed.trait, background = seed.background,
                    relation = seed.relation, signatureSkills = seed.signatureSkills, questline = seed.questline,
                    desc = seed.blurb
                };
            })).Concat(factions.Where(faction => state.IsFactionKnown(faction.id)).Select(FactionLeader))
                .GroupBy(person => person.id).Select(group => group.First());
            string[] filters = { "全部", "師門", "青梅", "敵對", "朝廷", "各派掌門", "新秀" };
            var visible = characters.Where(person =>
            {
                var faction = factions.FirstOrDefault(entry => entry.id == person.faction || entry.name == person.faction ||
                    entry.PortraitId == person.id);
                bool leader = faction != null && faction.PortraitId == person.id;
                if (leader && !state.IsFactionKnown(faction.id)) return false;
                if (person.id is "friend_m" or "friend_f" && person.id != state.FriendId) return false;
                return filter switch
                {
                    1 => person.id is "master" or "uncle_schemer" || person.faction == state.Data.Start.sectName,
                    2 => person.id == state.FriendId,
                    3 => person.id is "elder_villain" or "warlord" or "rival_head" or "official" ||
                        faction?.StanceWith(state.RelationShift(faction.id)) == "hostile",
                    4 => faction?.Court == true || person.id == "official",
                    5 => leader || (person.id?.StartsWith("leader_", StringComparison.Ordinal) == true),
                    6 => person.id?.StartsWith("prodigy_", StringComparison.Ordinal) == true,
                    _ => true
                };
            }).ToArray();
            ui.ShowDirectory(filters, filter, ShowCharacterDirectory, visible,
                person => ShowCharacterDetail(person, () => ShowCharacterDirectory(filter)));
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
                var hero = state.Protagonist;
                string portrait = part.portrait == "hero" ? hero.portrait ?? hero.lookId :
                    PortraitService.StoryId(part.portrait, state.FriendId == "friend_m" ? "男" : "女");
                ui.ShowStoryBeat(FormatQuestText(part.speaker), FormatQuestText(part.text), art, portrait,
                    part.portrait != "hero", ShowNext, ShowChoices, "前往抉擇", part.text, hero.gender == "女");
            }

            void ShowChoices()
            {
                ui.ShowCreation(beat.title ?? line.title ?? "人物逸事", new[] { FormatQuestText(beat.summary) },
                    null, null, choices.Length == 0 ? () => Complete(null) : null);
                foreach (var choice in choices)
                    ui.CreationChoice(FormatQuestText(choice.text), "", null, () => Complete(choice));
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
