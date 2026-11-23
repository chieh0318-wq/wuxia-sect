using System;
using System.Collections.Generic;
using System.Linq;

namespace Wuxia
{
    [Serializable]
    internal sealed class BattleSkill
    {
        public string id, label, element, status;
        public int qi, restoreQi;
        public float power;
        public bool all, telegraph;
    }

    [Serializable]
    internal sealed class BattleStatus
    {
        public string id, label;
        public int turns, speed, qi;
        public float dot, attack = 1, incoming = 1, defense = 1;
        public bool skip, seal, evade;
    }

    [Serializable]
    internal sealed class BattleEnemy
    {
        public string id, element;
        public int phaseAt;
        public BattleAiRule[] moves;
    }

    [Serializable]
    internal sealed class BattleAiRule
    {
        public string skill;
        public int weight, phase, every, opening;
    }

    internal sealed class BattleEvent
    {
        public string Text, Float, Cue;
        public BattleCombatant Actor, Target;
        public bool Hit, Critical, Recovery;
        public BattleSkill Skill;
    }

    internal sealed class BattleModel
    {
        private readonly List<BattleCombatant> party, enemies;
        private readonly BattleCatalog catalog;
        private readonly Random random;
        private readonly Func<bool> hasMedicine;
        private readonly Func<(string name, int hp, int qi)> useMedicine;
        private readonly List<BattleCombatant> order = new();
        private readonly List<BattleEvent> events = new();
        private readonly int intentSeed;
        private int cursor;

        public int Round { get; private set; }
        public BattleCombatant Actor => Finished ? null : order[cursor];
        public bool Won => !enemies.Any(u => u.Alive);
        public bool Finished => Won || !party.Any(u => u.Alive);
        public bool NeedsCommand => Actor is { isPlayer: true } actor &&
            !Effects(actor).Any(status => status.skip) && actor.hp > TurnDamage(actor);

        public BattleModel(List<BattleCombatant> party, List<BattleCombatant> enemies, BattleCatalog catalog,
            int seed, Func<bool> hasMedicine, Func<(string name, int hp, int qi)> useMedicine)
        {
            this.party = party;
            this.enemies = enemies;
            this.catalog = catalog;
            this.hasMedicine = hasMedicine;
            this.useMedicine = useMedicine;
            random = new Random(seed);
            intentSeed = seed;
            StartRound();
        }

        /// <summary>
        /// The move behind a command id. v1124 C14: the acting fighter's own martial arts replace the generic
        /// 「skill」/「ultimate」 slots (e.g. 回雪劍 for 許雁), so labels, costs and effects follow each character.
        /// </summary>
        public BattleSkill Skill(string id)
        {
            var actor = Finished || cursor >= order.Count ? null : order[cursor];
            if (actor?.moves != null && actor.moves.TryGetValue(id, out var own) && catalog.Skills.TryGetValue(own, out var move))
                return move;
            return catalog.Skills[id];
        }
        private IEnumerable<BattleStatus> Effects(BattleCombatant unit) =>
            unit.statuses.Keys.Select(id => catalog.Statuses[id]);

        public BattleStatus Status(string id) => catalog.Statuses[id];

        public IEnumerable<BattleCombatant> RemainingTurns =>
            order.Skip(cursor).Where(unit => unit.Alive);

        public (BattleSkill move, BattleCombatant target, string unavailable) PreviewIntent(BattleCombatant unit)
        {
            if (TurnDamage(unit) >= unit.hp) return (null, null, "內傷將發作");
            if (Effects(unit).Any(status => status.skip)) return (null, null, "無法出招");
            if (unit.intent != null)
            {
                if (!unit.intent.all && !unit.intentTarget.Alive)
                    return (unit.intent, unit.intentTarget, "目標已倒下");
                if (unit.intent.qi > 0 && Effects(unit).Any(status => status.seal))
                    return (unit.intent, unit.intentTarget, "已被封招");
                return (unit.intent, unit.intentTarget, null);
            }
            if (Finished) return (null, null, "戰鬥結束");

            int round = RemainingTurns.Contains(unit) ? Round : Round + 1;
            var (move, target) = PlanEnemyMove(unit, round, TurnQi(unit), EnemyPhase(unit));
            return (move, target, null);
        }

        public string Reason(string id, BattleCombatant target)
        {
            if (!NeedsCommand) return "尚未輪到我方";
            var actor = Actor;
            if (id == "item")
                return !hasMedicine() ? "沒有靈藥" :
                    actor.hp == actor.maxHp && actor.qi == actor.maxQi ? "氣血內力已滿" : "";
            var move = Skill(id);
            if (move.power > 0 && (target == null || !target.Alive)) return "沒有目標";
            if (move.qi > 0 && Effects(actor).Any(status => status.seal)) return "已被封招";
            if (TurnQi(actor) < move.qi) return "內力不足";
            if (id == "interrupt" && target.intent == null) return "目標未蓄勢";
            return "";
        }

