using System.Collections.Generic;
using System.Linq;

namespace Wuxia
{
    public sealed class MonthlyReport
    {
        public readonly List<string> Lines = new List<string>();
    }

    public sealed class GameState
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
        public string Date => $"開元{(Year == 1 ? "元" : ChineseNumber(Year))}年 {Months[Month - 1]}";

        public GameState(GameData data)
        {
            Data = data;
            Year = data.Start.year;
            Month = data.Start.month;
            Silver = data.Start.silver;
            Food = data.Start.food;
            Reputation = data.Start.reputation;
            Disciples = data.Disciples.Where(d => d.startInSect).ToList();
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

            if (++Month > 12)
            {
                Month = 1;
                Year++;
            }
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
