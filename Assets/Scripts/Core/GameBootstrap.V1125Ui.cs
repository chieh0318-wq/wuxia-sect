using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    /// <summary>v1125 PA (介面與操作): back navigation, sect / hero pages that can act, and the monthly flow.</summary>
    public sealed partial class GameBootstrap
    {
        /// <summary>
        /// v1125 #16: right-click / Esc in the courtyard. Order: registered back steps (UiBack), modal pages
        /// (their own 返回 / 關閉), the portrait talk, the old speech box, build placement, the building card,
        /// the 門派事務 sub-menu. Esc with nothing open still brings up the system menu.
        /// </summary>
        private bool HandleBack()
        {
            bool escape = Input.GetKeyDown(KeyCode.Escape);
            if (!UiBack.BackPressed) return false;
            if (UiBack.TryBack()) return true;
            UiBack.Consume();
            if (ui.ModalOpen)
            {
                ui.ModalBack();
                return true;
            }
            if (ui.TalkOpen)
            {
                ui.CloseTalk();
                return true;
            }
            if (ui.DialogueOpen)
            {
                ui.CloseDialogue();
                return true;
            }
            if (choosingBuilding)
            {
                CancelBuild();
                return true;
            }
            if (ui.BuildingOpen)
            {
                ui.CloseBuilding();
                return true;
            }
            if (ui.CommandsBack()) return true;
            if (escape)
            {
                OpenSystemMenu();
                return true;
            }
            return false;
        }

        // ── #13 / #18: the hero and our own people in the character lists ──────────────────────────────────────

        private const string HeroCharacterId = "hero";

        /// <summary>The protagonist as a roster entry: 代掌門, between the elders and the senior disciples.</summary>
        private StoryCharacter HeroCharacter()
        {
            var hero = state.Protagonist;
            return new StoryCharacter
            {
                id = HeroCharacterId, name = hero.name, gender = hero.gender, age = hero.age, title = "代掌門",
                faction = state.Data.Start.sectName, portrait = PortraitService.IdOrDefault(hero.portrait, hero.lookId),
                rank = hero.lines?.Length > 0 ? RankSystem.Martial(hero.lines) : null,
                cap = hero.lines?.Length > 0 ? RankSystem.Martial(hero.lines, true) : null,
                lines = hero.lines, attrs = hero.attrs,
                // v1126 #18: the hero's own arts are listed like anyone else's signature skills.
                signatureSkills = (hero.arts ?? Array.Empty<string>()).Select(MartialArts.Find).Where(art => art != null)
                    .Select(art => art.name).ToArray(),
                personality = "天資聰穎，從前不愛舞刀弄劍，讀書雜學卻樣樣上心；如今代掌門戶，凡事親力親為。",
                background = $"{state.Data.Start.sectName}門下排行第三，師父遠行後受同門推舉暫代掌門。"
            };
        }

        /// <summary>Disciples recruited in play (not in the seed list) also appear in 本門 / 師門.</summary>
        private IEnumerable<StoryCharacter> RecruitCharacters()
        {
            var seeds = new HashSet<string>(state.Data.Disciples.Select(seed => seed.id));
            return state.Disciples.Where(member => member != null && !seeds.Contains(member.id)).Select(member => new StoryCharacter
            {
                id = member.id, name = member.name, gender = member.gender, age = member.age, title = member.title,
                faction = state.Data.Start.sectName, portrait = PortraitService.IdOrDefault(member.portrait, member.id),
                rank = member.rank ?? (member.lines?.Length > 0 ? RankSystem.Martial(member.lines) : null),
                cap = member.cap ?? (member.lines?.Length > 0 ? RankSystem.Martial(member.lines, true) : null),
                lines = member.lines, attrs = member.attrs, personality = member.personality ?? member.trait,
                background = member.background, desc = member.blurb
            });
        }

        /// <summary>Our own people: hero, master, uncle, the childhood friend and every disciple now on the mountain.</summary>
        private bool OwnPerson(StoryCharacter person) =>
            person.id == HeroCharacterId || person.id is "master" or "uncle_schemer" || person.id == state.FriendId ||
            state.Disciples.Any(member => member.id == person.id);

        /// <summary>師父、師叔、代掌門 (hero), then the senior disciples in seniority order, the friend, new recruits.</summary>
        private int SectOrder(StoryCharacter person, int order)
        {
            if (person.id == "master") return 0;
            if (person.id == "uncle_schemer") return 1;
            if (person.id == HeroCharacterId) return 2;
            int seed = Array.FindIndex(state.Data.Disciples, entry => entry.id == person.id);
            if (seed >= 0) return 10 + seed;
            if (person.id == state.FriendId) return 100;
            int member = state.Disciples.FindIndex(entry => entry.id == person.id);
            if (member >= 0) return 200 + member;
            return 1000 + order;
        }

        /// <summary>Known life skills, best first, e.g. 「醫術 B　鍛造 C」 (top <paramref name="count"/>).</summary>
        private static string LifeSkillSummary(SkillLine[] lines, int count)
        {
            var known = (lines ?? Array.Empty<SkillLine>()).Where(line => line != null && !string.IsNullOrEmpty(line.id) &&
                    !RankSystem.IsCombat(line.id) && RankSystem.Index(line.rank) >= 0)
                .OrderByDescending(line => RankSystem.Index(line.rank)).Take(count)
                .Select(line => (RankSystem.Categories.FirstOrDefault(category => category.id == line.id)?.name ?? line.id) +
                    " " + line.rank).ToArray();
            return known.Length == 0 ? "生活技藝未習" : string.Join("　", known);
        }

        // ── #17: who is away from the mountain ──────────────────────────────────────────────────────────────

        private bool StoryFlag(string id) => state.Story.flags?.Any(flag => flag?.id == id && flag.value > 0) == true;

        /// <summary>
        /// Why someone cannot be talked to / taught right now, or null when they are here. The master is travelling
        /// and the uncle in seclusion until the story brings them back (flags master_returned / uncle_returned).
        /// </summary>
        private string AbsenceReason(string id)
        {
            if (state.IsDispatched(id == HeroCharacterId ? GameState.HeroId : id)) return "（外派未返）";
            if (id == "master") return StoryFlag("master_returned") ? null : "（雲遊未歸）";
            if (id == "uncle_schemer") return StoryFlag("uncle_returned") ? null : "（閉關後山）";
            if (id == state.FriendId) return StoryFlag("friend_arrived") || state.Story.flags == null || FriendHasArrived() ? null : "（尚未上山）";
            var member = state.Disciples.FirstOrDefault(entry => entry.id == id);
            if (member == null) return state.Data.Disciples.Any(seed => seed.id == id) ? "（不在門中）" : null;
            if (member.injury > 0) return "（受傷臥床）";
            if (StoryFlag("away_" + id)) return "（外出歷練）";
            if (StoryFlag("realm_" + id)) return "（秘境未返）";
            return null;
        }

        /// <summary>The 軍師 friend joins during the opening story (v1124 B9); once the hero is 代掌門 they are here.</summary>
        private bool FriendHasArrived() => state.Protagonist.title == "代掌門" || StoryFlag("friend_arrived") ||
            state.Story.relations?.Any(entry => entry?.id == state.FriendId) == true;

        private static readonly string[] FriendLines =
        {
            // v1126 #6/#7: spoken lines never use the hero's (random) name; a plainer, older register.
            "帳我又對了一遍。銀兩尚可支撐三月，糧食卻須早作打算。",
            "洛陽那年你說要闖出一番名堂，如今真做了代掌門。莫要愁眉苦臉，有我在。",
            "江湖上的風聲，我都記著。哪家是敵、哪家是友，問我便是。",
            "且慢出兵。人手、糧草、時機，三樣缺一不可。",
            "家裡又捎了信來，洛陽城裡近來不太平。有什麼要打聽的，儘管開口。"
        };

        private void TalkWithFriend(StoryCharacter friend)
        {
            string line = FriendLines[UnityEngine.Random.Range(0, FriendLines.Length)].Replace("{name}", state.Protagonist.name);
            ui.ShowTalk(new PortraitLine(friend.name, $"{QuestBoardData.Data.guestRole}・軍師", friend.portrait ?? friend.id, friend.gender, line));
        }

        // ── #5 / #9: disciple page actions ──────────────────────────────────────────────────────────────────

        private string DiscipleTitle(DiscipleData disciple) =>
            state.Story.delegateId == disciple.id ? $"{disciple.title}・總管" : disciple.title;

        /// <summary>Extra buttons on a disciple's page: talk, assign to a hall, arrange training, back to the ledger.</summary>
        private void DiscipleActions(DiscipleData disciple)
        {
            string away = AbsenceReason(disciple.id);
            ui.AddModalAction(away == null ? "談話" : "談話" + away, away == null ? () =>
            {
                ui.CloseModal(force: true);
                Speak(disciple);
            } : null, first: true);
            var halls = PersonHalls().ToArray();
            ui.AddModalAction(halls.Length == 0 ? "指派堂口（尚無堂口）" : "指派堂口　›",
                halls.Length == 0 || away != null ? null : () => ShowHallAssignment(disciple, halls), first: true);
            var train = state.Data.Actions.FirstOrDefault(action => action.id == "train");
            bool mayTrain = train != null && CanUseAp(train) && away == null && state.Story.delegateId != disciple.id;
            ui.AddModalAction(train == null ? "安排修練" : $"安排修練　行動力 {train.apCost}", mayTrain ? () =>
            {
                ui.CloseModal(force: true);
                ChooseTraining(train, new[] { disciple.id });
            } : null, first: true);
            if (ledgerOpen)
                ui.AddModalAction("‹ 返回政務", ShowSect, first: true);
            ui.FitModalButtons(1300);
        }

        private IEnumerable<(HallData hall, HallFunction function)> PersonHalls() =>
            HallCatalog.Data.halls.Where(hall => state.HallLevel(hall.id) > 0)
                .SelectMany(hall => (hall.functions ?? Array.Empty<HallFunction>()).Where(function => function.target == "person")
                    .Select(function => (hall, function)));

        private void ShowHallAssignment(DiscipleData disciple, (HallData hall, HallFunction function)[] halls)
        {
            ui.ShowModal($"指派{disciple.name}", new[] { "選一處堂口差事，交給此人去辦。" },
                halls.Select(entry => ($"{entry.hall.name}・{entry.function.name}" +
                    (entry.function.ap > 0 ? $"　行動力 {entry.function.ap}" : ""), (Action)(() =>
                {
                    ui.CloseModal(force: true);
                    string result = state.RunHallFunction(entry.hall, entry.function, disciple.id, disciple.id);
                    ui.Refresh(state);
                    ShowCommands();
                    ui.Message(string.IsNullOrEmpty(result) ? $"{disciple.name}領命前往{entry.hall.name}。" : result);
                }))).Append(("‹ 返回", (Action)(() => ShowDisciple(disciple)))).ToArray());
        }

        // ── #9: 門派政務 hooks ──────────────────────────────────────────────────────────────────────────────

        private bool ledgerOpen;

        private void ShowSectLedgerPage()
        {
            ledgerOpen = true;
            int silver = 0, food = 0, reputation = 0, upkeep = 0;
            foreach (var building in state.Buildings.Where(entry => entry != null))
            {
                silver += building.income.silver;
                food += building.income.food;
                reputation += building.income.reputation;
                upkeep += building.upkeep;
            }
            silver = Mathf.RoundToInt(silver * state.ProductionMultiplier("commerce"));
            food = Mathf.RoundToInt(food * state.ProductionMultiplier("farming"));
            int meals = state.Disciples.Count * state.Data.Start.foodPerDisciple;
            int netSilver = silver - upkeep, netFood = food - meals;
            string delegateName = state.Disciples.FirstOrDefault(member => member.id == state.Story.delegateId)?.name;
            ui.ShowSectLedger(state, new SectUI.LedgerHooks
            {
                incomeLine = $"每月收入（預估）　銀兩 +{silver}　糧食 +{food}　聲望 +{reputation}",
                netLine = $"每月支出　維護 銀 −{upkeep}　口糧 糧 −{meals}　｜　淨額 銀 {netSilver:+0;−0;0}・糧 {netFood:+0;−0;0}",
                apLine = $"行動力 {state.ActionPoints}／{state.ApMax}\n{state.ApDescription.Replace("代掌門", "總管")}" +
                    (delegateName != null ? $"\n總管：{delegateName}" : ""),
                openBuilding = building =>
                {
                    ui.CloseModal(force: true);
                    ShowBuilding(building);
                },
                openDisciple = member => ShowDisciple(member),
                status = member => DiscipleStatus(member),
                life = member => LifeSkillSummary(member.lines, 3),
                openHalls = () =>
                {
                    ui.CloseModal(force: true);
                    OpenHalls();
                },
                openRoster = () =>
                {
                    ui.CloseModal(force: true);
                    ShowSectRoster();
                }
            });
        }

        private string DiscipleStatus(DiscipleData member)
        {
            var parts = new List<string>();
            if (state.Story.delegateId == member.id) parts.Add("總管（不練武、不計生產）");
            string away = AbsenceReason(member.id);
            if (away != null) parts.Add(away.Trim('（', '）'));
            return string.Join("・", parts);
        }

        // ── #10: the hero page acts ─────────────────────────────────────────────────────────────────────────

        /// <summary>Buttons on the hero page: change gear, switch 主修, train, and manuals / medicine.</summary>
        private void HeroActions()
        {
            var hero = state.Protagonist;
            int stored = state.Story.armoury?.Count(item => item != null) ?? 0;
            ui.AddModalAction(stored > 0 ? $"換裝備　›\n裝備庫 {stored} 件" : "換裝備\n裝備庫空", stored > 0 ? ShowArmoury : null, first: true);
            var combat = (hero.lines ?? Array.Empty<SkillLine>()).Where(line => line != null && RankSystem.IsCombat(line.id) &&
                RankSystem.Index(line.rank) >= 0).ToArray();
            string majorName = EffectText.LineName(RankSystem.Major(hero.lines, hero.major)?.id ?? hero.major);
            ui.AddModalAction($"調整主修\n目前：{majorName}", combat.Length > 1 ? ShowMajorChoice : null, first: true);
            var train = state.Data.Actions.FirstOrDefault(action => action.id == "train");
            ui.AddModalAction(train == null ? "修練" : train.apCost > 0 ? $"修練\n行動力 {train.apCost}" : "修練",
                train != null && CanUseAp(train) ? () =>
                {
                    ui.CloseModal(force: true);
                    ChooseTraining(train, new[] { GameState.HeroId });
                } : null, first: true);
            int pills = LegacySystem.GetInventory(state.Story, "item_jinchuang") + LegacySystem.GetInventory(state.Story, "item_jiuzhuan");
            int arts = hero.arts?.Length ?? 0;
            ui.AddModalAction($"秘笈與丹藥　›\n秘笈 {arts}・丹藥 {pills}", ShowManuals, first: true);
            ui.FitModalButtons(1300);
        }

        private void ShowArmoury()
        {
            var stored = (state.Story.armoury ?? Array.Empty<GearItem>()).Where(item => item != null).ToArray();
            var lines = GameState.GearSlots.Select(slot =>
            {
                var worn = state.Equipped(slot);
                return $"{slot}　{(worn == null ? "未裝備" : $"【{worn.grade}】{worn.name}（{worn.Stats}）")}";
            }).ToList();
            lines.Insert(0, "目前裝備：");
            lines.Add("點下方裝備即換上，換下的收入裝備庫。");
            var buttons = stored.Take(8).Select(item => ($"【{item.grade}】{item.name}\n{item.slot}・{item.Stats}", (Action)(() =>
            {
                Equip(item);
                ShowArmoury();
            }))).ToList();
            buttons.Add(("‹ 返回主角頁", ShowProtagonist));
            ui.ShowModal("換裝備", lines, buttons);
            ui.SizeModal(1300, 680);
            ui.FitModalButtons(1300);
        }

        /// <summary>Swaps a stored item in; whatever was worn in that slot goes back to the armoury.</summary>
        private void Equip(GearItem item)
        {
            var hero = state.Protagonist;
            var worn = state.Equipped(item.slot);
            var armoury = (state.Story.armoury ?? Array.Empty<GearItem>()).ToList();
            armoury.Remove(item);
            if (worn != null) armoury.Add(worn);
            state.Story.armoury = armoury.ToArray();
            hero.gear = (hero.gear ?? Array.Empty<GearItem>()).Where(entry => entry != null && entry.slot != item.slot)
                .Append(item).ToArray();
            ui.Message($"換上【{item.grade}】{item.name}（{item.slot}・{item.Stats}）" + (worn != null ? $"，{worn.name}收入裝備庫。" : "。"),
                UiKit.SlotIcon(item.slot), Array.IndexOf(GameState.GearGrades, item.grade));
        }

        private void ShowMajorChoice()
        {
            var hero = state.Protagonist;
            var combat = (hero.lines ?? Array.Empty<SkillLine>()).Where(line => line != null && RankSystem.IsCombat(line.id) &&
                RankSystem.Index(line.rank) >= 0).OrderByDescending(line => RankSystem.Index(line.rank)).ToArray();
            var buttons = combat.Take(6).Select(line => ($"{EffectText.LineName(line.id)}\n{RankSystem.FormatKnown(line.rank, line.cap)}" +
                (line.id == hero.major ? "（主修）" : ""), (Action)(() =>
            {
                hero.major = line.id;
                ui.Message($"主修改為{EffectText.LineName(line.id)}。修練這一路另有加成，「主修武學」天賦、切磋心得與上陣招式都以這一路為主。", "skill_sword");
                ShowProtagonist();
            }))).ToList();
            buttons.Add(("‹ 返回主角頁", ShowProtagonist));
            // v1126 #2: the same explanation as on the creation screen, for the line that is 主修 now.
            ui.ShowModal("調整主修", new[] { NewGameFlow.MajorExplanation(RankSystem.IsCombat(hero.major) ? hero.major : "sword"),
                "點選下方一路武學改為主修（可隨時再改）。" }, buttons);
            ui.SizeModal(1300, 680);
            ui.FitModalButtons(1300);
        }

        private void ShowManuals()
        {
            var hero = state.Protagonist;
            var lines = new List<string> { "已參悟的秘笈與武學：" };
            foreach (string id in hero.arts ?? Array.Empty<string>())
            {
                var art = MartialArts.Find(id);
                if (art != null) lines.Add($"【{art.name}】{EffectText.LineName(art.category)}・{art.tier}　{art.desc}");
            }
            if (lines.Count == 1) lines.Add("尚未參悟秘笈。秘笈可從奇遇、秘境與師承取得，取得時立即參悟。");
            int jin = LegacySystem.GetInventory(state.Story, "item_jinchuang"), jiu = LegacySystem.GetInventory(state.Story, "item_jiuzhuan");
            lines.Add($"丹藥：金創靈藥 {jin}　九轉回元丹 {jiu}（戰鬥中亦可服用）。");
            lines.Add(hero.injury > 0 ? $"目前傷勢 {hero.injury}，服藥可療傷。" : "目前無傷。");
            var buttons = new List<(string, Action)>
            {
                ("服用金創靈藥\n傷勢 −1", jin > 0 && hero.injury > 0 ? () => UsePill("item_jinchuang", 1) : null),
                ("服用九轉回元丹\n傷勢全癒", jiu > 0 && hero.injury > 0 ? () => UsePill("item_jiuzhuan", 99) : null),
                ("‹ 返回主角頁", ShowProtagonist)
            };
            ui.ShowModal("秘笈與丹藥", lines, buttons);
            ui.SizeModal(1300, 680);
            ui.FitModalButtons(1300);
        }

        private void UsePill(string id, int heal)
        {
            LegacySystem.AddInventory(state.Story, id, -1);
            state.Protagonist.injury = Mathf.Max(0, state.Protagonist.injury - heal);
            ui.Message($"服下{EffectText.ItemName(id)}，傷勢{(state.Protagonist.injury == 0 ? "痊癒" : "減輕")}。", "skill_medicine", 1);
            ShowManuals();
        }

        // ── #20: month report scroll, the 軍師's summary and the new-month opener ─────────────────────────────

        private string lastOpenerId;

        /// <summary>
        /// Shows the memorial-scroll report; when it is rolled up the 軍師 (謝瑾 / 謝蘊) gives a one-line summary and a
        /// suggestion for next month, then one of our own people opens the new month.
        /// </summary>
        private void ShowMonthReport(string title, IList<string> lines, (int silver, int food, int reputation) before, Action read)
        {
            var net = (state.Silver - before.silver, state.Food - before.food, state.Reputation - before.reputation);
            ui.ShowMonthScroll(title, lines, net, () =>
            {
                read?.Invoke();
                if (capturing || ui.ModalOpen) return;
                PlayMonthTalks(net, null);
            });
        }

        private void PlayMonthTalks((int silver, int food, int reputation) net, Action done)
        {
            var talks = new List<PortraitLine>();
            var advisor = AdvisorLine(net);
            if (advisor.HasValue) talks.Add(advisor.Value);
            var opener = NewMonthOpener();
            if (opener.HasValue) talks.Add(opener.Value);
            ui.ShowTalk(talks, done);
        }

        /// <summary>The 軍師's summary for the month just closed, with one concrete suggestion from the numbers.</summary>
        private PortraitLine? AdvisorLine((int silver, int food, int reputation) net)
        {
            var friend = StoryCharacters.FirstOrDefault(person => person.id == state.FriendId);
            if (friend == null || AbsenceReason(friend.id) != null) return null;
            string summary = $"本月結算：銀兩 {net.silver:+0;−0;0}、糧食 {net.food:+0;−0;0}、聲望 {net.reputation:+0;−0;0}。";
            return new PortraitLine(friend.name, "軍師・本月總結", friend.portrait ?? friend.id, friend.gender,
                summary + AdvisorAdvice(net), right: true);
        }

        private string AdvisorAdvice((int silver, int food, int reputation) net)
        {
            if (state.FactionWarning is string warning) return warning;
            int meals = Math.Max(1, state.Disciples.Count * state.Data.Start.foodPerDisciple);
            int foodMonths = state.Food / meals;
            if (foodMonths < 2)
                return $"糧倉只夠撐{(foodMonths <= 0 ? "不到一" : "一")}個月，下月務必先顧糧食：開伙前先興建田舍、伙房，或拿銀兩去買糧。";
            if (net.silver < 0)
                return $"入不敷出，每月少了 {-net.silver} 兩。下月宜興建能生財的屋舍，或派善經商的弟子去打點買賣。";
            var hostile = WorldMapData.Load().Factions.Where(faction => state.IsFactionKnown(faction.id) &&
                faction.StanceWith(state.RelationShift(faction.id)) == "hostile").Select(faction => faction.Label).FirstOrDefault();
            int injured = state.Disciples.Count(member => member.injury > 0);
            if (injured > 0)
                return $"門中有 {injured} 人帶傷，下月先施藥義診，別急著出兵。" + (hostile != null ? $"{hostile}那邊也還虎視眈眈。" : "");
            if (hostile != null)
                return $"{hostile}對我們敵意未消，下月出門歷練多帶幾個好手，也可派人去外交斡旋。";
            if (state.Disciples.Count < 6)
                return "人手還太少，下月不妨開門收徒，派得出去的人多了，事情才辦得快。";
            return "門中諸事順遂。下月可以讓弟子外出歷練，或去探一探江湖傳聞裡的秘境。";
        }

        /// <summary>
        /// One of our own people speaks first in the new month. Weight = 1 + assignment rank,
        /// the childhood friend ×2; everyone present has a chance, and nobody opens two months running.
        /// </summary>
        private PortraitLine? NewMonthOpener()
        {
            var pool = new List<(string id, string name, string title, string portrait, string gender, float weight)>();
            foreach (var member in state.Disciples.Where(member => AbsenceReason(member.id) == null))
                pool.Add((member.id, member.name, DiscipleTitle(member), PortraitService.IdOrDefault(member.portrait, member.id),
                    member.gender, 1 + RankWeight(member)));
            var friend = StoryCharacters.FirstOrDefault(person => person.id == state.FriendId);
            if (friend != null && AbsenceReason(friend.id) == null)
                pool.Add((friend.id, friend.name, "軍師", friend.portrait ?? friend.id, friend.gender, 2 * (1 + 2)));
            if (pool.Count > 1) pool.RemoveAll(entry => entry.id == lastOpenerId);
            if (pool.Count == 0) return null;
            float roll = UnityEngine.Random.value * pool.Sum(entry => entry.weight);
            var pick = pool[pool.Count - 1];
            foreach (var entry in pool)
            {
                if ((roll -= entry.weight) > 0) continue;
                pick = entry;
                break;
            }
            lastOpenerId = pick.id;
            string month = state.Date;
            string text;
            if (pick.id == state.FriendId)
                text = FriendLines[UnityEngine.Random.Range(0, FriendLines.Length)].Replace("{name}", state.Protagonist.name);
            else
            {
                var member = state.Disciples.First(entry => entry.id == pick.id);
                var cast = CastDialogue.Find(member.id)?.daily?.Get(CastDialogue.Tier(CastDialogue.Score(state, member)));
                var fallback = (member.dialogue ?? Array.Empty<string>()).Concat(state.Data.GeneralLines)
                    .Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
                text = CastDialogue.Format(state, CastDialogue.Pick(cast) ?? CastDialogue.Pick(fallback) ?? "新的一月，請吩咐。", member.id);
            }
            return new PortraitLine(pick.name, $"{month}・新月　{pick.title}", pick.portrait, pick.gender, text);
        }

        private int RankWeight(DiscipleData member) => AssignmentSystem.Grade(state, member.id);
    }
}