        public (string id, BattleCombatant target) AutoCommand(BattleCombatant target)
        {
            var threatened = enemies.FirstOrDefault(u => u.Alive && u.intent != null && u.intentRound < Round);
            if (threatened != null && Reason("interrupt", threatened) == "") return ("interrupt", threatened);
            if (threatened != null && (threatened.intent.all || threatened.intentTarget == Actor))
                return ("defend", target);
            if (Actor.hp * 3 < Actor.maxHp && Reason("item", target) == "") return ("item", target);
            if (enemies.Count(u => u.Alive) > 1 && Reason("ultimate", target) == "") return ("ultimate", target);
            if (Reason("skill", target) == "") return ("skill", target);
            return ("attack", target);
        }

        public IReadOnlyList<BattleEvent> Step(string command, BattleCombatant target)
        {
            events.Clear();
            if (Finished || (NeedsCommand && (command == null || Reason(command, target) != ""))) return events;

            var actor = Actor;
            var expiring = actor.statuses.Keys.ToArray();
            int damage = TurnDamage(actor);
            if (damage > 0)
            {
                actor.hp = Math.Max(0, actor.hp - damage);
                Emit($"{actor.name} 受持續傷害 {damage}。", target: actor, floating: $"-{damage}");
            }
            actor.qi = TurnQi(actor);

            bool skip = Effects(actor).Any(status => status.skip);
            if (actor.Alive)
            {
                if (skip)
                {
                    actor.intent = null;
                    Emit($"{actor.name} 無法出招！");
                }
                else if (!actor.isPlayer)
                    EnemyTurn(actor);
                else if (command == "item")
                {
                    var medicine = useMedicine();
                    int hp = actor.hp;
                    actor.hp = Math.Min(actor.maxHp, actor.hp + medicine.hp);
                    actor.qi = Math.Min(actor.maxQi, actor.qi + medicine.qi);
                    Emit($"{actor.name} 使用{medicine.name}，氣血 +{actor.hp - hp}。", target: actor,
                        floating: $"+{actor.hp - hp}", cue: "complete", recovery: true);
                }
                else
                    Use(actor, Skill(command), target);
            }

            string refreshed = actor.isPlayer && actor.Alive && !skip && command != null && command != "item"
                && Skill(command).power == 0 ? Skill(command).status : null;
            foreach (var id in expiring)
            {
                if (id == refreshed || !actor.statuses.TryGetValue(id, out int turns)) continue;
                if (turns <= 1) actor.statuses.Remove(id);
                else actor.statuses[id] = turns - 1;
            }

            while (++cursor < order.Count && !order[cursor].Alive) { }
            if (cursor == order.Count && !Finished) StartRound();
            return events;
        }

        private void StartRound()
        {
            Round++;
            cursor = 0;
            order.Clear();
            order.AddRange(party.Concat(enemies).Where(u => u.Alive)
                .OrderByDescending(u => u.speed + Effects(u).Sum(status => status.speed))
                .ThenByDescending(u => u.isPlayer));
        }

        private int TurnQi(BattleCombatant actor) =>
            Math.Clamp(actor.qi + Effects(actor).Sum(status => status.qi), 0, actor.maxQi);

        private int TurnDamage(BattleCombatant actor)
        {
            float rate = Effects(actor).Sum(status => status.dot);
            return rate > 0 ? Math.Max(1, (int)Math.Round(actor.maxHp * rate)) : 0;
        }

        private void EnemyTurn(BattleCombatant actor)
        {
            if (actor.intent != null)
            {
                var move = actor.intent;
                var target = actor.intentTarget;
                actor.intent = null;
                if (!move.all && !target.Alive)
                    Emit($"{actor.name} 的{move.label}失去目標。");
                else if (move.qi > 0 && Effects(actor).Any(status => status.seal))
                    Emit($"{actor.name} 被封招，{move.label}無法施展。");
                else
                    Use(actor, move, target);
                return;
            }

            int phase = EnemyPhase(actor);
            if (actor.phase != phase)
            {
                actor.phase = phase;
                Emit($"{actor.name} 轉入第二階段，攻勢更烈！");
            }

            var (skill, victim) = PlanEnemyMove(actor, Round, actor.qi, phase);
            if (skill.telegraph)
            {
                actor.intentRound = Round;
                actor.intent = skill;
                actor.intentTarget = victim;
                Emit($"{actor.name} 蓄勢準備{skill.label}，目標：{(skill.all ? "全體" : victim.name)}！");
            }
            else
                Use(actor, skill, victim);
        }

        private int EnemyPhase(BattleCombatant actor)
        {
            int threshold = catalog.Enemies[actor.enemyProfile].phaseAt;
            // A forecast uses the same phase check as the eventual enemy turn.
            return actor.phase == 1 && threshold > 0 && actor.hp * 100 <= actor.maxHp * threshold
                ? 2 : actor.phase;
        }

