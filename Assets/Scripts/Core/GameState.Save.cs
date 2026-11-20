using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    public sealed partial class GameState
    {
        private const int SaveVersion = 1;
        private static string SavePath => Path.Combine(Application.persistentDataPath, "save_slot1.json");

        [Serializable]
        private sealed class SaveData
        {
            public int version, year, month, silver, food, reputation, actionPoints;
            public string[] buildings;
            public DiscipleData[] disciples;
            public ProtagonistData protagonist;
            public StoryProgress story;
        }

        public static bool HasSave => File.Exists(SavePath);

        public bool Save() => Save(SavePath);

        internal bool Save(string path)
        {
            try
            {
                var snapshot = new SaveData
                {
                    version = SaveVersion, year = Year, month = Month, silver = Silver, food = Food,
                    reputation = Reputation, actionPoints = ActionPoints,
                    buildings = Buildings.Select(building => building?.id ?? "").ToArray(),
                    disciples = Disciples.ToArray(), protagonist = Protagonist, story = Story
                };
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(snapshot, true));
                if (File.Exists(path))
                    File.Replace(temp, path, null);
                else
                    File.Move(temp, path);
                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[Save] {error.Message}");
                return false;
            }
        }

        public static GameState LoadSave(GameData data) => LoadSave(data, SavePath);

        internal static GameState LoadSave(GameData data, string path)
        {
            try
            {
                var snapshot = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (snapshot == null || snapshot.version != SaveVersion || snapshot.year < 1 ||
                    snapshot.month < 1 || snapshot.month > 12 || snapshot.buildings == null ||
                    snapshot.disciples == null || snapshot.disciples.Any(d => d == null || string.IsNullOrEmpty(d.id)))
                    throw new InvalidDataException("Unsupported or incomplete save.");

                var state = new GameState(data)
                {
                    Year = snapshot.year, Month = snapshot.month, Silver = snapshot.silver, Food = snapshot.food,
                    Reputation = snapshot.reputation
                };
                if (snapshot.protagonist != null)
                {
                    snapshot.protagonist.lines ??= state.Protagonist.lines;
                    state.Protagonist = snapshot.protagonist;
                }
                state.Story = snapshot.story ?? new StoryProgress();
                for (int index = 0; index < state.Buildings.Length; index++)
                {
                    string id = index < snapshot.buildings.Length ? snapshot.buildings[index] : "";
                    state.Buildings[index] = data.Buildings.FirstOrDefault(building => building.id == id);
                }
                state.Disciples.Clear();
                state.Disciples.AddRange(snapshot.disciples.Select(CloneDisciple));
                state.ActionPoints = Mathf.Clamp(snapshot.actionPoints, 0, state.ApMax);
                return state;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[Load] {error.Message}");
                return null;
            }
        }
    }
}
