using System.Collections.Generic;
using System.Linq;

namespace Wuxia
{
    public sealed class MonthlyReport
    {
        public readonly List<string> Lines = new List<string>();
    }

    public sealed partial class GameState
    {
        private static readonly string[] Months =
        {
            "正月", "二月", "三月", "四月", "五月", "六月",
            "七月", "八月", "九月", "十月", "十一月", "臘月"
        };
        public readonly GameData Data;
        public readonly List<DiscipleData> Disciples;
        public readonly BuildingData[] Buildings;
        public int Year { get; private set; }
        public int Month { get; private set; }
        public int Silver { get; private set; }
        public int Food { get; private set; }
        public int Reputation { get; private set; }
        public int ActionPoints { get; private set; }
        public int ApMax => System.Math.Max(1, Data.Ap.baseMax + Story.apBonus + ApSources.Sum(entry => entry.amount));
        public string ApDescription => $"基礎 {Data.Ap.baseMax}" + (Story.apBonus != 0 ? $"　際遇 {Story.apBonus:+0;-0}" : "") +
            string.Concat(ApSources.Select(source => $"\n{source.name} {source.amount:+0;-0}"));

        private IEnumerable<(string name, int amount)> ApSources
        {
            get
            {
                foreach (var source in Data.Ap.sources ?? System.Array.Empty<ApSource>())
                {
                    int actual = source.type switch
                    {
                        "building" => BuildingLevel(source.@ref),
                        "age" => Protagonist.age,
                        "delegation" => Disciples.Any(d => d.id == Story.delegateId) ? 1 : 0,
                        "tech" => Story.flags.FirstOrDefault(entry => entry.id == source.@ref)?.value ?? 0,
                        "talent" => Protagonist.talents.Contains(source.@ref) ? 1 : 0,
                        "attr" => AttributeValue(source.@ref),
                        _ => -1
                    };
                    if (actual >= source.threshold)
                        yield return (source.desc, source.value);
                }
            }
        }

        /// <summary>Signed distance of a protagonist attribute from the base 10 (negative when lowered).</summary>
        internal int AttributeBonus(string id) => AttributeValue(id) - AttributeSystem.Base;

        private int AttributeValue(string id)
        {
            Protagonist.attrs = AttributeSystem.Normalize(Protagonist.attrs);
            int index = AttributeSystem.Index(id);
            return index < 0 ? 0 : Protagonist.attrs[index];
        }

        internal DerivedStats HeroStats => WithBonuses(AttributeSystem.Derive(Protagonist.attrs, Protagonist.lines,
            WeaponRankDelta(Protagonist.weapon)));

        /// <summary>Extra qi-yu (adventure) chance from 幸運, with a touch of 身法 for finding hidden paths.</summary>
        internal float AdventureBonus => .012f * AttributeBonus("luk") + .003f * AttributeBonus("agi");

        internal int BuildingLevel(string id) => Buildings != null && Buildings.Any(building => building?.id == id)
            ? (Story.buildingLevels.FirstOrDefault(entry => entry.id == id)?.value ?? 1) : 0;

        internal void UpgradeCouncil()
        {
            int level = BuildingLevel("council");
            if (level is <= 0 or >= 3)
                return;
            int silver = BuildSilver(18 * level);
            var cost = new Cost { silver = silver };
            if (!CanPay(cost) || !TrySpendAp(1, "council_upgrade"))
                return;
            Pay(cost);
            SetValue(ref Story.buildingLevels, "council", level + 1, false);
        }

        internal void ToggleDelegate(DiscipleData disciple)
        {
            Story.delegateId = Story.delegateId == disciple.id ? null : disciple.id;
            ActionPoints = System.Math.Min(ActionPoints, ApMax);
        }

        public bool TrySpendAp(int cost, string reason)
        {
            if (cost < 0)
                throw new System.ArgumentOutOfRangeException(nameof(cost), reason);
            if (cost > ActionPoints)
                return false;
            ActionPoints -= cost;
            return true;
        }
        public string Date => $"開元{(Year == 1 ? "元" : ChineseNumber(Year))}年 {Months[Month - 1]}";
        public string DateWithDay => Day <= 1 ? Date : $"{Date}{(Day <= 10 ? "初" : "")}{ChineseNumber(Day)}";

