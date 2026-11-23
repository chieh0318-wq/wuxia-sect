using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    internal sealed class DiplomacyTalk
    {
        private readonly RectTransform overlay;
        private readonly GameState state;
        private readonly MapFaction target;
        private readonly TalkScene scene;
        private readonly DiploAction action;
        private readonly DiploEvent incident;
        private readonly Action<string> finish;
        private readonly Action<string> dispatched;
        private string dissentRole;
        private readonly string band, seat, leader, leaderTitle, envoy;
        private readonly DiscipleData envoyMember;
        private readonly TalkPersona persona;
        private readonly List<(DiscipleData member, string recommendation)> advisors = new();
        internal bool Committed { get; private set; }

        internal DiplomacyTalk(RectTransform overlay, GameState state, WorldMapData map, MapFaction target,
            TalkScene scene, DiploAction action, DiploEvent incident, string lastDissent, Action<string> dispatched, Action<string> finish)
        {
            this.overlay = overlay;
            this.state = state;
            this.target = target;
            this.scene = scene;
            this.action = action;
            this.incident = incident;
            this.dispatched = dispatched;
            this.finish = finish;
            seat = map.NodeLabel(target.seat);
            var names = (target.leader ?? target.Label).Split('　', 2);
            leader = names.Last();
            leaderTitle = names.Length > 1 ? names[0] : target.TypeName + "首領";
            int attitude = state.Attitude(target);
            band = attitude < 0 ? "cold" : attitude < 25 ? "neutral" : "warm";
            persona = DiplomacyDialogue.Data.personas.FirstOrDefault(entry => entry.factions.Contains(target.id));
            state.Eloquence(out envoy);
            envoyMember = state.Disciples.FirstOrDefault(member => member.name == envoy);
            var council = Council(lastDissent);
            if (incident == null) Play("議事", council, () => Dispatch());
            else Play("使者來報", scene.arrive, () => Play("議事", council, () => Choices(0)), true);
        }

        private IEnumerable<TalkLine> Council(string lastDissent)
        {
            var available = state.Disciples.Where(member => member.injury < 3).ToArray();
            var dissent = available
                .Where(member => CastDialogue.Find(member.id)?.role?.StartsWith("disciple_pro_", StringComparison.Ordinal) == true)
                .OrderBy(member => CastDialogue.Find(member.id).role == lastDissent)
                .ThenBy(member => CastDialogue.Score(state, member)).FirstOrDefault();
            dissentRole = CastDialogue.Find(dissent?.id)?.role;
            foreach (var id in new[] { "lin", "xu", dissent?.id })
            {
                var member = available.FirstOrDefault(candidate => candidate.id == id);
                if (member == null) continue;
                var cast = CastDialogue.Find(id);
                if (cast == null) continue;
                string tier = CastDialogue.Tier(CastDialogue.Score(state, member));
                if (tier == "frosty")
                {
                    yield return Line(id, cast.advisorVoice?.frosty?.advisor);
                    continue;
                }
                string hint = persona?.hint?.Get(cast.role);
                if (!string.IsNullOrEmpty(hint))
                    yield return Line(id, CastDialogue.CharacterSpeech(state, id, hint));
                var choices = scene.advisors.Where(a => a.role == cast.role && (a.tier == tier || a.tier == "any")
                    && (a.band == band || a.band == "any")).ToArray();
                int Priority(TalkAdvice a) => (a.tier == tier ? 2 : 0) + (a.band == band ? 1 : 0);
                if (choices.Length == 0) continue;
                int best = choices.Max(Priority);
                var advice = CastDialogue.Pick(choices.Where(a => Priority(a) == best).ToArray());
                advisors.Add((member, advice.recommends));
                yield return Line(id, advice.text);
            }
            foreach (var entry in CastDialogue.Data.interjections)
                if (available.Any(member => member.id == entry.id) && UnityEngine.Random.value < .25f)
                    yield return Line(entry.id, CastDialogue.CharacterSpeech(state, entry.id, CastDialogue.Pick(entry.lines)));
        }

        private void Dispatch()
        {
            string block = state.DiploBegin(action, target, out float chance);
            if (block != null) { finish(block); return; }
            Committed = true;
            dispatched(dissentRole);
            var arrival = DiplomacyDialogue.Data.envoy;
            var opening = scene.openingByPersona.FirstOrDefault(entry => entry.id == persona?.id)?.lines?.Get(band)
                ?? scene.opening?.Get(band);
            Play("使者抵達", new[]
            {
                Line("narrator", (envoyMember == null ? arrival?.arriveSelf : arrival?.arrive)?.Get(band)),
                Line("leader", persona?.greet?.Get(band)), Line("leader", opening)
            }, () => Choices(chance), true);
        }

        private void Choices(float baseChance)
        {
            var page = Page("答覆", true, incident == null ? "envoy" : "hero");
            int row = 0;
            foreach (var choice in scene.options)
            {
                var mods = choice.mods ?? new TalkModifiers();
                if (!mods.Visible(state, target, persona?.id, band)) continue;
                var effective = mods.Effective(state, target, persona?.id, band);
                var original = incident?.options[choice.index];
                int ap = (original?.ap ?? 0) + effective.ap, silver = (original?.silver ?? 0) + effective.silver;
                float chance = GameState.ModifiedDiploChance(baseChance, effective);
                string block = state.DiploCostBlock(ap, silver, effective.reputation);
                var costs = new List<string>();
                if (ap != 0) costs.Add($"行動力 {ap}");
                if (silver != 0) costs.Add($"銀兩 {silver} 兩");
                if (effective.reputation != 0) costs.Add($"聲望 {effective.reputation}");
                if (costs.Count == 0) costs.Add(incident == null ? "無額外花費" : "不需花費");
                if (incident == null)
                    costs.Add($"成功率 {baseChance:P0} → {chance:P0}");
                if (effective.gain != 0) costs.Add($"事成態度 {effective.gain:+0;-0}");
                if (effective.fail != 0) costs.Add($"談崩態度 {effective.fail:+0;-0}");
                if (effective.terms.tradeIncome != 0) costs.Add($"每月通商收入 {effective.terms.tradeIncome:+0;-0}");
                if (effective.terms.pactPower != 0) costs.Add($"戰力 {effective.terms.pactPower:+0;-0}%");
                if (effective.terms.tributePct != 0) costs.Add($"朝貢 {effective.terms.tributePct:+0;-0}%");
                if (effective.terms.rivalShift != 0) costs.Add($"宿敵態度 {effective.terms.rivalShift:+0;-0}");
                if (effective.terms.repOnSuccess != 0) costs.Add($"事成聲望 {effective.terms.repOnSuccess:+0;-0}");
                if (effective.terms.monthlyAttitude != 0) costs.Add($"每月態度 {effective.terms.monthlyAttitude:+0;-0}");
                var endorsements = advisors.Where(a => a.recommendation == choice.id).Select(a => a.member.name + "所言").ToArray();
                if (endorsements.Length > 0) costs.Add(string.Join("、", endorsements));
                if (block != null) costs.Add(block);
                var button = MapUi.Button(page, "", 264, 504 + row++ * 91, 1016, 80,
                    () => Choose(choice, effective, ap, silver, chance), row == 1 ? MapUi.Red : null);
                button.interactable = block == null;
                button.name = "答覆:" + choice.id;
                MapUi.Text(button.transform, choice.label, 24, 4, 966, 36, 27, MapUi.Paper);
                MapUi.Text(button.transform, "影響　" + string.Join("　・　", costs), 24, 40, 966, 32, 19, MapUi.Gold);
            }
            if (incident != null)
                MapUi.Button(page, "稍後處理", 1266, 808, 204, 50, () => finish(null));
        }

        private void Choose(TalkChoice choice, TalkModifiers mods, int ap, int silver, float chance)
        {
            string block = state.DiploPay(ap, silver, mods.reputation, "diplo_reply:" + scene.id);
            if (block != null) { finish(block); return; }
            if (incident != null) dispatched(dissentRole);
            Committed = true;
            string reaction = choice.personaReaction.FirstOrDefault(entry => entry.id == persona?.id)?.text
                ?? choice.reaction?.Get(band);
            Play("對方回應", new[] { Line(incident == null ? "envoy" : "hero", choice.line), Line("leader", reaction) },
                () => Outcome(choice, mods, chance), true);
        }

        private void Outcome(TalkChoice choice, TalkModifiers mods, float chance)
        {
            if (incident != null)
            {
                var member = state.Disciples.FirstOrDefault(d => d.injury < 3 && CastDialogue.Find(d.id)?.role == choice.aftermath?.role);
                string note = null;
                if (member != null)
                    note = CastDialogue.CharacterSpeech(state, member.id, choice.aftermath?.text);
                Play("結局", new[] { Line("narrator", incident.options[choice.index].result), Line(member?.id, note) },
                    () => finish(state.ResolveDiploEvent(choice.index, choice, prepaid: true)), true);
                return;
            }
            bool success = UnityEngine.Random.value < chance;
            var speaker = advisors.FirstOrDefault(a => !string.IsNullOrEmpty(a.recommendation) &&
                (success ? a.recommendation == choice.id : a.recommendation != choice.id)).member
                ?? state.Disciples.FirstOrDefault(member => member.id == "lin" && member.injury < 3);
            string tier = speaker == null ? null : CastDialogue.Tier(CastDialogue.Score(state, speaker));
            var reactions = CastDialogue.Find(speaker?.id)?.aftermath;
            var lines = (success ? scene.success : scene.failure) ?? Array.Empty<TalkLine>();
            Play("結局", lines.Append(Line(speaker?.id, (success ? reactions?.success : reactions?.failure)?.Get(tier))), () =>
            {
                var feedback = CastDialogue.Data.adviceFeedback;
                foreach (var advisor in advisors.Where(a => !string.IsNullOrEmpty(a.recommendation)))
                {
                    bool adopted = advisor.recommendation == choice.id;
                    int delta = success && adopted ? feedback.adoptedSuccess : !success && !adopted ? feedback.opposedFailure : 0;
                    if (delta != 0)
                        state.ApplyEffects(new[] { new GameEffect { key = "charRelation", @ref = advisor.member.id, value = delta } });
                }
                finish(state.DiploResolve(action, target, mods, chance, success));
            }, true);
        }

        private static TalkLine Line(string speaker, string text) => new() { speaker = speaker, text = text };

        private void Play(string stage, IEnumerable<TalkLine> source, Action next, bool abroad = false)
        {
            var lines = (source ?? Array.Empty<TalkLine>()).Where(line => !string.IsNullOrWhiteSpace(line.text)).ToArray();
            bool council = stage == "議事";
            if (lines.Length == 0 && incident == null && council)
                lines = new[] { Line("narrator", $"議定派使者前往{seat}。") };
            Draw(0);

            void Draw(int index)
            {
                if (index >= lines.Length) { next(); return; }
                var line = lines[index];
                var page = Page(stage, abroad, line.speaker);
                bool onRight = line.speaker is "leader" or "messenger";
                var paper = MapUi.Kit(page, "對白", onRight ? 48 : 376, 480, 1124, 310, "Lacquer");
                var plaque = MapUi.Kit(paper, "名牌", 32, -22, 340, 62, "Plaque");
                MapUi.Text(plaque, Speaker(line.speaker), 16, 4, 308, 54, 32, MapUi.Gold, role: "Names");
                MapUi.Text(paper, Format(line), 36, 68, 1052, 218, 28, MapUi.Paper, TextAnchor.UpperLeft, "Narration");
                string label = index + 1 < lines.Length ? "繼續" : stage == "結局" ? "收卷" :
                    council && incident == null ? "遣使" : "繼續";
                var advance = MapUi.Button(page, label, 1266, 808, 204, 50, () => Draw(index + 1), MapUi.Red, 24);
                advance.name = "對話繼續";
                var pageTurn = paper.gameObject.AddComponent<Button>();
                pageTurn.transition = Selectable.Transition.None;
                pageTurn.onClick.AddListener(() => advance.onClick.Invoke());
                if (!Committed)
                    MapUi.Button(page, "再議", 1046, 808, 204, 50, () => finish(null));
                if (council && incident == null)
                    MapUi.Text(page, $"遣使花費　行動力 {action.ap}　銀兩 {action.silver} 兩　聲望 {action.reputation}",
                        264, 804, 756, 56, 21, MapUi.Paper);
            }
        }

        private RectTransform Page(string stage, bool abroad, string speaker)
        {
            MapUi.Clear(overlay);
            overlay.gameObject.SetActive(true);
            overlay.SetAsLastSibling();
            var page = MapUi.Docket(overlay, stage, 26, 18, 1548, 864, false);
            var backdrop = MapUi.Rect(page, "殿內", 18, 78, 1512, 766).gameObject.AddComponent<RawImage>();
            var venue = Resources.Load<Texture2D>("DiplomacyVenues/" + Venue(abroad));
            backdrop.texture = venue;
            if (venue != null)
            {
                float crop = 1512f * venue.height / (766f * venue.width);
                backdrop.uvRect = crop <= 1 ? new Rect((1 - crop) / 2, 0, crop, 1)
                    : new Rect(0, (1 - 1 / crop) / 2, 1, 1 / crop);
            }
            backdrop.color = new Color(.86f, .83f, .78f);
            backdrop.raycastTarget = false;
            var head = MapUi.Kit(page, "刊首", 18, 10, 1512, 68, "Wood");
            MapUi.Text(head, (action?.name ?? incident.title) + "　・　" + stage, 24, 0, 770, 68, 36, MapUi.Paper);
            MapUi.Text(head, $"{state.SectLabel}　／　{target.Label}", 800, 0, 684, 68, 24, MapUi.Gold, TextAnchor.MiddleRight);
            var member = state.Disciples.FirstOrDefault(d => d.id == speaker);
            var left = member ?? (abroad && speaker != "hero" ? envoyMember : null);
            var hero = state.Protagonist;
            string portrait = left == null ? PortraitService.IdOrDefault(hero.portrait, hero.lookId)
                : PortraitService.IdOrDefault(left.portrait, left.id);
            Card(58, left == null ? hero.name : left.title + "　" + left.name,
                left == null ? state.SectLabel + "掌門" : abroad && member == null ? state.SectLabel + "使者" : state.SectLabel,
                PortraitService.Resolve(portrait, left?.gender ?? hero.gender));
            if (abroad)
            {
                bool messenger = speaker == "messenger" || (incident != null && speaker != "leader");
                Card(1196, messenger ? Speaker("messenger") : leader,
                    messenger ? "來使" : target.Label + "　" + leaderTitle,
                    PortraitService.Resolve(messenger ? MessengerPortrait() : target.PortraitId));
                int attitude = state.Attitude(target);
                MapUi.Text(page, $"態度　{Diplomacy.AttitudeLabel(attitude)} {attitude:+0;-0;0}", 1196, 440, 292, 36,
                    21, MapUi.Paper, TextAnchor.MiddleCenter);
                MapUi.Panel(page, "態度槽", 1220, 480, 244, 8, MapUi.Wood);
                MapUi.Panel(page, "態度", 1220, 480, 244 * (attitude + 100) / 200f, 8, Diplomacy.AttitudeColor(attitude));
            }
            else if (left != null)
                Card(1196, hero.name, state.SectLabel + "掌門",
                    PortraitService.Resolve(PortraitService.IdOrDefault(hero.portrait, hero.lookId), hero.gender));
            return page;

            void Card(float x, string name, string subtitle, Texture2D texture)
            {
                MapUi.Portrait(page, texture, x + 28, 96, 236, 264);
                var plaque = MapUi.Kit(page, name, x, 360, 292, 80, "Wood");
                MapUi.Text(plaque, name, 6, 4, 280, 40, 27, MapUi.Paper, TextAnchor.MiddleCenter, "Names");
                MapUi.Text(plaque, subtitle, 6, 43, 280, 30, 18, MapUi.Gold, TextAnchor.MiddleCenter);
            }
        }

        private string Venue(bool abroad)
        {
            if (!abroad) return "home";
            return target.id switch
            {
                "wulong" or "louguan" => "daoist",
                "shaolin" => "buddhist",
                "nongchao" or "yanbang" => "guild",
                _ => target.type switch
                {
                    "merchant" => "guild",
                    "court" => "courthouse",
                    "military" => "office",
                    "foreign" => "yurt",
                    "bandit" or "outlaw" => "stockade",
                    _ => "sword"
                }
            };
        }

        private string MessengerPortrait() => target.id == "emei" ? "npc_nun01" : Venue(true) switch
        {
            "daoist" => "npc_tao01",
            "buddhist" => "npc_monk01",
            "guild" => "npc_mer01",
            "courthouse" or "office" => "npc_off01",
            "yurt" => "npc_mer02",
            "stockade" => "npc_em05",
            _ => "npc_ym01"
        };

        private string Speaker(string id) => id switch
        {
            "leader" => leader,
            "messenger" => target.Label + "使者",
            "envoy" => envoy,
            "hero" => state.Protagonist.name,
            "narrator" => "旁白",
            _ => state.Disciples.FirstOrDefault(member => member.id == id)?.name ?? "旁白"
        };

        private string Format(TalkLine line)
        {
            string rival = Diplomacy.Rivals(target.id).Select(GameState.FindFaction).FirstOrDefault(f => f != null)?.Label ?? "宿敵";
            return CastDialogue.Format(state, Diplomacy.Fill(line.text, target), line.speaker)
                .Replace("{title}", state.SectLabel + "掌門").Replace("{faction}", target.Label)
                .Replace("{leader}", leader).Replace("{leaderTitle}", leaderTitle).Replace("{seat}", seat)
                .Replace("{envoy}", envoy).Replace("{speaker}", Speaker(line.speaker)).Replace("{rival}", rival)
                .Replace("{silver}", (action?.silver ?? incident?.options.FirstOrDefault()?.silver ?? 0).ToString());
        }
    }
}
