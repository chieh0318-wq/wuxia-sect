using System;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    [Serializable] internal sealed class PatronageTerms
    {
        public string id, label;
        public int silver, food, reputation, orthodox, troops, legitimacy;

        internal string Benefit => legitimacy > 0
            ? $"外交成功率＋{legitimacy} 個百分點、招募忠誠＋{legitimacy}。"
            : $"每次門派戰役可借兵 {troops}。";
        internal string Cost => silver > 0
            ? $"每月上繳銀兩 {silver}、糧食 {food}。"
            : $"締約被譏為賣國求榮，聲望 −{reputation}、正派關係 −{orthodox}。";
    }

    public sealed partial class GameState
    {
        internal MapFaction Patron => FindFaction(Story.patronFaction);
        internal static PatronageTerms Patronage(MapFaction faction) => faction?.Court == true
            ? Diplomacy.Data.patronage?.FirstOrDefault(terms => terms.id == faction.type)
            : null;
        internal int CampaignSupport => Patronage(Patron)?.troops ?? 0;
        internal int Legitimacy => Patronage(Patron)?.legitimacy ?? 0;

        internal string PatronBlock(MapFaction faction)
        {
            var terms = Patronage(faction);
            if (terms == null) return "此勢力不提供庇護。";
            if (!IsFactionKnown(faction.id) || Attitude(faction) < 25) return "需探明勢力且態度至少 25。";
            if (Patron != null) return "須先解除現有約書。";
            if (ActionPoints < 1) return "行動力不足。";
            return null;
        }

        internal string SeekPatron(MapFaction faction, string id)
        {
            var terms = Patronage(faction);
            string block = PatronBlock(faction);
            if (block != null) return block;
            if (!AssignmentSystem.Commit(this, new[] { id }, "diplomacy")) return "使者無法出勤。";
            if (!TrySpendAp(1, "patronage:" + faction.id)) return "行動力不足。";
            if (UnityEngine.Random.value > PatronChance(id)) return $"{faction.Label}未允所請。";
            AssignmentSystem.Credit(this, id, 2);
            return EstablishPatronage(faction, terms);
        }

        private string EstablishPatronage(MapFaction faction, PatronageTerms terms)
        {
            Story.patronFaction = faction.id;
            Reputation -= terms.reputation;
            if (terms.orthodox > 0)
                foreach (var orthodox in WorldMapData.Load().Factions.Where(f => f.type == "sect" && f.stance != "hostile"))
                    ShiftAttitude(orthodox, -terms.orthodox);
            return faction.Label + "已允立約。" + terms.Benefit + terms.Cost;
        }

        internal float PatronChance(string id) => Mathf.Clamp(.72f + .024f *
            (AssignmentSystem.Eloquence(this, id) - 15), .35f, .96f);

        internal string ReleasePatron()
        {
            var patron = Patron;
            if (patron == null) return "目前沒有約書。";
            Story.patronFaction = null;
            ShiftAttitude(patron, -10);
            return $"已解除與{patron.Label}的約書，態度 −10。";
        }

        private bool CanPayPatron(MapFaction patron, PatronageTerms terms, int silver, int food) =>
            Attitude(patron) > -25 && silver >= terms.silver && food >= terms.food;

        internal void PatronMonth(MonthlyReport report)
        {
            var patron = Patron;
            var terms = Patronage(patron);
            if (terms == null) return;
            if (!CanPayPatron(patron, terms, Silver, Food))
            {
                report.Lines.Add("敵意過深或貢賦不足，" + ReleasePatron());
                return;
            }
            if (terms.silver == 0) return;
            Silver -= terms.silver;
            Food -= terms.food;
            report.Lines.Add($"上繳{patron.Label}　銀兩 −{terms.silver}、糧食 −{terms.food}。");
        }
    }
}
