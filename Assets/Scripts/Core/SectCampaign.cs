using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    internal enum TroopKind { Shield, Armor, Bow, Horse, Disciples }
    internal enum WarFormation { Spear, Circle, Geese }

    internal sealed class WarUnit
    {
        internal string Id, Name;
        internal bool Rear, Commander;
        internal int Troops, Capacity, Health, BaseHealth, Attack, Speed, Defense, Shield, Qi = 30;
        internal TroopKind Kind;
        internal string Portrait;
        internal int MaxHealth => BaseHealth + (Kind == TroopKind.Armor ? Troops / 4 : 0);
        internal int TurnSpeed => Speed + (Kind == TroopKind.Horse ? Troops / 35 : 0);
        internal bool Alive => Health > 0;
        internal bool Ranged => Kind == TroopKind.Bow && Troops > 0;

        internal void Deploy()
        {
            Health = MaxHealth;
            Shield = Kind == TroopKind.Shield ? Troops / 5 : 0;
        }

        internal void Hit(int damage)
        {
            int absorbed = Math.Min(Shield, damage);
            Shield -= absorbed;
            damage -= absorbed;
            int screening = Math.Min(Troops / 4, damage / 2);
            Troops -= Math.Min(Troops, screening * 4);
            Health = Math.Max(0, Math.Min(MaxHealth, Health - damage + screening));
            if (!Alive) Troops = 0;
            Shield = Math.Min(Shield, Troops / 5);
        }
    }

    internal sealed class SectCampaign
    {
        internal static readonly string[] TroopNames = { "盾兵", "重甲", "弓弩", "騎兵", "門派弟子" };
        internal static readonly string[] FormationNames = { "鋒矢陣・攻擊", "方圓陣・防禦", "雁行陣・遠射" };
        internal readonly WarUnit[] Allies, Enemies;
        internal readonly bool Surprise;
        internal readonly int Prepared, Reserves;
        internal int Unassigned => Reserves - Allies.Sum(unit => unit.Troops);
        internal WarFormation Formation;
        internal int Round, Morale;
        internal bool DuelUsed;
        internal bool Won => !Enemies[0].Alive;
        internal bool Finished => Won || !Allies[0].Alive;

        internal SectCampaign(GameState state, string[] party, MapFaction foe, int strength, SectRaid raid = null, int support = 0)
        {
            Surprise = raid != null && !raid.detected;
            Prepared = Surprise ? 0 : raid?.preparation ?? 0;
            Reserves = 100 + state.Disciples.Count * 45 + (state.Story.heldNodes?.Length ?? 0) * 80 + support + (raid?.aid ?? 0);
            var catalog = new BattleCatalog();
            var roster = BattleSetup.Create(state, new BattleRequest { expeditionParty = party, enemyCount = 1 }, catalog).party;
            Allies = roster.Select((fighter, i) =>
            {
                string id = party[i];
                int command = (AssignmentSystem.Attribute(state, id, "wit") + AssignmentSystem.Attribute(state, id, "cha")) / 2;
                int cap = AssignmentSystem.RankOf(state, id).Troops * (60 + 4 * command) / 100;
                return new WarUnit
                {
                    Id = id, Name = fighter.name, Portrait = fighter.portraitId, Commander = i == 0, Rear = i >= 5,
                    Capacity = cap, Troops = Math.Min(cap, Reserves / roster.Count),
                    BaseHealth = fighter.maxHp, Attack = fighter.attack, Defense = fighter.defense, Speed = fighter.speed
                };
            }).ToArray();
            int divisions = Mathf.Clamp(1 + strength / 400, 1, 5);
            Enemies = Enumerable.Range(0, divisions).Select(i => new WarUnit
            {
                Name = i == 0 ? foe.leader ?? foe.Label : $"第{new[] { "一", "二", "三", "四", "五" }[i]}營",
                Portrait = i == 0 ? foe.PortraitId : null,
                Commander = i == 0, Rear = i >= 3, Kind = (TroopKind)(i % 4),
                Troops = strength / divisions + (i < strength % divisions ? 1 : 0), BaseHealth = i == 0 ? 155 : 105,
                Attack = 21 + Math.Min(20, foe.reputation / 5), Defense = 6, Speed = 15 + i * 2
            }).ToArray();
        }

        internal void Deploy()
        {
            foreach (var unit in Allies.Concat(Enemies)) unit.Deploy();
        }

        internal bool CanDuel => !DuelUsed && !Finished && !Allies[0].Rear && !Enemies[0].Rear && Round > 0;

        internal void ResolveDuel(bool won)
        {
            DuelUsed = true;
            Morale = won ? 8 : -8;
        }

        internal string Advance(bool rally)
        {
            Round++;
            string lastMove = "";
            var order = Allies.Select(unit => (unit, ally: true)).Concat(Enemies.Select(unit => (unit, ally: false)))
                .OrderByDescending(entry => entry.unit.TurnSpeed).ToArray();
            foreach (var (unit, ally) in order)
            {
                if (Finished) break;
                if (!unit.Alive) continue;
                var targets = (ally ? Enemies : Allies).Where(target => target.Alive).ToArray();
                bool volley = unit.Ranged && ally && Formation == WarFormation.Geese;
                var target = volley ? targets.FirstOrDefault(candidate => candidate.Rear) : null;
                target ??= unit.Ranged ? targets.OrderBy(candidate => candidate.Health).First()
                    : targets.FirstOrDefault(candidate => !candidate.Rear) ?? targets[0];
                int troops = unit.Troops / 28 + (unit.Ranged ? unit.Troops / 40 : 0);
                int cost = unit.Kind == TroopKind.Disciples ? Math.Max(3, 12 - unit.Troops / 28) : 12;
                bool charge = !rally && ally && unit.Qi >= cost;
                unit.Qi = charge ? unit.Qi - cost : Math.Min(30, unit.Qi + 6);
                int offense = unit.Attack + troops + (charge ? 10 : 0) + (volley ? 6 : 0);
                if (ally) offense += Morale + (!Surprise && Formation == WarFormation.Spear ? 7 : 0);
                int defense = target.Defense;
                if (!ally) defense += Prepared + (rally ? 5 : 0) + (!Surprise && Formation == WarFormation.Circle ? 7 : 0);
                target.Hit(Math.Max(3, offense - defense));
                lastMove = $"{unit.Name}攻向{target.Name}" + (charge ? "，以內力催發攻勢。" : "。");
            }
            return Finished ? Won ? "敵主將倒下，敵軍潰散。" : "我方主將倒下，全軍潰散。" : lastMove;
        }
    }
}
