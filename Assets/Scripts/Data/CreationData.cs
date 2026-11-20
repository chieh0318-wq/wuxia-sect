using System;
using System.IO;
using UnityEngine;

namespace Wuxia
{
    [Serializable] public sealed class GameEffect { public string key, @ref; public float value; }

    [Serializable] public class CreationOption
    {
        public string id, name, desc, summary, image, rarity, category, grantsRank;
        public int cost, weight = 10, capBonus;
        public GameEffect[] effects;
    }

    [Serializable] public sealed class CharacterLook
    {
        public string id, gender, label, desc, portrait, robeColor, sashColor, hairColor, hairStyle, role;
        public float skinTone = .5f, build = .5f;
        public int age;
        public string origin;
        public CharacterModelSpec model;
    }

    [Serializable] public sealed class CharacterModelSpec
    {
        public string @base, hair, beard, robe, robe2, sash, style;
        internal bool HasOverrides => !string.IsNullOrEmpty(@base) || !string.IsNullOrEmpty(hair) ||
            !string.IsNullOrEmpty(beard) || !string.IsNullOrEmpty(robe) || !string.IsNullOrEmpty(robe2) ||
            !string.IsNullOrEmpty(sash) || !string.IsNullOrEmpty(style);
    }

    [Serializable] internal sealed class NameSuggestions { public string gender; public string[] names; }
    [Serializable] internal sealed class StoryCharacter
    {
        public string id, name, gender, portrait, rank, cap, role, faction;
        public int age;
        public CharacterModelSpec model;
        public SkillLine[] lines;
    }
    [Serializable] internal sealed class CharacterCatalog
    {
        public int attrBase = 3, attrMax = 10, attrPoints = 8;
        public CharacterLook[] protagonistLooks;
        public NameSuggestions[] nameSuggestions;
        public CreationOption[] backgrounds;
        public StoryCharacter[] storyCharacters;
    }

    [Serializable] internal sealed class TalentCatalog
    {
        public int budget = 10, maxPicks = 4, rerollFree = 3;
        public CreationOption[] talents;
    }

    [Serializable] internal sealed class SkillCatalog { public int budget = 6; public CreationOption[] skills; public SkillCategory[] categories; }
    [Serializable] internal sealed class StoryBeat { public string speaker, portrait, image, text; }
    [Serializable] internal sealed class StoryResponse : CreationOption { public StoryBeat[] beats; }
    [Serializable] internal sealed class StoryDisaster : CreationOption
    {
        public StoryBeat[] beats;
        public string responsePrompt;
        public StoryResponse[] responses;
    }
    [Serializable] internal sealed class StoryLocation : CreationOption { public string node, region, resources, bonusText; }
    [Serializable] internal sealed class PrologueData
    {
        public StoryBeat[] openingBeats, closingBeats;
        public string disasterPrompt, locationPrompt;
        public StoryDisaster[] disasters;
        public StoryLocation[] locations;
    }

    [Serializable] public sealed class ProtagonistData
    {
        public string name = "顧行舟", gender = "男", title = "大師兄", lookId = "hero_m1", backgroundId, portrait;
        public int age = 20;
        public int[] attrs = { 3, 3, 3, 3, 3 };
        public string[] talents = Array.Empty<string>(), skills = Array.Empty<string>();
        public bool noGraveInjury;
        public SkillLine[] lines;
        public int martialTraining, trainingMonths, lastTrainingMonth, injury;
        public string weapon;
        public string[] breakthroughs = Array.Empty<string>();
        public CharacterLook look;
    }

    [Serializable] public sealed class StoryProgress
    {
        public string disasterId, responseId, locationId, mapNode, delegateId, partyNode;
        public int day = 1;
        public string[] knownFactions = Array.Empty<string>();
        public StoryValue[] buildingLevels = Array.Empty<StoryValue>();
        public int apBonus, loyaltyBonus, recruitPoolSize, recruitCostPct, buildCostPct, trainBonusPct;
        public float insightChance;
        public StoryValue[] relations = Array.Empty<StoryValue>(), flags = Array.Empty<StoryValue>();
    }

    [Serializable] public sealed class StoryValue { public string id; public int value; }

    internal static class M2Data
    {
        internal static T Load<T>(string name) where T : class, new()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Data", name + ".json");
            try
            {
                return File.Exists(path) ? JsonUtility.FromJson<T>(File.ReadAllText(path)) ?? new T() : new T();
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[Data] {name}: {error.Message}");
                return new T();
            }
        }
    }
}
