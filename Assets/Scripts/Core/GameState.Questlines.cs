using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    [Serializable] internal sealed class QuestCatalog { public Questline[] questlines; }
    [Serializable] internal sealed class Questline
    {
        public string id, character, title, summary;
        public QuestBeat[] beats;
    }
    [Serializable] internal sealed class QuestBeat
    {
        public string id, title, summary, outline;
        public bool playable;
        public QuestTrigger trigger;
        public StoryBeat[] scene;
        public QuestChoice[] choices;
        internal bool HasScene => playable && scene?.Any(part => !string.IsNullOrWhiteSpace(part?.text)) == true;
    }
    [Serializable] internal sealed class QuestTrigger
    {
        public int minMonth, maxMonth, minRelation = -100;
        public string flag, notFlag, disaster, response, location;
    }
    [Serializable] internal sealed class QuestChoice
    {
        public string text, setFlag, next;
        public int relation;
        public GameEffect[] effects;
    }
    [Serializable] public sealed class QuestProgress
    {
        public string id, next;
        public string[] completed = Array.Empty<string>();
    }
    [Serializable] public sealed class QueuedQuest
    {
        public string quest, beat;
    }

    public sealed partial class GameState
    {
        private Questline[] questlines;
        internal Questline[] Questlines => questlines ??= LoadQuestlines();

        private static Questline[] LoadQuestlines()
        {
            var catalog = M2Data.Load<QuestCatalog>("questlines");
            if (catalog.questlines?.Length > 0 != true)
                Debug.LogWarning("[Questlines] No questline data available.");
            var valid = (catalog.questlines ?? Array.Empty<Questline>()).Where(line =>
                line != null && !string.IsNullOrEmpty(line.id)).ToArray();
            foreach (var line in valid)
            {
                line.beats = (line.beats ?? Array.Empty<QuestBeat>())
                    .Where(beat => beat != null && !string.IsNullOrEmpty(beat.id)).ToArray();
                foreach (var beat in line.beats.Where(beat => beat.playable && !beat.HasScene))
                    Debug.LogWarning($"[Questlines] Skipping incomplete playable beat {beat.id}.");
            }
            return valid;
        }

        internal string FriendId => Protagonist.gender == "女" ? "friend_m" : "friend_f";
        internal string CharacterId(string id) => id == "friend" ? FriendId : id;
        internal int CharacterRelation(string id) => ValueOf(Story.characterRelations, CharacterId(id));
        private static int ValueOf(StoryValue[] values, string id) =>
            Array.Find(values, entry => entry?.id == id)?.value ?? 0;

        internal QuestProgress QuestStatus(string id) => Array.Find(Story.questProgress, entry => entry?.id == id);
        private int CampaignMonth => (Year - Data.Start.year) * 12 + Month - Data.Start.month + 1;

        internal void EvaluateQuestlines()
        {
            int month = CampaignMonth;
            if (Story.questMonth >= month) return;

            // Early personal stories get first claim on a month when several beats unlock together.
            foreach (var line in Questlines.OrderBy(line => QuestPriority(CharacterId(line.character))))
            {
                if (line.character is "friend_m" or "friend_f" && line.character != FriendId) continue;
                if (Array.Exists(Story.queuedQuests, entry => entry?.quest == line.id)) continue;
                var progress = QuestStatus(line.id);
                var beat = progress == null ? line.beats.FirstOrDefault() :
                    Array.Find(line.beats, entry => entry.id == progress.next);
                if (beat?.HasScene != true || !Matches(beat.trigger, line.character, month)) continue;
                Story.questMonth = month;
                Array.Resize(ref Story.queuedQuests, Story.queuedQuests.Length + 1);
                Story.queuedQuests[^1] = new QueuedQuest { quest = line.id, beat = beat.id };
                break;
            }
        }

        private int QuestPriority(string character) => character == "master" ? 0 :
            character == FriendId ? 1 : character == "uncle_schemer" ? 2 : 3;

        private bool Matches(QuestTrigger trigger, string character, int month) => trigger == null ||
            (month >= trigger.minMonth && (trigger.maxMonth <= 0 || month <= trigger.maxMonth)
                && (string.IsNullOrEmpty(trigger.flag) || ValueOf(Story.flags, trigger.flag) != 0)
                && (string.IsNullOrEmpty(trigger.notFlag) || ValueOf(Story.flags, trigger.notFlag) == 0)
                && (string.IsNullOrEmpty(trigger.disaster) || trigger.disaster == Story.disasterId)
                && (string.IsNullOrEmpty(trigger.response) || trigger.response == Story.responseId)
                && (string.IsNullOrEmpty(trigger.location) || trigger.location == Story.locationId || trigger.location == PartyNode)
                && CharacterRelation(character) >= trigger.minRelation);

        internal (Questline line, QuestBeat beat) PendingQuest()
        {
            while (Story.queuedQuests.Length > 0)
            {
                var pending = Story.queuedQuests[0];
                var line = Array.Find(Questlines, entry => entry.id == pending?.quest);
                var beat = line == null ? null : Array.Find(line.beats, entry => entry.id == pending?.beat);
                if (beat?.HasScene == true) return (line, beat);
                Debug.LogWarning($"[Questlines] Saved beat {pending?.beat} is unavailable.");
                DequeueQuest();
            }
            return (null, null);
        }

        internal void CompleteQuest(Questline line, QuestBeat beat, QuestChoice choice)
        {
            if (Story.queuedQuests.FirstOrDefault()?.quest != line.id ||
                Story.queuedQuests[0].beat != beat.id) return;

            var progress = QuestStatus(line.id);
            if (progress == null)
            {
                progress = new QuestProgress { id = line.id };
                Array.Resize(ref Story.questProgress, Story.questProgress.Length + 1);
                Story.questProgress[^1] = progress;
            }
            progress.completed = (progress.completed ?? Array.Empty<string>()).Append(beat.id).ToArray();
            progress.next = choice?.next;
            if (!string.IsNullOrEmpty(progress.next) && !Array.Exists(line.beats, entry => entry.id == progress.next))
                Debug.LogWarning($"[Questlines] Unknown next beat {progress.next} in {line.id}.");

            ApplyEffects(choice?.effects);
            if (!string.IsNullOrEmpty(choice?.setFlag))
                SetValue(ref Story.flags, choice.setFlag, 1, false);
            if ((choice?.relation ?? 0) != 0)
                SetValue(ref Story.characterRelations, CharacterId(line.character), choice.relation, true);
            DequeueQuest();
        }

        private void DequeueQuest() => Story.queuedQuests = Story.queuedQuests.Skip(1).ToArray();
    }
}