        private (BattleSkill move, BattleCombatant target) PlanEnemyMove(
            BattleCombatant actor, int round, int qi, int phase)
        {
            // Keep each enemy's decision roll stable for the round, while eligibility and
            // target preference still use the current state when its turn actually arrives.
            var decision = new Random(unchecked(intentSeed ^ (round * 73856093) ^
                ((enemies.IndexOf(actor) + 1) * 19349663)));
            var profile = catalog.Enemies[actor.enemyProfile];
            bool sealedMoves = Effects(actor).Any(status => status.seal);
            var rules = profile.moves.Where(rule =>
                (rule.phase == 0 || rule.phase == phase) &&
                (rule.every == 0 || round % rule.every == 0) &&
                (rule.opening == 0 || rule.opening == round) &&
                Skill(rule.skill).qi <= qi &&
                (!sealedMoves || Skill(rule.skill).qi == 0)).ToArray();
            var opening = rules.FirstOrDefault(rule => rule.opening == round);
            var moveId = opening?.skill ?? "attack";
            if (opening == null && rules.Length > 0)
            {
                int roll = decision.Next(rules.Sum(rule => rule.weight));
                foreach (var rule in rules)
                {
                    roll -= rule.weight;
                    if (roll >= 0) continue;
                    moveId = rule.skill;
                    break;
                }
            }

            var alive = party.Where(u => u.Alive).ToArray();
            double preference = decision.NextDouble();
            var victim = preference < .5 ? alive.OrderBy(u => (double)u.hp / u.maxHp).First()
                : preference < .8 ? alive.OrderByDescending(u => u.attack).First()
                : alive[decision.Next(alive.Length)];
            return (Skill(moveId), victim);
        }

        private void Use(BattleCombatant actor, BattleSkill move, BattleCombatant target)
        {
            actor.qi = Math.Clamp(actor.qi - move.qi + move.restoreQi, 0, actor.maxQi);
            if (move.power == 0)
            {
                Apply(actor, move.status);
                Emit($"{actor.name} 施展{move.label}。", target: actor, floating: move.label, cue: "page", skill: move);
                return;
            }

            var targets = move.all ? (actor.isPlayer ? enemies : party).Where(u => u.Alive).ToArray()
                : new[] { target };
            foreach (var victim in targets)
            {
                var evasion = Effects(victim).FirstOrDefault(status => status.evade);
                if (evasion != null || (!actor.isPlayer && !move.telegraph && random.NextDouble() < victim.dodge))
                {
                    if (evasion != null) victim.statuses.Remove(evasion.id);
                    Emit($"{victim.name} 閃過了{actor.name}的{move.label}！", actor, victim, "閃避！");
                    continue;
                }

                float attack = Effects(actor).Aggregate((float)actor.attack, (value, effect) => value * effect.attack);
                float defense = Effects(victim).Aggregate((float)victim.defense, (value, effect) => value * effect.defense);
                int counter = Counter(move.element, victim.element);
                double multiplier = counter > 0 ? 1.25 : counter < 0 ? .8 : 1;
                float reduction = Effects(victim).Aggregate(1f, (value, effect) => value * effect.incoming);
                double roll = random.NextDouble();
                bool critical = roll >= .92;
                int amount = Math.Max(1, (int)Math.Round(move.power * attack * attack / (attack + defense) *
                    multiplier * reduction * (.95 + roll * .1) * (critical ? 1.5 : 1)));
                victim.hp = Math.Max(0, victim.hp - amount);
                string element = counter > 0 ? " 剋制！" : counter < 0 ? " 被剋" : "";
                string damageType = critical ? "暴擊" : "傷害";
                Emit($"{actor.name}・{move.label} → {victim.name} {amount} {damageType}。{element}",
                    actor, victim, $"-{amount}{element}", hit: true, skill: move, critical: critical);
                if (!victim.Alive) victim.intent = null;
                if (!victim.Alive) continue;
                if (move.id == "interrupt" && victim.intent != null)
                {
                    Emit($"{victim.name} 的{victim.intent.label}被打斷！");
                    victim.intent = null;
                }
                Apply(victim, move.status);
            }
        }

        private void Apply(BattleCombatant target, string id)
        {
            if (!string.IsNullOrEmpty(id)) target.statuses[id] = catalog.Statuses[id].turns;
        }

        private static int Counter(string attack, string defense)
        {
            const string cycle = "金木土水火";
            if (string.IsNullOrEmpty(attack) || string.IsNullOrEmpty(defense)) return 0;
            int a = cycle.IndexOf(attack, StringComparison.Ordinal);
            int d = cycle.IndexOf(defense, StringComparison.Ordinal);
            if (a < 0 || d < 0) return 0;
            return (a + 1) % 5 == d ? 1 : (d + 1) % 5 == a ? -1 : 0;
        }

        private void Emit(string text, BattleCombatant actor = null, BattleCombatant target = null,
            string floating = null, string cue = null, bool hit = false, BattleSkill skill = null,
            bool critical = false, bool recovery = false) =>
            events.Add(new BattleEvent
            {
                Text = text, Actor = actor, Target = target, Float = floating, Cue = cue,
                Hit = hit, Skill = skill, Critical = critical, Recovery = recovery
            });
    }
}
