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
        public string id, name, courtesy, title, role, gender, faction, portrait, rank, cap;
        public string personality, background, relation, questline, desc;
        public int age;
        public string[] expressions, signatureSkills;
        public CharacterModelSpec model;
        public SkillLine[] lines;
        public int[] attrs;
    }
    [Serializable] internal sealed class AttributeInfo { public string id, name, desc; }
    [Serializable] internal sealed class CharacterCatalog
    {
        public int attrBase = AttributeSystem.Base, attrMin = AttributeSystem.Min, attrMax = AttributeSystem.Max, attrPoints = 20;
        public AttributeInfo[] attributes;
        public CharacterLook[] protagonistLooks;
        public NameSuggestions[] nameSuggestions;
        public CreationOption[] backgrounds;
        public StoryCharacter[] storyCharacters;
    }

    [Serializable] internal sealed class TalentCatalog
    {
        public int budget = 20, maxPicks = 8, rerollFree;
        /// <summary>v1124 C13: explains the creation ceiling (開局上限至多 A) for the talent screen.</summary>
        public string ceilingNote;
        public CreationOption[] talents;
    }

    [Serializable] internal sealed class SkillCatalog { public int budget = 20; public CreationOption[] skills; public SkillCategory[] categories; }
    [Serializable] internal sealed class StoryBeat { public string speaker, portrait, image, text; }
    [Serializable] internal sealed class StoryResponse : CreationOption { public StoryBeat[] beats; }
    [Serializable] internal sealed class StoryDisaster : CreationOption
    {
        public StoryBeat[] beats;
        public string responsePrompt;
        /// <summary>v1124: 1 易 … 4 極難, authored in prologue.json and shown as a badge on the card.</summary>
        public int difficulty;
        public StoryResponse[] responses;
    }
    [Serializable] internal sealed class StoryLocation : CreationOption { public string node, region, resources, bonusText; }
    [Serializable] internal sealed class PrologueData
    {
        public StoryBeat[] openingBeats, closingBeats;
        /// <summary>v1124: 接任代掌門 (after the master vanishes) and the childhood friend's arrival as 軍師.</summary>
        public StoryBeat[] successionBeats, friendBeats;
        public string actingTitle = "代掌門";
        public string disasterPrompt, locationPrompt;
        public StoryDisaster[] disasters;
        public StoryLocation[] locations;
    }

    [Serializable] public sealed class ProtagonistData
    {
        public string name = "顧行舟", gender = "男", title = "三師兄", lookId = "hero_m1", backgroundId, portrait;
        public int age = 20;
        public int[] attrs = { 10, 10, 10, 10, 10, 10, 10 };
        public string[] talents = Array.Empty<string>(), skills = Array.Empty<string>();
        public bool noGraveInjury;
        public SkillLine[] lines;
        public int martialTraining, trainingMonths, lastTrainingMonth, injury;
        public string weapon;
        public string[] breakthroughs = Array.Empty<string>();
        /// <summary>v1124 C12: the chosen 主修 combat line; 「主修武學」 talents (ref "martial") land here.</summary>
        public string major = "sword";
        /// <summary>v1124 C13: per-line study speed in percent (id "" = every combat line).</summary>
        public StoryValue[] study = Array.Empty<StoryValue>();
        /// <summary>v1124 C14: martial arts learned after creation (秘笈、奇遇、秘境、師承), ids from martial_arts.json.</summary>
        public string[] arts = Array.Empty<string>();
        public CharacterLook look;
        /// <summary>Worn gear, at most one item per slot (武器 護腕 飾品 冠帽 衣袍 履靴).</summary>
        public GearItem[] gear = Array.Empty<GearItem>();
    }

    /// <summary>A forged or looted piece of wuxia gear with flat combat bonuses.</summary>
    [Serializable] public sealed class GearItem
    {
        public string slot, name, grade;
        public int attack, speed, hp, qi;
        public float dodge;
        internal int Power => attack * 2 + speed * 2 + hp / 4 + qi / 4 + Mathf.RoundToInt(dodge * 200);
        internal string Stats
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                if (attack != 0) parts.Add($"攻擊 +{attack}");
                if (speed != 0) parts.Add($"先手 +{speed}");
                if (hp != 0) parts.Add($"氣血 +{hp}");
                if (qi != 0) parts.Add($"內力 +{qi}");
                if (dodge > 0) parts.Add($"閃避 +{dodge:P0}");
                return string.Join("・", parts);
            }
        }
    }

    [Serializable] public sealed class StoryProgress
    {
        public string disasterId, responseId, locationId, mapNode, delegateId, partyNode;
        public int day = 1;
        public string[] knownFactions = Array.Empty<string>();
        public StoryValue[] buildingLevels = Array.Empty<StoryValue>();
        public int apBonus, loyaltyBonus, recruitPoolSize, recruitCostPct, buildCostPct, trainBonusPct;
        public float insightChance;
        public StoryValue[] characterRelations = Array.Empty<StoryValue>();
        public QuestProgress[] questProgress = Array.Empty<QuestProgress>();
        public QueuedQuest[] queuedQuests = Array.Empty<QueuedQuest>();
        public int questMonth;
        public StoryValue[] usedPortraits = Array.Empty<StoryValue>();
        public StoryValue[] relations = Array.Empty<StoryValue>(), flags = Array.Empty<StoryValue>();
        /// <summary>M6: gear kept in the sect armoury, strongholds taken by the sect, and the next qi-yu boost.</summary>
        public GearItem[] armoury = Array.Empty<GearItem>();
        public string[] heldNodes = Array.Empty<string>();
        public float rumorBonus;
        public int lodgeMonth, realmsCleared;
        /// <summary>M9: carried consumables & legacy items (id -> quantity).</summary>
        public StoryValue[] inventory = Array.Empty<StoryValue>();
        /// <summary>Diplomacy: ties per faction and the pending 外交急報 (event id + target faction).</summary>
        public DiploTie[] ties = Array.Empty<DiploTie>();
        public string diploEvent, diploEventTarget;
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
