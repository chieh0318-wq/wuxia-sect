using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Wuxia
{
    [Serializable] public class Cost { public int silver, food; }
    [Serializable] public class Yield { public int silver, food, reputation; }
    [Serializable] public class BuildingData
    {
        public string id, name, description, color;
        public float[] size;
        public Cost cost;
        public int upkeep;
        public Yield income;
        public bool unique, startBuilt;
        public string[] actions;
    }

    [Serializable] public class ApSource
    {
        public string id, type, @ref, desc;
        public int threshold, value;
    }

    [Serializable] public class ApData
    {
        public int baseMax = 4;
        public ApSource[] sources;
    }

    [Serializable] public class ActionData
    {
        public string id, name, description, type;
        public int apCost;
        public Cost cost;
        public Yield effect;
    }

    [Serializable] public class DiscipleData
    {
        public string id, name, gender, title, trait, robeColor, portrait;
        public int age, injury;
        public int loyalty = 50;
        public bool startInSect;
        public string[] dialogue;
        public SkillLine[] lines;
        public string rank, cap, weapon;
        public int[] attrs;
        public string[] talents;
        public string origin, blurb, personality;
        public Aptitude aptitude;
    }

    [Serializable] public class SlotData
    {
        public float x, z;
        public string buildingId;
    }

    [Serializable] public class StartData
    {
        public string sectName;
        public int year, month, silver, food, reputation, recruitCost, foodPerDisciple;
        public SlotData[] slots;
    }

    public sealed class GameData
    {
        [Serializable] private class BuildingFile { public BuildingData[] buildings; }
        [Serializable] private class ActionFile { public ActionData[] actions; }
        [Serializable] private class DiscipleFile
        {
            public DiscipleData[] disciples;
            public string[] generalLines;
        }

        public readonly BuildingData[] Buildings;
        public readonly ActionData[] Actions;
        public readonly DiscipleData[] Disciples;
        public readonly string[] GeneralLines;
        public readonly StartData Start;
        public readonly ApData Ap;

        private GameData()
        {
            Ap = Read<ApData>("ap.json", file => file.baseMax > 0) ?? new ApData();
            var buildings = Read<BuildingFile>("buildings.json", file => ValidIds(file.buildings, b => b.id) &&
                file.buildings.All(b => HasText(b.name) && b.size != null && b.size.Length == 3 &&
                    b.size.All(n => n > 0 && !float.IsInfinity(n)) && ValidCost(b.cost) && b.upkeep >= 0 &&
                    b.income != null && b.actions != null));
            Buildings = buildings?.buildings ?? new[]
            {
                new BuildingData { id = "hall", name = "大殿", description = "門中議事之所。", color = "#A76849",
                    size = new[] { 5f, 2f, 4f }, unique = true, startBuilt = true,
                    cost = new Cost(), income = new Yield { silver = 3 }, actions = new[] { "recruit" } },
                new BuildingData { id = "kitchen", name = "伙房", description = "按月備糧。", color = "#92805E",
                    size = new[] { 4f, 2f, 3f }, cost = new Cost { silver = 15 },
                    income = new Yield { food = 4 }, actions = Array.Empty<string>() }
            };
            var actions = Read<ActionFile>("actions.json", file => ValidIds(file.actions, a => a.id) &&
                file.actions.All(a => HasText(a.name) && ValidCost(a.cost) && a.apCost >= 0 && a.effect != null &&
                    (a.type == "generic" || a.type == "recruit" || a.type == "build")));
            Actions = actions?.actions ?? new[]
            {
                new ActionData { id = "build", name = "建造", type = "build", apCost = 2, cost = new Cost(), effect = new Yield() },
                new ActionData { id = "recruit", name = "招收弟子", type = "recruit", apCost = 1, cost = new Cost(), effect = new Yield() }
            };
            var disciples = Read<DiscipleFile>("disciples.json", file => ValidIds(file.disciples, d => d.id) &&
                file.disciples.All(d => HasText(d.name)));
            Disciples = disciples?.disciples ?? new[]
            {
                new DiscipleData { id = "lin", name = "林照", title = "入門弟子", trait = "勤勉", startInSect = true },
                new DiscipleData { id = "shen", name = "沈青", title = "入門弟子", trait = "沉著" }
            };
            GeneralLines = disciples?.generalLines ?? new[] { "今日院中一切如常。" };
            Start = Read<StartData>("start.json", file => HasText(file.sectName) && file.year >= 1 &&
                file.month >= 1 && file.month <= 12 && file.silver >= 0 && file.food >= 0 &&
                file.recruitCost >= 0 && file.foodPerDisciple >= 1 && file.slots != null &&
                file.slots.Length > 0 && file.slots.All(slot => slot != null &&
                    Math.Abs(slot.x) <= 19f && Math.Abs(slot.z) <= 19f) &&
                file.slots.Select(slot => (slot.x, slot.z)).Distinct().Count() == file.slots.Length)
                ?? new StartData { sectName = "崤山劍門", year = 1, month = 1, silver = 60, food = 24,
                    recruitCost = 12, foodPerDisciple = 1, slots = new[]
                    {
                        new SlotData { x = -8, buildingId = "hall" },
                        new SlotData { x = 0 }, new SlotData { x = 8 }
                    } };
        }

        public static GameData Load() => new GameData();

        private static bool HasText(string value) => !string.IsNullOrWhiteSpace(value);
        private static bool ValidCost(Cost cost) => cost != null && cost.silver >= 0 && cost.food >= 0;

        private static bool ValidIds<T>(T[] items, Func<T, string> id) where T : class =>
            items != null && items.Length > 0 && items.All(item => item != null && HasText(id(item))) &&
            items.Select(id).Distinct().Count() == items.Length;

        private static T Read<T>(string name, Predicate<T> valid) where T : class
        {
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, "Data", name);
                T result = JsonUtility.FromJson<T>(File.ReadAllText(path, Encoding.UTF8));
                if (result == null || !valid(result))
                    throw new InvalidDataException("Required data is missing or invalid.");
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Wuxia: {name}: {ex.Message} Using built-in defaults.");
                return null;
            }
        }
    }
}
