using System;
using System.Linq;

namespace Wuxia
{
    [Serializable] internal sealed class FactionRelations
    {
        [Serializable] internal sealed class Entry { public string id, reason; }
        public Entry[] entries = Array.Empty<Entry>();
        internal static readonly FactionRelations Data = M2Data.Load<FactionRelations>("faction_relations");
    }

    public sealed partial class GameState
    {
        internal string[] GoalLines()
        {
            var victory = Victory;
            var rules = victory.Rules;
            return new[]
            {
                $"立穩山門｜弟子 {Disciples.Count}/6、建築 {Buildings.Count(building => building != null)}/8、聲望 {Reputation}/22。達標後可擴建。",
                "立足江湖｜接掌宗門之後，可從軍師與情報頁了解各派立場；興建宗門、結交盟友或攻占據點皆可累積聲望。",
                $"外交結盟｜友好勢力 {victory.Loyal}/{rules.loyalNeeded}、歸附勢力 {victory.Subjects}/{rules.subjectNeeded}、聲望 {victory.Reputation}/{rules.reputationNeeded}。達標後在外交頁宣告。",
                $"武力征服｜轄下據點 {victory.Held}/{rules.conquestNodes}、聲望 {victory.Reputation}/{rules.conquestReputation}。",
                victory.Diplomatic || victory.Conquest ? "已有勝利條件達成。" : ""
            };
        }

        internal static string RelationExplanation(MapFaction faction, int shift)
        {
            string reason = FactionRelations.Data.entries?.FirstOrDefault(entry => entry?.id == faction.id)?.reason;
            reason ??= faction.stance switch
            {
                "friendly" => "兩家素有往來。",
                "hostile" => "兩家的地盤或利益曾起衝突。",
                _ => "兩家尚未立約，各自觀望。"
            };
            string change = shift switch
            {
                > 0 => "近來往來轉暖。",
                < 0 => "近來嫌隙加深。",
                _ => ""
            };
            string advice = faction.Outlaw ? "攻占寨堡會激化敵意。" : "送禮可改善關係，威嚇失敗會生嫌隙。";
            return reason + change + advice;
        }
    }
}