        public GameState(GameData data)
        {
            Data = data;
            Year = data.Start.year;
            Month = data.Start.month;
            Silver = data.Start.silver;
            Food = data.Start.food;
            Reputation = data.Start.reputation;
            // Creation base: the sect's sword line starts C / B, every other line D / C; picks then stack on top.
            Protagonist.lines = RankSystem.Seed(null, RankSystem.Catalog.protagonistPath?.defaultRank ?? RankSystem.PrimaryRank,
                RankSystem.Catalog.protagonistPath?.defaultCap ?? RankSystem.PrimaryCap, RankSystem.BaseCap, RankSystem.BaseRank);
            Disciples = data.Disciples.Where(d => d.startInSect).Select(CloneDisciple).ToList();
            foreach (var disciple in Disciples) EnsurePortrait(disciple);
            Buildings = new BuildingData[data.Start.slots.Length];
            for (int index = 0; index < Buildings.Length; index++)
            {
                string id = data.Start.slots[index].buildingId;
                var building = data.Buildings.FirstOrDefault(b => b.id == id);
                if (building != null && (!building.unique || !Buildings.Contains(building)))
                    Buildings[index] = building;
            }

            foreach (var building in data.Buildings.Where(b => b.startBuilt && !Buildings.Contains(b)))
            {
                int index = System.Array.IndexOf(Buildings, null);
                if (index < 0)
                    break;
                Buildings[index] = building;
            }
            ActionPoints = ApMax;
        }

        public bool IsAvailable(BuildingData building) => !building.unique || !Buildings.Contains(building);
        public bool CanPay(Cost cost, int extraSilver = 0) =>
            Silver >= cost.silver && Silver - cost.silver >= extraSilver && Food >= cost.food;

        public void Pay(Cost cost, int extraSilver = 0)
        {
            Silver -= cost.silver + extraSilver;
            Food -= cost.food;
        }

        public void Apply(Yield effect)
        {
            Silver += effect.silver;
            Food += effect.food;
            Reputation += effect.reputation;
        }

        public MonthlyReport EndMonth()
        {
            var report = new MonthlyReport();
            report.Lines.Add($"{Date}結算");
            int silver = 0, food = 0, reputation = 0, upkeep = 0;
            foreach (var building in Buildings)
            {
                if (building == null)
                    continue;
                silver += building.income.silver;
                food += building.income.food;
                reputation += building.income.reputation;
                upkeep += building.upkeep;
            }

            silver = UnityEngine.Mathf.RoundToInt(silver * ProductionMultiplier("commerce"));
            food = UnityEngine.Mathf.RoundToInt(food * ProductionMultiplier("farming"));
            M6MonthIncome(ref silver, ref food, report);
            int consumed = Disciples.Count * Data.Start.foodPerDisciple;
            Silver += silver - upkeep;
            Food += food - consumed;
            Reputation += reputation;
            report.Lines.Add($"建築收入　銀兩 {silver}、糧食 {food}、聲望 {reputation}");
            report.Lines.Add($"建築維護　銀兩 −{upkeep}");
            report.Lines.Add($"弟子口糧　糧食 −{consumed}");
            if (Food < 0)
            {
                Food = 0;
                Reputation--;
                report.Lines.Add("糧食短缺，聲望下降 1。");
            }
            if (Silver < 0)
            {
                Silver = 0;
                Reputation--;
                report.Lines.Add("銀兩不敷，拖欠工錢，聲望下降 1。");
            }

            Story.day = 1;
            if (++Month > 12)
            {
                Month = 1;
                Year++;
                Protagonist.age++;
            }
            string breakthrough = CheckMartialPath(true);
            if (!string.IsNullOrEmpty(breakthrough))
                report.Lines.Add(breakthrough);
            EvaluateQuestlines();
            RecoverInjuries();
            ActionPoints = ApMax;
            report.Lines.Add($"行動力已恢復 {ActionPoints}／{ApMax}。");
            report.Lines.Add($"本月結餘　銀兩 {Silver}、糧食 {Food}、聲望 {Reputation}");
            return report;
        }

        private static string ChineseNumber(int number)
        {
            const string digits = "零一二三四五六七八九";
            if (number < 10)
                return digits[number].ToString();
            if (number < 100)
                return (number < 20 ? "" : digits[number / 10].ToString()) +
                    "十" + (number % 10 == 0 ? "" : digits[number % 10].ToString());
            if (number < 1000)
                return $"{digits[number / 100]}百" +
                    (number % 100 == 0 ? "" : (number % 100 < 10 ? "零" : "") +
                        (number % 100 is >= 10 and < 20 ? "一" : "") + ChineseNumber(number % 100));
            return string.Concat(number.ToString().Select(digit => digits[digit - '0']));
        }
    }
}
