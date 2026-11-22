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
                    // Tolerate saves with missing/partial fields: JsonUtility leaves absent arrays empty.
                    var hero = snapshot.protagonist;
                    hero.lines = hero.lines == null || hero.lines.Length == 0 ? state.Protagonist.lines
                        : RankSystem.Seed(hero.lines, null, null, RankSystem.BaseCap, RankSystem.BaseRank);
                    var attrs = hero.attrs ?? Array.Empty<int>();
                    hero.attrs = AttributeSystem.Normalize(attrs);
                    hero.talents ??= Array.Empty<string>();
                    hero.skills ??= Array.Empty<string>();
                    hero.breakthroughs ??= Array.Empty<string>();
                    hero.gear ??= Array.Empty<GearItem>();
                    state.Protagonist = hero;
                }
                state.Story = snapshot.story ?? new StoryProgress();
                state.Story.knownFactions ??= Array.Empty<string>();
                state.Story.buildingLevels ??= Array.Empty<StoryValue>();
                state.Story.relations ??= Array.Empty<StoryValue>();
                state.Story.flags ??= Array.Empty<StoryValue>();
                state.Story.characterRelations ??= Array.Empty<StoryValue>();
                state.Story.questProgress ??= Array.Empty<QuestProgress>();
                state.Story.queuedQuests ??= Array.Empty<QueuedQuest>();
                state.Story.armoury ??= Array.Empty<GearItem>();
                state.Story.heldNodes ??= Array.Empty<string>();
                for (int index = 0; index < state.Buildings.Length; index++)
                {
                    string id = index < snapshot.buildings.Length ? snapshot.buildings[index] : "";
                    state.Buildings[index] = data.Buildings.FirstOrDefault(building => building.id == id);
                }
                state.Disciples.Clear();
                state.Disciples.AddRange(snapshot.disciples.Select(CloneDisciple));
                bool migratePortraits = state.Story.usedPortraits?.Length > 0 != true;
                state.Story.usedPortraits ??= Array.Empty<StoryValue>();
                foreach (var disciple in state.Disciples)
                {
                    // Named disciples always resync to their own portrait, overriding stale ids from old saves.
                    if (state.IsNamedDisciple(disciple.id) && PortraitService.FindPortrait(disciple.id) != null)
                        disciple.portrait = disciple.id;
                    if (migratePortraits) PortraitPool.Track(state.Story, disciple.portrait);
                    state.EnsurePortrait(disciple);
                }
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
