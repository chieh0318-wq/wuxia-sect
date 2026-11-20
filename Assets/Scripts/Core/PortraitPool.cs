using System;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    internal sealed class PortraitPool
    {
        [Serializable] private sealed class Catalog { public Entry[] pool; }
        [Serializable] private sealed class Entry { public string id, gender, age; public string[] tags; }

        private static Entry[] entries;
        private static Entry[] Load()
        {
            var catalog = M2Data.Load<Catalog>("portraits");
            if (catalog.pool?.Length > 0 != true)
                Debug.LogWarning("[PortraitPool] No catalog entries. Using legacy recruit portraits.");
            var legacy = from gender in new[] { "男", "女" }
                         from index in Enumerable.Range(1, 4)
                         select new Entry
                         {
                             id = $"recruit_{(gender == "女" ? "f" : "m")}{index}",
                             gender = gender, age = index <= 2 ? "youth" : "adult",
                             tags = new[] { "commoner", "martial" }
                         };
            return (catalog.pool ?? Array.Empty<Entry>()).Concat(legacy)
                .Where(entry => entry != null && IsGeneric(entry.id))
                .GroupBy(entry => entry.id).Select(group => group.First())
                .Where(entry => PortraitService.FindPortrait(entry.id) != null).ToArray();
        }

        internal static bool IsGeneric(string id) => id != null &&
            (id.StartsWith("npc_", StringComparison.Ordinal) || id.StartsWith("recruit_", StringComparison.Ordinal));

        internal static string Assign(StoryProgress story, DiscipleData person)
        {
            string band = person.age < 25 ? "youth" : person.age < 50 ? "adult" : "elder";
            string tag = RoleTag(person.origin + " " + person.title, person.gender);
            var available = (entries ??= Load()).Where(entry => entry.gender == person.gender).ToArray();
            var chosen = available.OrderBy(entry => Count(story, entry.id))
                .ThenByDescending(entry => (entry.age == band ? 2 : 0) +
                    (entry.tags?.Contains(tag) == true ? 2 : 0))
                .ThenBy(_ => UnityEngine.Random.value).FirstOrDefault();
            Track(story, chosen?.id);
            return chosen?.id;
        }

        private static int Count(StoryProgress story, string id) =>
            Array.Find(story.usedPortraits, entry => entry?.id == id)?.value ?? 0;

        internal static void Track(StoryProgress story, string id)
        {
            if (!IsGeneric(id)) return;
            var entry = Array.Find(story.usedPortraits, use => use?.id == id);
            if (entry != null)
                entry.value++;
            else
            {
                Array.Resize(ref story.usedPortraits, story.usedPortraits.Length + 1);
                story.usedPortraits[^1] = new StoryValue { id = id, value = 1 };
            }
        }

        private static string RoleTag(string description, string gender)
        {
            if (description.Contains("寺") || description.Contains("佛")) return gender == "女" ? "nun" : "monk";
            if (description.Contains("道觀") || description.Contains("道門")) return "taoist";
            if (description.Contains("西域") || description.Contains("胡") || description.Contains("粟特")) return "hu";
            if (description.Contains("草原") || description.Contains("突厥") || description.Contains("回紇")) return "steppe";
            if (description.Contains("商")) return "merchant";
            if (description.Contains("書") || description.Contains("儒")) return "scholar";
            if (description.Contains("官")) return "official";
            if (description.Contains("武")) return "martial";
            return "commoner";
        }
    }
}
