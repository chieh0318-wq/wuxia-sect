using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    internal static class AssignmentSystem
    {
        internal readonly struct Rank
        {
            internal readonly string Name;
            internal readonly int Contribution, Monthly, Ability, Troops;

            internal Rank(string name, int contribution, int monthly, int ability, int troops)
            {
                Name = name;
                Contribution = contribution;
                Monthly = monthly;
                Ability = ability;
                Troops = troops;
            }
        }

        internal static readonly Rank[] Ladder =
        {
            new("雜役", 0, 1, 0, 100),
            new("外門", 4, 1, 0, 250),
            new("內門", 12, 2, 1, 500),
            new("親傳", 24, 2, 2, 800),
            new("長老", 42, 3, 3, 1200)
        };

        internal static readonly Rank Head = new("代掌門", 0, 3, 0, 1600);
        internal static int Grade(GameState state, string id) => id == GameState.HeroId ? Ladder.Length :
            id == state.FriendId ? Ladder.Length - 1 :
            Mathf.Clamp(Record(state, id)?.rank ?? (state.IsNamedDisciple(id) ? 2 : 0), 0, Ladder.Length - 1);
        internal static Rank RankOf(GameState state, string id) => id == GameState.HeroId ? Head : Ladder[Grade(state, id)];
        internal static int Contribution(GameState state, string id) => Record(state, id)?.contribution ?? 0;

        private static AssignmentRecord Record(GameState state, string id) =>
            state.Story.assignments?.FirstOrDefault(entry => entry?.id == id);

        private static AssignmentRecord GetRecord(GameState state, string id)
        {
            var record = Record(state, id);
            if (record != null) return record;
            record = new AssignmentRecord { id = id, rank = Grade(state, id) };
            state.Story.assignments = (state.Story.assignments ?? Array.Empty<AssignmentRecord>()).Append(record).ToArray();
            return record;
        }

        private static int Month(GameState state) => state.Year * 12 + state.Month;

        internal static int Remaining(GameState state, string id)
        {
            var record = Record(state, id);
            int used = record?.month == Month(state) ? record.used : 0;
            return Mathf.Max(0, RankOf(state, id).Monthly - used);
        }

        internal static string Block(GameState state, string id)
        {
            if (state.IsDispatched(id)) return "正在外派";
            if (id == GameState.HeroId)
            {
                if (state.Protagonist.injury >= 3) return "傷勢未癒";
            }
            else
            {
                var member = state.Disciples.FirstOrDefault(d => d?.id == id);
                if (member == null) return "未在門中";
                if (member.injury >= 3) return "傷勢未癒";
                if (id == state.Story.delegateId) return "正專責門務";
            }
            return Remaining(state, id) == 0 ? "本月次數已用完" : null;
        }

        internal static bool Commit(GameState state, IEnumerable<string> ids, string action = null)
        {
            var party = ids.Distinct().ToArray();
            if (party.Length == 0 || party.Any(id => Block(state, id) != null)) return false;
            foreach (string id in party)
            {
                var record = GetRecord(state, id);
                if (record.month != Month(state))
                {
                    record.month = Month(state);
                    record.used = 0;
                }
                record.used++;
                if (action != null) state.PracticeWork(id, Relevance(action).Skill);
            }
            return true;
        }

        internal static void Credit(GameState state, string id, int amount)
        {
            if (!string.IsNullOrEmpty(id) && id != GameState.HeroId) GetRecord(state, id).contribution += amount;
        }

        internal static bool CanPromote(GameState state, string id)
        {
            int rank = Grade(state, id);
            return id != GameState.HeroId && rank + 1 < Ladder.Length &&
                Contribution(state, id) >= Ladder[rank + 1].Contribution;
        }

        internal static void Promote(GameState state, string id)
        {
            if (CanPromote(state, id)) GetRecord(state, id).rank++;
        }

        internal static int Skill(GameState state, string id, string key)
        {
            var lines = id == GameState.HeroId ? state.Protagonist.lines : state.Disciples.FirstOrDefault(d => d.id == id)?.lines;
            return RankSystem.Index(lines?.FirstOrDefault(line => line?.id == key)?.rank);
        }

        internal static int Attribute(GameState state, string id, string key) => id == GameState.HeroId
            ? AttributeSystem.Value(state.HeroAttributes, key)
            : AttributeSystem.Value(state.DiscipleAttributes(state.Disciples.FirstOrDefault(d => d.id == id)), key);

        internal static int Eloquence(GameState state, string id) =>
            Attribute(state, id, "cha") + Attribute(state, id, "wit") / 2;

        internal static (string Skill, string Attribute) Relevance(string actionId) => actionId switch
            {
                "forge" or "forge_gear" => ("forging", "str"),
                "alchemy" => ("alchemy", "wit"),
                "treat" or "medicine" => ("medicine", "wit"),
                "farming" or "feast" => ("farming", "con"),
                "build" or "gather_materials" => ("construction", "wit"),
                "commerce" or "recruit" or "host" or "diplomacy" => ("commerce", "cha"),
                "train" or "lecture" or "study" => ("teaching", "wit"),
                "seclusion" => ("neigong", "qi"),
                "gather_rumors" => ("commerce", "cha"),
                "explore" or "secret_realm" => ("qinggong", "luk"),
                "council" or "decree" => ("commerce", "cha"),
                "lodge" => ("construction", "wit"),
                _ => (null, "wit")
            };

        internal static (float Success, float Quality, string Detail) Predict(GameState state, string actionId, string id)
        {
            var (skill, attribute) = Relevance(actionId);
            int rank = Skill(state, id, skill);
            int value = attribute == "cha" ? Eloquence(state, id) : Attribute(state, id, attribute);
            int basis = attribute == "cha" ? 15 : 10;
            float quality = AttributeSystem.WorkQuality(rank, value, basis);
            string talentKey = actionId == "diplomacy" ? "diplomacy" : skill == "forging" ? "forge" : skill;
            quality *= 1 + (talentKey == null ? 0 : state.TalentValue(id, talentKey, 20)) / 100f;
            float success = actionId switch
            {
                "explore" => state.PatrolChance(id),
                "gather_rumors" => state.RumorDiscoveryChance(id),
                _ => 1
            };
            string detail = $"{EffectText.LineName(skill)} {RankSystem.At(rank)}・{(attribute == "cha" ? "口才" : AttributeSystem.Names[AttributeSystem.Index(attribute)])} {value}";
            return (success, quality, detail);
        }
    }
}
